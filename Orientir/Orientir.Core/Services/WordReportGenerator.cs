using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Orientir.Core.Models;

namespace Orientir.Core.Services;

// Генерує друкований Word-протокол зведеного заліку («Сума» за всі дні) у стилі
// офіційного протоколу: плашка (клуб, назва змагань, заголовок, дата/регіон),
// таблиця по кожній групі (місце | ПіБ | ДН | регіон | клуб | [день: М/Час/Очки]×N
// | Сума), підписи головного судді/секретаря.
//
// Логіка зведення — порт renderSum() з online/results.html: учасник = bib у межах
// групи, бали за всі дні підсумовуємо, сортуємо спадно, місце = позиція в списку.
public static class WordReportGenerator
{
    public enum Orientation { Landscape, Portrait }

    // Створює .docx за шляхом outputPath. Повертає той самий шлях.
    // orientation — орієнтація сторінки (за замовчуванням альбомна).
    public static string Generate(EventConfig ev, string outputPath,
                                  Orientation orientation = Orientation.Landscape)
    {
        var days = ev.ResolveDays();
        var meta = EventMetaReader.ReadEventMeta(ev);

        // Збираємо результати + перелік груп (за ord найранішого дня).
        var groupOrder = new List<string>();
        var seenGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // grp -> bib -> SumRow
        var byGroup = new Dictionary<string, Dictionary<int, SumRow>>(StringComparer.OrdinalIgnoreCase);

        foreach (var d in days)
        {
            foreach (var g in ResultsReader.ReadGroups(ev, d))
            {
                var name = (string?)g["name"] ?? "";
                if (!string.IsNullOrWhiteSpace(name) && seenGroups.Add(name))
                    groupOrder.Add(name);
            }

            foreach (var r in ResultsReader.ReadResults(ev, d))
            {
                var grp = (string?)r["grp"] ?? "";
                var bib = (int?)r["bib"];
                if (string.IsNullOrWhiteSpace(grp) || bib is null) continue;

                if (seenGroups.Add(grp)) groupOrder.Add(grp); // група лише в результатах

                if (!byGroup.TryGetValue(grp, out var people))
                    byGroup[grp] = people = new Dictionary<int, SumRow>();
                if (!people.TryGetValue(bib.Value, out var row))
                    people[bib.Value] = row = new SumRow { Bib = bib.Value };

                var status = (string?)r["status"] ?? "";
                bool finished = status is "finished" or "finished_pending";
                int? place = (status == "finished") ? (int?)r["rk"] : null;
                string? time = finished ? (string?)r["result_time"] : null;
                decimal pts = (decimal?)r["points"] ?? 0m;

                row.ByDay[d.Day] = new DayCell(place, time, pts, finished);
                row.Total += pts;

                // Найсвіжіші непорожні значення з будь-якого дня.
                var name = (string?)r["full_name"] ?? "";
                var region = (string?)r["region"] ?? "";
                var club = (string?)r["club"] ?? "";
                var birth = (string?)r["birth"] ?? "";
                if (!string.IsNullOrWhiteSpace(name)) row.Name = name;
                if (!string.IsNullOrWhiteSpace(region)) row.Region = region;
                if (!string.IsNullOrWhiteSpace(club)) row.Club = club;
                if (!string.IsNullOrWhiteSpace(birth)) row.Birth = birth;
            }
        }

        WriteDocx(outputPath, ev, meta, days, groupOrder, byGroup, orientation);
        return outputPath;
    }

    // Ширина контенту сторінки (A4 мінус ліве+праве поля по 720), залежить від
    // орієнтації. Встановлюється на початку WriteDocx; нею тягнемо всі таблиці.
    [ThreadStatic] static int _pageContentTwips;

