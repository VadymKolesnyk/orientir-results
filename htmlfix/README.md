# htmlfix

Helper для нормалізації старих HTML-звітів системи **«Орієнтир»** (VFP-експорт).

Що робить:

- читає файл у кодуванні **Windows-1251** і перезаписує в **UTF-8** (без BOM);
- змінює розширення `.htm` → `.html` (**оригінал лишається поруч**);
- модернізує розмітку:
  - `<!DOCTYPE html>` на початку;
  - `<html lang="uk">`;
  - `<meta charset="utf-8">` + `<meta name="viewport" ...>` на початку `<head>`;
  - прибирає старий `<meta charset=windows-1251>` / `Content-Type`;
  - знімає застарілі теги `<font>` та атрибути `bgcolor`, `text`, `link`,
    `vlink`, `alink`, `background`.

## Запуск

```powershell
cd htmlfix
dotnet run -- d:\orientir\doc\split2.htm
```

Кілька файлів за раз:

```powershell
dotnet run -- file1.htm file2.htm file3.htm
```

Результат — `split2.html` поруч з оригіналом `split2.htm`.
