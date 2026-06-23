using System.Reflection;
using System.Runtime.InteropServices;

namespace Glutin;

internal static class NativeLibraryResolver
{
    private static int s_registered;

    internal static void EnsureRegistered()
    {
        if (Interlocked.Exchange(ref s_registered, 1) != 0)
        {
            return;
        }

        NativeLibrary.SetDllImportResolver(typeof(NativeLibraryResolver).Assembly, Resolve);
    }

    private static nint Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        return libraryName switch
        {
            "glutin-egl" => LoadFirst(EglLibraryNames(), assembly, searchPath),
            "glutin-wayland-egl" => LoadFirst(WaylandEglLibraryNames(), assembly, searchPath),
            _ => 0,
        };
    }

    private static IEnumerable<string> EglLibraryNames()
    {
        if (OperatingSystem.IsWindows())
        {
            yield return "libEGL.dll";
            yield return "EGL.dll";
            yield break;
        }

        if (OperatingSystem.IsAndroid())
        {
            yield return "libEGL.so";
            yield break;
        }

        yield return "libEGL.so.1";
        yield return "libEGL.so";
    }

    private static IEnumerable<string> WaylandEglLibraryNames()
    {
        if (OperatingSystem.IsAndroid())
        {
            yield return "libwayland-egl.so";
            yield break;
        }

        yield return "libwayland-egl.so.1";
        yield return "libwayland-egl.so";
    }

    private static nint LoadFirst(
        IEnumerable<string> names,
        Assembly assembly,
        DllImportSearchPath? searchPath)
    {
        foreach (string name in names)
        {
            if (NativeLibrary.TryLoad(name, assembly, searchPath, out nint handle))
            {
                return handle;
            }
        }

        return 0;
    }
}
