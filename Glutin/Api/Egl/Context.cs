using GlutinConfig = Glutin.Config;
using GlutinDisplay = Glutin.Display;
using GlutinNotCurrentContext = Glutin.NotCurrentContext;
using GlutinPossiblyCurrentContext = Glutin.PossiblyCurrentContext;

namespace Glutin.Backend.Egl;

public sealed class NotCurrentContext : IPlatformNotCurrentGlContext
{
    private readonly ContextInner _inner;

    internal NotCurrentContext(ContextInner inner)
    {
        _inner = inner;
    }

    public ContextApi ContextApi => _inner.ContextApi;

    public Priority Priority => _inner.Priority;

    public GlutinDisplay Display => _inner.Display.Facade;

    public GlutinConfig Config => new(_inner.Config);

    public RawContext RawContext => new(new RawContext.Egl(_inner.Raw));

    public GlutinPossiblyCurrentContext TreatAsPossiblyCurrent()
    {
        return new GlutinPossiblyCurrentContext(new PossiblyCurrentContext(_inner));
    }

    public GlutinPossiblyCurrentContext MakeCurrent<TSurface>(Glutin.Surface<TSurface> surface)
        where TSurface : struct, ISurfaceType
    {
        if (surface.Backend is not Surface<TSurface> eglSurface)
        {
            throw new GlutinException("EGL context received a surface from another backend.");
        }

        _inner.MakeCurrentDrawRead(eglSurface.Raw, eglSurface.Raw);
        return new GlutinPossiblyCurrentContext(new PossiblyCurrentContext(_inner));
    }

    public GlutinPossiblyCurrentContext MakeCurrentDrawRead<TSurface>(
        Glutin.Surface<TSurface> surfaceDraw,
        Glutin.Surface<TSurface> surfaceRead)
        where TSurface : struct, ISurfaceType
    {
        if (surfaceDraw.Backend is not Surface<TSurface> eglDraw
            || surfaceRead.Backend is not Surface<TSurface> eglRead)
        {
            throw new GlutinException("EGL context received a surface from another backend.");
        }

        _inner.MakeCurrentDrawRead(eglDraw.Raw, eglRead.Raw);
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

    internal void BindApi()
    {
        _inner.BindApi();
    }

    public ContextApi ContextApi => _inner.ContextApi;

    public Priority Priority => _inner.Priority;

    public GlutinDisplay Display => _inner.Display.Facade;

    public GlutinConfig Config => new(_inner.Config);

    public RawContext RawContext => new(new RawContext.Egl(_inner.Raw));

    public bool IsCurrent
    {
        get
        {
            _inner.BindApi();
            return Ffi.eglGetCurrentContext() == _inner.Raw;
        }
    }

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
        if (surface.Backend is not Surface<TSurface> eglSurface)
        {
            throw new GlutinException("EGL context received a surface from another backend.");
        }

        _inner.MakeCurrentDrawRead(eglSurface.Raw, eglSurface.Raw);
    }

