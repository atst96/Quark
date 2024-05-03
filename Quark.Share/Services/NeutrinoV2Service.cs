using System.Runtime.CompilerServices;
using System.Text;
using Quark.Components;
using Quark.Constants;
using Quark.Data;
using Quark.Data.Projects;
using Quark.Data.Settings;
using Quark.DependencyInjection;
using Quark.Extensions;
using Quark.Models.Neutrino;
using Quark.Neutrino;
using Quark.Projects.Tracks;
using Quark.Utils;

namespace Quark.Services;

/// <summary>
///  NEUTRINO(v21)を操作するためのサービス
/// TODO: NSFの処理が一部未実装
/// </summary>
[Singleton]
internal class NeutrinoV2Service
{
    /// <summary>設定情報</summary>
    private Settings _setting;

    /// <summary>binディレクトリのファイルパス</summary>
    private const string BinDirName = "bin";

    /// <summary>モデルディレクトリのファイルパス</summary>
    private const string ModelDirName = "model";

    /// <summary>musicXMLtoLabelのファイルパス</summary>
    private static readonly string MusicXmlExe = Path.Combine(BinDirName, "musicXMLtoLabel.exe");

    /// <summary>NEUTRINOのファイルパス</summary>
    private static readonly string NeutrinoExe = Path.Combine(BinDirName, "NEUTRINO.exe");

    /// <summary>NSFのファイルパス</summary>
    private static readonly string NsfExe = Path.Combine(BinDirName, "NSF.exe");

    /// <summary>WORLDのファイルパス</summary>
    private static readonly string WorldExe = Path.Combine(BinDirName, "WORLD.exe");

    /// <summary>データ受信タスクをキャンセルするまでの時間</summary>
    private static TimeSpan DataReceiveTaskCancelDuration { get; } = TimeSpan.FromSeconds(10);

    /// <summary>NSFモデルの規定値</summary>
    private static readonly NSFV2Model DefaultNSFModel = NSFV2Model.VA;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="settingService">アプリケーション設定情報</param>
    public NeutrinoV2Service(SettingService settingService)
    {
        this._setting = settingService.Settings;
    }

    private string GetModelDir() => Path.Combine(this.GetNeutrinoWorkingDirectory(), "model");

    private string GetModelPath(string modelId)
        => Path.Combine(this.GetModelDir(), modelId) + Path.DirectorySeparatorChar.ToString();

    /// <summary>
    /// CPUスレッド数を取得する
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int? GetCpuThreads()
        => this._setting.Synthesis.CpuThreads;

    /// <summary>
    /// GPUを使用するかどうかを取得する
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool GetUseGpu()
        => this._setting.Synthesis.UseGpu;

    /// <summary>
    /// モデル情報を取得する
    /// </summary>
    public IList<ModelInfo> GetModels()
    {
        var modelDir = this.GetModelDir();

        return string.IsNullOrEmpty(modelDir)
            ? new List<ModelInfo>()
            : NeutrinoModelUtil.GetModelsV2(modelDir, ModelType.NeutorinoV2);
    }

    /// <summary>NEUTRINOの作業ディレクトリを取得する</summary>
    private string GetNeutrinoWorkingDirectory() => this._setting.NeutrinoV2.Directory!;

    /// <summary>
    /// NEUTRINO内のファイルパスを取得する
    /// </summary>
    /// <param name="relativePath">相対パス</param>
    /// <returns>ファイルパス</returns>
    private string GetNeutrinoPath(string relativePath)
        => Path.Combine(this.GetNeutrinoWorkingDirectory(), relativePath);

    /// <summary>NEUTRINOと送受信するテキストデータの文字コード</summary>
    private static readonly Encoding OutputTextEncoding = new UTF8Encoding(false);

