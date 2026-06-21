using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Glutin.Backend.Egl;

internal static unsafe partial class Ffi
{
#if WINDOWS
    private const string LibEgl = "libEGL.dll";
#elif ANDROID
    private const string LibEgl = "libEGL.so";
    private const string LibWaylandEgl = "libwayland-egl.so";
#else
    private const string LibEgl = "libEGL.so.1";
    private const string LibWaylandEgl = "libwayland-egl.so.1";
#endif

    [DllImport(LibEgl, EntryPoint = "eglGetError")]
    internal static extern int eglGetError();

    [DllImport(LibEgl, EntryPoint = "eglQueryString")]
    internal static extern sbyte* eglQueryString(nint display, int name);

    [DllImport(LibEgl, EntryPoint = "eglGetDisplay")]
    internal static extern nint eglGetDisplay(nint displayId);

    [DllImport(LibEgl, EntryPoint = "eglInitialize")]
    internal static extern int eglInitialize(nint display, out int major, out int minor);

    [DllImport(LibEgl, EntryPoint = "eglTerminate")]
    internal static extern int eglTerminate(nint display);

    [DllImport(LibEgl, EntryPoint = "eglGetConfigs")]
    internal static extern int eglGetConfigs(nint display, nint* configs, int configSize, out int numConfig);

    [DllImport(LibEgl, EntryPoint = "eglChooseConfig")]
    internal static extern int eglChooseConfig(
        nint display,
        int* attribList,
        nint* configs,
        int configSize,
        out int numConfig);

    [DllImport(LibEgl, EntryPoint = "eglGetConfigAttrib")]
    internal static extern int eglGetConfigAttrib(nint display, nint config, int attribute, out int value);

    [DllImport(LibEgl, EntryPoint = "eglBindAPI")]
    internal static extern int eglBindAPI(uint api);

    [DllImport(LibEgl, EntryPoint = "eglQueryAPI")]
    internal static extern uint eglQueryAPI();

    [DllImport(LibEgl, EntryPoint = "eglCreateContext")]
    internal static extern nint eglCreateContext(nint display, nint config, nint shareContext, int* attribList);

    [DllImport(LibEgl, EntryPoint = "eglDestroyContext")]
    internal static extern int eglDestroyContext(nint display, nint context);

    [DllImport(LibEgl, EntryPoint = "eglMakeCurrent")]
    internal static extern int eglMakeCurrent(nint display, nint draw, nint read, nint context);

    [DllImport(LibEgl, EntryPoint = "eglGetCurrentContext")]
    internal static extern nint eglGetCurrentContext();

    [DllImport(LibEgl, EntryPoint = "eglGetCurrentSurface")]
    internal static extern nint eglGetCurrentSurface(int readdraw);

    [DllImport(LibEgl, EntryPoint = "eglQueryContext")]
    internal static extern int eglQueryContext(nint display, nint context, int attribute, out int value);

    [DllImport(LibEgl, EntryPoint = "eglCreateWindowSurface")]
    internal static extern nint eglCreateWindowSurface(nint display, nint config, nint nativeWindow, int* attribList);

    [DllImport(LibEgl, EntryPoint = "eglCreatePbufferSurface")]
    internal static extern nint eglCreatePbufferSurface(nint display, nint config, int* attribList);

    [DllImport(LibEgl, EntryPoint = "eglCreatePixmapSurface")]
    internal static extern nint eglCreatePixmapSurface(nint display, nint config, nint nativePixmap, int* attribList);

    [DllImport(LibEgl, EntryPoint = "eglDestroySurface")]
    internal static extern int eglDestroySurface(nint display, nint surface);

    [DllImport(LibEgl, EntryPoint = "eglQuerySurface")]
    internal static extern int eglQuerySurface(nint display, nint surface, int attribute, out int value);

    [DllImport(LibEgl, EntryPoint = "eglSwapBuffers")]
    internal static extern int eglSwapBuffers(nint display, nint surface);

    [DllImport(LibEgl, EntryPoint = "eglSwapInterval")]
    internal static extern int eglSwapInterval(nint display, int interval);

    [DllImport(LibEgl, EntryPoint = "eglGetProcAddress")]
    internal static extern nint eglGetProcAddress(byte* procName);

#if !WINDOWS
    [DllImport(LibWaylandEgl, EntryPoint = "wl_egl_window_create")]
    internal static extern nint wl_egl_window_create(nint surface, int width, int height);

    [DllImport(LibWaylandEgl, EntryPoint = "wl_egl_window_resize")]
    internal static extern void wl_egl_window_resize(nint eglWindow, int width, int height, int dx, int dy);

    [DllImport(LibWaylandEgl, EntryPoint = "wl_egl_window_destroy")]
    internal static extern void wl_egl_window_destroy(nint eglWindow);
#endif

    internal static HashSet<string> LoadExtensionSet(nint display)
    {
        sbyte* extensions = eglQueryString(display, EglConstants.Extensions);
        if (extensions is null)
        {
            return [];
        }

        string? extensionString = Marshal.PtrToStringAnsi((nint)extensions);
        return extensionString is { Length: > 0 }
            ? extensionString.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal)
            : [];
    }

    internal static nint LoadProc(string name)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(name + '\0');
        fixed (byte* ptr = bytes)
        {
            return eglGetProcAddress(ptr);
        }
    }

    internal static GlutinException LastError(string operation)
    {
        int error = eglGetError();
        string description = error == EglConstants.Success
            ? new Win32Exception(Marshal.GetLastWin32Error()).Message
            : ErrorName(error);
        return new GlutinException($"{operation} failed: {description}");
    }

    internal static void Check(string operation, bool success)
    {
        if (!success)
        {
            throw LastError(operation);
        }
    }

    internal static string ErrorName(int error)
    {
        return error switch
        {
            EglConstants.Success => "EGL_SUCCESS",
            EglConstants.NotInitialized => "EGL_NOT_INITIALIZED",
            EglConstants.BadAccess => "EGL_BAD_ACCESS",
            EglConstants.BadAlloc => "EGL_BAD_ALLOC",
            EglConstants.BadAttribute => "EGL_BAD_ATTRIBUTE",
            EglConstants.BadConfig => "EGL_BAD_CONFIG",
            EglConstants.BadContext => "EGL_BAD_CONTEXT",
            EglConstants.BadCurrentSurface => "EGL_BAD_CURRENT_SURFACE",
            EglConstants.BadDisplay => "EGL_BAD_DISPLAY",
            EglConstants.BadMatch => "EGL_BAD_MATCH",
            EglConstants.BadNativePixmap => "EGL_BAD_NATIVE_PIXMAP",
            EglConstants.BadNativeWindow => "EGL_BAD_NATIVE_WINDOW",
            EglConstants.BadParameter => "EGL_BAD_PARAMETER",
            EglConstants.BadSurface => "EGL_BAD_SURFACE",
            _ => $"0x{error:X4}",
        };
    }
}
