using System.Text;

namespace Orientir.ConsoleApp.UI;

/// <summary>
/// Універсальний клас для роботи з консольним введенням:
/// - Меню з навігацією стрілками
/// - Текстовий ввід з автокомплітом команд та шляхів
/// - Навігація стрілками по підказках автокомпліту
/// </summary>
public static class ConsoleInput
{
    public const int SUGGESTIONS_COUNT = 10;
    public record MenuItem
    {
        public MenuItem(string key, string label, string? command, Func<bool> action)
        {
            Key = key;
            Label = label;
            Command = command;
            Action = action;
        }

        public MenuItem(string key, string label, string? command, Action action)
        {
            Key = key;
            Label = label;
            Command = command;
            Action = () =>
            {
                action();
                return false;
            };
        }

        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public string? Command { get; set; }
        public Func<bool> Action { get; set; }
    }

    /// <summary>
    /// Показує меню з можливістю навігації стрілками та введенням команд/номерів
    /// За замовчуванням виділений перший елемент. При введенні - знімається виділення.
    /// ArrowUp/ArrowDown відновлюють виділення і очищують інпут.
    /// </summary>
    public static bool ShowMenuWithInput(List<MenuItem> items)
    {
        var selections = SelectMenuWithInputBuffered(items);
        var item = items.Find(x => x.Key.Equals(selections, StringComparison.InvariantCultureIgnoreCase));
        if (item is null)
        {
            ConsoleBuffer.Draw();
            Console.WriteLine("\nНевірний вибір!");
            WaitForKey();
            return false;
        }

        return item.Action.Invoke();
    }

    /// <summary>
    /// Нова версія SelectMenu з використанням ConsoleBuffer та InputLineHandler
    /// </summary>
    private static string SelectMenuWithInputBuffered(List<MenuItem> items)
    {
        var inputHandler = new InputLineHandler
        {
            Menu = items,
            AvailableCommands = items
                .Select(x => x.Command!)
                .Where(x => !string.IsNullOrEmpty(x))
                .ToList()
        };


        while (true)
        {
            // 1. Відображаємо базовий буфер (все що було додано раніше)
            ConsoleBuffer.Draw();

            // 2. Відображаємо меню
            WriteMenu(inputHandler);

            // Заповнюємо решту екрану до рядка введення
            var currentLine = Console.CursorTop;
            var availableLines = Console.WindowHeight - 3 - Math.Min(inputHandler.Suggestions.Count, SUGGESTIONS_COUNT);
            for (var i = currentLine; i < availableLines; i++)
            {
                Console.WriteLine();
            }

            // 3. Відображаємо підказки якщо є
            WriteSuggestions(inputHandler);

            // 4. Малюємо роздільник та рядок введення
            WriteInput(inputHandler.Input, inputHandler.GhostText, inputHandler.CursorPosition);

            var key = Console.ReadKey(intercept: true);

            // Enter - підтвердити вибір
            if (key.Key == ConsoleKey.Enter)
            {
                if (inputHandler.HasInput)
                {
                    // Якщо є ghost text - автозаповнюємо його
                    inputHandler.ApplyGhostText();

                    // Повертаємо введену команду або знайдену по команді
                    var command = inputHandler.Input.Trim();
                    var keyCommand = items.Find(x => x.Command?.Equals(command, StringComparison.OrdinalIgnoreCase) ?? false)?.Key;
                    Console.Clear();
                    Console.Write("Завантаження...");
                    return !string.IsNullOrEmpty(keyCommand) ? keyCommand : command;
                }

                Console.Clear();
                Console.Write("Завантаження...");
                // Якщо інпут порожній - повертаємо виділений пункт
                return items[inputHandler.SelectedMenuIndex].Key;
            }

            // Стрілки вгору/вниз
            if (key.Key == ConsoleKey.UpArrow)
            {
                if (inputHandler.ShowingSuggestions)
                {
                    inputHandler.NavigateSuggestionsUp();
                }
                else
                {
                    // Навігація по меню
                    inputHandler.Clear();
                    inputHandler.SelectedMenuIndex = (inputHandler.SelectedMenuIndex - 1 + items.Count) % items.Count;
                }
                continue;
            }

            if (key.Key == ConsoleKey.DownArrow)
            {
                if (inputHandler.ShowingSuggestions)
                {
                    inputHandler.NavigateSuggestionsDown();
                }
                else
                {
                    // Навігація по меню
                    inputHandler.Clear();
                    inputHandler.SelectedMenuIndex = (inputHandler.SelectedMenuIndex + 1) % items.Count;
                }
                continue;
            }

            // Escape - скасування або очистка
            if (key.Key == ConsoleKey.Escape)
            {
                inputHandler.Clear();
                continue;
            }

            // Backspace - видалення символу
            if (key.Key == ConsoleKey.Backspace)
            {
                inputHandler.Backspace();
                continue;
            }

            // Tab - автокомпліт
            if (key.Key == ConsoleKey.Tab)
            {
                inputHandler.Tab();
                continue;
            }

            // Стрілка вліво - навігація по тексту
            if (key.Key == ConsoleKey.LeftArrow)
            {
                if (inputHandler.HasInput && !inputHandler.ShowingSuggestions)
                {
                    inputHandler.MoveCursorLeft();
                }
                continue;
            }

            // Стрілка вправо - навігація по тексту
            if (key.Key == ConsoleKey.RightArrow)
            {
                if (inputHandler.HasInput && !inputHandler.ShowingSuggestions)
                {
                    inputHandler.MoveCursorRight();
                }
                continue;
            }

            // Звичайний символ - введення тексту
            if (!char.IsControl(key.KeyChar))
            {
                inputHandler.AppendChar(key.KeyChar);
            }
        }
    }

