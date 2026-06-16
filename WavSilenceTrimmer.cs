using System.Buffers.Binary;

namespace YmmAivoice2Plugin;

public static class WavSilenceTrimmer
{
    /// <summary>
    /// WAVファイルを解析し、末尾無音を除いた再生時間(秒)を返す。
    /// trae-video-helper/core/wav_processor.py の _wav_trim_duration() と同アルゴリズム。
    /// </summary>
    public static double GetTrimmedDurationSec(
        string filePath,
        double silenceThresholdDb = -40.0,
        double tailMarginSec = 0.05)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            if (!TryParseWavHeader(fs, out var sampleRate, out var channels, out var bitDepth, out var dataLength))
                return GetFallbackDuration(filePath, sampleRate, channels, bitDepth);

            var data = new byte[dataLength];
            int read = fs.Read(data, 0, dataLength);

            // Python版と同じサンプル単位スキャン（末尾から走査して最後の「音あり」サンプルを探す）
            int lastActiveSample = FindLastActiveSample(data, read, bitDepth, silenceThresholdDb);

            int lastActiveFrame = lastActiveSample / Math.Max(1, channels);
            int totalFrames     = (read / (bitDepth / 8)) / Math.Max(1, channels);
            int marginFrames    = (int)(sampleRate * tailMarginSec);
            int effectiveFrames = Math.Min(lastActiveFrame + marginFrames, totalFrames);

            return effectiveFrames / (double)sampleRate;
        }
        catch
        {
            return GetFallbackDuration(filePath, 0, 0, 0);
        }
    }

    // 末尾から走査し、閾値を超えた最後のサンプルインデックスを返す
    static int FindLastActiveSample(byte[] data, int byteCount, int bitDepth, double thresholdDb)
    {
        if (bitDepth == 16)
        {
            // Python: threshold = 328 (= 0dBfs の 1% ≈ −40dB)
            short threshold = (short)Math.Clamp(Math.Pow(10.0, thresholdDb / 20.0) * 32767.0, 1, 32767);
            int sampleCount = byteCount / 2;
            for (int i = sampleCount - 1; i >= 0; i--)
            {
                if (Math.Abs(BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(i * 2, 2))) > threshold)
                    return i;
            }
        }
        else if (bitDepth == 8)
        {
            // Python: threshold = max(1, 328 >> 8) = 1
            int threshold = Math.Max(1, (int)(Math.Pow(10.0, thresholdDb / 20.0) * 127.0));
            for (int i = byteCount - 1; i >= 0; i--)
            {
                if (Math.Abs(data[i] - 128) > threshold)
                    return i;
            }
        }
        else if (bitDepth == 24)
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
            float threshold = (float)Math.Pow(10.0, thresholdDb / 20.0);
            int sampleCount = byteCount / 4;
            for (int i = sampleCount - 1; i >= 0; i--)
            {
                if (Math.Abs(BitConverter.ToSingle(data, i * 4)) > threshold)
                    return i;
            }
        }

        return 0;
    }

    static bool TryParseWavHeader(
        Stream stream,
        out int sampleRate,
        out int channels,
        out int bitDepth,
        out int dataLength)
    {
        sampleRate = 0; channels = 0; bitDepth = 0; dataLength = 0;

        Span<byte> buf = stackalloc byte[44];
        if (stream.Read(buf) < 44) return false;

        if (buf[0] != 'R' || buf[1] != 'I' || buf[2] != 'F' || buf[3] != 'F') return false;
        if (buf[8] != 'W' || buf[9] != 'A' || buf[10] != 'V' || buf[11] != 'E') return false;
        if (buf[12] != 'f' || buf[13] != 'm' || buf[14] != 't' || buf[15] != ' ') return false;

        int fmtSize = BinaryPrimitives.ReadInt32LittleEndian(buf[16..]);
        channels   = BinaryPrimitives.ReadInt16LittleEndian(buf[22..]);
        sampleRate = BinaryPrimitives.ReadInt32LittleEndian(buf[24..]);
        bitDepth   = BinaryPrimitives.ReadInt16LittleEndian(buf[34..]);

        if (channels <= 0 || sampleRate <= 0 || bitDepth is not (8 or 16 or 24 or 32)) return false;

        if (fmtSize == 16)
        {
            // 標準44バイトWAV: buf[36..43] に "data" チャンクが含まれている
            if (buf[36] == 'd' && buf[37] == 'a' && buf[38] == 't' && buf[39] == 'a')
            {
                dataLength = BinaryPrimitives.ReadInt32LittleEndian(buf[40..]);
                return dataLength > 0; // stream は位置44（PCMデータ先頭）にある
            }
        }
        else
        {
            stream.Seek(20 + fmtSize, SeekOrigin.Begin);
        }

        // "data" チャンクをストリームから検索（拡張fmtや追加チャンクがある場合）
        Span<byte> chunk = stackalloc byte[8];
        while (stream.Read(chunk) == 8)
        {
            int chunkSize = BinaryPrimitives.ReadInt32LittleEndian(chunk[4..]);
            if (chunk[0] == 'd' && chunk[1] == 'a' && chunk[2] == 't' && chunk[3] == 'a')
            {
                dataLength = chunkSize;
                return dataLength > 0;
            }
            stream.Seek(chunkSize, SeekOrigin.Current);
        }
        return false;
    }

    public static double GetFullDurationSec(string filePath)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            if (!TryParseWavHeader(fs, out var sampleRate, out var channels, out var bitDepth, out var dataLength))
                return GetFallbackDuration(filePath, sampleRate, channels, bitDepth);
            int totalFrames = dataLength / (bitDepth / 8) / Math.Max(1, channels);
            return totalFrames / (double)sampleRate;
        }
        catch { return GetFallbackDuration(filePath, 0, 0, 0); }
    }

    static double GetFallbackDuration(string filePath, int sampleRate, int channels, int bitDepth)
    {
        if (sampleRate <= 0 || channels <= 0 || bitDepth <= 0) return 0.0;
        try
        {
            long dataBytes = Math.Max(0, new FileInfo(filePath).Length - 44);
            return dataBytes / (bitDepth / 8) / channels / (double)sampleRate;
        }
        catch { return 0.0; }
    }
}
