using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Quark.Projects.Tracks;

public interface IMspecDynamicsPhrase<T> : INeutrinoPhrase
    where T : IFloatingPointIeee754<T>
{
    /// <summary>オリジナルのMspec</summary>
    public T[]? Mspec { get; }

    /// <summary>編集中のダイナミクス</summary>
    public T[]? EditingDynamics { get; }

    /// <summary>編集後のダイナミクス</summary>
    public T[]? EditedDynamics { get; }

    // HACK: 後で削除する
    /// <summary>
    /// 編集内容を反映したMspecを取得する。
    /// </summary>
    /// <returns></returns>
    public T[]? GetEditedMspec();

    /// <summary>
    /// 編集内容を反映したMspecを取得する。
    /// </summary>
    /// <returns></returns>
    public T[]? GetEditingMspec();

}
