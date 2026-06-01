using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Orientir.Core;
using Orientir.Core.Models;
using Orientir.Core.Services;

namespace Orientir.Desktop;

public partial class MainWindow : Window
{
    private readonly SettingsService _settings;
    private EventConfig? _current;          // обране/редаговане змагання
    private CancellationTokenSource? _cts;  // активна публікація

    public MainWindow()
    {
        InitializeComponent();

        var dbPath = Path.Combine(AppContext.BaseDirectory, "orientir-settings.db");
        _settings = new SettingsService(dbPath);

        var legacy = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        _settings.ImportLegacyIfEmpty(legacy);

        SetSettingsEditMode(false); // стартуємо в режимі перегляду (ключ замаскований)
        ReloadEvents();
        Closed += (_, _) => { _cts?.Cancel(); _settings.Dispose(); };
    }

    // ====================================================================
    //  Налаштування (перегляд за замовчуванням, ключ замаскований;
    //  редагування — лише після кнопки «Редагувати»)
    // ====================================================================
    private bool _editingSettings;

    private void LoadSettings()
    {
        var s = _settings.GetSettings();
        TxtUrl.Text = s.SupabaseUrl;
        TxtInterval.Text = s.IntervalSeconds.ToString();
        TxtPublicUrl.Text = s.PublicBaseUrl;
        // У перегляді ключ замаскований; у редагуванні — повний.
        TxtKey.Text = _editingSettings ? s.ServiceRoleKey : MaskKey(s.ServiceRoleKey);
    }

    private void SetSettingsEditMode(bool editing)
    {
        _editingSettings = editing;
        var ro = !editing;
        var bg = editing ? System.Windows.Media.Brushes.White
                         : (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#F3F3F3")!;
        foreach (var tb in new[] { TxtUrl, TxtKey, TxtInterval, TxtPublicUrl })
        {
            tb.IsReadOnly = ro;
            tb.Background = bg;
        }
        BtnEditSettings.Visibility = editing ? Visibility.Collapsed : Visibility.Visible;
        BtnSaveSettings.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        BtnCancelSettings.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        LoadSettings(); // перемальовуємо значення (показати/замаскувати ключ)
    }

    private void EditSettings_Click(object sender, RoutedEventArgs e) => SetSettingsEditMode(true);

    private void CancelSettings_Click(object sender, RoutedEventArgs e) => SetSettingsEditMode(false);

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        var s = _settings.GetSettings();
        s.SupabaseUrl = TxtUrl.Text.Trim();
        s.ServiceRoleKey = TxtKey.Text.Trim();
        s.IntervalSeconds = int.TryParse(TxtInterval.Text.Trim(), out var n) ? n : 10;
        s.PublicBaseUrl = TxtPublicUrl.Text.Trim();
        _settings.SaveSettings(s);
        SetSettingsEditMode(false);
        MessageBox.Show("Налаштування збережено.", "Orientir", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static string MaskKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return "(не задано)";
        if (key.Length <= 8) return new string('•', key.Length);
        return $"{key[..4]}{new string('•', 8)}{key[^4..]}";
    }

    // ====================================================================
    //  Змагання
    // ====================================================================
    private void ReloadEvents()
    {
        var events = _settings.GetEvents();
        GridEvents.ItemsSource = events;
    }

    private void GridEvents_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _current = GridEvents.SelectedItem as EventConfig;
        BindEventEditor(_current);
    }

    private void BindEventEditor(EventConfig? ev)
    {
        TxtEvSlug.Text      = ev?.Slug ?? "";
        TxtEvTitle.Text     = ev?.Title ?? "";
        TxtEvSubtitle.Text  = ev?.Subtitle ?? "";
        TxtEvBasePath.Text  = ev?.BasePath ?? "";
        ChkStandings.IsChecked = ev?.Standings ?? false;
        TxtEvActiveDay.Text = ev?.ActiveDay?.ToString() ?? "";
    }

    private void NewEvent_Click(object sender, RoutedEventArgs e)
    {
        _current = null;
        GridEvents.SelectedItem = null;
        BindEventEditor(null);
        TxtEvSlug.Focus();
    }

