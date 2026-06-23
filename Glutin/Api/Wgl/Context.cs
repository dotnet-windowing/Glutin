#if !ANDROID
using RawWindowHandles;
using GlutinConfig = Glutin.Config;
using GlutinDisplay = Glutin.Display;
using GlutinNotCurrentContext = Glutin.NotCurrentContext;
using GlutinPossiblyCurrentContext = Glutin.PossiblyCurrentContext;
using GlutinSurfaceWindow = Glutin.Surface<Glutin.WindowSurface>;

namespace Glutin.Backend.Wgl;

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

    public RawContext RawContext => new(new RawContext.Wgl(_inner.Raw));

    public GlutinPossiblyCurrentContext TreatAsPossiblyCurrent()
    {
        return new GlutinPossiblyCurrentContext(new PossiblyCurrentContext(_inner));
    }

    public GlutinPossiblyCurrentContext MakeCurrent<TSurface>(Glutin.Surface<TSurface> surface)
        where TSurface : struct, ISurfaceType
    {
        if (surface.Backend is not Surface<TSurface> wglSurface)
        {
            throw new GlutinException("WGL context received a surface from another backend.");
        }

        _inner.MakeCurrent(wglSurface.Hdc);
        return new GlutinPossiblyCurrentContext(new PossiblyCurrentContext(_inner));
    }

    public GlutinPossiblyCurrentContext MakeCurrentDrawRead<TSurface>(
        Glutin.Surface<TSurface> surfaceDraw,
        Glutin.Surface<TSurface> surfaceRead)
        where TSurface : struct, ISurfaceType
    {
        throw new GlutinException("make_current_draw_read is not supported by WGL");
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

    public RawContext RawContext => new(new RawContext.Wgl(_inner.Raw));

    public bool IsCurrent => Ffi.wglGetCurrentContext() == _inner.Raw;

    public GlutinNotCurrentContext MakeNotCurrent()
    {
        MakeNotCurrentInPlace();
        return new GlutinNotCurrentContext(new NotCurrentContext(_inner));
    }

    public void MakeNotCurrentInPlace()
    {
        if (!IsCurrent)
        {
            return;
        }

        nint currentDc = Ffi.wglGetCurrentDC();
        if (!Ffi.wglMakeCurrent(currentDc, 0))
        {
            throw Ffi.LastError("wglMakeCurrent");
        }
    }

    public void MakeCurrent<TSurface>(Glutin.Surface<TSurface> surface)
        where TSurface : struct, ISurfaceType
    {
        if (surface.Backend is not Surface<TSurface> wglSurface)
        {
            throw new GlutinException("WGL context received a surface from another backend.");
        }

        _inner.MakeCurrent(wglSurface.Hdc);
    }

    public void MakeCurrentDrawRead<TSurface>(
        Glutin.Surface<TSurface> surfaceDraw,
        Glutin.Surface<TSurface> surfaceRead)
        where TSurface : struct, ISurfaceType
    {
        throw new GlutinException("make_current_draw_read is not supported by WGL");
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
        nint hdc = config.Hdc;
        bool releaseHdc = false;
        nint hwnd = 0;

        if (contextAttributes.RawWindowHandle is { } rawWindow)
        {
            if (!rawWindow.TryGetValue(out RawWindowHandle.Win32 win32))
            {
                throw new GlutinException("provided native window is not supported by WGL");
            }

            config.ApplyOnNativeWindow(rawWindow);
            hwnd = win32.Hwnd;
            hdc = Ffi.GetDC(hwnd);
            if (hdc == 0)
            {
                throw Ffi.LastError("GetDC");
            }

            releaseHdc = true;
        }

        try
        {
            nint shareContext = 0;
            if (contextAttributes.SharedContext is { } shared
                && shared.TryGetValue(out RawContext.Wgl sharedWgl))
            {
                shareContext = sharedWgl.Context;
            }

            bool isGles = contextAttributes.Api is { } api && api.TryGetValue(out ContextApi.Gles _);
            (nint raw, bool supportsSurfaceless) =
                display.Extensions.Contains("WGL_ARB_create_context")
                && display.Wgl is { HasCreateContextAttribsARB: true }
                    ? CreateContextArb(display, hdc, shareContext, contextAttributes)
                    : CreateLegacyContext(hdc, shareContext);

            return new ContextInner(display, config, raw, isGles, supportsSurfaceless);
        }
        finally
        {
            if (releaseHdc)
            {
                Ffi.ReleaseDC(hwnd, hdc);
            }
        }
    }

    internal void MakeCurrent(nint hdc)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!Ffi.wglMakeCurrent(hdc, Raw))
        {
            throw Ffi.LastError("wglMakeCurrent");
        }
    }

    internal void MakeCurrentSurfaceless()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!SupportsSurfaceless)
        {
            throw new GlutinException("the surfaceless context API is not supported by this WGL context");
        }

        if (!Ffi.wglMakeCurrent(0, Raw))
        {
            throw Ffi.LastError("wglMakeCurrent");
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

        if (Ffi.wglGetCurrentContext() == Raw)
        {
            Ffi.wglMakeCurrent(Ffi.wglGetCurrentDC(), 0);
        }

        Ffi.wglDeleteContext(Raw);
        Raw = 0;
    }

    private static (nint Raw, bool SupportsSurfaceless) CreateLegacyContext(nint hdc, nint shareContext)
    {
        nint raw = Ffi.wglCreateContext(hdc);
        if (raw == 0)
        {
            throw Ffi.LastError("wglCreateContext");
        }

        if (shareContext != 0 && !Ffi.wglShareLists(shareContext, raw))
        {
            Ffi.wglDeleteContext(raw);
            throw Ffi.LastError("wglShareLists");
        }

        return (raw, SupportsSurfaceless: false);
    }

    private static (nint Raw, bool SupportsSurfaceless) CreateContextArb(
        Display display,
        nint hdc,
        nint shareContext,
        ContextAttributes contextAttributes)
    {
        WglExtensions wgl = display.Wgl!;
        List<int> attrs = BuildContextAttributes(display, contextAttributes, out bool supportsSurfaceless);
        nint raw;
        fixed (int* attrsPtr = attrs.ToArray())
        {
            raw = wgl.CreateContextAttribsARB(hdc, shareContext, attrsPtr);
        }

        if (raw == 0)
        {
            throw Ffi.LastError("wglCreateContextAttribsARB");
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
                throw new GlutinException("extension to create ES context with WGL is not present");
            }

            GlVersion version = gles.Version ?? new GlVersion(2, 0);
            attrs.Add(WglConstants.ContextProfileMaskArb);
            attrs.Add(WglConstants.ContextEs2ProfileBitExt);
            attrs.Add(WglConstants.ContextMajorVersionArb);
            attrs.Add(version.Major);
            attrs.Add(WglConstants.ContextMinorVersionArb);
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
                attrs.Add(WglConstants.ContextMajorVersionArb);
                attrs.Add(requestedVersion.Major);
                attrs.Add(WglConstants.ContextMinorVersionArb);
                attrs.Add(requestedVersion.Minor);
                supportsSurfaceless = requestedVersion.CompareTo(new GlVersion(3, 0)) >= 0;
            }

            if (profile is { } requestedProfile)
            {
                attrs.Add(WglConstants.ContextProfileMaskArb);
                attrs.Add(requestedProfile == GlProfile.Core
                    ? WglConstants.ContextCoreProfileBitArb
                    : WglConstants.ContextCompatibilityProfileBitArb);
            }
        }

        int flags = 0;

        if (attributes.Debug)
        {
            flags |= WglConstants.ContextDebugBitArb;
        }

        if (attributes.Robustness != Robustness.NotRobust)
        {
            if (!display.Features.HasFlag(DisplayFeatures.ContextRobustness))
            {
                if (attributes.Robustness == Robustness.NoError)
                {
                    attrs.Add(WglConstants.ContextOpenGlNoErrorArb);
                    attrs.Add(1);
                }
                else
                {
                    throw new GlutinException("WGL_ARB_create_context_robustness is not supported");
                }
            }
            else
            {
                switch (attributes.Robustness)
                {
                    case Robustness.RobustNoResetNotification:
                        attrs.Add(WglConstants.ContextResetNotificationStrategyArb);
                        attrs.Add(WglConstants.NoResetNotificationArb);
                        flags |= WglConstants.ContextRobustAccessBitArb;
                        break;
                    case Robustness.RobustLoseContextOnReset:
                        attrs.Add(WglConstants.ContextResetNotificationStrategyArb);
                        attrs.Add(WglConstants.LoseContextOnResetArb);
                        flags |= WglConstants.ContextRobustAccessBitArb;
                        break;
                    case Robustness.NoError:
                        if (!display.Features.HasFlag(DisplayFeatures.ContextNoError))
                        {
                            throw new GlutinException("WGL_ARB_create_context_no_error is not supported");
                        }

                        attrs.Add(WglConstants.ContextOpenGlNoErrorArb);
                        attrs.Add(1);
                        break;
                }
            }
        }

        if (flags != 0)
        {
            attrs.Add(WglConstants.ContextFlagsArb);
            attrs.Add(flags);
        }

        if (attributes.ReleaseBehavior == ReleaseBehavior.None)
        {
            if (!display.Features.HasFlag(DisplayFeatures.ContextReleaseBehavior))
            {
                throw new GlutinException("WGL_ARB_context_flush_control is not supported");
            }

            attrs.Add(WglConstants.ContextReleaseBehaviorArb);
            attrs.Add(WglConstants.ContextReleaseBehaviorNoneArb);
        }

        attrs.Add(0);
        return attrs;
    }
}
#endif
