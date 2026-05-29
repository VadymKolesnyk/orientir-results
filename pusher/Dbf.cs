using System.Text;

namespace Pusher;

// Простий читач DBF (dBASE/FoxPro) з кодуванням Windows-1251.
// Достатньо для таблиць OLD.DBF та GRUPA.DBF системи "Орієнтир".
// Memo-поля (тип M/тип у .FPT) повертаються як порожні — вони нам не потрібні.
public static class Dbf
{
    public record Field(string Name, char Type, int Length);

    public static List<Dictionary<string, string>> Read(string path)
    {
        var enc = Encoding.GetEncoding(1251); // Windows-1251 (кирилиця)
        var bytes = File.ReadAllBytes(path);

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
                string val = (f.Type is 'M' or 'G' or 'P')
                    ? "" // memo / general — пропускаємо
                    : enc.GetString(bytes, p, f.Length).Trim();
                row[f.Name] = val;
                p += f.Length;
            }
            rows.Add(row);
        }
        return rows;
    }
}
