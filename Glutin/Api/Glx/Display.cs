#if !ANDROID
using System.Runtime.InteropServices;
using System.Text;
using RawWindowHandles;
using GlutinConfig = Glutin.Config;
using GlutinDisplay = Glutin.Display;
using GlutinNotCurrentContext = Glutin.NotCurrentContext;
using GlutinSurfacePbuffer = Glutin.Surface<Glutin.PbufferSurface>;
using GlutinSurfacePixmap = Glutin.Surface<Glutin.PixmapSurface>;
using GlutinSurfaceWindow = Glutin.Surface<Glutin.WindowSurface>;

namespace Glutin.Backend.Glx;

public sealed unsafe class Display : IPlatformGlDisplay
{
    private readonly nint _raw;
    private readonly int _screen;
    private readonly GlVersion _version;
    private readonly GlxExtensions _glxExtra;
    private readonly HashSet<string> _extensions;
    private readonly DisplayFeatures _features;
    private readonly GlutinDisplay _facade;
    private bool _disposed;

    private Display(nint raw, int screen, GlVersion version, GlxExtensions glxExtra, HashSet<string> extensions)
    {
        _raw = raw;
        _screen = screen;
        _version = version;
        _glxExtra = glxExtra;
        _extensions = extensions;
        _features = ExtractDisplayFeatures(extensions, version);
        _facade = new GlutinDisplay(this);
    }

    internal nint Raw => _raw;

    internal int Screen => _screen;

    internal GlVersion Version => _version;

    internal GlxExtensions GlxExtra => _glxExtra;

    internal GlutinDisplay Facade => _facade;

    internal DisplayFeatures Features => _features;

    public static GlutinDisplay New(RawDisplayHandle rawDisplay)
    {
        if (!rawDisplay.TryGetValue(out RawDisplayHandle.Xlib xlib) || xlib.Display is not { } display || display == 0)
        {
            throw new GlutinException("provided native display is not supported by GLX");
        }

        if (Ffi.glXQueryExtension(display, out _, out _) == 0)
        {
            throw new GlutinException("GLX extension is not available on the X11 display.");
        }

        if (Ffi.glXQueryVersion(display, out int major, out int minor) == 0)
        {
            throw new GlutinException("glXQueryVersion failed.");
        }

        var version = new GlVersion(checked((byte)major), checked((byte)minor));
        if (version.CompareTo(new GlVersion(1, 3)) < 0)
        {
            throw new GlutinException("GLX versions below 1.3 are not supported.");
        }

        GlxExtensions glxExtra = GlxExtensions.Load();
        HashSet<string> extensions = LoadExtensionSet(display);
        return new Display(display, xlib.Screen, version, glxExtra, extensions).Facade;
    }

    public IEnumerable<GlutinConfig> FindConfigs(ConfigTemplate template)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Config.FindConfigs(this, template).Select(config => new GlutinConfig(config));
    }

    public GlutinNotCurrentContext CreateContext(GlutinConfig config, ContextAttributes contextAttributes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (config.Backend is not Config glxConfig)
        {
            throw new GlutinException("GLX display received a config from another backend.");
        }

        return new GlutinNotCurrentContext(new NotCurrentContext(ContextInner.Create(this, glxConfig, contextAttributes)));
    }

    public GlutinSurfaceWindow CreateWindowSurface(
        GlutinConfig config,
        SurfaceAttributes<WindowSurface> surfaceAttributes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (config.Backend is not Config glxConfig)
        {
            throw new GlutinException("GLX display received a config from another backend.");
        }

        return new GlutinSurfaceWindow(Surface<WindowSurface>.CreateWindow(this, glxConfig, surfaceAttributes));
    }

    public GlutinSurfacePbuffer CreatePbufferSurface(
        GlutinConfig config,
        SurfaceAttributes<PbufferSurface> surfaceAttributes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (config.Backend is not Config glxConfig)
        {
            throw new GlutinException("GLX display received a config from another backend.");
        }

        return new GlutinSurfacePbuffer(Surface<PbufferSurface>.CreatePbuffer(this, glxConfig, surfaceAttributes));
    }

    public GlutinSurfacePixmap CreatePixmapSurface(
        GlutinConfig config,
        SurfaceAttributes<PixmapSurface> surfaceAttributes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (config.Backend is not Config glxConfig)
        {
            throw new GlutinException("GLX display received a config from another backend.");
        }

        return new GlutinSurfacePixmap(Surface<PixmapSurface>.CreatePixmap(this, glxConfig, surfaceAttributes));
    }

    public nint GetProcAddress(string symbol)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        byte[] bytes = Encoding.ASCII.GetBytes(symbol + '\0');
        fixed (byte* ptr = bytes)
        {
            return Ffi.glXGetProcAddress(ptr);
        }
    }

    public string VersionString => $"GLX {_version.Major}.{_version.Minor}";

    public DisplayFeatures SupportedFeatures => _features;

    public IReadOnlySet<string> Extensions => _extensions;

    public RawDisplay RawDisplay => new(new RawDisplay.Glx(_raw));

    public void Dispose()
    {
        _disposed = true;
    }

    private static DisplayFeatures ExtractDisplayFeatures(HashSet<string> extensions, GlVersion version)
    {
        DisplayFeatures features = DisplayFeatures.None;

        if (version.CompareTo(new GlVersion(1, 4)) >= 0 || extensions.Contains("GLX_ARB_multisample"))
        {
            features |= DisplayFeatures.MultisamplingPixelFormats;
        }

        if (extensions.Contains("GLX_ARB_fbconfig_float"))
        {
            features |= DisplayFeatures.FloatPixelFormat;
        }

        if (extensions.Contains("GLX_ARB_framebuffer_sRGB") || extensions.Contains("GLX_EXT_framebuffer_sRGB"))
        {
            features |= DisplayFeatures.SrgbFramebuffers;
        }

        if (extensions.Contains("GLX_EXT_create_context_es2_profile")
            || extensions.Contains("GLX_EXT_create_context_es_profile"))
        {
            features |= DisplayFeatures.CreateEsContext;
        }

        if (extensions.Contains("GLX_EXT_swap_control")
            || extensions.Contains("GLX_SGI_swap_control")
            || extensions.Contains("GLX_MESA_swap_control"))
        {
            features |= DisplayFeatures.SwapControl;
        }

        if (extensions.Contains("GLX_ARB_create_context_robustness"))
        {
            features |= DisplayFeatures.ContextRobustness;
        }

        if (extensions.Contains("GLX_ARB_context_flush_control"))
        {
            features |= DisplayFeatures.ContextReleaseBehavior;
        }

        if (extensions.Contains("GLX_ARB_create_context_no_error"))
        {
            features |= DisplayFeatures.ContextNoError;
        }

        return features;
    }

    private static HashSet<string> LoadExtensionSet(nint display)
    {
        sbyte* extensions = Ffi.glXGetClientString(display, GlxConstants.Extensions);
        if (extensions is null)
        {
            return [];
        }

        string? extensionString = Marshal.PtrToStringAnsi((nint)extensions);
        return extensionString is { Length: > 0 }
            ? extensionString.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal)
            : [];
    }
}
#endif
