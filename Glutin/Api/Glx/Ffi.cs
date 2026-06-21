#if !WINDOWS
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Glutin.Backend.Glx;

internal static unsafe partial class Ffi
{
    private const string LibGl = "libGL.so.1";
    private const string LibX11 = "libX11.so.6";

    internal const nint VisualIdMask = 0x1;

    [DllImport(LibGl, EntryPoint = "glXQueryExtension")]
    internal static extern int glXQueryExtension(nint display, out int errorBase, out int eventBase);

    [DllImport(LibGl, EntryPoint = "glXQueryVersion")]
    internal static extern int glXQueryVersion(nint display, out int major, out int minor);

    [DllImport(LibGl, EntryPoint = "glXGetClientString")]
    internal static extern sbyte* glXGetClientString(nint display, int name);

    [DllImport(LibGl, EntryPoint = "glXChooseFBConfig")]
    internal static extern nint glXChooseFBConfig(nint display, int screen, int* attribList, out int count);

    [DllImport(LibGl, EntryPoint = "glXGetFBConfigAttrib")]
    internal static extern int glXGetFBConfigAttrib(nint display, nint config, int attribute, out int value);

    [DllImport(LibGl, EntryPoint = "glXGetVisualFromFBConfig")]
    internal static extern XVisualInfo* glXGetVisualFromFBConfig(nint display, nint config);

    [DllImport(LibGl, EntryPoint = "glXCreateWindow")]
    internal static extern nuint glXCreateWindow(nint display, nint config, nuint window, int* attribList);

    [DllImport(LibGl, EntryPoint = "glXDestroyWindow")]
    internal static extern void glXDestroyWindow(nint display, nuint window);

    [DllImport(LibGl, EntryPoint = "glXCreatePbuffer")]
    internal static extern nuint glXCreatePbuffer(nint display, nint config, int* attribList);

    [DllImport(LibGl, EntryPoint = "glXDestroyPbuffer")]
    internal static extern void glXDestroyPbuffer(nint display, nuint pbuffer);

    [DllImport(LibGl, EntryPoint = "glXCreatePixmap")]
    internal static extern nuint glXCreatePixmap(nint display, nint config, nuint pixmap, int* attribList);

    [DllImport(LibGl, EntryPoint = "glXDestroyPixmap")]
    internal static extern void glXDestroyPixmap(nint display, nuint pixmap);

    [DllImport(LibGl, EntryPoint = "glXQueryDrawable")]
    internal static extern void glXQueryDrawable(nint display, nuint drawable, int attribute, out uint value);

    [DllImport(LibGl, EntryPoint = "glXSwapBuffers")]
    internal static extern void glXSwapBuffers(nint display, nuint drawable);

    [DllImport(LibGl, EntryPoint = "glXCreateNewContext")]
    internal static extern nint glXCreateNewContext(nint display, nint config, int renderType, nint shareList, int direct);

    [DllImport(LibGl, EntryPoint = "glXMakeContextCurrent")]
    internal static extern int glXMakeContextCurrent(nint display, nuint draw, nuint read, nint context);

    [DllImport(LibGl, EntryPoint = "glXDestroyContext")]
    internal static extern void glXDestroyContext(nint display, nint context);

    [DllImport(LibGl, EntryPoint = "glXGetCurrentContext")]
    internal static extern nint glXGetCurrentContext();

    [DllImport(LibGl, EntryPoint = "glXGetCurrentDrawable")]
    internal static extern nuint glXGetCurrentDrawable();

    [DllImport(LibGl, EntryPoint = "glXGetCurrentReadDrawable")]
    internal static extern nuint glXGetCurrentReadDrawable();

    [DllImport(LibGl, EntryPoint = "glXGetProcAddressARB")]
    internal static extern nint glXGetProcAddress(byte* procName);

    [DllImport(LibX11, EntryPoint = "XFree")]
    internal static extern int XFree(nint data);

    [DllImport(LibX11, EntryPoint = "XSync")]
    internal static extern int XSync(nint display, int discard);

    internal static GlutinException LastError(string operation)
    {
        return new GlutinException($"{operation} failed: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
    }

    internal static void Check(string operation, bool success)
    {
        if (!success)
        {
            throw LastError(operation);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct XVisualInfo
    {
        public nint Visual;
        public nuint VisualId;
        public int Screen;
        public int Depth;
        public int Class;
        public nuint RedMask;
        public nuint GreenMask;
        public nuint BlueMask;
        public int ColormapSize;
        public int BitsPerRgb;
    }
}
#endif
