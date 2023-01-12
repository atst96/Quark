using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Navigation;
using Livet.Behaviors.Messaging;
using Quark.Data.Projects;
using Quark.Data.Settings;
using Quark.Models.Neutrino;
using Quark.Projects.Tracks;
using Quark.Utils;

namespace Quark.Services;

internal class NeutrinoService
{
    private Settings _setting;

    private const string BinDirName = "bin";
    private const string ModelDirName = "model";

    private static readonly string MusicXmlExe = Path.Combine(BinDirName, "musicXMLtoLabel.exe");
    private static readonly string NeutrinoExe = Path.Combine(BinDirName, "NEUTRINO.exe");
    private static readonly string NsfExe = Path.Combine(BinDirName, "NSF.exe");
    private static readonly string WorldExe = Path.Combine(BinDirName, "WORLD.exe");

    public NeutrinoService(SettingService settingService)
    {
        this._setting = settingService.Settings;
    }

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

    public Task ConvertMusicXmlToTiming(NeutrinoTrack track)
    {
        var procExe = this.GetNeutrinoPath(MusicXmlExe);

        var args = $"\"{track.GetMusicXmlPath()}\" \"{track.GetFullLabelPath()}\" \"{track.GetMonoLabelPath()}\"";
        var p = Process.Start(new ProcessStartInfo(procExe, args)
        {
            WorkingDirectory = this.GetNeturinoDir(),
        })!;

        return p.WaitForExitAsync();
    }

    private static readonly Regex Regex = new(@"^.+Progress\s*=\s*(?<progress>\d+)\s*%.+$", RegexOptions.Compiled);

    private static bool IsSuccess(int exitCode) => exitCode == 0;

    private static async Task<int> Run(string exePath, string? args = null, string? pwd = null, IProgress<ProgressReport>? progress = null)
    {
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

        p.OutputDataReceived -= receive;


        if (!IsSuccess(p.ExitCode))
        {
            progress?.Report(new(ProgressReportType.Error, null, 100));
        }

        return p.ExitCode;
    }

    public Task<bool> GetTiming(NeutrinoTrack track, string modelId, IProgress<ProgressReport>? progress = null)
    {
        var procExe = this.GetNeutrinoPath(NeutrinoExe);

        var args = string.Join(" ",
            PathUtil.Dq(track.GetFullLabelPath()),
            PathUtil.Dq(track.GetTimingLabelPath()),
            PathUtil.Dq(track.GetF0Path(modelId)),
            PathUtil.Dq(track.GetMspecPath(modelId)),
            this.GetModelPath(modelId),
            "-i", PathUtil.Dq(track.GetPhrasePath(modelId)),
            "-m -t -a");

        return Run(procExe, args, this.GetNeturinoDir(), progress).ContinueWith(t => t.Result != 0, TaskContinuationOptions.OnlyOnRanToCompletion);
    }

    public async Task<bool> EstimateFeatures(NeutrinoTrack track, string modelId, IProgress<ProgressReport>? progress = null)
    {
        var procExe = this.GetNeutrinoPath(NeutrinoExe);

        var args = string.Join(" ",
            PathUtil.Dq(track.GetFullLabelPath()),
            PathUtil.Dq(track.GetTimingLabelPath()),
            PathUtil.Dq(track.GetF0Path(modelId)),
            PathUtil.Dq(track.GetMspecPath(modelId)),
            this.GetModelPath(modelId),
            "-i", PathUtil.Dq(track.GetPhrasePath(modelId)),
            "-m -t");

        int exitCode = await Run(procExe, args, this.GetNeturinoDir(), progress)
            .ConfigureAwait(false);

        return IsSuccess(exitCode);
    }
}
