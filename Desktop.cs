using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Cally;

internal static class Desktop
{
    const int WindowPosChanging = 0x0046;
    const uint NoSize = 0x0001, NoMove = 0x0002, NoZOrder = 0x0004, NoActivate = 0x0010, Show = 0x0040;
    [StructLayout(LayoutKind.Sequential)]
    struct WindowPosition
    {
        public nint Window, InsertAfter;
        public int X, Y, Width, Height;
        public uint Flags;
    }
    [DllImport("user32")] static extern bool SetWindowPos(nint window,nint order,int x,int y,int width,int height,uint flags);
    [DllImport("user32")] static extern nint GetForegroundWindow();
    [DllImport("user32",CharSet=CharSet.Unicode)] static extern int GetClassName(nint window,System.Text.StringBuilder name,int size);
    [DllImport("user32")] static extern bool IsIconic(nint window);
    [DllImport("user32")] static extern bool IsWindowVisible(nint window);
    [DllImport("user32")] static extern bool ShowWindow(nint window,int command);

    // Keep the transparent WPF surface in its own compositor window, below applications.
    public static void Constrain(Window widget)
    {
        var source=HwndSource.FromHwnd(new WindowInteropHelper(widget).Handle);
        source.AddHook(KeepBelowApplications);
    }
    static nint KeepBelowApplications(nint window,int message,nint wParam,nint lParam,ref bool handled)
    {
        if(message==WindowPosChanging && lParam!=0)
        {
            var position=Marshal.PtrToStructure<WindowPosition>(lParam);
            position.InsertAfter=1; // HWND_BOTTOM
            position.Flags=(position.Flags & ~NoZOrder) | NoActivate;
            Marshal.StructureToPtr(position,lParam,false);
        }
        return 0;
    }
    public static void Move(Window widget, Point screen)
    {
        SetWindowPos(new WindowInteropHelper(widget).Handle,1,(int)screen.X,(int)screen.Y,0,0,NoSize|NoActivate);
    }
    public static bool Attach(Window widget) =>
        SetWindowPos(new WindowInteropHelper(widget).Handle,1,0,0,0,0,NoSize|NoMove|NoActivate|Show);

    public static void RevealOnDesktop(Window widget)
    {
        var name=new System.Text.StringBuilder(64);
        GetClassName(GetForegroundWindow(),name,64);
        if(name.ToString() is not ("Progman" or "WorkerW")) return;
        var handle=new WindowInteropHelper(widget).Handle;
        if(IsIconic(handle) || !IsWindowVisible(handle)) ShowWindow(handle,4);
        Attach(widget);
    }
}
