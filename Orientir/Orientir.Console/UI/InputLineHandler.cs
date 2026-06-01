using System.Text;

namespace Orientir.ConsoleApp.UI;

/// <summary>
/// Клас для обробки рядка введення з автокомплітом та навігацією
/// </summary>
public class InputLineHandler
{
    private readonly StringBuilder _input = new();
    private int _cursorPosition = 0;

    public string Input => _input.ToString();
    public string GhostText { get; private set; } = "";
    public int CursorPosition => _cursorPosition;

    public List<string> Suggestions { get; } = [];

    public bool ShowingSuggestions => Suggestions.Count > 0;

    public int SuggestionIndex { get; private set; } = 0;

    public int SuggestionScrollOffset { get; private set; } = 0;

    public List<ConsoleInput.MenuItem> Menu { get; set; } = [];

    public int MenuScrollOffset { get; private set; } = 0;

    /// <summary>
    /// Додає символ до інпуту в позицію курсору
    /// </summary>
    public void AppendChar(char c)
    {
        _input.Insert(_cursorPosition, c);
        _cursorPosition++;
        SuggestionIndex = 0;
        UpdateCommandSuggestions();
        UpdateMenuSelection();
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
        UpdateCommandSuggestions();
        UpdateMenuSelection();
    }

    private void UpdateMenuSelection()
    {
        SelectedMenuIndex =
            Menu.FindIndex(x => x.Key.Equals(_input.ToString(), StringComparison.InvariantCultureIgnoreCase));
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
        }
    }

    /// <summary>
    /// Оновлює підказки команд
    /// </summary>
    public void UpdateCommandSuggestions()
    {
        if (!_input.ToString().StartsWith('/'))
        {
            Suggestions.Clear();
            GhostText = "";
            SuggestionScrollOffset = 0;
            return;
        }

        var matches = AvailableCommands
            .Where(cmd => cmd.StartsWith(_input.ToString(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        Suggestions.Clear();
        Suggestions.AddRange(matches);
        SuggestionScrollOffset = 0;

        // Генеруємо ghost text з першої підказки
        if (matches.Count > 0 && matches[0].Length > _input.Length)
        {
            GhostText = matches[0][_input.Length..];
        }
        else
        {
            GhostText = "";
        }
    }

    /// <summary>
    /// Обробляє Tab для автокомпліту команд
    /// </summary>
    public bool TryCompleteCommand()
    {
        if (!_input.ToString().StartsWith('/'))
            return false;

        var matches = AvailableCommands
            .Where(cmd => cmd.StartsWith(_input.ToString(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            SuggestionIndex = 0;
            Suggestions.Clear();
            return false;
        }


        // При повторних натисканнях - циклічно перебирати варіанти
        if (ShowingSuggestions)
        {
            int index = (SuggestionIndex + 1) % Suggestions.Count;
            _input.Clear();
            _input.Append(Suggestions[index]);
            _cursorPosition = _input.Length;
            NavigateSuggestionsDown();
            return true;
        }

        return false;
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
            Suggestions.Clear();
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
            GhostText = Suggestions[SuggestionIndex][_input.Length..];
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
            GhostText = Suggestions[SuggestionIndex][_input.Length..];
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

    /// <summary>
    /// Оновлює scroll offset для меню щоб selected був видимим
    /// </summary>
    public void UpdateMenuScrollOffset(int availableLines)
    {
        if (Menu == null || Menu.Count == 0)
        {
            MenuScrollOffset = 0;
            return;
        }

        var visibleCount = Math.Min(availableLines, Menu.Count);

        // Якщо selected виходить за верхню межу - прокручуємо вгору
        if (SelectedMenuIndex < MenuScrollOffset)
        {
            // Намагаємось зберегти 1 елемент контексту вище
            MenuScrollOffset = Math.Max(0, SelectedMenuIndex - 1);
        }
        // Якщо selected виходить за нижню межу - прокручуємо вниз
        else if (SelectedMenuIndex >= MenuScrollOffset + visibleCount)
        {
            // Намагаємось зберегти 1 елемент контексту нижче
            MenuScrollOffset = Math.Min(
                Math.Max(0, Menu.Count - visibleCount),
                SelectedMenuIndex - visibleCount + 2
            );
        }
    }


    /// <summary>
    /// Повертає чи є введення
    /// </summary>
    public bool HasInput => _input.Length > 0;

    public List<string> AvailableCommands { get; set; } = [];
    public int SelectedMenuIndex { get; set; }
    public bool HasGhostText => !string.IsNullOrEmpty(GhostText);

    public void Tab()
    {
        if (HasGhostText)
        {
            ApplyGhostText();
        }
        else if (Input.StartsWith('/'))
        {
            TryCompleteCommand();
        }
        else
        {
            ApplySelectedSuggestion();
        }
    }

    public void SetInput(string baseDirectory)
    {
        _input.Clear();
        _input.Append(baseDirectory);
        _cursorPosition = _input.Length;
    }
}
