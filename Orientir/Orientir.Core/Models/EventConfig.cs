using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Orientir.Core.Models;

public class EventConfig
{
    [Key]
    public int Id { get; set; }

    public string Slug { get; set; } = "";      // slug для URL (?event=Slug) — раніше "Id" у appsettings
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string BasePath { get; set; } = "";  // тека змагання
    public bool Points { get; set; } = false;    // рахувати/показувати бали
    public bool Standings { get; set; } = false; // вмикає вкладку «Сума» (залік); активна лише разом із Points
    public bool SeparateDsqLg { get; set; } = false; // розділяти час та колонку «Статус/DSQ» на великому екрані
    public bool SeparateDsqSm { get; set; } = false; // те саме на малому екрані
    public string DisplayConfigJson { get; set; } = ""; // конфіг колонок (порожньо = типові)
    public int? ActiveDay { get; set; } = null;  // якщо задано — щотакту шлемо результати ЛИШЕ цього дня
    public bool IsActive { get; set; } = false;  // публікувати при «Старт» лише позначені змагання

    public List<DayConfig> Days { get; set; } = new();

    // Якщо Days не задано — автоматично знаходимо підтеки D_1..D_n з OLD.DBF.
    public List<DayConfig> ResolveDays()
    {
        if (Days.Count > 0) return Days.OrderBy(d => d.Day).ToList();

        if (!Directory.Exists(BasePath))
            throw new Exception($"Тека змагання не існує: {BasePath}");

        var days = ScanDays(BasePath);
        if (days.Count == 0)
            throw new Exception($"У теці {BasePath} не знайдено підтек D_1..D_n з OLD.DBF");
        return days;
    }

    // Сканує теку змагання й повертає дні-підтеки D_1..D_n з OLD.DBF.
    // Тека d_0 — службова (реєстрація/попередній стан), її пропускаємо.
    // Label лишається порожнім — його заповнює користувач.
    public static List<DayConfig> ScanDays(string basePath)
    {
        if (string.IsNullOrWhiteSpace(basePath) || !Directory.Exists(basePath))
            return new();

        var rx = new Regex(@"^D_(\d+)$", RegexOptions.IgnoreCase);
        return Directory.GetDirectories(basePath)
            .Select(p => Path.GetFileName(p))
            .Select(name => new { name, M = rx.Match(name) })
            .Where(x => x.M.Success && File.Exists(Path.Combine(basePath, x.name, "OLD.DBF")))
            .Select(x => new DayConfig { Day = int.Parse(x.M.Groups[1].Value), Folder = x.name })
            .Where(d => d.Day >= 1)
            .OrderBy(d => d.Day)
            .ToList();
    }
}