    private void BrowseBasePath_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Тека змагання" };
        if (!string.IsNullOrWhiteSpace(TxtEvBasePath.Text) && Directory.Exists(TxtEvBasePath.Text))
            dlg.InitialDirectory = TxtEvBasePath.Text;
        if (dlg.ShowDialog() == true)
            TxtEvBasePath.Text = dlg.FolderName;
    }

    private void SaveEvent_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtEvSlug.Text) || string.IsNullOrWhiteSpace(TxtEvBasePath.Text))
        {
            MessageBox.Show("Slug і шлях обов'язкові.", "Orientir", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        int? activeDay = int.TryParse(TxtEvActiveDay.Text.Trim(), out var ad) ? ad : (int?)null;
        bool isNew = _current is null;
        var ev = _current ?? new EventConfig();
        ev.Slug      = TxtEvSlug.Text.Trim();
        ev.Title     = TxtEvTitle.Text.Trim();
        ev.Subtitle  = TxtEvSubtitle.Text.Trim();
        ev.BasePath  = TxtEvBasePath.Text.Trim();
        ev.Standings = ChkStandings.IsChecked == true;
        ev.ActiveDay = activeDay;

        if (isNew)
        {
            _settings.AddEvent(ev);
            ReloadEvents();
            _current = ev;
            // Для нового змагання одразу відкриваємо дні: сканує D_1..D_n і
            // дає ввести лише підписи.
            var win = new DaysWindow(ev, autoScan: true) { Owner = this };
            if (win.ShowDialog() == true)
                _settings.SaveEventDays(ev);
            ReloadEvents();
            GridEvents.SelectedItem = _settings.GetEvents().FirstOrDefault(x => x.Id == ev.Id);
        }
        else
        {
            _settings.UpdateEvent(ev);
            ReloadEvents();
            MessageBox.Show("Збережено.", "Orientir", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    // Збереження прапорця «Активне» одразу після редагування комірки.
    private void GridEvents_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.Row.Item is EventConfig ev)
        {
            // Значення вже застосоване до об'єкта (TwoWay binding); зберігаємо в БД.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _settings.UpdateEvent(ev);
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
    }

    private void DeleteEvent_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null)
        {
            MessageBox.Show("Оберіть змагання у таблиці.", "Orientir");
            return;
        }
        if (MessageBox.Show($"Видалити «{_current.Slug}»?", "Orientir",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _settings.DeleteEvent(_current.Id);
            _current = null;
            BindEventEditor(null);
            ReloadEvents();
        }
    }

    private void EditDays_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null)
        {
            MessageBox.Show("Спершу оберіть і збережіть змагання.", "Orientir");
            return;
        }
        var ev = _settings.GetEvents().First(x => x.Id == _current.Id);
        var win = new DaysWindow(ev) { Owner = this };
        if (win.ShowDialog() == true)
        {
            _settings.SaveEventDays(ev);
            ReloadEvents();
        }
    }

    // ====================================================================
    //  Публікація
    // ====================================================================
    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        var s = _settings.GetSettings();
        // Публікуємо ЛИШЕ активні змагання (галочка «Активне» у таблиці).
        var events = _settings.GetEvents().Where(x => x.IsActive).ToList();

        if (!s.IsReadyForLive())
        {
            MessageBox.Show("Заповніть SupabaseUrl / ServiceRoleKey на вкладці «Налаштування».",
                "Orientir", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (events.Count == 0)
        {
            MessageBox.Show("Немає активних змагань. Позначте галочку «Активне» у таблиці змагань.",
                "Orientir", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        LogBox.Document.Blocks.Clear();
        _cts = new CancellationTokenSource();
        BtnStart.IsEnabled = false;
        BtnStop.IsEnabled = true;
        LblStatus.Text = $"Публікація… ({string.Join(", ", events.Select(x => x.Slug))})";

        var log = new Progress<string>(AppendLog);

        try
        {
            await PublisherLoop.RunAsync(s, events, log, _cts.Token);
        }
        catch (OperationCanceledException) { /* зупинено вручну */ }
        catch (Exception ex)
        {
            AppendLog($"ПОМИЛКА: {ex.Message}");
        }
        finally
        {
            BtnStart.IsEnabled = true;
            BtnStop.IsEnabled = false;
            LblStatus.Text = "Зупинено";
        }
    }

    // Додає рядок у лог; http(s)-посилання робить клікабельним Hyperlink'ом.
    private void AppendLog(string line)
    {
        var para = new System.Windows.Documents.Paragraph { Margin = new Thickness(0) };
        int pos = 0;
        foreach (System.Text.RegularExpressions.Match m in
                 System.Text.RegularExpressions.Regex.Matches(line, @"https?://\S+"))
        {
            if (m.Index > pos)
                para.Inlines.Add(new System.Windows.Documents.Run(line[pos..m.Index]));

            var uri = m.Value;
            var link = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run(uri))
            {
                NavigateUri = new Uri(uri),
            };
            link.RequestNavigate += (_, args) =>
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(args.Uri.AbsoluteUri)
                {
                    UseShellExecute = true,
                });
                args.Handled = true;
            };
            para.Inlines.Add(link);
            pos = m.Index + m.Length;
        }
        if (pos < line.Length)
            para.Inlines.Add(new System.Windows.Documents.Run(line[pos..]));

        LogBox.Document.Blocks.Add(para);
        LogBox.ScrollToEnd();
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
    }

    // ====================================================================
    //  HTML
    // ====================================================================
    private void PickHtmlFiles_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Оберіть .htm файли",
            Filter = "HTML звіти (*.htm)|*.htm|Усі файли (*.*)|*.*",
            Multiselect = true,
        };
        if (dlg.ShowDialog() == true)
            ConvertHtml(dlg.FileNames);
    }

    private void PickHtmlFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Тека з .htm файлами" };
        if (dlg.ShowDialog() == true)
        {
            var files = Directory.GetFiles(dlg.FolderName, "*.htm", SearchOption.TopDirectoryOnly);
            ConvertHtml(files);
        }
    }

    private void ConvertHtml(IEnumerable<string> files)
    {
        LstHtmlLog.Items.Clear();
        int ok = 0, err = 0;
        foreach (var f in files)
        {
            try
            {
                var dst = HtmlFix.Convert(f);
                LstHtmlLog.Items.Add($"OK: {f}  →  {dst}");
                ok++;
            }
            catch (Exception ex)
            {
                LstHtmlLog.Items.Add($"ПОМИЛКА [{f}]: {ex.Message}");
                err++;
            }
        }
        LstHtmlLog.Items.Add($"Готово: {ok} успішно, {err} з помилками.");
    }
}
