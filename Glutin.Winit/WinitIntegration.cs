using Glutin;
using RawWindowHandles;
using Winit.Core;

namespace Glutin.Winit;

public readonly record struct WindowSurfaceTarget(
    RawDisplayHandle? DisplayHandle,
    RawWindowHandle WindowHandle,
    SurfaceAttributes<WindowSurface> Attributes);

public static class WinitWindowExtensions
{
    public static WindowSurfaceTarget BuildSurfaceTarget(this IWindow window)
    {
        RawWindowHandle windowHandle = window.WindowHandle
            ?? throw new GlutinException("The Winit window does not expose a raw window handle.");

        var size = window.SurfaceSize;
        return new WindowSurfaceTarget(
            window.DisplayHandle,
            windowHandle,
            SurfaceAttributesBuilder<WindowSurface>
                .New()
                .BuildWindow(windowHandle, size.Width, size.Height));
    }
}

public static class WinitEventLoopExtensions
{
    public static RawDisplayHandle? GetGlDisplayHandle(this IActiveEventLoop eventLoop)
    {
        return eventLoop.DisplayHandle;
    }
}
