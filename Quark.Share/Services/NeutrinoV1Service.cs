using System.Diagnostics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.RegularExpressions;
using Quark.Constants;
using Quark.Data.Projects;
using Quark.Data.Settings;
using Quark.DependencyInjection;
using Quark.Models.Neutrino;
using Quark.Neutrino;
using Quark.Projects.Neutrino;
using Quark.Projects.Tracks;
using Quark.Utils;

namespace Quark.Services;

[Singleton]
internal class NeutrinoV1Service
{
    private Settings _setting;

    private const string BinDirName = "bin";
    private const string ModelDirName = "model";

    private static readonly string MusicXmlExe = Path.Combine(BinDirName, "musicXMLtoLabel.exe");
    private static readonly string NeutrinoExe = Path.Combine(BinDirName, "NEUTRINO.exe");
    private static readonly string NeutrinoLegacyExe = Path.Combine(BinDirName, "NEUTRINO-legacy.exe");
    private static readonly string NsfExe = Path.Combine(BinDirName, "NSF.exe");
    private static readonly string WorldExe = Path.Combine(BinDirName, "WORLD.exe");

    public NeutrinoV1Service(SettingService settingService)
    {
        this._setting = settingService.Settings;
    }

    /// <summary>
    /// レガシー版を使用するかどうかを取得する
    /// TODO: GPU
    /// </summary>
    /// <returns></returns>
    public static bool IsLegacy() => !Avx2.IsSupported;

    private string GetModelPath(string modelId)
        => Path.Combine(this.GetNeturinoDir(), "model", modelId) + Path.DirectorySeparatorChar.ToString();

    public IList<ModelInfo> GetModels()
    {
        if (this._setting.Neutrino.Directory is null)
        {
            return new List<ModelInfo>();
        }
        else
        {
            var path = Path.Combine(this._setting.Neutrino.Directory!, ModelDirName);

            return NeutrinoModelUtil.GetModels(path);
        }
    }

    private string GetNeturinoDir() => this._setting.Neutrino.Directory!;

    private string GetNeutrinoPath(string path)
        => Path.Combine(this.GetNeturinoDir(), path);

    private string GetNeutrinoExePath()
        => this.GetNeutrinoPath(IsLegacy() ? NeutrinoLegacyExe : NeutrinoExe);

    private string GetWorldExePath()
        => this.GetNeutrinoPath(WorldExe);

    private string GetNsfExePath()
        => this.GetNeutrinoPath(NsfExe);

    private static readonly Encoding TextEncoding = new UTF8Encoding(false);

    public async Task<ConvertScoreToTimingResult?> ConvertMusicXmlToTiming(string musicXml)
    {
        var procExe = this.GetNeutrinoPath(MusicXmlExe);

        using (var musicXmlFile = TempFile.Create(FileAccess.ReadWrite, FileShare.Read, FileExtensions.MusicXML))
        using (var fullLabFile = new VirtualFile(FileExtensions.Label))
        using (var monoLabFile = new VirtualFile(FileExtensions.Label))
        {
            musicXmlFile.Write(TextEncoding.GetBytes(musicXml));

            var t1 = fullLabFile.Read();
            var t2 = monoLabFile.Read();

            var args = string.Join(" ",
                PathUtil.Dq(musicXmlFile.Path),
                PathUtil.Dq(fullLabFile.FilePath),
                PathUtil.Dq(monoLabFile.FilePath));

            var p = Process.Start(new ProcessStartInfo(procExe, args)
            {
                WorkingDirectory = this.GetNeturinoDir(),
            })!;

            await p.WaitForExitAsync().ConfigureAwait(false);

            if (IsSuccess(p.ExitCode))
            {
                await Task.WhenAll(t1, t2).ConfigureAwait(false);

                return new(t1.Result, t2.Result);
            }
        }

        return null;
    }

    private static readonly Regex Regex = new(@"^.+Progress\s*=\s*(?<progress>\d+)\s*%.+$", RegexOptions.Compiled);

    private static bool IsSuccess(int exitCode) => exitCode == 0;

