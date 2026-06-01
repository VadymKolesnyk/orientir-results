using System.Text;
using System.Text.Json;
using Orientir.Core.Models;

namespace Orientir.Core.Services;

// Публікація у Supabase (PostgREST upsert) + один такт обробки змагань.
// Логіка перенесена з тіла циклу pusher/Program.cs.
public class SupabasePublisher
{
    private readonly HttpClient _http;
    private readonly AppSettings _settings;

    // Метадані (events/event_days/groups) шлемо один раз на (event, day) —
    // вони під час змагань не змінюються. Ключ: "<slug>:<day>"; "<slug>" для events.
    private readonly HashSet<string> _metaSent = new();

    public SupabasePublisher(HttpClient http, AppSettings settings)
    {
        _http = http;
        _settings = settings;
    }

    // Скидає кеш метаданих — наступний такт перешле events/days/groups.
    public void ResetMeta() => _metaSent.Clear();

    // Один повний такт по всіх змаганнях. log — необов'язковий приймач рядків логу.
    public async Task RunOnceAsync(IReadOnlyList<EventConfig> events, IProgress<string>? log = null,
                                   CancellationToken ct = default)
    {
        foreach (var ev in events)
        {
            ct.ThrowIfCancellationRequested();
            var days = ev.ResolveDays();

            // Запис самого змагання (один раз).
            if (!_metaSent.Contains(ev.Slug))
            {
                try
                {
                    await Push("events", "id", new() { new()
                    {
                        ["id"]         = ev.Slug,
                        ["title"]      = ev.Title,
                        ["subtitle"]   = NullIfEmpty(ev.Subtitle),
                        ["days_count"] = days.Count,
                        ["standings"]  = ev.Standings,
                        ["updated_at"] = DateTime.UtcNow.ToString("o"),
                    }}, ct);
                    await Push("event_days", "event,day",
                        days.Select((d, i) => new Dictionary<string, object?>
                        {
                            ["event"] = ev.Slug,
                            ["day"]   = d.Day,
                            ["label"] = NullIfEmpty(d.Label),
                            ["ord"]   = i,
                        }).ToList(), ct);
                    _metaSent.Add(ev.Slug);
                    log?.Report($"[{Now()}] {ev.Slug}: змагання + {days.Count} дн. зареєстровано");
                    foreach (var d in days)
                        log?.Report($"    🔗 {_settings.PublicBaseUrl}?event={ev.Slug}&day={d.Day}");
                    // Якщо ввімкнено залік («Сума») — окреме посилання на ?day=summ.
                    if (ev.Standings)
                        log?.Report($"    🔗 Сума: {_settings.PublicBaseUrl}?event={ev.Slug}&day=summ");
                }
                catch (Exception ex)
                {
                    log?.Report($"[{Now()}] {ev.Slug} META ПОМИЛКА: {ex.Message}");
                }
            }

            foreach (var d in days)
            {
                ct.ThrowIfCancellationRequested();
                var key = $"{ev.Slug}:{d.Day}";
                try
                {
                    // Параметри груп (GRUPA.DBF) шлемо для КОЖНОГО дня один раз —
                    // вони потрібні в БД для всіх днів (вкладки, КП, довжина).
                    if (!_metaSent.Contains(key))
                    {
                        var groups = ResultsReader.ReadGroups(ev, d);
                        if (groups.Count > 0)
                        {
                            await Push("groups", "event,name,day", groups, ct);
                            _metaSent.Add(key);
                        }
                    }

                    // Результати щотакту читаємо/шлемо лише для активного дня (якщо
                    // ActiveDay задано). Інші дні вже залиті — їх не чіпаємо, щоб не
                    // ганяти зайві дані кожні N секунд. Без ActiveDay — як раніше, усі.
                    if (ev.ActiveDay is int act && d.Day != act) continue;

                    var results = ResultsReader.ReadResults(ev, d);
                    if (results.Count > 0)
                    {
                        await Push("results", "event,bib,day", results, ct);
                        // учасників — усі записи; біжать — ще на дистанції (running);
                        // результатів — усі, хто вже не біжить (учасників − біжать).
                        int running  = results.Count(r => (string)r["status"]! == "running");
                        int finished = results.Count - running;
                        log?.Report($"[{Now()}] {ev.Slug} день {d.Day}: учасників {results.Count} (результатів {finished}, біжать {running})");
                    }
                }
                catch (Exception ex)
                {
                    log?.Report($"[{Now()}] {ev.Slug} день {d.Day} ПОМИЛКА: {ex.Message}");
                }
            }
        }
    }

    // ---------------------------------------------------------------------
    //  Upsert у Supabase (PostgREST). onConflict — список PK-колонок.
    // ---------------------------------------------------------------------
    private async Task Push(string table, string onConflict,
                            List<Dictionary<string, object?>> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return;
        var url = $"{_settings.SupabaseUrl.TrimEnd('/')}/rest/v1/{table}?on_conflict={onConflict}";
        var json = JsonSerializer.Serialize(rows);

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Add("apikey", _settings.ServiceRoleKey);
        req.Headers.Add("Authorization", "Bearer " + _settings.ServiceRoleKey);
        req.Headers.Add("Prefer", "resolution=merge-duplicates,return=minimal");
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new Exception($"Supabase {(int)resp.StatusCode} [{table}]: {body}");
        }
    }

    private static string Now() => DateTime.Now.ToString("HH:mm:ss");
    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
