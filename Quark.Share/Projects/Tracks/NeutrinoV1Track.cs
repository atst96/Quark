using Quark.Audio;
using Quark.Data.Projects.Neutrino;
using Quark.Data.Projects.Tracks;
using Quark.Models.Neutrino;
using Quark.Neutrino;
using Quark.Services;
using Quark.Utils;

namespace Quark.Projects.Tracks;

internal class NeutrinoV1Track : TrackBase, INeutrinoTrack
{
    public event EventHandler TimingEstimated;

    public event EventHandler FeatureChanged;

    public ModelInfo Singer { get; set; }

    public string MusicXml { get; set; }

    public byte[]? FullTiming { get; set; }

    public byte[]? MonoTiming { get; set; }

    public TimingInfo[] Timings { get; set; } = Array.Empty<TimingInfo>();

    public PhraseInfo[] RawPhrases { get; private set; } = Array.Empty<PhraseInfo>();

    public NeutrinoV1Phrase[] Phrases { get; private set; } = Array.Empty<NeutrinoV1Phrase>();

    INeutrinoPhrase[] INeutrinoTrack.Phrases => this.Phrases;

    public WaveData WaveData { get; } = new();

    public NeutrinoV1Track(Project project, string trackName, string musicXml, ModelInfo model)
        : base(project, trackName)
    {
        this.Singer = model;
        this.MusicXml = musicXml;

        _ = this.Load();
    }

    public NeutrinoV1Track(Project project, NeutrinoV1TrackConfig config, IEnumerable<ModelInfo> models)
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

        var value = config.Features;
        this.Timings = value.Timings;
        this.RawPhrases = value.RawPhrases;
        this.Phrases = value.Phrases.Select(ConvertConfig).ToArray();

