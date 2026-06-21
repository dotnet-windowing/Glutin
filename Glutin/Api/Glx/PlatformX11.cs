#if !WINDOWS
namespace Glutin.Platform.X11;

public static class GlxConfigExtX11
{
    public static uint? GetX11VisualId(this Config config)
    {
        return config.Backend is Backend.Glx.Config glxConfig
            ? glxConfig.X11VisualId
            : null;
    }
}
#endif