    private static void WriteInput(string input, string ghostText, int cursorPosition)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(new string('─', Console.WindowWidth - 1));
        Console.ResetColor();

        Console.Write("> ");
        var inputLineLeft = Console.CursorLeft;
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine();
        Console.Write(new string('─', Console.WindowWidth - 1));
        Console.ResetColor();
        var inputLineTop = Console.CursorTop - 1;
        Console.SetCursorPosition(inputLineLeft, inputLineTop);

        Console.Write(input);

        // Відображаємо ghost text
        if (!string.IsNullOrEmpty(ghostText))
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(ghostText);
            Console.ResetColor();
        }

        // Встановлюємо курсор в правильну позицію
        Console.SetCursorPosition(inputLineLeft + cursorPosition, inputLineTop);
    }

    private static void WriteSuggestions(InputLineHandler inputHandler)
    {
        if (inputHandler is not { ShowingSuggestions: true, Suggestions.Count: > 0 })
        {
            return;
        }

        Console.ResetColor();

        var totalCount = inputHandler.Suggestions.Count;
        var scrollOffset = inputHandler.SuggestionScrollOffset;
        var visibleCount = SUGGESTIONS_COUNT;

        // Показуємо елементи з scrollOffset до scrollOffset + visibleCount
        for (int i = 0; i < visibleCount; i++)
        {
            var itemIndex = scrollOffset + i;
            if (itemIndex >= totalCount) break;

            if (scrollOffset > 0 && i == 0)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("˄ ");
                Console.ResetColor();
            }
            else if (scrollOffset < totalCount - visibleCount && i == visibleCount - 1)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("˅ ");
                Console.ResetColor();
            }
            else
            {
                Console.Write("  ");
            }

            if (itemIndex == inputHandler.SuggestionIndex)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(inputHandler.Suggestions[itemIndex]);
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine(inputHandler.Suggestions[itemIndex]);
                Console.ResetColor();
            }
        }
    }

    private static void WritePathSuggestions(PathInputHandler pathHandler)
    {
        if (!pathHandler.ShowingSuggestions || pathHandler.Suggestions.Count == 0)
        {
            return;
        }

        Console.ResetColor();

        var totalCount = pathHandler.Suggestions.Count;
        var scrollOffset = pathHandler.SuggestionScrollOffset;

        var visibleCount = SUGGESTIONS_COUNT;

        // Показуємо елементи з scrollOffset до scrollOffset + visibleCount
        for (int i = 0; i < visibleCount; i++)
        {
            var itemIndex = scrollOffset + i;
            if (itemIndex >= totalCount) break;
            if ((scrollOffset > 0 && i == 0))
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("˄ ");
                Console.ResetColor();
            }
            else if (scrollOffset < totalCount - visibleCount && i == visibleCount - 1)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("˅ ");
                Console.ResetColor();
            }
            else
            {
                Console.Write("  ");
            }

            var path = pathHandler.Suggestions[itemIndex];
            var displayPath = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            if (itemIndex == pathHandler.SuggestionIndex)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
            }

            if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar))
            {
                Console.Write($"📁 {(string.IsNullOrEmpty(displayPath) ? path : displayPath)}");
            }
            else
            {
                Console.Write($"📄 {displayPath}");
            }

            // Очистити залишок рядка
            var currentPos = Console.CursorLeft;
            if (currentPos < Console.WindowWidth - 1)
            {
                Console.Write(new string(' ', Console.WindowWidth - currentPos - 1));
            }

            Console.ResetColor();
            Console.WriteLine();
        }

    }

    private static void WriteMenu(InputLineHandler inputHandler)
    {
        // Визначаємо доступні рядки для меню
        var availableLines = Console.WindowHeight - 5 - Math.Min(inputHandler.Suggestions.Count, SUGGESTIONS_COUNT);
        var totalCount = inputHandler.Menu.Count;
        var visibleCount = Math.Min(availableLines, totalCount);

        // Оновлюємо scroll offset
        inputHandler.UpdateMenuScrollOffset(availableLines);
        var scrollOffset = inputHandler.MenuScrollOffset;

        // Показуємо індикатор прокрутки зверху якщо є
        if (scrollOffset > 0)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  ... ({inputHandler.SelectedMenuIndex + 1}/{totalCount})");
            Console.ResetColor();
        }

        // Показуємо пункти меню з scrollOffset до scrollOffset + visibleCount
        for (var i = 0; i < visibleCount; i++)
        {
            var itemIndex = scrollOffset + i;
            if (itemIndex >= totalCount) break;

            var item = inputHandler.Menu[itemIndex];

            if (itemIndex == inputHandler.SelectedMenuIndex)
            {
                // Виділяємо обраний пункт тільки якщо немає введення
                Console.BackgroundColor = ConsoleColor.DarkBlue;
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("► ");
                Console.Write(item.Key + ". " + item.Label + (string.IsNullOrEmpty(item.Command) ? "" : $" ({item.Command})"));
                Console.ResetColor();
            }
            else
            {
                Console.Write("  ");
                Console.Write(item.Key + ". " + item.Label + (string.IsNullOrEmpty(item.Command) ? "" : $" ({item.Command})"));
            }

            // Очищаємо залишок рядка
            var currentPos = Console.CursorLeft;
            var consoleWidth = Console.WindowWidth;
            if (currentPos < consoleWidth)
            {
                Console.Write(new string(' ', consoleWidth - currentPos - 1));
            }
            Console.WriteLine();
        }

        // Показуємо індикатор прокрутки знизу якщо є
        if (scrollOffset + visibleCount < totalCount)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  ... ({inputHandler.SelectedMenuIndex + 1}/{totalCount})");
            Console.ResetColor();
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Читає шлях до файлу/папки з автокомплітом та навігацією стрілками
    /// </summary>
    public static string ReadPathInput(string? baseDirectory = null)
    {
        var pathHandler = new PathInputHandler(baseDirectory);

        while (true)
        {
            // 1. Відображаємо базовий буфер (все що було додано раніше)
            ConsoleBuffer.Draw();

            // 2. Відображаємо заголовок
            Console.WriteLine("\nВведіть шлях (Tab для автокомпліту): ");

            // Заповнюємо решту екрану до рядка введення
            var currentLine = Console.CursorTop;
            var availableLines = Console.WindowHeight - 3 - Math.Min(pathHandler.Suggestions.Count, SUGGESTIONS_COUNT);
            for (var i = currentLine; i < availableLines; i++)
            {
                Console.WriteLine();
            }

            // 3. Відображаємо підказки якщо є
            WritePathSuggestions(pathHandler);

            // 4. Малюємо роздільник та рядок введення
            WriteInput(pathHandler.Input, pathHandler.GhostText, pathHandler.CursorPosition);

            var key = Console.ReadKey(intercept: true);

            // Enter - підтвердити вибір
            if (key.Key == ConsoleKey.Enter)
            {
                return pathHandler.Input;
            }

            // Стрілки вгору/вниз
            if (key.Key == ConsoleKey.UpArrow)
            {
                if (pathHandler.ShowingSuggestions)
                {
                    pathHandler.NavigateSuggestionsUp();
                }
                continue;
            }

            if (key.Key == ConsoleKey.DownArrow)
            {
                if (pathHandler.ShowingSuggestions)
                {
                    pathHandler.NavigateSuggestionsDown();
                }
                continue;
            }

            // Escape - очистити
            if (key.Key == ConsoleKey.Escape)
            {
                pathHandler.Clear();
                continue;
            }

            // Backspace - видалення символу
            if (key.Key == ConsoleKey.Backspace)
            {
                pathHandler.Backspace();
                continue;
            }

            // Tab - автокомпліт
            if (key.Key == ConsoleKey.Tab)
            {
                pathHandler.Tab();
                continue;
            }

            // Стрілка вліво - навігація по тексту
            if (key.Key == ConsoleKey.LeftArrow)
            {
                if (pathHandler.HasInput && !pathHandler.ShowingSuggestions)
                {
                    pathHandler.MoveCursorLeft();
                }
                continue;
            }

            // Стрілка вправо - навігація по тексту
            if (key.Key == ConsoleKey.RightArrow)
            {
                if (pathHandler.HasInput && !pathHandler.ShowingSuggestions)
                {
                    pathHandler.MoveCursorRight();
                }
                continue;
            }

            // Звичайний символ - введення тексту
            if (!char.IsControl(key.KeyChar))
            {
                pathHandler.AppendChar(key.KeyChar);
            }
        }
    }

    /// <summary>
    /// Універсальний метод для пошуку та вибору елемента зі списку.
    /// Підтримує пошук по Contains в заданих полях та навігацію стрілками.
    /// </summary>
    public static T? FindItem<T>(
        List<T> items,
        Func<T, string> displayFunc,
        params Func<T, string>[] searchFields)
    {
        if (items.Count == 0)
        {
            Console.WriteLine("Список порожній.");
            return default;
        }

        var filteredItems = new List<T>(items);
        var selectedIndex = 0;
        var searchQuery = new StringBuilder();

        // Малюємо статичний заголовок один раз
        Console.Clear();
        Console.WriteLine("╔═══════════════════════════════════════════════╗");
        Console.WriteLine("║            ПОШУК ЕЛЕМЕНТА                     ║");
        Console.WriteLine("╚═══════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.Write("Пошук: ");

        // Зберігаємо позицію рядка пошуку
        var searchLineTop = Console.CursorTop;
        var searchLineLeft = Console.CursorLeft;

        while (true)
        {
            // Очищаємо та відображаємо рядок пошуку
            Console.SetCursorPosition(searchLineLeft, searchLineTop);
            Console.Write(searchQuery.ToString());
            var searchEndPos = Console.CursorLeft;
            Console.Write(new string(' ', Console.WindowWidth - searchEndPos - 1));

            // Відображаємо результати нижче
            ShowFindResults(filteredItems, selectedIndex, displayFunc, searchLineTop);

            // Повертаємо курсор на позицію пошуку
            Console.SetCursorPosition(searchEndPos, searchLineTop);

            var key = Console.ReadKey(intercept: true);

            // Enter - підтвердити вибір
            if (key.Key == ConsoleKey.Enter)
            {
                if (filteredItems.Count > 0)
                {
                    ClearFindResults(searchLineTop);
                    Console.SetCursorPosition(0, searchLineTop + 1);
                    return filteredItems[selectedIndex];
                }
                continue;
            }

            // Escape - скасувати
            if (key.Key == ConsoleKey.Escape)
            {
                ClearFindResults(searchLineTop);
                Console.SetCursorPosition(0, searchLineTop + 1);
                return default;
            }

            // Стрілка вгору
            if (key.Key == ConsoleKey.UpArrow)
            {
                if (filteredItems.Count > 0)
                {
                    selectedIndex = (selectedIndex - 1 + filteredItems.Count) % filteredItems.Count;
                }
                continue;
            }

            // Стрілка вниз
            if (key.Key == ConsoleKey.DownArrow)
            {
                if (filteredItems.Count > 0)
                {
                    selectedIndex = (selectedIndex + 1) % filteredItems.Count;
                }
                continue;
            }

            // Backspace - видалити символ
            if (key.Key == ConsoleKey.Backspace)
            {
                if (searchQuery.Length > 0)
                {
                    searchQuery.Remove(searchQuery.Length - 1, 1);
                    selectedIndex = 0;

                    // Фільтруємо елементи
                    if (searchQuery.Length == 0)
                    {
                        filteredItems = [..items];
                    }
                    else
                    {
                        filteredItems = items.Where(item =>
                            searchFields.Any(field =>
                                field(item).Contains(searchQuery.ToString(), StringComparison.OrdinalIgnoreCase)
                            )
                        ).ToList();
                    }
                }
                continue;
            }

            // Звичайний символ - пошук
            if (!char.IsControl(key.KeyChar))
            {
                searchQuery.Append(key.KeyChar);
                selectedIndex = 0;

                // Фільтруємо елементи
                filteredItems = items.Where(item =>
                    searchFields.Any(field =>
                        field(item).Contains(searchQuery.ToString(), StringComparison.OrdinalIgnoreCase)
                    )
                ).ToList();
            }
        }
    }

    private static void ShowFindResults<T>(List<T> filteredItems, int selectedIndex, Func<T, string> displayFunc, int searchLineTop)
    {
        // Перевірити, чи є місце для результатів
        if (searchLineTop + 1 >= Console.BufferHeight)
        {
            return;
        }

        // Обмежити кількість результатів з урахуванням розміру буфера
        var availableLines = Console.BufferHeight - searchLineTop - 2;
        var maxShow = Math.Min(Math.Min(filteredItems.Count, 10), Math.Max(availableLines, 0));

        // Очистити рядки для результатів (заголовок + елементи + підсумок)
        var linesToClear = maxShow + 15;
        for (var i = 0; i < linesToClear; i++)
        {
            if (searchLineTop + 1 + i >= Console.BufferHeight) break;
            Console.SetCursorPosition(0, searchLineTop + 1 + i);
            Console.Write(new string(' ', Console.WindowWidth - 1));
        }

        // Показати результати
        Console.SetCursorPosition(0, searchLineTop + 1);
        Console.WriteLine();

        if (filteredItems.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("Нічого не знайдено. Натисніть Esc для скасування або Backspace для очистки пошуку.");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"Знайдено {filteredItems.Count} елемент(ів) (↑↓ для навігації, Enter для вибору, Esc для скасування):");
            Console.ResetColor();

            for (var i = 0; i < maxShow; i++)
            {
                if (Console.CursorTop >= Console.BufferHeight - 1) break;

                // Виділення обраного елемента
                if (i == selectedIndex)
                {
                    Console.BackgroundColor = ConsoleColor.DarkBlue;
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write("► ");
                }
                else
                {
                    Console.Write("  ");
                }

                Console.Write(displayFunc(filteredItems[i]));

                // Очистити залишок рядка
                var currentPos = Console.CursorLeft;
                if (currentPos < Console.WindowWidth - 1)
                {
                    Console.Write(new string(' ', Console.WindowWidth - currentPos - 1));
                }

                Console.ResetColor();
                Console.WriteLine();
            }

            if (filteredItems.Count > maxShow && Console.CursorTop < Console.BufferHeight - 1)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"  ... та ще {filteredItems.Count - maxShow} елемент(ів)");
                Console.ResetColor();
            }
        }
    }

    private static void ClearFindResults(int searchLineTop)
    {
        // Очистити рядки результатів
        for (int i = 0; i < 15; i++)
        {
            int targetLine = searchLineTop + 1 + i;
            if (targetLine >= Console.BufferHeight) break;

            Console.SetCursorPosition(0, targetLine);
            Console.Write(new string(' ', Console.WindowWidth - 1));
        }
    }

    private static void WaitForKey()
    {
        Console.WriteLine("\nНатисніть будь-яку клавішу...");
        Console.ReadKey(true);
    }

    /// <summary>
    /// Записує текст в буфер без переносу рядка
    /// </summary>
    public static void Write(string text)
    {
        ConsoleBuffer.Write(text);
    }

    /// <summary>
    /// Записує текст в буфер з переносом рядка
    /// </summary>
    public static void WriteLine(string text = "")
    {
        ConsoleBuffer.WriteLine(text);
    }

    /// <summary>
    /// Очищає буфер консолі
    /// </summary>
    public static void ClearBuffer()
    {
        ConsoleBuffer.Clear();
    }
}