    // ---------------------------------------------------------------------
    //  Запис .docx
    // ---------------------------------------------------------------------
    static void WriteDocx(string path, EventConfig ev, EventMeta meta, List<DayConfig> days,
                          List<string> groupOrder, Dictionary<string, Dictionary<int, SumRow>> byGroup,
                          Orientation orientation)
    {
        bool landscape = orientation == Orientation.Landscape;
        // A4: 11906 × 16838 twips. У альбомній міняємо ширину/висоту місцями.
        int pageWidth = landscape ? 16838 : 11906;
        int pageHeight = landscape ? 11906 : 16838;
        _pageContentTwips = pageWidth - 720 - 720;

        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var main = doc.AddMainDocumentPart();
        main.Document = new Document();
        var body = main.Document.AppendChild(new Body());

        // --- Плашка ---
        if (!string.IsNullOrWhiteSpace(meta.OrgName))
            body.AppendChild(CenterPara(meta.OrgName, bold: true));
        var title = !string.IsNullOrWhiteSpace(meta.EventTitle) ? meta.EventTitle : ev.Title;
        if (!string.IsNullOrWhiteSpace(title))
            body.AppendChild(CenterPara(title, bold: true, sizePt: 14));
        body.AppendChild(CenterPara("ПРОТОКОЛ  РЕЗУЛЬТАТІВ ЗМАГАНЬ З ОРІЄНТУВАННЯ", bold: true));
        body.AppendChild(CenterPara($"Підсумковий залік (сума за {days.Count} дн.)"));

        // Дата (ліворуч) + регіон (праворуч) — таблиця 1×2 без рамок.
        if (!string.IsNullOrWhiteSpace(meta.Period) || !string.IsNullOrWhiteSpace(meta.Region))
            body.AppendChild(LeftRightRow(meta.Period, meta.Region));

        body.AppendChild(EmptyPara());

        // --- Групи ---
        foreach (var grp in groupOrder)
        {
            if (!byGroup.TryGetValue(grp, out var people) || people.Count == 0)
                continue; // група без учасників/результатів — пропускаємо

            body.AppendChild(Para($"Вікова група  {grp}", bold: true));

            var rows = people.Values.ToList();
            rows.Sort((a, b) => CompareSummary(a, b, days));

            body.AppendChild(BuildGroupTable(days, rows));
            body.AppendChild(EmptyPara());
        }

        // --- Підписи ---
        // GLAV_SUD/GLAV_SEK у DBF уже містять «посада {пробіли} ПІБ»; ділимо на
        // ліву/праву частину по групі пробілів (із запасним лейблом, якщо її нема).
        body.AppendChild(EmptyPara());
        if (!string.IsNullOrWhiteSpace(meta.HeadJudge))
            body.AppendChild(SignatureRow(meta.HeadJudge, "Головний суддя"));
        if (!string.IsNullOrWhiteSpace(meta.HeadSecretary))
            body.AppendChild(SignatureRow(meta.HeadSecretary, "Головний секретар"));

        // Колонтитул із номером сторінки (праворуч внизу).
        var footerRef = AddPageNumberFooter(main);

        // Розмір/орієнтація сторінки (за вибором користувача).
        // Порядок у SectionProperties за схемою: спершу footer-посилання, далі pgSz/pgMar.
        body.AppendChild(new SectionProperties(
            footerRef,
            new PageSize
            {
                Width = (uint)pageWidth,
                Height = (uint)pageHeight,
                Orient = landscape ? PageOrientationValues.Landscape : PageOrientationValues.Portrait,
            },
            new PageMargin { Top = 720, Bottom = 720, Left = 720, Right = 720, Header = 360, Footer = 360, Gutter = 0 }));

        main.Document.Save();
    }

    // Створює FooterPart із номером сторінки (праворуч) і повертає посилання
    // на нього для вставки в SectionProperties.
    static FooterReference AddPageNumberFooter(MainDocumentPart main)
    {
        var footerPart = main.AddNewPart<FooterPart>();
        footerPart.Footer = new Footer(MakePageField());
        footerPart.Footer.Save();

        var id = main.GetIdOfPart(footerPart);
        return new FooterReference { Type = HeaderFooterValues.Default, Id = id };
    }

    // Run-послідовність поля { PAGE } для номера сторінки.
    static IEnumerable<OpenXmlElement> MakePageFieldRuns()
    {
        RunProperties Rp() => new(new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman" },
                                  new FontSize { Val = "18" });
        yield return new Run(Rp(), new FieldChar { FieldCharType = FieldCharValues.Begin });
        yield return new Run(Rp(), new FieldCode(" PAGE ") { Space = SpaceProcessingModeValues.Preserve });
        yield return new Run(Rp(), new FieldChar { FieldCharType = FieldCharValues.End });
    }

    static Paragraph MakePageField()
    {
        var p = new Paragraph(
            new ParagraphProperties(
                new SpacingBetweenLines { After = "0", Before = "0" },
                new Justification { Val = JustificationValues.Right }));
        foreach (var r in MakePageFieldRuns()) p.AppendChild(r);
        return p;
    }

