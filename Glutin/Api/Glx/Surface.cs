#if !ANDROID
using RawWindowHandles;
using GlutinConfig = Glutin.Config;
using GlutinDisplay = Glutin.Display;
using GlutinPossiblyCurrentContext = Glutin.PossiblyCurrentContext;

namespace Glutin.Backend.Glx;

public sealed unsafe class Surface<TSurface> : IPlatformGlSurface<TSurface>
    where TSurface : struct, ISurfaceType
{
    private readonly Display _display;
    private readonly Config _config;
    private readonly GlxSurfaceKind _kind;
    private nuint _raw;
    private bool _disposed;

    private Surface(Display display, Config config, GlxSurfaceKind kind, nuint raw)
    {
        _display = display;
        _config = config;
        _kind = kind;
        _raw = raw;
    }

    internal nuint Raw => _raw;

    internal static Surface<WindowSurface> CreateWindow(
        Display display,
        Config config,
        SurfaceAttributes<WindowSurface> attributes)
    {
        if (attributes.RawWindowHandle is not { } rawWindowHandle)
        {
            throw new GlutinException("GLX window surface requires a raw window handle.");
        }

        if (!rawWindowHandle.TryGetValue(out RawWindowHandle.Xlib xlib) || xlib.Window == 0)
        {
            throw new GlutinException("provided native window is not supported by GLX");
        }

        Span<int> attrs = [0];
        nuint surface;
        fixed (int* attrsPtr = attrs)
        {
            surface = Ffi.glXCreateWindow(display.Raw, config.Raw, xlib.Window, attrsPtr);
        }

        if (surface == 0)
        {
            throw new GlutinException("glXCreateWindow failed.");
        }

        return new Surface<WindowSurface>(display, config, GlxSurfaceKind.Window, surface);
    }

    internal static Surface<PbufferSurface> CreatePbuffer(
        Display display,
        Config config,
        SurfaceAttributes<PbufferSurface> attributes)
    {
        int width = checked((int)(attributes.Width ?? throw new GlutinException("GLX pbuffer width is required.")));
        int height = checked((int)(attributes.Height ?? throw new GlutinException("GLX pbuffer height is required.")));

        Span<int> attrs =
        [
            GlxConstants.PbufferWidth, width,
            GlxConstants.PbufferHeight, height,
            GlxConstants.LargestPbuffer, attributes.LargestPbuffer ? 1 : 0,
            0,
        ];

        nuint surface;
        fixed (int* attrsPtr = attrs)
        {
            surface = Ffi.glXCreatePbuffer(display.Raw, config.Raw, attrsPtr);
        }

        if (surface == 0)
        {
            throw new GlutinException("glXCreatePbuffer failed.");
        }

        return new Surface<PbufferSurface>(display, config, GlxSurfaceKind.Pbuffer, surface);
    }

    internal static Surface<PixmapSurface> CreatePixmap(
        Display display,
        Config config,
        SurfaceAttributes<PixmapSurface> attributes)
    {
        if (attributes.NativePixmap is not { } nativePixmap
            || !nativePixmap.TryGetValue(out NativePixmap.XlibPixmap xlib)
            || xlib.Pixmap == 0)
        {
            throw new GlutinException("provided native pixmap is not supported by GLX");
        }

        Span<int> attrs = [0];
        nuint surface;
        fixed (int* attrsPtr = attrs)
        {
            surface = Ffi.glXCreatePixmap(display.Raw, config.Raw, xlib.Pixmap, attrsPtr);
        }

        if (surface == 0)
        {
            throw new GlutinException("glXCreatePixmap failed.");
        }

        return new Surface<PixmapSurface>(display, config, GlxSurfaceKind.Pixmap, surface);
    }

    public uint BufferAge => _display.Extensions.Contains("GLX_EXT_buffer_age")
        ? DrawableAttribute(GlxConstants.BackBufferAgeExt)
        : 0;

    public uint? Width => DrawableAttribute(GlxConstants.Width);

    public uint? Height => DrawableAttribute(GlxConstants.Height);

    public bool IsSingleBuffered => _config.IsSingleBuffered;

    public GlutinDisplay Display => _display.Facade;

    public GlutinConfig Config => new(_config);

    public RawSurface RawSurface => new(new RawSurface.Glx((ulong)_raw));

    public void SwapBuffers(GlutinPossiblyCurrentContext context)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Ffi.glXSwapBuffers(_display.Raw, _raw);
    }

    public bool IsCurrent(GlutinPossiblyCurrentContext context)
    {
        return IsCurrentDraw(context) && IsCurrentRead(context);
    }

    public bool IsCurrentDraw(GlutinPossiblyCurrentContext context)
    {
        return context.Backend is PossiblyCurrentContext && Ffi.glXGetCurrentDrawable() == _raw;
    }

    public bool IsCurrentRead(GlutinPossiblyCurrentContext context)
    {
        return context.Backend is PossiblyCurrentContext && Ffi.glXGetCurrentReadDrawable() == _raw;
    }

    public void SetSwapInterval(GlutinPossiblyCurrentContext context, SwapInterval interval)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_display.Features.HasFlag(DisplayFeatures.SwapControl))
        {
            throw new GlutinException("swap control extensions are not supported");
        }

        int value = interval.TryGetWait(out uint wait) ? checked((int)wait) : 0;
        GlxExtensions extra = _display.GlxExtra;

        if (_display.Extensions.Contains("GLX_EXT_swap_control") && extra.HasSwapIntervalEXT)
        {
            extra.SwapIntervalEXT(_display.Raw, _raw, value);
            return;
        }

        if (_display.Extensions.Contains("GLX_MESA_swap_control") && extra.HasSwapIntervalMESA)
        {
            if (extra.SwapIntervalMESA((uint)value) == 0)
            {
                return;
            }
        }

        if (_display.Extensions.Contains("GLX_SGI_swap_control") && extra.HasSwapIntervalSGI)
        {
            if (extra.SwapIntervalSGI(value) == 0)
            {
                return;
            }
        }

        throw new GlutinException("failed to apply GLX swap interval.");
    }

    public void Resize(GlutinPossiblyCurrentContext context, uint width, uint height)
    {
        // GLX drawables follow their native size. Pbuffers are immutable.
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_raw == 0)
        {
            return;
        }

        switch (_kind)
        {
            case GlxSurfaceKind.Window:
                Ffi.glXDestroyWindow(_display.Raw, _raw);
                break;
            case GlxSurfaceKind.Pbuffer:
                Ffi.glXDestroyPbuffer(_display.Raw, _raw);
                break;
            case GlxSurfaceKind.Pixmap:
                Ffi.glXDestroyPixmap(_display.Raw, _raw);
                break;
        }

        _raw = 0;
    }

    private uint DrawableAttribute(int attribute)
    {
        Ffi.glXQueryDrawable(_display.Raw, _raw, attribute, out uint value);
        return value;
    }
}

internal enum GlxSurfaceKind
{
    Window,
    Pbuffer,
    Pixmap,
}
#endif
