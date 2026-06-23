#if !ANDROID
using System.Text;
using RawWindowHandles;
using GlutinDisplay = Glutin.Display;
using GlutinConfig = Glutin.Config;
using GlutinNotCurrentContext = Glutin.NotCurrentContext;
using GlutinSurfaceWindow = Glutin.Surface<Glutin.WindowSurface>;
using GlutinSurfacePbuffer = Glutin.Surface<Glutin.PbufferSurface>;
using GlutinSurfacePixmap = Glutin.Surface<Glutin.PixmapSurface>;

namespace Glutin.Backend.Wgl;

public sealed unsafe class Display : IPlatformGlDisplay
{
    internal const string OpenGlDllEnv = "GLUTIN_WGL_OPENGL_DLL";

    private readonly nint _opengl32;
    private readonly WglExtensions? _wgl;
    private readonly HashSet<string> _extensions;
    private readonly DisplayFeatures _features;
    private readonly GlutinDisplay _facade;
    private bool _disposed;

    private Display(nint opengl32, WglExtensions? wgl, HashSet<string> extensions)
    {
        _opengl32 = opengl32;
        _wgl = wgl;
        _extensions = extensions;
        _features = ExtractDisplayFeatures(extensions);
        _facade = new GlutinDisplay(this);
    }

    internal WglExtensions? Wgl => _wgl;

    internal GlutinDisplay Facade => _facade;

    internal DisplayFeatures Features => _features;

    public static GlutinDisplay New(RawDisplayHandle rawDisplay, RawWindowHandle? nativeWindow)
    {
        if (!rawDisplay.TryGetValue(out RawDisplayHandle.Windows _))
        {
            throw new GlutinException("provided native display is not supported by WGL");
        }

        string dllName = Environment.GetEnvironmentVariable(OpenGlDllEnv) is { Length: > 0 } configured
            ? configured
            : "opengl32.dll";

        nint opengl32 = Ffi.LoadLibrary(dllName);
        if (opengl32 == 0)
        {
            throw Ffi.LastError($"LoadLibraryW({dllName})");
        }

        try
        {
            WglExtensions? wgl = null;
            HashSet<string> extensions = [];

            if (nativeWindow is { } window)
            {
                if (!window.TryGetValue(out RawWindowHandle.Win32 win32))
                {
                    throw new GlutinException("provided native window is not supported by WGL");
                }

                (wgl, extensions) = LoadExtraFunctions(
                    win32.HInstance is { } instance && instance != 0 ? instance : Ffi.CurrentModuleHandle(),
                    win32.Hwnd);
            }

            return new Display(opengl32, wgl, extensions).Facade;
        }
        catch
        {
            Ffi.FreeLibrary(opengl32);
            throw;
        }
    }

    public IEnumerable<GlutinConfig> FindConfigs(ConfigTemplate template)
    {
        return Config.FindConfigs(this, template).Select(config => new GlutinConfig(config));
    }

    public GlutinNotCurrentContext CreateContext(GlutinConfig config, ContextAttributes contextAttributes)
    {
        if (config.Backend is not Config wglConfig)
        {
            throw new GlutinException("WGL display received a config from another backend.");
        }

        return new GlutinNotCurrentContext(new NotCurrentContext(ContextInner.Create(this, wglConfig, contextAttributes)));
    }

    public GlutinSurfaceWindow CreateWindowSurface(
        GlutinConfig config,
        SurfaceAttributes<WindowSurface> surfaceAttributes)
    {
        if (config.Backend is not Config wglConfig)
        {
            throw new GlutinException("WGL display received a config from another backend.");
        }

        return new GlutinSurfaceWindow(Surface<WindowSurface>.CreateWindow(this, wglConfig, surfaceAttributes));
    }

    public GlutinSurfacePbuffer CreatePbufferSurface(
        GlutinConfig config,
        SurfaceAttributes<PbufferSurface> surfaceAttributes)
    {
        if (config.Backend is not Config wglConfig)
        {
            throw new GlutinException("WGL display received a config from another backend.");
        }

        return new GlutinSurfacePbuffer(Surface<PbufferSurface>.CreatePbuffer(this, wglConfig, surfaceAttributes));
    }

    public GlutinSurfacePixmap CreatePixmapSurface(
        GlutinConfig config,
        SurfaceAttributes<PixmapSurface> surfaceAttributes)
    {
        throw new GlutinException("pixmaps are not implemented with WGL");
    }

