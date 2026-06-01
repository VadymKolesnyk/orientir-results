using Orientir.Core.Models;

namespace Orientir.Core.Services;

// Читання метаданих змагання (назва, клуб, регіон, дати, гол. суддя/секретар)
// з SISTEM1.DBF системи "Орієнтир". Поля NAME_SOR/NAME_ORG — memo (.FPT),
// тож читаємо з readMemo:true. Якщо файл відсутній — повертаємо порожні метадані
// (звіт усе одно згенерується, лише без плашки).
public static class EventMetaReader
{
    public static EventMeta ReadEventMeta(EventConfig ev)
    {
        var path = FindSistem1(ev);
        if (path is null) return new EventMeta();

        var rows = Dbf.Read(path, readMemo: true);
        if (rows.Count == 0) return new EventMeta();

        var r = rows[0];
        return new EventMeta
        {
            EventTitle    = Get(r, "NAME_SOR"),
            OrgName       = Get(r, "NAME_ORG"),
            HeadJudge     = Get(r, "GLAV_SUD"),
            HeadSecretary = Get(r, "GLAV_SEK"),
            Region        = Get(r, "REGION"),
            Period        = Get(r, "PERIOD"),
        };
    }

    // SISTEM1.DBF лежить у корені теки змагання; як запас — у першій теці дня.
    static string? FindSistem1(EventConfig ev)
    {
        if (string.IsNullOrWhiteSpace(ev.BasePath) || !Directory.Exists(ev.BasePath))
            return null;

        var root = Path.Combine(ev.BasePath, "SISTEM1.DBF");
        if (File.Exists(root)) return root;

        foreach (var d in Directory.GetDirectories(ev.BasePath))
        {
            var p = Path.Combine(d, "SISTEM1.DBF");
            if (File.Exists(p)) return p;
        }
        return null;
    }

    static string Get(Dictionary<string, string> r, string k) =>
        r.TryGetValue(k, out var v) ? v.Trim() : "";
}
