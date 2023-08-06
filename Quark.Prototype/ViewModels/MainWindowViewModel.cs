using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using Livet.Messaging;
using Livet.Messaging.IO;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Quark.Audio;
using Quark.Behaviors;
using Quark.Controls;
using Quark.Data.Projects;
using Quark.DependencyInjection;
using Quark.Drawing;
using Quark.Models.Neutrino;
using Quark.Mvvm;
using Quark.Projects;
using Quark.Projects.Tracks;
using Quark.Services;
using Quark.Utils;

namespace Quark.ViewModels;

[Prototype]
internal class MainWindowViewModel : ViewModelBase, IProgress<ProgressReport>
{
    private ProjectService _projects;

    private Project? _currentProject;

    private DispatcherTimer _timer;

    private NeutrinoV1Service _neutrinoV1;
    private NeutrinoV2Service _neutrinoV2;

    public bool HasProject => this._currentProject is not null;

    private ProgressWindowViewModel? _progressWindowViewModel;
    public ProgressWindowViewModel ProgressWindowViewModel
        => this._progressWindowViewModel ??= new("進捗状況", closeable: false);

    public Dictionary<LineType, string> Quantizes { get; } = new()
    {
        [LineType.Whole] = "1/1",
        [LineType.Note2th] = "1/2",
        [LineType.Note4th] = "1/4",
        [LineType.Note8th] = "1/8",
        [LineType.Note16th] = "1/16",
        [LineType.Note32th] = "1/32",
        [LineType.Note64th] = "1/64",
        [LineType.Note128th] = "1/128",
        [LineType.Note2thTriplet] = "1/2 連符",
        [LineType.Note4thTriplet] = "1/4 連符",
        [LineType.Note8thTriplet] = "1/8 連符",
        [LineType.Note16thTriplet] = "1/16 連符",
        [LineType.Note32thTriplet] = "1/32 連符",
        [LineType.Note64thTriplet] = "1/64 連符",
        [LineType.Note2thDotted] = "1/2 付点",
        [LineType.Note4thDotted] = "1/4 付点",
        [LineType.Note8thDotted] = "1/8 付点",
        [LineType.Note16thDotted] = "1/16 付点",
        [LineType.Note32thDotted] = "1/32 付点",
        [LineType.Note64thDotted] = "1/64 付点",
    };

    public IReadOnlyDictionary<EditMode, string> EditModes { get; } = new Dictionary<EditMode, string>
    {
        [EditMode.ScoreAndTiming] = "スコア・音素タイミング編集",
        [EditMode.AudioFeatures] = "音響パラメータ編集",
    };

    private ModelInfo? _selectedModelInfo;
    /// <summary>
    /// 選択中のモデル
    /// </summary>
    public ModelInfo? SelectedModelInfo
    {
        get => this._selectedModelInfo;
        set => this.RaisePropertyChangedIfSet(ref this._selectedModelInfo, value);
    }

    public bool IsProjectCreated => this._currentProject != null;

    /// <summary>
    /// 現在のプロジェクト
    /// </summary>
    public Project? CurrentProject
    {
        get => this._currentProject;
        private set
        {
            if (this.RaisePropertyChangedIfSet(ref this._currentProject, value, nameof(this.HasProject)))
            {
                this.RaisePropertyChanged(nameof(this.IsProjectCreated));

                this.SetTitle(value!.Name);
                if (value.Tracks.FirstOrDefault() is NeutrinoV1Track track)
                {
                    var foundModel = this.Models.FirstOrDefault(i => i.ModelId == track.Singer!.ModelId);
                    if (foundModel is not null)
                    {
                        this.SelectedModelInfo = foundModel;
                    }
                }
                this._saveCommand?.RaiseCanExecute();
            }
        }
    }

    private ProjectSession? _projectSession;
    private string _title = App.AppName;
    public string Title
    {
        get => this._title;
        private set => this.RaisePropertyChangedIfSet(ref this._title, value);
    }

    private string SetTitle(string title)
        => this.Title = $"{title} - {App.AppName}";

    private IList<ModelInfo> _models = new List<ModelInfo>();
    /// <summary>
    /// 選択可能なモデル情報
    /// </summary>
    public IList<ModelInfo> Models
    {
        get => this._models;
        set => this.RaisePropertyChangedIfSet(ref this._models, value);
    }

    private NewProjectWindowViewModel? _newProjectViewModel;
    public NewProjectWindowViewModel NewProjectViewModel
        => this._newProjectViewModel ??= this.AddDisposable(new NewProjectWindowViewModel());

