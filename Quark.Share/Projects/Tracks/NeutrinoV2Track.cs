﻿using NAudio.Wave;
using Quark.Audio;
using Quark.Components;
using Quark.Data.Projects.Neutrino;
using Quark.Data.Projects.Tracks;
using Quark.Data.Settings;
using Quark.Extensions;
using Quark.Models;
using Quark.Models.Neutrino;
using Quark.Models.Scores;
using Quark.Neutrino;
using Quark.Projects.Tracks.Base;
using Quark.Services;
using Quark.Utils;

namespace Quark.Projects.Tracks;

internal class NeutrinoV2Track : AudioTrackBase, INeutrinoTrack
{
    private readonly Settings _settings = ServiceLocator.GetService<SettingService>().Settings;

    /// <summary><inheritdoc/></summary>
    public event EventHandler TimingEstimated;

    /// <summary><inheritdoc/></summary>
    public event EventHandler FeatureChanged;

    public ModelInfo Singer { get; set; }

    /// <summary><inheritdoc/></summary>
    public ScoreInfo Score { get; }

    public byte[]? FullTiming { get; set; }

    public byte[]? MonoTiming { get; set; }

    /// <summary><inheritdoc/></summary>
    public TimingInfo[] Timings { get; set; } = [];

    /// <summary><inheritdoc/></summary>
    public int Duration { get; private set; }

    public PhraseInfo[] RawPhrases { get; private set; } = [];

    public NeutrinoV2Phrase[] Phrases { get; private set; } = [];

    INeutrinoPhrase[] INeutrinoTrack.Phrases => this.Phrases;

    public EstimateMode EstimateMode { get; private set; } = EstimateMode.Fast;

    public WaveData WaveData { get; } = new();

    /// <summary>全フレーズのタイミング情報</summary>
    public PhraseTiming[] PhraseTimings { get; private set; }

    /// <summary>有声フレーズ情報(無声フレーズは含めない)</summary>
    public NeutrinoV2Phrase2[] Phrases2 { get; private set; }

    public NeutrinoV2Track(Project project, string trackName, string musicXml, ModelInfo model) : base(project, trackName)
    {
        this.Singer = model;
        this.Score = MusicXmlUtil.Parse(musicXml);

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

        this.Score = MusicXmlUtil.Parse(config.MusicXml);
        this.FullTiming = config.FullTiming;
        this.MonoTiming = config.MonoTiming;

        var features = config.Features;

        this.Timings = features.Timing ?? [];
        if (this.Timings is { Length: > 0 } timings)
        {
            this.Duration = NeutrinoUtil.TimingTimeToMs(timings[^1].OriginEndTime100Ns);
        }
        else
        {
            // TODO: MusicXMLからDurationを取得する
        }

        this.RawPhrases = features.RawPhraseInfo ?? [];
        this.Phrases = features.Phrases.Select(p =>
        {
            var ph = new NeutrinoV2Phrase(p.No, p.BeginTime, p.EndTime, p.Phonemes, PhraseStatus.Complete);

            if (p.F0?.Any() ?? false)
            {
                ph.SetAudioFeatures(features.EstimateMode, p.F0!, p.Mspec!, p.Mgc!, p.Bap!);
                ph.SetEdited(p.EditedF0, p.EditedDynamics);
                ph.SetStatus(PhraseStatus.WaitAudioRender);
            }

            return ph;

        }).ToArray();

        this.EstimateMode = features.EstimateMode;

        this.IsMute = config.IsMute;
        // this.IsSolo = config.IsSolo;
        this.Volume = config.Volume;

        _ = this.Load();
    }

    protected override WaveStream LoadAudioStream()
        => new WaveDataStream(this.WaveData);

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
            EstimateMode = this.EstimateMode,
        };

        return new NeutrinoV2TrackConfig(this.TrackId, this.TrackName, this.CreateMusicXml(), this.FullTiming, this.MonoTiming, this.Singer?.ModelId, features)
        {
            IsMute = this.IsMute,
            // IsSolo = this.IsSolo,
            Volume = this.Volume,
        };
    }

    public bool HasScoreTiming() => !(this.FullTiming is null && this.MonoTiming is null);

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
            var result = await session.NeutrinoV2.ConvertMusicXmlToTiming(new() { MusicXml = this.CreateMusicXml() });
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
            this.Duration = NeutrinoUtil.TimingTimeToMs(this.Timings[^1].OriginEndTime100Ns);
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
    private IEnumerable<NeutrinoV2Phrase> GetPhrases(int beginTime, int endTime)
        => this.Phrases.Where(p => beginTime <= p.EndTime && p.BeginTime < endTime);

    /// <summary>
    /// F0値を編集する。
    /// </summary>
    /// <param name="beginTime">開始時間</param>
    /// <param name="frequencies">ピッチ</param>
    public void EditF0(int beginTime, Span<float> frequencies)
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
    public void AddPitch12(int beginTime, Span<float> pitches)
    {
        int endTime = beginTime + NeutrinoUtil.FrameIndexToMs(pitches.Length);

        var phrases = this.GetPhrases(beginTime, endTime);
        foreach (var phrase in phrases)
            phrase.AddPitch12(beginTime, pitches);
    }

    /// <summary>
    /// ダイナミクス値を編集する。
    /// </summary>
    /// <param name="beginTime">開始時間</param>
    /// <param name="dynamics">編集値</param>
    public void EditDynamics(int beginTime, Span<float> dynamics)
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
    public void AddDynamicsCoe(int beginTime, float[] coeDelta)
    {
        int endTime = beginTime + NeutrinoUtil.FrameIndexToMs(coeDelta.Length);

        var phrases = this.GetPhrases(beginTime, endTime);
        foreach (var phrase in phrases)
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
