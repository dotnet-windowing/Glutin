using RawWindowHandles;

namespace Glutin;

public interface IGlDisplay
{
    IEnumerable<Config> FindConfigs(ConfigTemplate template);

    NotCurrentContext CreateContext(Config config, ContextAttributes contextAttributes);

    Surface<WindowSurface> CreateWindowSurface(
        Config config,
        SurfaceAttributes<WindowSurface> surfaceAttributes);

    Surface<PbufferSurface> CreatePbufferSurface(
        Config config,
        SurfaceAttributes<PbufferSurface> surfaceAttributes);

    Surface<PixmapSurface> CreatePixmapSurface(
        Config config,
        SurfaceAttributes<PixmapSurface> surfaceAttributes);

    nint GetProcAddress(string symbol);

    string VersionString { get; }

    DisplayFeatures SupportedFeatures { get; }
}

public interface IGetGlDisplay
{
    Display Display { get; }
}

public interface IGetDisplayExtensions
{
    IReadOnlySet<string> Extensions { get; }
}

public interface IAsRawDisplay
{
    RawDisplay RawDisplay { get; }
}

public interface IPlatformGlDisplay : IGlDisplay, IGetDisplayExtensions, IAsRawDisplay, IDisposable
{
}

public sealed class Display : IGlDisplay, IGetDisplayExtensions, IAsRawDisplay, IDisposable
{
    private readonly IPlatformGlDisplay _backend;

    internal Display(IPlatformGlDisplay backend)
    {
        _backend = backend;
    }

    internal IPlatformGlDisplay Backend => _backend;

    public static Display New(RawDisplayHandle display, DisplayApiPreference preference)
    {
#if WINDOWS
        if (preference.TryGetValue(out DisplayApiPreference.Wgl wgl))
        {
            return global::Glutin.Backend.Wgl.Display.New(display, wgl.WindowHandle);
        }

        if (preference.TryGetValue(out DisplayApiPreference.WglThenEgl wglThenEgl))
        {
            return global::Glutin.Backend.Wgl.Display.New(display, wglThenEgl.WindowHandle);
        }
#endif

        throw new GlutinException(
            $"No OpenGL display backend is implemented yet for {preference.ApiName}.");
    }

    public IEnumerable<Config> FindConfigs(ConfigTemplate template)
    {
        return _backend.FindConfigs(template);
    }

    public NotCurrentContext CreateContext(Config config, ContextAttributes contextAttributes)
    {
        return _backend.CreateContext(config, contextAttributes);
    }

    public Surface<WindowSurface> CreateWindowSurface(
        Config config,
        SurfaceAttributes<WindowSurface> surfaceAttributes)
    {
        return _backend.CreateWindowSurface(config, surfaceAttributes);
    }

    public Surface<PbufferSurface> CreatePbufferSurface(
        Config config,
        SurfaceAttributes<PbufferSurface> surfaceAttributes)
    {
        return _backend.CreatePbufferSurface(config, surfaceAttributes);
    }

    public Surface<PixmapSurface> CreatePixmapSurface(
        Config config,
        SurfaceAttributes<PixmapSurface> surfaceAttributes)
    {
        return _backend.CreatePixmapSurface(config, surfaceAttributes);
    }

    public nint GetProcAddress(string symbol)
    {
        return _backend.GetProcAddress(symbol);
    }

    public string VersionString => _backend.VersionString;

    public DisplayFeatures SupportedFeatures => _backend.SupportedFeatures;

    public IReadOnlySet<string> Extensions => _backend.Extensions;

    public RawDisplay RawDisplay => _backend.RawDisplay;

    public void Dispose()
    {
        _backend.Dispose();
    }
}

[Flags]
public enum DisplayFeatures : uint
{
    None = 0,
    MultisamplingPixelFormats = 1 << 0,
    FloatPixelFormat = 1 << 1,
    SrgbFramebuffers = 1 << 2,
    CreateEsContext = 1 << 3,
    SwapControl = 1 << 4,
    ContextRobustness = 1 << 5,
    ContextReleaseBehavior = 1 << 6,
    ContextNoError = 1 << 7,
}

public record struct DisplayApiPreference
{
    public readonly record struct Egl;

    public readonly record struct Glx;

    public readonly record struct Wgl(RawWindowHandle? WindowHandle);

    public readonly record struct Cgl;

    public readonly record struct EglThenGlx;

    public readonly record struct GlxThenEgl;

    public readonly record struct EglThenWgl(RawWindowHandle? WindowHandle);

