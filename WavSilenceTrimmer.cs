using System.Buffers.Binary;

namespace YmmAivoice2Plugin;

public static class WavSilenceTrimmer
{
    // thresholdDb はピーク振幅に対する相対値 (例: -40 → ピークの1/100)
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

            long peak = FindPeak(data, read, bd);
            if (peak <= 0)
                return (0.0, "ピーク=0 (無音ファイル)");

            // 相対閾値: peak × 10^(dB/20)
            long absThreshold = Math.Max(1L, (long)(peak * Math.Pow(10.0, thresholdDb / 20.0)));

            int lastSmp   = FindLastActive(data, read, bd, absThreshold);
            int lastFrame = lastSmp / Math.Max(1, ch);
            int total     = (read / (bd / 8)) / Math.Max(1, ch);
            int margin    = (int)(sr * tailMarginSec);
            int effective = Math.Min(lastFrame + margin, total);
            double trimmed = effective / (double)sr;
            double full    = total    / (double)sr;

            return (trimmed,
                $"sr={sr} ch={ch} bit={bd} peak={peak} absThreshold={absThreshold} " +
                $"full={full:F2}s trim={trimmed:F2}s (thr={thresholdDb}dB margin={tailMarginSec*1000:F0}ms)");
        }
        catch (Exception ex) { return (0.0, $"例外: {ex.Message}"); }
    }

    static long FindPeak(byte[] data, int byteCount, int bitDepth)
    {
        long peak = 0;
        if (bitDepth == 16)
        {
            for (int i = 0; i < byteCount / 2; i++)
            {
                long v = Math.Abs((long)BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(i * 2, 2)));
                if (v > peak) peak = v;
            }
        }
        else if (bitDepth == 8)
        {
            for (int i = 0; i < byteCount; i++)
            {
                long v = Math.Abs(data[i] - 128);
                if (v > peak) peak = v;
            }
        }
        else if (bitDepth == 24)
        {
            for (int i = 0; i < byteCount / 3; i++)
            {
                int raw = data[i*3] | (data[i*3+1] << 8) | (data[i*3+2] << 16);
                if ((raw & 0x800000) != 0) raw |= unchecked((int)0xFF000000);
                long v = Math.Abs((long)raw);
                if (v > peak) peak = v;
            }
        }
        else if (bitDepth == 32)
        {
            float fpeak = 0;
            for (int i = 0; i < byteCount / 4; i++)
            {
                float v = Math.Abs(BitConverter.ToSingle(data, i * 4));
                if (v > fpeak) fpeak = v;
            }
            return (long)(fpeak * 1_000_000);
        }
        return peak;
    }

    static int FindLastActive(byte[] data, int byteCount, int bitDepth, long threshold)
    {
        if (bitDepth == 16)
        {
            for (int i = byteCount / 2 - 1; i >= 0; i--)
                if (Math.Abs((long)BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(i * 2, 2))) > threshold)
                    return i;
        }
        else if (bitDepth == 8)
        {
            for (int i = byteCount - 1; i >= 0; i--)
                if (Math.Abs(data[i] - 128) > threshold) return i;
        }
        else if (bitDepth == 24)
        {
            for (int i = byteCount / 3 - 1; i >= 0; i--)
            {
                int raw = data[i*3] | (data[i*3+1] << 8) | (data[i*3+2] << 16);
                if ((raw & 0x800000) != 0) raw |= unchecked((int)0xFF000000);
                if (Math.Abs((long)raw) > threshold) return i;
            }
        }
        else if (bitDepth == 32)
        {
            float fthr = threshold / 1_000_000f;
            for (int i = byteCount / 4 - 1; i >= 0; i--)
                if (Math.Abs(BitConverter.ToSingle(data, i * 4)) > fthr) return i;
        }
        return 0;
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
