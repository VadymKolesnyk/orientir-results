using System.Globalization;
using Orientir.Core.Models;

namespace Orientir.Core.Services;

// Читання DBF (OLD.DBF + GRUPA.DBF) системи "Орієнтир" та підрахунок
// місць/балів. Логіка перенесена з pusher/Program.cs без змін.
public static class ResultsReader
{
    // ---------------------------------------------------------------------
    //  GRUPA.DBF → таблиця groups
    // ---------------------------------------------------------------------
    public static List<Dictionary<string, object?>> ReadGroups(EventConfig ev, DayConfig d)
    {
        var path = Path.Combine(ev.BasePath, d.Folder, "GRUPA.DBF");
        var rows = Dbf.Read(path);
        var list = new List<Dictionary<string, object?>>();
        int ord = 0;
        foreach (var r in rows)
        {
            var name = Get(r, "NAME");
            if (string.IsNullOrWhiteSpace(name)) continue;
            list.Add(new()
            {
                ["event"]       = ev.Slug,
                ["name"]        = name,
                ["day"]         = d.Day,
                ["distance_km"] = ParseNum(Get(r, "DLINA")),
                ["controls"]    = ParseInt(Get(r, "KP")),
                ["ord"]         = ord++,
            });
        }
        return list;
    }

    // ---------------------------------------------------------------------
    //  OLD.DBF → таблиця results
    // ---------------------------------------------------------------------
    public static List<Dictionary<string, object?>> ReadResults(EventConfig ev, DayConfig d)
    {
        var path = Path.Combine(ev.BasePath, d.Folder, "OLD.DBF");
        var rows = Dbf.Read(path);
        var list = new List<Dictionary<string, object?>>();

        foreach (var r in rows)
        {
            var bib = ParseInt(Get(r, "NOMER"));
            var grp = Get(r, "GRUP");
            var fam = Get(r, "FAM");
            if (bib is null || string.IsNullOrWhiteSpace(grp) || string.IsNullOrWhiteSpace(fam))
                continue;

            var start  = Get(r, "S_1");
            var finish = Get(r, "F_1");
            var result = Get(r, "R_1");
            var udal   = Get(r, "U_DAL"); // причина зняття: "MP" (mispunch), "DNS"…
            // Увага: DSO (О/С/К/Т…) — це класифікація спорттовариства, а НЕ зняття.
            // 75+ нормальних призерів мають DSO заповнене, тож у статусі його не чіпаємо.

            // Місце (M_1 з DBF) НЕ читаємо — рахуємо самі за часом нижче (AssignPlaces),
            // щоб результат з'являвся одразу після фінішу, не чекаючи оператора.
            // «Зараз» — локальний час ПК публікатора (= місцевий час змагань).
            string status = ComputeStatus(start, finish, result, udal, DateTime.Now.TimeOfDay);

            list.Add(new()
            {
                ["event"]          = ev.Slug,
                ["bib"]            = bib,
                ["day"]            = d.Day,
                ["grp"]            = grp,
                ["rk"]             = (int?)null, // призначається в AssignPlaces
                ["full_name"]      = fam,
                ["team"]           = Get(r, "KOM1"),   // область — як "Team" на бланку
                ["club"]           = Get(r, "KOM2"),   // клуб
                ["region"]         = Get(r, "KOM1"),
                ["birth"]          = Get(r, "GR"),
                ["qual"]           = Get(r, "KVAL"),
                // Причина зняття для відображення ("MP", "DNS"…); інакше null.
                ["reason"]         = status == "dsq" ? NullIfEmpty(udal) : null,
                ["start_time"]     = NullIfEmpty(start),
                ["finish_time"]    = NullIfEmpty(finish),
                // Час показуємо для будь-кого, у кого він уже є в DBF — зокрема для
                // знятих (DSQ). На місця/бали це не впливає: AssignPlaces/AssignPoints
                // фільтрують за статусом, а не за наявністю часу.
                ["result_time"]    = NullIfEmpty(result),
                ["result_seconds"] = TimeToSeconds(result),
                ["points"]         = (decimal?)null, // призначається в AssignPoints
                ["status"]         = status,
                ["updated_at"]     = DateTime.UtcNow.ToString("o"),
            });
        }

        AssignPlaces(list);
        AssignPoints(list);
        return list;
    }

    // ---------------------------------------------------------------------
    //  Підрахунок місць усередині кожної групи за чистим часом.
    //  Рахуємо самі (а не з M_1 у DBF), щоб місце з'являлося одразу після
    //  фінішу. Однаковий час → спільне місце (стандарт орієнтування):
    //  два по 0:25:00 → обидва 3-тє, наступний — 5-те.
    //  Знятих/незавершених (status != finished*) у залік не беремо.
    // ---------------------------------------------------------------------
    public static void AssignPlaces(List<Dictionary<string, object?>> rows)
    {
        var byGroup = rows
            .Where(r => (string)r["status"]! is "finished" or "finished_pending"
                        && r["result_seconds"] is int)
            .GroupBy(r => (string)r["grp"]!);

        foreach (var g in byGroup)
        {
            var sorted = g.OrderBy(r => (int)r["result_seconds"]!).ToList();
            int place = 0, seen = 0, prevSec = int.MinValue;
            foreach (var r in sorted)
            {
                seen++;
                int sec = (int)r["result_seconds"]!;
                if (sec != prevSec) { place = seen; prevSec = sec; } // ex aequo → те саме місце
                r["rk"]     = place;
                r["status"] = "finished"; // місце є → знімаємо "(обробка)"
            }
        }
    }

