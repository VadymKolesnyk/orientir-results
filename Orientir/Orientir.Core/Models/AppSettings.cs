using System.ComponentModel.DataAnnotations;

namespace Orientir.Core.Models;

// Глобальні налаштування публікації. У БД зберігається рівно один рядок (Id = 1).
public class AppSettings
{
    [Key]
    public int Id { get; set; } = 1;

    public string SupabaseUrl { get; set; } = "";
    public string ServiceRoleKey { get; set; } = "";
    public int IntervalSeconds { get; set; } = 10;
    public string PublicBaseUrl { get; set; } = "";

    // Глобальний «типовий вигляд» колонок (JSON DisplayConfig). Підставляється
    // для нових змагань, де власний DisplayConfigJson порожній. Порожньо → вшитий
    // DisplayConfig.Default().
    public string DefaultDisplayConfigJson { get; set; } = "";

    // Чи коректно заповнено ключі для бойового запуску (не плейсхолдери).
    public bool IsReadyForLive() =>
        !string.IsNullOrWhiteSpace(SupabaseUrl) && !SupabaseUrl.Contains("ВАШ") &&
        !string.IsNullOrWhiteSpace(ServiceRoleKey) && !ServiceRoleKey.Contains("ВАШ");
}