    private static async Task<bool> Run(string exePath, string? args = null, string? pwd = null, IProgress<ProgressReport>? progress = null)
    {
        Debug.WriteLine("=== START NEUTRINO ===");

        using var p = new Process()
        {
            StartInfo = new(exePath)
            {
                Arguments = args,
                WorkingDirectory = pwd,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            }
        };

        bool isInitializing = true;

        void receive(object _, DataReceivedEventArgs args)
        {
            var line = args.Data ?? string.Empty;
            Debug.WriteLine(line);

            double? progressValue = null;

            var m = Regex.Match(line);
            if (m.Success)
            {
                isInitializing = false;
                progressValue = double.Parse(m.Groups["progress"].Value);
            }

            progress?.Report(new(isInitializing ? ProgressReportType.Idertimate : ProgressReportType.InProgress, line, progressValue));
        };

        p.OutputDataReceived += receive;

        progress?.Report(new(ProgressReportType.Idertimate, null, null));

        p.Start();
        p.BeginOutputReadLine();

        await p.WaitForExitAsync().ConfigureAwait(false);

        Debug.WriteLine("=== END NEUTRINO ===");

        p.OutputDataReceived -= receive;


        bool isSuccess = IsSuccess(p.ExitCode);

        if (!isSuccess)
        {
            progress?.Report(new(ProgressReportType.Error, null, 100));
        }

        return isSuccess;
    }

    public async Task<EstimateTimingResult?> GetTiming(NeutrinoV1Track track, AudioFeaturesV1 features, IProgress<ProgressReport>? progress = null)
    {
        var procExe = this.GetNeutrinoPath(IsLegacy() ? NeutrinoLegacyExe : NeutrinoExe);

        // 対象ファイル用のCancellationToken
        using var targetClTokenSource = new CancellationTokenSource();
        var targetClToken = targetClTokenSource.Token;

        // 一時ファイル用のCancellationToken
        using var tempClTokenSource = new CancellationTokenSource();
        var tempClToken = tempClTokenSource.Token;

        using (var fullLabFile = TempFile.Create(FileAccess.Write, FileShare.Read, FileExtensions.Label))
        using (var timingLabFile = new VirtualFile(FileExtensions.Label))
        using (var phraseFile = new VirtualFile(FileExtensions.Text))
        using (var f0File = new VirtualFile(FileExtensions.F0))
        using (var mgcFile = new VirtualFile(FileExtensions.Mgc))
        using (var bapFile = new VirtualFile(FileExtensions.Bap))
        {
            // 一時ファイルのタイミング情報を書き込む
            fullLabFile.Write(track.FullTiming);

            var args = $@"""{fullLabFile.Path}"" ""{timingLabFile.FilePath}"" "
                + $@"""{f0File.FilePath}"" ""{mgcFile.FilePath}"" ""{bapFile.FilePath}"" "
                + $@"{this.GetModelPath(features.ModelId)} "
                + $@"-i ""{phraseFile.FilePath}"" "
                + $@"-m -t -a";

            var timingLabTask = timingLabFile.Read(targetClToken);
            var phraseTask = phraseFile.Read(targetClToken);
            var f0Task = f0File.Read(tempClToken);
            var mgcTask = mgcFile.Read(tempClToken);
            var bapTask = bapFile.Read(tempClToken);

            bool isSuccess = await Run(procExe, args, this.GetNeturinoDir(), progress)
                .ConfigureAwait(false);

            if (isSuccess)
            {
                tempClTokenSource.Cancel();

                await Task.WhenAll(timingLabTask, phraseTask).ConfigureAwait(false);

                return new(
                    TextEncoding.GetString(timingLabTask.Result),
                    TextEncoding.GetString(phraseTask.Result));
            }
            else
            {
                targetClTokenSource.Cancel();
                tempClTokenSource.Cancel();
            }
        }

        return null;
    }

