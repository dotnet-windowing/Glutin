using RawWindowHandles;

namespace Glutin;

public interface IGlConfig
{
    ColorBufferType? ColorBufferType { get; }

    bool FloatPixels { get; }

    byte AlphaSize { get; }

    byte DepthSize { get; }

    byte StencilSize { get; }

    byte NumSamples { get; }

    bool SrgbCapable { get; }

    bool? SupportsTransparency { get; }

    bool HardwareAccelerated { get; }

    ConfigSurfaceTypes ConfigSurfaceTypes { get; }

    Api Api { get; }
}

public interface IGetGlConfig
{
    Config Config { get; }
}

public interface IAsRawConfig
{
    RawConfig RawConfig { get; }
}

public interface IPlatformGlConfig : IGlConfig, IGetGlDisplay, IAsRawConfig
{
}

public sealed class Config : IGlConfig, IGetGlDisplay, IAsRawConfig
{
    private readonly IPlatformGlConfig _backend;

    internal Config(IPlatformGlConfig backend)
    {
        _backend = backend;
    }

    internal IPlatformGlConfig Backend => _backend;

    public ColorBufferType? ColorBufferType => _backend.ColorBufferType;

    public bool FloatPixels => _backend.FloatPixels;

    public byte AlphaSize => _backend.AlphaSize;

    public byte DepthSize => _backend.DepthSize;

    public byte StencilSize => _backend.StencilSize;

    public byte NumSamples => _backend.NumSamples;

    public bool SrgbCapable => _backend.SrgbCapable;

    public bool? SupportsTransparency => _backend.SupportsTransparency;

    public bool HardwareAccelerated => _backend.HardwareAccelerated;

    public ConfigSurfaceTypes ConfigSurfaceTypes => _backend.ConfigSurfaceTypes;

    public Api Api => _backend.Api;

    public Display Display => _backend.Display;

    public RawConfig RawConfig => _backend.RawConfig;
}

public sealed class ConfigTemplateBuilder
{
    private ConfigTemplate _template = ConfigTemplate.Default;

    public static ConfigTemplateBuilder New()
    {
        return new ConfigTemplateBuilder();
    }

    public ConfigTemplateBuilder WithAlphaSize(byte alphaSize)
    {
        _template = _template with { AlphaSize = alphaSize };
        return this;
    }

    public ConfigTemplateBuilder WithFloatPixels(bool floatPixels)
    {
        _template = _template with { FloatPixels = floatPixels };
        return this;
    }

    public ConfigTemplateBuilder WithStencilSize(byte stencilSize)
    {
        _template = _template with { StencilSize = stencilSize };
        return this;
    }

    public ConfigTemplateBuilder WithDepthSize(byte depthSize)
    {
        _template = _template with { DepthSize = depthSize };
        return this;
    }

    public ConfigTemplateBuilder WithMultisampling(byte numSamples)
    {
        if (!IsPowerOfTwo(numSamples))
        {
            throw new ArgumentOutOfRangeException(nameof(numSamples), "Multisampling count must be a power of two.");
        }

        _template = _template with { NumSamples = numSamples };
        return this;
    }

    public ConfigTemplateBuilder WithSurfaceType(ConfigSurfaceTypes configSurfaceTypes)
    {
        _template = _template with { ConfigSurfaceTypes = configSurfaceTypes };
        return this;
    }

    public ConfigTemplateBuilder WithBufferType(ColorBufferType colorBufferType)
    {
        _template = _template with { ColorBufferType = colorBufferType };
        return this;
    }

    public ConfigTemplateBuilder WithApi(Api api)
    {
        _template = _template with { Api = api };
        return this;
    }

    public ConfigTemplateBuilder WithStereoscopy(bool? stereoscopy)
    {
        _template = _template with { Stereoscopy = stereoscopy };
        return this;
    }

    public ConfigTemplateBuilder WithSingleBuffering(bool singleBuffering)
    {
        _template = _template with { SingleBuffering = singleBuffering };
        return this;
    }

    public ConfigTemplateBuilder WithTransparency(bool transparency)
    {
        _template = _template with { Transparency = transparency };
        return this;
    }

    public ConfigTemplateBuilder WithPbufferSizes(uint width, uint height)
    {
        RequireNonZero(width, nameof(width));
        RequireNonZero(height, nameof(height));
        _template = _template with { MaxPbufferWidth = width, MaxPbufferHeight = height };
        return this;
    }