    static Table BuildGroupTable(List<DayConfig> days, List<SumRow> rows)
    {
        var table = new Table(DefaultTableProps());

        // Ширини колонок у twips нормуємо так, щоб у сумі завжди було рівно
        // _pageContentTwips (ширина сторінки мінус поля). Денних блоків однаково
        // для всіх груп → усі таблиці виходять однакової ширини й на всю сторінку.
        int[] baseW = { 600, 2400, 900, 1500, 1500 }; // Місце, ПіБ, ДН, Регіон, Клуб
        int sumW = 700;                                // Сума
        int perDayW = 1400;                            // ширина одного денного блоку (М+Час+Очки)
        int rawTotal = baseW.Sum() + sumW + days.Count * perDayW;
        double k = (double)_pageContentTwips / rawTotal; // коефіцієнт нормування до ширини сторінки

        int Wbase(int i) => (int)Math.Round(baseW[i] * k);
        int Wsum = (int)Math.Round(sumW * k);
        // День ділимо на М/Час/Очки у пропорції 5:9:7 (як було).
        int dayUnit = (int)Math.Round(perDayW * k);
        int Wm = (int)Math.Round(dayUnit * 5.0 / 21), Wt = (int)Math.Round(dayUnit * 9.0 / 21);
        int Wp = dayUnit - Wm - Wt;

        var widths = new List<int>();
        for (int i = 0; i < baseW.Length; i++) widths.Add(Wbase(i));
        foreach (var _ in days) { widths.Add(Wm); widths.Add(Wt); widths.Add(Wp); }
        widths.Add(Wsum);

        // Сітка колонок.
        var grid = new TableGrid();
        foreach (var w in widths)
            grid.AppendChild(new GridColumn { Width = w.ToString() });
        table.AppendChild(grid);

        // Зручні зрізи ширин: 0-4 базові, далі по 3 на день, останній — Сума.
        int W(int col) => widths[col];
        int DayBlockW(int dayIdx) => W(5 + dayIdx * 3) + W(5 + dayIdx * 3 + 1) + W(5 + dayIdx * 3 + 2);
        int sumCol = widths.Count - 1;

        // --- Шапка (2 рядки) ---
        var h1 = new TableRow();
        h1.AppendChild(HeaderCell("Місце", W(0), vmerge: VMerge.Restart));
        h1.AppendChild(HeaderCell("Прізвище, ім'я", W(1), vmerge: VMerge.Restart));
        h1.AppendChild(HeaderCell("ДН", W(2), vmerge: VMerge.Restart));
        h1.AppendChild(HeaderCell("Регіон", W(3), vmerge: VMerge.Restart));
        h1.AppendChild(HeaderCell("Клуб", W(4), vmerge: VMerge.Restart));
        for (int di = 0; di < days.Count; di++)
            h1.AppendChild(HeaderCell(DayTitle(days[di]), DayBlockW(di), gridSpan: 3));
        h1.AppendChild(HeaderCell("Сума", W(sumCol), vmerge: VMerge.Restart));
        table.AppendChild(h1);

        var h2 = new TableRow();
        for (int i = 0; i < 5; i++) h2.AppendChild(HeaderCell(null, W(i), vmerge: VMerge.Continue));
        for (int di = 0; di < days.Count; di++)
        {
            h2.AppendChild(HeaderCell("М", W(5 + di * 3)));
            h2.AppendChild(HeaderCell("Час", W(5 + di * 3 + 1)));
            h2.AppendChild(HeaderCell("Очки", W(5 + di * 3 + 2)));
        }
        h2.AppendChild(HeaderCell(null, W(sumCol), vmerge: VMerge.Continue));
        table.AppendChild(h2);

        // --- Дані ---
        int rank = 0;
        foreach (var r in rows)
        {
            rank++;
            var tr = new TableRow();
            tr.AppendChild(DataCell(rank.ToString(), W(0), center: true));
            tr.AppendChild(DataCell(r.Name, W(1)));
            tr.AppendChild(DataCell(FormatBirth(r.Birth), W(2), center: true));
            tr.AppendChild(DataCell(r.Region, W(3)));
            tr.AppendChild(DataCell(r.Club, W(4)));
            for (int di = 0; di < days.Count; di++)
            {
                var c = r.ByDay.TryGetValue(days[di].Day, out var cell) ? cell : null;
                var place = (c?.Place)?.ToString() ?? "–";
                var time = (c is { Finished: true }) ? (c.Time ?? "–") : "–";
                var pts = c is null ? "0.00" : FormatPts(c.Points);
                tr.AppendChild(DataCell(place, W(5 + di * 3), center: true));
                tr.AppendChild(DataCell(time, W(5 + di * 3 + 1), center: true));
                tr.AppendChild(DataCell(pts, W(5 + di * 3 + 2), center: true));
            }
            tr.AppendChild(DataCell(FormatPts(r.Total), W(sumCol), center: true, bold: true));
            table.AppendChild(tr);
        }

        return table;
    }