    public nint GetProcAddress(string symbol)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        byte[] bytes = Encoding.ASCII.GetBytes(symbol + '\0');
        fixed (byte* ptr = bytes)
        {
            nint address = Ffi.wglGetProcAddress(ptr);
            if (!Ffi.IsInvalidProcAddress(address))
            {
                return address;
            }

            address = Ffi.GetProcAddress(_opengl32, ptr);
            return Ffi.IsInvalidProcAddress(address) ? 0 : address;
        }
    }

    public string VersionString => "WGL";

    public DisplayFeatures SupportedFeatures => _features;

    public IReadOnlySet<string> Extensions => _extensions;

    public RawDisplay RawDisplay => new(new RawDisplay.Wgl());

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Ffi.FreeLibrary(_opengl32);
    }

    private static (WglExtensions Extensions, HashSet<string> ClientExtensions) LoadExtraFunctions(
        nint instance,
        nint nativeWindow)
    {
        nint dummyWindow = 0;
        nint hdc = 0;
        nint context = 0;

        try
        {
            dummyWindow = CreateDummyWindow(instance, nativeWindow);
            hdc = Ffi.GetDC(dummyWindow);
            if (hdc == 0)
            {
                throw Ffi.LastError("GetDC");
            }

            (int pixelFormat, Ffi.PIXELFORMATDESCRIPTOR descriptor) = Config.ChooseDummyPixelFormat(hdc);
            if (!Ffi.SetPixelFormat(hdc, pixelFormat, &descriptor))
            {
                throw Ffi.LastError("SetPixelFormat");
            }

            context = Ffi.wglCreateContext(hdc);
            if (context == 0)
            {
                throw Ffi.LastError("wglCreateContext");
            }

            if (!Ffi.wglMakeCurrent(hdc, context))
            {
                throw Ffi.LastError("wglMakeCurrent");
            }

            WglExtensions wgl = WglExtensions.Load();
            return (wgl, wgl.LoadExtensionSet(hdc));
        }
        finally
        {
            if (Ffi.wglGetCurrentContext() == context)
            {
                Ffi.wglMakeCurrent(0, 0);
            }

            if (context != 0)
            {
                Ffi.wglDeleteContext(context);
            }

            if (hdc != 0 && dummyWindow != 0)
            {
                Ffi.ReleaseDC(dummyWindow, hdc);
            }

            if (dummyWindow != 0)
            {
                Ffi.DestroyWindow(dummyWindow);
            }
        }
    }

    private static nint CreateDummyWindow(nint instance, nint nativeWindow)
    {
        string className = $"GlutinWglDummyWindow-{Environment.ProcessId}-{Environment.CurrentManagedThreadId}";
        string title = "dummy window";
        int width = 1;
        int height = 1;

        if (nativeWindow != 0 && Ffi.GetClientRect(nativeWindow, out Ffi.RECT rect))
        {
            width = Math.Max(1, rect.Right - rect.Left);
            height = Math.Max(1, rect.Bottom - rect.Top);
        }

        fixed (char* classNamePtr = className)
        fixed (char* titlePtr = title)
        {
            var windowClass = new Ffi.WNDCLASSEXW
            {
                cbSize = (uint)sizeof(Ffi.WNDCLASSEXW),
                hInstance = instance,
                lpfnWndProc = (nint)(delegate* unmanaged[Stdcall]<nint, uint, nuint, nint, nint>)&Ffi.DummyWndProc,
                lpszClassName = classNamePtr,
            };

            ushort atom = Ffi.RegisterClassEx(&windowClass);
            if (atom == 0 && MarshalLastError() != Ffi.ErrorClassAlreadyExists)
            {
                throw Ffi.LastError("RegisterClassExW");
            }

            nint hwnd = Ffi.CreateWindowEx(
                0,
                classNamePtr,
                titlePtr,
                Ffi.WsPopup | Ffi.WsClipSiblings | Ffi.WsClipChildren,
                0,
                0,
                width,
                height,
                0,
                0,
                instance,
                null);

            if (hwnd == 0)
            {
                throw Ffi.LastError("CreateWindowExW");
            }

            return hwnd;
        }
    }

    private static DisplayFeatures ExtractDisplayFeatures(HashSet<string> extensions)
    {
        DisplayFeatures features = DisplayFeatures.None;

        if (extensions.Contains("WGL_ARB_multisample"))
        {
            features |= DisplayFeatures.MultisamplingPixelFormats;
        }

        if (extensions.Contains("WGL_ARB_pixel_format_float"))
        {
            features |= DisplayFeatures.FloatPixelFormat;
        }

        if (extensions.Contains("WGL_ARB_framebuffer_sRGB")
            || extensions.Contains("WGL_EXT_framebuffer_sRGB")
            || extensions.Contains("WGL_EXT_colorspace"))
        {
            features |= DisplayFeatures.SrgbFramebuffers;
        }

        if (extensions.Contains("WGL_EXT_create_context_es2_profile")
            || extensions.Contains("WGL_EXT_create_context_es_profile"))
        {
            features |= DisplayFeatures.CreateEsContext;
        }

        if (extensions.Contains("WGL_EXT_swap_control"))
        {
            features |= DisplayFeatures.SwapControl;
        }

        if (extensions.Contains("WGL_ARB_create_context_robustness"))
        {
            features |= DisplayFeatures.ContextRobustness;
        }

        if (extensions.Contains("WGL_ARB_context_flush_control"))
        {
            features |= DisplayFeatures.ContextReleaseBehavior;
        }

        if (extensions.Contains("WGL_ARB_create_context_no_error"))
        {
            features |= DisplayFeatures.ContextNoError;
        }

        return features;
    }

    private static int MarshalLastError()
    {
        return System.Runtime.InteropServices.Marshal.GetLastWin32Error();
    }
}
#endif
