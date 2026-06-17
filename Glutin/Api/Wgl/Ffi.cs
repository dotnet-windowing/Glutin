#if WINDOWS
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Glutin.Backend.Wgl;

internal static unsafe partial class Ffi
{
    internal const uint PfdDoubleBuffer = 0x00000001;
    internal const uint PfdStereo = 0x00000002;
    internal const uint PfdDrawToWindow = 0x00000004;
    internal const uint PfdDrawToBitmap = 0x00000008;
    internal const uint PfdSupportOpenGl = 0x00000020;
    internal const uint PfdGenericFormat = 0x00000040;
    internal const uint PfdGenericAccelerated = 0x00001000;
    internal const uint PfdStereoDontCare = 0x80000000;

    internal const byte PfdTypeRgba = 0;
    internal const byte PfdMainPlane = 0;

    internal const uint WsPopup = 0x80000000;
    internal const uint WsClipChildren = 0x02000000;
    internal const uint WsClipSiblings = 0x04000000;

    internal const int ErrorClassAlreadyExists = 1410;

    [DllImport("kernel32.dll", EntryPoint = "LoadLibraryW", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint LoadLibrary(string fileName);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool FreeLibrary(nint module);

    [DllImport("kernel32.dll", EntryPoint = "GetProcAddress", SetLastError = true)]
    internal static extern nint GetProcAddress(nint module, byte* procName);

    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", SetLastError = true)]
    internal static extern nint GetModuleHandle(nint moduleName);

    [DllImport("user32.dll", EntryPoint = "RegisterClassExW", SetLastError = true)]
    internal static extern ushort RegisterClassEx(WNDCLASSEXW* windowClass);

    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true)]
    internal static extern nint CreateWindowEx(
        uint exStyle,
        char* className,
        char* windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        nint parent,
        nint menu,
        nint instance,
        void* param);

    [DllImport("user32.dll", EntryPoint = "DestroyWindow", SetLastError = true)]
    internal static extern bool DestroyWindow(nint hwnd);

    [DllImport("user32.dll", EntryPoint = "DefWindowProcW")]
    internal static extern nint DefWindowProc(nint hwnd, uint message, nuint wParam, nint lParam);

    [DllImport("user32.dll", EntryPoint = "GetClientRect", SetLastError = true)]
    internal static extern bool GetClientRect(nint hwnd, out RECT rect);

    [DllImport("user32.dll", EntryPoint = "GetDC", SetLastError = true)]
    internal static extern nint GetDC(nint hwnd);

    [DllImport("user32.dll", EntryPoint = "ReleaseDC", SetLastError = true)]
    internal static extern int ReleaseDC(nint hwnd, nint hdc);

    [DllImport("gdi32.dll", EntryPoint = "ChoosePixelFormat", SetLastError = true)]
    internal static extern int ChoosePixelFormat(nint hdc, PIXELFORMATDESCRIPTOR* descriptor);

    [DllImport("gdi32.dll", EntryPoint = "DescribePixelFormat", SetLastError = true)]
    internal static extern int DescribePixelFormat(
        nint hdc,
        int pixelFormat,
        uint bytes,
        PIXELFORMATDESCRIPTOR* descriptor);

    [DllImport("gdi32.dll", EntryPoint = "SetPixelFormat", SetLastError = true)]
    internal static extern bool SetPixelFormat(
        nint hdc,
        int pixelFormat,
        PIXELFORMATDESCRIPTOR* descriptor);

    [DllImport("gdi32.dll", EntryPoint = "GetPixelFormat", SetLastError = true)]
    internal static extern int GetPixelFormat(nint hdc);

    [DllImport("gdi32.dll", EntryPoint = "SwapBuffers", SetLastError = true)]
    internal static extern bool SwapBuffers(nint hdc);

    [DllImport("opengl32.dll", EntryPoint = "wglCreateContext", SetLastError = true)]
    internal static extern nint wglCreateContext(nint hdc);

    [DllImport("opengl32.dll", EntryPoint = "wglMakeCurrent", SetLastError = true)]
    internal static extern bool wglMakeCurrent(nint hdc, nint context);

    [DllImport("opengl32.dll", EntryPoint = "wglDeleteContext", SetLastError = true)]
    internal static extern bool wglDeleteContext(nint context);

    [DllImport("opengl32.dll", EntryPoint = "wglGetProcAddress", SetLastError = true)]
    internal static extern nint wglGetProcAddress(byte* procName);

    [DllImport("opengl32.dll", EntryPoint = "wglGetCurrentContext")]
    internal static extern nint wglGetCurrentContext();

    [DllImport("opengl32.dll", EntryPoint = "wglGetCurrentDC")]
    internal static extern nint wglGetCurrentDC();

    [DllImport("opengl32.dll", EntryPoint = "wglShareLists", SetLastError = true)]
    internal static extern bool wglShareLists(nint share, nint context);

    internal static GlutinException LastError(string operation)
    {
        return new GlutinException($"{operation} failed: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
    }

    internal static nint CurrentModuleHandle()
    {
        nint module = GetModuleHandle(0);
        if (module == 0)
        {
            throw LastError("GetModuleHandleW");
        }

        return module;
    }

    internal static bool IsInvalidProcAddress(nint address)
    {
        return address == 0 || address == 1 || address == 2 || address == 3 || address == -1;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    internal static nint DummyWndProc(nint hwnd, uint message, nuint wParam, nint lParam)
    {
        return DefWindowProc(hwnd, message, wParam, lParam);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WNDCLASSEXW
    {
        public uint cbSize;
        public uint style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        public char* lpszMenuName;
        public char* lpszClassName;
        public nint hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PIXELFORMATDESCRIPTOR
    {
        public ushort nSize;
        public ushort nVersion;
        public uint dwFlags;
        public byte iPixelType;
        public byte cColorBits;
        public byte cRedBits;
        public byte cRedShift;
        public byte cGreenBits;
        public byte cGreenShift;
        public byte cBlueBits;
        public byte cBlueShift;
        public byte cAlphaBits;
        public byte cAlphaShift;
        public byte cAccumBits;
        public byte cAccumRedBits;
        public byte cAccumGreenBits;
        public byte cAccumBlueBits;
        public byte cAccumAlphaBits;
        public byte cDepthBits;
        public byte cStencilBits;
        public byte cAuxBuffers;
        public byte iLayerType;
        public byte bReserved;
        public uint dwLayerMask;
        public uint dwVisibleMask;
        public uint dwDamageMask;

        public static PIXELFORMATDESCRIPTOR Create()
        {
            return new PIXELFORMATDESCRIPTOR
            {
                nSize = (ushort)sizeof(PIXELFORMATDESCRIPTOR),
                nVersion = 1,
                iPixelType = PfdTypeRgba,
                iLayerType = PfdMainPlane,
            };
        }
    }
}
#endif
