using RawWindowHandles;
using GlutinConfig = Glutin.Config;
using GlutinDisplay = Glutin.Display;
using GlutinPossiblyCurrentContext = Glutin.PossiblyCurrentContext;

namespace Glutin.Backend.Egl;

public sealed unsafe class Surface<TSurface> : IPlatformGlSurface<TSurface>
    where TSurface : struct, ISurfaceType
{
    private readonly Display _display;
    private readonly Config _config;
    private readonly NativeWindow? _nativeWindow;
    private nint _raw;
    private bool _disposed;

    private Surface(Display display, Config config, NativeWindow? nativeWindow, nint raw)
    {
        _display = display;
        _config = config;
        _nativeWindow = nativeWindow;
        _raw = raw;
    }

    internal nint Raw => _raw;

    internal static Surface<WindowSurface> CreateWindow(
        Display display,
        Config config,
        SurfaceAttributes<WindowSurface> attributes)
    {
        if (attributes.RawWindowHandle is not { } rawWindowHandle)
        {
            throw new GlutinException("EGL window surface requires a raw window handle.");
        }

        uint width = attributes.Width ?? throw new GlutinException("EGL window surface width is required.");
        uint height = attributes.Height ?? throw new GlutinException("EGL window surface height is required.");
        NativeWindow nativeWindow = NativeWindow.Create(rawWindowHandle, width, height);

        List<nint> attribs = BuildSurfaceAttributes(config, attributes.Srgb, attributes.SingleBuffer);
        try
        {
            nint raw = CreateWindowSurface(display, config, nativeWindow, attribs);
            if (raw == 0)
            {
                throw Ffi.LastError("eglCreateWindowSurface");
            }

            return new Surface<WindowSurface>(display, config, nativeWindow, raw);
        }
        catch
        {
            nativeWindow.Dispose();
            throw;
        }
    }

    internal static Surface<PbufferSurface> CreatePbuffer(
        Display display,
        Config config,
        SurfaceAttributes<PbufferSurface> attributes)
    {
        int width = checked((int)(attributes.Width ?? throw new GlutinException("EGL pbuffer width is required.")));
        int height = checked((int)(attributes.Height ?? throw new GlutinException("EGL pbuffer height is required.")));

        Span<int> attrs =
        [
            EglConstants.Width, width,
            EglConstants.Height, height,
            EglConstants.LargestPbuffer, attributes.LargestPbuffer ? EglConstants.True : EglConstants.False,
            EglConstants.None,
        ];

        nint raw;
        fixed (int* attrsPtr = attrs)
        {
            raw = Ffi.eglCreatePbufferSurface(display.Raw, config.Raw, attrsPtr);
        }

        if (raw == 0)
        {
            throw Ffi.LastError("eglCreatePbufferSurface");
        }

        return new Surface<PbufferSurface>(display, config, null, raw);
    }

    internal static Surface<PixmapSurface> CreatePixmap(
        Display display,
        Config config,
        SurfaceAttributes<PixmapSurface> attributes)
    {
        if (attributes.NativePixmap is not { } nativePixmap)
        {
            throw new GlutinException("EGL pixmap surface requires a native pixmap.");
        }

        List<nint> attribs = BuildSurfaceAttributes(config, attributes.Srgb, singleBuffer: false);
        nint raw = CreatePixmapSurface(display, config, nativePixmap, attribs);
        if (raw == 0)
        {
            throw Ffi.LastError("eglCreatePixmapSurface");
        }

        return new Surface<PixmapSurface>(display, config, null, raw);
    }

    public uint BufferAge => _display.Extensions.Contains("EGL_EXT_buffer_age")
        ? RawAttribute(EglConstants.BufferAgeExt)
        : 0;

    public uint? Width => RawAttribute(EglConstants.Width);

    public uint? Height => RawAttribute(EglConstants.Height);

    public bool IsSingleBuffered => RawAttribute(EglConstants.RenderBuffer) == EglConstants.SingleBuffer;

    public GlutinDisplay Display => _display.Facade;

    public GlutinConfig Config => new(_config);

    public RawSurface RawSurface => new(new RawSurface.Egl(_raw));

    public void SwapBuffers(GlutinPossiblyCurrentContext context)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        RequireContextApi(context);

        if (Ffi.eglSwapBuffers(_display.Raw, _raw) == EglConstants.False)
        {
            throw Ffi.LastError("eglSwapBuffers");
        }
    }

    public bool IsCurrent(GlutinPossiblyCurrentContext context)
    {
        return IsCurrentDraw(context) && IsCurrentRead(context);
    }

    public bool IsCurrentDraw(GlutinPossiblyCurrentContext context)
    {
        return TryBindContextApi(context) && Ffi.eglGetCurrentSurface(EglConstants.Draw) == _raw;
    }

    public bool IsCurrentRead(GlutinPossiblyCurrentContext context)
    {
        return TryBindContextApi(context) && Ffi.eglGetCurrentSurface(EglConstants.Read) == _raw;
    }

    public void SetSwapInterval(GlutinPossiblyCurrentContext context, SwapInterval interval)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        RequireContextApi(context);

        int value = interval.TryGetWait(out uint wait) ? checked((int)wait) : 0;
        if (Ffi.eglSwapInterval(_display.Raw, value) == EglConstants.False)
        {
            throw Ffi.LastError("eglSwapInterval");
        }
    }

    public void Resize(GlutinPossiblyCurrentContext context, uint width, uint height)
    {
        _nativeWindow?.Resize(width, height);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_raw != 0)
        {
            _ = Ffi.eglDestroySurface(_display.Raw, _raw);
            _raw = 0;
        }

        _nativeWindow?.Dispose();
    }

    private uint RawAttribute(int attribute)
    {
        return Ffi.eglQuerySurface(_display.Raw, _raw, attribute, out int value) == EglConstants.True
            ? checked((uint)value)
            : 0;
    }

    private static void RequireContextApi(GlutinPossiblyCurrentContext context)
    {
        if (!TryBindContextApi(context))
        {
            throw new GlutinException("EGL surface received a context from another backend.");
        }
    }

    private static bool TryBindContextApi(GlutinPossiblyCurrentContext context)
    {
        if (context.Backend is not PossiblyCurrentContext eglContext)
        {
            return false;
        }

        eglContext.BindApi();
        return true;
    }

    private static nint CreateWindowSurface(
        Display display,
        Config config,
        NativeWindow nativeWindow,
        List<nint> attribs)
    {
        if (display.Kind == EglDisplayKind.Khr && display.EglExtra.HasCreatePlatformWindowSurface)
        {
            fixed (nint* attrsPtr = attribs.ToArray())
            {
            return nativeWindow.Kind == NativeWindowKind.Xlib
                ? CreatePlatformWindowSurfaceX11(display, config, nativeWindow, attrsPtr)
                : display.EglExtra.CreatePlatformWindowSurface(display.Raw, config.Raw, nativeWindow.PlatformWindow, attrsPtr);
            }
        }

        if (display.Kind == EglDisplayKind.Ext && display.EglExtra.HasCreatePlatformWindowSurfaceEXT)
        {
            int[] intAttrs = ToIntAttributes(attribs);
            fixed (int* attrsPtr = intAttrs)
            {
                return nativeWindow.Kind == NativeWindowKind.Xlib
                    ? CreatePlatformWindowSurfaceX11Ext(display, config, nativeWindow, attrsPtr)
                    : display.EglExtra.CreatePlatformWindowSurfaceEXT(display.Raw, config.Raw, nativeWindow.PlatformWindow, attrsPtr);
            }
        }

        int[] legacyAttrs = ToIntAttributes(attribs);
        fixed (int* attrsPtr = legacyAttrs)
        {
            return Ffi.eglCreateWindowSurface(display.Raw, config.Raw, nativeWindow.NativeHandle, attrsPtr);
        }
    }

    private static nint CreatePixmapSurface(
        Display display,
        Config config,
        NativePixmap nativePixmap,
        List<nint> attribs)
    {
        if (!TryGetNativePixmap(nativePixmap, out nint pixmap))
        {
            throw new GlutinException("provided native pixmap is not supported by EGL");
        }

        if (display.Kind == EglDisplayKind.Khr && display.EglExtra.HasCreatePlatformPixmapSurface)
        {
            fixed (nint* attrsPtr = attribs.ToArray())
            {
                return display.EglExtra.CreatePlatformPixmapSurface(display.Raw, config.Raw, pixmap, attrsPtr);
            }
        }

        if (display.Kind == EglDisplayKind.Ext && display.EglExtra.HasCreatePlatformPixmapSurfaceEXT)
        {
            int[] intAttrs = ToIntAttributes(attribs);
            fixed (int* attrsPtr = intAttrs)
            {
                return display.EglExtra.CreatePlatformPixmapSurfaceEXT(display.Raw, config.Raw, pixmap, attrsPtr);
            }
        }

        int[] legacyAttrs = ToIntAttributes(attribs);
        fixed (int* attrsPtr = legacyAttrs)
        {
            return Ffi.eglCreatePixmapSurface(display.Raw, config.Raw, pixmap, attrsPtr);
        }
    }

    private static nint CreatePlatformWindowSurfaceX11(
        Display display,
        Config config,
        NativeWindow nativeWindow,
        nint* attrsPtr)
    {
        nuint window = checked((nuint)nativeWindow.NativeHandle);
        return display.EglExtra.CreatePlatformWindowSurface(display.Raw, config.Raw, (nint)(&window), attrsPtr);
    }

    private static nint CreatePlatformWindowSurfaceX11Ext(
        Display display,
        Config config,
        NativeWindow nativeWindow,
        int* attrsPtr)
    {
        nuint window = checked((nuint)nativeWindow.NativeHandle);
        return display.EglExtra.CreatePlatformWindowSurfaceEXT(display.Raw, config.Raw, (nint)(&window), attrsPtr);
    }

    private static List<nint> BuildSurfaceAttributes(Config config, bool? srgb, bool singleBuffer)
    {
        var attrs = new List<nint>
        {
            EglConstants.RenderBuffer,
            singleBuffer ? EglConstants.SingleBuffer : EglConstants.BackBuffer,
        };

        if (srgb is { } requestedSrgb && config.SrgbCapable)
        {
            attrs.Add(EglConstants.GlColorspace);
            attrs.Add(requestedSrgb ? EglConstants.GlColorspaceSrgb : EglConstants.GlColorspaceLinear);
        }

        attrs.Add(EglConstants.None);
        return attrs;
    }

    private static int[] ToIntAttributes(List<nint> attribs)
    {
        var result = new int[attribs.Count];
        for (int i = 0; i < attribs.Count; i++)
        {
            result[i] = checked((int)attribs[i]);
        }

        return result;
    }

    private static bool TryGetNativePixmap(NativePixmap pixmap, out nint value)
    {
        if (pixmap.TryGetValue(out NativePixmap.XlibPixmap xlib))
        {
            value = checked((nint)xlib.Pixmap);
            return value != 0;
        }

        if (pixmap.TryGetValue(out NativePixmap.XcbPixmap xcb))
        {
            value = checked((nint)xcb.Pixmap);
            return value != 0;
        }

        if (pixmap.TryGetValue(out NativePixmap.WindowsPixmap windows))
        {
            value = windows.Bitmap;
            return value != 0;
        }

        value = 0;
        return false;
    }
}

