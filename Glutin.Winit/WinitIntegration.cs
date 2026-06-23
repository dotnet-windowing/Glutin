using Glutin;
using RawWindowHandles;
using Winit.Core;
#if !ANDROID
using Glutin.Platform.X11;
using Winit.Platform.X11;
#endif

namespace Glutin.Winit;

public enum ApiPreference
{
    FallbackEgl = 0,
    PreferEgl = 1,
}

public readonly record struct WindowSurfaceTarget(
    RawDisplayHandle? DisplayHandle,
    RawWindowHandle WindowHandle,
    SurfaceAttributes<WindowSurface> Attributes);

public sealed class DisplayBuilder
{
    private ApiPreference _preference = ApiPreference.FallbackEgl;
    private WindowAttributes? _windowAttributes;

    public static DisplayBuilder New()
    {
        return new DisplayBuilder();
    }

    public DisplayBuilder WithPreference(ApiPreference preference)
    {
        _preference = preference;
        return this;
    }

    public DisplayBuilder WithWindowAttributes(WindowAttributes? windowAttributes)
    {
        _windowAttributes = windowAttributes?.Clone();
        return this;
    }

    public (IWindow? Window, Config Config) Build(
        IActiveEventLoop eventLoop,
        ConfigTemplateBuilder templateBuilder)
    {
        return Build(eventLoop, templateBuilder, PickDefaultConfig);
    }

    public (IWindow? Window, Config Config) Build(
        IActiveEventLoop eventLoop,
        ConfigTemplateBuilder templateBuilder,
        Func<IEnumerable<Config>, Config> configPicker)
    {
        ArgumentNullException.ThrowIfNull(eventLoop);
        ArgumentNullException.ThrowIfNull(templateBuilder);
        ArgumentNullException.ThrowIfNull(configPicker);

        WindowAttributes? windowAttributes = _windowAttributes?.Clone();
        IWindow? window = null;
        RawWindowHandle? rawWindowHandle = null;

        if (OperatingSystem.IsWindows() && windowAttributes is not null)
        {
            window = eventLoop.CreateWindow(windowAttributes);
            rawWindowHandle = window.WindowHandle
                ?? throw new GlutinException("The Winit window does not expose a raw window handle.");
        }

        Display display = CreateDisplay(eventLoop, _preference, rawWindowHandle);

        ConfigTemplate template = templateBuilder.Build();
        if (rawWindowHandle is { } nativeWindow)
        {
            template = template with { NativeWindow = nativeWindow };
        }

        Config[] configs = display.FindConfigs(template).ToArray();
        if (configs.Length == 0)
        {
            throw new GlutinException("OpenGL display did not return any matching configs.");
        }

        Config config = configPicker(configs)
            ?? throw new GlutinException("OpenGL config picker returned null.");

        if (window is null && windowAttributes is not null)
        {
            window = GlutinWinit.FinalizeWindow(eventLoop, windowAttributes, config);
        }

        return (window, config);
    }

    private static Config PickDefaultConfig(IEnumerable<Config> configs)
    {
        return configs
            .OrderByDescending(config => config.HardwareAccelerated)
            .ThenByDescending(config => config.NumSamples)
            .ThenByDescending(config => config.SrgbCapable)
            .ThenByDescending(config => config.DepthSize)
            .ThenByDescending(config => config.StencilSize)
            .ThenByDescending(config => config.AlphaSize)
            .FirstOrDefault()
            ?? throw new GlutinException("OpenGL display did not return any matching configs.");
    }

    private static Display CreateDisplay(
        IActiveEventLoop eventLoop,
        ApiPreference apiPreference,
        RawWindowHandle? rawWindowHandle)
    {
        RawDisplayHandle displayHandle = eventLoop.DisplayHandle ?? FallbackDisplayHandle();
        DisplayApiPreference displayPreference = SelectDisplayApiPreference(
            displayHandle,
            apiPreference,
            rawWindowHandle);
        return Display.New(displayHandle, displayPreference);
    }