    public ConfigTemplateBuilder PreferHardwareAccelerated(bool? hardwareAccelerated)
    {
        _template = _template with { HardwareAccelerated = hardwareAccelerated };
        return this;
    }

    public ConfigTemplateBuilder CompatibleWithNativeWindow(RawWindowHandle nativeWindow)
    {
        _template = _template with { NativeWindow = nativeWindow };
        return this;
    }

    public ConfigTemplateBuilder WithSwapInterval(ushort? minSwapInterval, ushort? maxSwapInterval)
    {
        _template = _template with
        {
            MinSwapInterval = minSwapInterval,
            MaxSwapInterval = maxSwapInterval,
        };
        return this;
    }

    public ConfigTemplate Build()
    {
        return _template;
    }

    private static bool IsPowerOfTwo(byte value)
    {
        return value != 0 && (value & (value - 1)) == 0;
    }

    private static void RequireNonZero(uint value, string parameterName)
    {
        if (value == 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Value must be non-zero.");
        }
    }
}

public sealed record ConfigTemplate
{
    public static ConfigTemplate Default { get; } = new();

    public ColorBufferType ColorBufferType { get; init; } =
        ColorBufferType.FromRgb(redSize: 8, greenSize: 8, blueSize: 8);

    public byte AlphaSize { get; init; } = 8;

    public byte DepthSize { get; init; } = 24;

    public byte StencilSize { get; init; } = 8;

    public byte? NumSamples { get; init; }

    public ushort? MinSwapInterval { get; init; }

    public ushort? MaxSwapInterval { get; init; }

    public ConfigSurfaceTypes ConfigSurfaceTypes { get; init; } = ConfigSurfaceTypes.Window;

    public Api? Api { get; init; }

    public bool Transparency { get; init; }

    public bool SingleBuffering { get; init; }

    public bool? Stereoscopy { get; init; }

    public bool FloatPixels { get; init; }

    public uint? MaxPbufferWidth { get; init; }

    public uint? MaxPbufferHeight { get; init; }

    public bool? HardwareAccelerated { get; init; }

    public RawWindowHandle? NativeWindow { get; init; }
}

[Flags]
public enum ConfigSurfaceTypes : byte
{
    None = 0,
    Window = 1 << 0,
    Pixmap = 1 << 1,
    Pbuffer = 1 << 2,
}

[Flags]
public enum Api : byte
{
    None = 0,
    OpenGl = 1 << 0,
    Gles1 = 1 << 1,
    Gles2 = 1 << 2,
    Gles3 = 1 << 3,
}

public record struct ColorBufferType
{
    public readonly record struct Rgb(byte RedSize, byte GreenSize, byte BlueSize);

    public readonly record struct Luminance(byte Size);

    private const byte RgbTag = 0;
    private const byte LuminanceTag = 1;

    private byte _tag;
    private Rgb _rgb;
    private Luminance _luminance;

    public ColorBufferType(Rgb value)
    {
        this = default;
        _tag = RgbTag;
        _rgb = value;
    }

    public ColorBufferType(Luminance value)
    {
        this = default;
        _tag = LuminanceTag;
        _luminance = value;
    }

    public static ColorBufferType FromRgb(byte redSize, byte greenSize, byte blueSize)
    {
        return new ColorBufferType(new Rgb(redSize, greenSize, blueSize));
    }

    public static ColorBufferType FromLuminance(byte size)
    {
        return new ColorBufferType(new Luminance(size));
    }

    public bool TryGetValue(out Rgb value)
    {
        value = _rgb;
        return _tag == RgbTag;
    }

    public bool TryGetValue(out Luminance value)
    {
        value = _luminance;
        return _tag == LuminanceTag;
    }
}

public record struct RawConfig
{
    public readonly record struct Egl(nint Config);

    public readonly record struct Glx(nuint FbConfig);

    public readonly record struct Wgl(int PixelFormatIndex);

    public readonly record struct Cgl(nint PixelFormat);

    private const byte EglTag = 0;
    private const byte GlxTag = 1;
    private const byte WglTag = 2;
    private const byte CglTag = 3;

    private byte _tag;
    private Egl _egl;
    private Glx _glx;
    private Wgl _wgl;
    private Cgl _cgl;

    public RawConfig(Egl value)
    {
        this = default;
        _tag = EglTag;
        _egl = value;
    }

    public RawConfig(Glx value)
    {
        this = default;
        _tag = GlxTag;
        _glx = value;
    }

    public RawConfig(Wgl value)
    {
        this = default;
        _tag = WglTag;
        _wgl = value;
    }

    public RawConfig(Cgl value)
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
