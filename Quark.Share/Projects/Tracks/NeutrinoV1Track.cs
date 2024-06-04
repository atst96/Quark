using NAudio.Wave;
using Quark.Audio;
using Quark.Components;
using Quark.Data.Projects.Neutrino;
using Quark.Data.Projects.Tracks;
using Quark.Data.Settings;
using Quark.Extensions;
using Quark.Models.Neutrino;
using Quark.Models.Scores;
using Quark.Neutrino;
using Quark.Projects.Tracks.Base;
using Quark.Services;
using Quark.Utils;

namespace Quark.Projects.Tracks;

internal class NeutrinoV1Track : AudioTrackBase, INeutrinoTrack, IF0PhraseTrack<double>
{
    private Settings _settings = ServiceLocator.GetService<SettingService>().Settings;

    /// <summary><inheritdoc/></summary>
    public event EventHandler TimingEstimated;

    /// <summary><inheritdoc/></summary>
    public event EventHandler FeatureChanged;

    public WaveData WaveData { get; } = new();

    /// <summary><inheritdoc/></summary>
    public ScoreInfo Score { get; }

    public byte[]? FullTiming { get; set; }

    public byte[]? MonoTiming { get; set; }

    /// <inheritdoc/>
    public IReadOnlyList<PhonemeTiming> Timings { get; private set; } = [];

    /// <summary><inheritdoc/></summary>
    public int Duration { get; private set; }

    public PhraseInfo[] RawPhrases { get; private set; } = [];

    public NeutrinoV1Phrase[] Phrases { get; private set; } = [];

    IF0Phrase<double>[] IF0PhraseTrack<double>.Phrases => this.Phrases;

    INeutrinoPhrase[] INeutrinoTrack.Phrases => this.Phrases;

    public EstimateMode EstimateMode { get; private set; } = EstimateMode.Fast;

    public ModelInfo Singer { get; set; }

    public NeutrinoV1Track(Project project, string trackName, string musicXml, ModelInfo model)
        : base(project, trackName)
    {
        this.Singer = model;
        this.Score = MusicXmlUtil.Parse(musicXml);

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

        this.Score = MusicXmlUtil.Parse(config.MusicXml);
        this.FullTiming = config.FullTiming;
        this.MonoTiming = config.MonoTiming;

        var features = config.Features;

        this.RawPhrases = features.RawPhrases;
        this.Phrases = features.Phrases.Select(c => ConvertConfig(features, c)).ToArray();

        var timings = NeutrinoUtil.ConvertTimingFromConfig(features.Timings, this.RawPhrases);
        this.Timings = timings;
        if (timings.Count > 0)
        {
            this.Duration = timings[^1].EditedTimeMs;
        }
        else
        {
            // TODO: MusicXMLからDurationを取得する
        }

        this.EstimateMode = features.EstimateMode;

        this.IsMute = config.IsMute;
        // this.IsSolo = config.IsSolo;
        this.Volume = config.Volume;

        _ = this.Load();
    }

    protected override WaveStream LoadAudioStream()
        => new WaveDataStream(this.WaveData);

    private static NeutrinoV1Phrase ConvertConfig(AudioFeaturesV1Config features, PhraseInfoV1 config)
    {
        bool hasAudioFeatures = config.F0 != null && config.Mgc != null && config.Bap != null;

        var phrase = new NeutrinoV1Phrase(config.No, config.BeginTime, config.EndTime, config.Phonemes, (hasAudioFeatures ? PhraseStatus.WaitAudioRender : PhraseStatus.WaitEstimate));

        if (hasAudioFeatures)
        {
            phrase.SetAudioFeatures(features.EstimateMode, config.F0!, config.Mgc!, config.Bap!);
            phrase.SetEdited(config.EditedF0, config.EditedDynamics);
        }

        return phrase;
    }

