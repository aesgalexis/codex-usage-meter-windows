using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using CodexUsageMeter.App;
using CodexUsageMeter.Core;
using Forms = System.Windows.Forms;

internal static class UsageBarWindowChecks
{
    // Opt-in because these checks need an interactive Windows desktop.
    public static void Run()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            UsageFlyoutWindow? bar = null;
            try
            {
                bar = new UsageFlyoutWindow { Left = -32000, Top = -32000 };
                bar.SetPinned(true);
                CheckMissingWeeklyData(bar);
                bar.SetMode(compact: false, line: true);
                if (bar.ShowActivated || bar.Focusable)
                    throw new InvalidOperationException("La línea de uso no debe solicitar activación ni foco.");

                var foreground = GetForegroundWindow();
                if (foreground == IntPtr.Zero)
                    throw new InvalidOperationException("La comprobación requiere un escritorio con una ventana activa.");

                for (var i = 0; i < 3; i++)
                {
                    bar.Show();
                    if (GetForegroundWindow() != foreground || bar.IsActive)
                        throw new InvalidOperationException("La línea de uso ha robado el foco al reaparecer.");
                    var handle = new WindowInteropHelper(bar).Handle;
                    var extendedStyle = GetWindowLong(handle, -20);
                    if ((extendedStyle & 0x08000020) != 0x08000020)
                        throw new InvalidOperationException("La línea de uso debe dejar pasar los clics y evitar activación nativa.");
                    if (SendMessage(handle, 0x0021, IntPtr.Zero, new IntPtr((0x0201 << 16) | 1)) != new IntPtr(3))
                        throw new InvalidOperationException("Los mensajes de ratón no deben activar la línea de uso.");
                    bar.Hide();
                }

                bar.SetMode(compact: true, line: false);
                if (!bar.ShowActivated || !bar.Focusable)
                    throw new InvalidOperationException("El widget debe conservar su comportamiento interactivo.");
                if ((GetWindowLong(new WindowInteropHelper(bar).Handle, -20) & 0x08000020) != 0)
                    throw new InvalidOperationException("El widget debe volver a recibir clics tras salir del modo línea.");
                bar.ShowWithActivation(activate: false);
                if (GetForegroundWindow() != foreground || bar.IsActive)
                    throw new InvalidOperationException("Restaurar el widget sin activación no debe robar el foco.");
                bar.Hide();

                CheckWidgetRecovery(bar, foreground);

                using (var toggleChecks = new FlyoutToggleChecks()) toggleChecks.Run();

                var testGuid = Guid.NewGuid();
                using var trayIcon = new StableNotifyIcon(testGuid) { Icon = System.Drawing.SystemIcons.Application, Visible = true };
                if (!trayIcon.IsRegistered || !trayIcon.UsesGuid || !trayIcon.TryGetBounds(out var iconBounds))
                    throw new InvalidOperationException("El icono de prueba debe registrarse en la bandeja real.");
                if (iconBounds.Width <= 0 || iconBounds.Height <= 0 || iconBounds.Width > 256 || iconBounds.Height > 256)
                    throw new InvalidOperationException($"Los límites del icono no tienen un tamaño de icono: {iconBounds}.");

                TrayProtocolChecks.RunNative(trayIcon);

                using (var duplicate = new StableNotifyIcon(testGuid) { Icon = System.Drawing.SystemIcons.Application, Visible = true })
                {
                    if (!duplicate.IsRegistered || duplicate.UsesGuid || !duplicate.TryGetBounds(out _))
                        throw new InvalidOperationException("Un GUID ocupado debe usar la identidad HWND/ID para conservar un icono visible.");
                    duplicate.Text = "Fallback identity check";
                }
                if (!trayIcon.TryGetBounds(out _))
                    throw new InvalidOperationException("Cerrar una copia alternativa no debe borrar el icono original.");
                trayIcon.Visible = false;
                if (trayIcon.IsRegistered || trayIcon.TryGetBounds(out _))
                    throw new InvalidOperationException("Ocultar el icono debe eliminar únicamente su propio registro.");
                trayIcon.Visible = true;
                if (!trayIcon.IsRegistered || !trayIcon.TryGetBounds(out _))
                    throw new InvalidOperationException("El icono debe poder registrarse de nuevo tras ocultarlo.");
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                bar?.Close();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new InvalidOperationException("Falló la comprobación nativa de la barra.", failure);
    }

