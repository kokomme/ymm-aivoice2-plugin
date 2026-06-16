using System.Buffers.Binary;

namespace YmmAivoice2Plugin;

/// <summary>
/// WAVファイルの末尾無音を解析してトリム後の再生時間を計算する。
/// wav_processor.py の _wav_trim_duration() と同アルゴリズム。
/// </summary>
public static class WavSilenceTrimmer
{
    // 16bit PCM の場合: 32767 の約1% ≈ −40 dB
    const short DefaultThreshold16 = 328;
    const double DefaultTailMarginSec = 0.05;

    /// <summary>
    /// WAVファイルを解析し、末尾無音を除いた再生時間(秒)を返す。
    /// 解析できない場合はファイル全体の長さを返す。
    /// </summary>
    /// <param name="filePath">WAVファイルパス</param>
    /// <param name="silenceThresholdDb">無音と判断するdB閾値（負値、デフォルト-40dB）</param>
    /// <param name="tailMarginSec">無音カット後に残すマージン秒数</param>
    public static double GetTrimmedDurationSec(
        string filePath,
        double silenceThresholdDb = -40.0,
        double tailMarginSec = DefaultTailMarginSec)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            if (!TryParseWavHeader(fs, out var sampleRate, out var channels, out var bitDepth, out var dataLength))
                return FallbackDuration(fs, sampleRate, channels, bitDepth);

            var threshold16 = DbToLinear16(silenceThresholdDb);
            var data = new byte[dataLength];
            int read = fs.Read(data, 0, dataLength);

            int lastActiveSample = FindLastActiveSample(data, read, bitDepth, threshold16);

            int samplesPerFrame = channels;
            int lastActiveFrame = lastActiveSample / Math.Max(1, samplesPerFrame);
            int totalFrames = (read / (bitDepth / 8)) / Math.Max(1, channels);
            int marginFrames = (int)(sampleRate * tailMarginSec);
            int effectiveFrames = Math.Min(lastActiveFrame + marginFrames, totalFrames);

            return effectiveFrames / (double)sampleRate;
        }
        catch
        {
            return 0.0;
        }
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

        // RIFF チェック
        if (buf[0] != 'R' || buf[1] != 'I' || buf[2] != 'F' || buf[3] != 'F') return false;
        if (buf[8] != 'W' || buf[9] != 'A' || buf[10] != 'V' || buf[11] != 'E') return false;
        if (buf[12] != 'f' || buf[13] != 'm' || buf[14] != 't' || buf[15] != ' ') return false;

        channels = BinaryPrimitives.ReadInt16LittleEndian(buf[22..]);
        sampleRate = BinaryPrimitives.ReadInt32LittleEndian(buf[24..]);
        bitDepth = BinaryPrimitives.ReadInt16LittleEndian(buf[34..]);

        // fmt チャンクサイズを読んでdataチャンクを探す
        int fmtSize = BinaryPrimitives.ReadInt32LittleEndian(buf[16..]);
        // 標準44バイトヘッダ以外のケースは data チャンクを検索する
        if (fmtSize > 16)
            stream.Seek(20 + fmtSize, SeekOrigin.Begin);

        Span<byte> chunk = stackalloc byte[8];
        while (stream.Read(chunk) == 8)
        {
            int chunkSize = BinaryPrimitives.ReadInt32LittleEndian(chunk[4..]);
            if (chunk[0] == 'd' && chunk[1] == 'a' && chunk[2] == 't' && chunk[3] == 'a')
            {
                dataLength = chunkSize;
                return bitDepth is 8 or 16 && channels > 0 && sampleRate > 0;
            }
            stream.Seek(chunkSize, SeekOrigin.Current);
        }
        return false;
    }

    static int FindLastActiveSample(byte[] data, int byteCount, int bitDepth, short threshold16)
    {
        if (bitDepth == 16)
        {
            int sampleCount = byteCount / 2;
            for (int i = sampleCount - 1; i >= 0; i--)
            {
                short s = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(i * 2, 2));
                if (Math.Abs(s) > threshold16)
                    return i;
            }
            return 0;
        }

        if (bitDepth == 8)
        {
            short t8 = (short)Math.Max(1, threshold16 >> 8);
            for (int i = byteCount - 1; i >= 0; i--)
            {
                if (Math.Abs(data[i] - 128) > t8)
                    return i;
            }
            return 0;
        }

        return byteCount;
    }

    static short DbToLinear16(double db)
    {
        // dB → 16bit PCM 線形振幅 (0 dB = 32767)
        double linear = Math.Pow(10.0, db / 20.0) * 32767.0;
        return (short)Math.Clamp(linear, 1, 32767);
    }

    static double FallbackDuration(Stream stream, int sampleRate, int channels, int bitDepth)
    {
        if (sampleRate <= 0 || channels <= 0 || bitDepth <= 0) return 0.0;
        long dataBytes = stream.Length - stream.Position;
        long totalSamples = dataBytes / (bitDepth / 8);
        long totalFrames = totalSamples / channels;
        return totalFrames / (double)sampleRate;
    }
}
