using Orientir.Core;
using Orientir.Core.Models;
using Orientir.Core.Services;
using Orientir.ConsoleApp.UI;

namespace Orientir.ConsoleApp;

// Екрани консольного UI. Редагування — інлайн через Console.ReadLine
// (у стилі RunUkraineChecker). Усі дані зберігаються в SQLite через SettingsService.
internal class MenuScreens
{
    private readonly SettingsService _settings;

    public MenuScreens(SettingsService settings) => _settings = settings;

    // ====================================================================
    //  1. Налаштування Supabase (спершу перегляд, редагування — окремо)
    // ====================================================================
    public void ViewSettings()
    {
        while (true)
        {
            var s = _settings.GetSettings();
            ConsoleInput.ClearBuffer();
            ConsoleInput.WriteLine("--- Налаштування Supabase ---");
            ConsoleInput.WriteLine();
            ConsoleInput.WriteLine($"  Supabase URL:    {OrDash(s.SupabaseUrl)}");
            ConsoleInput.WriteLine($"  Service Role Key: {MaskKey(s.ServiceRoleKey)}");
            ConsoleInput.WriteLine($"  Інтервал:        {s.IntervalSeconds} с");
            ConsoleInput.WriteLine($"  Публічний URL:   {OrDash(s.PublicBaseUrl)}");
            ConsoleInput.WriteLine();

            var back = false;
            ConsoleInput.ShowMenuWithInput([
                new("1", "Редагувати", "/edit", EditSettings),
                new("0", "Назад", "/back", () => { back = true; }),
            ]);
            if (back) return;
        }
    }

    private void EditSettings()
    {
        var s = _settings.GetSettings();
        ConsoleBuffer.Draw();
        Console.WriteLine("\n--- Редагування налаштувань ---");
        Console.WriteLine("(Enter — лишити поточне значення)\n");

        s.SupabaseUrl     = Prompt("Supabase URL", s.SupabaseUrl);
        s.ServiceRoleKey  = Prompt("Service Role Key", s.ServiceRoleKey, mask: true);
        s.IntervalSeconds = PromptInt("Інтервал (секунд)", s.IntervalSeconds);
        s.PublicBaseUrl   = Prompt("Публічний URL сторінки", s.PublicBaseUrl);

        _settings.SaveSettings(s);
        Console.WriteLine("\nЗбережено. Натисніть будь-яку клавішу...");
        Console.ReadKey(true);
    }

    // ====================================================================
    //  2. Змагання
    // ====================================================================
    public void ManageEvents()
    {
        while (true)
        {
            var events = _settings.GetEvents();
            ConsoleInput.ClearBuffer();
            ConsoleInput.WriteLine("--- Змагання ---");
            ConsoleInput.WriteLine();
            if (events.Count == 0)
                ConsoleInput.WriteLine("(список порожній)");
            else
                foreach (var e in events)
                    ConsoleInput.WriteLine($"  {(e.IsActive ? "[✔ активне]" : "[  —     ]")} {e.Slug} — {e.Title}  днів: {e.Days.Count}" +
                                           (e.ActiveDay is int a ? $"  активний день: {a}" : ""));
            ConsoleInput.WriteLine();
            ConsoleInput.WriteLine("(публікуються лише змагання, позначені [✔ активне])");
            ConsoleInput.WriteLine();

            var items = new List<ConsoleInput.MenuItem>
            {
                new("1", "Додати змагання", "/add", AddEvent),
            };
            int n = 2;
            foreach (var ev in events)
            {
                var capId = ev.Id;
                items.Add(new(n.ToString(), $"{(ev.IsActive ? "Зняти активність" : "Зробити активним")} «{ev.Slug}»",
                    null, () =>
                    {
                        var e = _settings.GetEvents().First(x => x.Id == capId);
                        e.IsActive = !e.IsActive;
                        _settings.UpdateEvent(e);
                    }));
                n++;
            }
            foreach (var ev in events)
            {
                var capId = ev.Id;
                items.Add(new(n.ToString(), $"Редагувати «{ev.Slug}»", null, () => EditEvent(capId)));
                n++;
            }
            var back = false;
            items.Add(new("0", "Назад", "/back", () => { back = true; }));

            ConsoleInput.ShowMenuWithInput(items);
            if (back) return;
        }
    }

