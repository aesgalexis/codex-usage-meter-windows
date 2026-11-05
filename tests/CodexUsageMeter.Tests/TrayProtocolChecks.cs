using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using CodexUsageMeter.App;
using Forms = System.Windows.Forms;

internal static class TrayProtocolChecks
{
    private const uint CallbackMessage = 0x8001;

    public static void Run()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                CheckTooltipFlags();
                CheckSelectionCallbacks();
                CheckMenuAnchors();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new InvalidOperationException("Falló la comprobación del protocolo de bandeja.", failure);
    }

    private static void CheckTooltipFlags()
    {
        using var icon = new StableNotifyIcon(Guid.NewGuid());
        var type = typeof(StableNotifyIcon);
        var flagsType = type.GetNestedType("NotifyIconFlags", BindingFlags.NonPublic)!;
        var createData = type.GetMethod("CreateData", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var guidField = type.GetField("_useGuid", BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (var useGuid in new[] { true, false })
        {
            guidField.SetValue(icon, useGuid);
            foreach (var operationFlags in new[] { 0x27u, 0x26u, 0x30u }) // Register, update icon/text, show balloon.
            {
                var data = createData.Invoke(icon, [Enum.ToObject(flagsType, operationFlags)])!;
                var flags = Convert.ToUInt32(data.GetType().GetField("Flags")!.GetValue(data));
                Assert((flags & 0x80) != 0,
                    "El tooltip estándar debe mantenerse habilitado al registrar, actualizar o notificar, también con la identidad alternativa.");
                Assert(((flags & 0x20) != 0) == useGuid,
                    "Habilitar el tooltip no debe cambiar la identidad GUID o HWND/ID del icono.");
            }
        }
    }

    private static void CheckSelectionCallbacks()
    {
        // This hidden native message sink never registers an icon in Explorer.
        using var icon = new StableNotifyIcon(Guid.NewGuid());
        var clicks = new List<Forms.MouseEventArgs>();
        icon.MouseClick += (_, e) => clicks.Add(e);
        SendCallback(icon, 0x0202, Point.Empty); // Legacy WM_LBUTTONUP
        Assert(clicks.Count == 1, "La ruta antigua debe seguir respondiendo al botón izquierdo.");

        typeof(StableNotifyIcon).GetField("_usesVersion4", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(icon, true);
        clicks.Clear();
        var negativeMonitor = new Point(-1700, -420);
        SendCallback(icon, 0x0202, negativeMonitor); // Raw mouse-up before NIN_SELECT
        SendCallback(icon, 0x0400, negativeMonitor); // NIN_SELECT
        Assert(clicks.Count == 1 && clicks[0].Button == Forms.MouseButtons.Left &&
            clicks[0].Location == negativeMonitor,
            "Un clic v4 debe ejecutar una sola acción y conservar las coordenadas negativas del callback.");

        var keyboardAnchor = new Point(-1900, 1020);
        SendCallback(icon, 0x0401, keyboardAnchor); // NIN_KEYSELECT
        Assert(clicks.Count == 2 && clicks[1].Location == keyboardAnchor,
            "Enter o Espacio deben ejecutar la acción izquierda en el icono, independientemente del puntero.");

        SendCallback(icon, 0x0202, negativeMonitor);
        SendCallback(icon, 0x0400, negativeMonitor);
        Assert(clicks.Count == 3,
            "Un segundo clic distinto debe conservar su acción sin duplicarla ni perderla por una ventana temporal de deduplicación.");
    }

    private static void CheckMenuAnchors()
    {
        var iconBounds = new Rectangle(-1900, -60, 24, 24);
        var mousePoint = new Point(-1890, -50);
        Assert(StableNotifyIcon.ResolveContextMenuPosition(mousePoint, iconBounds) == mousePoint,
            "El clic derecho debe mantener el menú en la posición del puntero dentro del icono.");
        Assert(StableNotifyIcon.ResolveContextMenuPosition(new Point(800, 500), iconBounds) ==
            new Point(iconBounds.Left, iconBounds.Bottom),
            "El menú por teclado debe anclarse al icono aunque el puntero esté en otro monitor.");
        Assert(StableNotifyIcon.UnpackPosition(PackPosition(new Point(-32700, 32000))) == new Point(-32700, 32000),
            "Las coordenadas v4 deben interpretarse como palabras de 16 bits con signo.");
    }

    // Called only by the opt-in desktop checks, using their uniquely identified test icon.
    public static void RunNative(StableNotifyIcon icon)
    {
        Assert(icon.UsesVersion4, "Windows 10/11 debe aceptar la versión 4 del protocolo de bandeja.");
        Assert(icon.TryGetBounds(out var bounds), "El icono de prueba debe ofrecer un anclaje nativo.");
        var clicks = 0;
        Forms.MouseEventHandler onClick = (_, _) => clicks++;
        icon.MouseClick += onClick;
        using var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Tray protocol check");
        var menuRequests = 0;
        menu.Opening += (_, e) =>
        {
            menuRequests++;
            e.Cancel = true; // Validate dispatch without leaving a menu on the desktop.
        };
        icon.ContextMenuStrip = menu;
        try
        {
            var anchor = bounds.Location;
            SendCallback(icon, 0x0202, anchor);
            SendCallback(icon, 0x0400, anchor);
            SendCallback(icon, 0x0401, anchor);
            Assert(clicks == 2, "El icono registrado debe aceptar ratón y teclado una sola vez por activación.");
            SendCallback(icon, 0x0205, anchor); // Raw mouse-up must not open the menu twice.
            Assert(menuRequests == 0, "El mensaje crudo de ratón no debe duplicar el menú v4.");
            SendCallback(icon, 0x007B, Point.Empty); // WM_CONTEXTMENU does not define callback coordinates.
            Assert(menuRequests == 1, "WM_CONTEXTMENU debe abrir el menú del icono registrado.");
            icon.Text = "Version 4 tooltip update check";
            Assert(icon.IsRegistered && icon.UsesVersion4,
                "Modificar el tooltip debe conservar el registro y el protocolo v4.");
        }
        finally
        {
            icon.ContextMenuStrip = null;
            icon.MouseClick -= onClick;
        }
    }

    private static void SendCallback(StableNotifyIcon icon, int notification, Point anchor)
    {
        var window = (Forms.NativeWindow)typeof(StableNotifyIcon)
            .GetField("_window", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(icon)!;
        _ = SendMessage(window.Handle, CallbackMessage, PackPosition(anchor), new IntPtr((1 << 16) | notification));
    }

    private static IntPtr PackPosition(Point position) =>
        new(unchecked((int)((uint)(ushort)position.X | ((uint)(ushort)position.Y << 16))));

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
