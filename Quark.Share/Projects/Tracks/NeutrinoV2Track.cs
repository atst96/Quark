using System.Diagnostics;
using Quark.Audio;
using Quark.Data.Projects.Neutrino;
using Quark.Data.Projects.Tracks;
using Quark.Models.Neutrino;
using Quark.Neutrino;
using Quark.Services;
using Quark.Utils;

namespace Quark.Projects.Tracks;

internal class NeutrinoV2Track : TrackBase, INeutrinoTrack
{
    public event EventHandler TimingEstimated;

    public event EventHandler FeatureChanged;

    public ModelInfo Singer { get; set; }

    public string MusicXml { get; set; }

    public byte[]? FullTiming { get; set; }

    public byte[]? MonoTiming { get; set; }

    public TimingInfo[] Timings { get; set; } = Array.Empty<TimingInfo>();

    public PhraseInfo[] RawPhrases { get; private set; } = Array.Empty<PhraseInfo>();

    public NeutrinoV2Phrase[] Phrases { get; private set; } = Array.Empty<NeutrinoV2Phrase>();

    INeutrinoPhrase[] INeutrinoTrack.Phrases => this.Phrases;
    public WaveData WaveData { get; } = new();

    public NeutrinoV2Track(Project project, string trackName, string musicXml, ModelInfo model) : base(project, trackName)
    {
        this.Singer = model;
        this.MusicXml = musicXml;

        _ = this.Load();
    }

    public NeutrinoV2Track(Project project, NeutrinoV2TrackConfig config, IEnumerable<ModelInfo> models)
        : base(project, config)
    {
        var singer = config.Singer;
        if (singer is not null)
        {
            this.Singer = models.FirstOrDefault(t => t.ModelId == singer)!; // TODO: モデルが見つからない場合
        }

        this.MusicXml = config.MusicXml;
        this.FullTiming = config.FullTiming;
        this.MonoTiming = config.MonoTiming;

        var features = config.Features;

        this.Timings = features.Timing ?? Array.Empty<TimingInfo>();
        this.RawPhrases = features.RawPhraseInfo ?? Array.Empty<PhraseInfo>();
        this.Phrases = features.Phrases.Select(p =>
        {
            var ph = new NeutrinoV2Phrase(p.No, p.BeginTime, p.EndTime, p.Phonemes, PhraseStatus.Complete);

            if (p.F0?.Any() ?? false)
            {
                ph.SetAudioFeatures(p.F0!, p.Mspec!, p.Mgc!, p.Bap!);
                ph.SetEdited(p.EditedF0, p.EditedDynamics);
                ph.SetStatus(PhraseStatus.WaitAudioRender);
            }

            return ph;

        }).ToArray();

        _ = this.Load();
    }

    public override TrackBaseConfig GetConfig()
    {
        var features = new AudioFeaturesV2Config(this.Singer?.ModelId!)
        {
            Timing = this.Timings,
            RawPhraseInfo = this.RawPhrases,
            Phrases = this.Phrases.Select(
                p => new PhraseInfoV2()
                {
                    No = p.No,
                    BeginTime = p.BeginTime,
                    EndTime = p.EndTime,
                    Phonemes = p.Phonemes,
                    F0 = p.F0,
                    Mspec = p.Mspec,
                    Mgc = p.Mgc,
                    Bap = p.Bap,
                    EditedF0 = p.EditedF0,
                    EditedDynamics = p.EditedDynamics,
                }
            ).ToArray(),
        };

        return new NeutrinoV2TrackConfig(this.TrackId, this.TrackName, this.MusicXml, this.FullTiming, this.MonoTiming, this.Singer?.ModelId, features);
    }

    public bool HasScoreTiming() => !(this.FullTiming is null && this.MonoTiming is null);

    private async Task Load()
    {
        var session = this.Project.Session;
        // TODO: 設定から取得する
        bool isBulkEstimation = true;

        // Label
        if (!this.HasScoreTiming())
        {
            var result = await session.NeutrinoV2.ConvertMusicXmlToTiming(new ConvertMusicXmlToTimingOption { MusicXml = this.MusicXml });
            if (result is null)
            {
                // TODO: 実行失敗時
                return;
            }

            this.FullTiming = result.FullTiming;
            this.MonoTiming = result.MonoTiming;
        }

        if (!this.HasTimings())
        {
            var result = await session.NeutrinoV2.GetTiming(this);
            if (result is null)
            {
                // TODO: 実行失敗時
                return;
            }

            this.Timings = NeutrinoUtil.ParseTiming(result.Timing);
            this.TimingEstimated?.Invoke(this, EventArgs.Empty);
            this.SetRawPhrase(result.Phrases);

            if (isBulkEstimation)
                session.AddEstimateQueue(this);
            else
                session.AddEstimateQueue(this, this.Phrases);
        }
        else
        {
            var estimatePhrases = this.Phrases.Where(p => !p.F0?.Any() ?? true);
            if (estimatePhrases.Any())
            {
                if (isBulkEstimation && estimatePhrases.Count() == this.Phrases.Length)
                    session.AddEstimateQueue(this);
                else
                    session.AddEstimateQueue(this, estimatePhrases);
            }

            var synthesisPhrases = this.Phrases.Where(p => p.F0?.Any() ?? false);
            if (synthesisPhrases.Any())
            {
                if (isBulkEstimation && synthesisPhrases.Count() == this.Phrases.Length)
                    session.AddAudioRenderQueue(this);
                else
                    session.AddAudioRenderQueue(this, synthesisPhrases);
            }
        }
    }

