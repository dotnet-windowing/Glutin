using Android.App;
using Android.Content.PM;
using Glutin;
using Glutin.OpenGL;
using Glutin.Winit;
using RawWindowHandles;
using Winit.Core;
using Winit.Platform.Android;
using CoreWindowId = Winit.Core.WindowId;

namespace GlutinAndroidWindow;

[Activity(
    Label = "Glutin EGL",
    MainLauncher = true,
    Exported = true,
    ConfigurationChanges =
        ConfigChanges.Density |
        ConfigChanges.Keyboard |
        ConfigChanges.KeyboardHidden |
        ConfigChanges.Orientation |
        ConfigChanges.ScreenLayout |
        ConfigChanges.ScreenSize |
        ConfigChanges.UiMode)]
public sealed class MainActivity : WinitActivity
{
    protected override IApplicationHandler CreateApplicationHandler() => new OpenGlApp();
}

internal sealed unsafe class OpenGlApp : IApplicationHandler
{
    private IWindow? _window;
    private Display? _display;
    private Config? _config;
    private Surface<WindowSurface>? _surface;
    private PossiblyCurrentContext? _context;
    private float _phase;

    public void CanCreateSurfaces(IActiveEventLoop eventLoop)
    {
        if (_surface is not null)
        {
            return;
        }

        _window ??= eventLoop.CreateWindow(WindowAttributes.Default.WithTitle("Glutin EGL"));
        WindowSurfaceTarget target = _window.BuildSurfaceTarget();
        RawDisplayHandle displayHandle = target.DisplayHandle ?? RawDisplayHandle.FromAndroid();

        _display = Display.New(displayHandle, DisplayApiPreference.UseEgl());

        ConfigTemplate template = ConfigTemplateBuilder
            .New()
            .WithApi(Api.Gles2 | Api.Gles3)
            .CompatibleWithNativeWindow(target.WindowHandle)
            .Build();

        _config = _display.FindConfigs(template).FirstOrDefault()
            ?? throw new GlutinException("EGL did not return a matching config.");

        _surface = _display.CreateWindowSurface(_config, target.Attributes);
        _context = _display
            .CreateContext(
                _config,
                ContextAttributesBuilder
                    .New()
                    .WithContextApi(ContextApi.FromGles(new GlVersion(2, 0)))
                    .Build(target.WindowHandle))
            .MakeCurrent(_surface);

        GL.Load(_display.GetProcAddress);
        TryEnableVsync();
        Render();
        _window.RequestRedraw();
    }

    public void WindowEvent(IActiveEventLoop eventLoop, CoreWindowId windowId, WindowEvent windowEvent)
    {
        if (_window is null || windowId != _window.Id)
        {
            return;
        }

        if (windowEvent.TryGetValue(out WindowEvent.SurfaceResized resized))
        {
            if (_context is not null && _surface is not null && resized.Size.Width > 0 && resized.Size.Height > 0)
            {
                _surface.Resize(_context, resized.Size.Width, resized.Size.Height);
                Render();
            }

            return;
        }

        if (windowEvent.TryGetValue(out WindowEvent.RedrawRequested _))
        {
            Render();
        }
    }

    public void AboutToWait(IActiveEventLoop eventLoop)
    {
        _window?.RequestRedraw();
    }

    public void DestroySurfaces(IActiveEventLoop eventLoop)
    {
        DisposeGl();
    }

    private void TryEnableVsync()
    {
        if (_surface is null || _context is null)
        {
            return;
        }

        try
        {
            _surface.SetSwapInterval(_context, SwapInterval.Wait(1));
        }
        catch (GlutinException)
        {
        }
    }

    private void Render()
    {
        if (_window is null || _context is null || _surface is null)
        {
            return;
        }

        var size = _window.SurfaceSize;
        if (size.Width == 0 || size.Height == 0)
        {
            return;
        }

        _context.MakeCurrent(_surface);

        _phase += 0.016f;
        float red = 0.16f + MathF.Sin(_phase) * 0.10f;
        float green = 0.24f + MathF.Sin(_phase + 2.1f) * 0.12f;
        float blue = 0.38f + MathF.Sin(_phase + 4.2f) * 0.14f;

        GL.Viewport(0, 0, checked((int)size.Width), checked((int)size.Height));
        GL.ClearColor(red, green, blue, 1.0f);
        GL.Clear(GL.COLOR_BUFFER_BIT | GL.DEPTH_BUFFER_BIT);

        _window.PrePresentNotify();
        _surface.SwapBuffers(_context);
    }

    private void DisposeGl()
    {
        _context?.Dispose();
        _context = null;
        _surface?.Dispose();
        _surface = null;
        _display?.Dispose();
        _display = null;
        _config = null;
    }
}