    /// <summary>
    /// MusicXMLをタイミング情報に変換する
    /// </summary>
    /// <param name="option">変換オプション</param>
    /// <returns>出力結果</returns>
    public async Task<ConvertScoreToTimingResult?> ConvertMusicXmlToTiming(ConvertMusicXmlToTimingOption option)
    {
        using (var musicXmlFile = PipeFile.CreateReadOnly(FileExtensions.MusicXml))
        using (var fullLabFile = PipeFile.CreateWriteOnly(FileExtensions.Label))
        using (var monoLabFile = PipeFile.CreateWriteOnly(FileExtensions.Label))
        {
            // 実行ファイル
            var command = this.GetNeutrinoPath(MusicXmlExe);

            // コマンドライン引数の作成
            var args = new StringBuilder();
            args.Append($@"""{musicXmlFile.Path}"" ""{fullLabFile.Path}"" ""{monoLabFile.Path}""");

            if (!string.IsNullOrEmpty(option.Directory))
                args.Append(" -x ").Append(option.Directory);

            // ファイル受信処理のキャンセラ
            using var receiveTaskCanceler = new CancellationTokenSource();

            // MusicXMLファイルを書込み
            var musicXmlFileTask = musicXmlFile.WriteAllBytesAsync(OutputTextEncoding.GetBytes(option.MusicXml), close: true, receiveTaskCanceler.Token);

            // 出力ファイルの読み取り開始
            var fullLabFileTask = fullLabFile.ReadAllBytesAsync(receiveTaskCanceler.Token);
            var monoLabFileTask = monoLabFile.ReadAllBytesAsync(receiveTaskCanceler.Token);

            try
            {
                // musicXMLtoLabelを実行
                await NeutrinoUtil.Execute(command, args.ToString(), this.GetNeutrinoWorkingDirectory()).ConfigureAwait(false);
            }
            finally
            {
                // 一定時間待機しても終了していなければキャンセルして、タスク終了を永遠に待ち続けるのを防ぐ
                receiveTaskCanceler.CancelAfter(DataReceiveTaskCancelDuration);
            }

            // 出力情報を取得
            (byte[] fullLabel, byte[] monoLabel) = await TaskUtil.WhenAll(fullLabFileTask, monoLabFileTask).ConfigureAwait(false);
            await musicXmlFileTask.ConfigureAwait(false);

            return new(fullLabel, monoLabel);
        }
    }

