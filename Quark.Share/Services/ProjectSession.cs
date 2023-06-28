using Quark.Components;
using Quark.DependencyInjection;
using Quark.Models.Neutrino;
using Quark.Neutrino;
using Quark.Projects;
using Quark.Projects.Tracks;
using Quark.Utils;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Quark.Services;

[Singleton]
internal class ProjectSession
{
    private class EstimateQueueInfo
    {
        // TODO: クラスを整理する

        /// <summary>トラック</summary>
        public INeutrinoTrack Track { get; }

        /// <summary>フレーズ情報</summary>
        public INeutrinoPhrase Phrase { get; }

        /// <summary>キャンセル用トークン</summary>
        private readonly CancellationTokenSource _tokenSource = new();

        public EstimateQueueInfo(INeutrinoTrack track, INeutrinoPhrase phrase)
        {
            this.Track = track;
            this.Phrase = phrase;
        }

        /// <summary>キャンセル用トークンを取得する</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CancellationToken GetCancellationToken() => this._tokenSource.Token;

        /// <summary>現在のタスクをキャンセルする</summary>
        public void Cancel()
            => this._tokenSource.Cancel();
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
        this._audioRenderQueue = new(1, this.ProcessAudioRenderQueue);

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

    /// <summary>
    /// キューに登録したタスクを現在実行中のものを含めて列挙する
    /// </summary>
    /// <typeparam name="T">型情報</typeparam>
    /// <param name="queue">取得対象のキュー</param>
    /// <returns>タスク情報</returns>
    private IEnumerable<T> EnumerateQueue<T>(TaskQueue<T> queue)
        => queue.Runnings.Concat(queue);

    /// <summary>
    /// 推論用のタスクキューを現在実行中のものも含めて列挙する
    /// </summary>
    /// <returns>タスク情報</returns>
    private IEnumerable<EstimateQueueInfo> EnumerateEstimateQueue()
        => this.EnumerateQueue(this._estimateQueue).Concat(this.EnumerateAudioQueue());

    /// <summary>
    /// 音声合成用のタスクキューを現在実行中のものも含めて列挙する
    /// </summary>
    /// <returns>タスク情報</returns>
    private IEnumerable<EstimateQueueInfo> EnumerateAudioQueue()
        => this.EnumerateQueue(this._audioRenderQueue);

    /// <summary>
    /// 指定トラック、指定フレーズの推論処理をすべてキャンセルする
    /// </summary>
    /// <param name="track">トラック</param>
    /// <param name="phrase">フレーズ</param>
    public void CancelForEstimate(INeutrinoTrack track, INeutrinoPhrase phrase)
    {
        var queues = this.EnumerateEstimateQueue()
            .Where(t => t.Track == track && t.Phrase == phrase);

        foreach (var queue in queues)
            queue.Cancel();
    }

    /// <summary>
    /// 指定トラック、指定フレーズの推論処理をすべてキャンセルする
    /// </summary>
    /// <param name="track">トラック</param>
    /// <param name="phrase">フレーズ</param>
    public void CancelForEstimate(INeutrinoTrack track, IEnumerable<INeutrinoPhrase> phrase)
    {
        var queues = this.EnumerateEstimateQueue()
            .Where(t => t.Track == track && phrase.Contains(t.Phrase));

        foreach (var queue in queues)
            queue.Cancel();
    }

    /// <summary>
    /// 指定トラックの推論処理をすべてキャンセルする
    /// </summary>
    /// <param name="track">トラック</param>
    public void CancelForEstimateAll(INeutrinoTrack track)
    {
        var queues = this.EnumerateEstimateQueue()
            .Where(t => t.Track == track);

        foreach (var queue in queues)
            queue.Cancel();
    }

    public void AddEstimateQueue(INeutrinoTrack track, INeutrinoPhrase? phrase)
    {
        if (phrase == null)
            return;

        // 既存の処理をキャンセル
        this.CancelForEstimate(track, phrase);

        this._estimateQueue.Enqueue(new(track, phrase));

        phrase.SetStatus(PhraseStatus.WaitEstimate);
        track.RaiseFeatureChanged();
    }

    public void AddEstimateQueue(INeutrinoTrack track, IEnumerable<INeutrinoPhrase> phrases)
    {
        this.CancelForEstimate(track, phrases);

        foreach (var phrase in phrases)
        {
            phrase.SetStatus(PhraseStatus.WaitEstimate);
            this._estimateQueue.Enqueue(new(track, phrase));
        }

        track.RaiseFeatureChanged();
    }

    public void AddAudioRenderQueue(NeutrinoV1Track track, IEnumerable<NeutrinoV1Phrase> phrases)
    {
        foreach (var phrase in phrases)
            this._audioRenderQueue.Enqueue(new(track, phrase));

        track.RaiseFeatureChanged();
    }

