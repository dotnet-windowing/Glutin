#if !ANDROID
using System.Runtime.InteropServices;
using System.Text;

namespace Glutin.Backend.Glx;

internal sealed unsafe class GlxExtensions
{
    internal readonly delegate* unmanaged<nint, nint, nint, int, int*, nint> CreateContextAttribsARB;
    internal readonly delegate* unmanaged<nint, nuint, int, void> SwapIntervalEXT;
    internal readonly delegate* unmanaged<uint, int> SwapIntervalMESA;
    internal readonly delegate* unmanaged<int, int> SwapIntervalSGI;

    private GlxExtensions(
        nint createContextAttribsArb,
        nint swapIntervalExt,
        nint swapIntervalMesa,
        nint swapIntervalSgi)
    {
        CreateContextAttribsARB = (delegate* unmanaged<nint, nint, nint, int, int*, nint>)createContextAttribsArb;
        SwapIntervalEXT = (delegate* unmanaged<nint, nuint, int, void>)swapIntervalExt;
        SwapIntervalMESA = (delegate* unmanaged<uint, int>)swapIntervalMesa;
        SwapIntervalSGI = (delegate* unmanaged<int, int>)swapIntervalSgi;
    }

    internal bool HasCreateContextAttribsARB => CreateContextAttribsARB is not null;

    internal bool HasSwapIntervalEXT => SwapIntervalEXT is not null;

    internal bool HasSwapIntervalMESA => SwapIntervalMESA is not null;

    internal bool HasSwapIntervalSGI => SwapIntervalSGI is not null;

    internal static GlxExtensions Load()
    {
        return new GlxExtensions(
            LoadProc("glXCreateContextAttribsARB"),
            LoadProc("glXSwapIntervalEXT"),
            LoadProc("glXSwapIntervalMESA"),
            LoadProc("glXSwapIntervalSGI"));
    }

    private static nint LoadProc(string name)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(name + '\0');
        fixed (byte* ptr = bytes)
        {
            return Ffi.glXGetProcAddress(ptr);
        }
    }
}
#endif
