using Quark.Components;
using Quark.DependencyInjection;
using Quark.Models.Neutrino;
using Quark.Neutrino;
using Quark.Projects;
using Quark.Projects.Tracks;
using Quark.Utils;
using System.Diagnostics;

namespace Quark.Services;

[Singleton]
internal class ProjectSession
{
    private class EstimateQueueInfo
    {
        // TODO: クラスを整理する
        public INeutrinoTrack Track { get; }

        public NeutrinoV1Phrase Phrase { get; }

        public EstimateQueueInfo(INeutrinoTrack track, NeutrinoV1Phrase phrase)
        {
            this.Track = track;
            this.Phrase = phrase;
        }
    }

    private bool _isSessionStarted = false;

    public Project Project { get; }

    private readonly TaskQueue<EstimateQueueInfo> _estimateQueue;

    private readonly TaskQueue<EstimateQueueInfo> _audioRenderQueue;

    public NeutrinoV1Service NeutrinoV1 { get; }

    public NeutrinoV2Service NeutrinoV2 { get; }

    public ProjectSession(Project project, NeutrinoV1Service neutrinoV1, NeutrinoV2Service neutrinoV2)
    {
        this.Project = project;
        this.NeutrinoV1 = neutrinoV1;
        this.NeutrinoV2 = neutrinoV2;
        this._estimateQueue = new(1, this.ProcessEstimateQueue);
        this._audioRenderQueue = new(2, this.ProcessAudioRenderQueue);

        this.BeginSession();
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

    public void AddEstimateQueue(NeutrinoV1Track track, IEnumerable<NeutrinoV1Phrase> phrases)
    {
        foreach (var phrase in phrases)
            this._estimateQueue.Enqueue(new(track, phrase));

        track.RaiseFeatureChanged();
    }

    public void AddAudioRenderQueue(NeutrinoV1Track track, IEnumerable<NeutrinoV1Phrase> phrases)
    {
        foreach (var phrase in phrases)
            this._audioRenderQueue.Enqueue(new(track, phrase));

        track.RaiseFeatureChanged();
    }

    private async Task ProcessEstimateQueue(EstimateQueueInfo info)
    {
        // セッションが開始されていなければ処理を抜ける
        if (!this._isSessionStarted)
            return;

        if (info.Track is NeutrinoV1Track v1Track)
        {
            var phrase = info.Phrase;

            phrase.SetStatus(PhraseStatus.EstimateProcessing);
            v1Track.RaiseFeatureChanged();

            EstimateFeaturesResultV1? result;
            try
            {
                result = await this.NeutrinoV1.EstimateFeatures(v1Track, phrase).ConfigureAwait(false);
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
                v1Track.RaiseFeatureChanged();
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
                v1Track.RaiseFeatureChanged();

                this._audioRenderQueue.Enqueue(new(v1Track, phrase));
            }
        }
        else if (info.Track is NeutrinoV2Track v2Track)
        {
            // TODO: v2
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

        if (info.Track is NeutrinoV1Track v1Track)
        {
            var phrase = info.Phrase;

            phrase.SetStatus(PhraseStatus.AudioRenderProcessing);
            v1Track.RaiseFeatureChanged();

            // 音声データを出力する
            byte[]? data;
            try
            {
                data = await this.NeutrinoV1.OutputPreviewWavWorld(phrase).ConfigureAwait(false);
                //data = await this.NeutrinoV1.OutputPreviewWavNsf(phrase, features).ConfigureAwait(false);
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
                v1Track.RaiseFeatureChanged();
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
                v1Track.WaveData.Write(
                    WavUtil.CalcDataPosition48k16bitMono(phrase.BeginTime),
                    data.AsSpan(WavUtil.WaveHeaderSize));

                phrase.SetStatus(PhraseStatus.Complete);
                v1Track.RaiseFeatureChanged();
            }
        }
        else if (info.Track is NeutrinoV2Track v2Track)
        {
            // TODO: v2
        }
    }
}