    public void AddAudioRenderQueue(NeutrinoV2Track track, IEnumerable<NeutrinoV2Phrase> phrases)
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
            var phrase = (NeutrinoV1Phrase)info.Phrase;

            phrase.SetStatus(PhraseStatus.EstimateProcessing);
            v1Track.RaiseFeatureChanged();

            EstimateFeaturesResultV1 result;
            try
            {
                result = await this.NeutrinoV1.EstimateFeatures(
                    v1Track, phrase, cancellationToken: info.GetCancellationToken()).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // キャンセル時は何もしない
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

            var (_, f0, mgc, bap, phrases) = result;

            phrase.SetAudioFeatures(f0!, mgc!, bap!);
            phrase.SetStatus(PhraseStatus.WaitAudioRender);
            v1Track.RaiseFeatureChanged();

            this._audioRenderQueue.Enqueue(new(v1Track, phrase));
        }
        else if (info.Track is NeutrinoV2Track v2Track)
        {
            var phrase = (NeutrinoV2Phrase)info.Phrase;

            phrase.SetStatus(PhraseStatus.EstimateProcessing);
            v2Track.RaiseFeatureChanged();

            EstimateFeaturesResultV2 result;
            try
            {
                result = await this.NeutrinoV2.EstimateFeatures(
                    v2Track, phrase, cancellationToken: info.GetCancellationToken()).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // キャンセル時は何もしない
                return;
            }
            catch (Exception ex)
            {
                // 例外発生時
                Debug.WriteLine($"{ex.Message}: {ex.StackTrace}");
                Debugger.Break();

                // TODO: 失敗時
                phrase.SetStatus(PhraseStatus.EstimateError);
                v2Track.RaiseFeatureChanged();
                return;
            }

            var (_, f0, mspec, mgc, bap, phrases) = result;

            phrase.SetAudioFeatures(f0!, mspec!, mgc!, bap!);
            phrase.SetStatus(PhraseStatus.WaitAudioRender);
            v2Track.RaiseFeatureChanged();

            this._audioRenderQueue.Enqueue(new(v2Track, phrase));
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
            var phrase = (NeutrinoV1Phrase)info.Phrase;

            phrase.SetStatus(PhraseStatus.AudioRenderProcessing);
            v1Track.RaiseFeatureChanged();

            // 音声データを出力する
            byte[]? data;
            try
            {
                data = await this.NeutrinoV1.SynthesisWorld(
                    phrase, cancellationToken: info.GetCancellationToken()).ConfigureAwait(false);
                // data = await this.NeutrinoV1.SynthesisNSF(phrase, v1Track.Singer,
                //     cancellationToken: info.GetCancellationToken()).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // キャンセル時は何もしない
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

            // 出力した音声データをキャッシュに書き込む
            // WAVEファイルとして出力されているので、WAVEヘッダ部分を取り除いて書き込む。
            v1Track.WaveData.Write(
                WavUtil.CalcDataPosition48k16bitMono(phrase.BeginTime),
                data.AsSpan(WavUtil.WaveHeaderSize));

            phrase.SetStatus(PhraseStatus.Complete);
            v1Track.RaiseFeatureChanged();
        }
        else if (info.Track is NeutrinoV2Track v2Track)
        {
            var phrase = (NeutrinoV2Phrase)info.Phrase;

            phrase.SetStatus(PhraseStatus.AudioRenderProcessing);
            v2Track.RaiseFeatureChanged();

            // 音声データを出力する
            byte[]? data;
            try
            {
                // data = await this.NeutrinoV2.SynthesisWorld(
                //     phrase, cancellationToken: info.GetCancellationToken()).ConfigureAwait(false);
                data = await this.NeutrinoV2.SynthesisNSF(
                    v2Track, phrase, cancellationToken: info.GetCancellationToken()).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // キャンセル時は何もしない
                return;
            }
            catch (Exception ex)
            {
                // 実行中に例外発生時
                Debug.WriteLine($"{ex.Message}: {ex.StackTrace}");

                // TODO: 失敗時
                phrase.SetStatus(PhraseStatus.AudioRenderError);
                v2Track.RaiseFeatureChanged();
                return;
            }

            // 出力した音声データをキャッシュに書き込む
            // WAVEファイルとして出力されているので、WAVEヘッダ部分を取り除いて書き込む。
            v2Track.WaveData.Write(
                WavUtil.CalcDataPosition48k16bitMono(phrase.BeginTime),
                data.AsSpan(WavUtil.WaveHeaderSize));

            phrase.SetStatus(PhraseStatus.Complete);
            v2Track.RaiseFeatureChanged();
        }
    }
}
