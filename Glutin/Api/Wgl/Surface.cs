#if !ANDROID
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
    private readonly WglSurfaceKind _kind;
    private readonly nint _handle;
    private readonly nint _hdc;
    private bool _disposed;

    private Surface(Display display, Config config, WglSurfaceKind kind, nint handle, nint hdc)
    {
        _display = display;
        _config = config;
        _kind = kind;
        _handle = handle;
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

        return new Surface<WindowSurface>(display, config, WglSurfaceKind.Window, win32.Hwnd, hdc);
    }

    internal static Surface<PbufferSurface> CreatePbuffer(
        Display display,
        Config config,
        SurfaceAttributes<PbufferSurface> attributes)
    {
        WglExtensions extra = display.Wgl is { HasPbufferARB: true } wgl
            && display.Extensions.Contains("WGL_ARB_pbuffer")
                ? wgl
                : throw new GlutinException("pbuffer extensions are not supported");

        if (!config.ConfigSurfaceTypes.HasFlag(ConfigSurfaceTypes.Pbuffer))
        {
            throw new GlutinException("WGL config does not support pbuffer surfaces.");
        }

        int width = checked((int)(attributes.Width ?? throw new GlutinException("WGL pbuffer width is required.")));
        int height = checked((int)(attributes.Height ?? throw new GlutinException("WGL pbuffer height is required.")));

        Span<int> attrs = attributes.LargestPbuffer
            ? [WglConstants.PbufferLargestArb, 1, 0]
            : [0];

        nint hpbuffer;
        fixed (int* attrsPtr = attrs)
        {
            hpbuffer = extra.CreatePbufferARB(config.Hdc, config.PixelFormatIndex, width, height, attrsPtr);
        }

        if (hpbuffer == 0)
        {
            throw Ffi.LastError("wglCreatePbufferARB");
        }

        nint hdc = extra.GetPbufferDCARB(hpbuffer);
        if (hdc == 0)
        {
            extra.DestroyPbufferARB(hpbuffer);
            throw Ffi.LastError("wglGetPbufferDCARB");
        }

        return new Surface<PbufferSurface>(display, config, WglSurfaceKind.Pbuffer, hpbuffer, hdc);
    }

    public uint BufferAge => 0;

    public uint? Width
    {
        get
        {
            return _kind switch
            {
                WglSurfaceKind.Window => Ffi.GetClientRect(_handle, out Ffi.RECT rect)
                    ? (uint)Math.Max(0, rect.Right - rect.Left)
                    : null,
                WglSurfaceKind.Pbuffer => PbufferAttribute(WglConstants.PbufferWidthArb),
                _ => null,
            };
        }
    }

    public uint? Height
    {
        get
        {
            return _kind switch
            {
                WglSurfaceKind.Window => Ffi.GetClientRect(_handle, out Ffi.RECT rect)
                    ? (uint)Math.Max(0, rect.Bottom - rect.Top)
                    : null,
                WglSurfaceKind.Pbuffer => PbufferAttribute(WglConstants.PbufferHeightArb),
                _ => null,
            };
        }
    }

    public bool IsSingleBuffered => _config.IsSingleBuffered;

    public GlutinDisplay Display => _display.Facade;

    public GlutinConfig Config => new(_config);

    public RawSurface RawSurface => new(new RawSurface.Wgl(_handle));

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

        if (_kind != WglSurfaceKind.Window)
        {
            throw new GlutinException("swap control is not supported for this WGL surface");
        }

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
        // WGL surfaces follow their native size. Pbuffers are immutable.
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        switch (_kind)
        {
            case WglSurfaceKind.Window:
                Ffi.ReleaseDC(_handle, _hdc);
                break;
            case WglSurfaceKind.Pbuffer:
                if (_display.Wgl is { HasPbufferARB: true } extra)
                {
                    extra.ReleasePbufferDCARB(_handle, _hdc);
                    extra.DestroyPbufferARB(_handle);
                }

                break;
        }
    }

    private uint? PbufferAttribute(int attribute)
    {
        if (_display.Wgl is not { HasPbufferARB: true } extra)
        {
            return null;
        }

        int value = 0;
        return extra.QueryPbufferARB(_handle, attribute, &value) != 0
            ? (uint)Math.Max(0, value)
            : null;
    }
}

internal enum WglSurfaceKind
{
    Window,
    Pbuffer,
}
#endif
