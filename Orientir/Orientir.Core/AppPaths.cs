using System.IO;

namespace Orientir.Core;

// Єдина точка визначення робочої папки даних. Усі файли, які застосунок
// створює/пише (БД налаштувань, старий appsettings.json), живуть у папці
// "data" поруч із exe. Папка створюється автоматично при першому зверненні.
public static class AppPaths
{
    // Папка "data" поруч із виконуваним файлом.
    public static string DataDir { get; } = EnsureDataDir();

    private static string EnsureDataDir()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dir); // idempotent — не падає, якщо вже є
        return dir;
    }

    // Повний шлях до файлу всередині папки data.
    public static string InData(string fileName) => Path.Combine(DataDir, fileName);

    // Стандартні файли застосунку.
    public static string SettingsDb => InData("orientir-settings.db");
    public static string LegacyAppSettings => InData("appsettings.json");
}
