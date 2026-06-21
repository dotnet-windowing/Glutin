using System.Runtime.InteropServices;
using System.Text;
using RawWindowHandles;
using GlutinConfig = Glutin.Config;
using GlutinDisplay = Glutin.Display;
using GlutinNotCurrentContext = Glutin.NotCurrentContext;
using GlutinSurfacePbuffer = Glutin.Surface<Glutin.PbufferSurface>;
using GlutinSurfacePixmap = Glutin.Surface<Glutin.PixmapSurface>;
using GlutinSurfaceWindow = Glutin.Surface<Glutin.WindowSurface>;

namespace Glutin.Backend.Egl;

public sealed unsafe class Display : IPlatformGlDisplay
{
    private readonly nint _raw;
    private readonly EglDisplayKind _kind;
    private readonly GlVersion _version;
    private readonly EglExtensions _eglExtra;
    private readonly HashSet<string> _clientExtensions;
    private readonly HashSet<string> _extensions;
    private readonly DisplayFeatures _features;
    private readonly RawDisplayHandle _nativeDisplay;
    private readonly GlutinDisplay _facade;
    private bool _disposed;

    private Display(
        nint raw,
        EglDisplayKind kind,
        GlVersion version,
        EglExtensions eglExtra,
        HashSet<string> clientExtensions,
        HashSet<string> extensions,
        RawDisplayHandle nativeDisplay)
    {
        _raw = raw;
        _kind = kind;
        _version = version;
        _eglExtra = eglExtra;
        _clientExtensions = clientExtensions;
        _extensions = extensions;
        _features = ExtractDisplayFeatures(extensions, version);
        _nativeDisplay = nativeDisplay;
        _facade = new GlutinDisplay(this);
    }

    internal nint Raw => _raw;

    internal EglDisplayKind Kind => _kind;

    internal GlVersion Version => _version;

    internal EglExtensions EglExtra => _eglExtra;

    internal GlutinDisplay Facade => _facade;

    internal DisplayFeatures Features => _features;

    internal RawDisplayHandle NativeDisplay => _nativeDisplay;

    public static GlutinDisplay New(RawDisplayHandle rawDisplay)
    {
        var extra = EglExtensions.Load();
        HashSet<string> clientExtensions = Ffi.LoadExtensionSet(0);

        (nint raw, EglDisplayKind kind) = CreateDisplay(rawDisplay, extra, clientExtensions);
        if (raw == 0)
        {
            throw Ffi.LastError("eglGetDisplay");
        }

        if (Ffi.eglInitialize(raw, out int major, out int minor) == EglConstants.False)
        {
            throw Ffi.LastError("eglInitialize");
        }

        var version = new GlVersion(checked((byte)major), checked((byte)minor));
        HashSet<string> displayExtensions = Ffi.LoadExtensionSet(raw);
        return new Display(raw, kind, version, extra, clientExtensions, displayExtensions, rawDisplay).Facade;
    }