    // ---------------------------------------------------------------------
    //  OpenXml-хелпери
    // ---------------------------------------------------------------------
    static TableProperties DefaultTableProps() => new(
        // Фіксована розкладка + ширина на всю сторінку: Word дотримується
        // заданих ширин колонок, не звужуючи таблицю під вміст.
        new TableWidth { Width = _pageContentTwips.ToString(), Type = TableWidthUnitValues.Dxa },
        new TableLayout { Type = TableLayoutValues.Fixed });
        // Рамок на рівні таблиці немає — їх задаємо лише коміркам шапки (HeaderCell).

    enum VMerge { None, Restart, Continue }

    // Рамки лише для заголовків таблиці. Порядок дочірніх елементів за схемою:
    // top, left, bottom, right (далі insideH/insideV — нам не потрібні).
    static TableCellBorders HeaderBorders() => new(
        new TopBorder { Val = BorderValues.Single, Size = 4 },
        new LeftBorder { Val = BorderValues.Single, Size = 4 },
        new BottomBorder { Val = BorderValues.Single, Size = 4 },
        new RightBorder { Val = BorderValues.Single, Size = 4 });

    static TableCell HeaderCell(string? text, int widthTwips, int gridSpan = 1, VMerge vmerge = VMerge.None)
    {
        // Порядок дочірніх елементів tcPr за схемою:
        // tcW → gridSpan → vMerge → tcBorders → vAlign.
        var props = new TableCellProperties(
            new TableCellWidth { Width = widthTwips.ToString(), Type = TableWidthUnitValues.Dxa });
        if (gridSpan > 1) props.AppendChild(new GridSpan { Val = gridSpan });
        if (vmerge == VMerge.Restart) props.AppendChild(new VerticalMerge { Val = MergedCellValues.Restart });
        else if (vmerge == VMerge.Continue) props.AppendChild(new VerticalMerge { Val = MergedCellValues.Continue });
        props.AppendChild(HeaderBorders());
        props.AppendChild(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });

