using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CodexUsageMeter.Core;
using Button = System.Windows.Controls.Button;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using FontFamily = System.Windows.Media.FontFamily;
using ProgressBar = System.Windows.Controls.ProgressBar;

namespace CodexUsageMeter.App;

public sealed class UsageFlyoutWindow : Window
{
    private readonly TextBlock _available = new();
    private readonly TextBlock _details = new();
    private readonly TextBlock _reset = new();
    private readonly ProgressBar _progress = new();
    private readonly Button _pin = new();
    private readonly Button _close = new();
    private readonly Border _card;

    public UsageFlyoutWindow()
    {
        Width = 310;
        Height = 154;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        _card = BuildCard();
        Content = _card;
        Deactivated += (_, _) => { if (!IsPinned) Hide(); };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape && !IsPinned) Hide(); };
    }

    public bool IsPinned { get; private set; }
    public event EventHandler<bool>? PinChanged;
    public event EventHandler? PositionChanged;

    public void SetPinned(bool pinned)
    {
        IsPinned = pinned;
        _pin.Content = "\uE718";
        _pin.ToolTip = pinned ? "Soltar widget" : "Fijar widget";
        _pin.Background = new SolidColorBrush(pinned
            ? Color.FromRgb(38, 135, 89)
            : Color.FromRgb(55, 59, 68));
        _close.Visibility = pinned ? Visibility.Visible : Visibility.Collapsed;
    }

    public void UpdateUsage(UsageSnapshot? snapshot, string? error = null)
    {
        if (snapshot is null)
        {
            _available.Text = "Sin datos";
            _details.Text = error ?? "Ejecuta una tarea en Codex";
            _reset.Text = "Actualizaremos la tarjeta automáticamente";
            _progress.Value = 0;
            return;
        }

        _available.Text = $"{snapshot.AvailablePercent:0}% disponible";
        _details.Text = $"{snapshot.UsedPercent:0.#}% usado";
        _reset.Text = snapshot.ResetsAt is { } reset
            ? $"Se reinicia {reset.ToLocalTime():g}"
            : $"Actualizado {snapshot.ObservedAt.ToLocalTime():t}";
        _progress.Value = snapshot.AvailablePercent;
    }

    private Border BuildCard()
    {
        var root = new Grid { Margin = new Thickness(18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new TextBlock { Text = "Codex", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
        _pin.Content = "\uE718";
        _pin.FontFamily = new FontFamily("Segoe MDL2 Assets");
        _pin.ToolTip = "Fijar widget";
        _pin.Width = 30;
        _pin.Height = 26;
        _pin.Padding = new Thickness(0);
        StyleButton(_pin);
        _pin.Click += (_, _) => { SetPinned(!IsPinned); PinChanged?.Invoke(this, IsPinned); };
        _close.Content = "×";
        StyleButton(_close);
        _close.Margin = new Thickness(6, 0, 0, 0);
        _close.Click += (_, _) =>
        {
            SetPinned(false);
            PinChanged?.Invoke(this, false);
            Hide();
        };
        header.Children.Add(title);
        Grid.SetColumn(_pin, 1); header.Children.Add(_pin);
        Grid.SetColumn(_close, 2); header.Children.Add(_close);
        root.Children.Add(header);

        _available.FontSize = 18; _available.FontWeight = FontWeights.SemiBold; _available.Foreground = Brushes.White; _available.Margin = new Thickness(0, 9, 0, 6);
        Grid.SetRow(_available, 1); root.Children.Add(_available);
        _progress.Height = 6; _progress.Minimum = 0; _progress.Maximum = 100; _progress.Foreground = new SolidColorBrush(Color.FromRgb(47, 190, 122)); _progress.Background = new SolidColorBrush(Color.FromRgb(65, 69, 78));
        Grid.SetRow(_progress, 2); root.Children.Add(_progress);
        var footer = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition());
        _details.Foreground = new SolidColorBrush(Color.FromRgb(190, 194, 202)); _details.FontSize = 12;
        _reset.Foreground = new SolidColorBrush(Color.FromRgb(155, 160, 170)); _reset.FontSize = 11; _reset.TextAlignment = TextAlignment.Right;
        footer.Children.Add(_details); Grid.SetColumn(_reset, 1); footer.Children.Add(_reset);
        Grid.SetRow(footer, 3); root.Children.Add(footer);

        var card = new Border { Background = new SolidColorBrush(Color.FromArgb(248, 31, 34, 40)), BorderBrush = new SolidColorBrush(Color.FromRgb(68, 72, 82)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(13), Child = root };
        card.MouseLeftButtonDown += (_, e) =>
        {
            if (!IsPinned || e.ButtonState != MouseButtonState.Pressed || IsInsideButton(e.OriginalSource as DependencyObject)) return;
            try { DragMove(); PositionChanged?.Invoke(this, EventArgs.Empty); }
            catch (InvalidOperationException) { }
        };
        return card;
    }

    private static bool IsInsideButton(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is Button) return true;
            element = VisualTreeHelper.GetParent(element);
        }
        return false;
    }

    private static void StyleButton(Button button)
    {
        button.Padding = new Thickness(9, 3, 9, 3);
        button.Foreground = Brushes.White;
        button.Background = new SolidColorBrush(Color.FromRgb(55, 59, 68));
        button.BorderThickness = new Thickness(0);
        button.FontSize = 11;
        button.Cursor = Cursors.Hand;
    }
}
