using System.Text;

namespace Orientir.Core;

// Простий читач DBF (dBASE/FoxPro) з кодуванням Windows-1251.
// Достатньо для таблиць OLD.DBF та GRUPA.DBF системи "Орієнтир".
// За замовчуванням memo-поля (тип M/тип у .FPT) повертаються як порожні —
// для OLD/GRUPA вони не потрібні. Для SISTEM1.DBF (назва змагань/клуб у memo)
// є перевантаження Read(path, readMemo:true), яке дочитує текст із .FPT.
public static class Dbf
{
    public record Field(string Name, char Type, int Length);

    // Зворотно-сумісний виклик: memo не читаємо.
    public static List<Dictionary<string, string>> Read(string path) => Read(path, readMemo: false);

    public static List<Dictionary<string, string>> Read(string path, bool readMemo)
    {
        var enc = Encoding.GetEncoding(1251); // Windows-1251 (кирилиця)
        // FileShare.ReadWrite — щоб читати файл, навіть коли програма хронометражу
        // тримає його відкритим на запис (інакше "used by another process").
        byte[] bytes = ReadAllBytesShared(path);

        // Memo (.FPT) читаємо лише на запит — і лише якщо файл існує.
        FptReader? fpt = null;
        if (readMemo)
        {
            var fptPath = Path.ChangeExtension(path, ".FPT");
            if (!File.Exists(fptPath)) fptPath = Path.ChangeExtension(path, ".fpt");
            if (File.Exists(fptPath))
            {
                try { fpt = new FptReader(ReadAllBytesShared(fptPath)); }
                catch { fpt = null; } // пошкоджений .FPT не має ламати читання DBF
            }
        }

        int numRecords = BitConverter.ToInt32(bytes, 4);
        int headerSize = BitConverter.ToUInt16(bytes, 8);
        int recordSize = BitConverter.ToUInt16(bytes, 10);

        // Опис полів: з offset 32 до байта 0x0D.
        var fields = new List<Field>();
        int off = 32;
        while (bytes[off] != 0x0D)
        {
            string name = Encoding.ASCII.GetString(bytes, off, 11).TrimEnd('\0');
            char type = (char)bytes[off + 11];
            int len = bytes[off + 16];
            fields.Add(new Field(name, type, len));
            off += 32;
        }

        var rows = new List<Dictionary<string, string>>(numRecords);
        for (int i = 0; i < numRecords; i++)
        {
            int rstart = headerSize + i * recordSize;
            if (rstart >= bytes.Length) break;
            if (bytes[rstart] == 0x2A) continue; // видалений запис

            int p = rstart + 1;
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in fields)
            {
                string val;
                if (f.Type is 'M' or 'G' or 'P')
                {
                    // Memo/general/picture: у самому DBF лежить лише НОМЕР блоку у .FPT.
                    // Текст дочитуємо з .FPT (лише якщо readMemo і файл є); інакше "".
                    val = fpt is null ? "" : fpt.ReadBlock(ParseMemoBlock(bytes, p, f.Length), enc);
                }
                else
                {
                    val = enc.GetString(bytes, p, f.Length).Trim();
                }
                row[f.Name] = val;
                p += f.Length;
            }
            rows.Add(row);
        }
        return rows;
    }

    static byte[] ReadAllBytesShared(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        var bytes = new byte[fs.Length];
        int read = 0;
        while (read < bytes.Length)
        {
            int n = fs.Read(bytes, read, bytes.Length - read);
            if (n == 0) break;
            read += n;
        }
        return bytes;
    }

    // Номер memo-блоку у записі DBF. У FoxPro це або 4-байтне ціле (LE),
    // або текстове число довжиною 10 (старіші формати). 0/порожньо → 0 (нема memo).
    static int ParseMemoBlock(byte[] bytes, int offset, int length)
    {
        if (length == 4)
            return BitConverter.ToInt32(bytes, offset);
        // Текстовий номер блоку (зазвичай довжина 10).
        var s = Encoding.ASCII.GetString(bytes, offset, length).Trim();
        return int.TryParse(s, out var n) ? n : 0;
    }

    // Читач memo-файлу FoxPro (.FPT). Кожен блок: 8-байтний заголовок
    // (4 байти тип + 4 байти довжина, big-endian), далі вміст. Текстові memo
    // (тип 1) повертаємо як cp1251-рядок; усе інше / помилки → "".
    sealed class FptReader
    {
        readonly byte[] _b;
        readonly int _blockSize;

        public FptReader(byte[] bytes)
        {
            _b = bytes;
            // Розмір блоку — big-endian у байтах 6-7 заголовка FPT.
            int bs = (_b.Length > 7) ? (_b[6] << 8) | _b[7] : 0;
            _blockSize = bs > 0 ? bs : 512; // дефолт FoxPro
        }

        public string ReadBlock(int block, Encoding enc)
        {
            if (block <= 0) return "";
            long start = (long)block * _blockSize;
            if (start + 8 > _b.Length) return "";
            int len = ReadBigEndianInt((int)start + 4);
            if (len <= 0) return "";
            int dataStart = (int)start + 8;
            if (dataStart + len > _b.Length) len = _b.Length - dataStart;
            if (len <= 0) return "";
            try { return enc.GetString(_b, dataStart, len).Trim(); }
            catch { return ""; }
        }

        int ReadBigEndianInt(int off) =>
            (_b[off] << 24) | (_b[off + 1] << 16) | (_b[off + 2] << 8) | _b[off + 3];
    }
}
