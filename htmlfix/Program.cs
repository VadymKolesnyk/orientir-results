using System.Text;
using Htmlfix;

// =====================================================================
//  htmlfix — приводить старі HTML-звіти (система "Орієнтир", VFP-експорт)
//  до сучасного вигляду:
//    • читає файл у Windows-1251 і перезаписує в UTF-8;
//    • змінює розширення .htm → .html (оригінал лишаємо поруч);
//    • оновлює <html>/<head> під HTML5 і прибирає застаріле.
//
//  Запуск:  dotnet run -- d:\orientir\doc\split2.htm
//           dotnet run -- file1.htm file2.htm ...   (кілька файлів)
// =====================================================================

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // для cp1251
Console.OutputEncoding = Encoding.UTF8;

if (args.Length == 0)
{
    Console.WriteLine("Вкажи шлях до файлу: dotnet run -- d:\\orientir\\doc\\split2.htm");
    return 1;
}

int errors = 0;
foreach (var src in args)
{
    try
    {
        var dst = HtmlFix.Convert(src);
        Console.WriteLine($"OK: {src}  →  {dst}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ПОМИЛКА [{src}]: {ex.Message}");
        errors++;
    }
}
return errors == 0 ? 0 : 1;
