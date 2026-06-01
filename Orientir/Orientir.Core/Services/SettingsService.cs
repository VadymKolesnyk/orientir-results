using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Orientir.Core.Data;
using Orientir.Core.Models;

namespace Orientir.Core.Services;

// Доступ до налаштувань і змагань у SQLite. Створює БД за потреби та
// одноразово імпортує старий appsettings.json (формат pusher), якщо БД порожня.
public class SettingsService : IDisposable
{
    private readonly SettingsDbContext _db;

    public SettingsService(string dbPath)
    {
        _db = new SettingsDbContext(dbPath);
        _db.Database.EnsureCreated();
        EnsureSchema();
    }

    // EnsureCreated() не змінює наявну БД при додаванні колонок. Тож для старих
    // БД доливаємо нові колонки вручну (idempotent — IF NOT EXISTS у SQLite немає,
    // тому ловимо помилку «duplicate column»).
    private void EnsureSchema()
    {
        TryAddColumn("Events", "IsActive", "INTEGER NOT NULL DEFAULT 0");
    }

    private void TryAddColumn(string table, string column, string type)
    {
        try
        {
            _db.Database.ExecuteSqlRaw($"ALTER TABLE {table} ADD COLUMN {column} {type};");
        }
        catch
        {
            // колонка вже є — нормально
        }
    }

    // -- Налаштування (один рядок) -----------------------------------------
    public AppSettings GetSettings()
    {
        var s = _db.Settings.FirstOrDefault();
        if (s is null)
        {
            s = new AppSettings { Id = 1 };
            _db.Settings.Add(s);
            _db.SaveChanges();
        }
        return s;
    }

    public void SaveSettings(AppSettings settings)
    {
        var existing = _db.Settings.FirstOrDefault();
        if (existing is null)
        {
            settings.Id = 1;
            _db.Settings.Add(settings);
        }
        else
        {
            existing.SupabaseUrl    = settings.SupabaseUrl;
            existing.ServiceRoleKey = settings.ServiceRoleKey;
            existing.IntervalSeconds = settings.IntervalSeconds;
            existing.PublicBaseUrl  = settings.PublicBaseUrl;
        }
        _db.SaveChanges();
    }

    // -- Змагання -----------------------------------------------------------
    public List<EventConfig> GetEvents() =>
        _db.Events.Include(e => e.Days).OrderBy(e => e.Id).ToList();

    public EventConfig AddEvent(EventConfig ev)
    {
        _db.Events.Add(ev);
        _db.SaveChanges();
        return ev;
    }

    public void UpdateEvent(EventConfig ev)
    {
        _db.Events.Update(ev);
        _db.SaveChanges();
    }

    public void DeleteEvent(int id)
    {
        var ev = _db.Events.Include(e => e.Days).FirstOrDefault(e => e.Id == id);
        if (ev is not null)
        {
            _db.Events.Remove(ev);
            _db.SaveChanges();
        }
    }

    // -- Дні (керування через EventConfig.Days; зберігаємо весь граф) -------
    public void SaveEventDays(EventConfig ev)
    {
        _db.Events.Update(ev);
        _db.SaveChanges();
    }

    // -- Імпорт зі старого appsettings.json ---------------------------------
    // Викликати при старті: якщо БД порожня й поруч є appsettings.json — перенести.
    public bool ImportLegacyIfEmpty(string appsettingsPath)
    {
        if (_db.Settings.Any() || _db.Events.Any()) return false;
        if (!File.Exists(appsettingsPath)) return false;

        try
        {
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var legacy = JsonSerializer.Deserialize<LegacyConfig>(File.ReadAllText(appsettingsPath), opts);
            if (legacy is null) return false;

            SaveSettings(new AppSettings
            {
                SupabaseUrl     = legacy.SupabaseUrl ?? "",
                ServiceRoleKey  = legacy.ServiceRoleKey ?? "",
                IntervalSeconds = legacy.IntervalSeconds ?? 10,
                PublicBaseUrl   = legacy.PublicBaseUrl ?? "",
            });

            foreach (var le in legacy.Events ?? new())
            {
                var ev = new EventConfig
                {
                    Slug      = le.Id ?? "",
                    Title     = le.Title ?? "",
                    Subtitle  = le.Subtitle ?? "",
                    BasePath  = le.BasePath ?? "",
                    Standings = le.Standings ?? false,
                    ActiveDay = le.ActiveDay,
                    Days = (le.Days ?? new()).Select(ld => new DayConfig
                    {
                        Day = ld.Day, Folder = ld.Folder ?? "", Label = ld.Label ?? "",
                    }).ToList(),
                };
                _db.Events.Add(ev);
            }
            _db.SaveChanges();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose() => _db.Dispose();

    // DTO під формат старого appsettings.json (pusher).
    private class LegacyConfig
    {
        public string? SupabaseUrl { get; set; }
        public string? ServiceRoleKey { get; set; }
        public int? IntervalSeconds { get; set; }
        public string? PublicBaseUrl { get; set; }
        public List<LegacyEvent>? Events { get; set; }
    }
    private class LegacyEvent
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Subtitle { get; set; }
        public string? BasePath { get; set; }
        public bool? Standings { get; set; }
        public int? ActiveDay { get; set; }
        public List<LegacyDay>? Days { get; set; }
    }
    private class LegacyDay
    {
        public int Day { get; set; }
        public string? Folder { get; set; }
        public string? Label { get; set; }
    }
}