    // ---------------------------------------------------------------------
    //  Підрахунок балів усередині кожної групи за формулою:
    //      бали = 100 * (2 - час_учасника / час_переможця)
    //  Час_переможця — найкращий (найменший) час фінішера в групі (той, хто
    //  отримав 1-ше місце). Переможець завжди дістає рівно 100 балів.
    //  Бали обрізаємо знизу нулем: дуже повільні (час > 2× переможця) → 0.
    //  Рахується щотакту від актуального час_переможця, тож зміна лідера
    //  автоматично перераховує бали ВСІЄЇ групи — окремої логіки не треба.
    //  Нефінішери (зняті/біжать/не старт.) балів не отримують (points = null).
    // ---------------------------------------------------------------------
    public static void AssignPoints(List<Dictionary<string, object?>> rows)
    {
        var byGroup = rows
            .Where(r => (string)r["status"]! == "finished" && r["result_seconds"] is int)
            .GroupBy(r => (string)r["grp"]!);

        foreach (var g in byGroup)
        {
            int winnerSec = g.Min(r => (int)r["result_seconds"]!);
            if (winnerSec <= 0) continue; // нульовий час переможця — ділити не можна

            foreach (var r in g)
            {
                int sec = (int)r["result_seconds"]!;
                double pts = 100.0 * (2.0 - (double)sec / winnerSec);
                if (pts < 0) pts = 0; // бали не можуть бути від'ємними (дуже повільні → 0)
                r["points"] = (decimal)Math.Round(pts, 2);
            }
        }
    }

    // Обчислення статусу з полів OLD.DBF.
    // Спирається на ЯВНІ ознаки, а не на "M_1=0":
    //   U_DAL заповнене (MP/DNS/…)     → dsq (явна причина зняття)
    //   фініш + чистий час є           → finished (місце дорахує AssignPlaces)
    //   фініш є, але часу ще нема       → finished_pending (рідкісний проміжний стан)
    //   немає фінішу/результату:
    //     S_1 (запланований старт) ще НЕ настав (start > now) → dns («не старт.»)
    //     S_1 уже настав (start ≤ now)                        → running («на дистанції»)
    //     S_1 порожнє                                         → dns
    // S_1 — ЗАПЛАНОВАНИЙ час старту (відомий заздалегідь для всіх), тож саму
    // наявність S_1 не можна вважати «вже біжить» — порівнюємо з now.
    // Місце (M_1) тут НЕ враховуємо — pusher рахує ранги сам у AssignPlaces.
    public static string ComputeStatus(string start, string finish, string result, string udal, TimeSpan now)
    {
        bool hasFinish = !string.IsNullOrWhiteSpace(finish);
        bool hasResult = !string.IsNullOrWhiteSpace(result);
        bool removed   = !string.IsNullOrWhiteSpace(udal);

        if (removed) return "dsq";
        if (hasFinish) return hasResult ? "finished" : "finished_pending";

        // Біжить лише той, чий запланований старт уже настав. Інакше — ще не стартував.
        var startTod = ParseTimeOfDay(start);
        if (startTod is TimeSpan s && s <= now) return "running";
        return "dns";
    }

    // "9:22" / "09:22:00" / "9:22:00.0" → TimeSpan доби; інакше null.
    static TimeSpan? ParseTimeOfDay(string s)
    {
        int? sec = TimeToSeconds(s);
        return sec is int n ? TimeSpan.FromSeconds(n) : null;
    }

    // ---------------------------------------------------------------------
    //  Хелпери
    // ---------------------------------------------------------------------
    static string Get(Dictionary<string, string> r, string k) =>
        r.TryGetValue(k, out var v) ? v : "";

    static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    static int? ParseInt(string s) =>
        int.TryParse(s?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;

    static double? ParseNum(string s) =>
        double.TryParse(s?.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;

    // "00:18:32" або "1:03:33" → секунди. Дробову частину секунд відкидаємо.
    static int? TimeToSeconds(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var parts = s.Trim().Split(':');
        if (parts.Length is < 2 or > 3) return null;
        try
        {
            int h = parts.Length == 3 ? int.Parse(parts[0]) : 0;
            int m = int.Parse(parts[parts.Length == 3 ? 1 : 0]);
            int sec = (int)Math.Floor(double.Parse(parts[^1], CultureInfo.InvariantCulture));
            return h * 3600 + m * 60 + sec;
        }
        catch { return null; }
    }
}
