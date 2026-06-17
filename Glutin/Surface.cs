using RawWindowHandles;

namespace Glutin;

public interface IGlSurface<TSurface> : IGetGlDisplay, IGetGlConfig, IAsRawSurface, IDisposable
    where TSurface : struct, ISurfaceType
{
    uint BufferAge { get; }

    uint? Width { get; }

    uint? Height { get; }

    bool IsSingleBuffered { get; }

    void SwapBuffers(PossiblyCurrentContext context);

    bool IsCurrent(PossiblyCurrentContext context);

    bool IsCurrentDraw(PossiblyCurrentContext context);

    bool IsCurrentRead(PossiblyCurrentContext context);

    void SetSwapInterval(PossiblyCurrentContext context, SwapInterval interval);

    void Resize(PossiblyCurrentContext context, uint width, uint height);
}

public interface IPlatformGlSurface<TSurface> : IGlSurface<TSurface>
    where TSurface : struct, ISurfaceType
{
}

public interface ISurfaceType
{
    SurfaceType SurfaceType { get; }
}

public interface IResizeableSurface : ISurfaceType
{
}

public readonly record struct WindowSurface : IResizeableSurface
{
    public SurfaceType SurfaceType => SurfaceType.Window;
}

public readonly record struct PbufferSurface : ISurfaceType
{
    public SurfaceType SurfaceType => SurfaceType.Pbuffer;
}

public readonly record struct PixmapSurface : ISurfaceType
{
    public SurfaceType SurfaceType => SurfaceType.Pixmap;
}

public enum SurfaceType
{
    Window,
    Pixmap,
    Pbuffer,
}

public interface IAsRawSurface
{
    RawSurface RawSurface { get; }
}

public sealed class SurfaceAttributesBuilder<TSurface>
    where TSurface : struct, ISurfaceType
{
    private bool? _srgb;
    private bool _singleBuffer;
    private bool _largestPbuffer;

    public static SurfaceAttributesBuilder<TSurface> New()
    {
        return new SurfaceAttributesBuilder<TSurface>();
    }

    public SurfaceAttributesBuilder<TSurface> WithSrgb(bool? srgb)
    {
        _srgb = srgb;
        return this;
    }

    public SurfaceAttributesBuilder<TSurface> WithSingleBuffer(bool singleBuffer)
    {
        _singleBuffer = singleBuffer;
        return this;
    }

    public SurfaceAttributesBuilder<TSurface> WithLargestPbuffer(bool largestPbuffer)
    {
        _largestPbuffer = largestPbuffer;
        return this;
    }

    public SurfaceAttributes<WindowSurface> BuildWindow(
        RawWindowHandle rawWindowHandle,
        uint width,
        uint height)
    {
        RequireNonZero(width, nameof(width));
        RequireNonZero(height, nameof(height));
        return new SurfaceAttributes<WindowSurface>
        {
            Srgb = _srgb,
            SingleBuffer = _singleBuffer,
            Width = width,
            Height = height,
            RawWindowHandle = rawWindowHandle,
        };
    }

    public SurfaceAttributes<PbufferSurface> BuildPbuffer(uint width, uint height)
    {
        RequireNonZero(width, nameof(width));
        RequireNonZero(height, nameof(height));
        return new SurfaceAttributes<PbufferSurface>
        {
            Srgb = _srgb,
            SingleBuffer = _singleBuffer,
            Width = width,
            Height = height,
            LargestPbuffer = _largestPbuffer,
        };
    }

    public SurfaceAttributes<PixmapSurface> BuildPixmap(NativePixmap nativePixmap)
    {
        return new SurfaceAttributes<PixmapSurface>
        {
            Srgb = _srgb,
            NativePixmap = nativePixmap,
        };
    }

    private static void RequireNonZero(uint value, string parameterName)
    {
        if (value == 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Value must be non-zero.");
        }
    }
}

public sealed record SurfaceAttributes<TSurface>
    where TSurface : struct, ISurfaceType
{
    public bool? Srgb { get; init; }

    public bool SingleBuffer { get; init; }

    public uint? Width { get; init; }

    public uint? Height { get; init; }

    public bool LargestPbuffer { get; init; }

    public RawWindowHandle? RawWindowHandle { get; init; }

    public NativePixmap? NativePixmap { get; init; }
}