    public MainWindowViewModel(ProjectService projects, NeutrinoV1Service v1Service, NeutrinoV2Service v2Service, NeutrinoV1Service neutrinoV1) : base()
    {
        this._neutrinoV1 = v1Service;
        this._neutrinoV2 = v2Service;

        this._projects = projects;

        this._timer = new(
            TimeSpan.FromMilliseconds(1000d / 60),
            DispatcherPriority.Normal,
            this.OnPlayerTimerTicked,
            App.Instance.Dispatcher)
        {
            IsEnabled = false,
        };

        this.UpdateModels();
    }

    private ICommand? _openSettingWindowCommand;
    public ICommand OpenSettingWindowCommand
    {
        get => this._openSettingWindowCommand ??= new DelegateCommand(() =>
        {
            this.Messenger.Raise(new TransitionMessage("OpenSettingWindow"));
            this.UpdateModels();
        });
    }

    public ICommand? _newProjectCommand;
    public ICommand NewProjectCommand
    {
        get => this._newProjectCommand ??= this.AddCommand(() =>
            this.Messenger.Raise(new InteractionMessage("OpenNewProjectWindow")));
    }

    public void OnNewProjectSelected(TransitionMessage msg)
    {
        var viewModel = (NewProjectWindowViewModel)msg.TransitionViewModel;
        if (viewModel is { IsInvalid: true })
        {
            this.CurrentProject = this._projects.Create(viewModel.ProjectName);
            this._projectSession = this.CurrentProject.Session;
        }
    }

    private Command? _openCommand;
    public Command OpenCommand => this._openCommand ??= this.AddCommand(() =>
    {
        this.Messenger.Raise(new("OpenProjectDialog"));
    });

    private Command? _saveCommand;
    public Command SaveCommand => this._saveCommand ??= this.AddCommand(
        () => this._currentProject is not null,
        () =>
        {
            if (this.CurrentProject!.ProjectFilePath is null)
            {
                this.Messenger.Raise(new("SaveProjectDialog"));
            }
            else
            {
                this.SaveProject();
            }
        });

    public void OnOpenProjectFileSelected(OpeningFileSelectionMessage msg)
    {
        if (msg is { Response.Length: > 0 })
        {
            this.CurrentProject = this._projects.Open(msg.Response[0]);
            this._projectSession = this.CurrentProject.Session;

            if (this.CurrentProject.Tracks.LastOrDefault() is INeutrinoTrack t)
            {
                this.CurrentTrack = t;
                this.InitAudio(t);
            }
        }
    }

    public void OnSaveProjectFileSelected(SavingFileSelectionMessage msg)
    {
        if (msg is { Response.Length: > 0 })
        {
            this.SaveProject(msg.Response[0]);
        }
    }

    private void SaveProject(string? path = null)
    {
        this.CurrentProject!.SaveToFile(path);
    }

    public void OnImportMusicXMLFileSelected(OpeningFileSelectionMessage msg)
    {
        if (!this.HasProject || msg is not { Response.Length: > 0 })
        {
            return;
        }

        var path = msg.Response[0];


        using var fs = File.OpenRead(path);
        // {
        var parts = MusicXmlUtil.EnumerateParts(fs);
        var part = parts.First();
        //}

        var track = this.CurrentProject!.Tracks.ImportFromMusicXmlV2(part.Part, part.Info?.PartName ?? Path.GetFileNameWithoutExtension(path), this.SelectedModelInfo!);
        this.CurrentTrack = track;
        this.InitAudio(track);
    }

    public async void OnWaveExportFileSelected(SavingFileSelectionMessage msg)
    {
        if (!this.HasProject || msg is not { Response.Length: > 0 })
        {
            return;
        }

        var track = this.CurrentTrack;
        var path = msg.Response[0];

        var progress = this.ProgressWindowViewModel;
        progress.Clear();

        Task task;
        if (track is NeutrinoV1Track v1Track)
        {
            var service = this._neutrinoV1;

            // TODO: 出力方法を選択できるようにする
            bool isWorldOutput = false;
            // bool isWorldOutput =  track.OutputConfig.ExportType == ExprotType.World

            task = isWorldOutput
                // WORLDで合成
                ? service.SynthesisWorld(v1Track, path, progress)
                // NSFで合成
                : service.SynthesisNSF(v1Track, path, progress);
        }
        else if (track is NeutrinoV2Track v2Track)
        {
            var service = this._neutrinoV2;

            // TODO: 出力方法を選択できるようにする
            bool isWorldOutput = false;
            // bool isWorldOutput =  track.OutputConfig.ExportType == ExprotType.World

            task = isWorldOutput
                 // WORLDで合成
                 ? service.SynthesisWorld(v2Track, path, progress)
                 // NSFで合成
                 : service.SynthesisNSF(v2Track, path, progress);
        }
        else
        {
            throw new NotSupportedException("");
        }

        // 進捗ダイアログを表示
        _ = this.Messenger.RaiseAsync(new("OpenProgressWindow"));

        try
        {
            await task;
            progress.Close();
        }
        catch (Exception)
        {
            // TODO: 
        }
    }

