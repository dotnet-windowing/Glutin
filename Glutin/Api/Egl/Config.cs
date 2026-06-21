using RawWindowHandles;
using GlutinDisplay = Glutin.Display;

namespace Glutin.Backend.Egl;

public sealed unsafe class Config : IPlatformGlConfig
{
    private readonly Display _display;
    private readonly nint _raw;

    internal Config(Display display, nint raw)
    {
        _display = display;
        _raw = raw;
    }

    internal nint Raw => _raw;

    internal static IEnumerable<Config> FindConfigs(Display display, ConfigTemplate template)
    {
        List<int> attrs = BuildAttributes(display, template);

        if (Ffi.eglGetConfigs(display.Raw, null, 0, out int total) == EglConstants.False)
        {
            throw Ffi.LastError("eglGetConfigs");
        }

        if (total <= 0)
        {
            throw new GlutinException("EGL did not report any configs.");
        }

        var rawConfigs = new nint[total];
        int count;
        fixed (int* attrsPtr = attrs.ToArray())
        fixed (nint* configsPtr = rawConfigs)
        {
            if (Ffi.eglChooseConfig(display.Raw, attrsPtr, configsPtr, rawConfigs.Length, out count)
                == EglConstants.False)
            {
                throw Ffi.LastError("eglChooseConfig");
            }
        }

        if (count <= 0)
        {
            throw new GlutinException("eglChooseConfig did not return a matching config.");
        }

        return rawConfigs
            .Take(count)
            .Select(raw => new Config(display, raw))
            .Where(config => IsCompatibleWithNativeWindow(config, template.NativeWindow))
            .Where(config => !template.Transparency || config.SupportsTransparency != false)
            .ToArray();
    }

    public uint NativeVisualId => checked((uint)RawAttribute(EglConstants.NativeVisualId));

    public ColorBufferType? ColorBufferType
    {
        get
        {
            return RawAttribute(EglConstants.ColorBufferType) switch
            {
                EglConstants.RgbBuffer => Glutin.ColorBufferType.FromRgb(
                    (byte)RawAttribute(EglConstants.RedSize),
                    (byte)RawAttribute(EglConstants.GreenSize),
                    (byte)RawAttribute(EglConstants.BlueSize)),
                EglConstants.LuminanceBuffer => Glutin.ColorBufferType.FromLuminance(
                    (byte)RawAttribute(EglConstants.LuminanceSize)),
                _ => null,
            };
        }
    }

    public bool FloatPixels =>
        _display.Features.HasFlag(DisplayFeatures.FloatPixelFormat)
        && RawAttribute(EglConstants.ColorComponentTypeExt) == EglConstants.ColorComponentTypeFloatExt;

    public byte AlphaSize => (byte)RawAttribute(EglConstants.AlphaSize);

    public byte DepthSize => (byte)RawAttribute(EglConstants.DepthSize);

    public byte StencilSize => (byte)RawAttribute(EglConstants.StencilSize);

    public byte NumSamples => (byte)RawAttribute(EglConstants.Samples);

    public bool SrgbCapable => _display.Features.HasFlag(DisplayFeatures.SrgbFramebuffers);

    public bool? SupportsTransparency
    {
        get
        {
            if (_display.NativeDisplay.TryGetValue(out RawDisplayHandle.Wayland _))
            {
                return AlphaSize != 0;
            }

            return null;
        }
    }

    public bool HardwareAccelerated => RawAttribute(EglConstants.ConfigCaveat) != EglConstants.SlowConfig;

    public ConfigSurfaceTypes ConfigSurfaceTypes
    {
        get
        {
            int raw = RawAttribute(EglConstants.SurfaceType);
            ConfigSurfaceTypes types = ConfigSurfaceTypes.None;
            if ((raw & EglConstants.WindowBit) != 0)
            {
                types |= ConfigSurfaceTypes.Window;
            }

            if ((raw & EglConstants.PixmapBit) != 0)
            {
                types |= ConfigSurfaceTypes.Pixmap;
            }

            if ((raw & EglConstants.PbufferBit) != 0)
            {
                types |= ConfigSurfaceTypes.Pbuffer;
            }

            return types;
        }
    }

    public Api Api
    {
        get
        {
            int raw = RawAttribute(EglConstants.RenderableType);
            Api api = Api.None;
            if ((raw & EglConstants.OpenGlBit) != 0)
            {
                api |= Api.OpenGl;
            }

            if ((raw & EglConstants.OpenGlEsBit) != 0)
            {
                api |= Api.Gles1;
            }

            if ((raw & EglConstants.OpenGlEs2Bit) != 0)
            {
                api |= Api.Gles2;
            }

            if ((raw & EglConstants.OpenGlEs3Bit) != 0)
            {
                api |= Api.Gles3;
            }

            return api;
        }
    }

    public GlutinDisplay Display => _display.Facade;

    public RawConfig RawConfig => new(new RawConfig.Egl(_raw));