    /// <summary>
    /// 音響情報の推論処理を行う
    /// </summary>
    /// <param name="option">推論オプション</param>
    /// <param name="progress">進捗通知</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns></returns>
    public async Task<EstimateFeaturesResultV2> EstimateFeatures(NeutrinoV2Option option, IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default)
    {
        PipeFile? mgcFile = null;
        PipeFile? bapFile = null;
        PipeFile? receivePhraseFile = null;
        PipeFile? receiveTimingFile = null;

        using (var fullLabFile = TempFile.CreateReadOnly(FileExtensions.Label))
        using (var f0File = PipeFile.CreateWriteOnly(FileExtensions.F0))
        using (var mspecFile = PipeFile.CreateWriteOnly(FileExtensions.Melspec))
        using (var additionalDisposables = new DisposableCollection(5))
        {
            // 一時ファイルのタイミング情報を書き込む
            fullLabFile.Write(option.FullLabel);

            // 実行ファイル
            var command = this.GetNeutrinoPath(NeutrinoExe);

            // コマンドライン引数の作成

            string timingFilePath;
            if (option.IsSkipTimingPrediction)
            {
                // タイミングの推論をスキップする場合はタイミング情報ファイルを渡す
                var sendTimingFile = TempFile.CreateReadOnly(FileExtensions.Label);
                additionalDisposables.Add(sendTimingFile);
                timingFilePath = sendTimingFile.Path;

                await sendTimingFile.WriteAsync(option.TimingLabel ?? []).ConfigureAwait(false);
            }
            else
            {
                // タイミングの推論を行う場合はタイミング情報ファイルを受け取る
                receiveTimingFile = PipeFile.CreateWriteOnly(FileExtensions.Label);
                additionalDisposables.Add(receiveTimingFile);
                timingFilePath = receiveTimingFile.Path;
            }

            var args = new StringBuilder();
            args.Append($@"""{fullLabFile.Path}"" ""{timingFilePath}"" ""{f0File.Path}"" ""{mspecFile.Path}"" {this.GetModelPath(option.ModelInfo.ModelId)}");

            if (option.NumberOfParallel != null)
                args.Append(" -n ").Append(option.NumberOfParallel);

            if (option.NumberOfParallelInSession != null)
                args.Append(" -o ").Append(option.NumberOfParallelInSession);

            if (option.InferenceMode != null)
                args.Append(" -d ").Append((int)option.InferenceMode);

            if (option.StyleShift != null)
                args.Append(" -k ").Append(option.StyleShift);

            if (option.RandomSeed != null)
                args.Append(" -r ").Append(option.RandomSeed);

            if (option.IsSkipTimingPrediction)
                args.Append(" -s");

            if (option.IsSkipAcousticFeaturesPrediction)
                args.Append(" -a");

            if (option.WorldFeaturesPrediction)
            {
                mgcFile = PipeFile.CreateWriteOnly(FileExtensions.Mgc);
                additionalDisposables.Add(mgcFile);

                bapFile = PipeFile.CreateWriteOnly(FileExtensions.Bap);
                additionalDisposables.Add(bapFile);

                args.Append(" -w ").AppendDoubleQuoted(mgcFile.Path).Append(' ').AppendDoubleQuoted(bapFile.Path);
            }

            if (option.SinglePhrasePrediction != null)
                args.Append(" -p ").Append(option.SinglePhrasePrediction);

            if (option.UseSingleGpu != null)
                args.Append(" -g ").Append(option.UseSingleGpu);

            if (option.UseMultipleGpus)
                args.Append(" -m");

            if (option.IsSkipTimingPrediction)
            {
                if (option.EstimatedPhrases != null)
                {
                    var sendPhraseFile = TempFile.CreateReadOnly(FileExtensions.Text);
                    await sendPhraseFile.WriteAsync(option.EstimatedPhrases).ConfigureAwait(false);

                    additionalDisposables.Add(sendPhraseFile);
                }
            }

            if (option.IsTracePhraseInformation)
            {
                receivePhraseFile = PipeFile.CreateWriteOnly(FileExtensions.Text);
                additionalDisposables.Add(receivePhraseFile);

                args.Append($@" -i ""{receivePhraseFile.Path}""");
            }

            if (option.IsViewInformation)
                args.Append(" -t");

            // ファイル受信処理のキャンセラ
            using var receiveTaskCanceler = new CancellationTokenSource();

            // ファイル出力を非同期で待機
            var timingTask = receiveTimingFile?.ReadAllBytesAsync(receiveTaskCanceler.Token) ?? Task.FromResult<byte[]>(null!);

            Task<byte[]> f0Task, mspecTask, mgcTask, bapTask;
            if (option.IsSkipAcousticFeaturesPrediction)
            {
                (f0Task, mspecTask, mgcTask, bapTask)
                    = (TaskUtil.NullByteArrayTask!, TaskUtil.NullByteArrayTask!, TaskUtil.NullByteArrayTask!, TaskUtil.NullByteArrayTask!);
            }
            else
            {
                f0Task = f0File.ReadAllBytesAsync(receiveTaskCanceler.Token)!;
                mspecTask = mspecFile.ReadAllBytesAsync(receiveTaskCanceler.Token)!;
                mgcTask = mgcFile?.ReadAllBytesAsync(receiveTaskCanceler.Token) ?? TaskUtil.NullByteArrayTask!;
                bapTask = bapFile?.ReadAllBytesAsync(receiveTaskCanceler.Token) ?? TaskUtil.NullByteArrayTask!;
            }

            var phraseTask = receivePhraseFile?.ReadAllBytesAsync(receiveTaskCanceler.Token) ?? TaskUtil.NullByteArrayTask!;

            try
            {
                // NEUTRINO実行
                await NeutrinoUtil.Execute(command, args.ToString(), this.GetNeutrinoWorkingDirectory(), progress, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                // 一定時間待機しても終了していなければキャンセルして、タスク終了を永遠に待ち続けるのを防ぐ
                receiveTaskCanceler.CancelAfter(DataReceiveTaskCancelDuration);
            }

            // 書込みデータの受信待機
            var (timing, f0, mspec, mgc, bap, phrase) = await TaskUtil.WhenAll(timingTask, f0Task, mspecTask, mgcTask, bapTask, phraseTask)
                .ConfigureAwait(false);

            return new(
                    timing == null ? null : OutputTextEncoding.GetString(timing),
                    f0 == null ? null : DataConvertUtil.Convert<float>(f0),
                    mspec == null ? null : DataConvertUtil.Convert<float>(mspec),
                    mgc == null ? null : DataConvertUtil.Convert<float>(mgc),
                    bap == null ? null : DataConvertUtil.Convert<float>(bap),
                    phrase == null ? null : OutputTextEncoding.GetString(phrase));
        }
    }

