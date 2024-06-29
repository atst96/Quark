using System;
using Quark.Models.Neutrino;
using Quark.Projects.Tracks;

namespace Quark.Renderers;

/// <summary>
/// ピッチの描画クラス
/// </summary>
internal abstract class PitchRenderer : RendererBase
{
    /// <summary>
    /// ピッチのレンダラを生成する
    /// </summary>
    /// <typeparam name="T">ピッチの型情報</typeparam>
    /// <param name="track">トラック</param>
    /// <returns></returns>
    public static IVisualRenderer Create(INeutrinoTrack track)
        => track switch
        {
            NeutrinoV1Track v1 => new ScorePitchRenderer<NeutrinoV1Phrase, double>(v1),
            NeutrinoV2Track v2 => new ScorePitchRenderer<NeutrinoV2Phrase, float>(v2),
            _ => throw new NotSupportedException(),
        };
}
