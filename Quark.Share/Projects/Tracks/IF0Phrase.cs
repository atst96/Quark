using System.Numerics;

namespace Quark.Projects.Tracks;

public interface IF0Phrase<T> : INeutrinoPhrase
    where T : IFloatingPointIeee754<T>
{
    /// <summary>オリジナルのF0</summary>
    public T[]? F0 { get; }

    /// <summary>編集中のF0</summary>
    public T[]? EditingF0 { get; }

    /// <summary>編集後のF0</summary>
    public T[]? EditedF0 { get; }
}