    private static RawDisplayHandle FallbackDisplayHandle()
    {
        if (OperatingSystem.IsWindows())
        {
            return RawDisplayHandle.FromWindows();
        }

        if (OperatingSystem.IsAndroid())
        {
            return RawDisplayHandle.FromAndroid();
        }

        throw new GlutinException("The Winit event loop does not expose a raw display handle.");
    }

    private static DisplayApiPreference SelectDisplayApiPreference(
        RawDisplayHandle displayHandle,
        ApiPreference apiPreference,
        RawWindowHandle? rawWindowHandle)
    {
#if ANDROID
        return DisplayApiPreference.UseEgl();
#else
        if (displayHandle.TryGetValue(out RawDisplayHandle.Android _))
        {
            return DisplayApiPreference.UseEgl();
        }

        if (displayHandle.TryGetValue(out RawDisplayHandle.Windows _) || OperatingSystem.IsWindows())
        {
            return apiPreference == ApiPreference.PreferEgl
                ? DisplayApiPreference.PreferEglThenWgl(rawWindowHandle)
                : DisplayApiPreference.PreferWglThenEgl(rawWindowHandle);
        }

        if (displayHandle.TryGetValue(out RawDisplayHandle.Wayland _))
        {
            return DisplayApiPreference.UseEgl();
        }

        if (displayHandle.TryGetValue(out RawDisplayHandle.Xlib _))
        {
            return apiPreference == ApiPreference.PreferEgl
                ? DisplayApiPreference.PreferEglThenGlx()
                : DisplayApiPreference.PreferGlxThenEgl();
        }

        return DisplayApiPreference.UseEgl();
#endif
    }
}

public static class GlutinWinit
{
    public static IWindow FinalizeWindow(
        IActiveEventLoop eventLoop,
        WindowAttributes windowAttributes,
        Config config)
    {
        ArgumentNullException.ThrowIfNull(eventLoop);
        ArgumentNullException.ThrowIfNull(windowAttributes);
        ArgumentNullException.ThrowIfNull(config);

        WindowAttributes attributes = windowAttributes.Clone();

        if (config.SupportsTransparency == false)
        {
            attributes.Transparent = false;
        }

#if !ANDROID
        if (config.GetX11VisualId() is { } visualId)
        {
            attributes = attributes.WithX11Visual(new global::Winit.X11.XVisualId(visualId));
        }
#endif

        return eventLoop.CreateWindow(attributes);
    }
}

public static class WinitWindowExtensions
{
    public static WindowSurfaceTarget BuildSurfaceTarget(this IWindow window)
    {
        RawWindowHandle windowHandle = window.WindowHandle
            ?? throw new GlutinException("The Winit window does not expose a raw window handle.");

        return new WindowSurfaceTarget(
            window.DisplayHandle,
            windowHandle,
            window.BuildSurfaceAttributes());
    }

    public static SurfaceAttributes<WindowSurface> BuildSurfaceAttributes(
        this IWindow window,
        SurfaceAttributesBuilder<WindowSurface>? builder = null)
    {
        RawWindowHandle windowHandle = window.WindowHandle
            ?? throw new GlutinException("The Winit window does not expose a raw window handle.");

        var size = window.SurfaceSize;
        return (builder ?? SurfaceAttributesBuilder<WindowSurface>.New())
            .BuildWindow(windowHandle, size.Width, size.Height);
    }

    public static void ResizeSurface<TSurface>(
        this IWindow window,
        Surface<TSurface> surface,
        PossiblyCurrentContext context)
        where TSurface : struct, IResizeableSurface
    {
        var size = window.SurfaceSize;
        if (size.Width == 0 || size.Height == 0)
        {
            return;
        }

        surface.Resize(context, size.Width, size.Height);
    }
}

public static class WinitEventLoopExtensions
{
    public static RawDisplayHandle? GetGlDisplayHandle(this IActiveEventLoop eventLoop)
    {
        return eventLoop.DisplayHandle;
    }
}
