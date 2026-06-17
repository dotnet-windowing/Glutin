#if WINDOWS
using RawWindowHandles;
using GlutinConfig = Glutin.Config;
using GlutinDisplay = Glutin.Display;
using GlutinPossiblyCurrentContext = Glutin.PossiblyCurrentContext;

namespace Glutin.Backend.Wgl;

public sealed unsafe class Surface<TSurface> : IPlatformGlSurface<TSurface>
    where TSurface : struct, ISurfaceType
{
    private readonly Display _display;
    private readonly Config _config;
    private readonly nint _hwnd;
    private readonly nint _hdc;
    private bool _disposed;

    private Surface(Display display, Config config, nint hwnd, nint hdc)
    {
        _display = display;
        _config = config;
        _hwnd = hwnd;
        _hdc = hdc;
    }

    internal nint Hdc => _hdc;

    internal static Surface<WindowSurface> CreateWindow(
        Display display,
        Config config,
        SurfaceAttributes<WindowSurface> attributes)
    {
        if (attributes.RawWindowHandle is not { } rawWindowHandle)
        {
            throw new GlutinException("WGL window surface requires a raw window handle.");
        }

        if (!rawWindowHandle.TryGetValue(out RawWindowHandle.Win32 win32))
        {
            throw new GlutinException("provided native window is not supported by WGL");
        }

        config.ApplyOnNativeWindow(rawWindowHandle);

        nint hdc = Ffi.GetDC(win32.Hwnd);
        if (hdc == 0)
        {
            throw Ffi.LastError("GetDC");
        }

        return new Surface<WindowSurface>(display, config, win32.Hwnd, hdc);
    }

    public uint BufferAge => 0;

    public uint? Width
    {
        get
        {
            return Ffi.GetClientRect(_hwnd, out Ffi.RECT rect)
                ? (uint)Math.Max(0, rect.Right - rect.Left)
                : null;
        }
    }

    public uint? Height
    {
        get
        {
            return Ffi.GetClientRect(_hwnd, out Ffi.RECT rect)
                ? (uint)Math.Max(0, rect.Bottom - rect.Top)
                : null;
        }
    }

    public bool IsSingleBuffered => _config.IsSingleBuffered;

    public GlutinDisplay Display => _display.Facade;

    public GlutinConfig Config => new(_config);

    public RawSurface RawSurface => new(new RawSurface.Wgl(_hwnd));

    public void SwapBuffers(GlutinPossiblyCurrentContext context)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!Ffi.SwapBuffers(_hdc))
        {
            throw Ffi.LastError("SwapBuffers");
        }
    }

    public bool IsCurrent(GlutinPossiblyCurrentContext context)
    {
        return context.Backend is PossiblyCurrentContext wglContext && wglContext.Raw == Ffi.wglGetCurrentContext();
    }

    public bool IsCurrentDraw(GlutinPossiblyCurrentContext context)
    {
        return IsCurrent(context);
    }

    public bool IsCurrentRead(GlutinPossiblyCurrentContext context)
    {
        return IsCurrent(context);
    }

    public void SetSwapInterval(GlutinPossiblyCurrentContext context, SwapInterval interval)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        WglExtensions extra = _display.Wgl is { HasSwapIntervalEXT: true } wgl
            && _display.Features.HasFlag(DisplayFeatures.SwapControl)
                ? wgl
                : throw new GlutinException("swap control extensions are not supported");

        int value = interval.TryGetWait(out uint wait) ? checked((int)wait) : 0;
        if (extra.SwapIntervalEXT(value) == 0)
        {
            throw Ffi.LastError("wglSwapIntervalEXT");
        }
    }

    public void Resize(GlutinPossiblyCurrentContext context, uint width, uint height)
    {
        // WGL window surfaces follow the native HWND size.
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Ffi.ReleaseDC(_hwnd, _hdc);
    }
}
#endif
