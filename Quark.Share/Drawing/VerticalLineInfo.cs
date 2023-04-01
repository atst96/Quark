namespace Quark.Drawing;

/// <summary>
/// ピアノロール上の縦線情報
/// </summary>
/// <param name="Time">時間</param>
/// <param name="LineType">線の種類</param>
public record VerticalLineInfo(decimal Time, LineType LineType);
