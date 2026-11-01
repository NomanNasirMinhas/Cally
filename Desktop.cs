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
    public static bool Attach(Window widget)
    {
        nint view=0;
        EnumWindows((window,_) => { view=FindWindowEx(window,0,"SHELLDLL_DefView",null); return view==0; },0);
        if(view==0) return false;
        var handle=new WindowInteropHelper(widget).Handle;
        var pos=widget.PointToScreen(new(0,0));
        var pixel=new Pixel { X=(int)pos.X,Y=(int)pos.Y };
        ScreenToClient(view,ref pixel);
        int style=GetWindowLong(handle,-16);
        SetWindowLong(handle,-16,(style & ~unchecked((int)0x80000000)) | 0x40000000);
        Marshal.SetLastPInvokeError(0);
        if(SetParent(handle,view)==0 && Marshal.GetLastPInvokeError()!=0) { SetWindowLong(handle,-16,style); return false; }
        SetWindowPos(handle,0,pixel.X,pixel.Y,0,0,0x31);
        return true;
    }
}