        _ = this.Load();
    }

    private static NeutrinoV1Phrase ConvertConfig(PhraseInfoV1 config)
    {
        bool hasAudioFeatures = config.F0 != null && config.Mgc != null && config.Bap != null;

        var phrase = new NeutrinoV1Phrase(config.No, config.BeginTime, config.EndTime, config.Phonemes, (hasAudioFeatures ? PhraseStatus.WaitAudioRender : PhraseStatus.WaitEstimate));

        if (hasAudioFeatures)
        {
            phrase.SetAudioFeatures(config.F0!, config.Mgc!, config.Bap!);
            phrase.SetEdited(config.EditedF0, config.EditedDynamics);
        }

        return phrase;
    }

    public override TrackBaseConfig GetConfig()
    {
        var config = new AudioFeaturesV1Config(this.Singer.ModelId)
        {
            Timings = this.Timings,
            RawPhrases = this.RawPhrases,
            Phrases = this.Phrases.Select(i => ToConfig(i)).ToArray(),
        };

        return new NeutrinoV1TrackConfig(this.TrackId, this.TrackName, this.MusicXml, this.FullTiming, this.MonoTiming, this.Singer?.ModelId, config);
    }

    private PhraseInfoV1 ToConfig(NeutrinoV1Phrase i) => new()
    {
        No = i.No,
        BeginTime = i.BeginTime,
        EndTime = i.EndTime,
        Phonemes = i.Phonemes,
        F0 = i.F0,
        Mgc = i.Mgc,
        Bap = i.Bap,
        EditedF0 = i.EditedF0,
        EditedDynamics = i.EditedDynamics,
    };

    public bool HasScoreTiming() => !(this.FullTiming is null && this.MonoTiming is null);

    internal void RaiseFeatureChanged() => this.FeatureChanged?.Invoke(this, EventArgs.Empty);

    void INeutrinoTrack.RaiseFeatureChanged() => this.RaiseFeatureChanged();

    private async Task Load()
    {
        var session = this.Project.Session;
        // TODO: 設定から取得する
        bool isBulkEstimation = true;

        // Label
        if (!this.HasScoreTiming())
        {
            var result = await session.NeutrinoV1.ConvertMusicXmlToTiming(new ConvertMusicXmlToTimingOption { MusicXml = this.MusicXml });
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
            var result = await session.NeutrinoV1.GetTiming(this);
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
            if (isBulkEstimation && estimatePhrases.Count() == this.Phrases.Length)
                session.AddEstimateQueue(this);
            else
                session.AddEstimateQueue(this, estimatePhrases);

            var synthesisPhrases = this.Phrases.Where(p => p.F0?.Any() ?? false);
            if (isBulkEstimation && synthesisPhrases.Count() == this.Phrases.Length)
                session.AddAudioRenderQueue(this);
            else
                session.AddAudioRenderQueue(this, synthesisPhrases);
        }
    }

    public bool HasTimings() => this.Timings.Any();

    public long GetTotalFramesCount()
    {
        var timings = this.Timings;

        return timings.Length > 0
            ? (int)Math.Ceiling(timings.Last().EditedEndTime100Ns / 10000d / 5d)
            : 0;
    }

    public void SetRawPhrase(string phrases)
    {
        var (raw, voices) = NeutrinoUtil.ParsePhrases(phrases, this.Timings,
                (int no, int beginTime, int endTime, string[][] label, PhraseStatus status) => new NeutrinoV1Phrase(no, beginTime, endTime, label, status));

        (this.RawPhrases, this.Phrases) = (raw, voices);
    }

    public void ChangeTiming(int timingIndex, int timeMs)
    {
        var timings = this.Timings;

        // タイミング変更により影響を受けるフレーズを取得
        var rawPhrases = this.RawPhrases;
        var phrases = this.Phrases;

        // 再推論対象のフレーズ
        var reProcessPhrases = new List<NeutrinoV1Phrase>();

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

    private static NeutrinoV1Phrase FindPhrase(IEnumerable<NeutrinoV1Phrase> phrase, int no)
        => phrase.First(p => p.No == no);

    /// <summary>
    /// 前フレーズと隣接するタイミングを変更する
    /// </summary>
    /// <param name="timeMs"></param>
    /// <param name="rawPhrases"></param>
    /// <param name="phrases"></param>
    /// <param name="phraseIdx"></param>
    /// <returns></returns>
    private IEnumerable<NeutrinoV1Phrase> ChangeTimingWithPrevPhrase(int timeMs, PhraseInfo[] rawPhrases, NeutrinoV1Phrase[] phrases, int phraseIdx)
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
    private IEnumerable<NeutrinoV1Phrase> ChangeTimingWithNextPhrase(int timeMs, PhraseInfo[] rawPhrases, NeutrinoV1Phrase[] phrases, int phraseIdx)
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
    private IEnumerable<NeutrinoV1Phrase> ChangeTimingInPhrase(PhraseInfo[] rawPhrases, NeutrinoV1Phrase[] phrases, int phraseIdx)
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

    /// <summary>
    /// 編集中情報を反映して再度 音声合成する。変更箇所がない場合は処理しない。
    /// </summary>
    public void ReSynseEditing()
    {
        var phrases = this.Phrases.Where(p => p.IsAudioFeatureEditing()).ToArray();
        if (phrases.Length < 1)
            return;

        foreach (var p in phrases)
            ((INeutrinoPhrase)p).DetermineEditingAudioFeatures();

        this.Project.Session.AddAudioRenderQueue(this, phrases);
    }

    /// <summary>
    /// フレーズ情報を取得する。
    /// </summary>
    /// <param name="beginTime">開始時間</param>
    /// <param name="endTime">終了時間</param>
    /// <returns></returns>
    private IEnumerable<NeutrinoV1Phrase> GetPhrases(int beginTime, int endTime)
        => this.Phrases.Where(p => p.BeginTime <= beginTime && endTime <= p.EndTime);

    /// <summary>
    /// F0値を編集する。
    /// </summary>
    /// <param name="beginTime">開始時間</param>
    /// <param name="frequencies">ピッチ</param>
    public void EditF0(int beginTime, double[] frequencies)
    {
        int endTime = beginTime + NeutrinoUtil.FrameIndexToMs(frequencies.Length);

        foreach (var phrase in this.GetPhrases(beginTime, endTime))
            phrase.EditF0(beginTime, frequencies);
    }

    /// <summary>
    /// ピッチに12音階の値を加算する
    /// </summary>
    /// <param name="beginTime"></param>
    /// <param name="pitches"></param>
    public void AddPitch12(int beginTime, double[] pitches)
    {
        int endTime = beginTime + NeutrinoUtil.FrameIndexToMs(pitches.Length);

        foreach (var phrase in this.GetPhrases(beginTime, endTime))
            phrase.AddPitch12(beginTime, pitches);
    }

    /// <summary>
    /// ダイナミクス値を編集する。
    /// </summary>
    /// <param name="beginTime">開始時間</param>
    /// <param name="dynamics">編集値</param>
    public void EditDynamics(int beginTime, double[] dynamics)
    {
        int endTime = beginTime + NeutrinoUtil.FrameIndexToMs(dynamics.Length);

        foreach (var phrase in this.GetPhrases(beginTime, endTime))
            phrase.EditDynamics(beginTime, dynamics);
    }

    /// <summary>
    /// ダイナミクスの編集値に加算する(編集値の範囲は0.0～1.0)
    /// </summary>
    /// <param name="beginTime"></param>
    /// <param name="coeDelta"></param>
    public void AddDynamicsCoe(int beginTime, double[] coeDelta)
    {
        int endTime = beginTime + NeutrinoUtil.FrameIndexToMs(coeDelta.Length);

        foreach (var phrase in this.GetPhrases(beginTime, endTime))
            phrase.AddDynamicsCoe(beginTime, coeDelta);
    }
}
