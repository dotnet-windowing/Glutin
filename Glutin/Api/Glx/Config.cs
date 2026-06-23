#if !ANDROID
using RawWindowHandles;
using GlutinConfig = Glutin.Config;
using GlutinDisplay = Glutin.Display;

namespace Glutin.Backend.Glx;

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

    internal bool IsSingleBuffered => RawAttribute(GlxConstants.DoubleBuffer) == 0;

    internal uint? X11VisualId
    {
        get
        {
            Ffi.XVisualInfo* visual = Ffi.glXGetVisualFromFBConfig(_display.Raw, _raw);
            if (visual is null)
            {
                return null;
            }

            try
            {
                return checked((uint)visual->VisualId);
            }
            finally
            {
                Ffi.XFree((nint)visual);
            }
        }
    }

    internal static IEnumerable<Config> FindConfigs(Display display, ConfigTemplate template)
    {
        List<int> attrs = BuildAttributes(display, template);

        nint configsPtr;
        int count;
        fixed (int* attrsPtr = attrs.ToArray())
        {
            configsPtr = Ffi.glXChooseFBConfig(display.Raw, display.Screen, attrsPtr, out count);
        }

        if (configsPtr == 0 || count <= 0)
        {
            throw new GlutinException("glXChooseFBConfig did not return a matching config.");
        }

        try
        {
            var configs = new nint[count];
            nint* rawConfigs = (nint*)configsPtr;
            for (int i = 0; i < count; i++)
            {
                configs[i] = rawConfigs[i];
            }

            return configs
                .Select(raw => new Config(display, raw))
                .Where(config => !template.Transparency || config.SupportsTransparency == true)
                .ToArray();
        }
        finally
        {
            Ffi.XFree(configsPtr);
        }
    }

    public ColorBufferType? ColorBufferType
    {
        get
        {
            return RawAttribute(GlxConstants.XVisualType) switch
            {
                GlxConstants.TrueColor => Glutin.ColorBufferType.FromRgb(
                    (byte)RawAttribute(GlxConstants.RedSize),
                    (byte)RawAttribute(GlxConstants.GreenSize),
                    (byte)RawAttribute(GlxConstants.BlueSize)),
                GlxConstants.GrayScale => Glutin.ColorBufferType.FromLuminance(
                    (byte)RawAttribute(GlxConstants.RedSize)),
                _ => null,
            };
        }
    }

    public bool FloatPixels =>
        _display.Features.HasFlag(DisplayFeatures.FloatPixelFormat)
        && RawAttribute(GlxConstants.RenderType) == GlxConstants.RgbaFloatBitArb;

    public byte AlphaSize => (byte)RawAttribute(GlxConstants.AlphaSize);

    public byte DepthSize => (byte)RawAttribute(GlxConstants.DepthSize);

    public byte StencilSize => (byte)RawAttribute(GlxConstants.StencilSize);

    public byte NumSamples => (byte)RawAttribute(GlxConstants.Samples);

    public bool SrgbCapable
    {
        get
        {
            if (_display.Extensions.Contains("GLX_ARB_framebuffer_sRGB"))
            {
                return RawAttribute(GlxConstants.FramebufferSrgbCapableArb) != 0;
            }

            return _display.Extensions.Contains("GLX_EXT_framebuffer_sRGB")
                && RawAttribute(GlxConstants.FramebufferSrgbCapableExt) != 0;
        }
    }

    public bool? SupportsTransparency => null;

    public bool HardwareAccelerated => RawAttribute(GlxConstants.ConfigCaveat) != GlxConstants.SlowConfig;

    public ConfigSurfaceTypes ConfigSurfaceTypes
    {
        get
        {
            int raw = RawAttribute(GlxConstants.DrawableType);
            ConfigSurfaceTypes types = ConfigSurfaceTypes.None;
            if ((raw & GlxConstants.WindowBit) != 0)
            {
                types |= ConfigSurfaceTypes.Window;
            }

            if ((raw & GlxConstants.PixmapBit) != 0)
            {
                types |= ConfigSurfaceTypes.Pixmap;
            }

            if ((raw & GlxConstants.PbufferBit) != 0)
            {
                types |= ConfigSurfaceTypes.Pbuffer;
            }

            return types;
        }
    }

    public Api Api => _display.Features.HasFlag(DisplayFeatures.CreateEsContext)
        ? Api.OpenGl | Api.Gles1 | Api.Gles2
        : Api.OpenGl;

    public GlutinDisplay Display => _display.Facade;

    public RawConfig RawConfig => new(new RawConfig.Glx((nuint)_raw));

    private int RawAttribute(int attribute)
    {
        return Ffi.glXGetFBConfigAttrib(_display.Raw, _raw, attribute, out int value) == 0
            ? value
            : 0;
    }

    private static List<int> BuildAttributes(Display display, ConfigTemplate template)
    {
        var attrs = new List<int>();

        if (template.ColorBufferType.TryGetValue(out ColorBufferType.Rgb rgb))
        {
            attrs.Add(GlxConstants.XVisualType);
            attrs.Add(GlxConstants.TrueColor);
            attrs.Add(GlxConstants.RedSize);
            attrs.Add(rgb.RedSize);
            attrs.Add(GlxConstants.GreenSize);
            attrs.Add(rgb.GreenSize);
            attrs.Add(GlxConstants.BlueSize);
            attrs.Add(rgb.BlueSize);
        }
        else if (template.ColorBufferType.TryGetValue(out ColorBufferType.Luminance luminance))
        {
            attrs.Add(GlxConstants.XVisualType);
            attrs.Add(GlxConstants.GrayScale);
            attrs.Add(GlxConstants.RedSize);
            attrs.Add(luminance.Size);
        }

        attrs.Add(GlxConstants.RenderType);
        if (template.FloatPixels)
        {
            if (!display.Features.HasFlag(DisplayFeatures.FloatPixelFormat))
            {
                throw new GlutinException("float pixels are not supported with GLX");
            }

            attrs.Add(GlxConstants.RgbaFloatBitArb);
        }
        else
        {
            attrs.Add(GlxConstants.RgbaBit);
        }

        if (template.HardwareAccelerated is { } hardwareAccelerated)
        {
            attrs.Add(GlxConstants.ConfigCaveat);
            attrs.Add(hardwareAccelerated ? GlxConstants.None : GlxConstants.SlowConfig);
        }

        attrs.Add(GlxConstants.DoubleBuffer);
        attrs.Add(template.SingleBuffering ? 0 : 1);

        attrs.Add(GlxConstants.AlphaSize);
        attrs.Add(template.AlphaSize);
        attrs.Add(GlxConstants.DepthSize);
        attrs.Add(template.DepthSize);
        attrs.Add(GlxConstants.StencilSize);
        attrs.Add(template.StencilSize);

        if (template.NativeWindow is { } nativeWindow
            && nativeWindow.TryGetValue(out RawWindowHandle.Xlib xlibWindow)
            && xlibWindow.VisualId is { } visualId
            && visualId != 0)
        {
            attrs.Add(GlxConstants.VisualId);
            attrs.Add(checked((int)visualId));
        }

        attrs.Add(GlxConstants.DrawableType);
        int surfaceType = 0;
        if (template.ConfigSurfaceTypes.HasFlag(ConfigSurfaceTypes.Window))
        {
            surfaceType |= GlxConstants.WindowBit;
        }

        if (template.ConfigSurfaceTypes.HasFlag(ConfigSurfaceTypes.Pixmap))
        {
            surfaceType |= GlxConstants.PixmapBit;
        }

        if (template.ConfigSurfaceTypes.HasFlag(ConfigSurfaceTypes.Pbuffer))
        {
            surfaceType |= GlxConstants.PbufferBit;
        }

        attrs.Add(surfaceType);

        if (template.MaxPbufferWidth is { } maxPbufferWidth)
        {
            attrs.Add(GlxConstants.MaxPbufferWidth);
            attrs.Add(checked((int)maxPbufferWidth));
        }

        if (template.MaxPbufferHeight is { } maxPbufferHeight)
        {
            attrs.Add(GlxConstants.MaxPbufferHeight);
            attrs.Add(checked((int)maxPbufferHeight));
        }

        if (template.Stereoscopy is { } stereoscopy)
        {
            attrs.Add(GlxConstants.Stereo);
            attrs.Add(stereoscopy ? 1 : 0);
        }

        if (template.NumSamples is { } numSamples
            && display.Features.HasFlag(DisplayFeatures.MultisamplingPixelFormats))
        {
            attrs.Add(GlxConstants.SampleBuffers);
            attrs.Add(1);
            attrs.Add(GlxConstants.Samples);
            attrs.Add(numSamples);
        }

        attrs.Add(0);
        return attrs;
    }
}
#endif
