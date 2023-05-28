using Quark.Components;
using Quark.DependencyInjection;
using Quark.Models.Neutrino;
using Quark.Neutrino;
using Quark.Projects;
using Quark.Projects.Neutrino;
using Quark.Projects.Tracks;
using Quark.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Quark.Services;

[Singleton]
internal class ProjectSession
{
    private class EstimateQueueInfo
    {
        // TODO: クラスを整理する
        public TrackBase Track { get; }

        public object AudioFeatures { get; }

        public PhraseInfo2 Phrase { get; }

        public EstimateQueueInfo(TrackBase track, object audioFeature, PhraseInfo2 phrase)
        {
            this.Track = track;
            this.AudioFeatures = audioFeature;
            this.Phrase = phrase;
        }
    }

    private bool _isSessionStarted = false;

    public Project Project { get; }

    private readonly TaskQueue<EstimateQueueInfo> _estimateQueue;

    private readonly TaskQueue<EstimateQueueInfo> _audioRenderQueue;

    private readonly NeutrinoV1Service _neutrinoV1;

    public ProjectSession(Project project, NeutrinoV1Service neutrinoV1)
    {
        this.Project = project;
        this._neutrinoV1 = neutrinoV1;
        this._estimateQueue = new(1, this.ProcessEstimateQueue);
        this._audioRenderQueue = new(1, this.ProcessAudioRenderQueue);
    }

    public void BeginSession()
    {
        if (this._isSessionStarted)
            return;

        this._isSessionStarted = true;

        this._estimateQueue.BeginSession();
        this._audioRenderQueue.BeginSession();
    }

    public Task EndSession()
    {
        this._isSessionStarted = false;
        return Task.CompletedTask;
    }

    public void AddEstimateQueue(NeutrinoV1Track track, AudioFeaturesV1 audioFeature, IEnumerable<PhraseInfo2> phrases)
    {
        foreach (var phrase in phrases)
            this._estimateQueue.Enqueue(new(track, audioFeature, phrase));

        track.RaiseFeatureChanged();
    }

    public void AddAudioRenderQueue(NeutrinoV1Track track, AudioFeaturesV1 audioFeature, IEnumerable<PhraseInfo2> phrases)
    {
        foreach (var phrase in phrases)
            this._audioRenderQueue.Enqueue(new(track, audioFeature, phrase));

        track.RaiseFeatureChanged();
    }

    /// <summary>キュー操作時ロック用のオブジェクト</summary>
    private object @_queue = new();

    private async Task ProcessEstimateQueue(EstimateQueueInfo info)
    {
        // セッションが開始されていなければ処理を抜ける
        if (!this._isSessionStarted)
            return;

        var track = (NeutrinoV1Track)info.Track;
        var features = (AudioFeaturesV1)info.AudioFeatures;
        var phrase = info.Phrase;

        phrase.SetStatus(PhraseStatus.EstimateProcessing);
        track.RaiseFeatureChanged();

        EstimateFeaturesResultV1? result;
        try
        {
            result = await this._neutrinoV1.EstimateFeatures(track, features, phrase).ConfigureAwait(false);
        }
        catch (AggregateException aex) when (aex.InnerException is TaskCanceledException)
        {
            // pass
            return;
        }
        catch (Exception ex)
        {
            // 例外発生時
            Debug.WriteLine($"{ex.Message}: {ex.StackTrace}");
            Debugger.Break();

            // TODO: 失敗時
            phrase.SetStatus(PhraseStatus.EstimateError);
            track.RaiseFeatureChanged();
            return;
        }

        if (result is null)
        {
            // TODO: 取得失敗時の処理
        }
        else
        {
            var (f0, mgc, bap) = result;

            phrase.SetAudioFeatures(f0, mgc, bap);
            phrase.SetStatus(PhraseStatus.WaitAudioRender);
            track.RaiseFeatureChanged();

            this._audioRenderQueue.Enqueue(new(track, features, phrase));
        }
    }

    /// <summary>
    /// </summary>
    /// <param name="track"></param>
    /// <param name="phrase"></param>
    private async Task ProcessAudioRenderQueue(EstimateQueueInfo info)
    {
        // セッションが開始されていなければ処理を抜ける
        if (!this._isSessionStarted)
            return;

        var track = (NeutrinoV1Track)info.Track;
        var features = (AudioFeaturesV1)info.AudioFeatures;
        var phrase = info.Phrase;

        phrase.SetStatus(PhraseStatus.AudioRenderProcessing);
        track.RaiseFeatureChanged();

        // 音声データを出力する
        byte[]? data;
        try
        {
            data = await this._neutrinoV1.OutputPreviewWav(phrase).ConfigureAwait(false);
        }
        catch (AggregateException aex) when (aex.InnerException is TaskCanceledException)
        {
            // pass
            return;
        }
        catch (Exception ex)
        {
            // 実行中に例外発生時
            Debug.WriteLine($"{ex.Message}: {ex.StackTrace}");

            // TODO: 失敗時
            phrase.SetStatus(PhraseStatus.AudioRenderError);
            track.RaiseFeatureChanged();
            return;
        }


        if (data is null)
        {
            // TODO: 取得失敗時の処理
        }
        else
        {
            // 出力した音声データをキャッシュに書き込む
            // WAVEファイルとして出力されているので、WAVEヘッダ部分を取り除いて書き込む。
            track.WaveData.Write(
                WavUtil.CalcDataPosition48k16bitMono(phrase.BeginTime),
                data.AsSpan(WavUtil.WaveHeaderSize));

            phrase.SetStatus(PhraseStatus.Complete);
            track.RaiseFeatureChanged();
        }
    }
}
