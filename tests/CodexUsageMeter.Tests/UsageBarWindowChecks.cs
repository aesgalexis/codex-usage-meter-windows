using System.Runtime.InteropServices;
using System.Windows.Interop;
using CodexUsageMeter.App;

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

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr window, int index);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
}
