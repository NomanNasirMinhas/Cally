using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Cally;

// Explorer owns the icon view. Parent only the compact widget to this layer.
internal static class Desktop
{
    delegate bool EnumWindow(nint window, nint parameter);
    [DllImport("user32")] static extern bool EnumWindows(EnumWindow callback, nint parameter);
    [DllImport("user32", CharSet=CharSet.Unicode)] static extern nint FindWindowEx(nint parent,nint after,string cls,string? title);
    [DllImport("user32",SetLastError=true)] static extern nint SetParent(nint child,nint parent);
    [DllImport("user32")] static extern int GetWindowLong(nint window,int index);
    [DllImport("user32")] static extern int SetWindowLong(nint window,int index,int value);
    [DllImport("user32")] static extern bool SetWindowPos(nint window,nint order,int x,int y,int w,int h,uint flags);
    [DllImport("user32")] static extern bool ScreenToClient(nint parent, ref Pixel point);
    [StructLayout(LayoutKind.Sequential)] struct Pixel { public int X,Y; }
    [DllImport("user32")] static extern nint GetParent(nint window);
    public static void Move(Window widget, Point screen)
    {
        var handle = new WindowInteropHelper(widget).Handle;
        var point = new Pixel { X=(int)screen.X,Y=(int)screen.Y };
        var parent = GetParent(handle);
        if(parent != 0) ScreenToClient(parent,ref point);
        SetWindowPos(handle,0,point.X,point.Y,0,0,0x15);
    }
    [DllImport("user32")] static extern bool IsWindowVisible(nint window);
    [DllImport("user32",CharSet=CharSet.Unicode)] static extern int GetClassName(nint window,System.Text.StringBuilder name,int size);
    public static void Detach(Window widget)
    {
        var handle=new WindowInteropHelper(widget).Handle;
        if((GetWindowLong(handle,-16) & 0x40000000)==0) return;
        SetParent(handle,0);
        SetWindowLong(handle,-16,(GetWindowLong(handle,-16) & ~0x40000000) | unchecked((int)0x80000000));
        SetWindowPos(handle,0,0,0,0,0,0x63);
    }
    [DllImport("user32")] static extern nint GetForegroundWindow();
    [DllImport("user32")] static extern bool IsIconic(nint window);
    [DllImport("user32")] static extern bool ShowWindow(nint window,int command);
    public static void RevealOnDesktop(Window widget)
    {
        var name=new System.Text.StringBuilder(64);
        GetClassName(GetForegroundWindow(),name,64);
        if(name.ToString() is not ("Progman" or "WorkerW")) return;
        var handle=new WindowInteropHelper(widget).Handle;
        if(IsIconic(handle) || !IsWindowVisible(handle)) ShowWindow(handle,4);
        SetWindowPos(handle,0,0,0,0,0,0x53);
    }
    public static bool Attach(Window widget)
    {
        // Keep the layered WPF surface in its own compositor window.
        var handle=new WindowInteropHelper(widget).Handle;
        return SetWindowPos(handle,1,0,0,0,0,0x53);
    }
}