    public bool HasTimings() => this.Timings.Any();

    internal void RaiseFeatureChanged() => this.FeatureChanged?.Invoke(this, EventArgs.Empty);

    void INeutrinoTrack.RaiseFeatureChanged() => this.RaiseFeatureChanged();

    public long GetTotalFramesCount()
    {
        var timings = this.Timings;

        return timings.Length > 0
            ? (int)Math.Ceiling(timings.Last().EditedEndTime100Ns / 10000d / 5d)
            : 0;
    }

    internal void SetRawPhrase(string phrases)
    {
        var (raw, voices) = NeutrinoUtil.ParsePhrases(phrases, this.Timings,
            (int no, int beginTime, int endTime, string[][] phonemes, PhraseStatus status) => new NeutrinoV2Phrase(no, beginTime, endTime, phonemes, status));

        (this.RawPhrases, this.Phrases) = (raw, voices);
    }

    public void ChangeTiming(int timingIndex, int timeMs)
    {
        var timings = this.Timings;

        // タイミング変更により影響を受けるフレーズを取得
        var rawPhrases = this.RawPhrases;
        var phrases = this.Phrases;

        // 再推論対象のフレーズ
        var reProcessPhrases = new List<NeutrinoV2Phrase>();

        var phraseInfoByPhraseNumber = this.Phrases.ToDictionary(p => p.No);

        for (int phraseIdx = 0, lastTimingIndex = 0; phraseIdx < rawPhrases.Length; ++phraseIdx)
        {
            // フレーズが見つかるまで繰り返す

            // フレーズ辺りの音素数(タイミング情報の件数)を計算
            int count = rawPhrases[phraseIdx].Phoneme.Sum(ps => ps.Length);

            if (lastTimingIndex == timingIndex)
            {
                // フレーズ先頭のタイミングが変更された場合
                reProcessPhrases.AddRange(this.ChangeTimingWithPrevPhrase(timeMs, rawPhrases, phrases, phraseIdx));
                break;
            }

            int rangeLastIndex = lastTimingIndex + count - 1;
            if (rangeLastIndex == timingIndex)
            {
                // フレーズ末尾のタイミングが変更された場合
                reProcessPhrases.AddRange(this.ChangeTimingWithNextPhrase(timeMs, rawPhrases, phrases, phraseIdx));
                break;
            }
            else if (timingIndex > lastTimingIndex && rangeLastIndex > timingIndex)
            {
                // フレーズの中間のタイミングが変更された場合
                reProcessPhrases.AddRange(this.ChangeTimingInPhrase(rawPhrases, phrases, phraseIdx));
                break;
            }

            lastTimingIndex += count;
        }

        // 変更したタイミングを推論用のタイミング情報に反映する
        long timingTime = NeutrinoUtil.GetTimingTimeFromMs(timeMs);

        // 変更時点と前のタイミングに反映
        timings[timingIndex].EditedBeginTime100Ns = timingTime;
        if (timingIndex > 0)
            timings[timingIndex - 1].EditedEndTime100Ns = timingTime;

        // 再推論を実行する
        // TODO: 推論済みのフレーズであれば再推論の優先度を上げる
        this.Project.Session.AddEstimateQueue(this, reProcessPhrases, EstimatePriority.Edit);
    }

    private static NeutrinoV2Phrase FindPhrase(IEnumerable<NeutrinoV2Phrase> phrase, int no)
        => phrase.First(p => p.No == no);

