using System.Text;
using System.Text.RegularExpressions;

namespace Orientir.Core;

// Приводить старі HTML-звіти (cp1251, .htm) до сучасного вигляду:
// читає у Windows-1251, зберігає в UTF-8 з розширенням .html і оновлює
// <html>/<head> під HTML5.
//
// ВАЖЛИВО: presentational-розмітку (bgcolor на комірках, <font color>,
// кольори посилань у <body>) НЕ чіпаємо — у звітах "Орієнтир" саме нею
// задано кольорове кодування спліт-таблиць (зелений/жовтий/червоний фон,
// сині заголовки). Прибирання цих атрибутів робить таблицю безбарвною.
public static class HtmlFix
{
    // Приймає шлях до старого .htm (cp1251), створює поруч .html (utf-8)
    // з модернізованою розміткою. Оригінал не чіпаємо. Повертає шлях виходу.
    public static string Convert(string srcPath)
    {
        if (!File.Exists(srcPath))
            throw new FileNotFoundException($"Файл не знайдено: {srcPath}");

        // 1) Читаємо у Windows-1251 (кирилиця старих VFP-звітів).
        var cp1251 = Encoding.GetEncoding(1251);
        string html = File.ReadAllText(srcPath, cp1251);

        // 2) Модернізуємо лише head/doctype (вміст body лишаємо як є).
        html = Modernize(html);

        // 3) Вихідний шлях: те саме ім'я, але .html.
        string dst = Path.ChangeExtension(srcPath, ".html");

        // 4) Пишемо в UTF-8 без BOM.
        File.WriteAllText(dst, html, new UTF8Encoding(false));

        return dst;
    }

    // Базова модернізація <head>/DOCTYPE. Усе інше (таблиці, кольори,
    // стилі) лишаємо без змін.
    static string Modernize(string html)
    {
        // Прибрати старі <meta charset> / <meta http-equiv="Content-Type">
        // (вони вказували windows-1251 — після перекодування це брехня).
        html = Regex.Replace(html,
            @"<meta\b[^>]*\bcharset\b[^>]*>", "",
            RegexOptions.IgnoreCase);
        html = Regex.Replace(html,
            @"<meta\b[^>]*\bhttp-equiv\s*=\s*[""']?content-type[""']?[^>]*>", "",
            RegexOptions.IgnoreCase);

        // Прибрати будь-який старий DOCTYPE (HTML5-варіант додамо в кінці).
        html = Regex.Replace(html,
            @"<!DOCTYPE[^>]*>", "",
            RegexOptions.IgnoreCase).TrimStart();

        // <html ...> → додаємо lang="uk", зберігаючи наявні атрибути.
        if (Regex.IsMatch(html, @"<html\b", RegexOptions.IgnoreCase))
        {
            if (!Regex.IsMatch(html, @"<html\b[^>]*\blang\s*=", RegexOptions.IgnoreCase))
                html = Regex.Replace(html,
                    @"<html\b", "<html lang=\"uk\"",
                    RegexOptions.IgnoreCase);
        }

        // Сучасні meta, які вставимо одразу після <head>.
        const string headMeta =
            "\n  <meta charset=\"utf-8\">\n" +
            "  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">";

        if (Regex.IsMatch(html, @"<head\b", RegexOptions.IgnoreCase))
        {
            // Вставляємо meta одразу після відкривального <head ...>,
            // не чіпаючи його атрибути.
            html = Regex.Replace(html,
                @"(<head\b[^>]*>)", "$1" + headMeta,
                RegexOptions.IgnoreCase);
        }
        else
        {
            // Немає <head> — обгортаємо весь вміст коректним каркасом.
            html = $"<head>{headMeta}\n</head>\n<body>\n{html}\n</body>";
        }

        // Гарантуємо HTML5-DOCTYPE на самому початку.
        return "<!DOCTYPE html>\n" + html.TrimStart();
    }
}