    public readonly record struct WglThenEgl(RawWindowHandle? WindowHandle);

    private const byte EglTag = 0;
    private const byte GlxTag = 1;
    private const byte WglTag = 2;
    private const byte CglTag = 3;
    private const byte EglThenGlxTag = 4;
    private const byte GlxThenEglTag = 5;
    private const byte EglThenWglTag = 6;
    private const byte WglThenEglTag = 7;

    private byte _tag;
    private Egl _egl;
    private Glx _glx;
    private Wgl _wgl;
    private Cgl _cgl;
    private EglThenGlx _eglThenGlx;
    private GlxThenEgl _glxThenEgl;
    private EglThenWgl _eglThenWgl;
    private WglThenEgl _wglThenEgl;

    public DisplayApiPreference(Egl value)
    {
        this = default;
        _tag = EglTag;
        _egl = value;
    }

    public DisplayApiPreference(Glx value)
    {
        this = default;
        _tag = GlxTag;
        _glx = value;
    }

    public DisplayApiPreference(Wgl value)
    {
        this = default;
        _tag = WglTag;
        _wgl = value;
    }

    public DisplayApiPreference(Cgl value)
    {
        this = default;
        _tag = CglTag;
        _cgl = value;
    }

    public DisplayApiPreference(EglThenGlx value)
    {
        this = default;
        _tag = EglThenGlxTag;
        _eglThenGlx = value;
    }

    public DisplayApiPreference(GlxThenEgl value)
    {
        this = default;
        _tag = GlxThenEglTag;
        _glxThenEgl = value;
    }

    public DisplayApiPreference(EglThenWgl value)
    {
        this = default;
        _tag = EglThenWglTag;
        _eglThenWgl = value;
    }

    public DisplayApiPreference(WglThenEgl value)
    {
        this = default;
        _tag = WglThenEglTag;
        _wglThenEgl = value;
    }

    public string ApiName => _tag switch
    {
        EglTag => nameof(Egl),
        GlxTag => nameof(Glx),
        WglTag => nameof(Wgl),
        CglTag => nameof(Cgl),
        EglThenGlxTag => nameof(EglThenGlx),
        GlxThenEglTag => nameof(GlxThenEgl),
        EglThenWglTag => nameof(EglThenWgl),
        WglThenEglTag => nameof(WglThenEgl),
        _ => "Unknown",
    };

    public static DisplayApiPreference UseEgl()
    {
        return new DisplayApiPreference(new Egl());
    }

    public static DisplayApiPreference UseGlx()
    {
        return new DisplayApiPreference(new Glx());
    }

    public static DisplayApiPreference UseWgl(RawWindowHandle? windowHandle = null)
    {
        return new DisplayApiPreference(new Wgl(windowHandle));
    }

    public static DisplayApiPreference UseCgl()
    {
        return new DisplayApiPreference(new Cgl());
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

    public bool TryGetValue(out EglThenGlx value)
    {
        value = _eglThenGlx;
        return _tag == EglThenGlxTag;
    }

    public bool TryGetValue(out GlxThenEgl value)
    {
        value = _glxThenEgl;
        return _tag == GlxThenEglTag;
    }

    public bool TryGetValue(out EglThenWgl value)
    {
        value = _eglThenWgl;
        return _tag == EglThenWglTag;
    }

    public bool TryGetValue(out WglThenEgl value)
    {
        value = _wglThenEgl;
        return _tag == WglThenEglTag;
    }
}

public record struct RawDisplay
{
    public readonly record struct Egl(nint Display);

    public readonly record struct Glx(nint Display);

    public readonly record struct Wgl;

    public readonly record struct Cgl(nint Display);

    private const byte EglTag = 0;
    private const byte GlxTag = 1;
    private const byte WglTag = 2;
    private const byte CglTag = 3;

    private byte _tag;
    private Egl _egl;
    private Glx _glx;
    private Wgl _wgl;
    private Cgl _cgl;

    public RawDisplay(Egl value)
    {
        this = default;
        _tag = EglTag;
        _egl = value;
    }

    public RawDisplay(Glx value)
    {
        this = default;
        _tag = GlxTag;
        _glx = value;
    }

    public RawDisplay(Wgl value)
    {
        this = default;
        _tag = WglTag;
        _wgl = value;
    }

    public RawDisplay(Cgl value)
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

public sealed class GlutinException : Exception
{
    public GlutinException(string message)
        : base(message)
    {
    }

    public GlutinException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
