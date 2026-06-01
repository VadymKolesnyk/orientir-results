using Orientir.Core.Models;

namespace Orientir.Core.Services;

// Безкінечний цикл публікації: раз на N секунд проходить усі змагання.
// Зупиняється через CancellationToken (Ctrl+C у консолі, кнопка Stop у WPF).
// UI-агностичний: лог віддає через IProgress<string>.
public static class PublisherLoop
{
    public static async Task RunAsync(AppSettings settings, IReadOnlyList<EventConfig> events,
                                      IProgress<string>? log = null, CancellationToken ct = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var publisher = new SupabasePublisher(http, settings);

        log?.Report($"Publisher запущено. Змагань: {events.Count}, інтервал: {settings.IntervalSeconds}с.");

        while (!ct.IsCancellationRequested)
        {
            await publisher.RunOnceAsync(events, log, ct);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(settings.IntervalSeconds), ct);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    // Один прохід без відправки — для dry-run зведення.
    public static List<string> DryRun(IReadOnlyList<EventConfig> events)
    {
        var lines = new List<string>();
        foreach (var ev in events)
        {
            var days = ev.ResolveDays();
            lines.Add($"#### {ev.Slug} — {ev.Title} ({days.Count} дн.) ####");
            foreach (var d in days)
            {
                var gs = ResultsReader.ReadGroups(ev, d);
                var rs = ResultsReader.ReadResults(ev, d);
                var byStatus = rs.GroupBy(r => (string)r["status"]!)
                                 .ToDictionary(g => g.Key, g => g.Count());
                lines.Add($"  День {d.Day} [{d.Folder}]  груп {gs.Count}, результатів {rs.Count}  " +
                          string.Join(" ", byStatus.Select(kv => $"{kv.Key}={kv.Value}")));
                var sample = rs.Where(r => (string?)r["grp"] == "Ч18")
                               .OrderBy(r => (int?)r["rk"] ?? 999).Take(3);
                foreach (var r in sample)
                    lines.Add($"      {r["rk"],3}  {r["full_name"],-24} " +
                              $"event={r["event"]} t={r["result_time"]} бали={r["points"]} [{r["status"]}]");
            }
        }
        return lines;
    }
}