    internal int RawAttribute(int attribute)
    {
        return Ffi.eglGetConfigAttrib(_display.Raw, _raw, attribute, out int value) == EglConstants.True
            ? value
            : 0;
    }

    private static bool IsCompatibleWithNativeWindow(Config config, RawWindowHandle? nativeWindow)
    {
        if (nativeWindow is { } raw
            && raw.TryGetValue(out RawWindowHandle.Xlib xlib)
            && xlib.VisualId is { } visualId
            && visualId != 0)
        {
            return visualId == config.NativeVisualId;
        }

        return true;
    }

    private static List<int> BuildAttributes(Display display, ConfigTemplate template)
    {
        var attrs = new List<int>();

        if (template.ColorBufferType.TryGetValue(out ColorBufferType.Rgb rgb))
        {
            attrs.Add(EglConstants.ColorBufferType);
            attrs.Add(EglConstants.RgbBuffer);
            attrs.Add(EglConstants.RedSize);
            attrs.Add(rgb.RedSize);
            attrs.Add(EglConstants.GreenSize);
            attrs.Add(rgb.GreenSize);
            attrs.Add(EglConstants.BlueSize);
            attrs.Add(rgb.BlueSize);
        }
        else if (template.ColorBufferType.TryGetValue(out ColorBufferType.Luminance luminance))
        {
            attrs.Add(EglConstants.ColorBufferType);
            attrs.Add(EglConstants.LuminanceBuffer);
            attrs.Add(EglConstants.LuminanceSize);
            attrs.Add(luminance.Size);
        }

        if (template.FloatPixels)
        {
            if (!display.Features.HasFlag(DisplayFeatures.FloatPixelFormat))
            {
                throw new GlutinException("float pixels are not supported with EGL");
            }

            attrs.Add(EglConstants.ColorComponentTypeExt);
            attrs.Add(EglConstants.ColorComponentTypeFloatExt);
        }

        attrs.Add(EglConstants.AlphaSize);
        attrs.Add(template.AlphaSize);
        attrs.Add(EglConstants.DepthSize);
        attrs.Add(template.DepthSize);
        attrs.Add(EglConstants.StencilSize);
        attrs.Add(template.StencilSize);

        attrs.Add(EglConstants.SurfaceType);
        int surfaceType = 0;
        if (template.ConfigSurfaceTypes.HasFlag(ConfigSurfaceTypes.Window))
        {
            surfaceType |= EglConstants.WindowBit;
        }

        if (template.ConfigSurfaceTypes.HasFlag(ConfigSurfaceTypes.Pbuffer))
        {
            surfaceType |= EglConstants.PbufferBit;
        }

        if (template.ConfigSurfaceTypes.HasFlag(ConfigSurfaceTypes.Pixmap))
        {
            surfaceType |= EglConstants.PixmapBit;
        }

        attrs.Add(surfaceType);

        if (template.HardwareAccelerated is { } hardwareAccelerated)
        {
            attrs.Add(EglConstants.ConfigCaveat);
            attrs.Add(hardwareAccelerated ? EglConstants.None : EglConstants.SlowConfig);
        }

        if (template.MinSwapInterval is { } minSwapInterval)
        {
            attrs.Add(EglConstants.MinSwapInterval);
            attrs.Add(minSwapInterval);
        }

        if (template.MaxSwapInterval is { } maxSwapInterval)
        {
            attrs.Add(EglConstants.MaxSwapInterval);
            attrs.Add(maxSwapInterval);
        }

        if (template.NumSamples is { } numSamples)
        {
            attrs.Add(EglConstants.SampleBuffers);
            attrs.Add(1);
            attrs.Add(EglConstants.Samples);
            attrs.Add(numSamples);
        }

        attrs.Add(EglConstants.RenderableType);
        attrs.Add(BuildRenderableType(template.Api));

        if (template.MaxPbufferWidth is { } maxPbufferWidth)
        {
            attrs.Add(EglConstants.MaxPbufferWidth);
            attrs.Add(checked((int)maxPbufferWidth));
        }

        if (template.MaxPbufferHeight is { } maxPbufferHeight)
        {
            attrs.Add(EglConstants.MaxPbufferHeight);
            attrs.Add(checked((int)maxPbufferHeight));
        }

        attrs.Add(EglConstants.None);
        return attrs;
    }

    private static int BuildRenderableType(Api? requestedApi)
    {
        if (requestedApi is not { } api)
        {
            return EglConstants.OpenGlEs2Bit;
        }

        int raw = 0;
        if (api.HasFlag(Api.Gles1))
        {
            raw |= EglConstants.OpenGlEsBit;
        }

        if (api.HasFlag(Api.Gles2))
        {
            raw |= EglConstants.OpenGlEs2Bit;
        }

        if (api.HasFlag(Api.Gles3))
        {
            raw |= EglConstants.OpenGlEs3Bit;
        }

        if (api.HasFlag(Api.OpenGl))
        {
            raw |= EglConstants.OpenGlBit;
        }

        return raw;
    }
}