    private void AddEvent()
    {
        ConsoleBuffer.Draw();
        Console.WriteLine("\n--- Нове змагання ---");
        var ev = new EventConfig
        {
            Slug      = Prompt("Slug (для URL, напр. kyiv_city_race_2026)", ""),
            Title     = Prompt("Назва", ""),
            Subtitle  = Prompt("Підзаголовок (дати)", ""),
        };
        Console.WriteLine("\nШлях до теки змагання:");
        ev.BasePath  = ConsoleInput.ReadPathInput();
        ev.Standings = PromptBool("Рахувати бали (вкладка «Сума»)?", false);
        ev.ActiveDay = PromptIntOrNull("Активний день (Enter = усі дні)");

        if (string.IsNullOrWhiteSpace(ev.Slug) || string.IsNullOrWhiteSpace(ev.BasePath))
        {
            Console.WriteLine("\nSlug та шлях обов'язкові. Скасовано.");
            Console.ReadKey(true);
            return;
        }
        _settings.AddEvent(ev);

        // Одразу відкриваємо налаштування днів: сканує D_1..D_n (D_0 пропускає)
        // і просить ввести лише підпис (Label) для кожного дня.
        SetupDaysWithLabels(ev.Id);
    }

    // Сканує теку, показує знайдені дні D_1..D_n і просить лише Label для кожного.
    private void SetupDaysWithLabels(int id)
    {
        var ev = _settings.GetEvents().First(e => e.Id == id);
        ConsoleBuffer.Draw();
        Console.Clear();
        Console.WriteLine($"--- Дні змагання «{ev.Slug}» ---\n");

        List<DayConfig> found;
        try
        {
            found = EventConfig.ScanDays(ev.BasePath);
        }
        catch (Exception exc)
        {
            Console.WriteLine($"Помилка сканування: {exc.Message}");
            Console.WriteLine("\nНатисніть будь-яку клавішу...");
            Console.ReadKey(true);
            return;
        }

        if (found.Count == 0)
        {
            Console.WriteLine($"У теці не знайдено підтек D_1..D_n з OLD.DBF:\n  {ev.BasePath}");
            Console.WriteLine("\nДні не задано — pusher шукатиме їх автоматично при публікації.");
            Console.WriteLine("Натисніть будь-яку клавішу...");
            Console.ReadKey(true);
            return;
        }

        Console.WriteLine($"Знайдено днів: {found.Count}. Введіть підпис для кожного (Enter — пропустити):\n");
        foreach (var d in found)
            d.Label = Prompt($"День {d.Day} ({d.Folder}) — підпис", d.Label);

        ev.Days.Clear();
        foreach (var d in found)
            ev.Days.Add(new DayConfig { Day = d.Day, Folder = d.Folder, Label = d.Label, EventConfigId = ev.Id });
        _settings.SaveEventDays(ev);

        Console.WriteLine("\nДні збережено. Натисніть будь-яку клавішу...");
        Console.ReadKey(true);
    }

    private void EditEvent(int id)
    {
        var ev = _settings.GetEvents().FirstOrDefault(e => e.Id == id);
        if (ev is null) return;

        while (true)
        {
            ev = _settings.GetEvents().First(e => e.Id == id);
            ConsoleInput.ClearBuffer();
            ConsoleInput.WriteLine($"--- Змагання «{ev.Slug}» ---");
            ConsoleInput.WriteLine($"  Назва: {ev.Title}");
            ConsoleInput.WriteLine($"  Підзаголовок: {ev.Subtitle}");
            ConsoleInput.WriteLine($"  Шлях: {ev.BasePath}");
            ConsoleInput.WriteLine($"  Бали: {(ev.Standings ? "так" : "ні")}    Активний день: {(ev.ActiveDay?.ToString() ?? "усі")}");
            ConsoleInput.WriteLine($"  Активне (для публікації): {(ev.IsActive ? "так" : "ні")}");
            ConsoleInput.WriteLine($"  Дні ({ev.Days.Count}): " +
                string.Join(", ", ev.Days.OrderBy(d => d.Day).Select(d => $"{d.Day}={d.Folder}/{d.Label}")));
            ConsoleInput.WriteLine();

            var back = false;
            var capturedId = ev.Id;
            ConsoleInput.ShowMenuWithInput([
                new("1", "Редагувати основні поля", "/edit", () => EditEventFields(capturedId)),
                new("2", "Редагувати дні", "/days", () => EditDays(capturedId)),
                new("3", "Видалити змагання", "/del", () =>
                {
                    if (PromptBool($"Видалити «{ev.Slug}»?", false))
                    {
                        _settings.DeleteEvent(capturedId);
                        back = true;
                    }
                }),
                new("0", "Назад", "/back", () => { back = true; }),
            ]);
            if (back) return;
        }
    }

