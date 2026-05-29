using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Pusher;

// =====================================================================
//  Publisher — раз на N секунд читає OLD.DBF + GRUPA.DBF (система "Орієнтир")
//  для КОЖНОГО налаштованого змагання та дня і шле дані у Supabase (upsert).
//
//  Запуск:  dotnet run            (бойовий — шле в Supabase)
//           dotnet run -- --dry-run   (перевірка без відправки)
//  Конфіг:  appsettings.json (масив Events)
// =====================================================================

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // для cp1251
Console.OutputEncoding = Encoding.UTF8;

bool dryRun = args.Contains("--dry-run");
var cfg = Config.Load(skipKeyCheck: dryRun);

if (dryRun)
{
    foreach (var ev in cfg.Events)
    {
        var days = ev.ResolveDays();
        Console.WriteLine($"\n#### {ev.Id} — {ev.Title} ({days.Count} дн.) ####");
        foreach (var d in days)
        {
            var gs = ReadGroups(ev, d);
            var rs = ReadResults(ev, d);
            var byStatus = rs.GroupBy(r => (string)r["status"]!)
                             .ToDictionary(g => g.Key, g => g.Count());
            Console.WriteLine($"  День {d.Day} [{d.Folder}]  груп {gs.Count}, результатів {rs.Count}  " +
                              string.Join(" ", byStatus.Select(kv => $"{kv.Key}={kv.Value}")));
            var sample = rs.Where(r => (string?)r["grp"] == "Ч18")
                           .OrderBy(r => (int?)r["rk"] ?? 999).Take(3);
            foreach (var r in sample)
                Console.WriteLine($"      {r["rk"],3}  {r["full_name"],-24} " +
                                  $"event={r["event"]} t={r["result_time"]} [{r["status"]}]");
        }
    }
    return;
}

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

Console.WriteLine($"Publisher запущено. Змагань: {cfg.Events.Count}, інтервал: {cfg.IntervalSeconds}с. Ctrl+C — стоп.");

// Метадані (events/event_days/groups) шлемо один раз на (event, day) — вони
// під час змагань не змінюються. Ключ: "<eventId>:<day>"; "<eventId>" для events.
var metaSent = new HashSet<string>();

while (true)
{
    foreach (var ev in cfg.Events)
    {
        var days = ev.ResolveDays();

        // Запис самого змагання (один раз).
        if (!metaSent.Contains(ev.Id))
        {
            try
            {
                await Push(http, cfg, "events", "id", new() { new()
                {
                    ["id"]         = ev.Id,
                    ["title"]      = ev.Title,
                    ["subtitle"]   = NullIfEmpty(ev.Subtitle),
                    ["days_count"] = days.Count,
                    ["updated_at"] = DateTime.UtcNow.ToString("o"),
                }});
                await Push(http, cfg, "event_days", "event,day",
                    days.Select((d, i) => new Dictionary<string, object?>
                    {
                        ["event"] = ev.Id,
                        ["day"]   = d.Day,
                        ["label"] = NullIfEmpty(d.Label),
                        ["ord"]   = i,
                    }).ToList());
                metaSent.Add(ev.Id);
                Console.WriteLine($"[{Now()}] {ev.Id}: змагання + {days.Count} дн. зареєстровано");
                foreach (var d in days)
                    Console.WriteLine($"    🔗 {cfg.PublicBaseUrl}?event={ev.Id}&day={d.Day}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{Now()}] {ev.Id} META ПОМИЛКА: {ex.Message}");
            }
        }

        foreach (var d in days)
        {
            var key = $"{ev.Id}:{d.Day}";
            try
            {
                if (!metaSent.Contains(key))
                {
                    var groups = ReadGroups(ev, d);
                    if (groups.Count > 0)
                    {
                        await Push(http, cfg, "groups", "event,name,day", groups);
                        metaSent.Add(key);
                    }
                }

                var results = ReadResults(ev, d);
                if (results.Count > 0)
                {
                    await Push(http, cfg, "results", "event,bib,day", results);
                    int fin = results.Count(r => (string)r["status"]! is "finished" or "finished_pending");
                    int run = results.Count(r => (string)r["status"]! == "running");
                    Console.WriteLine($"[{Now()}] {ev.Id} день {d.Day}: результатів {results.Count} (фініш {fin}, біжать {run})");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{Now()}] {ev.Id} день {d.Day} ПОМИЛКА: {ex.Message}");
            }
        }
    }

    await Task.Delay(TimeSpan.FromSeconds(cfg.IntervalSeconds));
}

static string Now() => DateTime.Now.ToString("HH:mm:ss");

// ---------------------------------------------------------------------
//  GRUPA.DBF → таблиця groups
// ---------------------------------------------------------------------
static List<Dictionary<string, object?>> ReadGroups(EventConfig ev, DayConfig d)
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
            ["event"]       = ev.Id,
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
static List<Dictionary<string, object?>> ReadResults(EventConfig ev, DayConfig d)
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
        var place  = ParseInt(Get(r, "M_1")) ?? 0;
        var udal   = Get(r, "U_DAL"); // причина зняття: "MP" (mispunch), "DNS"…
        // Увага: DSO (О/С/К/Т…) — це класифікація спорттовариства, а НЕ зняття.
        // 75+ нормальних призерів мають DSO заповнене, тож у статусі його не чіпаємо.

        string status = ComputeStatus(start, finish, place, udal);
        bool isFinished = status is "finished" or "finished_pending";

        list.Add(new()
        {
            ["event"]          = ev.Id,
            ["bib"]            = bib,
            ["day"]            = d.Day,
            ["grp"]            = grp,
            ["rk"]             = place > 0 ? place : (int?)null,
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
            // Час показуємо для будь-якого фінішу (зокрема ще-не-розставленого місця).
            ["result_time"]    = isFinished ? NullIfEmpty(result) : null,
            ["result_seconds"] = isFinished ? TimeToSeconds(result) : null,
            ["status"]         = status,
            ["updated_at"]     = DateTime.UtcNow.ToString("o"),
        });
    }
    return list;
}