    /// <summary>
    /// 前フレーズと隣接するタイミングを変更する
    /// </summary>
    /// <param name="timeMs"></param>
    /// <param name="rawPhrases"></param>
    /// <param name="phrases"></param>
    /// <param name="phraseIdx"></param>
    /// <returns></returns>
    private IEnumerable<NeutrinoV2Phrase> ChangeTimingWithPrevPhrase(int timeMs, PhraseInfo[] rawPhrases, NeutrinoV2Phrase[] phrases, int phraseIdx)
    {
        // 直前のフレーズを再推論対象にする

        if (phraseIdx > 0)
        {
            // 先頭フレーズの場合は直前のフレーズが存在しないのでスキップする

            int prevPhraseIdx = phraseIdx - 1;
            var prevPhrase = rawPhrases[prevPhraseIdx];
            if (prevPhrase.IsVoiced)
            {
                var info = FindPhrase(phrases, prevPhrase.No);

                // 出力音声を削除
                this.ClearRenderAudio(info);
                // フレーズの終了時間を更新
                info.ChangeEndTime(timeMs, isClearAudioFeature: true);

                yield return info;
            }
        }

        // 現在フレーズの情報を更新し、再推論対象にする
        var phrase = rawPhrases[phraseIdx];
        phrase.Time = timeMs;

        if (phrase.IsVoiced)
        {
            var info = FindPhrase(phrases, phrase.No);

            // 出力音声を削除
            this.ClearRenderAudio(info);
            // フレーズの終了時間を更新
            info.ChangeBeginTime(timeMs, isClearAudioFeature: true);

            yield return info;
        }
    }

    /// <summary>
    /// 次フレーズと隣接するタイミングを変更する
    /// </summary>
    /// <param name="timeMs"></param>
    /// <param name="rawPhrases"></param>
    /// <param name="phrases"></param>
    /// <param name="phraseIdx"></param>
    /// <returns></returns>
    private IEnumerable<NeutrinoV2Phrase> ChangeTimingWithNextPhrase(int timeMs, PhraseInfo[] rawPhrases, NeutrinoV2Phrase[] phrases, int phraseIdx)
    {
        // 現在のフレーズを再推論対象にする
        var phrase = rawPhrases[phraseIdx];
        if (phrase.IsVoiced)
        {
            var info = FindPhrase(phrases, phrase.No);

            // 出力音声を削除
            this.ClearRenderAudio(info);
            // フレーズの終了時間を更新
            info.ChangeEndTime(timeMs, isClearAudioFeature: true);

            yield return info;
        }

        // 次フレーズの情報を更新し、再推論対象にする
        int nextPhraseIdx = phraseIdx + 1;
        if (nextPhraseIdx < rawPhrases.Length)
        {
            var nextPhrase = rawPhrases[nextPhraseIdx];
            if (nextPhrase.IsVoiced)
            {
                var info = FindPhrase(phrases, nextPhrase.No);

                // 出力音声を削除
                this.ClearRenderAudio(info);

                yield return info;
            }
        }
    }

    /// <summary>
    /// フレーズ内のタイミングを変更する
    /// </summary>
    /// <param name="rawPhrases"></param>
    /// <param name="phrases"></param>
    /// <param name="phraseIdx"></param>
    /// <returns></returns>
    private IEnumerable<NeutrinoV2Phrase> ChangeTimingInPhrase(PhraseInfo[] rawPhrases, NeutrinoV2Phrase[] phrases, int phraseIdx)
    {
        // 現在のフレーズのみを再推論する
        var phrase = rawPhrases[phraseIdx];

        var info = FindPhrase(phrases, phrase.No);

        // 出力音声を削除
        this.ClearRenderAudio(info);
        // フレーズの終了時間を更新
        info.ClearAudioFeatures();

        yield return info;
    }

    /// <summary>
    /// フレーズの音声情報を消去する
    /// </summary>
    /// <param name="phrase">フレーズ情報</param>
    private void ClearRenderAudio(INeutrinoPhrase phrase)
    {
        this.WaveData.SilenceAtTimeRange(phrase.BeginTime, phrase.EndTime);
    }

    public void ReSynthesis()
    {
        var phrases = this.Phrases.Where(p => p.IsAudioFeatureEditing());
        if (!phrases.Any())
            return;

        foreach (var p in phrases)
            ((INeutrinoPhrase)p).DetermineEditingAudioFeatures();

        this.Project.Session.AddAudioRenderQueue(this, phrases);
    }

    private IEnumerable<NeutrinoV2Phrase> GetPhrases(int beginTime, int endTime)
        => this.Phrases.Where(p => p.BeginTime <= beginTime && endTime <= p.EndTime);

    public void EditF0(int beginTime, Span<float> frequencies)
    {
        int endTime = beginTime + NeutrinoUtil.FrameIndexToMs(frequencies.Length);

        foreach (var phrase in this.GetPhrases(beginTime, endTime))
            phrase.EditF0(beginTime, frequencies);
    }
}
