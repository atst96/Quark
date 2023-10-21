using Quark.Components;
using Quark.Constants;
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
    private bool _isSessionStarted = false;

    private readonly Project _project;

    private volatile Dictionary<EstimatePriority, int> _estimateQueueCount = new();
    private readonly TaskQueue<EstimateQueueInfo, int> _estimateQueue;

    private volatile Dictionary<EstimatePriority, int> _audioRenderQueueCount = new();
    private readonly TaskQueue<EstimateQueueInfo, int> _audioRenderQueue;

    public NeutrinoV1Service NeutrinoV1 { get; }

    public NeutrinoV2Service NeutrinoV2 { get; }

    public ProjectSession(Project project, NeutrinoV1Service neutrinoV1, NeutrinoV2Service neutrinoV2)
    {
        this._project = project;
        this.NeutrinoV1 = neutrinoV1;
        this.NeutrinoV2 = neutrinoV2;
        this._estimateQueue = new(1, this.ProcessEstimateQueue);
        this._audioRenderQueue = new(1, this.ProcessSynthesisQueue);

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

        this.CancelAll();

        return Task.CompletedTask;
    }

    /// <summary>
    /// 指定トラック、指定フレーズの推論処理をすべてキャンセルする
    /// </summary>
    /// <param name="track">トラック</param>
    /// <param name="phrase">フレーズ</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task CancelForEstimate(Func<EstimateQueueInfo, bool> func)
        => Task.WhenAll(this._estimateQueue.Cancel(func), this._audioRenderQueue.Cancel(func));

    /// <summary>
    /// 指定トラック、指定フレーズの推論処理をすべてキャンセルする
    /// </summary>
    /// <param name="track">トラック</param>
    /// <param name="phrase">フレーズ</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CancelForEstimate(INeutrinoTrack track, INeutrinoPhrase phrase)
        => this.CancelForEstimate(t => t.Track == track && t.Phrase == phrase);

    /// <summary>
    /// 指定トラック、指定フレーズの推論処理をすべてキャンセルする
    /// </summary>
    /// <param name="track">トラック</param>
    /// <param name="phrasees">フレーズ</param>
    public void CancelForEstimate(INeutrinoTrack track, IEnumerable<INeutrinoPhrase> phrasees)
        => this.CancelForEstimate(t => t.Track == track && phrasees.Contains(t.Phrase));

    /// <summary>
    /// プロジェクトのすべての推論処理をキャンセルする。
    /// </summary>
    public Task CancelAll()
        => this.CancelForEstimate(i => true);

    /// <summary>
    /// 指定トラックの推論処理をすべてキャンセルする
    /// </summary>
    /// <param name="track">トラック</param>
    public Task CancelAll(INeutrinoTrack track)
        => this.CancelForEstimate(t => t.Track == track);

    public void AddEstimateQueue(INeutrinoTrack track, INeutrinoPhrase? phrase, EstimatePriority priority = EstimatePriority.Sequence)
    {
        if (phrase == null)
            return;

        // 既存の処理をキャンセル
        this.CancelForEstimate(track, phrase);

        this._estimateQueue.Enqueue(new(track, phrase, priority), GetEstimatePriority(priority));

        phrase.SetStatus(PhraseStatus.WaitEstimate);
        track.RaiseFeatureChanged();
    }

    private int GetEstimatePriority<TElement>(Dictionary<EstimatePriority, int> dict, TaskQueue<TElement, int> queue, EstimatePriority priority)
        where TElement : ITaskQueueElement
    {
        int offset = (int)priority;
        if (dict.ContainsKey(priority))
        {
            // 該当のキーが存在する場合

            if (queue.QueueCount == 0)
            {
                // キューが空の場合はカウントをリセットする
                dict[priority] = 0;
                return offset;
            }
            else
            {
                // キューが空でない場合はカウントをインクリメントする
                return offset + (++dict[priority]);
            }
        }
        else
        {
            dict.Add(priority, 0);
            return offset;
        }
    }

    private int GetEstimatePriority(EstimatePriority priority)
        => this.GetEstimatePriority(this._estimateQueueCount, this._estimateQueue, priority);

    public void AddEstimateQueue(INeutrinoTrack track, EstimatePriority priority = EstimatePriority.Sequence)
    {
        var phrases = track.Phrases;

        this.CancelForEstimate(track, phrases);

        foreach (var phrase in phrases)
        {
            phrase.SetStatus(PhraseStatus.WaitEstimate);
        }

        this._estimateQueue.Enqueue(new(track, null, priority), GetEstimatePriority(priority));

        track.RaiseFeatureChanged();
    }

    public void AddEstimateQueue(INeutrinoTrack track, IEnumerable<INeutrinoPhrase> phrases, EstimatePriority priority = EstimatePriority.Sequence)
    {
        this.CancelForEstimate(track, phrases);

        foreach (var phrase in phrases)
        {
            phrase.SetStatus(PhraseStatus.WaitEstimate);
            this._estimateQueue.Enqueue(new(track, phrase, priority), GetEstimatePriority(priority));
        }

        track.RaiseFeatureChanged();
    }

    public void AddAudioRenderQueue(NeutrinoV1Track track, IEnumerable<NeutrinoV1Phrase> phrases, EstimatePriority priority = EstimatePriority.Sequence)
    {
        foreach (var phrase in phrases)
            this._audioRenderQueue.Enqueue(new(track, phrase, priority), GetAudioRenderPriority(priority));

        track.RaiseFeatureChanged();
    }

    private int GetAudioRenderPriority(EstimatePriority priority)
        => this.GetEstimatePriority(this._audioRenderQueueCount, this._audioRenderQueue, priority);

    public void AddAudioRenderQueue(NeutrinoV2Track track, IEnumerable<NeutrinoV2Phrase> phrases, EstimatePriority priority = EstimatePriority.Sequence)
    {
        foreach (var phrase in phrases)
            this._audioRenderQueue.Enqueue(new(track, phrase, priority), GetAudioRenderPriority(priority));

        track.RaiseFeatureChanged();
    }

    public void AddAudioRenderQueue(INeutrinoTrack track, EstimatePriority priority = EstimatePriority.Sequence)
    {
        var phrases = track.Phrases;

        this.CancelForEstimate(track, phrases);

        foreach (var phrase in phrases)
        {
            phrase.SetStatus(PhraseStatus.WaitAudioRender);
        }

        this._audioRenderQueue.Enqueue(new(track, null, priority), GetEstimatePriority(priority));

        track.RaiseFeatureChanged();
    }

    /// <summary>
    /// 推論キューを処理する
    /// </summary>
    /// <param name="queueInfo"></param>
    /// <returns></returns>
    private async Task ProcessEstimateQueue(EstimateQueueInfo queueInfo)
    {
        // セッションが開始されていなければ処理を抜ける
        if (!this._isSessionStarted)
            return;

        if (queueInfo.Track is NeutrinoV1Track v1Track)
        {
            if (queueInfo.Phrase == null)
            {
                // フレーズ指定がない場合はトラック全体を推論する

                foreach (var phrases in v1Track.Phrases)
                    phrases.SetStatus(PhraseStatus.EstimateProcessing);
                v1Track.RaiseFeatureChanged();
                v1Track.IsBusy = true;

                EstimateFeaturesResultV1 result;
                try
                {
                    var task = this.NeutrinoV1.EstimateFeatures(v1Track, cancellationToken: queueInfo.GetCancellationToken());
                    queueInfo.SetTask(task);
                    result = await task.ConfigureAwait(false);
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
                    // 個別の推論として再登録する
                    UpdatePhraseStatus(v1Track, PhraseStatus.EstimateError);
                    this.AddAudioRenderQueue(v1Track, v1Track.Phrases, queueInfo.Priority);
                    return;
                }
                finally
                {
                    queueInfo.ClearRunningTask();
                    v1Track.IsBusy = false;
                }

                double[] f0 = result.F0!;
                double[] mgc = result.Mgc!;
                double[] bap = result.Bap!;

                foreach (var phrase in v1Track.Phrases)
                {
                    int framesCount = f0.Length;

                    int start = Math.Min(framesCount, Math.Max(0, NeutrinoUtil.MsToFrameIndex(phrase.BeginTime)));
                    int end = Math.Min(framesCount, Math.Max(0, NeutrinoUtil.MsToFrameIndex(phrase.EndTime - 1)));

                    phrase.SetAudioFeatures(
                        f0[start..end],
                        mgc[(start * NeutrinoConfig.MgcDimension)..(end * NeutrinoConfig.MgcDimension)],
                        bap[(start * NeutrinoConfig.BapDimension)..(end * NeutrinoConfig.BapDimension)]);
                    phrase.SetStatus(PhraseStatus.WaitAudioRender);
                }
                v1Track.RaiseFeatureChanged();

                this._audioRenderQueue.Enqueue(new(v1Track, null, queueInfo.Priority), this.GetAudioRenderPriority(queueInfo.Priority));
            }
            else
            {
                // フレーズ指定がある場合は単体のフレーズのみ推論する

                var phrase = (NeutrinoV1Phrase)queueInfo.Phrase;

                phrase.SetStatus(PhraseStatus.EstimateProcessing);
                v1Track.RaiseFeatureChanged();

                EstimateFeaturesResultV1 result;
                try
                {
                    var task = this.NeutrinoV1.EstimateFeatures(v1Track, phrase, cancellationToken: queueInfo.GetCancellationToken());
                    queueInfo.SetTask(task);
                    result = await task.ConfigureAwait(false);
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
                finally
                {
                }

                var (_, f0, mgc, bap, phrases) = result;

                phrase.SetAudioFeatures(f0!, mgc!, bap!);
                phrase.SetStatus(PhraseStatus.WaitAudioRender);
                v1Track.RaiseFeatureChanged();

                this._audioRenderQueue.Enqueue(new(v1Track, phrase, queueInfo.Priority), this.GetAudioRenderPriority(queueInfo.Priority));
            }
        }
        else if (queueInfo.Track is NeutrinoV2Track v2Track)
        {
            if (queueInfo.Phrase == null)
            {
                foreach (var phrases in v2Track.Phrases)
                    phrases.SetStatus(PhraseStatus.EstimateProcessing);
                v2Track.RaiseFeatureChanged();
                v2Track.IsBusy = true;

                EstimateFeaturesResultV2 result;
                try
                {
                    var task = this.NeutrinoV2.EstimateFeatures(v2Track, cancellationToken: queueInfo.GetCancellationToken());
                    queueInfo.SetTask(task);
                    result = await task.ConfigureAwait(false);
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
                    // 個別の推論として再登録する
                    UpdatePhraseStatus(v2Track, PhraseStatus.EstimateError);
                    this.AddAudioRenderQueue(v2Track, v2Track.Phrases, queueInfo.Priority);
                    return;
                }
                finally
                {
                    queueInfo.ClearRunningTask();
                    v2Track.IsBusy = false;
                }

                float[] f0 = result.F0!;
                float[] mspec = result.Mspec!;
                float[] mgc = result.Mgc!;
                float[] bap = result.Bap!;

                foreach (var phrase in v2Track.Phrases)
                {
                    int framesCount = f0.Length;

                    int start = Math.Min(framesCount, Math.Max(0, NeutrinoUtil.MsToFrameIndex(phrase.BeginTime)));
                    int end = Math.Min(framesCount, Math.Max(0, NeutrinoUtil.MsToFrameIndex(phrase.EndTime - 1)));

                    phrase.SetAudioFeatures(
                        f0[start..end],
                        mspec[(start * NeutrinoConfig.MspecDimension)..(end * NeutrinoConfig.MspecDimension)],
                        mgc[(start * NeutrinoConfig.MgcDimension)..(end * NeutrinoConfig.MgcDimension)],
                        bap[(start * NeutrinoConfig.BapDimension)..(end * NeutrinoConfig.BapDimension)]);
                    phrase.SetStatus(PhraseStatus.WaitAudioRender);
                }
                v2Track.RaiseFeatureChanged();

                this._audioRenderQueue.Enqueue(new(v2Track, null, queueInfo.Priority), this.GetAudioRenderPriority(queueInfo.Priority));
            }
            else
            {
                var phrase = (NeutrinoV2Phrase)queueInfo.Phrase;

                phrase.SetStatus(PhraseStatus.EstimateProcessing);
                v2Track.RaiseFeatureChanged();

                EstimateFeaturesResultV2 result;
                try
                {
                    var task = this.NeutrinoV2.EstimateFeatures(v2Track, phrase, cancellationToken: queueInfo.GetCancellationToken());
                    queueInfo.SetTask(task);
                    result = await task.ConfigureAwait(false);
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
                finally
                {
                    queueInfo.ClearRunningTask();
                }

                var (_, f0, mspec, mgc, bap, phrases) = result;

                phrase.SetAudioFeatures(f0!, mspec!, mgc!, bap!);
                phrase.SetStatus(PhraseStatus.WaitAudioRender);
                v2Track.RaiseFeatureChanged();

                this._audioRenderQueue.Enqueue(new(v2Track, phrase, queueInfo.Priority), this.GetAudioRenderPriority(queueInfo.Priority));
            }
        }
    }

    /// <summary>
    /// 音声合成キューを処理する
    /// </summary>
    /// <param name="queueInfo"></param>
    private async Task ProcessSynthesisQueue(EstimateQueueInfo queueInfo)
    {
        // セッションが開始されていなければ処理を抜ける
        if (!this._isSessionStarted)
            return;

        if (queueInfo.Track is NeutrinoV1Track v1Track)
        {
            if (queueInfo.Phrase == null)
            {
                // フレーズの指定がない場合はトラック全体を合成する
                UpdatePhraseStatus(v1Track, PhraseStatus.AudioRenderProcessing);
                v1Track.RaiseFeatureChanged();
                v1Track.IsBusy = true;

                // 音声データを出力する
                byte[]? data;
                try
                {
                    var task = this.NeutrinoV1.SynthesisWorld(v1Track, cancellationToken: queueInfo.GetCancellationToken());
                    // var task = this.NeutrinoV1.SynthesisNSF(v1Track, cancellationToken: info.GetCancellationToken());
                    queueInfo.SetTask(task);
                    data = await task.ConfigureAwait(false);
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
                    UpdatePhraseStatus(v1Track, PhraseStatus.AudioRenderError);
                    v1Track.RaiseFeatureChanged();
                    return;
                }
                finally
                {
                    queueInfo.ClearRunningTask();
                    v1Track.IsBusy = false;
                }

                // 出力した音声データをキャッシュに書き込む
                // WAVEファイルとして出力されているので、WAVEヘッダ部分を取り除いて書き込む。
                var wav = v1Track.WaveData;
                wav.Clear();
                wav.Write(0, WavUtil.AsSpanData(data));

                UpdatePhraseStatus(v1Track, PhraseStatus.Complete);
                v1Track.RaiseFeatureChanged();
            }
            else
            {
                var phrase = (NeutrinoV1Phrase)queueInfo.Phrase;

                phrase.SetStatus(PhraseStatus.AudioRenderProcessing);
                v1Track.RaiseFeatureChanged();

                // 音声データを出力する
                byte[]? data;
                try
                {
                    var task = this.NeutrinoV1.SynthesisWorld(phrase, cancellationToken: queueInfo.GetCancellationToken());
                    // var task = this.NeutrinoV1.SynthesisNSF(phrase, v1Track.Singer, cancellationToken: info.GetCancellationToken());
                    queueInfo.SetTask(task);
                    data = await task.ConfigureAwait(false);
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
                finally
                {
                    queueInfo.ClearRunningTask();
                }

                // 出力した音声データをキャッシュに書き込む
                // WAVEファイルとして出力されているので、WAVEヘッダ部分を取り除いて書き込む。
                v1Track.WaveData.Write(
                    WavUtil.CalcDataPosition48k16bitMono(phrase.BeginTime),
                    WavUtil.AsSpanData(data));

                phrase.SetStatus(PhraseStatus.Complete);
                v1Track.RaiseFeatureChanged();
            }
        }
        else if (queueInfo.Track is NeutrinoV2Track v2Track)
        {
            if (queueInfo.Phrase == null)
            {
                // フレーズの指定がない場合はトラック全体を合成する
                UpdatePhraseStatus(v2Track, PhraseStatus.AudioRenderProcessing);
                v2Track.RaiseFeatureChanged();
                v2Track.IsBusy = true;

                // 音声データを出力する
                byte[]? data;
                try
                {
                    //var task = this.NeutrinoV2.SynthesisWorld(v2Track, cancellationToken: info.GetCancellationToken());
                    var task = this.NeutrinoV2.SynthesisNSF(v2Track, cancellationToken: queueInfo.GetCancellationToken());
                    queueInfo.SetTask(task);
                    data = await task.ConfigureAwait(false);
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
                    UpdatePhraseStatus(v2Track, PhraseStatus.AudioRenderError);
                    v2Track.RaiseFeatureChanged();
                    return;
                }
                finally
                {
                    queueInfo.ClearRunningTask();
                    v2Track.IsBusy = false;
                }

                // 出力した音声データをキャッシュに書き込む
                // WAVEファイルとして出力されているので、WAVEヘッダ部分を取り除いて書き込む。
                var wav = v2Track.WaveData;
                wav.Clear();
                wav.Write(0, WavUtil.AsSpanData(data));

                UpdatePhraseStatus(v2Track, PhraseStatus.Complete);
                v2Track.RaiseFeatureChanged();
            }
            else
            {
                var phrase = (NeutrinoV2Phrase)queueInfo.Phrase;

                phrase.SetStatus(PhraseStatus.AudioRenderProcessing);
                v2Track.RaiseFeatureChanged();

                // 音声データを出力する
                byte[]? data;
                try
                {
                    //var task = this.NeutrinoV2.SynthesisWorld(phrase, cancellationToken: info.GetCancellationToken());
                    var task = this.NeutrinoV2.SynthesisNSF(v2Track, phrase, cancellationToken: queueInfo.GetCancellationToken());
                    queueInfo.SetTask(task);
                    data = await task.ConfigureAwait(false);
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
                finally
                {
                }

                // 出力した音声データをキャッシュに書き込む
                // WAVEファイルとして出力されているので、WAVEヘッダ部分を取り除いて書き込む。
                v2Track.WaveData.Write(
                    WavUtil.CalcDataPosition48k16bitMono(phrase.BeginTime),
                    WavUtil.AsSpanData(data));

                phrase.SetStatus(PhraseStatus.Complete);
                v2Track.RaiseFeatureChanged();
            }
        }
    }

    /// <summary>
    /// トラック内の全フレーズのステータスを更新する。
    /// </summary>
    /// <param name="track">トラック</param>
    /// <param name="status">遷移先ステータス</param>
    private static void UpdatePhraseStatus(INeutrinoTrack track, PhraseStatus status)
    {
        foreach (var phrase in track.Phrases)
        {
            phrase.SetStatus(status);
        }
    }
}