        return new TableCell(props, CellPara(text ?? "", bold: true, center: true));
    }

    static TableCell DataCell(string? text, int widthTwips, bool center = false, bool bold = false) =>
        new(new TableCellProperties(
                new TableCellWidth { Width = widthTwips.ToString(), Type = TableWidthUnitValues.Dxa }),
            CellPara(text ?? "", bold: bold, center: center));

    // Абзац усередині комірки таблиці (кожна комірка мусить мати ≥1 абзац).
    // ВАЖЛИВО: задаємо й розмір МАРКЕРА абзацу (rPr у pPr) — інакше у порожніх
    // комірках (напр. без клубу) Word бере для маркера дефолтні 12pt і рядок
    // стає вищим за сусідні. Тримаємо 9pt скрізь.
    static Paragraph CellPara(string text, bool bold = false, bool center = false)
    {
        var markRp = new ParagraphMarkRunProperties(new FontSize { Val = "18" }); // 9pt = 18 напівпунктів
        var pp = new ParagraphProperties(new SpacingBetweenLines { After = "0", Before = "0" });
        if (center) pp.AppendChild(new Justification { Val = JustificationValues.Center });
        pp.AppendChild(markRp);
        return new Paragraph(pp, Run(text, bold: bold, sizePt: 9));
    }

    static Paragraph Para(string text, bool bold = false, double sizePt = 11) =>
        new(new ParagraphProperties(new SpacingBetweenLines { After = "40", Before = "40" }),
            Run(text, bold: bold, sizePt: sizePt));

    static Paragraph CenterPara(string text, bool bold = false, double sizePt = 11) =>
        new(new ParagraphProperties(
                new SpacingBetweenLines { After = "20", Before = "20" },
                new Justification { Val = JustificationValues.Center }),
            Run(text, bold: bold, sizePt: sizePt));

    static Paragraph EmptyPara() => new(Run("", sizePt: 6));

    // Рядок підпису: ділимо «посада {пробіли} ПІБ» на ліву/праву частину.
    // Якщо в значенні посади нема (просто ПІБ) — підставляємо запасний лейбл.
    static Table SignatureRow(string value, string fallbackLabel)
    {
        var parts = System.Text.RegularExpressions.Regex.Split(value.Trim(), @"\s{2,}");
        string left, right;
        if (parts.Length >= 2)
        {
            left = parts[0].Trim();
            right = string.Join(" ", parts.Skip(1)).Trim();
        }
        else
        {
            left = fallbackLabel;
            right = value.Trim();
        }
        return LeftRightRow(left, right);
    }

    // Рядок «ліворуч … праворуч» через таблицю 1×2 без рамок.
    static Table LeftRightRow(string left, string right)
    {
        var t = new Table(
            new TableProperties(
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                new TableBorders(
                    new TopBorder { Val = BorderValues.None },
                    new LeftBorder { Val = BorderValues.None },
                    new BottomBorder { Val = BorderValues.None },
                    new RightBorder { Val = BorderValues.None },
                    new InsideHorizontalBorder { Val = BorderValues.None },
                    new InsideVerticalBorder { Val = BorderValues.None })),
            new TableGrid(new GridColumn { Width = "5000" }, new GridColumn { Width = "5000" }));

        var tr = new TableRow();
        tr.AppendChild(new TableCell(
            new TableCellProperties(new TableCellWidth { Width = "2500", Type = TableWidthUnitValues.Pct }),
            new Paragraph(Run(left, sizePt: 11))));
        tr.AppendChild(new TableCell(
            new TableCellProperties(new TableCellWidth { Width = "2500", Type = TableWidthUnitValues.Pct }),
            new Paragraph(
                new ParagraphProperties(new Justification { Val = JustificationValues.Right }),
                Run(right, sizePt: 11))));
        t.AppendChild(tr);
        return t;
    }

    static Run Run(string text, bool bold = false, double sizePt = 11)
    {
        var rp = new RunProperties(new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", ComplexScript = "Times New Roman" });
        if (bold) rp.AppendChild(new Bold());
        rp.AppendChild(new FontSize { Val = ((int)(sizePt * 2)).ToString() }); // напівпункти
        return new Run(rp, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }

    static string DayTitle(DayConfig d) =>
        string.IsNullOrWhiteSpace(d.Label) ? $"День {d.Day}" : $"День {d.Day} ({d.Label})";

    // Бали — завжди 2 знаки (як fmtPts у results.html).
    static string FormatPts(decimal p) => p.ToString("F2", CultureInfo.InvariantCulture);

    // ДН: рік як є, якщо 4 цифри; якщо дата (з крапками) — лишаємо як є; інакше Trim.
    static string FormatBirth(string birth)
    {
        var s = (birth ?? "").Trim();
        return s;
    }

    // Сортування заліку «Сума» з тай-брейком за рівної суми балів:
    //   1) більша сума балів;
    //   2) більше результатів (фінішів, не DSQ);
    //   3) від останнього дня до першого: більше балів того дня, а за рівних —
    //      краще місце того дня;
    //   4) у крайньому разі — за алфавітом (ПіБ).
    static int CompareSummary(SumRow a, SumRow b, List<DayConfig> days)
    {
        if (a.Total != b.Total) return b.Total.CompareTo(a.Total); // більша сума — вище

        int fa = FinishCount(a), fb = FinishCount(b);
        if (fa != fb) return fb.CompareTo(fa); // більше результатів — вище

        // Від останнього дня до першого: спершу бали, потім місце того ж дня.
        for (int i = days.Count - 1; i >= 0; i--)
        {
            var ca = a.ByDay.TryGetValue(days[i].Day, out var x) ? x : null;
            var cb = b.ByDay.TryGetValue(days[i].Day, out var y) ? y : null;

            decimal pa = ca?.Points ?? 0m, pb = cb?.Points ?? 0m;
            if (pa != pb) return pb.CompareTo(pa); // більше балів — вище

            int ra = ca?.Place ?? int.MaxValue, rb = cb?.Place ?? int.MaxValue;
            if (ra != rb) return ra.CompareTo(rb); // краще (менше) місце — вище
        }

        // Усе однакове — за алфавітом ПіБ (українська локаль).
        return System.Globalization.CultureInfo.GetCultureInfo("uk-UA")
            .CompareInfo.Compare(a.Name, b.Name, System.Globalization.CompareOptions.None);
    }

    // К-ть «результатів»: дні, де учасник фінішував (не зняття/не-старт).
    static int FinishCount(SumRow r) => r.ByDay.Values.Count(c => c.Finished);

    // ---------------------------------------------------------------------
    //  Внутрішні моделі зведення
    // ---------------------------------------------------------------------
    record DayCell(int? Place, string? Time, decimal Points, bool Finished);

    sealed class SumRow
    {
        public int Bib;
        public string Name = "";
        public string Region = "";
        public string Club = "";
        public string Birth = "";
        public Dictionary<int, DayCell> ByDay = new();
        public decimal Total;
    }
}