internal sealed class NativeWindow : IDisposable
{
    private bool _disposed;

    private NativeWindow(NativeWindowKind kind, nint nativeWindow, nint platformWindow, bool ownsPlatformWindow)
    {
        Kind = kind;
        NativeHandle = nativeWindow;
        PlatformWindow = platformWindow;
        OwnsPlatformWindow = ownsPlatformWindow;
    }

    internal NativeWindowKind Kind { get; }

    internal nint NativeHandle { get; }

    internal nint PlatformWindow { get; }

    internal bool OwnsPlatformWindow { get; }

    internal static NativeWindow Create(RawWindowHandle rawWindowHandle, uint width, uint height)
    {
        if (rawWindowHandle.TryGetValue(out RawWindowHandle.AndroidNdk android))
        {
            if (android.ANativeWindow == 0)
            {
                throw new GlutinException("provided Android native window is null");
            }

            return new NativeWindow(NativeWindowKind.Android, android.ANativeWindow, android.ANativeWindow, ownsPlatformWindow: false);
        }

        if (rawWindowHandle.TryGetValue(out RawWindowHandle.Xlib xlib))
        {
            if (xlib.Window == 0)
            {
                throw new GlutinException("provided Xlib native window is null");
            }

            return new NativeWindow(NativeWindowKind.Xlib, checked((nint)xlib.Window), checked((nint)xlib.Window), ownsPlatformWindow: false);
        }

        if (rawWindowHandle.TryGetValue(out RawWindowHandle.Wayland wayland))
        {
            if (wayland.Surface == 0)
            {
                throw new GlutinException("provided Wayland native surface is null");
            }

#if ANDROID
            throw new GlutinException("Wayland EGL windows are not supported on Android.");
#else
            if (OperatingSystem.IsWindows())
            {
                throw new GlutinException("Wayland EGL windows are not supported on Windows.");
            }

            nint eglWindow = Ffi.wl_egl_window_create(wayland.Surface, checked((int)width), checked((int)height));
            if (eglWindow == 0)
            {
                throw new GlutinException("wl_egl_window_create failed.");
            }

            return new NativeWindow(NativeWindowKind.Wayland, eglWindow, eglWindow, ownsPlatformWindow: true);
#endif
        }

        if (rawWindowHandle.TryGetValue(out RawWindowHandle.Win32 win32))
        {
            if (win32.Hwnd == 0)
            {
                throw new GlutinException("provided Win32 native window is null");
            }

            return new NativeWindow(NativeWindowKind.Win32, win32.Hwnd, win32.Hwnd, ownsPlatformWindow: false);
        }

        throw new GlutinException("provided native window is not supported by EGL");
    }

    internal void Resize(uint width, uint height)
    {
#if !ANDROID
        if (Kind == NativeWindowKind.Wayland && NativeHandle != 0)
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }

            Ffi.wl_egl_window_resize(NativeHandle, checked((int)width), checked((int)height), 0, 0);
        }
#endif
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

#if !ANDROID
        if (Kind == NativeWindowKind.Wayland && NativeHandle != 0 && OwnsPlatformWindow)
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }

            Ffi.wl_egl_window_destroy(NativeHandle);
        }
#endif
    }
}

internal enum NativeWindowKind
{
    Android,
    Xlib,
    Wayland,
    Win32,
}
