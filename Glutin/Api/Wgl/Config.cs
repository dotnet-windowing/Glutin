#if WINDOWS
using RawWindowHandles;
using GlutinConfig = Glutin.Config;
using GlutinDisplay = Glutin.Display;

namespace Glutin.Backend.Wgl;

public sealed unsafe class Config : IPlatformGlConfig
{
    private const int MaxQueryConfigs = 256;

    private readonly Display _display;
    private readonly nint _hdc;
    private readonly int _pixelFormatIndex;
    private readonly Ffi.PIXELFORMATDESCRIPTOR? _descriptor;

    internal Config(
        Display display,
        nint hdc,
        int pixelFormatIndex,
        Ffi.PIXELFORMATDESCRIPTOR? descriptor)
    {
        _display = display;
        _hdc = hdc;
        _pixelFormatIndex = pixelFormatIndex;
        _descriptor = descriptor;
    }

    internal nint Hdc => _hdc;

    internal int PixelFormatIndex => _pixelFormatIndex;

    internal bool IsSingleBuffered => _descriptor is { } descriptor
        ? (descriptor.dwFlags & Ffi.PfdDoubleBuffer) == 0
        : RawAttribute(WglConstants.DoubleBufferArb) == 0;

    internal static IEnumerable<Config> FindConfigs(Display display, ConfigTemplate template)
    {
        nint hwnd = 0;
        if (template.NativeWindow is { } nativeWindow)
        {
            if (!nativeWindow.TryGetValue(out RawWindowHandle.Win32 win32))
            {
                throw new GlutinException("provided native window is not supported by WGL");
            }

            hwnd = win32.Hwnd;
        }

        nint hdc = Ffi.GetDC(hwnd);
        if (hdc == 0)
        {
            throw Ffi.LastError("GetDC");
        }

        return display.Wgl is { HasChoosePixelFormatARB: true }
            ? FindConfigsArb(display, template, hdc)
            : FindNormalConfigs(display, template, hdc);
    }

    internal static (int PixelFormatIndex, Ffi.PIXELFORMATDESCRIPTOR Descriptor) ChooseDummyPixelFormat(nint hdc)
    {
        var descriptor = Ffi.PIXELFORMATDESCRIPTOR.Create();
        descriptor.dwFlags = Ffi.PfdDrawToWindow | Ffi.PfdSupportOpenGl | Ffi.PfdDoubleBuffer;
        descriptor.cColorBits = 24;
        descriptor.cRedBits = 8;
        descriptor.cGreenBits = 8;
        descriptor.cBlueBits = 8;
        descriptor.cAlphaBits = 8;
        descriptor.cDepthBits = 24;
        descriptor.cStencilBits = 8;

        int pixelFormat = Ffi.ChoosePixelFormat(hdc, &descriptor);

        if (pixelFormat == 0)
        {
            throw Ffi.LastError("ChoosePixelFormat");
        }

        return (pixelFormat, descriptor);
    }

    internal void ApplyOnNativeWindow(RawWindowHandle rawWindowHandle)
    {
        if (!rawWindowHandle.TryGetValue(out RawWindowHandle.Win32 win32))
        {
            throw new GlutinException("provided native window is not supported by WGL");
        }

        nint hdc = Ffi.GetDC(win32.Hwnd);
        if (hdc == 0)
        {
            throw Ffi.LastError("GetDC");
        }

        try
        {
            int current = Ffi.GetPixelFormat(hdc);
            if (current == _pixelFormatIndex)
            {
                return;
            }

            if (current != 0)
            {
                throw new GlutinException(
                    $"native window already has pixel format {current}, cannot set {_pixelFormatIndex}");
            }

            Ffi.PIXELFORMATDESCRIPTOR descriptor = _descriptor ?? Ffi.PIXELFORMATDESCRIPTOR.Create();
            Ffi.PIXELFORMATDESCRIPTOR* descriptorPtr = _descriptor.HasValue ? &descriptor : null;
            if (!Ffi.SetPixelFormat(hdc, _pixelFormatIndex, descriptorPtr))
            {
                throw Ffi.LastError("SetPixelFormat");
            }
        }
        finally
        {
            Ffi.ReleaseDC(win32.Hwnd, hdc);
        }
    }

    public ColorBufferType? ColorBufferType
    {
        get
        {
            if (_descriptor is { } descriptor)
            {
                return Glutin.ColorBufferType.FromRgb(
                    descriptor.cRedBits,
                    descriptor.cGreenBits,
                    descriptor.cBlueBits);
            }

            return Glutin.ColorBufferType.FromRgb(
                (byte)RawAttribute(WglConstants.RedBitsArb),
                (byte)RawAttribute(WglConstants.GreenBitsArb),
                (byte)RawAttribute(WglConstants.BlueBitsArb));
        }
    }