    public void MakeCurrentDrawRead<TSurface>(
        Glutin.Surface<TSurface> surfaceDraw,
        Glutin.Surface<TSurface> surfaceRead)
        where TSurface : struct, ISurfaceType
    {
        if (surfaceDraw.Backend is not Surface<TSurface> eglDraw
            || surfaceRead.Backend is not Surface<TSurface> eglRead)
        {
            throw new GlutinException("EGL context received a surface from another backend.");
        }

        _inner.MakeCurrentDrawRead(eglDraw.Raw, eglRead.Raw);
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

    private ContextInner(Display display, Config config, nint raw, uint api)
    {
        Display = display;
        Config = config;
        Raw = raw;
        Api = api;
    }

    internal Display Display { get; }

    internal Config Config { get; }

    internal nint Raw { get; private set; }

    internal uint Api { get; }

    internal ContextApi ContextApi =>
        QueryAttribute(EglConstants.ContextClientType) == EglConstants.OpenGlApi
            ? ContextApi.FromOpenGl()
            : ContextApi.FromGles();

    internal Priority Priority => QueryAttribute(EglConstants.ContextPriorityLevelImg) switch
    {
        EglConstants.ContextPriorityLowImg => Priority.Low,
        EglConstants.ContextPriorityHighImg => Priority.High,
        _ => Priority.Medium,
    };

    internal static ContextInner Create(Display display, Config config, ContextAttributes contextAttributes)
    {
        uint api = SelectApi(display, config, contextAttributes, out GlVersion? version);
        List<int> attrs = BuildContextAttributes(display, api, version, contextAttributes);

        nint shareContext = 0;
        if (contextAttributes.SharedContext is { } shared)
        {
            if (!shared.TryGetValue(out RawContext.Egl sharedEgl))
            {
                throw new GlutinException("incompatible context was passed to EGL");
            }

            shareContext = sharedEgl.Context;
        }

        if (Ffi.eglBindAPI(api) == EglConstants.False)
        {
            throw Ffi.LastError("eglBindAPI");
        }

        nint raw;
        fixed (int* attrsPtr = attrs.ToArray())
        {
            raw = Ffi.eglCreateContext(display.Raw, config.Raw, shareContext, attrsPtr);
        }

        if (raw == 0)
        {
            throw Ffi.LastError("eglCreateContext");
        }

        return new ContextInner(display, config, raw, api);
    }

    internal void MakeCurrentDrawRead(nint draw, nint read)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        BindApi();

        if (Ffi.eglMakeCurrent(Display.Raw, draw, read, Raw) == EglConstants.False)
        {
            throw Ffi.LastError("eglMakeCurrent");
        }
    }

    internal void MakeCurrentSurfaceless()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        BindApi();

