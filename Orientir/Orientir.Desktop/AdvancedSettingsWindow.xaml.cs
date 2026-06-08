using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Orientir.Core.Models;
using Orientir.Core.Services;

namespace Orientir.Desktop;

// Діалог додаткових налаштувань змагання: прапорці балів/суми/DSQ і конфіг
// колонок (видимість на великому/малому екрані + порядок). Мутує переданий
// EventConfig лише на OK (DialogResult=true).
public partial class AdvancedSettingsWindow : Window
{
    private readonly EventConfig _event;
    private readonly SettingsService _settings;
    private readonly ObservableCollection<ColumnRow> _columns = new();
    private Point _dragStart;

    // Стан перетягування: рядок, що тягнемо; адорнер-лінія місця вставки;
    // індекс, КУДИ вставити (0..Count). -1 = ще не визначено.
    private ColumnRow? _dragged;
    private InsertionAdorner? _insertLine;
    private int _dropIndex = -1;

    // Людські назви колонок для відображення в таблиці.
    private static readonly Dictionary<string, string> Titles = new()
    {
        ["rk"]          = "Місце",
        ["full_name"]   = "Прізвище, ім'я",
        ["bib"]         = "Номер",
        ["team"]        = "Регіон",
        ["club"]        = "Клуб",
        ["start_time"]  = "Старт",
        ["result_time"] = "Результат (час)",
        ["status"]      = "Статус / DSQ",
        ["gap"]         = "Відставання від лідера",
        ["points"]      = "Бали",
        ["birth"]       = "Рік народження",
        ["qual"]        = "Розряд",
    };

    public AdvancedSettingsWindow(EventConfig ev, SettingsService settings)
    {
        InitializeComponent();
        _event = ev;
        _settings = settings;

        // Конфіг змагання → інакше глобальний типовий → інакше вшитий дефолт.
        var cfg = DisplayConfig.Parse(ev.DisplayConfigJson)
                  ?? DisplayConfig.Parse(settings.GetSettings().DefaultDisplayConfigJson)
                  ?? DisplayConfig.Default();

        // Прапорці беремо зі змагання, але для «порожнього» змагання — з обраного cfg.
        bool hasOwn = !string.IsNullOrWhiteSpace(ev.DisplayConfigJson);
        ChkPoints.IsChecked        = hasOwn ? ev.Points : cfg.Points;
        ChkSum.IsChecked           = hasOwn ? ev.Standings : (cfg.Points && cfg.Standings);
        ChkSeparateDsqLg.IsChecked = hasOwn ? ev.SeparateDsqLg : cfg.SeparateDsqLg;
        ChkSeparateDsqSm.IsChecked = hasOwn ? ev.SeparateDsqSm : cfg.SeparateDsqSm;

        LoadColumns(cfg);

        GridColumns.ItemsSource = _columns;
        UpdateAvailability();
    }

    private void LoadColumns(DisplayConfig cfg)
    {
        _columns.Clear();
        // Зберігаємо порядок із конфігу; невідомі ключі ігноруємо, відсутні —
        // доливаємо з типового набору в кінець (щоб нові колонки з'явились).
        var seen = new HashSet<string>();
        foreach (var c in cfg.Columns.OrderBy(c => c.Order))
        {
            if (!Titles.ContainsKey(c.Key) || !seen.Add(c.Key)) continue;
            _columns.Add(new ColumnRow { Key = c.Key, Title = Titles[c.Key], Lg = c.Lg, Sm = c.Sm });
        }
        foreach (var c in DisplayConfig.Default().Columns)
            if (seen.Add(c.Key))
                _columns.Add(new ColumnRow { Key = c.Key, Title = Titles[c.Key], Lg = c.Lg, Sm = c.Sm });
    }

    // Доступність галочок lg/sm для кожного рядка:
    //  • points — лише коли увімкнені бали (обидва екрани);
    //  • status (Статус/DSQ) — на тому екрані, де ввімкнено розділення;
    //  • решта — завжди. Недоступні галочки робимо disabled і гасимо (false).
    private void UpdateAvailability()
    {
        bool points = ChkPoints.IsChecked == true;
        bool sepLg  = ChkSeparateDsqLg.IsChecked == true;
        bool sepSm  = ChkSeparateDsqSm.IsChecked == true;

        ChkSum.IsEnabled = points;
        if (!points) ChkSum.IsChecked = false;

        foreach (var row in _columns)
        {
            switch (row.Key)
            {
                case "points":
                    row.LgEnabled = points;
                    row.SmEnabled = points;
                    break;
                case "status":
                    row.LgEnabled = sepLg;
                    row.SmEnabled = sepSm;
                    break;
                default:
                    row.LgEnabled = true;
                    row.SmEnabled = true;
                    break;
            }
            if (!row.LgEnabled) row.Lg = false;
            if (!row.SmEnabled) row.Sm = false;
        }
    }

