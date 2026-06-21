#if !WINDOWS
using GlutinConfig = Glutin.Config;
using GlutinDisplay = Glutin.Display;
using GlutinNotCurrentContext = Glutin.NotCurrentContext;
using GlutinPossiblyCurrentContext = Glutin.PossiblyCurrentContext;

namespace Glutin.Backend.Glx;

public sealed class NotCurrentContext : IPlatformNotCurrentGlContext
{
    private readonly ContextInner _inner;

    internal NotCurrentContext(ContextInner inner)
    {
        _inner = inner;
    }

    public ContextApi ContextApi => _inner.ContextApi;

    public Priority Priority => Priority.Medium;

    public GlutinDisplay Display => _inner.Display.Facade;

    public GlutinConfig Config => new(_inner.Config);

    public RawContext RawContext => new(new RawContext.Glx(_inner.Raw));

    public GlutinPossiblyCurrentContext TreatAsPossiblyCurrent()
    {
        return new GlutinPossiblyCurrentContext(new PossiblyCurrentContext(_inner));
    }

    public GlutinPossiblyCurrentContext MakeCurrent<TSurface>(Glutin.Surface<TSurface> surface)
        where TSurface : struct, ISurfaceType
    {
        if (surface.Backend is not Surface<TSurface> glxSurface)
        {
            throw new GlutinException("GLX context received a surface from another backend.");
        }

        _inner.MakeCurrentDrawRead(glxSurface.Raw, glxSurface.Raw);
        return new GlutinPossiblyCurrentContext(new PossiblyCurrentContext(_inner));
    }

    public GlutinPossiblyCurrentContext MakeCurrentDrawRead<TSurface>(
        Glutin.Surface<TSurface> surfaceDraw,
        Glutin.Surface<TSurface> surfaceRead)
        where TSurface : struct, ISurfaceType
    {
        if (surfaceDraw.Backend is not Surface<TSurface> glxDraw
            || surfaceRead.Backend is not Surface<TSurface> glxRead)
        {
            throw new GlutinException("GLX context received a surface from another backend.");
        }

        _inner.MakeCurrentDrawRead(glxDraw.Raw, glxRead.Raw);
        return new GlutinPossiblyCurrentContext(new PossiblyCurrentContext(_inner));
    }

    public GlutinPossiblyCurrentContext MakeCurrentSurfaceless()
    {
        _inner.MakeCurrentSurfaceless();
        return new GlutinPossiblyCurrentContext(new PossiblyCurrentContext(_inner));
    }

    public void Dispose()
    {
        _inner.Dispose();
    }
}

public sealed class PossiblyCurrentContext : IPlatformPossiblyCurrentGlContext
{
    private readonly ContextInner _inner;

    internal PossiblyCurrentContext(ContextInner inner)
    {
        _inner = inner;
    }

    internal nint Raw => _inner.Raw;

    public ContextApi ContextApi => _inner.ContextApi;

    public Priority Priority => Priority.Medium;

    public GlutinDisplay Display => _inner.Display.Facade;

    public GlutinConfig Config => new(_inner.Config);

    public RawContext RawContext => new(new RawContext.Glx(_inner.Raw));

    public bool IsCurrent => Ffi.glXGetCurrentContext() == _inner.Raw;

    public GlutinNotCurrentContext MakeNotCurrent()
    {
        MakeNotCurrentInPlace();
        return new GlutinNotCurrentContext(new NotCurrentContext(_inner));
    }

    public void MakeNotCurrentInPlace()
    {
        _inner.MakeNotCurrent();
    }

    public void MakeCurrent<TSurface>(Glutin.Surface<TSurface> surface)
        where TSurface : struct, ISurfaceType
    {
        if (surface.Backend is not Surface<TSurface> glxSurface)
        {
            throw new GlutinException("GLX context received a surface from another backend.");
        }

        _inner.MakeCurrentDrawRead(glxSurface.Raw, glxSurface.Raw);
    }

