using RawWindowHandles;

namespace Glutin;

public interface IGlContext
{
    ContextApi ContextApi { get; }

    Priority Priority { get; }
}

public interface INotCurrentGlContext : IGlContext, IGetGlDisplay, IGetGlConfig, IAsRawContext, IDisposable
{
    PossiblyCurrentContext TreatAsPossiblyCurrent();

    PossiblyCurrentContext MakeCurrent<TSurface>(Surface<TSurface> surface)
        where TSurface : struct, ISurfaceType;

    PossiblyCurrentContext MakeCurrentDrawRead<TSurface>(
        Surface<TSurface> surfaceDraw,
        Surface<TSurface> surfaceRead)
        where TSurface : struct, ISurfaceType;

    PossiblyCurrentContext MakeCurrentSurfaceless();
}

public interface IPossiblyCurrentGlContext : IGlContext, IGetGlDisplay, IGetGlConfig, IAsRawContext, IDisposable
{
    bool IsCurrent { get; }

    NotCurrentContext MakeNotCurrent();

    void MakeNotCurrentInPlace();

    void MakeCurrent<TSurface>(Surface<TSurface> surface)
        where TSurface : struct, ISurfaceType;

    void MakeCurrentDrawRead<TSurface>(
        Surface<TSurface> surfaceDraw,
        Surface<TSurface> surfaceRead)
        where TSurface : struct, ISurfaceType;

    void MakeCurrentSurfaceless();
}

public interface IPlatformNotCurrentGlContext : INotCurrentGlContext
{
}

public interface IPlatformPossiblyCurrentGlContext : IPossiblyCurrentGlContext
{
}

public interface IAsRawContext
{
    RawContext RawContext { get; }
}

public sealed class ContextAttributesBuilder
{
    private ContextAttributes _attributes = new();

    public static ContextAttributesBuilder New()
    {
        return new ContextAttributesBuilder();
    }

    public ContextAttributesBuilder WithDebug(bool debug)
    {
        _attributes = _attributes with { Debug = debug };
        return this;
    }

    public ContextAttributesBuilder WithSharing(IAsRawContext context)
    {
        _attributes = _attributes with { SharedContext = context.RawContext };
        return this;
    }

    public ContextAttributesBuilder WithRobustness(Robustness robustness)
    {
        _attributes = _attributes with { Robustness = robustness };
        return this;
    }

    public ContextAttributesBuilder WithReleaseBehavior(ReleaseBehavior releaseBehavior)
    {
        _attributes = _attributes with { ReleaseBehavior = releaseBehavior };
        return this;
    }

    public ContextAttributesBuilder WithProfile(GlProfile profile)
    {
        _attributes = _attributes with { Profile = profile };
        return this;
    }

    public ContextAttributesBuilder WithContextApi(ContextApi api)
    {
        _attributes = _attributes with { Api = api };
        return this;
    }

    public ContextAttributesBuilder WithPriority(Priority priority)
    {
        _attributes = _attributes with { Priority = priority };
        return this;
    }

    public ContextAttributes Build(RawWindowHandle? rawWindowHandle = null)
    {
        return _attributes with { RawWindowHandle = rawWindowHandle };
    }
}

public sealed record ContextAttributes
{
    public ReleaseBehavior ReleaseBehavior { get; init; } = ReleaseBehavior.Flush;

    public bool Debug { get; init; }

    public Robustness Robustness { get; init; } = Robustness.NotRobust;

    public GlProfile? Profile { get; init; }

    public ContextApi? Api { get; init; }

    public Priority? Priority { get; init; }

    public RawContext? SharedContext { get; init; }

    public RawWindowHandle? RawWindowHandle { get; init; }
}

public enum Robustness
{
    NotRobust,
    NoError,
    RobustNoResetNotification,
    RobustLoseContextOnReset,
}

public enum GlProfile
{
    Core,
    Compatibility,
}

public record struct ContextApi
{
    public readonly record struct OpenGl(GlVersion? Version);

    public readonly record struct Gles(GlVersion? Version);

    private const byte OpenGlTag = 0;
    private const byte GlesTag = 1;

    private byte _tag;
    private OpenGl _openGl;
    private Gles _gles;

    public ContextApi(OpenGl value)
    {
        this = default;
        _tag = OpenGlTag;
        _openGl = value;
    }

    public ContextApi(Gles value)
    {
        this = default;
        _tag = GlesTag;
        _gles = value;
    }

    public static ContextApi FromOpenGl(GlVersion? version = null)
    {
        return new ContextApi(new OpenGl(version));
    }

    public static ContextApi FromGles(GlVersion? version = null)
    {
        return new ContextApi(new Gles(version));
    }

    public bool TryGetValue(out OpenGl value)
    {
        value = _openGl;
        return _tag == OpenGlTag;
    }

    public bool TryGetValue(out Gles value)
    {
        value = _gles;
        return _tag == GlesTag;
    }
}

