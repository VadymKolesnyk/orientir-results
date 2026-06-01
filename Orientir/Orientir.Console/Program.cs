using System.Text;
using Orientir.Core.Services;
using Orientir.ConsoleApp.UI;

namespace Orientir.ConsoleApp;

internal static class Program
{
    private static void Main()
    {
        // cp1251 для читання DBF; UTF-8 для коректного виводу кирилиці.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Console.OutputEncoding = Encoding.UTF8;

        // БД налаштувань поруч із застосунком.
        var dbPath = Path.Combine(AppContext.BaseDirectory, "orientir-settings.db");
        using var settings = new SettingsService(dbPath);

        // Одноразовий імпорт старого appsettings.json (формат pusher), якщо БД порожня.
        var legacyPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(legacyPath)) legacyPath = "appsettings.json";
        if (settings.ImportLegacyIfEmpty(legacyPath))
        {
            ConsoleBuffer.Draw();
            Console.WriteLine("Налаштування імпортовано зі старого appsettings.json.");
            Console.WriteLine("Натисніть будь-яку клавішу...");
            Console.ReadKey(true);
        }

        var screens = new MenuScreens(settings);

        while (true)
        {
            ConsoleInput.ClearBuffer();
            ConsoleInput.WriteLine("╔═══════════════════════════════════════════════╗");
            ConsoleInput.WriteLine("║        Orientir — Publisher (консоль)         ║");
            ConsoleInput.WriteLine("╚═══════════════════════════════════════════════╝");
            ConsoleInput.WriteLine();

            var s = settings.GetSettings();
            var events = settings.GetEvents();
            ConsoleInput.WriteLine($"Supabase: {(string.IsNullOrWhiteSpace(s.SupabaseUrl) ? "(не задано)" : s.SupabaseUrl)}");
            ConsoleInput.WriteLine($"Інтервал: {s.IntervalSeconds}с    Змагань: {events.Count}");
            ConsoleInput.WriteLine();
            ConsoleInput.WriteLine("Виберіть дію:");
            ConsoleInput.WriteLine();

            var exit = false;
            ConsoleInput.ShowMenuWithInput([
                new("1", "Налаштування Supabase", "/settings", screens.ViewSettings),
                new("2", "Змагання (список / додати / редагувати)", "/events", screens.ManageEvents),
                new("3", "Запустити публікацію", "/run", screens.RunPublisher),
                new("4", "Конвертувати HTML-звіти", "/html", screens.ConvertHtml),
                new("5", "Dry-run (перевірка без відправки)", "/dry", screens.DryRun),
                new("0", "Вихід", "/exit", () => { exit = true; }),
            ]);

            if (exit) break;
        }

        ConsoleBuffer.Draw();
        Console.WriteLine("Програму завершено.");
    }
}
