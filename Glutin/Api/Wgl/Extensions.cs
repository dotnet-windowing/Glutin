#if WINDOWS
using System.Runtime.InteropServices;
using System.Text;

namespace Glutin.Backend.Wgl;

internal sealed unsafe class WglExtensions
{
    internal readonly delegate* unmanaged<nint, sbyte*> GetExtensionsStringARB;
    internal readonly delegate* unmanaged<sbyte*> GetExtensionsStringEXT;
    internal readonly delegate* unmanaged<nint, int*, float*, uint, int*, uint*, int> ChoosePixelFormatARB;
    internal readonly delegate* unmanaged<nint, int, int, uint, int*, int*, int> GetPixelFormatAttribivARB;
    internal readonly delegate* unmanaged<nint, nint, int*, nint> CreateContextAttribsARB;
    internal readonly delegate* unmanaged<int, int> SwapIntervalEXT;

    private WglExtensions(
        nint getExtensionsStringArb,
        nint getExtensionsStringExt,
        nint choosePixelFormatArb,
        nint getPixelFormatAttribivArb,
        nint createContextAttribsArb,
        nint swapIntervalExt)
    {
        GetExtensionsStringARB = (delegate* unmanaged<nint, sbyte*>)getExtensionsStringArb;
        GetExtensionsStringEXT = (delegate* unmanaged<sbyte*>)getExtensionsStringExt;
        ChoosePixelFormatARB = (delegate* unmanaged<nint, int*, float*, uint, int*, uint*, int>)choosePixelFormatArb;
        GetPixelFormatAttribivARB = (delegate* unmanaged<nint, int, int, uint, int*, int*, int>)getPixelFormatAttribivArb;
        CreateContextAttribsARB = (delegate* unmanaged<nint, nint, int*, nint>)createContextAttribsArb;
        SwapIntervalEXT = (delegate* unmanaged<int, int>)swapIntervalExt;
    }

    internal bool HasChoosePixelFormatARB => ChoosePixelFormatARB is not null;

    internal bool HasGetPixelFormatAttribivARB => GetPixelFormatAttribivARB is not null;

    internal bool HasCreateContextAttribsARB => CreateContextAttribsARB is not null;

    internal bool HasSwapIntervalEXT => SwapIntervalEXT is not null;

    internal static WglExtensions Load()
    {
        return new WglExtensions(
            LoadProc("wglGetExtensionsStringARB"),
            LoadProc("wglGetExtensionsStringEXT"),
            LoadProc("wglChoosePixelFormatARB"),
            LoadProc("wglGetPixelFormatAttribivARB"),
            LoadProc("wglCreateContextAttribsARB"),
            LoadProc("wglSwapIntervalEXT"));
    }

    internal HashSet<string> LoadExtensionSet(nint hdc)
    {
        sbyte* extensions = null;

        if (GetExtensionsStringARB is not null)
        {
            extensions = GetExtensionsStringARB(hdc);
        }
        else if (GetExtensionsStringEXT is not null)
        {
            extensions = GetExtensionsStringEXT();
        }

        if (extensions is null)
        {
            return [];
        }

        string? extensionString = Marshal.PtrToStringAnsi((nint)extensions);
        return extensionString is { Length: > 0 }
            ? extensionString.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal)
            : [];
    }

    private static nint LoadProc(string name)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(name + '\0');
        fixed (byte* ptr = bytes)
        {
            nint address = Ffi.wglGetProcAddress(ptr);
            return Ffi.IsInvalidProcAddress(address) ? 0 : address;
        }
    }
}
#endif
