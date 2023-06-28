using Quark.Constants;
using Quark.ImageRender.PianoRoll;
using Quark.ImageRender;
using Quark.Models.Neutrino;
using SkiaSharp;
using Quark.Projects.Tracks;
using Quark.Utils;

namespace Quark.Helpers;

public class ScoreLayoutHelper
{
    public static IList<TimingHandle> CreateTimingHandles(INeutrinoTrack track)
    {
        var timings = track.Timings;

        var list = new List<TimingHandle>(timings.Length);

        var rawPhrases = track.RawPhrases;
        var voicePhrases = track.Phrases;

        int currentPhonemeCount = 0;
        int rawPhraseIdx = 0;
        int voicedPhraseIdx = 0;

        PhraseInfo? rawPhrase = null;
        INeutrinoPhrase? voicePhrase = null;

        for (int timingIdx = 0; timingIdx < timings.Length; ++timingIdx)
        {
            var timing = timings[timingIdx];

            for (; currentPhonemeCount < timingIdx && rawPhraseIdx < rawPhrases.Length; ++rawPhraseIdx)
            {
                rawPhrase = rawPhrases[rawPhraseIdx];
                voicePhrase = rawPhrase.IsVoiced ? voicePhrases[voicedPhraseIdx++] : null;

                int phonemeCounts = rawPhrase.Phoneme.Sum(p => p.Length);

                currentPhonemeCount += phonemeCounts;
            }

            list.Add(new TimingHandle
            {
                TimingInfo = timing,
                Phrase = voicePhrase,
            });
        }

        return list;
    }

    public static IList<TimingHandle> GetRenderTargets(TimingRenderer renderer, RenderInfoCommon renderCommon, IList<TimingHandle>? handles, TimingHandle? editingTiming)
    {
        if (handles == null || handles.Count == 0)
            return Array.Empty<TimingHandle>();

        var ri = renderCommon;

        var rangeInfo = ri.RenderRange;
        var renderInfo = ri.PartRenderInfo;
        var scaling = renderInfo.Scaling;

        int beginTime = rangeInfo.BeginTime;
        int endTime = rangeInfo.EndTime;

        long tBegin = beginTime * 10000;
        long tEnd = endTime * 10000;

        // 1行辺りの高さ
        int fontHeight = renderer.GetTextHeight();

        // 行レベル毎の最後の使用された領域(X位置)
        // 必ず1件以上は存在するので、1件仮の値で登録しておく
        var latestAreaXPerLevel = new List<int>(10) { 0 };

        var targets = new List<TimingHandle>(handles.Count);

        int y = renderInfo.DisplayScorePosY;
        int h = renderInfo.RenderDisplayScoreHeight;

        foreach (var handle in handles)
        {
            var t = handle.TimingInfo;

            if (object.ReferenceEquals(t, editingTiming))
            {
                targets.Add(editingTiming);
                continue;
            }

            if (!(tBegin <= t.EditedBeginTime100Ns && t.EditedBeginTime100Ns <= tEnd))
                continue;

            int time = NeutrinoUtil.TimingTimeToMs(t.EditedBeginTime100Ns);
            int x = scaling.ToDisplayScaling((time - beginTime) * renderInfo.WidthStretch);

            // 行レベル
            int level = GetLevel(latestAreaXPerLevel, x);

            // 行の高さ
            int lineEndY = y + h;

            // 横線を描画
            int textX = x + 4;
            int textY = lineEndY - (level * fontHeight) - 4;
            int textW = renderer.MeasureTextWidth(t.Phoneme) + 2;
            int textH = renderer.GetTextHeight();

            handle.X = x;
            handle.PhonemeLocation = new SKPoint(textX, textY);
            handle.IsSelected = false;
            handle.PhonemeCollisionRect = SKRect.Create(textX - 2, textY - textH - 2, textW + 4, textH + 4);

            targets.Add(handle);

            latestAreaXPerLevel[level] = x + textW;
        }

        return targets;
    }

    private static int GetLevel(List<int> latestAreaXPerLevel, int currentX)
    {
        for (int idx = 0; idx < latestAreaXPerLevel.Count; ++idx)
        {
            if (latestAreaXPerLevel[idx] < currentX)
            {
                // 以前の描画済み領域とが被らない範囲が見つかれば、
                // その行レベルを返す
                return idx;
            }
        }

        // 非被り領域がない場合はレベルを追加する
        latestAreaXPerLevel.Add(currentX);
        return latestAreaXPerLevel.Count - 1;
    }
}
