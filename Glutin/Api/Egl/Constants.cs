namespace Glutin.Backend.Egl;

internal static class EglConstants
{
    internal const int False = 0;
    internal const int True = 1;

    internal const int Success = 0x3000;
    internal const int NotInitialized = 0x3001;
    internal const int BadAccess = 0x3002;
    internal const int BadAlloc = 0x3003;
    internal const int BadAttribute = 0x3004;
    internal const int BadConfig = 0x3005;
    internal const int BadContext = 0x3006;
    internal const int BadCurrentSurface = 0x3007;
    internal const int BadDisplay = 0x3008;
    internal const int BadMatch = 0x3009;
    internal const int BadNativePixmap = 0x300A;
    internal const int BadNativeWindow = 0x300B;
    internal const int BadParameter = 0x300C;
    internal const int BadSurface = 0x300D;

    internal const int BufferSize = 0x3020;
    internal const int AlphaSize = 0x3021;
    internal const int BlueSize = 0x3022;
    internal const int GreenSize = 0x3023;
    internal const int RedSize = 0x3024;
    internal const int DepthSize = 0x3025;
    internal const int StencilSize = 0x3026;
    internal const int ConfigCaveat = 0x3027;
    internal const int ConfigId = 0x3028;
    internal const int Level = 0x3029;
    internal const int MaxPbufferHeight = 0x302A;
    internal const int MaxPbufferPixels = 0x302B;
    internal const int MaxPbufferWidth = 0x302C;
    internal const int NativeRenderable = 0x302D;
    internal const int NativeVisualId = 0x302E;
    internal const int NativeVisualType = 0x302F;
    internal const int Samples = 0x3031;
    internal const int SampleBuffers = 0x3032;
    internal const int SurfaceType = 0x3033;
    internal const int TransparentType = 0x3034;
    internal const int TransparentBlueValue = 0x3035;
    internal const int TransparentGreenValue = 0x3036;
    internal const int TransparentRedValue = 0x3037;
    internal const int None = 0x3038;
    internal const int BindToTextureRgb = 0x3039;
    internal const int BindToTextureRgba = 0x303A;
    internal const int MinSwapInterval = 0x303B;
    internal const int MaxSwapInterval = 0x303C;
    internal const int LuminanceSize = 0x303D;
    internal const int AlphaMaskSize = 0x303E;
    internal const int ColorBufferType = 0x303F;
    internal const int RenderableType = 0x3040;

    internal const int SlowConfig = 0x3050;
    internal const int NonConformantConfig = 0x3051;
    internal const int TransparentRgb = 0x3052;
    internal const int RgbBuffer = 0x308E;
    internal const int LuminanceBuffer = 0x308F;

    internal const int Vendor = 0x3053;
    internal const int Version = 0x3054;
    internal const int Extensions = 0x3055;
    internal const int ClientApis = 0x308D;

    internal const int Height = 0x3056;
    internal const int Width = 0x3057;
    internal const int LargestPbuffer = 0x3058;
    internal const int TextureFormat = 0x3080;
    internal const int TextureTarget = 0x3081;
    internal const int MipmapTexture = 0x3082;
    internal const int MipmapLevel = 0x3083;
    internal const int RenderBuffer = 0x3086;
    internal const int BackBuffer = 0x3084;
    internal const int SingleBuffer = 0x3085;

    internal const int PbufferBit = 0x0001;
    internal const int PixmapBit = 0x0002;
    internal const int WindowBit = 0x0004;
    internal const int VgColorspaceLinearBit = 0x0020;
    internal const int VgAlphaFormatPreBit = 0x0040;
    internal const int MultisampleResolveBoxBit = 0x0200;
    internal const int SwapBehaviorPreservedBit = 0x0400;

    internal const int OpenGlEsBit = 0x0001;
    internal const int OpenVgBit = 0x0002;
    internal const int OpenGlEs2Bit = 0x0004;
    internal const int OpenGlBit = 0x0008;
    internal const int OpenGlEs3Bit = 0x00000040;

    internal const uint OpenGlEsApi = 0x30A0;
    internal const uint OpenGlApi = 0x30A2;

    internal const int Draw = 0x3059;
    internal const int Read = 0x305A;

    internal const int ContextClientType = 0x3097;
    internal const int ContextClientVersion = 0x3098;
    internal const int ContextMajorVersion = 0x3098;
    internal const int ContextMinorVersion = 0x30FB;
    internal const int ContextFlagsKhr = 0x30FC;
    internal const int ContextOpenGlProfileMask = 0x30FD;
    internal const int ContextOpenGlCoreProfileBit = 0x00000001;
    internal const int ContextOpenGlCompatibilityProfileBit = 0x00000002;
    internal const int ContextOpenGlDebug = 0x31B0;
    internal const int ContextOpenGlDebugBitKhr = 0x00000001;
    internal const int ContextOpenGlRobustAccessBitKhr = 0x00000004;
    internal const int ContextOpenGlResetNotificationStrategy = 0x31BD;
    internal const int NoResetNotification = 0x31BE;
    internal const int LoseContextOnReset = 0x31BF;
    internal const int ContextOpenGlNoErrorKhr = 0x31B3;

    internal const int ContextPriorityLevelImg = 0x3100;
    internal const int ContextPriorityHighImg = 0x3101;
    internal const int ContextPriorityMediumImg = 0x3102;
    internal const int ContextPriorityLowImg = 0x3103;

    internal const int GlColorspace = 0x309D;
    internal const int GlColorspaceSrgb = 0x3089;
    internal const int GlColorspaceLinear = 0x308A;
    internal const int BufferAgeExt = 0x313D;

    internal const int ColorComponentTypeExt = 0x3339;
    internal const int ColorComponentTypeFloatExt = 0x333B;

    internal const uint PlatformX11Khr = 0x31D5;
    internal const int PlatformX11ScreenKhr = 0x31D6;
    internal const uint PlatformGbmKhr = 0x31D7;
    internal const uint PlatformWaylandKhr = 0x31D8;
    internal const uint PlatformAndroidKhr = 0x3141;

    internal const uint PlatformX11Ext = 0x31D5;
    internal const int PlatformX11ScreenExt = 0x31D6;
    internal const uint PlatformGbmMesa = 0x31D7;
    internal const uint PlatformWaylandExt = 0x31D8;
    internal const uint PlatformXcbExt = 0x31DC;
    internal const int PlatformXcbScreenExt = 0x31DE;
}