    private void Flag_Changed(object sender, RoutedEventArgs e) => UpdateAvailability();

    // ---- Перетягування рядків для зміни порядку --------------------------
    private void Grid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => _dragStart = e.GetPosition(null);

    private void Grid_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        var diff = _dragStart - e.GetPosition(null);
        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        // Тягнемо лише з рядка (а не з галочки/заголовка).
        if (FindRow(e.OriginalSource as DependencyObject)?.Item is not ColumnRow row) return;

        _dragged = row;
        var rowItem = GridColumns.ItemContainerGenerator.ContainerFromItem(row) as DataGridRow;
        if (rowItem is not null) rowItem.Opacity = 0.4; // тьмяним рядок, що тягнемо
        try
        {
            DragDrop.DoDragDrop(GridColumns, row, DragDropEffects.Move);
        }
        finally
        {
            EndDrag(); // прибираємо лінію й повертаємо непрозорість у будь-якому разі
        }
    }

    // Курсор рухається над таблицею — показуємо лінію місця вставки й рахуємо,
    // куди стане рядок (над/під рядком під курсором за серединою його висоти).
    private void Grid_DragOver(object sender, DragEventArgs e)
    {
        if (_dragged is null) return;
        e.Effects = DragDropEffects.Move;

        var row = FindRow(e.OriginalSource as DependencyObject);
        double y;
        if (row is not null)
        {
            int idx = GridColumns.ItemContainerGenerator.IndexFromContainer(row);
            var p = e.GetPosition(row);
            bool below = p.Y > row.ActualHeight / 2; // нижня половина → вставка ПІД рядком
            _dropIndex = below ? idx + 1 : idx;
            // Y лінії в координатах DataGrid: верх або низ цього рядка.
            var top = row.TranslatePoint(new Point(0, below ? row.ActualHeight : 0), GridColumns);
            y = top.Y;
        }
        else
        {
            // Курсор нижче останнього рядка — вставка в кінець.
            _dropIndex = _columns.Count;
            y = GridColumns.ActualHeight;
        }
        ShowInsertLine(y);
    }

    private void Grid_DragLeave(object sender, DragEventArgs e)
    {
        // Вийшли за межі таблиці — ховаємо лінію (drop сюди вже не стане).
        var p = e.GetPosition(GridColumns);
        if (p.X < 0 || p.Y < 0 || p.X > GridColumns.ActualWidth || p.Y > GridColumns.ActualHeight)
        {
            RemoveInsertLine();
            _dropIndex = -1;
        }
    }

    // Esc під час перетягування — скасувати (лінію приберемо у finally MouseMove).
    private void Grid_QueryContinueDrag(object sender, QueryContinueDragEventArgs e)
    {
        if (e.EscapePressed) { e.Action = DragAction.Cancel; e.Handled = true; }
    }

    private void Grid_Drop(object sender, DragEventArgs e)
    {
        if (_dragged is null || _dropIndex < 0) return;
        int from = _columns.IndexOf(_dragged);
        if (from < 0) return;
        // Якщо знімаємо рядок раніше за точку вставки — індекс зсувається на 1.
        int to = _dropIndex > from ? _dropIndex - 1 : _dropIndex;
        if (to < 0) to = 0;
        if (to >= _columns.Count) to = _columns.Count - 1;
        if (to != from) _columns.Move(from, to);
        GridColumns.SelectedItem = _dragged;
    }

    // Прибирає лінію, скидає прозорість рядка й стан перетягування.
    private void EndDrag()
    {
        RemoveInsertLine();
        if (_dragged is not null &&
            GridColumns.ItemContainerGenerator.ContainerFromItem(_dragged) is DataGridRow r)
            r.Opacity = 1.0;
        _dragged = null;
        _dropIndex = -1;
    }

    private void ShowInsertLine(double y)
    {
        var layer = AdornerLayer.GetAdornerLayer(GridColumns);
        if (layer is null) return;
        if (_insertLine is null)
        {
            _insertLine = new InsertionAdorner(GridColumns);
            layer.Add(_insertLine);
        }
        _insertLine.SetY(y);
    }

    private void RemoveInsertLine()
    {
        if (_insertLine is null) return;
        AdornerLayer.GetAdornerLayer(GridColumns)?.Remove(_insertLine);
        _insertLine = null;
    }

    // Піднімаємось деревом візуалів до DataGridRow під курсором.
    private static DataGridRow? FindRow(DependencyObject? src)
    {
        while (src is not null and not DataGridRow)
            src = VisualTreeHelper.GetParent(src);
        return src as DataGridRow;
    }

    // Збирає DisplayConfig із поточного стану форми (прапорці + рядки колонок).
    private DisplayConfig BuildConfig()
    {
        bool points = ChkPoints.IsChecked == true;
        return new DisplayConfig
        {
            Version       = 1,
            Points        = points,
            Standings     = points && ChkSum.IsChecked == true, // Сума лише разом із балами
            SeparateDsqLg = ChkSeparateDsqLg.IsChecked == true,
            SeparateDsqSm = ChkSeparateDsqSm.IsChecked == true,
            Columns = _columns.Select((r, idx) => new ColumnConfig
            {
                Key = r.Key, Order = idx, Lg = r.Lg, Sm = r.Sm,
            }).ToList(),
        };
    }

    // Зберігає поточний вигляд як глобальний типовий (для нових змагань).
    private void UseAsDefault_Click(object sender, RoutedEventArgs e)
    {
        _settings.SetDefaultDisplayConfig(BuildConfig().Serialize());
        MessageBox.Show("Поточний вигляд збережено як типовий для нових змагань.",
            "Orientir", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var cfg = BuildConfig();
        _event.Points        = cfg.Points;
        _event.Standings     = cfg.Standings;
        _event.SeparateDsqLg = cfg.SeparateDsqLg;
        _event.SeparateDsqSm = cfg.SeparateDsqSm;
        _event.DisplayConfigJson = cfg.Serialize();
        DialogResult = true;
    }

    // Рядок таблиці колонок. INotifyPropertyChanged — щоб гасіння (Enabled→
    // false) і скидання Lg/Sm одразу відображались у DataGrid.
    public class ColumnRow : INotifyPropertyChanged
    {
        public string Key { get; set; } = "";
        public string Title { get; set; } = "";

        private bool _lg;
        public bool Lg { get => _lg; set { _lg = value; OnChanged(nameof(Lg)); } }

        private bool _sm;
        public bool Sm { get => _sm; set { _sm = value; OnChanged(nameof(Sm)); } }

        private bool _lgEnabled = true;
        public bool LgEnabled { get => _lgEnabled; set { _lgEnabled = value; OnChanged(nameof(LgEnabled)); } }

        private bool _smEnabled = true;
        public bool SmEnabled { get => _smEnabled; set { _smEnabled = value; OnChanged(nameof(SmEnabled)); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    // Адорнер-лінія, що показує місце вставки рядка під час перетягування.
    // Малюємо горизонтальну лінію з трикутниками-маркерами на кінцях.
    private sealed class InsertionAdorner : Adorner
    {
        private static readonly Pen LinePen = MakePen();
        private double _y;

        public InsertionAdorner(UIElement adorned) : base(adorned)
        {
            IsHitTestVisible = false;
        }

        public void SetY(double y)
        {
            _y = y;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ((FrameworkElement)AdornedElement).ActualWidth;
            double y = Math.Max(1, Math.Min(_y, ((FrameworkElement)AdornedElement).ActualHeight - 1));
            dc.DrawLine(LinePen, new Point(0, y), new Point(w, y));
            // Трикутники-маркери на кінцях для наочності.
            DrawCaret(dc, 0, y, +1);
            DrawCaret(dc, w, y, -1);
        }

        private static void DrawCaret(DrawingContext dc, double x, double y, int dir)
        {
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(new Point(x, y - 5), true, true);
                c.LineTo(new Point(x + dir * 8, y), true, false);
                c.LineTo(new Point(x, y + 5), true, false);
            }
            g.Freeze();
            dc.DrawGeometry(LinePen.Brush, null, g);
        }

        private static Pen MakePen()
        {
            var p = new Pen(new SolidColorBrush(Color.FromRgb(0x22, 0x2a, 0x92)), 2.5);
            p.Freeze();
            return p;
        }
    }
}
