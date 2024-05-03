using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Quark.ImageRender;
using Quark.Neutrino;

namespace Quark.Controls;

/// <summary>
/// タイミング編集用の画面要素を調整するクラス
/// </summary>
public class TimingEditNegotiator
{
    private const int HorizontalMargin = 4;
    private Canvas _parent;
    private RenderInfoCommon? _renderInfo;
    private IReadOnlyList<PhonemeTiming>? _timings;

    /// <summary>ctor</summary>
    /// <param name="renderTarget">描画対象</param>
    public TimingEditNegotiator(Canvas renderTarget)
    {
        this._parent = renderTarget;
    }

    /// <summary>
    /// 描画するタイミング情報を更新する
    /// </summary>
    /// <param name="timings">タイミング情報のリスト</param>
    public void UpdateTimings(IReadOnlyList<PhonemeTiming> timings)
    {
        this._timings = timings;
        this.Reredner();
    }

    /// <summary>
    /// 親エディタ画面のレイアウト変更を反映する
    /// </summary>
    public void OnParentEditorLayoutUpdated(RenderInfoCommon renderInfo)
    {
        this._renderInfo = renderInfo;
        this.Reredner();
    }

    /// <summary>
    /// 画面に描画するタイミング情報を更新する
    /// </summary>
    public void RelocateRenderElements() => this.Reredner();

    /// <summary>
    /// 画面に描画するタイミング情報の配置、表示範囲を更新する
    /// </summary>
    private void Reredner()
    {
        var children = this._parent.Children;

        var timings = this._timings;
        var renderInfo = this._renderInfo;

        if (timings is null || renderInfo is null)
            return;

        // 描画範囲
        var renderRange = renderInfo.RenderRange;
        // レイアウト情報
        var renderLayout = renderInfo.ScreenLayout;

        // 描画済みの情報を取得
        var renderedTimings = this.EnumerateElements()
            .ToDictionary(e => e.Timing);

        // 描画時間
        (int beginTime, int endTime) = (renderRange.BeginTime, renderRange.EndTime);

        // 新たに描画するタイミング
        var renderTimings = timings.Where(e => beginTime <= e.EditingTimeMs && e.EditingTimeMs <= endTime)
            .ToHashSet();

        // 不要なタイミングを削除
        if (renderedTimings.Count > 0)
            children.RemoveAll(renderedTimings.Values.Where(t => !renderTimings.Contains(t.Timing)));

        // 縦オフセットごとの横位置使用状況
        List<double> usedXPositionByYOffset = [0];

        using (Dispatcher.UIThread.DisableProcessing())
        {
            foreach (var timing in renderTimings)
            {
                int x = renderLayout.GetRenderPosXFromTime(timing.EditingTimeMs - beginTime);
                int y = 0;
                int h = renderLayout.ScoreArea.Height;

                TimingHandle? element;
                bool isExists = renderedTimings.TryGetValue(timing, out element);
                if (element is null)
                {
                    element = new(timing)
                    {
                        ZIndex = 3, // HACK
                        IsHitTestVisible = false,
                    };
                }

                SetLeft(element, x - HorizontalMargin);
                SetTop(element, y);
                element.Margin = new(HorizontalMargin, 0);
                element.Height = h + 2;
                element.Phoneme = timing.Phoneme;

                // 縦位置を計算
                double w = element.RenderWidth;

                int foundYOffset = -1;
                for (int idx = 0; idx < usedXPositionByYOffset.Count; ++idx)
                {
                    if (usedXPositionByYOffset[idx] < x)
                    {
                        foundYOffset = idx;
                        usedXPositionByYOffset[idx] = x + w;
                        break;
                    }
                }

                if (foundYOffset < 0)
                {
                    // 他が埋まっている場合はオフセットレベルを追加
                    usedXPositionByYOffset.Add(x + w);
                    foundYOffset = usedXPositionByYOffset.Count - 1;
                }

                element.YOffset = foundYOffset;

                // 要素が存在しないなら親部品に追加する
                if (!isExists)
                    children.Add(element);
            }
        }
    }

