using System.Buffers.Binary;

namespace YmmAivoice2Plugin;

public static class WavSilenceTrimmer
{
    // WAVファイルの全長を返す（末尾無音解析なし）
    public static (double FullSec, string Diag) GetFullDuration(string filePath)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            if (!TryParseWavHeader(fs, out var sr, out var ch, out var bd, out var dl))
                return (Fallback(filePath), $"ヘッダ解析失敗 fallback使用");

            int bps   = bd / 8;
            int chs   = Math.Max(1, ch);
            double sec = dl / (double)(sr * bps * chs);
            return (sec, $"sr={sr} ch={ch} bit={bd} full={sec:F2}s");
        }
        catch (Exception ex) { return (0.0, $"例外: {ex.Message}"); }
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

    static double Fallback(string path)
    {
        try { return Math.Max(0, new FileInfo(path).Length - 44) / 2.0 / 48000.0; }
        catch { return 0.0; }
    }
}
