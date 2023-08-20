using System.IO;
using System.Text;
using Quark.Components;
using Quark.Constants;
using Quark.Data;
using Quark.Data.Projects;
using Quark.Data.Settings;
using Quark.DependencyInjection;
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
        using (var musicXmlFile = TempFile.Create(FileAccess.ReadWrite, FileShare.Read, FileExtensions.MusicXml))
        using (var fullLabFile = new VirtualFile(FileExtensions.Label))
        using (var monoLabFile = new VirtualFile(FileExtensions.Label))
        {
            // MusicXMLファイルを書込み
            musicXmlFile.Write(OutputTextEncoding.GetBytes(option.MusicXml));

            // 実行ファイル
            var command = this.GetNeutrinoPath(MusicXmlExe);

            // コマンドライン引数の作成
            var args = new StringBuilder();
            args.Append($@"""{musicXmlFile.Path}"" ""{fullLabFile.FilePath}"" ""{monoLabFile.FilePath}""");

            if (!string.IsNullOrEmpty(option.Directory))
                args.Append(" -x ").Append(option.Directory);

            // ファイル受信処理のキャンセラ
            using var receiveTaskCanceler = new CancellationTokenSource();

            // 出力ファイルの読み取り開始
            var fullLabFileTask = fullLabFile.Read(receiveTaskCanceler.Token);
            var monoLabFileTask = monoLabFile.Read(receiveTaskCanceler.Token);

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
        VirtualFile? mgcFile = null;
        VirtualFile? bapFile = null;
        VirtualFile? receivePhraseFile = null;
        VirtualFile? receiveTimingFile = null;

        using (var fullLabFile = TempFile.Create(FileAccess.Write, FileShare.Read, FileExtensions.Label))
        using (var f0File = new VirtualFile(FileExtensions.F0))
        using (var mspecFile = new VirtualFile(FileExtensions.Melspec))
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
                var sendTimingFile = TempFile.Create(FileAccess.Write, FileShare.Read, FileExtensions.Label);
                additionalDisposables.Add(sendTimingFile);
                timingFilePath = sendTimingFile.Path;

                sendTimingFile.Write(option.TimingLabel);
            }
            else
            {
                receiveTimingFile = new VirtualFile(FileExtensions.Label);
                additionalDisposables.Add(receiveTimingFile);
                timingFilePath = receiveTimingFile.FilePath;
            }

            var args = new StringBuilder();
            args.Append($@"""{fullLabFile.Path}"" ""{timingFilePath}"" ""{f0File.FilePath}"" ""{mspecFile.FilePath}"" {this.GetModelPath(option.ModelInfo.ModelId)}");

            if (option.NumberOfParallel != null)
                args.Append(" -n ").Append(option.NumberOfParallel);

            if (option.NumberOfParallelInSession != null)
                args.Append(" -o ").Append(option.NumberOfParallelInSession);

            if (option.InferenceMode != null)
                args.Append(" -d ").Append((int)option.InferenceMode);

            if (option.StyleShift != null)
                args.Append(" -a ").Append(option.StyleShift);

            if (option.RandomSeed != null)
                args.Append(" -r ").Append(option.RandomSeed);

            if (option.IsSkipTimingPrediction)
                args.Append(" -s");

            if (option.IsSkipAcousticFeaturesPrediction)
                args.Append(" -a");

            if (option.WorldFeaturesPrediction)
            {
                mgcFile = new VirtualFile(FileExtensions.Mgc);
                additionalDisposables.Add(mgcFile);

                bapFile = new VirtualFile(FileExtensions.Bap);
                additionalDisposables.Add(bapFile);

                args.Append(" -w ").AppendDoubleQuoted(mgcFile.FilePath).Append(' ').AppendDoubleQuoted(bapFile.FilePath);
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
                    var sendPhraseFile = TempFile.Create(FileAccess.Write, FileShare.Read, FileExtensions.Text);
                    sendPhraseFile.Write(option.EstimatedPhrases);

                    additionalDisposables.Add(sendPhraseFile);
                }
            }

            if (option.IsTracePhraseInformation)
            {
                receivePhraseFile = new VirtualFile(FileExtensions.Text);
                additionalDisposables.Add(receivePhraseFile);

                args.Append($@" -i ""{receivePhraseFile.FilePath}""");
            }

            if (option.IsViewInformation)
                args.Append(" -t");

            // ファイル受信処理のキャンセラ
            using var receiveTaskCanceler = new CancellationTokenSource();

            // ファイル出力を非同期で待機
            var timingTask = receiveTimingFile?.Read(receiveTaskCanceler.Token) ?? Task.FromResult<byte[]>(null!);

            Task<byte[]> f0Task, mspecTask, mgcTask, bapTask;
            if (option.IsSkipAcousticFeaturesPrediction)
            {
                (f0Task, mspecTask, mgcTask, bapTask)
                    = (TaskUtil.NullByteArrayTask!, TaskUtil.NullByteArrayTask!, TaskUtil.NullByteArrayTask!, TaskUtil.NullByteArrayTask!);
            }
            else
            {
                f0Task = f0File.Read(receiveTaskCanceler.Token)!;
                mspecTask = mspecFile.Read(receiveTaskCanceler.Token)!;
                mgcTask = mgcFile?.Read(receiveTaskCanceler.Token) ?? TaskUtil.NullByteArrayTask!;
                bapTask = bapFile?.Read(receiveTaskCanceler.Token) ?? TaskUtil.NullByteArrayTask!;
            }

            var phraseTask = receivePhraseFile?.Read(receiveTaskCanceler.Token) ?? TaskUtil.NullByteArrayTask!;

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
            NumberOfParallel = setting.CpuThreads,
            UseMultipleGpus = setting.UseGpu,
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
    public Task<EstimateFeaturesResultV2> EstimateFeatures(NeutrinoV2Track track, IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default)
    {
        var setting = this._setting.NeutrinoV2;

        return this.EstimateFeatures(new NeutrinoV2Option()
        {
            FullLabel = track.FullTiming!,
            TimingLabel = NeutrinoUtil.GetTimingContent(track.Timings),
            InferenceMode = NeutrinoV2InferenceMode.Standard,
            ModelInfo = track.Singer,
            IsSkipTimingPrediction = true,
            IsTracePhraseInformation = true,
            WorldFeaturesPrediction = true,
            UseMultipleGpus = setting.UseGpu,
            NumberOfParallel = setting.CpuThreads,
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
    public Task<EstimateFeaturesResultV2> EstimateFeatures(NeutrinoV2Track track, NeutrinoV2Phrase phrase, IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default)
    {
        var setting = this._setting.NeutrinoV2;

        return this.EstimateFeatures(new NeutrinoV2Option()
        {
            FullLabel = track.FullTiming!,
            TimingLabel = NeutrinoUtil.GetTimingContent(track.Timings),
            InferenceMode = NeutrinoV2InferenceMode.Standard,
            ModelInfo = track.Singer,
            IsSkipTimingPrediction = true,
            IsTracePhraseInformation = true,
            SinglePhrasePrediction = phrase.No,
            WorldFeaturesPrediction = true,
            UseMultipleGpus = setting.UseGpu,
            NumberOfParallel = setting.CpuThreads,
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
        using (var f0File = TempFile.Create(FileAccess.Write, FileShare.Read, FileExtensions.F0))
        using (var mgcFile = TempFile.Create(FileAccess.Write, FileShare.Read, FileExtensions.Mgc))
        using (var bapFile = TempFile.Create(FileAccess.Write, FileShare.Read, FileExtensions.Bap))
        using (var wavFile = new VirtualFile(FileExtensions.Wave))
        {

            // 実行ファイル
            var command = this.GetNeutrinoPath(WorldExe);

            // コマンドライン引数の作成
            var args = new StringBuilder();
            args.Append($@"""{f0File.Path}"" ""{mgcFile.Path}"" ""{bapFile.Path}"" ""{wavFile.FilePath}""");

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
            f0File.Write(DataConvertUtil.CastToByte(option.F0));
            mgcFile.Write(DataConvertUtil.CastToByte(option.Mgc));
            bapFile.Write(DataConvertUtil.CastToByte(option.Bap));

            // ファイル出力を非同期で待機
            var wavTask = wavFile.Read(receiveTaskCanceler.Token);

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
        var setting = this._setting.NeutrinoV2;

        (float[] f0, float[] mgc, float[] bap) = GetAudioFeaturesForWorld(track);

        return this.SynthesisWorld(new WorldV2Option
        {
            F0 = f0,
            Mgc = mgc,
            Bap = bap,
            NumberOfParallel = setting.CpuThreads,
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
        var setting = this._setting.NeutrinoV2;

        return this.SynthesisWorld(new WorldV2Option
        {
            F0 = phrase.F0!,
            Mgc = phrase.Mgc!,
            Bap = phrase.Bap!,
            NumberOfParallel = setting.CpuThreads,
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
        var setting = this._setting.NeutrinoV2;

        var timings = track.Timings;
        var phrases = track.Phrases;

        if (timings.Length == 0 || phrases.Length == 0)
            return;

        (float[] f0, float[] mgc, float[] bap) = GetAudioFeaturesForWorld(track);

        // 音声出力
        byte[] data = await this.SynthesisWorld(new WorldV2Option
        {
            F0 = f0,
            Mgc = mgc,
            Bap = bap,
            NumberOfParallel = setting.CpuThreads,
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
        int frameCount = NeutrinoUtil.MsToFrameIndex(NeutrinoUtil.TimingTimeToMs(timings[^1].EditedEndTime100Ns));

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
        using (var f0File = TempFile.Create(FileAccess.Write, FileShare.Read, FileExtensions.F0))
        using (var mspecFile = TempFile.Create(FileAccess.Write, FileShare.Read, FileExtensions.Melspec))
        using (var wavFile = new VirtualFile(FileExtensions.Wave))
        using (var additionalDisposable = new DisposableCollection())
        {
            // 実行ファイル
            var command = this.GetNeutrinoPath(NsfExe);

            // コマンドライン引数の作成
            var args = new StringBuilder();
            args.Append($@"""{f0File.Path}"" ""{mspecFile.Path}"" "".\model\{option.Model.ModelId}\{option.ModelType.FileName}"" {wavFile.FilePath}");

            if (option.SamplingRate != null)
                args.Append(" -s ").Append(option.SamplingRate);

            if (option.PitchShift != null)
                args.Append(" -f ").Append(option.PitchShift);

            if (option.NumberOfParallel != null)
                args.Append(" -n ").Append(option.NumberOfParallel);

            if (option.NumberOfParallelInSession != null)
                args.Append(" -p ").Append(option.NumberOfParallelInSession);

            if (option.MultiPhrasePrediction?.Length > 0)
            {
                // labファイルの作成
                var timingFile = TempFile.Create(FileAccess.Write, FileShare.Read, FileExtensions.Label);
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
            f0File.Write(DataConvertUtil.CastToByte(option.F0));
            mspecFile.Write(DataConvertUtil.CastToByte(option.Melspec));

            // ファイル出力を非同期で待機
            var wavTask = wavFile.Read(receiveTaskCanceler.Token);

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
    /// <param name="progress">進捗通知</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns>WAVファイルデータ</returns>
    public Task<byte[]> SynthesisNSF(NeutrinoV2Track track, IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default)
    {
        var setting = this._setting.NeutrinoV2;

        (float[] f0, float[] mspec) = GetAudioFeatures(track);

        return this.SynthesisNSF(new NSFV2Option
        {
            F0 = f0,
            Melspec = mspec,
            Model = track.Singer,
            ModelType = NSFV2Model.VA,
            SamplingRate = 48,
            MultiPhrasePrediction = track.Timings,
            NumberOfParallel = setting.CpuThreads,
            UseMultiGpus = setting.UseGpu,
            IsViewInformation = true,
        }
        , progress, cancellationToken);
    }

    /// <summary>
    /// NSFで音声合成を行う
    /// </summary>
    /// <param name="track">トラック情報</param>
    /// <param name="phrase">フレーズ情報</param>
    /// <param name="progress">進捗通知</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns>WAVファイルデータ</returns>
    public Task<byte[]> SynthesisNSF(NeutrinoV2Track track, NeutrinoV2Phrase phrase, IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default)
    {
        var setting = this._setting.NeutrinoV2;

        return this.SynthesisNSF(new NSFV2Option
        {
            F0 = phrase.GetEditedF0()!,
            Melspec = phrase.GetEditedMspec()!,
            Model = track.Singer,
            ModelType = NSFV2Model.VA,
            SamplingRate = 48,
            NumberOfParallel = setting.CpuThreads,
            UseMultiGpus = setting.UseGpu,
            IsViewInformation = true,
        }
        , progress, cancellationToken);
    }

    /// <summary>
    /// トラック全体をNSFで音声合成を行う
    /// </summary>
    /// <param name="track">トラック情報</param>
    /// <param name="path">出力先</param>
    /// <param name="progress">進捗通知</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns></returns>
    public async Task SynthesisNSF(NeutrinoV2Track track, string path, IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default)
    {
        var setting = this._setting.NeutrinoV2;

        var timings = track.Timings;
        var phrases = track.Phrases;

        if (timings.Length == 0 || phrases.Length == 0)
            return;

        (float[] f0, float[] mspec) = GetAudioFeatures(track);

        // 音声出力
        byte[] data = await this.SynthesisNSF(new NSFV2Option
        {
            F0 = f0,
            Melspec = mspec,
            Model = track.Singer,
            ModelType = NSFV2Model.VA,
            SamplingRate = 48,
            NumberOfParallel = setting.CpuThreads,
            UseMultiGpus = setting.UseGpu,
            MultiPhrasePrediction = track.Timings,
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

        int frameCount = NeutrinoUtil.MsToFrameIndex(NeutrinoUtil.TimingTimeToMs(timings[^1].EditedEndTime100Ns));

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