    private static void CheckMissingWeeklyData(UsageFlyoutWindow window)
    {
        var now = DateTimeOffset.UtcNow;
        var fiveHourOnly = new UsageSnapshot(25, now.AddHours(2), 300, "plus", null, now,
            [new UsageWindow(25, now.AddHours(2), 300)]);
        foreach (var compact in new[] { false, true })
        {
            window.SetMode(compact, line: false);
            window.UpdateUsage(fiveHourOnly);
            var progress = GetField<System.Windows.Controls.ProgressBar>(window, "_progress");
            var available = GetField<System.Windows.Controls.TextBlock>(window, "_available");
            var compactPercent = GetField<System.Windows.Controls.TextBlock>(window, "_compactPercent");
            if (available.Text != AppText.Get("WeeklyUnavailable") || compactPercent.Text != "—" || progress.Value != 0)
                throw new InvalidOperationException("Un snapshot de cinco horas no debe mostrarse como porcentaje semanal en ningún widget.");
            if (progress.Foreground is not System.Windows.Media.SolidColorBrush brush ||
                brush.Color != System.Windows.Media.Color.FromRgb(120, 130, 140))
                throw new InvalidOperationException("La ausencia del límite semanal debe conservar el estado de color desconocido.");
        }
        window.UpdateUsage(null);
    }

    private static T GetField<T>(UsageFlyoutWindow window, string name) =>
        (T)typeof(UsageFlyoutWindow).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;

    private static void CheckWidgetRecovery(UsageFlyoutWindow window, IntPtr foreground)
    {
        window.SetMode(compact: false, line: false);
        window.SetPinned(true);
        window.ShowWithActivation(activate: false);
        var areas = Forms.Screen.AllScreens.Select(screen => screen.WorkingArea).ToArray();
        if (!NativeWindowPositioning.RecoverVisiblePosition(window, areas))
            throw new InvalidOperationException("El widget guardado fuera de pantalla debe recuperar una posición visible.");
        var handle = new WindowInteropHelper(window).Handle;
        if (!GetWindowRect(handle, out var bounds) || !NativeWindowPositioning.IsWidgetReachable(bounds.ToRectangle(), areas))
            throw new InvalidOperationException("El encabezado del widget recuperado debe quedar accesible en una pantalla actual.");
        if (GetForegroundWindow() != foreground || window.IsActive)
            throw new InvalidOperationException("Recuperar el widget fijado no debe cambiar la ventana activa.");
        if (NativeWindowPositioning.RecoverVisiblePosition(window, areas))
            throw new InvalidOperationException("Un widget ya accesible debe conservar su posición.");
        window.Hide();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr window, int index);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out StableNotifyIcon.NativeIconRect rectangle);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    private sealed class FlyoutToggleChecks : IDisposable
    {
        private readonly System.Windows.Window _other = new() { Left = -32000, Top = -32000, Width = 100, Height = 100 };
        private readonly UsageFlyoutWindow _card = new() { Left = -32000, Top = -32000 };

        public void Run()
        {
            _other.Show();
            foreach (var compact in new[] { false, true })
            {
                _card.SetMode(compact, line: false);
                _card.SetPinned(false);
                _card.IsTrayClickInProgress = () => true;
                _card.ShowWithActivation(activate: true);
                if (!_card.IsActive) throw new InvalidOperationException("La prueba requiere una tarjeta activa.");
                _other.Activate();
                if (_card.IsActive) throw new InvalidOperationException("La prueba debe desactivar la tarjeta antes de recibir el clic.");
                if (!_card.IsVisible)
                    throw new InvalidOperationException("El clic en la bandeja no debe ocultar la tarjeta antes de que el controlador pueda alternarla.");
                _card.Hide();

                _card.IsTrayClickInProgress = () => false;
                _card.ShowWithActivation(activate: true);
                _other.Activate();
                if (_card.IsVisible)
                    throw new InvalidOperationException("Un clic fuera de la bandeja debe seguir cerrando la tarjeta.");
            }
            _card.SetPinned(true);
            _card.ShowWithActivation(activate: true);
            _other.Activate();
            if (!_card.IsVisible)
                throw new InvalidOperationException("La tarjeta fijada debe permanecer visible al perder el foco.");
        }

        public void Dispose()
        {
            _card.Close();
            _other.Close();
        }
    }
}
