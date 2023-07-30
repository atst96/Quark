using System.Runtime.Intrinsics.X86;
using System.Text;
using Quark.Components;
using Quark.Constants;
using Quark.Data.Projects;
using Quark.Data.Settings;
using Quark.DependencyInjection;
using Quark.Models.Neutrino;
using Quark.Neutrino;
using Quark.Projects.Tracks;
using Quark.Utils;

namespace Quark.Services;

/// <summary>
/// NEUTRINO(v1)を操作するためのサービス
/// TODO: NSFの処理が一部未実装
/// </summary>
[Singleton]
internal class NeutrinoV1Service
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

    /// <summary>NEUTRINO-legacyのファイルパス</summary>
    private static readonly string NeutrinoLegacyExe = Path.Combine(BinDirName, "NEUTRINO-legacy.exe");

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
    public NeutrinoV1Service(SettingService settingService)
    {
        this._setting = settingService.Settings;
    }

    /// <summary>レガシー版を使用するかどうかを取得する</summary>
    public static bool IsLegacy() => !Avx2.IsSupported;

    private string GetModelPath(string modelId)
        => Path.Combine(this.GetNeutrinoWorkingDirectory(), "model", modelId) + Path.DirectorySeparatorChar.ToString();

    /// <summary>
    /// モデル情報を取得する
    /// </summary>
    /// <returns></returns>
    public IList<ModelInfo> GetModels()
    {
        if (this._setting.NeutrinoV1.Directory is null)
        {
            return new List<ModelInfo>();
        }
        else
        {
            var path = Path.Combine(this._setting.NeutrinoV1.Directory!, ModelDirName);

            return NeutrinoModelUtil.GetModels(path);
        }
    }

    /// <summary>NEUTRINOの作業ディレクトリを取得する</summary>
    private string GetNeutrinoWorkingDirectory() => this._setting.NeutrinoV1.Directory!;

    /// <summary>
    /// NEUTRINO内のファイルパスを取得する
    /// </summary>
    /// <param name="relativePath">相対パス</param>
    /// <returns>ファイルパス</returns>
    private string GetNeutrinoPath(string relativePath)
        => Path.Combine(this.GetNeutrinoWorkingDirectory(), relativePath);

    /// <summary>NEUTRINOと送受信するテキストデータの文字コード(暫定)</summary>
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
                await NeutrinoUtil.Execute(command, args.ToString(), this.GetNeutrinoWorkingDirectory())
                    .ConfigureAwait(false);
            }
            finally
            {
                // 一定時間待機しても終了していなければキャンセルして、タスク終了を永遠に待ち続けるのを防ぐ
                receiveTaskCanceler.CancelAfter(DataReceiveTaskCancelDuration);
            }

            // 出力情報を取得
            (byte[] fullLabel, byte[] monoLabel) = await TaskUtil.WhenAll(fullLabFileTask, monoLabFileTask);
            return new(fullLabel, monoLabel);
        }
    }

    /// <summary>
    /// 音響情報の推論処理を行う。
    /// </summary>
    /// <param name="option">推論オプション</param>
    /// <param name="progress">進捗通知</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns></returns>
    public async Task<EstimateFeaturesResultV1> EstimateFeatures(NeutrinoV1Option option, IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default)
    {
        var v1Setting = this._setting.NeutrinoV1;

        VirtualFile? receivePhraseFile = null;
        VirtualFile? receiveTimingFile = null;

        using (var fullLabFile = TempFile.Create(FileAccess.Write, FileShare.Read, FileExtensions.Label))
        using (var f0File = new VirtualFile(FileExtensions.F0))
        using (var mgcFile = new VirtualFile(FileExtensions.Mgc))
        using (var bapFile = new VirtualFile(FileExtensions.Bap))
        using (var additionalDisposables = new DisposableCollection(5))
        {
            // 一時ファイルのタイミング情報を書き込む
            fullLabFile.Write(option.FullLabel);

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

            // 実行ファイル
            var command = this.GetNeutrinoPath((v1Setting.UseLegacyExe ?? IsLegacy()) ? NeutrinoLegacyExe : NeutrinoExe);

            // コマンドライン引数の作成
            var args = new StringBuilder();
            args.Append($@"""{fullLabFile.Path}"" ""{timingFilePath}"" ""{f0File.FilePath}"" ""{mgcFile.FilePath}"" ""{bapFile.FilePath}"" {this.GetModelPath(option.Model.ModelId)}");

            if (option.NumberOfThreads != null)
                args.Append(" -n ").Append(option.NumberOfThreads);

            if (option.StyleShift != null)
                args.Append(" -k ").Append(option.StyleShift);

            if (option.IsSkipTimingPrediction)
                args.Append(" -s");

            if (option.IsSkipAcousticFeaturesPrediction)
                args.Append(" -a");

            if (option.SinglePhrasePrediction != null)
                args.Append(" -p ").Append(option.SinglePhrasePrediction);

            if (option.UseSingleGpu)
                args.Append(" -g");

            if (option.UseMultiGpus)
                args.Append(" -m");

            if (option.IsSkipTimingPrediction)
            {
                if (option.EstimatedPhrase != null)
                {
                    var sendPhraseFile = TempFile.Create(FileAccess.Write, FileShare.Read, FileExtensions.Text);
                    sendPhraseFile.Write(option.EstimatedPhrase);

                    additionalDisposables.Add(sendPhraseFile);
                }
            }

            if (option.IsTracePhraseInformation)
            {
                receivePhraseFile = new VirtualFile(FileExtensions.Text);
                additionalDisposables.Add(receivePhraseFile);

                args.Append(" -i ").AppendDoubleQuoted(receivePhraseFile.FilePath);
            }

            if (option.IsViewInformation)
                args.Append(" -t");

            // ファイル受信処理のキャンセラ
            using var receiveTaskCanceler = new CancellationTokenSource();

            // ファイル出力を非同期で待機
            var timingTask = receiveTimingFile?.Read(receiveTaskCanceler.Token) ?? TaskUtil.NullByteArrayTask!;

            Task<byte[]> f0Task, mgcTask, bapTask;
            if (option.IsSkipAcousticFeaturesPrediction)
            {
                f0Task = TaskUtil.NullByteArrayTask!;
                mgcTask = TaskUtil.NullByteArrayTask!;
                bapTask = TaskUtil.NullByteArrayTask!;
            }
            else
            {
                f0Task = f0File.Read(receiveTaskCanceler.Token)!;
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

            // 出力結果を返却
            var (timing, f0, mgc, bap, phrase) = await TaskUtil.WhenAll(timingTask, f0Task, mgcTask, bapTask, phraseTask)
                    .ConfigureAwait(false);

            return new(
                    timing == null ? null : OutputTextEncoding.GetString(timing),
                    f0 == null ? null : DataConvertUtil.Convert<double>(f0),
                    mgc == null ? null : DataConvertUtil.Convert<double>(mgc),
                    bap == null ? null : DataConvertUtil.Convert<double>(bap),
                    phrase == null ? null : OutputTextEncoding.GetString(phrase));
        }
    }

    /// <summary>
    /// タイミング情報を取得する
    /// </summary>
    /// <param name="track">トラック情報</param>
    /// <param name="progress">進捗通知</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns></returns>
    public Task<EstimateTimingResult> GetTiming(NeutrinoV1Track track, IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default)
    {
        var v1Setting = this._setting.NeutrinoV1;

        var option = new NeutrinoV1Option()
        {
            FullLabel = track.FullTiming!,
            Model = track.Singer,
            IsTracePhraseInformation = true,
            IsSkipAcousticFeaturesPrediction = true,
            NumberOfThreads = v1Setting.CpuThreads,
            UseMultiGpus = v1Setting.UseGpu,
            IsViewInformation = true,
        };

        return this.EstimateFeatures(option, progress, cancellationToken)
            .ContinueWith(t =>
            {
                var result = t.Result;
                return new EstimateTimingResult(result.Timing!, result.Phrase!);
            }
            , TaskContinuationOptions.OnlyOnRanToCompletion);
    }

    /// <summary>
    /// 音響情報の推論処理を行う。
    /// </summary>
    /// <param name="track">トラック情報</param>
    /// <param name="progress">進捗通知</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns></returns>
    public Task<EstimateFeaturesResultV1> EstimateFeatures(NeutrinoV1Track track, IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default)
    {
        var v1Setting = this._setting.NeutrinoV1;

        return this.EstimateFeatures(new NeutrinoV1Option
        {
            FullLabel = track.FullTiming!,
            TimingLabel = NeutrinoUtil.GetTimingContent(track.Timings),
            Model = track.Singer,
            IsSkipTimingPrediction = true,
            NumberOfThreads = v1Setting.CpuThreads,
            UseMultiGpus = v1Setting.UseGpu,
            IsViewInformation = true,
        }
        , progress, cancellationToken);
    }

    /// <summary>
    /// 単一フレーズの音響情報の推論処理を行う。
    /// </summary>
    /// <param name="track">トラック情報</param>
    /// <param name="phrase">フレーズ情報</param>
    /// <param name="progress">進捗通知</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns></returns>
    public Task<EstimateFeaturesResultV1> EstimateFeatures(NeutrinoV1Track track, NeutrinoV1Phrase phrase, IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default)
    {
        var v1Setting = this._setting.NeutrinoV1;

        return this.EstimateFeatures(new NeutrinoV1Option
        {
            FullLabel = track.FullTiming!,
            TimingLabel = NeutrinoUtil.GetTimingContent(track.Timings),
            Model = track.Singer,
            IsSkipTimingPrediction = true,
            EstimatedPhrase = NeutrinoUtil.GetPhraseContent(track.RawPhrases),
            SinglePhrasePrediction = phrase.No,
            NumberOfThreads = v1Setting.CpuThreads,
            UseMultiGpus = v1Setting.UseGpu,
            IsViewInformation = true,
        }
        , progress, cancellationToken);
    }

    /// <summary>
    /// WORLDで音声合成する。
    /// </summary>
    /// <param name="option">合成オプション</param>
    /// <param name="progress">進捗通知</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns></returns>
    public async Task<byte[]> SynthesisWorld(WorldV1Option option, IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default)
    {
        using (var f0File = TempFile.Create(FileAccess.Write, FileShare.Read, FileExtensions.F0))
        using (var mgcFile = TempFile.Create(FileAccess.Write, FileShare.Read, FileExtensions.Mgc))
        using (var bapFile = TempFile.Create(FileAccess.Write, FileShare.Read, FileExtensions.Bap))
        using (var wavFile = new VirtualFile(FileExtensions.F0))
        {
            // 実行ファイル
            var command = this.GetNeutrinoPath(WorldExe);

            // 一時ファイルのタイミング情報を書き込む
            f0File.Write(DataConvertUtil.CastToByte(option.F0));
            mgcFile.Write(DataConvertUtil.CastToByte(option.Mgc));
            bapFile.Write(DataConvertUtil.CastToByte(option.Bap));

            // コマンドライン引数の作成
            var args = new StringBuilder();
            args.Append($@"""{f0File.Path}"" ""{mgcFile.Path}"" ""{bapFile.Path}"" -o ""{wavFile.FilePath}""");

            if (option.PitchShift != null)
                args.Append(" -f ").Append(option.PitchShift);

            if (option.FormantShift != null)
                args.Append(" -m ").Append(option.FormantShift);

            if (option.NumberOfParallel != null)
                args.Append(" -n ").Append(option.NumberOfParallel);

            if (option.IsHiSpeedSynthesis)
                args.Append(" -s");

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
    /// WORKDで音声合成する。
    /// </summary>
    /// <param name="track">トラック情報</param>
    /// <param name="progress">進捗通知</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns></returns>
    public Task<byte[]> SynthesisWorld(NeutrinoV1Track track, IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default)
    {
        var settings = this._setting.NeutrinoV1;

        // f0, mgc, bapを取得
        (double[] f0, double[] mgc, double[] bap) = GetSynthesisParameters(track);

        return this.SynthesisWorld(new WorldV1Option
        {
            F0 = f0,
            Mgc = mgc,
            Bap = bap,
            NumberOfParallel = settings.CpuThreads,
            IsViewInformation = true,
        }
        , progress, cancellationToken);
    }

    /// <summary>
    /// WORKDで音声合成する。
    /// </summary>
    /// <param name="phrase">フレーズ情報</param>
    /// <param name="progress">進捗通知</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns></returns>
    public Task<byte[]> SynthesisWorld(NeutrinoV1Phrase phrase, IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default)
    {
        var settings = this._setting.NeutrinoV1;

        return this.SynthesisWorld(new WorldV1Option
        {
            F0 = phrase.GetEditedF0()!,
            Mgc = phrase.GetEditedMgc()!,
            Bap = phrase.Bap!,
            NumberOfParallel = settings.CpuThreads,
            IsViewInformation = true,
        }
        , progress, cancellationToken);
    }

    /// <summary>
    /// トラック全体をWORLDで音声合成を行う。
    /// </summary>
    /// <param name="track">トラック情報</param>
    /// <param name="path">出力先</param>
    /// <param name="progress">進捗通知</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns></returns>
    public async Task SynthesisWorld(NeutrinoV1Track track, string path, IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default)
    {
        var settings = this._setting.NeutrinoV1;

        var timings = track.Timings;
        var phrases = track.Phrases;

        if (timings.Length == 0 || phrases.Length == 0)
            return;

        // f0, mgc, bapを取得
        (double[] f0, double[] mgc, double[] bap) = GetSynthesisParameters(track);

        // 音声出力
        byte[] data = await this.SynthesisWorld(new WorldV1Option
        {
            F0 = f0,
            Mgc = mgc,
            Bap = bap,
            NumberOfParallel = settings.CpuThreads,
            IsViewInformation = true,
        }
        , progress, cancellationToken).ConfigureAwait(false);

        // ファイルに書き出し
        File.WriteAllBytes(path, data);
    }

    /// <summary>
    /// NSFで音声合成を行う。
    /// </summary>
    /// <param name="option">合成オプション</param>
    /// <param name="progress">進捗通知</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns>出力データ(WAVファイルデータ)</returns>
    public async Task<byte[]> SynthesisNSF(NSFV1Option option, IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default)
    {
        using (var f0File = TempFile.Create(FileAccess.Write, FileShare.Read, FileExtensions.F0))
        using (var mgcFile = TempFile.Create(FileAccess.Write, FileShare.Read, FileExtensions.Mgc))
        using (var bapFile = TempFile.Create(FileAccess.Write, FileShare.Read, FileExtensions.Bap))
        using (var wavFile = new VirtualFile(FileExtensions.Wave))
        using (var additionalDisposable = new DisposableCollection())
        {
            // 一時ファイルのタイミング情報を書き込む
            f0File.Write(DataConvertUtil.CastToByte(option.F0));
            mgcFile.Write(DataConvertUtil.CastToByte(option.Mgc));
            bapFile.Write(DataConvertUtil.CastToByte(option.Bap));

            // 実行ファイル
            var command = this.GetNeutrinoPath(NsfExe);

            // コマンドライン引数の組み立て
            var args = new StringBuilder();
            args.Append($@"""{f0File.Path}"" ""{mgcFile.Path}"" ""{bapFile.Path}"" ""{this.GetModelPath(option.Model.ModelId)}model_nsf.bin"" ""{wavFile.FilePath}""");

            if (option.SamplingRate != null)
                args.Append(" -s ").Append(option.SamplingRate);

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

            if (option.IsUseGpu)
                args.Append(" -g");

            if (option.GpuId != null)
                args.Append(" -i ").Append(option.GpuId);

            if (option.IsViewInformation)
                args.Append(" -t");

            // ファイル受信処理のキャンセラ
            using var receiveTaskCanceler = new CancellationTokenSource();

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
    /// NSFで音声合成を行う。
    /// </summary>
    /// <param name="track">トラック情報</param>
    /// <param name="progress">進捗通知</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns></returns>
    public Task<byte[]> SynthesisNSF(NeutrinoV1Track track, IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default)
    {
        var settings = this._setting.NeutrinoV1;

        // f0, mgc, bapを取得
        (double[] f0, double[] mgc, double[] bap) = GetSynthesisParameters(track);

        return this.SynthesisNSF(new NSFV1Option()
        {
            SamplingRate = 48,
            F0 = f0,
            Mgc = mgc,
            Bap = bap,
            Model = track.Singer,
            IsUseGpu = settings.UseGpu,
            NumberOfParallel = settings.CpuThreads,
            IsViewInformation = true,
        }
        , progress, cancellationToken);
    }

    /// <summary>
    /// NSFで音声合成を行う。
    /// </summary>
    /// <param name="phrase">フレーズ情報</param>
    /// <param name="modelInfo">モデル情報</param>
    /// <param name="progress">進捗通知</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns></returns>
    public Task<byte[]> SynthesisNSF(NeutrinoV1Phrase phrase, ModelInfo modelInfo, IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default)
    {
        var settings = this._setting.NeutrinoV1;

        return this.SynthesisNSF(new NSFV1Option()
        {
            SamplingRate = 48,
            F0 = phrase.GetEditedF0()!,
            Mgc = phrase.GetEditedMgc()!,
            Bap = phrase.Bap!,
            Model = modelInfo,
            IsUseGpu = settings.UseGpu,
            NumberOfParallel = settings.CpuThreads,
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
    public async Task SynthesisNSF(NeutrinoV1Track track, string path, IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default)
    {
        var settings = this._setting.NeutrinoV1;

        var timings = track.Timings;
        var phrases = track.Phrases;

        if (timings.Length == 0 || phrases.Length == 0)
            return;

        // f0, mgc, bapを取得
        (double[] f0, double[] mgc, double[] bap) = GetSynthesisParameters(track);

        // 音声出力
        byte[] data = await this.SynthesisNSF(new NSFV1Option
        {
            SamplingRate = 48,
            F0 = f0,
            Mgc = mgc,
            Bap = bap,
            Model = track.Singer,
            MultiPhrasePrediction = track.Timings,
            IsUseGpu = settings.UseGpu,
            NumberOfParallel = settings.CpuThreads,
            IsViewInformation = true,
        }
        , progress, cancellationToken).ConfigureAwait(false);

        // ファイルに書き出し
        File.WriteAllBytes(path, data);
    }

    /// <summary>
    /// 音声合成用のパラメータ(f0, mgc, bap)を取得する
    /// </summary>
    /// <param name="track">トラック</param>
    /// <returns></returns>
    private static (double[] f0, double[] mgc, double[] bap) GetSynthesisParameters(NeutrinoV1Track track)
    {
        const int mgcDimension = NeutrinoConfig.MgcDimension;
        const int bapDimension = NeutrinoConfig.BapDimension;

        var timings = track.Timings;
        var phrases = track.Phrases;

        // フレーム数
        int frameCount = NeutrinoUtil.MsToFrameIndex(NeutrinoUtil.TimingTimeToMs(timings[^1].EditedEndTime100Ns));

        // F0
        double[] f0 = new double[frameCount];
        // スペクトル包絡
        double[] mgc = ArrayUtil.CreateAndInitSegmentFirst(frameCount, mgcDimension, NeutrinoConfig.MgcLower);
        // 非同期成分
        double[] bap = new double[frameCount * bapDimension];

        // 各フレーズの音響情報を配列にコピーする
        foreach (var phrase in phrases)
        {
            int length = NeutrinoUtil.MsToFrameIndex(phrase.EndTime - phrase.BeginTime);
            if (length <= 0)
                continue;

            int frameIdx = NeutrinoUtil.MsToFrameIndex(phrase.BeginTime);

            var srcF0 = phrase.GetEditedF0();
            if (srcF0?.Length > 0)
                srcF0.AsSpan(..length).CopyTo(f0.AsSpan(frameIdx));

            var srcMgc = phrase.GetEditedMgc();
            if (srcMgc?.Length > 0)
                srcMgc.AsSpan(..(length * mgcDimension)).CopyTo(mgc.AsSpan(frameIdx * mgcDimension));

            var srcBap = phrase.Bap;
            if (srcBap?.Length > 0)
                srcBap.AsSpan(..(length * bapDimension)).CopyTo(bap.AsSpan(frameIdx * bapDimension));
        }

        return (f0, mgc, bap);
    }
}
