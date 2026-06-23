#if !ANDROID
namespace Glutin.Backend.Glx;

internal static class GlxConstants
{
    internal const int None = 0x8000;
    internal const int SlowConfig = 0x8001;
    internal const int TrueColor = 0x8002;
    internal const int GrayScale = 0x8006;
    internal const int VisualId = 0x800B;
    internal const int Screen = 0x800C;
    internal const int SampleBuffers = 100000;
    internal const int Samples = 100001;

    internal const int UseGl = 1;
    internal const int BufferSize = 2;
    internal const int Level = 3;
    internal const int Rgba = 4;
    internal const int DoubleBuffer = 5;
    internal const int Stereo = 6;
    internal const int RedSize = 8;
    internal const int GreenSize = 9;
    internal const int BlueSize = 10;
    internal const int AlphaSize = 11;
    internal const int DepthSize = 12;
    internal const int StencilSize = 13;
    internal const int AccumRedSize = 14;
    internal const int AccumGreenSize = 15;
    internal const int AccumBlueSize = 16;
    internal const int AccumAlphaSize = 17;
    internal const int ConfigCaveat = 0x20;
    internal const int XVisualType = 0x22;
    internal const int TransparentType = 0x23;
    internal const int TransparentIndexValue = 0x24;
    internal const int TransparentRedValue = 0x25;
    internal const int TransparentGreenValue = 0x26;
    internal const int TransparentBlueValue = 0x27;
    internal const int TransparentAlphaValue = 0x28;
    internal const int DrawableType = 0x8010;
    internal const int RenderType = 0x8011;
    internal const int XRenderable = 0x8012;
    internal const int FbConfigId = 0x8013;
    internal const int RgbaType = 0x8014;
    internal const int ColorIndexType = 0x8015;
    internal const int MaxPbufferWidth = 0x8016;
    internal const int MaxPbufferHeight = 0x8017;
    internal const int MaxPbufferPixels = 0x8018;
    internal const int PreservedContents = 0x801B;
    internal const int LargestPbuffer = 0x801C;
    internal const int Width = 0x801D;
    internal const int Height = 0x801E;
    internal const int PbufferWidth = 0x8041;
    internal const int PbufferHeight = 0x8040;

    internal const int WindowBit = 0x00000001;
    internal const int PixmapBit = 0x00000002;
    internal const int PbufferBit = 0x00000004;
    internal const int RgbaBit = 0x00000001;

    internal const int Extensions = 0x3;

    internal const int RgbaFloatBitArb = 0x00000004;
    internal const int RgbaFloatTypeArb = 0x20B9;
    internal const int FramebufferSrgbCapableArb = 0x20B2;
    internal const int FramebufferSrgbCapableExt = 0x20B2;
    internal const int BackBufferAgeExt = 0x20F4;

    internal const int ContextMajorVersionArb = 0x2091;
    internal const int ContextMinorVersionArb = 0x2092;
    internal const int ContextFlagsArb = 0x2094;
    internal const int ContextProfileMaskArb = 0x9126;
    internal const int ContextDebugBitArb = 0x0001;
    internal const int ContextForwardCompatibleBitArb = 0x0002;
    internal const int ContextRobustAccessBitArb = 0x0004;
    internal const int ContextCoreProfileBitArb = 0x00000001;
    internal const int ContextCompatibilityProfileBitArb = 0x00000002;
    internal const int ContextEs2ProfileBitExt = 0x00000004;
    internal const int ContextResetNotificationStrategyArb = 0x8256;
    internal const int NoResetNotificationArb = 0x8261;
    internal const int LoseContextOnResetArb = 0x8252;
    internal const int ContextReleaseBehaviorArb = 0x2097;
    internal const int ContextReleaseBehaviorNoneArb = 0;
    internal const int ContextOpenGlNoErrorArb = 0x31B3;
}
#endif
