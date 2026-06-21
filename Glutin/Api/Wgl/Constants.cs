#if WINDOWS
namespace Glutin.Backend.Wgl;

internal static class WglConstants
{
    internal const int DrawToWindowArb = 0x2001;
    internal const int AccelerationArb = 0x2003;
    internal const int TransparentArb = 0x200A;
    internal const int SupportOpenGlArb = 0x2010;
    internal const int DoubleBufferArb = 0x2011;
    internal const int StereoArb = 0x2012;
    internal const int PixelTypeArb = 0x2013;
    internal const int RedBitsArb = 0x2015;
    internal const int GreenBitsArb = 0x2017;
    internal const int BlueBitsArb = 0x2019;
    internal const int AlphaBitsArb = 0x201B;
    internal const int DepthBitsArb = 0x2022;
    internal const int StencilBitsArb = 0x2023;
    internal const int NoAccelerationArb = 0x2025;
    internal const int FullAccelerationArb = 0x2027;
    internal const int TypeRgbaArb = 0x202B;
    internal const int DrawToPbufferArb = 0x202D;
    internal const int MaxPbufferWidthArb = 0x202F;
    internal const int MaxPbufferHeightArb = 0x2030;
    internal const int PbufferLargestArb = 0x2033;
    internal const int PbufferWidthArb = 0x2034;
    internal const int PbufferHeightArb = 0x2035;
    internal const int SampleBuffersArb = 0x2041;
    internal const int SamplesArb = 0x2042;
    internal const int FramebufferSrgbCapableArb = 0x20A9;
    internal const int TypeRgbaFloatArb = 0x21A0;

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
