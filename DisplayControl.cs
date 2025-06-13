using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public static class DisplayControl
{
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);




    private const int SC_MONITORPOWER = 0xF170;
    private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);
    private const int WM_SYSCOMMAND = 0x0112;

    public static void TurnOff()
    {
       SendMessage(HWND_BROADCAST, WM_SYSCOMMAND, (IntPtr)SC_MONITORPOWER, (IntPtr)2);
    }

    public static void TurnOn()
    {
        Cursor.Position = new System.Drawing.Point(Cursor.Position.X + 1, Cursor.Position.Y);
        Cursor.Position = new System.Drawing.Point(Cursor.Position.X - 1, Cursor.Position.Y);
    }
}
