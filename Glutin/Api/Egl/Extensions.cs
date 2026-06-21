namespace Glutin.Backend.Egl;

internal sealed unsafe class EglExtensions
{
    internal readonly delegate* unmanaged<uint, nint, nint*, nint> GetPlatformDisplay;
    internal readonly delegate* unmanaged<uint, nint, int*, nint> GetPlatformDisplayEXT;
    internal readonly delegate* unmanaged<nint, nint, nint, nint*, nint> CreatePlatformWindowSurface;
    internal readonly delegate* unmanaged<nint, nint, nint, int*, nint> CreatePlatformWindowSurfaceEXT;
    internal readonly delegate* unmanaged<nint, nint, nint, nint*, nint> CreatePlatformPixmapSurface;
    internal readonly delegate* unmanaged<nint, nint, nint, int*, nint> CreatePlatformPixmapSurfaceEXT;
    internal readonly delegate* unmanaged<nint, nint, Rect*, int, int> SwapBuffersWithDamageKHR;
    internal readonly delegate* unmanaged<nint, nint, Rect*, int, int> SwapBuffersWithDamageEXT;

    private EglExtensions(
        nint getPlatformDisplay,
        nint getPlatformDisplayExt,
        nint createPlatformWindowSurface,
        nint createPlatformWindowSurfaceExt,
        nint createPlatformPixmapSurface,
        nint createPlatformPixmapSurfaceExt,
        nint swapBuffersWithDamageKhr,
        nint swapBuffersWithDamageExt)
    {
        GetPlatformDisplay = (delegate* unmanaged<uint, nint, nint*, nint>)getPlatformDisplay;
        GetPlatformDisplayEXT = (delegate* unmanaged<uint, nint, int*, nint>)getPlatformDisplayExt;
        CreatePlatformWindowSurface =
            (delegate* unmanaged<nint, nint, nint, nint*, nint>)createPlatformWindowSurface;
        CreatePlatformWindowSurfaceEXT =
            (delegate* unmanaged<nint, nint, nint, int*, nint>)createPlatformWindowSurfaceExt;
        CreatePlatformPixmapSurface =
            (delegate* unmanaged<nint, nint, nint, nint*, nint>)createPlatformPixmapSurface;
        CreatePlatformPixmapSurfaceEXT =
            (delegate* unmanaged<nint, nint, nint, int*, nint>)createPlatformPixmapSurfaceExt;
        SwapBuffersWithDamageKHR = (delegate* unmanaged<nint, nint, Rect*, int, int>)swapBuffersWithDamageKhr;
        SwapBuffersWithDamageEXT = (delegate* unmanaged<nint, nint, Rect*, int, int>)swapBuffersWithDamageExt;
    }

    internal bool HasGetPlatformDisplay => GetPlatformDisplay is not null;

    internal bool HasGetPlatformDisplayEXT => GetPlatformDisplayEXT is not null;

    internal bool HasCreatePlatformWindowSurface => CreatePlatformWindowSurface is not null;

    internal bool HasCreatePlatformWindowSurfaceEXT => CreatePlatformWindowSurfaceEXT is not null;

    internal bool HasCreatePlatformPixmapSurface => CreatePlatformPixmapSurface is not null;

    internal bool HasCreatePlatformPixmapSurfaceEXT => CreatePlatformPixmapSurfaceEXT is not null;

    internal bool HasSwapBuffersWithDamageKHR => SwapBuffersWithDamageKHR is not null;

    internal bool HasSwapBuffersWithDamageEXT => SwapBuffersWithDamageEXT is not null;

    internal static EglExtensions Load()
    {
        return new EglExtensions(
            Ffi.LoadProc("eglGetPlatformDisplay"),
            Ffi.LoadProc("eglGetPlatformDisplayEXT"),
            Ffi.LoadProc("eglCreatePlatformWindowSurface"),
            Ffi.LoadProc("eglCreatePlatformWindowSurfaceEXT"),
            Ffi.LoadProc("eglCreatePlatformPixmapSurface"),
            Ffi.LoadProc("eglCreatePlatformPixmapSurfaceEXT"),
            Ffi.LoadProc("eglSwapBuffersWithDamageKHR"),
            Ffi.LoadProc("eglSwapBuffersWithDamageEXT"));
    }
}
