namespace Quark.Renderers;

/// <summary>
/// 描画開始位置の情報
/// </summary>
/// <param name="BeginPointIdx">開始位置</param>
/// <param name="RenderType">描画種別</param>
internal record RenderStartInfo(int BeginPointIdx, RenderValueType RenderType);