    public IEnumerable<GlutinConfig> FindConfigs(ConfigTemplate template)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Config.FindConfigs(this, template).Select(config => new GlutinConfig(config));
    }

    public GlutinNotCurrentContext CreateContext(GlutinConfig config, ContextAttributes contextAttributes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (config.Backend is not Config eglConfig)
        {
            throw new GlutinException("EGL display received a config from another backend.");
        }

        return new GlutinNotCurrentContext(new NotCurrentContext(ContextInner.Create(this, eglConfig, contextAttributes)));
    }

    public GlutinSurfaceWindow CreateWindowSurface(
        GlutinConfig config,
        SurfaceAttributes<WindowSurface> surfaceAttributes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (config.Backend is not Config eglConfig)
        {
            throw new GlutinException("EGL display received a config from another backend.");
        }

        return new GlutinSurfaceWindow(Surface<WindowSurface>.CreateWindow(this, eglConfig, surfaceAttributes));
    }

    public GlutinSurfacePbuffer CreatePbufferSurface(
        GlutinConfig config,
        SurfaceAttributes<PbufferSurface> surfaceAttributes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (config.Backend is not Config eglConfig)
        {
            throw new GlutinException("EGL display received a config from another backend.");
        }

        return new GlutinSurfacePbuffer(Surface<PbufferSurface>.CreatePbuffer(this, eglConfig, surfaceAttributes));
    }

    public GlutinSurfacePixmap CreatePixmapSurface(
        GlutinConfig config,
        SurfaceAttributes<PixmapSurface> surfaceAttributes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (config.Backend is not Config eglConfig)
        {
            throw new GlutinException("EGL display received a config from another backend.");
        }

        return new GlutinSurfacePixmap(Surface<PixmapSurface>.CreatePixmap(this, eglConfig, surfaceAttributes));
    }

    public nint GetProcAddress(string symbol)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        byte[] bytes = Encoding.ASCII.GetBytes(symbol + '\0');
        fixed (byte* ptr = bytes)
        {
            return Ffi.eglGetProcAddress(ptr);
        }
    }

    public string VersionString => $"EGL {_version.Major}.{_version.Minor}";

    public DisplayFeatures SupportedFeatures => _features;

    public IReadOnlySet<string> Extensions => _extensions;

    public RawDisplay RawDisplay => new(new RawDisplay.Egl(_raw));

    public void Dispose()
    {
        _disposed = true;
    }

    private static (nint Raw, EglDisplayKind Kind) CreateDisplay(
        RawDisplayHandle rawDisplay,
        EglExtensions extra,
        HashSet<string> clientExtensions)
    {
        if (TryGetPlatformDisplay(rawDisplay, extra, clientExtensions, out nint platformDisplay, out EglDisplayKind kind))
        {
            return (platformDisplay, kind);
        }

        nint nativeDisplay = GetLegacyNativeDisplay(rawDisplay);
        return (Ffi.eglGetDisplay(nativeDisplay), EglDisplayKind.Legacy);
    }

    private static bool TryGetPlatformDisplay(
        RawDisplayHandle rawDisplay,
        EglExtensions extra,
        HashSet<string> clientExtensions,
        out nint display,
        out EglDisplayKind kind)
    {
        display = 0;
        kind = EglDisplayKind.Legacy;

        if (extra.HasGetPlatformDisplay
            && TryGetKhrPlatform(rawDisplay, clientExtensions, out uint platform, out nint nativeDisplay, out nint[] khrAttrs))
        {
            fixed (nint* attrs = khrAttrs)
            {
                display = extra.GetPlatformDisplay(platform, nativeDisplay, attrs);
            }

            if (display != 0)
            {
                kind = EglDisplayKind.Khr;
                return true;
            }
        }

        if (extra.HasGetPlatformDisplayEXT
            && TryGetExtPlatform(rawDisplay, clientExtensions, out uint extPlatform, out nint extNativeDisplay, out int[] extAttrs))
        {
            fixed (int* attrs = extAttrs)
            {
                display = extra.GetPlatformDisplayEXT(extPlatform, extNativeDisplay, attrs);
            }

            if (display != 0)
            {
                kind = EglDisplayKind.Ext;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetKhrPlatform(
        RawDisplayHandle rawDisplay,
        HashSet<string> clientExtensions,
        out uint platform,
        out nint nativeDisplay,
        out nint[] attrs)
    {
        attrs = [EglConstants.None];
        platform = 0;
        nativeDisplay = 0;

        if (rawDisplay.TryGetValue(out RawDisplayHandle.Android _)
            && clientExtensions.Contains("EGL_KHR_platform_android"))
        {
            platform = EglConstants.PlatformAndroidKhr;
            nativeDisplay = 0;
            return true;
        }

        if (rawDisplay.TryGetValue(out RawDisplayHandle.Wayland wayland)
            && wayland.Display != 0
            && clientExtensions.Contains("EGL_KHR_platform_wayland"))
        {
            platform = EglConstants.PlatformWaylandKhr;
            nativeDisplay = wayland.Display;
            return true;
        }

        if (rawDisplay.TryGetValue(out RawDisplayHandle.Xlib xlib)
            && clientExtensions.Contains("EGL_KHR_platform_x11"))
        {
            platform = EglConstants.PlatformX11Khr;
            nativeDisplay = xlib.Display ?? 0;
            attrs = [EglConstants.PlatformX11ScreenKhr, xlib.Screen, EglConstants.None];
            return true;
        }

        return false;
    }

    private static bool TryGetExtPlatform(
        RawDisplayHandle rawDisplay,
        HashSet<string> clientExtensions,
        out uint platform,
        out nint nativeDisplay,
        out int[] attrs)
    {
        attrs = [EglConstants.None];
        platform = 0;
        nativeDisplay = 0;

        if (rawDisplay.TryGetValue(out RawDisplayHandle.Wayland wayland)
            && wayland.Display != 0
            && clientExtensions.Contains("EGL_EXT_platform_wayland"))
        {
            platform = EglConstants.PlatformWaylandExt;
            nativeDisplay = wayland.Display;
            return true;
        }

        if (rawDisplay.TryGetValue(out RawDisplayHandle.Xlib xlib)
            && clientExtensions.Contains("EGL_EXT_platform_x11"))
        {
            platform = EglConstants.PlatformX11Ext;
            nativeDisplay = xlib.Display ?? 0;
            attrs = [EglConstants.PlatformX11ScreenExt, xlib.Screen, EglConstants.None];
            return true;
        }

        return false;
    }

    private static nint GetLegacyNativeDisplay(RawDisplayHandle rawDisplay)
    {
        if (rawDisplay.TryGetValue(out RawDisplayHandle.Android _))
        {
            return 0;
        }

        if (rawDisplay.TryGetValue(out RawDisplayHandle.Xlib xlib))
        {
            return xlib.Display ?? 0;
        }

        if (rawDisplay.TryGetValue(out RawDisplayHandle.Wayland wayland) && wayland.Display != 0)
        {
            return wayland.Display;
        }

        throw new GlutinException("provided native display is not supported by EGL");
    }

    private static DisplayFeatures ExtractDisplayFeatures(HashSet<string> extensions, GlVersion version)
    {
        DisplayFeatures features =
            DisplayFeatures.CreateEsContext
            | DisplayFeatures.MultisamplingPixelFormats
            | DisplayFeatures.SwapControl;

        if (extensions.Contains("EGL_EXT_pixel_format_float"))
        {
            features |= DisplayFeatures.FloatPixelFormat;
        }

        if (extensions.Contains("EGL_KHR_gl_colorspace"))
        {
            features |= DisplayFeatures.SrgbFramebuffers;
        }

        if (version.CompareTo(new GlVersion(1, 5)) >= 0
            || extensions.Contains("EGL_EXT_create_context_robustness"))
        {
            features |= DisplayFeatures.ContextRobustness;
        }

        if (extensions.Contains("EGL_KHR_create_context_no_error"))
        {
            features |= DisplayFeatures.ContextNoError;
        }

        return features;
    }
}

internal enum EglDisplayKind
{
    Khr,
    Ext,
    Legacy,
}