    public void MakeCurrentDrawRead<TSurface>(
        Glutin.Surface<TSurface> surfaceDraw,
        Glutin.Surface<TSurface> surfaceRead)
        where TSurface : struct, ISurfaceType
    {
        if (surfaceDraw.Backend is not Surface<TSurface> glxDraw
            || surfaceRead.Backend is not Surface<TSurface> glxRead)
        {
            throw new GlutinException("GLX context received a surface from another backend.");
        }

        _inner.MakeCurrentDrawRead(glxDraw.Raw, glxRead.Raw);
    }

    public void MakeCurrentSurfaceless()
    {
        _inner.MakeCurrentSurfaceless();
    }

    public void Dispose()
    {
        _inner.Dispose();
    }
}

internal sealed unsafe class ContextInner : IDisposable
{
    private bool _disposed;

    private ContextInner(
        Display display,
        Config config,
        nint raw,
        bool isGles,
        bool supportsSurfaceless)
    {
        Display = display;
        Config = config;
        Raw = raw;
        IsGles = isGles;
        SupportsSurfaceless = supportsSurfaceless;
    }

    internal Display Display { get; }

    internal Config Config { get; }

    internal nint Raw { get; private set; }

    internal bool IsGles { get; }

    internal bool SupportsSurfaceless { get; }

    internal ContextApi ContextApi => IsGles ? ContextApi.FromGles() : ContextApi.FromOpenGl();

    internal static ContextInner Create(
        Display display,
        Config config,
        ContextAttributes contextAttributes)
    {
        nint shareContext = 0;
        if (contextAttributes.SharedContext is { } shared)
        {
            if (!shared.TryGetValue(out RawContext.Glx sharedGlx))
            {
                throw new GlutinException("incompatible context was passed to GLX");
            }

            shareContext = sharedGlx.Context;
        }

        bool isGles = contextAttributes.Api is { } api && api.TryGetValue(out ContextApi.Gles _);
        (nint raw, bool supportsSurfaceless) =
            display.Extensions.Contains("GLX_ARB_create_context")
            && display.GlxExtra.HasCreateContextAttribsARB
                ? CreateContextArb(display, config, shareContext, contextAttributes)
                : CreateLegacyContext(display, config, shareContext);

        if (raw == 0)
        {
            throw new GlutinException("GLX failed to create a context.");
        }

        return new ContextInner(display, config, raw, isGles, supportsSurfaceless);
    }

