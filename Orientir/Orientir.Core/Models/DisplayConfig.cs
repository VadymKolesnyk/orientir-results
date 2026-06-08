using System.Text.Json;
using System.Text.Json.Serialization;

namespace Orientir.Core.Models;

// Конфіг колонок таблиці результатів на фронтенді. Серіалізується у
// EventConfig.DisplayConfigJson (локально) і в колонку events.display_config (jsonb)
// у Supabase. Порожній рядок/null = типові колонки (Default()).
public class DisplayConfig
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    // Дублюємо прапорці тут, щоб фронтенд читав усе з одного місця.
    [JsonPropertyName("points")]
    public bool Points { get; set; }

    [JsonPropertyName("standings")]
    public bool Standings { get; set; }

    [JsonPropertyName("separateDsqLg")]
    public bool SeparateDsqLg { get; set; }

    [JsonPropertyName("separateDsqSm")]
    public bool SeparateDsqSm { get; set; }

    [JsonPropertyName("columns")]
    public List<ColumnConfig> Columns { get; set; } = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    // Типовий набір колонок — 1:1 із сьогоднішнім жорстко зашитим layout'ом
    // фронтенду (ResultsTable). col-club лише на великому екрані; № і старт
    // ховаються на малому. status/gap/birth/qual вимкнені за замовчуванням.
    public static DisplayConfig Default() => new()
    {
        Version = 1,
        // За замовчуванням розділяємо «Результат»/«Статус(DSQ)» на великому екрані,
        // на малому — об'єднано. Колонка gap іде одразу після result_time.
        SeparateDsqLg = true,
        SeparateDsqSm = false,
        Columns = new()
        {
            new() { Key = "rk",          Order = 0,  Lg = true,  Sm = true  },
            new() { Key = "full_name",   Order = 1,  Lg = true,  Sm = true  },
            new() { Key = "bib",         Order = 2,  Lg = true,  Sm = false },
            new() { Key = "birth",       Order = 3,  Lg = false, Sm = false },
            new() { Key = "qual",        Order = 4,  Lg = false, Sm = false },
            new() { Key = "team",        Order = 5,  Lg = true,  Sm = true  },
            new() { Key = "club",        Order = 6,  Lg = true,  Sm = false },
            new() { Key = "start_time",  Order = 7,  Lg = true,  Sm = false },
            new() { Key = "result_time", Order = 8,  Lg = true,  Sm = true  },
            new() { Key = "gap",         Order = 9,  Lg = true,  Sm = false },
            new() { Key = "status",      Order = 10, Lg = true,  Sm = false },
            new() { Key = "points",      Order = 11, Lg = true,  Sm = false },
        },
    };

    // Розбирає JSON у конфіг; повертає null, якщо рядок порожній/некоректний
    // (тоді викликач бере Default()).
    public static DisplayConfig? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<DisplayConfig>(json, JsonOpts); }
        catch { return null; }
    }

    public string Serialize() => JsonSerializer.Serialize(this, JsonOpts);
}

public class ColumnConfig
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("lg")]
    public bool Lg { get; set; }

    [JsonPropertyName("sm")]
    public bool Sm { get; set; }
}