    public override TrackBaseConfig GetConfig()
    {
        var config = new AudioFeaturesV1Config(this.Singer.ModelId)
        {
            Timings = NeutrinoUtil.ConvertTimingToConfig(this.Timings),
            RawPhrases = this.RawPhrases,
            Phrases = this.Phrases.Select(i => ToConfig(i)).ToArray(),
            EstimateMode = this.EstimateMode,
        };

        return new NeutrinoV1TrackConfig(this.TrackId, this.TrackName, this.CreateMusicXml(), this.FullTiming, this.MonoTiming, this.Singer?.ModelId, config)
        {
            IsMute = this.IsMute,
            // IsSolo = this.IsSolo,
            Volume = this.Volume,
        };
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

    /// <summary>一括推論をするかどうかを取得する</summary>
    private bool GetIsBulkMode()
        => this._settings.Synthesis.UseBulkEstimate;

    private async Task Load()
    {
        var session = this.Project.Session;
        bool isBulkEstimation = this.GetIsBulkMode();

        // Label
        if (!this.HasScoreTiming())
        {
            var result = await session.NeutrinoV1.ConvertMusicXmlToTiming(new() { MusicXml = this.CreateMusicXml() });
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

            var rawPhrases = NeutrinoUtil.ParseRawPhrase(result.Phrases);
            var timings = NeutrinoUtil.ParseTiming(rawPhrases, result.Timing);
            var phrases = ParsePhrase(rawPhrases, timings);

            (this.RawPhrases, this.Phrases, this.Timings, this.Duration)
                = (rawPhrases, phrases, timings, timings[^1].TimeMs);

            this.TimingEstimated?.Invoke(this, EventArgs.Empty);

            if (isBulkEstimation)
                session.AddEstimateQueue(this);
            else
                session.AddEstimateQueue(this, phrases);
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

        return timings.Count > 0
            ? NeutrinoUtil.MsToFrameIndex(timings[^1].EditedTimeMs)
            : 0;
    }

    public static NeutrinoV1Phrase[] ParsePhrase(IReadOnlyList<PhraseInfo> phrases, IReadOnlyList<PhonemeTiming> timings)
        => NeutrinoUtil.ParsePhrases(phrases, timings,
                (int no, int beginTime, int endTime, string[][] label, PhraseStatus status) => new NeutrinoV1Phrase(no, beginTime, endTime, label, status));

    public void ChangeTiming(PhonemeTiming timing)
    {
        var timings = this.Timings;
        var rawPhrases = this.RawPhrases;
        var phrases = this.Phrases;

        int timeMs = timing.EditedTimeMs;

        // 再推論対象のフレーズ
        // 直前のフレーズと隣接していれば、そのフレーズも対象とする
        var reProcessPhrases = new List<NeutrinoV1Phrase>(2);

        int timingIdx = timings.IndexOf(timing);
        int currentPhraseIdx = timing.PhraseIndex;
        var currentPhrase = FindPhrase(phrases, rawPhrases[currentPhraseIdx].No);

        // 1つ前に音声ありフレーズがある場合は再処理対象とする
        if (timingIdx > 0)
        {
            int prevPhraseIdx = timings[timingIdx - 1].PhraseIndex;
            if (currentPhraseIdx != prevPhraseIdx)
            {
                var prevPhrase = FindPhrase(phrases, rawPhrases[prevPhraseIdx].No);
                if (prevPhrase != null)
                {
                    // 前の音声ありフレームとの境界のタイミングを変更した場合、そのフレーズの終了時間を更新
                    prevPhrase.ChangeEndTime(timeMs);

                    reProcessPhrases.Add(prevPhrase);
                }

                // 現在フレーズが有声ならそのフレーズの開始時間を更新
                currentPhrase?.ChangeBeginTime(timeMs);
            }
        }
        else if (timingIdx == 0)
        {
            // 先頭のタイミングなら現在フレーズの開始時間を更新
            currentPhrase?.ChangeBeginTime(timeMs);
        }

        if (currentPhrase != null)
        {
            // 有声フレーズなら再推論対象
            reProcessPhrases.Add(currentPhrase);
        }

        // 再処理対象フレーズの音響情報をクリア
        foreach (var phrase in reProcessPhrases)
        {
            this.ClearRenderAudio(phrase);
            phrase.ClearAudioFeatures();
        }

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

    /// <summary>
    /// 推論モードを変更する
    /// </summary>
    /// <param name="mode"></param>
    public void ChangeEstimateMode(EstimateMode mode)
    {
        this.EstimateMode = mode;
        bool isBulkMode = this.GetIsBulkMode();

        var pharses = this.Phrases.EnumerateLowerModePhrases(mode);
        if (isBulkMode && pharses.Count() == this.Phrases.Length)
            this.Project.Session.AddEstimateQueue(this);
        else
            this.Project.Session.AddEstimateQueue(this, pharses);
    }
}
