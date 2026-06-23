#if !ANDROID
namespace Glutin.Platform.X11;

public static class GlxConfigExtX11
{
    public static uint? GetX11VisualId(this Config config)
    {
        if (config.Backend is Backend.Glx.Config glxConfig)
        {
            return glxConfig.X11VisualId;
        }

        if (config.Backend is Backend.Egl.Config eglConfig)
        {
            uint visualId = eglConfig.NativeVisualId;
            return visualId != 0 ? visualId : null;
        }

        return null;
    }
}
#endif
