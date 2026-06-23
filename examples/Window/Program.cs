using Glutin;
using Glutin.OpenGL;
using Glutin.Winit;
using Winit;
using Winit.Core;
using Winit.Dpi;
using Winit.Platform.X11;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        EventLoop eventLoop = OperatingSystem.IsWindows()
            ? EventLoop.New()
            : EventLoop.Builder().WithX11().Build();

        eventLoop.RunApp(new OpenGlWindowApp());
    }
}

internal sealed unsafe class OpenGlWindowApp : IApplicationHandler
{
    private IWindow? _window;
    private Display? _display;
    private Config? _config;
    private Surface<WindowSurface>? _surface;
    private PossiblyCurrentContext? _context;
    private float _phase;

    public void CanCreateSurfaces(IActiveEventLoop eventLoop)
    {
        if (_window is not null)
        {
            return;
        }

        (IWindow? createdWindow, Config config) = DisplayBuilder
            .New()
            .WithWindowAttributes(CreateWindowAttributes("Glutin OpenGL Example"))
            .Build(eventLoop, ConfigTemplateBuilder.New());

        IWindow window = createdWindow ?? throw new GlutinException("DisplayBuilder did not create a Winit window.");
        Display display = config.Display;

        _window = window;
        _config = config;
        _display = display;

        WindowSurfaceTarget target = window.BuildSurfaceTarget();
        _surface = display.CreateWindowSurface(config, target.Attributes);
        _context = display
            .CreateContext(config, ContextAttributesBuilder.New().Build(target.WindowHandle))
            .MakeCurrent(_surface);

        GL.Load(display.GetProcAddress);
        TryEnableVsync();

        Render();
        window.RequestRedraw();
    }

    private static WindowAttributes CreateWindowAttributes(string title)
    {
        return new WindowAttributes
        {
            Title = title,
            SurfaceSize = new LogicalSize<double>(900.0, 600.0),
            Position = new LogicalPosition<double>(120.0, 120.0),
            Visible = true,
        };
    }

    public void WindowEvent(IActiveEventLoop eventLoop, WindowId windowId, WindowEvent windowEvent)
    {
        if (_window is null || windowId != _window.Id)
        {
            return;
        }

        if (windowEvent.TryGetValue(out WindowEvent.CloseRequested _))
        {
            eventLoop.Exit();
            return;
        }

        if (windowEvent.TryGetValue(out WindowEvent.Destroyed _))
        {
            DisposeGl();
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
            // Some GLX/WGL stacks do not expose swap control.
        }
    }

    private void Render()
    {
        if (_window is null || _context is null || _surface is null)
        {
            return;
        }

        PhysicalSize<uint> size = _window.SurfaceSize;
        if (size.Width == 0 || size.Height == 0)
        {
            return;
        }

        _context.MakeCurrent(_surface);

        _phase += 0.0125f;
        float red = 0.12f + MathF.Sin(_phase) * 0.08f;
        float green = 0.18f + MathF.Sin(_phase + 2.1f) * 0.08f;
        float blue = 0.28f + MathF.Sin(_phase + 4.2f) * 0.08f;

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