// Обчислення статусу з полів OLD.DBF.
// Спирається на ЯВНІ ознаки, а не на "M_1=0":
//   U_DAL заповнене (MP/DNS/…)     → dsq (явна причина зняття)
//   фініш є, місце > 0             → finished
//   фініш є, місця ще нема         → finished_pending (час є, місце рахується)
//   старт є, фінішу нема           → running
//   інакше                         → dns
// Це прибирає ризик "обмовити" фінішера як знятого, поки оператор ще не
// перерахував місця в групі під час живих змагань.
static string ComputeStatus(string start, string finish, int place, string udal)
{
    bool hasStart  = !string.IsNullOrWhiteSpace(start);
    bool hasFinish = !string.IsNullOrWhiteSpace(finish);
    bool removed   = !string.IsNullOrWhiteSpace(udal);

    if (removed) return "dsq";
    if (hasFinish) return place > 0 ? "finished" : "finished_pending";
    if (hasStart) return "running";
    return "dns";
}

// ---------------------------------------------------------------------
//  Upsert у Supabase (PostgREST). on_conflict — список PK-колонок.
// ---------------------------------------------------------------------
static async Task Push(HttpClient http, Config cfg, string table, string onConflict,
                       List<Dictionary<string, object?>> rows)
{
    if (rows.Count == 0) return;
    var url = $"{cfg.SupabaseUrl.TrimEnd('/')}/rest/v1/{table}?on_conflict={onConflict}";
    var json = JsonSerializer.Serialize(rows);

    using var req = new HttpRequestMessage(HttpMethod.Post, url);
    req.Headers.Add("apikey", cfg.ServiceRoleKey);
    req.Headers.Add("Authorization", "Bearer " + cfg.ServiceRoleKey);
    req.Headers.Add("Prefer", "resolution=merge-duplicates,return=minimal");
    req.Content = new StringContent(json, Encoding.UTF8, "application/json");

    using var resp = await http.SendAsync(req);
    if (!resp.IsSuccessStatusCode)
    {
        var body = await resp.Content.ReadAsStringAsync();
        throw new Exception($"Supabase {(int)resp.StatusCode} [{table}]: {body}");
    }
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

// ---------------------------------------------------------------------
//  Конфіг
// ---------------------------------------------------------------------
class DayConfig
{
    public int Day { get; set; } = 1;
    public string Folder { get; set; } = "";  // підтека дня: "D_1"
    public string Label { get; set; } = "";   // підпис: "30 травня"
}

class EventConfig
{
    public string Id { get; set; } = "";        // slug для URL
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string BasePath { get; set; } = "";  // тека змагання
    public List<DayConfig> Days { get; set; } = new();

    // Якщо Days не задано — автоматично знаходимо підтеки D_1..D_n з OLD.DBF.
    public List<DayConfig> ResolveDays()
    {
        if (Days.Count > 0) return Days;

        if (!Directory.Exists(BasePath))
            throw new Exception($"Тека змагання не існує: {BasePath}");

        // Дні — підтеки D_1..D_n. Тека d_0 — службова (реєстрація/попередній
        // стан), її пропускаємо: беремо лише номери >= 1.
        var rx = new Regex(@"^D_(\d+)$", RegexOptions.IgnoreCase);
        var days = Directory.GetDirectories(BasePath)
            .Select(p => Path.GetFileName(p))
            .Select(name => new { name, M = rx.Match(name) })
            .Where(x => x.M.Success && File.Exists(Path.Combine(BasePath, x.name, "OLD.DBF")))
            .Select(x => new DayConfig { Day = int.Parse(x.M.Groups[1].Value), Folder = x.name })
            .Where(d => d.Day >= 1)
            .OrderBy(d => d.Day)
            .ToList();
        if (days.Count == 0)
            throw new Exception($"У теці {BasePath} не знайдено підтек D_1..D_n з OLD.DBF");
        return days;
    }
}

class Config
{
    public string SupabaseUrl { get; set; } = "";
    public string ServiceRoleKey { get; set; } = "";
    public int IntervalSeconds { get; set; } = 10;
    public string PublicBaseUrl { get; set; } = "";
    public List<EventConfig> Events { get; set; } = new();

    public static Config Load(bool skipKeyCheck = false)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path)) path = "appsettings.json";
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var cfg = JsonSerializer.Deserialize<Config>(File.ReadAllText(path), opts)
                  ?? throw new Exception("Не вдалося прочитати appsettings.json");
        if (!skipKeyCheck && (cfg.SupabaseUrl.Contains("ВАШ") || cfg.ServiceRoleKey.Contains("ВАШ")))
            throw new Exception("Заповни SupabaseUrl та ServiceRoleKey в appsettings.json");
        if (cfg.Events.Count == 0)
            throw new Exception("Вкажи хоча б одне змагання в масиві Events в appsettings.json");
        foreach (var ev in cfg.Events)
            if (string.IsNullOrWhiteSpace(ev.Id) || string.IsNullOrWhiteSpace(ev.BasePath))
                throw new Exception("У кожного змагання мають бути заповнені Id та BasePath");
        return cfg;
    }
}