    private IWavePlayer _player;
    private WaveDataStream _waveStream;

    private void InitAudio(INeutrinoTrack track)
    {
        this._waveStream?.Dispose();

        var device = new WasapiOut(AudioClientShareMode.Shared, Latency);
        var waveStream = new WaveDataStream(track.WaveData);

        device.Init(waveStream);

        this._player = device;
        this._waveStream = waveStream;
    }

    /// <summary>初期選択のモデルID</summary>
    private const string TempDefaultModelId = "KIRITAN";

    private void UpdateModels()
    {
        // 前回選択のモデルID
        var model = this.SelectedModelInfo;

        // モデル一覧を取得
        this.Models = this._neutrinoV1.GetModels();


        if (model is not null)
        {
            // 前回選択済みなら同じモデルを選択する
            this.SelectedModelInfo = this.Models.FirstOrDefault(m => m.ModelId == model.ModelId);
        }

        if (this.SelectedModelInfo is null)
        {
            // 前回未選択または前回のモデルが見つからない場合は既定のモデルを選択
            // それも見つからなければ最初のモデルを選択する
            this.SelectedModelInfo = this.Models.FirstOrDefault(m => m.ModelId == TempDefaultModelId)
                ?? this.Models.FirstOrDefault();
        }
    }

    private INeutrinoTrack _currentTrack;
    public INeutrinoTrack CurrentTrack
    {
        get => this._currentTrack;
        set
        {
            if (this.RaisePropertyChangedIfSet(ref this._currentTrack, value))
            {
                this.CurrentTime = TimeSpan.Zero;
                // this.MaximumTime = TimeSpan.FromMilliseconds(value.GetFeatures().F0!.Length * 5);
            }
        }
    }

    private TimeSpan _currentTime = TimeSpan.Zero;
    public TimeSpan CurrentTime
    {
        get => this._currentTime;
        set => this.RaisePropertyChangedIfSet(ref this._currentTime, value);
    }


    private TimeSpan _maximumTime = TimeSpan.Zero;
    public TimeSpan MaximumTime
    {
        get => this._maximumTime;
        private set => this.RaisePropertyChangedIfSet(ref this._maximumTime, value);
    }

    private LineType _selectedQuantize = LineType.Note4th;
    public LineType SelectedQuantize
    {
        get => this._selectedQuantize;
        set => this.RaisePropertyChangedIfSet(ref this._selectedQuantize, value);
    }

    private Command _playCommand;
    public Command PlayCommand => this._playCommand ??= this.AddCommand(() =>
    {
        this._player?.Play();
        this.StartPlayTimer();
    });

    private Command _pauseCommand;
    public Command PauseCommand => this._pauseCommand ??= this.AddCommand(() =>
    {
        this._player?.Pause();
        this.StopPlayTimer();
    });

    private Command _stopCommand;
    public Command StopCommand => this._stopCommand ??= this.AddCommand(() =>
    {
        this._player?.Stop();
        this.StopPlayTimer();
    });

    private void StartPlayTimer()
    {
        this._waveStream.Position = (int)((double)this._waveStream.WaveFormat.AverageBytesPerSecond / 1000 * (int)this.CurrentTime.TotalMilliseconds);

        this._timer.Start();
    }

    private void StopPlayTimer()
    {
        this._timer.Stop();
    }

    private const int Latency = 96 * 1;

    private void OnPlayerTimerTicked(object? sender, EventArgs e)
    {
        var stream = this._waveStream;

        if (stream is not null)
        {
            int millis = Math.Max(0, (int)(stream.Position / 96) - Latency);

            this.CurrentTime = TimeSpan.FromMilliseconds(millis);
        }
    }

    public void Report(ProgressReport value)
    {
        Debug.WriteLine(value);
    }

    private Command<WindowCloseRequest> _onCloseCommand;
    public Command<WindowCloseRequest> OnClosingCommand => this._onCloseCommand ??= (this.AddCommand(
        (WindowCloseRequest request) =>
    {
        this.StopPlayTimer();

        this._projectSession?.EndSession();

        var player = this._player;
        if (player != null && player.PlaybackState != PlaybackState.Stopped)
        {
            player.Stop();
        }
    }));
}