    /// <summary>
    /// 指定した座標にヒットするタイミング情報を取得する
    /// </summary>
    /// <param name="point">検索する座標</param>
    /// <returns>ヒットしたタイミング情報。1つもヒットしない場合はnull</returns>
    public PhonemeTiming? HitTest(Point point)
    {
        var renderInfo = this._renderInfo;
        if (renderInfo is null)
            return null;

        var screenLayout = renderInfo.ScreenLayout;

        int mouseX = (int)point.X;

        var children = this._parent.Children;
        var elements = this.EnumerateElements().Where(t =>
        {
            double x = GetLeft(t);
            return x <= mouseX && mouseX <= (x + t.RenderWidth);
        }).ToArray();

        // 該当するタイミング要素が見つからない場合はnullを返却
        if (elements.Length == 0)
            return null;

        if (elements.Length == 1)
        {
            var element = elements[0];

            // 音素名、もしくは垂直バー部分に当たっていれば
            if (IsHitTestPhonemeLabel(element, point) || IsHitTestVerticalBar(element, mouseX))
                return element.Timing;
            else
                return null;
        }

        var singles = new List<TimingHandle>(elements.Length);

        // 音素名部分に当たっていればそのタイミングを返却
        foreach (var element in elements)
        {
            if (IsHitTestPhonemeLabel(element, point))
                return element.Timing;

            // ついでに垂直バーに当たっている情報を収集しておく
            if (IsHitTestVerticalBar(element, mouseX))
                singles.Add(element);
        }

        if (singles.Count == 0)
            // 垂直バーに当たっているものがなければ情報なし
            return null;
        else if (singles.Count == 1)
            // 垂直バーに当たっているものが一つだけならそのタイミングを返却
            return singles[0].Timing;

        // TODO
        return null;
    }

    /// <summary>
    /// 垂直バーが指定座標(X)にヒットしているかを判定する
    /// </summary>
    /// <param name="element"></param>
    /// <param name="mouseX"></param>
    /// <returns></returns>
    private static bool IsHitTestVerticalBar(TimingHandle element, int mouseX)
    {
        double x = GetLeft(element);
        return x <= mouseX && mouseX <= (x + 1 + (HorizontalMargin * 2));
    }

    /// <summary>
    /// 音素ラベルが指定座標にヒットしているかを判定する
    /// </summary>
    /// <param name="element"></param>
    /// <param name="point"></param>
    /// <returns></returns>
    private static bool IsHitTestPhonemeLabel(TimingHandle element, Point point)
    {
        double x = GetLeft(element);
        double w = element.RenderWidth;

        double bottomMargin = 1;
        double lower = element.Bounds.Height - bottomMargin;

        double areaH = element.PhonemeRenderHeight;

        lower -= element.YOffset * areaH;
        double upper = lower - areaH;

        return x <= point.X && point.X <= (x + w) && upper <= point.Y && point.Y <= lower;
    }

    /// <summary>
    /// 編集用の画面要素を取得する
    /// </summary>
    /// <returns></returns>
    private IEnumerable<TimingHandle> EnumerateElements()
        => this._parent.Children.OfType<TimingHandle>();

    /// <summary>
    /// Canvas内の横位置を設定する
    /// </summary>
    /// <param name="element">画面要素</param>
    /// <param name="x">横位置</param>
    private static void SetLeft(TimingHandle element, double x)
        => Canvas.SetLeft(element, x);

    /// <summary>
    /// キャンバス内の縦位置を設定する
    /// </summary>
    /// <param name="element">画面要素</param>
    /// <param name="y">縦位置</param>
    private static void SetTop(TimingHandle element, double y)
        => Canvas.SetTop(element, y);

    /// <summary>
    /// キャンバス内の横位置を取得する
    /// </summary>
    /// <param name="element">画面要素</param>
    /// <returns>横位置</returns>
    private static double GetLeft(TimingHandle element)
        => Canvas.GetLeft(element);

    /// <summary>
    /// キャンバス内の縦位置を取得する
    /// </summary>
    /// <param name="element">画面要素</param>
    /// <returns>縦位置</returns>
    private static double GetTop(TimingHandle element)
        => Canvas.GetTop(element);

    /// <summary>
    /// 指定のタイミングを選択済みにする
    /// </summary>
    /// <param name="timing">対象のタイミング</param>
    public void Select(PhonemeTiming timing)
    {
        foreach (var element in this.EnumerateElements())
        {
            if (element.Timing == timing)
                element.Select();
            else if (element.IsSelected)
                element.Unselect();
        }
    }

    /// <summary>
    /// すべての要素を未選択にする
    /// </summary>
    public void UnselectAll()
    {
        foreach (var timing in this.EnumerateElements().Where(t => t.IsSelected))
        {
            timing.Unselect();
        }
    }
}
