using System.Text;

namespace Orientir.ConsoleApp.UI;

/// <summary>
/// Клас для обробки введення шляху з автокомплітом папок
/// </summary>
public class PathInputHandler
{
    private readonly StringBuilder _input = new();
    private int _cursorPosition = 0;
    private readonly string? _baseDirectory;

    public PathInputHandler(string? baseDirectory = null)
    {
        _baseDirectory = baseDirectory;

        // Якщо basePath не null - додаємо його відразу до input
        if (!string.IsNullOrEmpty(baseDirectory))
        {
            _input.Append(baseDirectory);
            _cursorPosition = _input.Length;
        }

        UpdatePathSuggestions();
    }

    public string Input => _input.ToString();
    public string GhostText { get; private set; } = "";
    public int CursorPosition => _cursorPosition;

    public List<string> Suggestions { get; } = [];

    public bool ShowingSuggestions => Suggestions.Count > 0;

    public int SuggestionIndex { get; private set; } = 0;

    public int SuggestionScrollOffset { get; private set; } = 0;

    /// <summary>
    /// Додає символ до інпуту в позицію курсору
    /// </summary>
    public void AppendChar(char c)
    {
        _input.Insert(_cursorPosition, c);
        _cursorPosition++;
        SuggestionIndex = 0;
        UpdatePathSuggestions();
    }

    /// <summary>
    /// Видаляє символ перед курсором
    /// </summary>
    public void Backspace()
    {
        if (_cursorPosition == 0)
        {
            return;
        }
        _input.Remove(_cursorPosition - 1, 1);
        _cursorPosition--;
        SuggestionIndex = 0;
        UpdatePathSuggestions();
    }

    /// <summary>
    /// Очищає інпут
    /// </summary>
    public void Clear()
    {
        _input.Clear();
        _cursorPosition = 0;
        GhostText = "";
        Suggestions.Clear();
        SuggestionIndex = 0;
        SuggestionScrollOffset = 0;
    }

    /// <summary>
    /// Переміщує курсор вліво
    /// </summary>
    public void MoveCursorLeft()
    {
        if (_cursorPosition > 0)
        {
            _cursorPosition--;
        }
    }

    /// <summary>
    /// Переміщує курсор вправо
    /// </summary>
    public void MoveCursorRight()
    {
        if (_cursorPosition < _input.Length)
        {
            _cursorPosition++;
        }
    }

    /// <summary>
    /// Застосовує ghost text до інпуту
    /// </summary>
    public void ApplyGhostText()
    {
        if (!string.IsNullOrEmpty(GhostText))
        {
            _input.Append(GhostText);
            _cursorPosition = _input.Length;
            GhostText = "";
            UpdatePathSuggestions();
        }
    }

