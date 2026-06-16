using System.Buffers.Binary;

namespace YmmAivoice2Plugin;

public static class WavSilenceTrimmer
{
    public static double GetTrimmedDurationSec(
        string filePath,
        double silenceThresholdDb = -60.0,
        double tailMarginSec = 0.05)
        => GetTrimmedDurationSecWithDiag(filePath, silenceThresholdDb, tailMarginSec).TrimmedSec;

    public static double GetFullDurationSec(string filePath)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            if (!TryParseWavHeader(fs, out var sr, out var ch, out var bd, out var dl, out _))
                return Fallback(filePath, sr, ch, bd);
            return dl / (bd / 8.0) / Math.Max(1, ch) / sr;
        }
        catch { return 0.0; }
    }

    public static (double TrimmedSec, double FullSec, string Diag) GetTrimmedDurationSecWithDiag(
        string filePath,
        double silenceThresholdDb = -60.0,
        double tailMarginSec = 0.05)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            if (!TryParseWavHeader(fs, out var sampleRate, out var channels, out var bitDepth, out var dataLength, out var audioFmt))
            {
                var fb = Fallback(filePath, sampleRate, channels, bitDepth);
                return (fb, fb, $"ヘッダ解析失敗 sr={sampleRate} ch={channels} bit={bitDepth}");
            }

            double fullSec = dataLength / (bitDepth / 8.0) / Math.Max(1, channels) / sampleRate;

            // Stream.Read は一度に全バイト読むことを保証しないためループで読む
            var data = new byte[dataLength];
            int read = 0, n;
            while (read < dataLength && (n = fs.Read(data, read, dataLength - read)) > 0)
                read += n;

            int lastActiveSample = FindLastActiveSample(data, read, bitDepth, audioFmt, silenceThresholdDb);
            int lastActiveFrame  = lastActiveSample / Math.Max(1, channels);
            int totalFrames      = (read / (bitDepth / 8)) / Math.Max(1, channels);
            int marginFrames     = (int)(sampleRate * tailMarginSec);
            int effectiveFrames  = Math.Min(lastActiveFrame + marginFrames, totalFrames);
            double trimmedSec    = effectiveFrames / (double)sampleRate;

            string diag = $"fmt={audioFmt} sr={sampleRate} ch={channels} bit={bitDepth} " +
                          $"data={dataLength}B read={read}B " +
                          $"lastSmp={lastActiveSample}/{read / (bitDepth / 8)} " +
                          $"full={fullSec:F2}s trim={trimmedSec:F2}s";

            return (trimmedSec, fullSec, diag);
        }
        catch (Exception ex)
        {
            return (0.0, 0.0, $"例外: {ex.Message}");
        }
    }

    static int FindLastActiveSample(byte[] data, int byteCount, int bitDepth, int audioFmt, double thresholdDb)
    {
        if (bitDepth == 16 && audioFmt == 1)
        {
            short threshold = (short)Math.Clamp(Math.Pow(10.0, thresholdDb / 20.0) * 32767.0, 1, 32767);
            int sampleCount = byteCount / 2;
            for (int i = sampleCount - 1; i >= 0; i--)
                if (Math.Abs(BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(i * 2, 2))) > threshold)
                    return i;
        }
        else if (bitDepth == 8 && audioFmt == 1)
        {
            int threshold = Math.Max(1, (int)(Math.Pow(10.0, thresholdDb / 20.0) * 127.0));
            for (int i = byteCount - 1; i >= 0; i--)
                if (Math.Abs(data[i] - 128) > threshold) return i;
        }
        else if (bitDepth == 24 && audioFmt == 1)
        {
            int threshold = (int)(Math.Pow(10.0, thresholdDb / 20.0) * 8388607.0);
            int sampleCount = byteCount / 3;
            for (int i = sampleCount - 1; i >= 0; i--)
            {
                int raw = data[i*3] | (data[i*3+1] << 8) | (data[i*3+2] << 16);
                if ((raw & 0x800000) != 0) raw |= unchecked((int)0xFF000000);
                if (Math.Abs(raw) > threshold) return i;
            }
        }
        else if (bitDepth == 32)
        {
            // audioFmt=3: IEEE float  /  audioFmt=1 32bit: integer PCM
            if (audioFmt == 3)
            {
                float threshold = (float)Math.Pow(10.0, thresholdDb / 20.0);
                int sampleCount = byteCount / 4;
                for (int i = sampleCount - 1; i >= 0; i--)
                    if (Math.Abs(BitConverter.ToSingle(data, i * 4)) > threshold)
                        return i;
            }
            else
            {
                int threshold = (int)(Math.Pow(10.0, thresholdDb / 20.0) * 2147483647.0);
                int sampleCount = byteCount / 4;
                for (int i = sampleCount - 1; i >= 0; i--)
                    if (Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(i * 4, 4))) > threshold)
                        return i;
            }
        }
        return 0;
    }

    static bool TryParseWavHeader(
        Stream stream,
        out int sampleRate, out int channels, out int bitDepth, out int dataLength, out int audioFmt)
    {
        sampleRate = 0; channels = 0; bitDepth = 0; dataLength = 0; audioFmt = 0;

        Span<byte> buf = stackalloc byte[44];
        if (stream.Read(buf) < 44) return false;

        if (buf[0] != 'R' || buf[1] != 'I' || buf[2] != 'F' || buf[3] != 'F') return false;
        if (buf[8] != 'W' || buf[9] != 'A' || buf[10] != 'V' || buf[11] != 'E') return false;
        if (buf[12] != 'f' || buf[13] != 'm' || buf[14] != 't' || buf[15] != ' ') return false;

        int fmtSize = BinaryPrimitives.ReadInt32LittleEndian(buf[16..]);
        audioFmt   = BinaryPrimitives.ReadInt16LittleEndian(buf[20..]);
        channels   = BinaryPrimitives.ReadInt16LittleEndian(buf[22..]);
        sampleRate = BinaryPrimitives.ReadInt32LittleEndian(buf[24..]);
        bitDepth   = BinaryPrimitives.ReadInt16LittleEndian(buf[34..]);

        if (channels <= 0 || sampleRate <= 0 || bitDepth is not (8 or 16 or 24 or 32)) return false;
        if (audioFmt is not (1 or 3)) return false; // PCM or IEEE float のみ対応

        if (fmtSize == 16)
        {
            // 最適ケース: buf[36..43] に "data" チャンクが含まれる
            if (buf[36] == 'd' && buf[37] == 'a' && buf[38] == 't' && buf[39] == 'a')
            {
                dataLength = BinaryPrimitives.ReadInt32LittleEndian(buf[40..]);
                return dataLength > 0;
            }
            // fact 等の追加チャンクがある場合: fmt チャンクの直後（offset 36）へ戻す
            stream.Seek(36, SeekOrigin.Begin);
        }
        else
        {
            // 拡張 fmt (cbSize あり): fmt データの末尾へシーク
            stream.Seek(20 + fmtSize, SeekOrigin.Begin);
        }

        // "data" チャンクをストリームから検索
        Span<byte> chunk = stackalloc byte[8];
        while (stream.Read(chunk) == 8)
        {
            int chunkSize = BinaryPrimitives.ReadInt32LittleEndian(chunk[4..]);
            if (chunk[0] == 'd' && chunk[1] == 'a' && chunk[2] == 't' && chunk[3] == 'a')
            {
                dataLength = chunkSize;
                return dataLength > 0;
            }
            if (chunkSize < 0 || stream.Position + chunkSize > stream.Length) return false;
            stream.Seek(chunkSize, SeekOrigin.Current);
        }
        return false;
    }

    static double Fallback(string filePath, int sampleRate, int channels, int bitDepth)
    {
        if (sampleRate <= 0 || channels <= 0 || bitDepth <= 0) return 0.0;
        try
        {
            long dataBytes = Math.Max(0, new FileInfo(filePath).Length - 44);
            return dataBytes / (bitDepth / 8.0) / channels / sampleRate;
        }
        catch { return 0.0; }
    }
}
