using System.Collections.ObjectModel;
using System.Windows;
using Orientir.Core.Models;

namespace Orientir.Desktop;

public partial class DaysWindow : Window
{
    private readonly EventConfig _event;
    private readonly ObservableCollection<DayConfig> _days;

    // autoScan=true (при створенні нового змагання) — одразу скануємо теку,
    // щоб користувач лише ввів підписи.
    public DaysWindow(EventConfig ev, bool autoScan = false)
    {
        InitializeComponent();
        _event = ev;

        var initial = ev.Days.Count > 0
            ? ev.Days.OrderBy(d => d.Day).ToList()
            : (autoScan ? EventConfig.ScanDays(ev.BasePath) : new());

        _days = new ObservableCollection<DayConfig>(initial);
        GridDays.ItemsSource = _days;
    }

    private void Rescan_Click(object sender, RoutedEventArgs e)
    {
        var scanned = EventConfig.ScanDays(_event.BasePath);
        if (scanned.Count == 0)
        {
            MessageBox.Show($"У теці не знайдено підтек D_1..D_n з OLD.DBF:\n{_event.BasePath}",
                "Orientir", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Зберігаємо вже введені підписи за номером дня.
        var labels = _days.ToDictionary(d => d.Day, d => d.Label);
        _days.Clear();
        foreach (var d in scanned)
        {
            d.Label = labels.TryGetValue(d.Day, out var lbl) ? lbl : "";
            _days.Add(d);
        }
    }

    private void DeleteRow_Click(object sender, RoutedEventArgs e)
    {
        if (GridDays.SelectedItem is DayConfig d)
            _days.Remove(d);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        // Переносимо відредаговані дні назад у змагання (нові рядки матимуть Id = 0 → insert).
        _event.Days.Clear();
        foreach (var d in _days.Where(d => !string.IsNullOrWhiteSpace(d.Folder) || d.Day > 0))
        {
            _event.Days.Add(new DayConfig
            {
                Day = d.Day,
                Folder = d.Folder ?? "",
                Label = d.Label ?? "",
                EventConfigId = _event.Id,
            });
        }
        DialogResult = true;
    }
}
