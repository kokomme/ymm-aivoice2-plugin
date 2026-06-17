using System.Buffers.Binary;

namespace YmmAivoice2Plugin;

public static class WavSilenceTrimmer
{
    // thresholdDb: ピーク振幅に対する相対値 (例: -40 → ピークの1/100)
    // 50ms窓のRMSで判定するため、1サンプルのリバーブスパイクに引っかからない
    public static (double TrimmedSec, string Diag) GetTrimmedDuration(
        string filePath,
        double thresholdDb   = -40.0,
        double tailMarginSec = 0.50)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            if (!TryParseWavHeader(fs, out var sr, out var ch, out var bd, out var dl))
            {
                double fb = Fallback(filePath, sr, ch, bd);
                return (fb, $"ヘッダ解析失敗 sr={sr} ch={ch} bit={bd} fallback={fb:F2}s");
            }

            var data = new byte[dl];
            int read = 0, n;
            while (read < dl && (n = fs.Read(data, read, dl - read)) > 0)
                read += n;

            int chs = Math.Max(1, ch);
            int bps = bd / 8;
            int totalFrames = read / (bps * chs);

            long peak = FindPeak(data, read, bd);
            if (peak <= 0)
                return (totalFrames / (double)sr, $"ピーク=0 (無音ファイル)");

            long absThreshold = Math.Max(1L, (long)(peak * Math.Pow(10.0, thresholdDb / 20.0)));

            // 50ms窓のRMSで末尾から走査
            int windowFrames = Math.Max(1, (int)(sr * 0.05));
            int hopFrames    = Math.Max(1, windowFrames / 5); // 10ms刻み
            int lastFrame    = FindLastActiveFrame(data, read, bd, chs, bps, totalFrames, absThreshold, windowFrames, hopFrames);

            int marginFrames = (int)(sr * tailMarginSec);
            int effective    = Math.Min(lastFrame + marginFrames, totalFrames);
            double trimmed   = effective / (double)sr;
            double full      = totalFrames / (double)sr;

            return (trimmed,
                $"sr={sr} ch={ch} bit={bd} peak={peak} thr={absThreshold} " +
                $"full={full:F2}s trim={trimmed:F2}s (dB={thresholdDb} margin={tailMarginSec*1000:F0}ms)");
        }
        catch (Exception ex) { return (0.0, $"例外: {ex.Message}"); }
    }

    // 50msウィンドウのRMSをチェック。末尾から走査してRMS>閾値の最後の窓末端を返す
    static int FindLastActiveFrame(
        byte[] data, int byteCount, int bitDepth, int chs, int bps,
        int totalFrames, long absThreshold, int windowFrames, int hopFrames)
    {
        for (int endFrame = totalFrames; endFrame >= windowFrames; endFrame -= hopFrames)
        {
            int startFrame = endFrame - windowFrames;
            double sumSq   = 0;
            int    count   = 0;

            for (int f = startFrame; f < endFrame; f++)
            {
                for (int c = 0; c < chs; c++)
                {
                    int byteIdx = (f * chs + c) * bps;
                    if (byteIdx + bps > byteCount) continue;
                    long s = ReadSampleAbs(data, byteIdx, bitDepth);
                    sumSq += (double)s * s;
                    count++;
                }
            }

            if (count > 0 && Math.Sqrt(sumSq / count) > absThreshold)
                return endFrame;
        }
        return 0;
    }

    static long FindPeak(byte[] data, int byteCount, int bitDepth)
    {
        int bps = bitDepth / 8;
        long peak = 0;
        for (int i = 0; i + bps <= byteCount; i += bps)
        {
            long v = ReadSampleAbs(data, i, bitDepth);
            if (v > peak) peak = v;
        }
        return peak;
    }

    static long ReadSampleAbs(byte[] data, int byteIdx, int bitDepth) => bitDepth switch
    {
        16 => Math.Abs((long)BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(byteIdx, 2))),
        8  => Math.Abs(data[byteIdx] - 128),
        24 => Math.Abs(ReadInt24(data, byteIdx)),
        32 => (long)(Math.Abs(BitConverter.ToSingle(data, byteIdx)) * 1_000_000),
        _  => 0
    };

    static long ReadInt24(byte[] data, int i)
    {
        int raw = data[i] | (data[i+1] << 8) | (data[i+2] << 16);
        if ((raw & 0x800000) != 0) raw |= unchecked((int)0xFF000000);
        return Math.Abs((long)raw);
    }

    static bool TryParseWavHeader(Stream s, out int sr, out int ch, out int bd, out int dl)
    {
        sr = 0; ch = 0; bd = 0; dl = 0;
        Span<byte> buf = stackalloc byte[44];
        if (s.Read(buf) < 44) return false;
        if (buf[0]!='R'||buf[1]!='I'||buf[2]!='F'||buf[3]!='F') return false;
        if (buf[8]!='W'||buf[9]!='A'||buf[10]!='V'||buf[11]!='E') return false;
        if (buf[12]!='f'||buf[13]!='m'||buf[14]!='t'||buf[15]!=' ') return false;
        int fmtSize = BinaryPrimitives.ReadInt32LittleEndian(buf[16..]);
        ch = BinaryPrimitives.ReadInt16LittleEndian(buf[22..]);
        sr = BinaryPrimitives.ReadInt32LittleEndian(buf[24..]);
        bd = BinaryPrimitives.ReadInt16LittleEndian(buf[34..]);
        if (ch <= 0 || sr <= 0 || bd is not (8 or 16 or 24 or 32)) return false;
        if (fmtSize == 16 && buf[36]=='d'&&buf[37]=='a'&&buf[38]=='t'&&buf[39]=='a')
        {
            dl = BinaryPrimitives.ReadInt32LittleEndian(buf[40..]);
            return dl > 0;
        }
        s.Seek(fmtSize == 16 ? 36 : 20 + fmtSize, SeekOrigin.Begin);
        Span<byte> chunk = stackalloc byte[8];
        while (s.Read(chunk) == 8)
        {
            int sz = BinaryPrimitives.ReadInt32LittleEndian(chunk[4..]);
            if (chunk[0]=='d'&&chunk[1]=='a'&&chunk[2]=='t'&&chunk[3]=='a') { dl = sz; return dl > 0; }
            if (sz < 0 || s.Position + sz > s.Length) return false;
            s.Seek(sz, SeekOrigin.Current);
        }
        return false;
    }

    static double Fallback(string path, int sr, int ch, int bd)
    {
        if (sr <= 0 || ch <= 0 || bd <= 0) return 0.0;
        try { return Math.Max(0, new FileInfo(path).Length - 44) / (bd / 8.0) / ch / sr; }
        catch { return 0.0; }
    }
}
