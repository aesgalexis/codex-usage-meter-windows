using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CodexUsageMeter.Core;
using Button = System.Windows.Controls.Button;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using ProgressBar = System.Windows.Controls.ProgressBar;
using ShapePath = System.Windows.Shapes.Path;

namespace CodexUsageMeter.App;

public sealed class UsageFlyoutWindow : Window
{
    private readonly TextBlock _available = new();
    private readonly TextBlock _details = new();
    private readonly TextBlock _reset = new();
    private readonly TextBlock _credits = new();
    private readonly ProgressBar _progress = new();
    private readonly Button _pin = new();
    private readonly Border _card;

    public UsageFlyoutWindow()
    {
        Width = 310;
        Height = 168;
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
        _pin.ToolTip = pinned ? "Soltar widget" : "Fijar widget";
        _pin.LayoutTransform = new RotateTransform(pinned ? 45 : 0);
    }

    public void UpdateUsage(UsageSnapshot? snapshot, string? error = null)
    {
        if (snapshot is null)
        {
            _available.Text = "Sin datos";
            _details.Text = error ?? "Ejecuta una tarea en Codex";
            _reset.Text = "Actualizaremos la tarjeta automáticamente";
            _credits.Text = "Créditos: sin datos";
            _progress.Value = 0;
            _progress.Foreground = new SolidColorBrush(Color.FromRgb(120, 130, 140));
            return;
        }

        var available = (int)Math.Round(snapshot.AvailablePercent);
        _available.Text = $"{available}% disponible";
        _details.Text = $"{snapshot.UsedPercent:0.#}% usado";
        _reset.Text = snapshot.ResetsAt is { } reset
            ? FormatTimeUntilReset(reset)
            : $"Actualizado {snapshot.ObservedAt.ToLocalTime():t}";
        _credits.Text = snapshot.CreditBalance is { } balance
            ? $"Créditos: {balance:0.##}"
            : "Créditos: sin datos";
        _progress.Value = snapshot.AvailablePercent;
        _progress.Foreground = new SolidColorBrush(available switch
        {
            >= 50 => Color.FromRgb(32, 180, 110),
            >= 20 => Color.FromRgb(240, 170, 35),
            _ => Color.FromRgb(220, 65, 70)
        });
    }

    private static string FormatTimeUntilReset(DateTimeOffset reset)
    {
        var remaining = reset - DateTimeOffset.Now;
        if (remaining <= TimeSpan.Zero) return "Reinicio del uso pendiente";
        if (remaining < TimeSpan.FromDays(1)) return "Reinicio del uso en menos de 1 día";

        var days = (int)Math.Ceiling(remaining.TotalDays);
        return days == 1 ? "Reinicio del uso en 1 día" : $"Reinicio del uso en {days} días";
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
        var title = new TextBlock { Text = "Codex", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
        _pin.Content = CreatePinIcon();
        _pin.ToolTip = "Fijar widget";
        _pin.Width = 34;
        _pin.Height = 30;
        _pin.Padding = new Thickness(0);
        _pin.Background = Brushes.Transparent;
        _pin.BorderBrush = Brushes.Transparent;
        StyleButton(_pin);
        _pin.Click += (_, _) => { SetPinned(!IsPinned); PinChanged?.Invoke(this, IsPinned); };
        header.Children.Add(title);
        Grid.SetColumn(_pin, 1); header.Children.Add(_pin);
        root.Children.Add(header);

        _available.FontSize = 18; _available.FontWeight = FontWeights.SemiBold; _available.Foreground = Brushes.White; _available.Margin = new Thickness(0, 9, 0, 6);
        Grid.SetRow(_available, 1); root.Children.Add(_available);
        _progress.Height = 6; _progress.Minimum = 0; _progress.Maximum = 100; _progress.Foreground = new SolidColorBrush(Color.FromRgb(32, 180, 110)); _progress.Background = new SolidColorBrush(Color.FromRgb(65, 69, 78));
        Grid.SetRow(_progress, 2); root.Children.Add(_progress);
        var footer = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition());
        footer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        footer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _details.Foreground = new SolidColorBrush(Color.FromRgb(190, 194, 202)); _details.FontSize = 12;
        _reset.Foreground = new SolidColorBrush(Color.FromRgb(155, 160, 170)); _reset.FontSize = 11; _reset.TextAlignment = TextAlignment.Right;
        _credits.Foreground = new SolidColorBrush(Color.FromRgb(155, 160, 170)); _credits.FontSize = 11; _credits.TextAlignment = TextAlignment.Right; _credits.Margin = new Thickness(0, 2, 0, 0);
        footer.Children.Add(_details); Grid.SetColumn(_reset, 1); footer.Children.Add(_reset);
        Grid.SetColumn(_credits, 1); Grid.SetRow(_credits, 1); footer.Children.Add(_credits);
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

    private static Viewbox CreatePinIcon()
    {
        var pin = new ShapePath
        {
            Data = Geometry.Parse("M 7,2 L 17,2 L 16,5 L 15,10 L 18,13 L 18,15 L 13,15 L 12,22 L 11,15 L 6,15 L 6,13 L 9,10 L 8,5 Z"),
            Fill = new SolidColorBrush(Color.FromRgb(205, 208, 214)),
            Stretch = Stretch.Uniform
        };
        return new Viewbox { Width = 20, Height = 20, Child = pin };
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
        button.Background = Brushes.Transparent;
        button.BorderBrush = Brushes.Transparent;
        button.BorderThickness = new Thickness(0);
        button.FontSize = 11;
        button.Cursor = Cursors.Hand;
    }
}
