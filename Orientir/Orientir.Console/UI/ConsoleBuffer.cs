namespace Orientir.ConsoleApp.UI;

/// <summary>
/// Клас для буферизації консольного виводу з постійним рядком введення внизу (як в Claude Code)
/// </summary>
public static class ConsoleBuffer
{
    private static readonly List<string> BUFFER = [];
    private static readonly object LOCK = new();

    /// <summary>
    /// Записує текст в буфер без переносу рядка
    /// </summary>
    public static void Write(string text)
    {
        lock (LOCK)
        {
            if (BUFFER.Count == 0)
            {
                BUFFER.Add(text);
            }
            else
            {
                BUFFER[^1] += text;
            }
        }
    }

    /// <summary>
    /// Записує текст в буфер з переносом рядка
    /// </summary>
    public static void WriteLine(string text = "")
    {
        lock (LOCK)
        {
            if (BUFFER.Count == 0 || BUFFER[^1].Length > 0)
            {
                BUFFER.Add(text);
            }
            else
            {
                BUFFER[^1] = text;
            }
        }
    }

    public static void Draw()
    {
        Console.Clear();
        Console.WriteLine(GetBuffer());
    }

    /// <summary>
    /// Очищає буфер
    /// </summary>
    public static void Clear()
    {
        lock (LOCK)
        {
            BUFFER.Clear();
        }
    }

    /// <summary>
    /// Повертає всі рядки буфера як масив (для тестування або експорту)
    /// </summary>
    public static string[] GetBufferLines()
    {
        lock (LOCK)
        {
            return BUFFER.ToArray();
        }
    }

    public static string GetBuffer()
    {
        return string.Join('\n', GetBufferLines());
    }
}