    /// <summary>
    ///タイミング情報を取得する
    /// </summary>
    /// <param name="track">トラック情報</param>
    /// <param name="progress">進捗通知</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns></returns>
    public Task<EstimateTimingResult> GetTiming(NeutrinoV2Track track, IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default)
    {
        var setting = this._setting.NeutrinoV2;

        var option = new NeutrinoV2Option()
        {
            FullLabel = track.FullTiming!,
            ModelInfo = track.Singer,
            IsSkipAcousticFeaturesPrediction = true,
            IsTracePhraseInformation = true,
            NumberOfParallel = this.GetCpuThreads(),
            UseMultipleGpus = this.GetUseGpu(),
            IsViewInformation = true,
        };

        return EstimateFeatures(option, progress, cancellationToken)
            .ContinueWith(t =>
            {
                var result = t.Result;
                return new EstimateTimingResult(result.Timing!, result.Phrases!);
            },
            TaskContinuationOptions.OnlyOnRanToCompletion);
    }

    /// <summary>
    /// 音響情報の推論処理を行う
    /// </summary>
    /// <param name="track">トラック情報</param>
    /// <param name="progress">進捗通知</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns></returns>
    public Task<EstimateFeaturesResultV2> EstimateFeatures(NeutrinoV2Track track,
        NeutrinoV2InferenceMode inferenceMode = NeutrinoV2InferenceMode.Advanced,
        IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default)
    {
        return this.EstimateFeatures(new NeutrinoV2Option()
        {
            FullLabel = track.FullTiming!,
            TimingLabel = NeutrinoUtil.GetTimingContent(track.Timings),
            InferenceMode = inferenceMode,
            ModelInfo = track.Singer,
            IsSkipTimingPrediction = true,
            IsTracePhraseInformation = true,
            WorldFeaturesPrediction = true,
            UseMultipleGpus = this.GetUseGpu(),
            NumberOfParallel = this.GetCpuThreads(),
            IsViewInformation = true,
        }
        , progress, cancellationToken);
    }

    /// <summary>
    /// 音響情報の推論処理を行う
    /// </summary>
    /// <param name="track">トラック情報</param>
    /// <param name="phrase">フレーズ情報</param>
    /// <param name="progress">進捗通知</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns></returns>
    public Task<EstimateFeaturesResultV2> EstimateFeatures(NeutrinoV2Track track, NeutrinoV2Phrase phrase,
        NeutrinoV2InferenceMode inferenceMode = NeutrinoV2InferenceMode.Advanced,
        IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default)
    {
        return this.EstimateFeatures(new NeutrinoV2Option()
        {
            FullLabel = track.FullTiming!,
            TimingLabel = NeutrinoUtil.GetTimingContent(track.Timings),
            InferenceMode = inferenceMode,
            ModelInfo = track.Singer,
            IsSkipTimingPrediction = true,
            IsTracePhraseInformation = true,
            SinglePhrasePrediction = phrase.No,
            WorldFeaturesPrediction = true,
            UseMultipleGpus = this.GetUseGpu(),
            NumberOfParallel = this.GetCpuThreads(),
            IsViewInformation = true,
        }
        , progress, cancellationToken);
    }