public sealed class Surface<TSurface> : IGlSurface<TSurface>
    where TSurface : struct, ISurfaceType
{
    private readonly IPlatformGlSurface<TSurface> _backend;

    internal Surface(IPlatformGlSurface<TSurface> backend)
    {
        _backend = backend;
    }

    internal IPlatformGlSurface<TSurface> Backend => _backend;

    public uint BufferAge => _backend.BufferAge;

    public uint? Width => _backend.Width;

    public uint? Height => _backend.Height;

    public bool IsSingleBuffered => _backend.IsSingleBuffered;

    public Display Display => _backend.Display;

    public Config Config => _backend.Config;

    public RawSurface RawSurface => _backend.RawSurface;

    public void SwapBuffers(PossiblyCurrentContext context)
    {
        _backend.SwapBuffers(context);
    }

    public bool IsCurrent(PossiblyCurrentContext context)
    {
        return _backend.IsCurrent(context);
    }

    public bool IsCurrentDraw(PossiblyCurrentContext context)
    {
        return _backend.IsCurrentDraw(context);
    }

    public bool IsCurrentRead(PossiblyCurrentContext context)
    {
        return _backend.IsCurrentRead(context);
    }

    public void SetSwapInterval(PossiblyCurrentContext context, SwapInterval interval)
    {
        _backend.SetSwapInterval(context, interval);
    }

    public void Resize(PossiblyCurrentContext context, uint width, uint height)
    {
        if (width == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Value must be non-zero.");
        }

        if (height == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Value must be non-zero.");
        }

        _backend.Resize(context, width, height);
    }

    public void Dispose()
    {
        _backend.Dispose();
    }
}

public readonly record struct SwapInterval
{
    private const byte DontWaitTag = 0;
    private const byte WaitTag = 1;

    private readonly byte _tag;
    private readonly uint _interval;

    private SwapInterval(byte tag, uint interval)
    {
        _tag = tag;
        _interval = interval;
    }

    public static SwapInterval DontWait => new(DontWaitTag, 0);

    public static SwapInterval Wait(uint interval)
    {
        if (interval == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Swap interval must be non-zero.");
        }

        return new SwapInterval(WaitTag, interval);
    }

    public bool TryGetWait(out uint interval)
    {
        interval = _interval;
        return _tag == WaitTag;
    }
}

public record struct NativePixmap
{
    public readonly record struct XlibPixmap(nuint Pixmap);

    public readonly record struct XcbPixmap(uint Pixmap);

    public readonly record struct WindowsPixmap(nint Bitmap);

    private const byte XlibPixmapTag = 0;
    private const byte XcbPixmapTag = 1;
    private const byte WindowsPixmapTag = 2;

    private byte _tag;
    private XlibPixmap _xlibPixmap;
    private XcbPixmap _xcbPixmap;
    private WindowsPixmap _windowsPixmap;

    public NativePixmap(XlibPixmap value)
    {
        this = default;
        _tag = XlibPixmapTag;
        _xlibPixmap = value;
    }

    public NativePixmap(XcbPixmap value)
    {
        this = default;
        _tag = XcbPixmapTag;
        _xcbPixmap = value;
    }

    public NativePixmap(WindowsPixmap value)
    {
        this = default;
        _tag = WindowsPixmapTag;
        _windowsPixmap = value;
    }

    public bool TryGetValue(out XlibPixmap value)
    {
        value = _xlibPixmap;
        return _tag == XlibPixmapTag;
    }

    public bool TryGetValue(out XcbPixmap value)
    {
        value = _xcbPixmap;
        return _tag == XcbPixmapTag;
    }

    public bool TryGetValue(out WindowsPixmap value)
    {
        value = _windowsPixmap;
        return _tag == WindowsPixmapTag;
    }
}

public record struct RawSurface
{
    public readonly record struct Egl(nint Surface);

    public readonly record struct Glx(ulong Drawable);

    public readonly record struct Wgl(nint Surface);

    public readonly record struct Cgl(nint View);

    private const byte EglTag = 0;
    private const byte GlxTag = 1;
    private const byte WglTag = 2;
    private const byte CglTag = 3;

    private byte _tag;
    private Egl _egl;
    private Glx _glx;
    private Wgl _wgl;
    private Cgl _cgl;

    public RawSurface(Egl value)
    {
        this = default;
        _tag = EglTag;
        _egl = value;
    }

    public RawSurface(Glx value)
    {
        this = default;
        _tag = GlxTag;
        _glx = value;
    }

    public RawSurface(Wgl value)
    {
        this = default;
        _tag = WglTag;
        _wgl = value;
    }

    public RawSurface(Cgl value)
    {
        this = default;
        _tag = CglTag;
        _cgl = value;
    }

    public bool TryGetValue(out Egl value)
    {
        value = _egl;
        return _tag == EglTag;
    }

    public bool TryGetValue(out Glx value)
    {
        value = _glx;
        return _tag == GlxTag;
    }

    public bool TryGetValue(out Wgl value)
    {
        value = _wgl;
        return _tag == WglTag;
    }

    public bool TryGetValue(out Cgl value)
    {
        value = _cgl;
        return _tag == CglTag;
    }
}

public readonly record struct Rect(int X, int Y, int Width, int Height);
