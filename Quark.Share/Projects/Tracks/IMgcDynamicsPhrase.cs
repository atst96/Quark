using System.Numerics;

namespace Quark.Projects.Tracks;

public interface IMgcDynamicsPhrase<T>
    where T : IFloatingPointIeee754<T>
{
    /// <summary>オリジナルのダイナミクス</summary>
    public T[]? Mgc { get; }

    /// <summary>編集中のダイナミクス</summary>
    public T[]? EditingDynamics { get; }

    /// <summary>編集後のダイナミクス</summary>
    public T[]? EditedDynamics { get; }
}
