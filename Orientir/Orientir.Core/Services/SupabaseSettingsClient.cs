using System.Net.Http.Headers;
using System.Text.Json;
using Orientir.Core.Models;

namespace Orientir.Core.Services;

// Читання налаштувань змагання з Supabase (сервер → локально). Використовує
// service-role-ключ (обходить RLS на читання). НЕ тягне результати/секрети —
// лише метадані змагання + конфіг колонок + підписи днів.
public class SupabaseSettingsClient
{
    private readonly HttpClient _http;
    private readonly AppSettings _settings;

    public SupabaseSettingsClient(HttpClient http, AppSettings settings)
    {
        _http = http;
        _settings = settings;
    }

    public async Task<RemoteEventSettings?> GetEventSettingsAsync(string slug, CancellationToken ct = default)
    {
        var baseUrl = _settings.SupabaseUrl.TrimEnd('/');
        var evUrl = $"{baseUrl}/rest/v1/events?id=eq.{Uri.EscapeDataString(slug)}" +
                    "&select=id,title,subtitle,standings,points,display_config,days_count";

        var evRows = await GetJsonAsync(evUrl, ct);
        if (evRows.ValueKind != JsonValueKind.Array || evRows.GetArrayLength() == 0)
            return null; // змагання немає на сервері

        var ev = evRows[0];
        var result = new RemoteEventSettings
        {
            Slug        = GetString(ev, "id") ?? slug,
            Title       = GetString(ev, "title"),
            Subtitle    = GetString(ev, "subtitle"),
            Standings   = GetBool(ev, "standings"),
            Points      = GetBool(ev, "points"),
            DisplayConfigJson = GetRawJson(ev, "display_config"),
        };

        // Підписи днів (необов'язково — лишаємо локальні, якщо запит не вдався).
        try
        {
            var daysUrl = $"{baseUrl}/rest/v1/event_days?event=eq.{Uri.EscapeDataString(slug)}&select=day,label,ord";
            var dayRows = await GetJsonAsync(daysUrl, ct);
            if (dayRows.ValueKind == JsonValueKind.Array)
                foreach (var d in dayRows.EnumerateArray())
                {
                    var day = GetInt(d, "day");
                    var label = GetString(d, "label");
                    if (day is int n && !string.IsNullOrWhiteSpace(label))
                        result.DayLabels[n] = label!;
                }
        }
        catch { /* підписи днів необов'язкові */ }

        return result;
    }

    private async Task<JsonElement> GetJsonAsync(string url, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("apikey", _settings.ServiceRoleKey);
        req.Headers.Add("Authorization", "Bearer " + _settings.ServiceRoleKey);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new Exception($"Supabase {(int)resp.StatusCode}: {body}");

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }

    private static string? GetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool GetBool(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    private static int? GetInt(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : null;

    // jsonb-колонка повертається як вкладений об'єкт — серіалізуємо назад у рядок.
    private static string? GetRawJson(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.Object or JsonValueKind.Array
            ? v.GetRawText() : null;
}

// DTO налаштувань змагання з сервера (без BasePath/секретів/результатів).
public class RemoteEventSettings
{
    public string Slug { get; set; } = "";
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public bool Standings { get; set; }
    public bool Points { get; set; }
    public string? DisplayConfigJson { get; set; }
    public Dictionary<int, string> DayLabels { get; set; } = new();

    // Прапорці розділення DSQ живуть усередині display_config — дістаємо їх звідти.
    public bool SeparateDsqLg => DisplayConfig.Parse(DisplayConfigJson)?.SeparateDsqLg ?? false;
    public bool SeparateDsqSm => DisplayConfig.Parse(DisplayConfigJson)?.SeparateDsqSm ?? false;
}