    private void EditEventFields(int id)
    {
        var ev = _settings.GetEvents().First(e => e.Id == id);
        ConsoleBuffer.Draw();
        Console.WriteLine("\n--- Основні поля (Enter — лишити) ---");
        ev.Slug      = Prompt("Slug", ev.Slug);
        ev.Title     = Prompt("Назва", ev.Title);
        ev.Subtitle  = Prompt("Підзаголовок", ev.Subtitle);
        Console.WriteLine($"Поточний шлях: {ev.BasePath}");
        if (PromptBool("Змінити шлях?", false))
            ev.BasePath = ConsoleInput.ReadPathInput(ev.BasePath);
        ev.Standings = PromptBool("Рахувати бали?", ev.Standings);
        ev.ActiveDay = PromptIntOrNull($"Активний день (Enter = усі){(ev.ActiveDay is int a ? $", зараз {a}" : "")}");
        _settings.UpdateEvent(ev);
        Console.WriteLine("\nЗбережено. Натисніть будь-яку клавішу...");
        Console.ReadKey(true);
    }

    private void EditDays(int id)
    {
        while (true)
        {
            var ev = _settings.GetEvents().First(e => e.Id == id);
            ConsoleInput.ClearBuffer();
            ConsoleInput.WriteLine($"--- Дні «{ev.Slug}» ---");
            ConsoleInput.WriteLine("(якщо днів нема — pusher сам знайде підтеки D_1..D_n)");
            ConsoleInput.WriteLine();
            foreach (var d in ev.Days.OrderBy(d => d.Day))
                ConsoleInput.WriteLine($"  День {d.Day}: тека «{d.Folder}», підпис «{d.Label}»");
            ConsoleInput.WriteLine();

            var back = false;
            ConsoleInput.ShowMenuWithInput([
                new("1", "Знайти дні автоматично (сканувати теку) + ввести підписи", "/scan",
                    () => SetupDaysWithLabels(id)),
                new("2", "Додати день вручну", "/add", () =>
                {
                    var e = _settings.GetEvents().First(x => x.Id == id);
                    ConsoleBuffer.Draw();
                    Console.WriteLine("\n--- Новий день ---");
                    var day = new DayConfig
                    {
                        Day    = PromptInt("Номер дня", e.Days.Count + 1),
                        Folder = Prompt("Підтека (напр. D_1)", ""),
                        Label  = Prompt("Підпис (напр. 30 травня)", ""),
                    };
                    e.Days.Add(day);
                    _settings.SaveEventDays(e);
                    Console.WriteLine("\nДодано. Натисніть будь-яку клавішу...");
                    Console.ReadKey(true);
                }),
                new("3", "Очистити всі дні", "/clear", () =>
                {
                    if (PromptBool("Видалити всі дні (буде автопошук D_1..D_n)?", false))
                    {
                        var e = _settings.GetEvents().First(x => x.Id == id);
                        e.Days.Clear();
                        _settings.SaveEventDays(e);
                    }
                }),
                new("0", "Назад", "/back", () => { back = true; }),
            ]);
            if (back) return;
        }
    }