    public bool FloatPixels =>
        _descriptor is null
        && _display.Features.HasFlag(DisplayFeatures.FloatPixelFormat)
        && RawAttribute(WglConstants.PixelTypeArb) == WglConstants.TypeRgbaFloatArb;

    public byte AlphaSize => _descriptor is { } descriptor
        ? descriptor.cAlphaBits
        : (byte)RawAttribute(WglConstants.AlphaBitsArb);

    public byte DepthSize => _descriptor is { } descriptor
        ? descriptor.cDepthBits
        : (byte)RawAttribute(WglConstants.DepthBitsArb);

    public byte StencilSize => _descriptor is { } descriptor
        ? descriptor.cStencilBits
        : (byte)RawAttribute(WglConstants.StencilBitsArb);

    public byte NumSamples => _descriptor is null && _display.Features.HasFlag(DisplayFeatures.MultisamplingPixelFormats)
        ? (byte)RawAttribute(WglConstants.SamplesArb)
        : (byte)0;

    public bool SrgbCapable => _descriptor is null
        && _display.Features.HasFlag(DisplayFeatures.SrgbFramebuffers)
        && RawAttribute(WglConstants.FramebufferSrgbCapableArb) != 0;

    public bool? SupportsTransparency => _descriptor is null && RawAttribute(WglConstants.TransparentArb) == 1
        ? true
        : null;

    public bool HardwareAccelerated => _descriptor is { } descriptor
        ? (descriptor.dwFlags & Ffi.PfdGenericFormat) == 0
        : RawAttribute(WglConstants.AccelerationArb) == WglConstants.FullAccelerationArb;

    public ConfigSurfaceTypes ConfigSurfaceTypes
    {
        get
        {
            ConfigSurfaceTypes surfaceTypes = ConfigSurfaceTypes.None;

            if (_descriptor is { } descriptor)
            {
                if ((descriptor.dwFlags & Ffi.PfdDrawToWindow) != 0)
                {
                    surfaceTypes |= ConfigSurfaceTypes.Window;
                }

                if ((descriptor.dwFlags & Ffi.PfdDrawToBitmap) != 0)
                {
                    surfaceTypes |= ConfigSurfaceTypes.Pixmap;
                }

                return surfaceTypes;
            }

            if (RawAttribute(WglConstants.DrawToWindowArb) != 0)
            {
                surfaceTypes |= ConfigSurfaceTypes.Window;
            }

            if (RawAttribute(WglConstants.DrawToPbufferArb) != 0)
            {
                surfaceTypes |= ConfigSurfaceTypes.Pbuffer;
            }

            return surfaceTypes;
        }
    }

    public Api Api => _display.Features.HasFlag(DisplayFeatures.CreateEsContext)
        ? Api.OpenGl | Api.Gles1 | Api.Gles2
        : Api.OpenGl;

    public GlutinDisplay Display => _display.Facade;

    public RawConfig RawConfig => new(new RawConfig.Wgl(_pixelFormatIndex));

    private static IEnumerable<Config> FindNormalConfigs(
        Display display,
        ConfigTemplate template,
        nint hdc)
    {
        Ffi.PIXELFORMATDESCRIPTOR descriptor = CreateDescriptor(template);
        int pixelFormat = Ffi.ChoosePixelFormat(hdc, &descriptor);

        if (pixelFormat == 0)
        {
            throw Ffi.LastError("ChoosePixelFormat");
        }

        Ffi.PIXELFORMATDESCRIPTOR actual = Ffi.PIXELFORMATDESCRIPTOR.Create();
        if (Ffi.DescribePixelFormat(hdc, pixelFormat, (uint)sizeof(Ffi.PIXELFORMATDESCRIPTOR), &actual) == 0)
        {
            throw Ffi.LastError("DescribePixelFormat");
        }

        if (actual.iPixelType != Ffi.PfdTypeRgba)
        {
            throw new GlutinException("WGL selected a non-RGBA pixel format");
        }

        return [new Config(display, hdc, pixelFormat, actual)];
    }

    private static IEnumerable<Config> FindConfigsArb(
        Display display,
        ConfigTemplate template,
        nint hdc)
    {
        WglExtensions wgl = display.Wgl!;
        List<int> attrs = BuildArbAttributes(display, template);

        int[] formats = new int[MaxQueryConfigs];
        uint numFormats = 0;
        fixed (int* attrsPtr = attrs.ToArray())
        fixed (int* formatsPtr = formats)
        {
            if (wgl.ChoosePixelFormatARB(hdc, attrsPtr, null, MaxQueryConfigs, formatsPtr, &numFormats) == 0)
            {
                throw Ffi.LastError("wglChoosePixelFormatARB");
            }
        }

        return formats.Take((int)numFormats).Select(format => new Config(display, hdc, format, descriptor: null));
    }

