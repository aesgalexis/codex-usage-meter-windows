using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Button = System.Windows.Controls.Button;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Cursors = System.Windows.Input.Cursors;

namespace CodexUsageMeter.App;

public sealed class AboutWindow : Window
{
    private const string ProjectUrl = "https://github.com/aesgalexis/codex-usage-meter-windows";
    private const string ReleasesUrl = ProjectUrl + "/releases";
    private const string IssuesUrl = ProjectUrl + "/issues";
    private const string UnatomoUrl = "https://unatomo.com";

    public AboutWindow()
    {
        Title = AppText.Get("About");
        Width = 470;
        Height = 570;
        MinWidth = Width;
        MinHeight = Height;
        MaxWidth = Width;
        MaxHeight = Height;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = true;
        Content = BuildCard();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    private Border BuildCard()
    {
        var root = new Grid { Margin = new Thickness(28, 22, 28, 22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition());
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var brand = new TextBlock
        {
            Text = "CODEX USAGE METER",
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("#8F96A3"),
            VerticalAlignment = VerticalAlignment.Center
        };
        var close = IconButton("×", AppText.Get("AboutClose"));
        close.Click += (_, _) => Close();
        top.Children.Add(brand);
        Grid.SetColumn(close, 1);
        top.Children.Add(close);
        root.Children.Add(top);

        var hero = new StackPanel { Margin = new Thickness(0, 18, 0, 18) };
        var icon = new Border
        {
            Width = 62,
            Height = 62,
            CornerRadius = new CornerRadius(18),
            Background = new LinearGradientBrush(Color.FromRgb(28, 194, 120), Color.FromRgb(11, 112, 83), 45),
            Child = new TextBlock
            {
                Text = "C",
                FontSize = 30,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        hero.Children.Add(icon);
        hero.Children.Add(new TextBlock
        {
            Text = "Codex Usage Meter",
            FontSize = 25,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 13, 0, 4)
        });
        hero.Children.Add(new TextBlock
        {
            Text = AppText.Get("AboutTagline"),
            FontSize = 13,
            Foreground = Brush("#B8BDC7")
        });
        hero.Children.Add(new Border
        {
            Background = Brush("#30343C"),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 5, 10, 5),
            Margin = new Thickness(0, 12, 0, 0),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = AppText.Get("AboutVersion", DisplayVersion()),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush("#DCE0E7")
            }
        });
        Grid.SetRow(hero, 1);
        root.Children.Add(hero);

        var facts = new StackPanel();
        facts.Children.Add(Fact("●", "#20B46E", AppText.Get("AboutPrivacyTitle"), AppText.Get("AboutPrivacy")));
        facts.Children.Add(Fact("◆", "#5DA9FF", AppText.Get("AboutSourceTitle"), AppText.Get("AboutSource")));
        facts.Children.Add(Fact("◇", "#C69BFF", AppText.Get("AboutLicenseTitle"), AppText.Get("AboutLicense")));
        Grid.SetRow(facts, 2);
        root.Children.Add(facts);

        var links = new Grid { Margin = new Thickness(0, 16, 0, 14) };
        links.ColumnDefinitions.Add(new ColumnDefinition());
        links.ColumnDefinitions.Add(new ColumnDefinition());
        links.ColumnDefinitions.Add(new ColumnDefinition());
        AddLink(links, AppText.Get("AboutProject"), ProjectUrl, 0);
        AddLink(links, AppText.Get("AboutReleases"), ReleasesUrl, 1);
        AddLink(links, AppText.Get("AboutIssues"), IssuesUrl, 2);
        Grid.SetRow(links, 3);
        root.Children.Add(links);

        var powered = TextButton(AppText.Get("AboutPowered") + "  ↗");
        powered.Foreground = Brush("#7FE0B2");
        powered.FontWeight = FontWeights.SemiBold;
        powered.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
        powered.Click += (_, _) => OpenUrl(UnatomoUrl);
        Grid.SetRow(powered, 4);
        root.Children.Add(powered);

        var card = new Border
        {
            Background = Brush("#1F2228"),
            BorderBrush = Brush("#444954"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Child = root,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 28, ShadowDepth = 7, Opacity = 0.45 }
        };
        card.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState != MouseButtonState.Pressed || IsInsideButton(e.OriginalSource as DependencyObject)) return;
            try { DragMove(); } catch (InvalidOperationException) { }
        };
        return card;
    }

    private static Border Fact(string glyph, string color, string title, string body)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(new TextBlock { Text = glyph, Foreground = Brush(color), FontSize = 15, Margin = new Thickness(1, 2, 0, 0) });
        var copy = new StackPanel();
        copy.Children.Add(new TextBlock { Text = title, Foreground = Brushes.White, FontSize = 12, FontWeight = FontWeights.SemiBold });
        copy.Children.Add(new TextBlock { Text = body, Foreground = Brush("#AEB4BF"), FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
        Grid.SetColumn(copy, 1);
        grid.Children.Add(copy);
        return new Border { Background = Brush("#272B32"), CornerRadius = new CornerRadius(10), Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 8), Child = grid };
    }

    private static void AddLink(Grid grid, string text, string url, int column)
    {
        var button = TextButton(text + "  ↗");
        button.Margin = new Thickness(column == 0 ? 0 : 4, 0, column == 2 ? 0 : 4, 0);
        button.Background = Brush("#2B2F37");
        button.BorderBrush = Brush("#444A55");
        button.BorderThickness = new Thickness(1);
        button.Padding = new Thickness(7, 8, 7, 8);
        button.Click += (_, _) => OpenUrl(url);
        Grid.SetColumn(button, column);
        grid.Children.Add(button);
    }

    private static Button TextButton(string text) => new()
    {
        Content = text,
        Foreground = Brush("#DCE0E7"),
        Background = Brushes.Transparent,
        BorderBrush = Brushes.Transparent,
        Cursor = Cursors.Hand,
        FontSize = 11
    };

    private static Button IconButton(string text, string tooltip) => new()
    {
        Content = text,
        ToolTip = tooltip,
        Width = 30,
        Height = 30,
        FontSize = 20,
        Foreground = Brush("#C8CDD5"),
        Background = Brushes.Transparent,
        BorderBrush = Brushes.Transparent,
        Cursor = Cursors.Hand
    };

    private static string DisplayVersion()
    {
        var informational = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return (informational ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "development")
            .Split('+')[0];
    }

    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }
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

    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color));
}