    // ====================================================================
    //  3. Публікація (на передньому плані)
    // ====================================================================
    public void RunPublisher()
    {
        var s = _settings.GetSettings();
        // Публікуємо ЛИШЕ активні змагання (позначені [✔ активне] у списку).
        var events = _settings.GetEvents().Where(e => e.IsActive).ToList();

        ConsoleBuffer.Draw();
        Console.Clear();
        Console.WriteLine("--- Публікація ---\n");

        if (!s.IsReadyForLive())
        {
            Console.WriteLine("Не заповнено SupabaseUrl / ServiceRoleKey. Спочатку зайдіть у Налаштування.");
            Console.WriteLine("\nНатисніть будь-яку клавішу...");
            Console.ReadKey(true);
            return;
        }
        if (events.Count == 0)
        {
            Console.WriteLine("Немає активних змагань. Позначте змагання як «активне» у розділі «Змагання».");
            Console.WriteLine("\nНатисніть будь-яку клавішу...");
            Console.ReadKey(true);
            return;
        }

        Console.WriteLine($"Активних змагань: {events.Count} ({string.Join(", ", events.Select(e => e.Slug))}), інтервал: {s.IntervalSeconds}с.");
        Console.WriteLine("Натисніть Ctrl+C або Esc щоб зупинити та повернутися в меню.\n");

        using var cts = new CancellationTokenSource();

        // Ctrl+C → м'яка зупинка (не вбиваємо процес).
        ConsoleCancelEventHandler handler = (_, e) => { e.Cancel = true; cts.Cancel(); };
        Console.CancelKeyPress += handler;

        // Рядки з URL робимо клікабельними через OSC 8 (Windows Terminal/VS Code).
        var log = new Progress<string>(line => Console.WriteLine(MakeUrlsClickable(line)));

        // Окремий потік слухає Esc.
        var escWatcher = Task.Run(() =>
        {
            while (!cts.IsCancellationRequested)
            {
                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
                {
                    cts.Cancel();
                    break;
                }
                Thread.Sleep(100);
            }
        });

        try
        {
            PublisherLoop.RunAsync(s, events, log, cts.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) { /* очікувано при зупинці */ }
        finally
        {
            Console.CancelKeyPress -= handler;
        }

        Console.WriteLine("\nПублікацію зупинено. Натисніть будь-яку клавішу...");
        Console.ReadKey(true);
    }

    // ====================================================================
    //  4. Конвертація HTML-звітів
    // ====================================================================
    public void ConvertHtml()
    {
        ConsoleBuffer.Draw();
        Console.WriteLine("\n--- Конвертація HTML-звітів (cp1251 .htm → UTF-8 .html) ---");
        Console.WriteLine("Вкажіть .htm файл АБО теку (конвертуються всі .htm всередині):\n");
        var path = ConsoleInput.ReadPathInput();

        Console.Clear();
        if (string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("Шлях не вказано.");
            Console.ReadKey(true);
            return;
        }

        var files = new List<string>();
        if (Directory.Exists(path))
            files.AddRange(Directory.GetFiles(path, "*.htm", SearchOption.TopDirectoryOnly));
        else if (File.Exists(path))
            files.Add(path);

        if (files.Count == 0)
        {
            Console.WriteLine("Не знайдено .htm файлів за вказаним шляхом.");
            Console.WriteLine("\nНатисніть будь-яку клавішу...");
            Console.ReadKey(true);
            return;
        }

        int ok = 0, err = 0;
        foreach (var f in files)
        {
            try
            {
                var dst = HtmlFix.Convert(f);
                Console.WriteLine($"OK: {f}  →  {dst}");
                ok++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ПОМИЛКА [{f}]: {ex.Message}");
                err++;
            }
        }
        Console.WriteLine($"\nГотово: {ok} успішно, {err} з помилками.");
        Console.WriteLine("Натисніть будь-яку клавішу...");
        Console.ReadKey(true);
    }

    // ====================================================================
    //  5. Dry-run
    // ====================================================================
    public void DryRun()
    {
        var events = _settings.GetEvents();
        Console.Clear();
        Console.WriteLine("--- Dry-run (без відправки) ---\n");
        if (events.Count == 0)
        {
            Console.WriteLine("Немає жодного змагання.");
            Console.WriteLine("\nНатисніть будь-яку клавішу...");
            Console.ReadKey(true);
            return;
        }
        try
        {
            foreach (var line in PublisherLoop.DryRun(events))
                Console.WriteLine(line);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ПОМИЛКА: {ex.Message}");
        }
        Console.WriteLine("\nНатисніть будь-яку клавішу...");
        Console.ReadKey(true);
    }

    // Обгортає http(s)-посилання у рядку в OSC 8 escape-послідовність —
    // сучасні термінали (Windows Terminal, VS Code) роблять їх клікабельними.
    private static string MakeUrlsClickable(string line)
    {
        const string esc = "";
        string st = esc + "\\"; // ST — кінець параметра OSC
        return System.Text.RegularExpressions.Regex.Replace(
            line, @"https?://\S+",
            m => $"{esc}]8;;{m.Value}{st}{m.Value}{esc}]8;;{st}");
    }

    // ====================================================================
    //  Хелпери відображення/вводу
    // ====================================================================
    private static string OrDash(string s) => string.IsNullOrWhiteSpace(s) ? "(не задано)" : s;

    private static string MaskKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return "(не задано)";
        if (key.Length <= 8) return new string('•', key.Length);
        return $"{key[..4]}{new string('•', 8)}{key[^4..]}";
    }

    private static string Prompt(string label, string current, bool mask = false)
    {
        var shown = mask && !string.IsNullOrEmpty(current)
            ? $"{current[..Math.Min(6, current.Length)]}…"
            : current;
        Console.Write($"{label} [{shown}]: ");
        var input = Console.ReadLine();
        return string.IsNullOrEmpty(input) ? current : input.Trim();
    }

    private static int PromptInt(string label, int current)
    {
        Console.Write($"{label} [{current}]: ");
        var input = Console.ReadLine();
        return int.TryParse(input?.Trim(), out var n) ? n : current;
    }

    private static int? PromptIntOrNull(string label)
    {
        Console.Write($"{label}: ");
        var input = Console.ReadLine();
        return int.TryParse(input?.Trim(), out var n) ? n : (int?)null;
    }

    private static bool PromptBool(string label, bool current)
    {
        Console.Write($"{label} (y/n) [{(current ? "y" : "n")}]: ");
        var input = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(input)) return current;
        return input is "y" or "yes" or "так" or "т" or "1";
    }
}
