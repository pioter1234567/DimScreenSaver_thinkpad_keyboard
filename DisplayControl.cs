using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

static class DisplayControl
{
    private const int WM_SYSCOMMAND = 0x0112;
    private const int SC_MONITORPOWER = 0xF170;
    private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xFFFF);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(
        IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    public static void TurnOff()
    {
        // 2 = power off
        PostMessage(HWND_BROADCAST, WM_SYSCOMMAND,
                    (IntPtr)SC_MONITORPOWER, (IntPtr)2);
    }


public static void TurnOn()
    {
        Cursor.Position = new System.Drawing.Point(Cursor.Position.X + 1, Cursor.Position.Y);
        Cursor.Position = new System.Drawing.Point(Cursor.Position.X - 1, Cursor.Position.Y);
    }
}