public readonly record struct GlVersion(byte Major, byte Minor) : IComparable<GlVersion>
{
    public int CompareTo(GlVersion other)
    {
        int major = Major.CompareTo(other.Major);
        return major != 0 ? major : Minor.CompareTo(other.Minor);
    }

    public override string ToString()
    {
        return $"{Major}.{Minor}";
    }
}

public enum ReleaseBehavior
{
    None,
    Flush,
}

public enum Priority
{
    Low,
    Medium,
    High,
}

public sealed class NotCurrentContext : INotCurrentGlContext
{
    private readonly IPlatformNotCurrentGlContext _backend;

    internal NotCurrentContext(IPlatformNotCurrentGlContext backend)
    {
        _backend = backend;
    }

    internal IPlatformNotCurrentGlContext Backend => _backend;

    public ContextApi ContextApi => _backend.ContextApi;

    public Priority Priority => _backend.Priority;

    public Display Display => _backend.Display;

    public Config Config => _backend.Config;

    public RawContext RawContext => _backend.RawContext;

    public PossiblyCurrentContext TreatAsPossiblyCurrent()
    {
        return _backend.TreatAsPossiblyCurrent();
    }

    public PossiblyCurrentContext MakeCurrent<TSurface>(Surface<TSurface> surface)
        where TSurface : struct, ISurfaceType
    {
        return _backend.MakeCurrent(surface);
    }

    public PossiblyCurrentContext MakeCurrentDrawRead<TSurface>(
        Surface<TSurface> surfaceDraw,
        Surface<TSurface> surfaceRead)
        where TSurface : struct, ISurfaceType
    {
        return _backend.MakeCurrentDrawRead(surfaceDraw, surfaceRead);
    }

    public PossiblyCurrentContext MakeCurrentSurfaceless()
    {
        return _backend.MakeCurrentSurfaceless();
    }

    public void Dispose()
    {
        _backend.Dispose();
    }
}

public sealed class PossiblyCurrentContext : IPossiblyCurrentGlContext
{
    private readonly IPlatformPossiblyCurrentGlContext _backend;

    internal PossiblyCurrentContext(IPlatformPossiblyCurrentGlContext backend)
    {
        _backend = backend;
    }

    internal IPlatformPossiblyCurrentGlContext Backend => _backend;

    public ContextApi ContextApi => _backend.ContextApi;

    public Priority Priority => _backend.Priority;

    public Display Display => _backend.Display;

    public Config Config => _backend.Config;

    public RawContext RawContext => _backend.RawContext;

    public bool IsCurrent => _backend.IsCurrent;

    public NotCurrentContext MakeNotCurrent()
    {
        return _backend.MakeNotCurrent();
    }

    public void MakeNotCurrentInPlace()
    {
        _backend.MakeNotCurrentInPlace();
    }

    public void MakeCurrent<TSurface>(Surface<TSurface> surface)
        where TSurface : struct, ISurfaceType
    {
        _backend.MakeCurrent(surface);
    }

    public void MakeCurrentDrawRead<TSurface>(
        Surface<TSurface> surfaceDraw,
        Surface<TSurface> surfaceRead)
        where TSurface : struct, ISurfaceType
    {
        _backend.MakeCurrentDrawRead(surfaceDraw, surfaceRead);
    }

    public void MakeCurrentSurfaceless()
    {
        _backend.MakeCurrentSurfaceless();
    }

    public void Dispose()
    {
        _backend.Dispose();
    }
}

public record struct RawContext
{
    public readonly record struct Egl(nint Context);

    public readonly record struct Glx(nint Context);

    public readonly record struct Wgl(nint Context);

    public readonly record struct Cgl(nint Context);

    private const byte EglTag = 0;
    private const byte GlxTag = 1;
    private const byte WglTag = 2;
    private const byte CglTag = 3;

    private byte _tag;
    private Egl _egl;
    private Glx _glx;
    private Wgl _wgl;
    private Cgl _cgl;

    public RawContext(Egl value)
    {
        this = default;
        _tag = EglTag;
        _egl = value;
    }

    public RawContext(Glx value)
    {
        this = default;
        _tag = GlxTag;
        _glx = value;
    }

    public RawContext(Wgl value)
    {
        this = default;
        _tag = WglTag;
        _wgl = value;
    }

    public RawContext(Cgl value)
    {
        this = default;
        _tag = CglTag;
        _cgl = value;
    }

    public bool TryGetValue(out Egl value)
    {
        value = _egl;
        return _tag == EglTag;
    }

    public bool TryGetValue(out Glx value)
    {
        value = _glx;
        return _tag == GlxTag;
    }

    public bool TryGetValue(out Wgl value)
    {
        value = _wgl;
        return _tag == WglTag;
    }

    public bool TryGetValue(out Cgl value)
    {
        value = _cgl;
        return _tag == CglTag;
    }
}