    private static Ffi.PIXELFORMATDESCRIPTOR CreateDescriptor(ConfigTemplate template)
    {
        if (!template.ColorBufferType.TryGetValue(out ColorBufferType.Rgb rgb))
        {
            throw new GlutinException("luminance buffers are not supported with WGL");
        }

        var descriptor = Ffi.PIXELFORMATDESCRIPTOR.Create();
        descriptor.dwFlags = Ffi.PfdSupportOpenGl;

        if (!template.SingleBuffering)
        {
            descriptor.dwFlags |= Ffi.PfdDoubleBuffer;
        }

        if (template.ConfigSurfaceTypes.HasFlag(ConfigSurfaceTypes.Window))
        {
            descriptor.dwFlags |= Ffi.PfdDrawToWindow;
        }

        if (template.ConfigSurfaceTypes.HasFlag(ConfigSurfaceTypes.Pixmap))
        {
            descriptor.dwFlags |= Ffi.PfdDrawToBitmap;
        }

        descriptor.dwFlags |= template.Stereoscopy switch
        {
            true => Ffi.PfdStereo,
            false => 0,
            null => Ffi.PfdStereoDontCare,
        };

        descriptor.dwFlags |= template.HardwareAccelerated switch
        {
            true => Ffi.PfdGenericAccelerated,
            false => Ffi.PfdGenericFormat,
            null => 0,
        };

        descriptor.cColorBits = (byte)(rgb.RedSize + rgb.GreenSize + rgb.BlueSize);
        descriptor.cRedBits = rgb.RedSize;
        descriptor.cGreenBits = rgb.GreenSize;
        descriptor.cBlueBits = rgb.BlueSize;
        descriptor.cAlphaBits = template.AlphaSize;
        descriptor.cDepthBits = template.DepthSize;
        descriptor.cStencilBits = template.StencilSize;
        return descriptor;
    }

    private static List<int> BuildArbAttributes(Display display, ConfigTemplate template)
    {
        if (!template.ColorBufferType.TryGetValue(out ColorBufferType.Rgb rgb))
        {
            throw new GlutinException("luminance buffers are not supported with WGL");
        }

        var attrs = new List<int>
        {
            WglConstants.RedBitsArb, rgb.RedSize,
            WglConstants.GreenBitsArb, rgb.GreenSize,
            WglConstants.BlueBitsArb, rgb.BlueSize,
            WglConstants.AlphaBitsArb, template.AlphaSize,
            WglConstants.DepthBitsArb, template.DepthSize,
            WglConstants.StencilBitsArb, template.StencilSize,
            WglConstants.SupportOpenGlArb, 1,
            WglConstants.DoubleBufferArb, template.SingleBuffering ? 0 : 1,
            WglConstants.PixelTypeArb,
            template.FloatPixels ? WglConstants.TypeRgbaFloatArb : WglConstants.TypeRgbaArb,
        };

        if (template.FloatPixels && !display.Features.HasFlag(DisplayFeatures.FloatPixelFormat))
        {
            throw new GlutinException("float pixels are not supported with WGL");
        }

        if (template.NumSamples is { } numSamples
            && display.Features.HasFlag(DisplayFeatures.MultisamplingPixelFormats))
        {
            attrs.Add(WglConstants.SampleBuffersArb);
            attrs.Add(1);
            attrs.Add(WglConstants.SamplesArb);
            attrs.Add(numSamples);
        }

        if (template.Stereoscopy is { } stereo)
        {
            attrs.Add(WglConstants.StereoArb);
            attrs.Add(stereo ? 1 : 0);
        }

        if (template.HardwareAccelerated is { } hardwareAccelerated)
        {
            attrs.Add(WglConstants.AccelerationArb);
            attrs.Add(hardwareAccelerated ? WglConstants.FullAccelerationArb : WglConstants.NoAccelerationArb);
        }

        if (template.ConfigSurfaceTypes.HasFlag(ConfigSurfaceTypes.Window))
        {
            attrs.Add(WglConstants.DrawToWindowArb);
            attrs.Add(1);
        }

        if (template.ConfigSurfaceTypes.HasFlag(ConfigSurfaceTypes.Pbuffer))
        {
            attrs.Add(WglConstants.DrawToPbufferArb);
            attrs.Add(1);
        }

        if (template.Transparency)
        {
            attrs.Add(WglConstants.TransparentArb);
            attrs.Add(1);
        }

        attrs.Add(0);
        return attrs;
    }

    private int RawAttribute(int attribute)
    {
        if (_display.Wgl is not { HasGetPixelFormatAttribivARB: true } wgl)
        {
            return 0;
        }

        int value = 0;
        if (wgl.GetPixelFormatAttribivARB(
            _hdc,
            _pixelFormatIndex,
            Ffi.PfdMainPlane,
            1,
            &attribute,
            &value) == 0)
        {
            return 0;
        }

        return value;
    }
}
#endif
