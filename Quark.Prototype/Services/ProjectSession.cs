using Quark.Models.Neutrino;
using Quark.Projects;
using Quark.Projects.Neutrino;
using Quark.Projects.Tracks;
using Quark.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Quark.Services;

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

    private readonly Queue<EstimateQueueInfo> _estimateQueue = new();

    private readonly NeutrinoV1Service _neutrinoV1;

    public ProjectSession(Project project, NeutrinoV1Service neutrinoV1)
    {
        this.Project = project;
        this._neutrinoV1 = neutrinoV1;
    }

    public void BeginSession()
    {
        if (this._isSessionStarted)
            return;

        this._isSessionStarted = true;

        this.ProcessEstimateQueue();
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

        this.ProcessEstimateQueue();
    }

    /// <summary>キュー操作時ロック用のオブジェクト</summary>
    private object @_queue = new();

    private void ProcessEstimateQueue()
    {
        // セッションが開始されていなければ処理を抜ける
        if (!this._isSessionStarted)
            return;

        EstimateQueueInfo info;
        lock (this._queue)
            if (!this._estimateQueue.TryDequeue(out info!))
                return;

        var track = (NeutrinoV1Track)info.Track;
        var features = (AudioFeaturesV1)info.AudioFeatures;
        var phrase = info.Phrase;

        phrase.SetStatus(PhraseStatus.EstimateProcessing);
        track.RaiseFeatureChanged();

        this._neutrinoV1.EstimateFeatures(track, features, phrase)
            .ContinueWith(t =>
            {
                if (t.Status == TaskStatus.RanToCompletion)
                {
                    // 推論完了時
                    var (f0, mgc, bap) = t.Result!;

                    phrase.SetAudioFeatures(f0, mgc, bap);
                    phrase.SetStatus(PhraseStatus.WaitAudioRender);
                    track.RaiseFeatureChanged();

                    this.RenderAudio(track, phrase);
                }
                else
                {
                    Debug.WriteLine(t.Exception);
                    Debugger.Break();

                    // TODO: 失敗時
                    phrase.SetStatus(PhraseStatus.EstimateError);
                    track.RaiseFeatureChanged();
                }

                this.ProcessEstimateQueue();
            });
    }


    /// <summary>
    /// TODO: キューで管理sするように変更
    /// </summary>
    /// <param name="track"></param>
    /// <param name="phrase"></param>
    public void RenderAudio(NeutrinoV1Track track, PhraseInfo2 phrase)
    {
        phrase.SetStatus(PhraseStatus.AudioRenderProcessing);
        track.RaiseFeatureChanged();

        // 音声データを出力する
        var data = this._neutrinoV1.OutputPreviewWav(phrase)
            .WaitForResult();

        // 出力した音声データをキャッシュに書き込む
        // WAVEファイルとして出力されているので、WAVEヘッダ部分を取り除いて書き込む。
        track.WaveData.Write(
            WavUtil.CalcDataPosition48k16bitMono(phrase.BeginTime),
            data.AsSpan(WavUtil.WaveHeaderSize));

        phrase.SetStatus(PhraseStatus.Complete);
        track.RaiseFeatureChanged();
    }
}
