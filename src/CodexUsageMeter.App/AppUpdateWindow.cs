using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Button = System.Windows.Controls.Button;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace CodexUsageMeter.App;

public sealed class AppUpdateWindow : Window
{
    public AppUpdateWindow(Version installedVersion, Version? availableVersion, bool isInstalled)
    {
        Title = AppText.Get("AppUpdatesTitle");
        Width = 480;
        SizeToContent = SizeToContent.Height;
        MaxHeight = SystemParameters.WorkArea.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(31, 34, 40));
        Foreground = Brushes.White;

        var content = new StackPanel { Margin = new Thickness(24) };
        content.Children.Add(new TextBlock
        {
            Text = AppText.Get("AppUpdatesTitle"),
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        });
        content.Children.Add(Copy(AppText.Get("InstalledVersion", installedVersion.ToString(3))));
        if (availableVersion is not null)
        {
            content.Children.Add(Copy(AppText.Get("AvailableVersion", availableVersion.ToString(3))));
            content.Children.Add(Copy(AppText.Get("InstallUpdatePrompt")));
            if (!isInstalled) content.Children.Add(Copy(AppText.Get("PortableUpdatePrompt")));
        }
        else
        {
            content.Children.Add(Copy(AppText.Get("NoAppUpdate")));
        }

        var actions = new WrapPanel
        {
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        var cancel = new Button
        {
            Content = AppText.Get(availableVersion is null ? "AboutClose" : "UpdateLater"),
            IsCancel = true,
            Padding = new Thickness(14, 7, 14, 7),
            Margin = new Thickness(0, 0, 8, 8)
        };
        cancel.Click += (_, _) => DialogResult = false;
        actions.Children.Add(cancel);
        if (availableVersion is not null)
        {
            var install = new Button
            {
                Content = AppText.Get("DownloadInstall"),
                Padding = new Thickness(14, 7, 14, 7),
                Margin = new Thickness(0, 0, 0, 8)
            };
            install.Click += (_, _) => DialogResult = true;
            actions.Children.Add(install);
        }
        content.Children.Add(actions);
        Content = new ScrollViewer
        {
            Content = content,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }

    private static TextBlock Copy(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        FontSize = 13,
        Margin = new Thickness(0, 0, 0, 10)
    };
}
