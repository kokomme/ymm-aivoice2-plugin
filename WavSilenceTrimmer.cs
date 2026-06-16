using System.Buffers.Binary;

namespace YmmAivoice2Plugin;

public static class WavSilenceTrimmer
{
    /// <summary>
    /// WAVファイルを解析し、末尾無音を除いた再生時間(秒)を返す。
    /// 解析できない場合はファイル全体の長さを返す。
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

            // RMSウィンドウ方式で末尾無音を検出（20msウィンドウ）
            double thresholdLinear = Math.Pow(10.0, silenceThresholdDb / 20.0);
            int bytesPerSample = bitDepth / 8;
            int windowSamples = Math.Max(1, (int)(sampleRate * 0.02)); // 20ms
            int windowBytes = windowSamples * channels * bytesPerSample;

            int lastActiveEnd = 0; // 音があった最後の位置（バイト単位）

            for (int offset = 0; offset + windowBytes <= read; offset += windowBytes)
            {
                double rms = CalcRms(data, offset, windowBytes, bitDepth);
                if (rms > thresholdLinear)
                    lastActiveEnd = offset + windowBytes;
            }

            int marginBytes = (int)(sampleRate * tailMarginSec) * channels * bytesPerSample;
            int effectiveBytes = Math.Min(lastActiveEnd + marginBytes, read);
            int totalSamplesPerChannel = effectiveBytes / bytesPerSample / Math.Max(1, channels);
            return totalSamplesPerChannel / (double)sampleRate;
        }
        catch
        {
            return GetFallbackDuration(filePath, 0, 0, 0);
        }
    }

    static double CalcRms(byte[] data, int offset, int length, int bitDepth)
    {
        double sum = 0;
        int count = 0;

        if (bitDepth == 16)
        {
            for (int i = offset; i + 1 < offset + length && i + 1 < data.Length; i += 2)
            {
                double s = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(i, 2)) / 32768.0;
                sum += s * s;
                count++;
            }
        }
        else if (bitDepth == 24)
        {
            for (int i = offset; i + 2 < offset + length && i + 2 < data.Length; i += 3)
            {
                int raw = data[i] | (data[i + 1] << 8) | (data[i + 2] << 16);
                if ((raw & 0x800000) != 0) raw |= unchecked((int)0xFF000000); // 符号拡張
                double s = raw / 8388608.0;
                sum += s * s;
                count++;
            }
        }
        else if (bitDepth == 32)
        {
            // 32bit float PCM
            for (int i = offset; i + 3 < offset + length && i + 3 < data.Length; i += 4)
            {
                double s = BitConverter.ToSingle(data, i);
                sum += s * s;
                count++;
            }
        }

        return count == 0 ? 0.0 : Math.Sqrt(sum / count);
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

        if (channels <= 0 || sampleRate <= 0 || bitDepth is not (16 or 24 or 32)) return false;

        if (fmtSize == 16)
        {
            // 標準44バイトWAV: 44バイト読み込み済みで buf[36..43] が "data" チャンク
            if (buf[36] == 'd' && buf[37] == 'a' && buf[38] == 't' && buf[39] == 'a')
            {
                dataLength = BinaryPrimitives.ReadInt32LittleEndian(buf[40..]);
                // stream は既に位置 44 (PCMデータ先頭) にある
                return dataLength > 0;
            }
            // "data" が 36 バイト目にない場合はストリームを検索
        }
        else
        {
            // 拡張 fmt チャンク: data チャンクの前に他のチャンクがある可能性
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
            stream.Seek(chunkSize, SeekOrigin.Current);
        }
        return false;
    }

    static double GetFallbackDuration(string filePath, int sampleRate, int channels, int bitDepth)
    {
        if (sampleRate <= 0 || channels <= 0 || bitDepth <= 0) return 0.0;
        try
        {
            long fileSize = new FileInfo(filePath).Length;
            // WAVヘッダを除いたデータサイズから推定（最低44バイト）
            long dataBytes = Math.Max(0, fileSize - 44);
            long totalFrames = dataBytes / (bitDepth / 8) / channels;
            return totalFrames / (double)sampleRate;
        }
        catch { return 0.0; }
    }
}
