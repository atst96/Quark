using Quark.Models.Neutrino;
using Quark.Projects.Tracks;
using SkiaSharp;

namespace Quark.Constants;

/// <summary>
/// タイミング調整用のハンドル
/// </summary>
public class TimingHandle
{
    /// <summary>横位置</summary>
    public float X { get; set; }

    /// <summary>縦線当たり判定のマージン</summary>
    public int HorizontalMargin { get; set; } = 4;

    /// <summary>音素の描画位置</summary>
    public SKPoint PhonemeLocation { get; set; }

    /// <summary>音素部分の当たり判定領域</summary>
    public SKRect PhonemeCollisionRect { get; set; }

    /// <summary>選択中状態</summary>
    public bool IsSelected { get; set; } = false;

    /// <summary>タイミング情報</summary>
    public required TimingInfo TimingInfo { get; init; }

    /// <summary>フレーズ情報</summary>
    public INeutrinoPhrase? Phrase { get; set; }

    /// <summary>
    /// 描画位置を変更する
    /// </summary>
    /// <param name="x"></param>
    public void MoveX(float x)
    {
        var point = new SKPoint((float)(x - this.X), 0);
        var rect = this.PhonemeCollisionRect;

        this.X = x;
        this.PhonemeLocation += point;
        this.PhonemeCollisionRect = SKRect.Create(rect.Location + point, rect.Size);
    }

    /// <summary>
    /// ハンドルの当たり判定
    /// </summary>
    /// <param name="x">X</param>
    /// <param name="y">Y</param>
    /// <returns></returns>
    public bool IsCollisionDetection(int x, int y)
    {
        float lineX = this.X;
        int margin = this.HorizontalMargin;

        return (lineX - margin < x && x < (lineX + margin)) || this.PhonemeCollisionRect.Contains(x, y);
    }
}