    internal void MakeCurrentDrawRead(nuint draw, nuint read)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (Ffi.glXMakeContextCurrent(Display.Raw, draw, read, Raw) == 0)
        {
            throw new GlutinException("glXMakeContextCurrent failed.");
        }
    }

    internal void MakeCurrentSurfaceless()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!SupportsSurfaceless)
        {
            throw new GlutinException("the surfaceless context API is not supported by this GLX context");
        }

        if (Ffi.glXMakeContextCurrent(Display.Raw, 0, 0, Raw) == 0)
        {
            throw new GlutinException("glXMakeContextCurrent failed.");
        }
    }

    internal void MakeNotCurrent()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (Ffi.glXMakeContextCurrent(Display.Raw, 0, 0, 0) == 0)
        {
            throw new GlutinException("glXMakeContextCurrent failed.");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (Raw == 0)
        {
            return;
        }

        if (Ffi.glXGetCurrentContext() == Raw)
        {
            _ = Ffi.glXMakeContextCurrent(Display.Raw, 0, 0, 0);
        }

        Ffi.glXDestroyContext(Display.Raw, Raw);
        Raw = 0;
    }

    private static (nint Raw, bool SupportsSurfaceless) CreateLegacyContext(
        Display display,
        Config config,
        nint shareContext)
    {
        int renderType = config.FloatPixels ? GlxConstants.RgbaFloatTypeArb : GlxConstants.RgbaType;
        nint raw = Ffi.glXCreateNewContext(display.Raw, config.Raw, renderType, shareContext, 1);
        return (raw, SupportsSurfaceless: false);
    }

    private static (nint Raw, bool SupportsSurfaceless) CreateContextArb(
        Display display,
        Config config,
        nint shareContext,
        ContextAttributes contextAttributes)
    {
        List<int> attrs = BuildContextAttributes(display, contextAttributes, out bool supportsSurfaceless);
        nint raw;
        fixed (int* attrsPtr = attrs.ToArray())
        {
            raw = display.GlxExtra.CreateContextAttribsARB(display.Raw, config.Raw, shareContext, 1, attrsPtr);
        }

        return (raw, supportsSurfaceless);
    }

    private static List<int> BuildContextAttributes(
        Display display,
        ContextAttributes attributes,
        out bool supportsSurfaceless)
    {
        var attrs = new List<int>();
        ContextApi api = attributes.Api ?? ContextApi.FromOpenGl();
        supportsSurfaceless = false;

        if (api.TryGetValue(out ContextApi.Gles gles))
        {
            if (!display.Features.HasFlag(DisplayFeatures.CreateEsContext))
            {
                throw new GlutinException("extension to create ES context with GLX is not present");
            }

            GlVersion version = gles.Version ?? new GlVersion(2, 0);
            attrs.Add(GlxConstants.ContextProfileMaskArb);
            attrs.Add(GlxConstants.ContextEs2ProfileBitExt);
            attrs.Add(GlxConstants.ContextMajorVersionArb);
            attrs.Add(version.Major);
            attrs.Add(GlxConstants.ContextMinorVersionArb);
            attrs.Add(version.Minor);
        }
        else
        {
            api.TryGetValue(out ContextApi.OpenGl openGl);
            GlVersion? version = openGl.Version;
            GlProfile? profile = attributes.Profile;

            if (profile == GlProfile.Core && version is null)
            {
                version = new GlVersion(3, 3);
            }

            if (version is { } requestedVersion)
            {
                attrs.Add(GlxConstants.ContextMajorVersionArb);
                attrs.Add(requestedVersion.Major);
                attrs.Add(GlxConstants.ContextMinorVersionArb);
                attrs.Add(requestedVersion.Minor);
                supportsSurfaceless = requestedVersion.CompareTo(new GlVersion(3, 0)) >= 0;
            }

            if (profile is { } requestedProfile)
            {
                attrs.Add(GlxConstants.ContextProfileMaskArb);
                attrs.Add(requestedProfile == GlProfile.Core
                    ? GlxConstants.ContextCoreProfileBitArb
                    : GlxConstants.ContextCompatibilityProfileBitArb);
            }
        }

        int flags = 0;
        bool noError = false;

        if (attributes.Robustness != Robustness.NotRobust)
        {
            if (!display.Features.HasFlag(DisplayFeatures.ContextRobustness))
            {
                throw new GlutinException("GLX_ARB_create_context_robustness is not supported");
            }

            switch (attributes.Robustness)
            {
                case Robustness.RobustNoResetNotification:
                    attrs.Add(GlxConstants.ContextResetNotificationStrategyArb);
                    attrs.Add(GlxConstants.NoResetNotificationArb);
                    flags |= GlxConstants.ContextRobustAccessBitArb;
                    break;
                case Robustness.RobustLoseContextOnReset:
                    attrs.Add(GlxConstants.ContextResetNotificationStrategyArb);
                    attrs.Add(GlxConstants.LoseContextOnResetArb);
                    flags |= GlxConstants.ContextRobustAccessBitArb;
                    break;
                case Robustness.NoError:
                    if (!display.Features.HasFlag(DisplayFeatures.ContextNoError))
                    {
                        throw new GlutinException("GLX_ARB_create_context_no_error is not supported");
                    }

                    attrs.Add(GlxConstants.ContextOpenGlNoErrorArb);
                    attrs.Add(1);
                    noError = true;
                    break;
            }
        }

        if (attributes.Debug && !noError)
        {
            flags |= GlxConstants.ContextDebugBitArb;
        }

        if (flags != 0)
        {
            attrs.Add(GlxConstants.ContextFlagsArb);
            attrs.Add(flags);
        }

        if (attributes.ReleaseBehavior == ReleaseBehavior.None)
        {
            if (!display.Features.HasFlag(DisplayFeatures.ContextReleaseBehavior))
            {
                throw new GlutinException("GLX_ARB_context_flush_control is not supported");
            }

            attrs.Add(GlxConstants.ContextReleaseBehaviorArb);
            attrs.Add(GlxConstants.ContextReleaseBehaviorNoneArb);
        }

        attrs.Add(0);
        return attrs;
    }
}
#endif