        if (Ffi.eglMakeCurrent(Display.Raw, 0, 0, Raw) == EglConstants.False)
        {
            throw Ffi.LastError("eglMakeCurrent");
        }
    }

    internal void MakeNotCurrent()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        BindApi();

        if (Ffi.eglMakeCurrent(Display.Raw, 0, 0, 0) == EglConstants.False)
        {
            throw Ffi.LastError("eglMakeCurrent");
        }
    }

    internal void BindApi()
    {
        if (Ffi.eglQueryAPI() == Api)
        {
            return;
        }

        if (Ffi.eglBindAPI(Api) == EglConstants.False)
        {
            throw Ffi.LastError("eglBindAPI");
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

        if (Ffi.eglGetCurrentContext() == Raw)
        {
            _ = Ffi.eglMakeCurrent(Display.Raw, 0, 0, 0);
        }

        _ = Ffi.eglDestroyContext(Display.Raw, Raw);
        Raw = 0;
    }

    private int QueryAttribute(int attribute)
    {
        return Ffi.eglQueryContext(Display.Raw, Raw, attribute, out int value) == EglConstants.True
            ? value
            : 0;
    }

    private static uint SelectApi(
        Display display,
        Config config,
        ContextAttributes attributes,
        out GlVersion? version)
    {
        Glutin.Api configApi = config.Api;
        bool supportsOpenGl = display.Version.CompareTo(new GlVersion(1, 3)) > 0;

        if (attributes.Api is { } requestedApi)
        {
            if (requestedApi.TryGetValue(out ContextApi.OpenGl openGl))
            {
                if (!supportsOpenGl || !configApi.HasFlag(Glutin.Api.OpenGl))
                {
                    throw new GlutinException("the requested OpenGL context API is not supported by this EGL config.");
                }

                version = openGl.Version;
                return EglConstants.OpenGlApi;
            }

            requestedApi.TryGetValue(out ContextApi.Gles gles);
            version = gles.Version ?? DefaultGlesVersion(configApi);
            return EglConstants.OpenGlEsApi;
        }

        if (supportsOpenGl && configApi.HasFlag(Glutin.Api.OpenGl))
        {
            version = null;
            return EglConstants.OpenGlApi;
        }

        version = DefaultGlesVersion(configApi);
        return EglConstants.OpenGlEsApi;
    }

    private static GlVersion DefaultGlesVersion(Glutin.Api configApi)
    {
        if (configApi.HasFlag(Glutin.Api.Gles3))
        {
            return new GlVersion(3, 0);
        }

        if (configApi.HasFlag(Glutin.Api.Gles2))
        {
            return new GlVersion(2, 0);
        }

        return new GlVersion(1, 0);
    }

    private static List<int> BuildContextAttributes(
        Display display,
        uint api,
        GlVersion? version,
        ContextAttributes attributes)
    {
        var attrs = new List<int>();
        bool hasKhrCreateContext =
            display.Version.CompareTo(new GlVersion(1, 5)) >= 0
            || display.Extensions.Contains("EGL_KHR_create_context");

        if (hasKhrCreateContext)
        {
            int flags = 0;

            if (api == EglConstants.OpenGlApi)
            {
                (GlProfile profile, GlVersion? selectedVersion) = PickOpenGlProfile(attributes.Profile, version);
                version = selectedVersion;

                attrs.Add(EglConstants.ContextOpenGlProfileMask);
                attrs.Add(profile == GlProfile.Core
                    ? EglConstants.ContextOpenGlCoreProfileBit
                    : EglConstants.ContextOpenGlCompatibilityProfileBit);
            }

            if (version is { } requestedVersion)
            {
                attrs.Add(EglConstants.ContextMajorVersion);
                attrs.Add(requestedVersion.Major);
                attrs.Add(EglConstants.ContextMinorVersion);
                attrs.Add(requestedVersion.Minor);
            }

            bool requestedNoError = false;
            if (attributes.Robustness != Robustness.NotRobust)
            {
                if (attributes.Robustness == Robustness.NoError)
                {
                    if (!display.Features.HasFlag(DisplayFeatures.ContextNoError))
                    {
                        throw new GlutinException("EGL_KHR_create_context_no_error is not supported");
                    }

                    attrs.Add(EglConstants.ContextOpenGlNoErrorKhr);
                    attrs.Add(EglConstants.True);
                    requestedNoError = true;
                }
                else
                {
                    if (!display.Features.HasFlag(DisplayFeatures.ContextRobustness))
                    {
                        throw new GlutinException("EGL context robustness is not supported");
                    }

                    attrs.Add(EglConstants.ContextOpenGlResetNotificationStrategy);
                    attrs.Add(attributes.Robustness == Robustness.RobustLoseContextOnReset
                        ? EglConstants.LoseContextOnReset
                        : EglConstants.NoResetNotification);
                    flags |= EglConstants.ContextOpenGlRobustAccessBitKhr;
                }
            }

            if (attributes.Debug && !requestedNoError)
            {
                if (display.Version.CompareTo(new GlVersion(1, 5)) >= 0)
                {
                    attrs.Add(EglConstants.ContextOpenGlDebug);
                    attrs.Add(EglConstants.True);
                }
                else
                {
                    flags |= EglConstants.ContextOpenGlDebugBitKhr;
                }
            }

            if (flags != 0)
            {
                attrs.Add(EglConstants.ContextFlagsKhr);
                attrs.Add(flags);
            }
        }
        else if (display.Version.CompareTo(new GlVersion(1, 3)) >= 0 && version is { } requestedVersion)
        {
            attrs.Add(EglConstants.ContextClientVersion);
            attrs.Add(requestedVersion.Major);
        }

        if (attributes.Priority is { } priority
            && (display.Extensions.Contains("EGL_IMG_context_priority")
                || IsAndroidPriorityQuirk(display.Extensions)))
        {
            attrs.Add(EglConstants.ContextPriorityLevelImg);
            attrs.Add(priority switch
            {
                Priority.Low => EglConstants.ContextPriorityLowImg,
                Priority.High => EglConstants.ContextPriorityHighImg,
                _ => EglConstants.ContextPriorityMediumImg,
            });
        }

        attrs.Add(EglConstants.None);
        return attrs;
    }

    private static (GlProfile Profile, GlVersion? Version) PickOpenGlProfile(
        GlProfile? profile,
        GlVersion? version)
    {
        if (profile == GlProfile.Core && version is null)
        {
            return (GlProfile.Core, new GlVersion(3, 3));
        }

        return (profile ?? GlProfile.Compatibility, version);
    }

    private static bool IsAndroidPriorityQuirk(IReadOnlySet<string> extensions)
    {
        return extensions.Contains("EGL_ANDROID_front_buffer_auto_refresh")
            && extensions.Contains("EGL_ANDROID_create_native_client_buffer");
    }
}