    /// <summary>
    /// Оновлює підказки шляхів
    /// </summary>
    public void UpdatePathSuggestions()
    {
        try
        {
            string baseDir;
            string searchPattern;

            var input = _input.ToString();
            if (input.StartsWith(Path.DirectorySeparatorChar) || input.StartsWith(Path.AltDirectorySeparatorChar))
            {
                Suggestions.Clear();
                SuggestionIndex = 0;
                SuggestionScrollOffset = 0;
                GhostText = "";
                return;
            }

            if (string.IsNullOrWhiteSpace(input) || !(input.Contains(Path.DirectorySeparatorChar) || input.Contains(Path.AltDirectorySeparatorChar)))
            {
                // Якщо інпут пустий
                var drives = DriveInfo.GetDrives()
                    .Where(d => d.IsReady && d.Name.StartsWith(input, StringComparison.InvariantCultureIgnoreCase))
                    .Select(d => d.Name)
                    .ToList();

                Suggestions.Clear();
                Suggestions.AddRange(drives);
                SuggestionIndex = 0;
                SuggestionScrollOffset = 0;
                if (Suggestions.Count > 0)
                {
                    var suggestion = Suggestions[SuggestionIndex];
                    var inputLength = input.Length;
                    GhostText = suggestion[inputLength..];
                }
                else
                {
                    GhostText = "";
                }
                return;
            }

            // Якщо шлях абсолютний - використовуємо як є
            if (Path.IsPathRooted(input))
            {
                if (input.EndsWith(Path.DirectorySeparatorChar) || input.EndsWith(Path.AltDirectorySeparatorChar))
                {
                    baseDir = Path.GetFullPath(input);
                    searchPattern = "";
                }
                else
                {
                    var dirName = Path.GetDirectoryName(input);
                    baseDir = string.IsNullOrEmpty(dirName) ? Directory.GetCurrentDirectory() : Path.GetFullPath(dirName);
                    searchPattern = Path.GetFileName(input);
                }
            }
            else
            {
                Suggestions.Clear();
                SuggestionScrollOffset = 0;
                SuggestionIndex = 0;
                GhostText = "";
                return;
            }

            // Якщо шлях не існує або містить некоректні символи - не показуємо ghost text
            if (string.IsNullOrEmpty(baseDir) || !Directory.Exists(baseDir))
            {
                Suggestions.Clear();
                SuggestionScrollOffset = 0;
                SuggestionIndex = 0;
                GhostText = "";
                return;
            }

            // Перевіряємо чи інпут має некоректні символи для шляху
            if (!string.IsNullOrWhiteSpace(searchPattern))
            {
                var invalidChars = Path.GetInvalidFileNameChars();
                if (searchPattern.Any(c => invalidChars.Contains(c)))
                {
                    Suggestions.Clear();
                    SuggestionScrollOffset = 0;
                    SuggestionIndex = 0;
                    GhostText = "";
                    return;
                }
            }

            // Знайти всі папки та файли
            var directories = Directory.GetDirectories(baseDir)
                .Where(d => Path.GetFileName(d).StartsWith(searchPattern, StringComparison.OrdinalIgnoreCase))
                .Select(d => d + Path.DirectorySeparatorChar)
                .ToList();

            var files = Directory.GetFiles(baseDir)
                .Where(f => Path.GetFileName(f).StartsWith(searchPattern, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var matches = directories.Concat(files).ToList();

            Suggestions.Clear();
            Suggestions.AddRange(matches);
            SuggestionScrollOffset = 0;
            SuggestionIndex = 0;

            // Генеруємо ghost text з першої підказки
            if (matches.Count > 0 && SuggestionIndex < matches.Count)
            {
                var suggestion = matches[SuggestionIndex];
                var inputLength = input.Length;
                GhostText = suggestion[inputLength..];
            }
            else
            {
                GhostText = "";
            }
        }
        catch
        {
            Suggestions.Clear();
            SuggestionScrollOffset = 0;
            GhostText = "";
        }
    }

    /// <summary>
    /// Застосовує обрану підказку
    /// </summary>
    public bool ApplySelectedSuggestion()
    {
        if (ShowingSuggestions && SuggestionIndex < Suggestions.Count)
        {
            _input.Clear();
            _input.Append(Suggestions[SuggestionIndex]);
            _cursorPosition = _input.Length;
            SuggestionIndex = 0;
            GhostText = "";
            UpdatePathSuggestions();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Навігація по підказках вгору
    /// </summary>
    public void NavigateSuggestionsUp()
    {
        if (ShowingSuggestions)
        {
            SuggestionIndex = SuggestionIndex <= 0 ? Suggestions.Count - 1 : SuggestionIndex - 1;
            UpdateGhostTextFromSuggestion();
            UpdateSuggestionScrollOffset();
        }
    }

    /// <summary>
    /// Навігація по підказках вниз
    /// </summary>
    public void NavigateSuggestionsDown()
    {
        if (ShowingSuggestions && Suggestions.Count > 0)
        {
            SuggestionIndex = (SuggestionIndex + 1) % Suggestions.Count;
            UpdateGhostTextFromSuggestion();
            UpdateSuggestionScrollOffset();
        }
    }

    /// <summary>
    /// Оновлює scroll offset для suggestions щоб selected був видимим
    /// </summary>
    private void UpdateSuggestionScrollOffset()
    {

        // Якщо selected виходить за верхню межу - прокручуємо вгору
        if (SuggestionIndex - 1 < SuggestionScrollOffset)
        {
            // Намагаємось зберегти 1 елемент контексту вище
            SuggestionScrollOffset = Math.Max(0, SuggestionIndex - 1);
        }
        // Якщо selected виходить за нижню межу - прокручуємо вниз
        else if (SuggestionIndex + 1 >= SuggestionScrollOffset + ConsoleInput.SUGGESTIONS_COUNT)
        {
            // Намагаємось зберегти 1 елемент контексту нижче
            SuggestionScrollOffset = Math.Min(
                Math.Max(0, Suggestions.Count - ConsoleInput.SUGGESTIONS_COUNT),
                SuggestionIndex - ConsoleInput.SUGGESTIONS_COUNT + 2
            );
        }
    }

    private void UpdateGhostTextFromSuggestion()
    {
        var suggestion = Suggestions[SuggestionIndex];
        GhostText = suggestion[Input.Length..];
    }

    /// <summary>
    /// Обробляє Tab для автокомпліту шляхів
    /// </summary>
    public void Tab()
    {
        if (!string.IsNullOrEmpty(GhostText))
        {
            ApplyGhostText();
        }
        else
        {
            ApplySelectedSuggestion();
        }
    }

    /// <summary>
    /// Повертає чи є введення
    /// </summary>
    public bool HasInput => _input.Length > 0;

    public bool HasGhostText => !string.IsNullOrEmpty(GhostText);

    public void SetInput(string text)
    {
        _input.Clear();
        _input.Append(text);
        _cursorPosition = _input.Length;
        UpdatePathSuggestions();
    }
}