    /// <summary>
    /// WORLDで音声合成する
    /// </summary>
    /// <param name="option">合成オプション</param>
    /// <param name="progress">進捗通知</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns>WAVファイルデータ</returns>
    public async Task<byte[]> SynthesisWorld(WorldV2Option option, IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default)
    {
        using (var f0File = TempFile.CreateReadOnly(FileExtensions.F0))
        using (var mgcFile = TempFile.CreateReadOnly(FileExtensions.Mgc))
        using (var bapFile = TempFile.CreateReadOnly(FileExtensions.Bap))
        using (var wavFile = PipeFile.CreateWriteOnly(FileExtensions.Wave))
        {

            // 実行ファイル
            var command = this.GetNeutrinoPath(WorldExe);

            // コマンドライン引数の作成
            var args = new StringBuilder();
            args.Append($@"""{f0File.Path}"" ""{mgcFile.Path}"" ""{bapFile.Path}"" ""{wavFile.Path}""");

            if (option.PitchShift != null)
                args.Append(" -f ").Append(option.PitchShift);

            if (option.FormantShift != null)
                args.Append(" -m ").Append(option.FormantShift);

            if (option.NumberOfParallel != null)
                args.Append(" -n ").Append(option.NumberOfParallel);

            if (option.IsRealtimeSynthesis)
                args.Append(" -r");

            if (option.SmoothPitch != null)
                args.Append(" -p ").Append(option.SmoothPitch);

            if (option.SmoothFormant != null)
                args.Append(" -c ").Append(option.SmoothFormant);

            if (option.EnhanceBreathiness != null)
                args.Append(" -b ").Append(option.EnhanceBreathiness);

            if (option.IsViewInformation)
                args.Append(" -t");

            // ファイル受信処理のキャンセラ
            using var receiveTaskCanceler = new CancellationTokenSource();

            // 一時ファイルの情報を書き込む
            // HACK: ToAraryしないようにする
            var t1 = StructArrayStream.CopyToAsync(option.F0, f0File);
            var t2 = StructArrayStream.CopyToAsync(option.Mgc, mgcFile);
            var t3 = StructArrayStream.CopyToAsync(option.Bap, bapFile);
            await t1.ConfigureAwait(false);
            await t2.ConfigureAwait(false);
            await t3.ConfigureAwait(false);

            // ファイル出力を非同期で待機
            var wavTask = wavFile.ReadAllBytesAsync(receiveTaskCanceler.Token);

            try
            {
                // WORLD実行
                await NeutrinoUtil.Execute(command, args.ToString(), this.GetNeutrinoWorkingDirectory(), progress, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                // 一定時間待機しても終了していなければキャンセルして、タスク終了を永遠に待ち続けるのを防ぐ
                receiveTaskCanceler.CancelAfter(DataReceiveTaskCancelDuration);
            }

            // 出力ファイルの内容を返却
            return await wavTask.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// WORKDで音声合成する
    /// </summary>
    /// <param name="track">トラック情報</param>
    /// <param name="progress">進捗通知</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns>WAVファイルデータ</returns>
    public Task<byte[]> SynthesisWorld(NeutrinoV2Track track, IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default)
    {
        (float[] f0, float[] mgc, float[] bap) = GetAudioFeaturesForWorld(track);

        return this.SynthesisWorld(new WorldV2Option
        {
            F0 = f0,
            Mgc = mgc,
            Bap = bap,
            NumberOfParallel = this.GetCpuThreads(),
            IsViewInformation = true,
        }
        , progress, cancellationToken);
    }

    /// <summary>
    /// WORKDで音声合成する
    /// </summary>
    /// <param name="phrase">フレーズ情報</param>
    /// <param name="progress">進捗通知</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns>WAVファイルデータ</returns>
    public Task<byte[]> SynthesisWorld(NeutrinoV2Phrase phrase, IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default)
    {
        return this.SynthesisWorld(new WorldV2Option
        {
            F0 = phrase.F0!,
            Mgc = phrase.Mgc!,
            Bap = phrase.Bap!,
            NumberOfParallel = this.GetCpuThreads(),
            IsViewInformation = true,
        }
        , progress, cancellationToken);
    }

    /// <summary>
    /// WORKDで音声合成する
    /// </summary>
    /// <param name="track">トラック</param>
    /// <param name="path">出力先</param>
    /// <param name="progress">進捗通知</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns>WAVファイルデータ</returns>
    public async Task SynthesisWorld(NeutrinoV2Track track, string path, IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default)
    {
        var timings = track.Timings;
        var phrases = track.Phrases;

        if (timings.Count == 0 || phrases.Length == 0)
            return;

        (float[] f0, float[] mgc, float[] bap) = GetAudioFeaturesForWorld(track);

        // 音声出力
        byte[] data = await this.SynthesisWorld(new WorldV2Option
        {
            F0 = f0,
            Mgc = mgc,
            Bap = bap,
            NumberOfParallel = this.GetCpuThreads(),
            IsViewInformation = true,
        }
        , progress, cancellationToken).ConfigureAwait(false);

        // ファイルに書き出し
        File.WriteAllBytes(path, data);
    }

    private static (float[] f0, float[] mgc, float[] bap) GetAudioFeaturesForWorld(NeutrinoV2Track track)
    {
        var timings = track.Timings;
        var phrases = track.Phrases;

        int mgcDimension = NeutrinoConfig.MgcDimension;
        int bapDimension = NeutrinoConfig.BapDimension;
        int frameCount = NeutrinoUtil.MsToFrameIndex(timings[^1].EditedTimeMs);

        // F0
        float[] f0 = new float[frameCount];
        // スペクトル包絡(※各フレームの先頭の値の初期値を-60.0とする)
        float[] mgc = ArrayUtil.CreateAndInitSegmentFirst(frameCount, mgcDimension, NeutrinoConfig.MgcLowerF);
        // 非同期成分
        float[] bap = new float[frameCount * bapDimension];

        // 各フレーズの音響情報を配列にコピーする
        foreach (var phrase in phrases)
        {
            int count = NeutrinoUtil.MsToFrameIndex(phrase.EndTime - phrase.BeginTime);
            if (count <= 0)
                continue;

            int frameIdx = NeutrinoUtil.MsToFrameIndex(phrase.BeginTime);

            var srcF0 = phrase.F0;
            if (srcF0?.Length > 0)
                ArrayUtil.CopyTo(srcF0, 0, f0, frameIdx, count, 1);

            var srcMgc = phrase.Mgc;
            if (srcMgc?.Length > 0)
                ArrayUtil.CopyTo(srcMgc, 0, mgc, frameIdx, count, mgcDimension);

            var srcBap = phrase.Bap;
            if (srcBap?.Length > 0)
                ArrayUtil.CopyTo(srcBap, 0, bap, frameIdx, count, bapDimension);
        }

        return (f0, mgc, bap);
    }

    /// <summary>
    /// NSFで音声合成を行う
    /// </summary>
    /// <param name="option">合成オプション</param>
    /// <param name="progress">進捗通知</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns>WAVファイルデータ</returns>
    public async Task<byte[]> SynthesisNSF(NSFV2Option option, IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default)
    {
        using (var f0File = TempFile.CreateReadOnly(FileExtensions.F0))
        using (var mspecFile = TempFile.CreateReadOnly(FileExtensions.Melspec))
        using (var wavFile = PipeFile.CreateWriteOnly(FileExtensions.Wave))
        using (var additionalDisposable = new DisposableCollection())
        {
            // 実行ファイル
            var command = this.GetNeutrinoPath(NsfExe);

            // コマンドライン引数の作成
            var args = new StringBuilder();
            args.Append($@"""{f0File.Path}"" ""{mspecFile.Path}"" "".\model\{option.Model.ModelId}\{option.ModelType.FileName}"" {wavFile.Path}");

            if (option.SamplingRate != null)
                args.Append(" -s ").Append(option.SamplingRate);

            if (option.PitchShift != null)
                args.Append(" -f ").Append(option.PitchShift);

            if (option.NumberOfParallel != null)
                args.Append(" -n ").Append(option.NumberOfParallel);

            if (option.NumberOfParallelInSession != null)
                args.Append(" -p ").Append(option.NumberOfParallelInSession);

            if (option.MultiPhrasePrediction?.Count > 0)
            {
                // labファイルの作成
                var timingFile = TempFile.CreateReadOnly(FileExtensions.Label);
                timingFile.Write(NeutrinoUtil.GetTimingContent(option.MultiPhrasePrediction));
                additionalDisposable.Add(timingFile);

                args.Append(" -l ").AppendDoubleQuoted(timingFile.Path);
            }

            if (option.UseSingleGpu != null)
                args.Append(" -g ").Append(option.UseSingleGpu);

            if (option.UseMultiGpus)
                args.Append(" -m");

            if (option.IsViewInformation)
                args.Append(" -t");

            // ファイル受信処理のキャンセラ
            using var receiveTaskCanceler = new CancellationTokenSource();

            // 一時ファイルの情報を書き込む
            await Task.WhenAll([
                StructArrayStream.CopyToAsync(option.F0, f0File),
                StructArrayStream.CopyToAsync(option.Melspec, mspecFile),
            ]).ConfigureAwait(false);

            // ファイル出力を非同期で待機
            var wavTask = wavFile.ReadAllBytesAsync(receiveTaskCanceler.Token);

            try
            {
                // NSF実行
                await NeutrinoUtil.Execute(command, args.ToString(), this.GetNeutrinoWorkingDirectory(), progress, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                // 一定時間待機しても終了していなければキャンセルして、タスク終了を永遠に待ち続けるのを防ぐ
                receiveTaskCanceler.CancelAfter(DataReceiveTaskCancelDuration);
            }

            // 出力ファイルの内容を返却
            return await wavTask.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// NSFで音声合成を行う
    /// </summary>
    /// <param name="track">トラック情報</param>
    /// <param name="model">推論モード</param>
    /// <param name="progress">進捗通知</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns>WAVファイルデータ</returns>
    public Task<byte[]> SynthesisNSF(NeutrinoV2Track track,
        NSFV2Model? model = null,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        (float[] f0, float[] mspec) = GetAudioFeatures(track);

        return this.SynthesisNSF(new NSFV2Option
        {
            F0 = f0,
            Melspec = mspec,
            Model = track.Singer,
            ModelType = model ?? DefaultNSFModel,
            SamplingRate = 48,
            MultiPhrasePrediction = track.Timings,
            NumberOfParallel = this.GetCpuThreads(),
            UseMultiGpus = this.GetUseGpu(),
            IsViewInformation = true,
        }
        , progress, cancellationToken);
    }

    /// <summary>
    /// NSFで音声合成を行う
    /// </summary>
    /// <param name="track">トラック情報</param>
    /// <param name="phrase">フレーズ情報</param>
    /// <param name="model">推論モード</param>
    /// <param name="progress">進捗通知</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns>WAVファイルデータ</returns>
    public Task<byte[]> SynthesisNSF(NeutrinoV2Track track, NeutrinoV2Phrase phrase,
        NSFV2Model? model = null,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return this.SynthesisNSF(new NSFV2Option
        {
            F0 = phrase.GetEditedF0()!,
            Melspec = phrase.GetEditedMspec()!,
            Model = track.Singer,
            ModelType = model ?? DefaultNSFModel,
            SamplingRate = 48,
            NumberOfParallel = this.GetCpuThreads(),
            UseMultiGpus = this.GetUseGpu(),
            IsViewInformation = true,
        }
        , progress, cancellationToken);
    }

    /// <summary>
    /// トラック全体をNSFで音声合成を行う
    /// </summary>
    /// <param name="track">トラック情報</param>
    /// <param name="path">出力先</param>
    /// <param name="model">推論モード</param>
    /// <param name="progress">進捗通知</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns></returns>
    public async Task SynthesisNSF(NeutrinoV2Track track, string path,
        NSFV2Model? model = null,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var timings = track.Timings;
        var phrases = track.Phrases;

        if (timings.Count == 0 || phrases.Length == 0)
            return;

        (float[] f0, float[] mspec) = GetAudioFeatures(track);

        // 音声出力
        byte[] data = await this.SynthesisNSF(new NSFV2Option
        {
            F0 = f0,
            Melspec = mspec,
            Model = track.Singer,
            ModelType = model ?? DefaultNSFModel,
            SamplingRate = 48,
            NumberOfParallel = this.GetCpuThreads(),
            UseMultiGpus = this.GetUseGpu(),
            MultiPhrasePrediction = timings,
            IsViewInformation = true,
        }
        , progress, cancellationToken).ConfigureAwait(false);

        // ファイルに書き出し
        File.WriteAllBytes(path, data);
    }

    private static (float[], float[]) GetAudioFeatures(NeutrinoV2Track track)
    {
        var timings = track.Timings;
        var phrases = track.Phrases;

        const int mspecDimension = NeutrinoConfig.MspecDimension;

        int frameCount = NeutrinoUtil.MsToFrameIndex(timings[^1].EditedTimeMs);

        // F0
        float[] f0 = new float[frameCount];
        // メルスペクトログラム
        float[] mspec = new float[frameCount * mspecDimension];

        // 各フレーズの音響情報を配列にコピーする
        foreach (var phrase in phrases)
        {
            int length = NeutrinoUtil.MsToFrameIndex(phrase.EndTime - phrase.BeginTime);
            if (length <= 0)
                continue;

            int frameIdx = NeutrinoUtil.MsToFrameIndex(phrase.BeginTime);

            var srcF0 = phrase.GetEditedF0();
            if (srcF0?.Length > 0)
                ArrayUtil.CopyTo(srcF0, 0, f0, frameIdx, length, 1);

            var srcMspec = phrase.GetEditedMspec();
            if (srcMspec?.Length > 0)
                ArrayUtil.CopyTo(srcMspec, 0, mspec, frameIdx, length, mspecDimension);
        }

        return (f0, mspec);
    }
}