    public async Task<EstimateFeaturesResultV1?> EstimateFeatures(NeutrinoV1Track track, AudioFeaturesV1 features, IProgress<ProgressReport>? progress = null)
    {
        var procExe = this.GetNeutrinoPath(NeutrinoExe);

        // 対象ファイル用のCancellationToken
        using var clTokenSource = new CancellationTokenSource();
        var clToken = clTokenSource.Token;

        using (var fullLabFile = TempFile.Create(FileAccess.Write, FileShare.Read, FileExtensions.Label))
        using (var timingLabFile = TempFile.Create(FileAccess.Write, FileShare.Read, FileExtensions.Label))
        using (var f0File = new VirtualFile(FileExtensions.F0))
        using (var mgcFile = new VirtualFile(FileExtensions.Mgc))
        using (var bapFile = new VirtualFile(FileExtensions.Bap))
        {
            // 一時ファイルのタイミング情報を書き込む
            fullLabFile.Write(track.FullTiming);
            timingLabFile.Write(NeutrinoUtil.ToString(features.Timings!));

            var args = $@"""{fullLabFile.Path}"" ""{timingLabFile.Path}"" "
                + @$"""{f0File.FilePath}"" ""{mgcFile.FilePath}"" ""{bapFile.FilePath}"" "
                + @$"""{this.GetModelPath(features.ModelId)}"" "
                + $@"-m -t -s";

            var f0Task = f0File.Read(clToken);
            var mgcTask = mgcFile.Read(clToken);
            var bapTask = bapFile.Read(clToken);

            bool isSuccess = await Run(procExe, args, this.GetNeturinoDir(), progress)
                .ConfigureAwait(false);

            if (isSuccess)
            {
                await Task.WhenAll(f0Task, mgcTask, bapTask).ConfigureAwait(false);

                return new(
                    DataConvertUtil.Convert<double>(f0Task.Result),
                    DataConvertUtil.Convert<double>(mgcTask.Result),
                    DataConvertUtil.Convert<double>(bapTask.Result));
            }
            else
            {
                clTokenSource.Cancel();
            }
        }

        return null;
    }

    public async Task<EstimateFeaturesResultV1?> EstimateFeatures(NeutrinoV1Track track, AudioFeaturesV1 features, PhraseInfo2 phrase, IProgress<ProgressReport>? progress = null)
    {
        var procExe = this.GetNeutrinoExePath();

        // 対象ファイル用のCancellationToken
        using var clTokenSource = new CancellationTokenSource();
        var clToken = clTokenSource.Token;

        using (var fullLabFile = TempFile.Create(FileAccess.Write, FileShare.Read, FileExtensions.Label))
        using (var timingLabFile = TempFile.Create(FileAccess.Write, FileShare.Read, FileExtensions.Label))
        using (var phraseFile = TempFile.Create(FileAccess.Write, FileShare.Read, FileExtensions.Label))
        using (var f0File = new VirtualFile(FileExtensions.F0))
        using (var mgcFile = new VirtualFile(FileExtensions.Mgc))
        using (var bapFile = new VirtualFile(FileExtensions.Bap))
        {
            // 一時ファイルのタイミング情報を書き込む
            fullLabFile.Write(track.FullTiming);
            timingLabFile.Write(NeutrinoUtil.ToString(features.Timings!));
            phraseFile.Write(NeutrinoUtil.ToString(features.RawPhrases!));

            var args = string.Join(" ",
                PathUtil.Dq(fullLabFile.Path),
                PathUtil.Dq(timingLabFile.Path),
                PathUtil.Dq(f0File.FilePath),
                PathUtil.Dq(mgcFile.FilePath),
                PathUtil.Dq(bapFile.FilePath),
                this.GetModelPath(features.ModelId),
                "-i", PathUtil.Dq(phraseFile.Path),
                "-p", phrase.No,
                "-m", "-t", "-s"
                // "-k", styleshift // TODO: スタイルシフト
                );

            var f0Task = f0File.Read(clToken);
            var mgcTask = mgcFile.Read(clToken);
            var bapTask = bapFile.Read(clToken);

            bool isSuccess = await Run(procExe, args, this.GetNeturinoDir(), progress)
                .ConfigureAwait(false);

            if (isSuccess)
            {
                await Task.WhenAll(f0Task, mgcTask, bapTask).ConfigureAwait(false);

                return new(
                    DataConvertUtil.Convert<double>(f0Task.Result),
                    DataConvertUtil.Convert<double>(mgcTask.Result),
                    DataConvertUtil.Convert<double>(bapTask.Result));
            }
            else
            {
                clTokenSource.Cancel();
            }
        }

        return null;
    }

    public async Task<byte[]?> OutputPreviewWavWorld(PhraseInfo2 phrase, IProgress<ProgressReport>? progress = null)
    {
        var procExe = this.GetWorldExePath();

        // 対象ファイル用のCancellationToken
        using var clTokenSource = new CancellationTokenSource();
        var clToken = clTokenSource.Token;

        using (var f0File = TempFile.Create(FileAccess.Write, FileShare.Read, FileExtensions.F0))
        using (var mgcFile = TempFile.Create(FileAccess.Write, FileShare.Read, FileExtensions.Mgc))
        using (var bapFile = TempFile.Create(FileAccess.Write, FileShare.Read, FileExtensions.Bap))
        using (var wavFile = new VirtualFile(FileExtensions.F0))
        {
            // 一時ファイルのタイミング情報を書き込む
            f0File.Write(DataConvertUtil.Cast<double, byte>(phrase.F0));
            mgcFile.Write(DataConvertUtil.Cast<double, byte>(phrase.Mgc));
            bapFile.Write(DataConvertUtil.Cast<double, byte>(phrase.Bap));

            var args = string.Join(" ",
                PathUtil.Dq(f0File.Path),
                PathUtil.Dq(mgcFile.Path),
                PathUtil.Dq(bapFile.Path),
                // "-p", PitchShift,
                // "-m", FormalShift,
                // "-p", SmoothPitch,
                // "-b", EnhanceBreathiness
                "-o", PathUtil.Dq(wavFile.FilePath),
                // "-n", NumThreads,
                "-t"
                );

            var wavTask = wavFile.Read(clToken);

            bool isSuccess = await Run(procExe, args, this.GetNeturinoDir(), progress)
                .ConfigureAwait(false);

            if (isSuccess)
            {
                return await wavTask.ConfigureAwait(false);
            }
            else
            {
                clTokenSource.Cancel();
            }
        }

        return null;
    }

    public async Task<byte[]?> OutputPreviewWavNsf(PhraseInfo2 phrase, AudioFeaturesV1 features, IProgress<ProgressReport>? progress = null)
    {
        var procExe = this.GetNsfExePath();

        // 対象ファイル用のCancellationToken
        using var clTokenSource = new CancellationTokenSource();
        var clToken = clTokenSource.Token;

        using (var f0File = TempFile.Create(FileAccess.Write, FileShare.Read, FileExtensions.F0))
        using (var mgcFile = TempFile.Create(FileAccess.Write, FileShare.Read, FileExtensions.Mgc))
        using (var bapFile = TempFile.Create(FileAccess.Write, FileShare.Read, FileExtensions.Bap))
        using (var wavFile = new VirtualFile(FileExtensions.F0))
        {
            // 一時ファイルのタイミング情報を書き込む
            f0File.Write(DataConvertUtil.Cast<double, byte>(phrase.F0));
            mgcFile.Write(DataConvertUtil.Cast<double, byte>(phrase.Mgc));
            bapFile.Write(DataConvertUtil.Cast<double, byte>(phrase.Bap));

            var args = $@"""{f0File.Path}"" ""{mgcFile.Path}"" ""{bapFile.Path}"" "
                + @$"""{this.GetModelPath(features.ModelId)}model_nsf.bin"" "
                + $@"""{PathUtil.Dq(wavFile.FilePath)}"" "
                + $@"-t -g";

            var wavTask = wavFile.Read(clToken);

            bool isSuccess = await Run(procExe, args, this.GetNeturinoDir(), progress)
                .ConfigureAwait(false);

            if (isSuccess)
            {
                return await wavTask.ConfigureAwait(false);
            }
            else
            {
                clTokenSource.Cancel();
            }
        }

        return null;
    }
}
