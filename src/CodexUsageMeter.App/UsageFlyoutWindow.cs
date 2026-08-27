using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CodexUsageMeter.Core;
using Button = System.Windows.Controls.Button;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using FontFamily = System.Windows.Media.FontFamily;
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
    private readonly Button _compactPin = new();
    private readonly TextBlock _compactPercent = new();
    private readonly TextBlock _compactDots = new();
    private readonly ContentControl _modelIcon = new();
    private readonly ContentControl _compactModelIcon = new();
    private readonly Border _compactFill = new();
    private readonly Border _activityShine = new();
    private readonly TranslateTransform _shineTranslation = new();
    private readonly Grid _lineProgressLayer = new();
    private readonly Border _lineFill = new();
    private readonly Border _lineFiveHourMarker = new();
    private readonly Border _lineActivityShine = new();
    private readonly TranslateTransform _lineShineTranslation = new();
    private readonly RectangleGeometry _compactClip = new() { RadiusX = 20, RadiusY = 20 };
    private readonly Border _card;
    private readonly Border _compactCard;
    private readonly Border _lineCard;
    private double _availablePercent;
    private double _lineAvailablePercent;
    private double? _fiveHourAvailablePercent;
    private int _lineThickness = 3;
    private DateTimeOffset _lastShineAt = DateTimeOffset.MinValue;

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
        _compactCard = BuildCompactCard();
        _lineCard = BuildLineCard();
        Content = _card;
        Deactivated += (_, _) => { if (!IsPinned) Hide(); };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape && !IsPinned) Hide(); };
    }

    public bool IsPinned { get; private set; }
    public bool IsCompact { get; private set; }
    public bool IsLine { get; private set; }
    public event EventHandler<bool>? PinChanged;
    public event EventHandler? PositionChanged;

    public void SetPinned(bool pinned)
    {
        IsPinned = pinned;
        _pin.ToolTip = AppText.Get(pinned ? "Unpin" : "Pin");
        _pin.LayoutTransform = new RotateTransform(pinned ? 45 : 0);
        _compactPin.ToolTip = AppText.Get(pinned ? "Unpin" : "Pin");
        _compactPin.LayoutTransform = new RotateTransform(pinned ? 45 : 0);
    }

    public void SetMode(bool compact, bool line)
    {
        IsLine = line;
        IsCompact = compact && !line;
        Width = IsLine || IsCompact ? 260 : 310;
        Height = IsLine ? _lineThickness : IsCompact ? 40 : 154;
        Content = IsLine ? _lineCard : IsCompact ? _compactCard : _card;
        IsHitTestVisible = !IsLine;
        if (IsCompact) Dispatcher.BeginInvoke(UpdateCompactFill);
        if (IsLine) Dispatcher.BeginInvoke(UpdateLineFill);
    }

    public void SetLineThickness(int thickness)
    {
        _lineThickness = Math.Clamp(thickness, 1, 5);
        if (IsLine)
        {
            Height = _lineThickness;
            UpdateLineFill();
        }
    }

    public void PlayShine(bool force = false)
    {
        if (!IsCompact && !IsLine) return;
        var now = DateTimeOffset.Now;
        if (!force && now - _lastShineAt < TimeSpan.FromSeconds(3)) return;
        _lastShineAt = now;

        var shine = IsLine ? _lineActivityShine : _activityShine;
        var translation = IsLine ? _lineShineTranslation : _shineTranslation;
        var travelWidth = IsLine ? _lineProgressLayer.ActualWidth : Width;
        if (travelWidth <= 0) return;
        shine.Visibility = Visibility.Visible;
        var animation = new DoubleAnimation
        {
            From = -60,
            To = travelWidth + 60,
            Duration = TimeSpan.FromSeconds(IsLine ? 1.6 : 1.1),
            FillBehavior = FillBehavior.Stop
        };
        animation.Completed += (_, _) => shine.Visibility = Visibility.Collapsed;
        translation.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    public void UpdateUsage(UsageSnapshot? snapshot, string? error = null, bool stale = false)
    {
        if (snapshot is null)
        {
            _available.Text = AppText.Get("NoData");
            _details.Text = error ?? AppText.Get("WaitingTask");
            _reset.Text = AppText.Get("AutoUpdate");
            _credits.Text = AppText.Get("NoCredits");
            _progress.Value = 0;
            _progress.Foreground = new SolidColorBrush(Color.FromRgb(120, 130, 140));
            _availablePercent = 0;
            _lineAvailablePercent = 0;
            _fiveHourAvailablePercent = null;
            _compactPercent.Text = AppText.Get("NoData");
            _compactDots.Text = string.Empty;
            _compactDots.ToolTip = null;
            SetModelIcon(null);
            _compactFill.Background = new SolidColorBrush(Color.FromRgb(120, 130, 140));
            _lineFill.Background = new SolidColorBrush(Color.FromRgb(120, 130, 140));
            UpdateCompactFill();
            UpdateLineFill();
            return;
        }

        var weeklyWindow = snapshot.WeeklyWindow;
        var available = (int)Math.Round(weeklyWindow.AvailablePercent);
        _available.Text = AppText.Get("AvailableText", available);
        _details.Text = snapshot.Windows.Count > 1
            ? string.Join(" · ", snapshot.Windows.Select(window => AppText.Get("WindowUsed", FormatWindow(window.WindowMinutes), window.UsedPercent.ToString("0.#", AppText.Culture))))
            : AppText.Get("UsedText", snapshot.UsedPercent.ToString("0.#", AppText.Culture));
        var weeklyReset = weeklyWindow.ResetsAt is { } reset
            ? AppText.Get("WindowResets", "7d", FormatResetCountdown(reset))
            : AppText.Get("Updated", snapshot.ObservedAt.ToLocalTime().ToString("t", AppText.Culture));
        var fiveHourWindow = snapshot.FiveHourWindow;
        var fiveHourReset = fiveHourWindow?.ResetsAt is { } fiveHourResetAt
            ? AppText.Get("WindowResets", "5h", FormatResetCountdown(fiveHourResetAt))
            : null;
        _reset.Text = fiveHourReset is null ? weeklyReset : $"{weeklyReset} · {fiveHourReset}";
        var age = FormatAge(snapshot.ObservedAt);
        var creditsText = snapshot.CreditBalance is { } balance
            ? AppText.Get("Credits", balance.ToString("0.##", AppText.Culture))
            : AppText.Get("NoCredits");
        _credits.Text = stale ? $"{AppText.Get("Stale")}: {age} · {creditsText}" : $"{age} · {creditsText}";
        _credits.ToolTip = stale ? error : null;
        _progress.Value = weeklyWindow.AvailablePercent;
        _progress.Foreground = new SolidColorBrush(available switch
        {
            >= 50 => Color.FromRgb(32, 180, 110),
            >= 20 => Color.FromRgb(240, 170, 35),
            _ => Color.FromRgb(220, 65, 70)
        });
        _availablePercent = weeklyWindow.AvailablePercent;
        _lineAvailablePercent = weeklyWindow.AvailablePercent;
        _fiveHourAvailablePercent = snapshot.FiveHourWindow?.AvailablePercent;
        _compactPercent.Text = $"{available}%";
        _compactDots.Text = fiveHourReset ?? string.Empty;
        _compactDots.ToolTip = fiveHourWindow?.ResetsAt?.ToLocalTime().ToString("g", AppText.Culture);
        SetModelIcon(snapshot.ActiveModel);
        _compactFill.Background = _progress.Foreground;
        _lineFill.Background = _progress.Foreground;
        UpdateCompactFill();
        UpdateLineFill();
    }

    private void SetModelIcon(string? model)
    {
        foreach (var icon in new[] { _modelIcon, _compactModelIcon })
        {
            icon.Content = CreateModelGlyph(model);
            icon.ToolTip = model;
            icon.Visibility = string.IsNullOrWhiteSpace(model) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private static FrameworkElement? CreateModelGlyph(string? model)
    {
        if (string.IsNullOrWhiteSpace(model)) return null;
        var normalized = model.ToLowerInvariant();
        if (normalized.Contains("terra"))
        {
            return new System.Windows.Shapes.Ellipse
            {
                Width = 11,
                Height = 11,
                Fill = Brushes.White,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        var symbol = normalized.Contains("sol") ? "☀" : normalized.Contains("luna") ? "☽" : "◆";
        return new TextBlock
        {
            Text = symbol,
            FontFamily = new FontFamily("Segoe UI Symbol"),
            FontSize = 14,
            Foreground = Brushes.White,
            Width = 16,
            Height = 16,
            LineHeight = 16,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private static string FormatWindow(int? minutes) => minutes switch
    {
        null => "?",
        < 60 => $"{minutes}m",
        < 1440 => $"{minutes / 60d:0.#}h",
        _ => $"{minutes / 1440d:0.#}d"
    };

    private static string FormatAge(DateTimeOffset observedAt)
    {
        var age = DateTimeOffset.Now - observedAt;
        if (age < TimeSpan.FromMinutes(1)) return AppText.Get("JustNow");
        if (age < TimeSpan.FromHours(1)) return AppText.Get("MinutesAgo", Math.Max(1, (int)age.TotalMinutes));
        if (age < TimeSpan.FromDays(1)) return AppText.Get("HoursAgo", Math.Max(1, (int)age.TotalHours));
        return AppText.Get("DaysAgo", Math.Max(1, (int)age.TotalDays));
    }

    private static string FormatResetCountdown(DateTimeOffset reset)
    {
        var remaining = reset - DateTimeOffset.Now;
        if (remaining <= TimeSpan.Zero) return AppText.Get("ResetNow");

        var totalMinutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
        var days = totalMinutes / (24 * 60);
        var hours = totalMinutes % (24 * 60) / 60;
        var minutes = totalMinutes % 60;
        if (days > 0) return hours > 0 ? $"{days}d {hours}h" : $"{days}d";
        if (hours > 0) return minutes > 0 ? $"{hours}h {minutes}m" : $"{hours}h";
        return $"{minutes}m";
    }

    private static void ConfigureModelIconHost(ContentControl icon)
    {
        icon.Width = 16;
        icon.Height = 16;
        icon.Margin = new Thickness(6, 0, 0, 0);
        icon.Padding = new Thickness(0);
        icon.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Center;
        icon.VerticalContentAlignment = VerticalAlignment.Center;
        icon.VerticalAlignment = VerticalAlignment.Center;
    }

    private Border BuildCard()
    {
        var root = new Grid { Margin = new Thickness(18, 10, 18, 14) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new TextBlock { Text = "Codex", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
        ConfigureModelIconHost(_modelIcon);
        var titleGroup = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        titleGroup.Children.Add(title); titleGroup.Children.Add(_modelIcon);
        ConfigurePinButton(_pin, 34, 30, 20);
        header.Children.Add(titleGroup);
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

    private Border BuildCompactCard()
    {
        _compactFill.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
        _compactFill.CornerRadius = new CornerRadius(19);
        _compactFill.IsHitTestVisible = false;

        var content = new Grid { Margin = new Thickness(14, 0, 7, 0) };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition());
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new TextBlock { Text = "Codex", Foreground = Brushes.White, FontSize = 13, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        ConfigureModelIconHost(_compactModelIcon);
        _compactPercent.Foreground = Brushes.White; _compactPercent.FontSize = 13; _compactPercent.FontWeight = FontWeights.SemiBold; _compactPercent.VerticalAlignment = VerticalAlignment.Center; _compactPercent.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
        _compactDots.Foreground = Brushes.White; _compactDots.FontSize = 9; _compactDots.VerticalAlignment = VerticalAlignment.Center; _compactDots.Margin = new Thickness(6, 0, 5, 0);
        ConfigurePinButton(_compactPin, 28, 28, 17);
        content.Children.Add(title);
        Grid.SetColumn(_compactModelIcon, 1); content.Children.Add(_compactModelIcon);
        Grid.SetColumn(_compactPercent, 2); content.Children.Add(_compactPercent);
        Grid.SetColumn(_compactDots, 3); content.Children.Add(_compactDots);
        Grid.SetColumn(_compactPin, 4); content.Children.Add(_compactPin);

        var layers = new Grid();
        layers.Clip = _compactClip;
        layers.Children.Add(_compactFill);
        _activityShine.Width = 44;
        _activityShine.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
        _activityShine.IsHitTestVisible = false;
        _activityShine.Visibility = Visibility.Collapsed;
        _activityShine.Background = new LinearGradientBrush
        {
            GradientStops = new GradientStopCollection
            {
                new(Color.FromArgb(0, 255, 255, 255), 0),
                new(Color.FromArgb(24, 255, 255, 255), 0.3),
                new(Color.FromArgb(105, 255, 255, 255), 0.5),
                new(Color.FromArgb(24, 255, 255, 255), 0.7),
                new(Color.FromArgb(0, 255, 255, 255), 1)
            },
            ColorInterpolationMode = ColorInterpolationMode.SRgbLinearInterpolation,
            StartPoint = new System.Windows.Point(0, 0),
            EndPoint = new System.Windows.Point(1, 0)
        };
        _activityShine.RenderTransform = new TransformGroup
        {
            Children = new TransformCollection { new SkewTransform(-18, 0), _shineTranslation }
        };
        layers.Children.Add(_activityShine);
        layers.Children.Add(content);
        var card = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(47, 50, 58)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(76, 80, 90)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            ClipToBounds = true,
            Child = layers
        };
        card.SizeChanged += (_, _) =>
        {
            _compactClip.Rect = new Rect(0, 0, card.ActualWidth, card.ActualHeight);
            UpdateCompactFill();
        };
        AttachDrag(card);
        return card;
    }

    private Border BuildLineCard()
    {
        _lineFill.Background = new SolidColorBrush(Color.FromRgb(120, 130, 140));
        _lineActivityShine.Width = 120;
        _lineActivityShine.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
        _lineActivityShine.IsHitTestVisible = false;
        _lineActivityShine.Visibility = Visibility.Collapsed;
        _lineActivityShine.Background = CreateLineShineBrush();
        _lineActivityShine.RenderTransform = new TransformGroup
        {
            Children = new TransformCollection { new SkewTransform(-18, 0), _lineShineTranslation }
        };
        _lineProgressLayer.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
        _lineProgressLayer.ClipToBounds = true;
        _lineProgressLayer.Children.Add(_lineFill);
        _lineProgressLayer.Children.Add(_lineActivityShine);

        _lineFiveHourMarker.Background = new SolidColorBrush(Color.FromRgb(255, 255, 255));
        _lineFiveHourMarker.BorderThickness = new Thickness(0);
        _lineFiveHourMarker.CornerRadius = new CornerRadius(1.5);
        _lineFiveHourMarker.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
        _lineFiveHourMarker.VerticalAlignment = VerticalAlignment.Stretch;
        _lineFiveHourMarker.IsHitTestVisible = false;
        _lineFiveHourMarker.Visibility = Visibility.Collapsed;

        var layers = new Grid { ClipToBounds = true };
        layers.Children.Add(_lineProgressLayer);
        layers.Children.Add(_lineFiveHourMarker);

        var card = new Border
        {
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(1.5),
            IsHitTestVisible = false,
            Child = layers
        };
        card.SizeChanged += (_, _) => UpdateLineFill();
        return card;
    }

    private void UpdateLineFill()
    {
        if (_lineCard is null) return;
        var width = _lineCard.ActualWidth;
        _lineProgressLayer.Width = Math.Max(0, width * Math.Clamp(_lineAvailablePercent, 0, 100) / 100d);

        if (_fiveHourAvailablePercent is not { } fiveHourAvailable || width <= 0)
        {
            _lineFiveHourMarker.Visibility = Visibility.Collapsed;
            return;
        }

        // Keep the five-hour indicator clearly visible even on a wide display. It is
        // deliberately white and sits above the colored weekly progress layer.
        var markerWidth = Math.Max(14d, _lineThickness * 4d);
        _lineFiveHourMarker.Width = markerWidth;
        _lineFiveHourMarker.Margin = new Thickness(
            Math.Clamp(width * Math.Clamp(fiveHourAvailable, 0, 100) / 100d - markerWidth / 2d, 0, Math.Max(0, width - markerWidth)),
            0,
            0,
            0);
        _lineFiveHourMarker.Visibility = Visibility.Visible;
    }

    private static LinearGradientBrush CreateLineShineBrush() => new()
    {
        GradientStops = new GradientStopCollection
        {
            new(Color.FromArgb(0, 255, 255, 255), 0),
            new(Color.FromArgb(90, 255, 255, 255), 0.2),
            new(Color.FromArgb(190, 255, 255, 255), 0.4),
            new(Color.FromArgb(255, 255, 255, 255), 0.5),
            new(Color.FromArgb(190, 255, 255, 255), 0.6),
            new(Color.FromArgb(90, 255, 255, 255), 0.8),
            new(Color.FromArgb(0, 255, 255, 255), 1)
        },
        ColorInterpolationMode = ColorInterpolationMode.SRgbLinearInterpolation,
        StartPoint = new System.Windows.Point(0, 0),
        EndPoint = new System.Windows.Point(1, 0)
    };

    private void UpdateCompactFill()
    {
        if (_compactCard is null) return;
        _compactFill.Width = Math.Max(0, _compactCard.ActualWidth * Math.Clamp(_availablePercent, 0, 100) / 100d);
    }

    private void ConfigurePinButton(Button button, double width, double height, double iconSize)
    {
        button.Content = CreatePinIcon(iconSize);
        button.ToolTip = AppText.Get("Pin");
        button.Width = width;
        button.Height = height;
        button.Padding = new Thickness(0);
        StyleButton(button);
        button.Template = CreateIconButtonTemplate();
        button.Click += (_, _) => { SetPinned(!IsPinned); PinChanged?.Invoke(this, IsPinned); };
    }

    private void AttachDrag(Border card)
    {
        card.MouseLeftButtonDown += (_, e) =>
        {
            if (!IsPinned || e.ButtonState != MouseButtonState.Pressed || IsInsideButton(e.OriginalSource as DependencyObject)) return;
            try { DragMove(); PositionChanged?.Invoke(this, EventArgs.Empty); }
            catch (InvalidOperationException) { }
        };
    }

    private static Viewbox CreatePinIcon(double size)
    {
        var pin = new ShapePath
        {
            Data = Geometry.Parse("M 7,2 L 17,2 L 16,5 L 15,10 L 18,13 L 18,15 L 13,15 L 12,22 L 11,15 L 6,15 L 6,13 L 9,10 L 8,5 Z"),
            Fill = new SolidColorBrush(Color.FromRgb(205, 208, 214)),
            Stretch = Stretch.Uniform
        };
        return new Viewbox { Width = size, Height = size, Child = pin };
    }

    private static ControlTemplate CreateIconButtonTemplate()
    {
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
        presenter.SetValue(VerticalAlignmentProperty, System.Windows.VerticalAlignment.Center);
        return new ControlTemplate(typeof(Button)) { VisualTree = presenter };
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
