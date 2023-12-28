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
using Quark.Models.MusicXML;
using Quark.Data;
using Quark.Data.Settings;
using Quark.Neutrino;
using Quark.Components;
using Quark.Audio;

namespace Quark.ViewModels;

[Prototype]
internal class MainWindowViewModel : ViewModelBase, IProgress<ProgressReport>
{
    private ProjectService _projects;

    private Project? _currentProject;

    private DispatcherTimer _playerTimer;

    private NeutrinoV1Service _neutrinoV1;
    private NeutrinoV2Service _neutrinoV2;
    private Settings _settings;

    public bool HasProject => this._currentProject is not null;

    private ProgressWindowViewModel? _progressWindowViewModel;
    public ProgressWindowViewModel ProgressWindowViewModel
        => this._progressWindowViewModel ??= new("進捗状況", closeable: true);

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

                this._saveProjectFileCommand?.RaiseCanExecute();
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

    private NewProjectWindowViewModel? _newProjectViewModel;
    public NewProjectWindowViewModel NewProjectViewModel
        => this._newProjectViewModel ??= this.AddDisposable(new NewProjectWindowViewModel());

    public MainWindowViewModel(
        SettingService settings,
        ProjectService projects, NeutrinoV1Service v1Service, NeutrinoV2Service v2Service, NeutrinoV1Service neutrinoV1) : base()
    {
        this._neutrinoV1 = v1Service;
        this._neutrinoV2 = v2Service;

        this._settings = settings.Settings;
        this._projects = projects;

        this._playerTimer = new(
            TimeSpan.FromMilliseconds(1000d / 60),
            DispatcherPriority.Normal,
            this.OnMonitoringTimerTicked,
            App.Instance.Dispatcher)
        {
            IsEnabled = false,
        };
    }

    /// <summary>
    /// 直近で使用したフォルダを取得する
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    private string? GetRecentDirectory(RecentDirectoryType type)
        => this._settings.Recents.UseRecentDirectories ? this._settings.Recents.GetRecentDirectory(type) : null;

    /// <summary>
    /// 直近で使用したフォルダの設定を更新する。
    /// </summary>
    /// <param name="type"></param>
    /// <param name="path"></param>
    private void SetRecentDirectory(RecentDirectoryType type, string path)
        => this._settings.Recents.SetRecentDirectory(type, path);

    private ICommand? _openSettingWindowCommand;
    public ICommand OpenSettingWindowCommand
    {
        get => this._openSettingWindowCommand ??= new DelegateCommand(() =>
        {
            this.Messenger.Raise(new TransitionMessage("OpenSettingWindow"));
        });
    }

    private Command? _selectMusicXmlForNewProjectCommand;

    /// <summary>MusicXMLを選択する</summary>
    public Command SelectMusicXmlForNewProjectCommand => this._selectMusicXmlForNewProjectCommand ??= this.AddCommand(() =>
    {
        // TODO: 未保存の場合は確認ダイアログを表示する

        var message = new OpeningFileSelectionMessage("SelectNewProjectMusicXml")
        {
            Title = "インポートするMusicXMLを選択",
            Filter = "MusicXMLファイル|*.musicxml;*.xml;*.mxl",
            InitialDirectory = this.GetRecentDirectory(RecentDirectoryType.ImportMusicXml),
        };

        this.Messenger.Raise(message);

        var paths = message.Response;
        if (paths is not { Length: > 0 })
            // キャンセルされたら何も入っていないので処理を終わる
            return;

        var path = paths.First();
        this.SetRecentDirectory(RecentDirectoryType.ImportMusicXml, Path.GetDirectoryName(path)!);

        (PartList.ScorePartElement Info, Models.MusicXML.Part Part)[] parts;
        using (var fs = File.OpenRead(path))
        {
            parts = MusicXmlUtil.EnumerateParts(fs).ToArray();
        }

        var viewModel = new MusicXMLImportWindowViewModel(path, parts.Select(p => p.Info));
        this.Messenger.Raise(new TransitionMessage(viewModel, "SelectImportMusicXmlPart"));

        var selectParts = viewModel.Response;
        if (selectParts is not { Length: > 0 })
            // 未選択の場合は特に何もしない
            return;

        var project = this._projects.Create(viewModel.ProjectName);
        foreach (var selectedPart in selectParts)
        {
            var part = parts[selectedPart.Index];
            var singer = selectedPart.Singer!;
            var modelType = singer.ModelType;

            if (modelType == ModelType.NeutorinoV1)
            {
                project.Tracks.ImportFromMusicXmlV1(part.Part, selectedPart.TrackName, singer);
            }
            else if (modelType == ModelType.NeutorinoV2)
            {
                project.Tracks.ImportFromMusicXmlV2(part.Part, selectedPart.TrackName, singer);
            }
        }

        this.SetProject(project);
    });

    private Command? _selectProjectFileCommand;

    /// <summary>プロジェクトファイル選択コマンド</summary>
    public Command SelectProjectFileCommand => this._selectProjectFileCommand ??= this.AddCommand(
        () =>
        {
            var message = new OpeningFileSelectionMessage("OpenProjectFileDialog")
            {
                InitialDirectory = GetRecentDirectory(RecentDirectoryType.OpenProjectFile),
                Title = "プロジェクトファイルを開く",
                Filter = "Quarkプロジェクト(*.qprj)|*.qprj",
            };

            this.Messenger.Raise(message);

            var paths = message.Response;
            if (paths is not { Length: > 0 })
                return; // 未選択の場合は処理しない

            var path = paths.First();
            this.SetRecentDirectory(RecentDirectoryType.OpenProjectFile, Path.GetDirectoryName(path)!);

            var project = this._projects.Open(paths[0]);
            this.SetProject(project);
        });

    private Command? _saveProjectFileCommand;

    /// <summary>プロジェクトファイル保存コマンド</summary>
    public Command SaveProjectFileCommand => this._saveProjectFileCommand ??= this.AddCommand(
        () => this._currentProject is not null,
        () =>
        {
            if (this.CurrentProject!.ProjectFilePath is null)
            {
                var message = new SavingFileSelectionMessage("SaveProjectDialog")
                {
                    InitialDirectory = this.GetRecentDirectory(RecentDirectoryType.SaveProjectFile),
                    Title = "プロジェクトファイルを保存",
                    Filter = "Quarkプロジェクト(*.qprj)|*.qprj",
                };

                this.Messenger.Raise(message);

                var paths = message.Response;
                if (paths is not { Length: > 0 })
                    return; // 未選択の場合は処理しない

                var path = paths.First();
                this.SetRecentDirectory(RecentDirectoryType.SaveProjectFile, Path.GetDirectoryName(path)!);

                this.SaveProject(path);
            }
            else
            {
                this.SaveProject();
            }
        });

    private Command _exportWaveCommand;
    public Command ExportWaveCommand => this._exportWaveCommand ??= this.AddCommand(async () =>
    {
        if (!this.HasProject)
            return; // プロジェクトを開いていない場合は処理しない

        var message = new SavingFileSelectionMessage("ShowExportWavDialog")
        {
            Title = "WAVEファイルをエクスポート",
            Filter = "WAVEファイル(*.wav)|*.wav",
            InitialDirectory = this.GetRecentDirectory(RecentDirectoryType.ExportWav),
        };

        this.Messenger.Raise(message);

        var paths = message.Response;
        if (paths is not { Length: > 0 })
            return; // 未選択の場合は処理しない

        var path = paths.First();
        this.SetRecentDirectory(RecentDirectoryType.ExportWav, Path.GetDirectoryName(path)!);

        var track = this.CurrentTrack;
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
                 : service.SynthesisNSF(v2Track, path, NSFV2Model.Get(NeutrinoV2InferenceMode.Advanced), progress);
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
    });

    /// <summary>
    /// 音声合成後WAVファイルの出力先選択時
    /// </summary>
    /// <param name="message"></param>
    /// <exception cref="NotSupportedException"></exception>
    public void OnWaveExportFileSelected(SavingFileSelectionMessage message)
    {
        if (!this.HasProject || message is not { Response.Length: > 0 })
        {
            // 未選択(キャンセル)の場合は処理しない
            return;
        }
    }

    private void SetProject(Project project)
    {
        this.CurrentProject = project;
        this._projectSession = project.Session;

        // 編集トラックを設定する
        // NOTE: 現時点では必ず存在する
        var track = project.Tracks.OfType<INeutrinoTrack>().LastOrDefault();
        this.SetTrack(track);

        // オーディオトラックを設定する
        var audioFileTrack = project.Tracks.OfType<AudioFileTrack>().FirstOrDefault();
        this.AudioTrackViewModel = audioFileTrack is not null ? new(audioFileTrack) : null;

        this.RefreshAudio();
    }

    private void SetTrack(INeutrinoTrack? track)
    {
        this.CurrentTrack = track!;
    }

    private void RefreshAudio()
    {
        if (this.CurrentTrack == null)
        {
            this.CloseAudio();
        }
        else
        {
            this.InitAudio();
        }
    }

    private void SaveProject(string? path = null)
    {
        this.CurrentProject!.SaveToFile(path);
    }

    private TimeSpan? _beginPlayTime = null;
    private ProjectPlayer _player;

    private bool _isPlaying;
    public bool IsPlaying
    {
        get => this._isPlaying;
        private set => this.RaisePropertyChangedIfSet(ref this._isPlaying, value);
    }

    private void CloseAudio()
    {
        if (this._player is { } player)
        {
            player.PlaybackStopped -= this.OnPlayerStopped;
            player.Dispose();
        }
    }

    private void InitAudio()
    {
        this.CloseAudio();

        if (this.CurrentProject is not { } project)
            return;

        var device = new ProjectPlayer(project);
        device.BindDevice(() => new WasapiOut(AudioClientShareMode.Shared, Latency));

        device.PlaybackStopped += this.OnPlayerStopped;

        this._player = device;
    }

    private void OnPlayerStopped(object? sender, StoppedEventArgs e) => App.Instance.Dispatcher.InvokeAsync(() =>
    {
        this.IsPlaying = false;
    });

    private INeutrinoTrack _currentTrack;
    public INeutrinoTrack CurrentTrack
    {
        get => this._currentTrack;
        set
        {
            var track = value;

            if (this.RaisePropertyChangedIfSet(ref this._currentTrack, track))
            {
                this.PlayingTime = TimeSpan.Zero;
                this.SelectionTime = TimeSpan.Zero;
                if (track.HasTimings())
                {
                    track.ApplyEstimaetMode();
                }
                // this.MaximumTime = TimeSpan.FromMilliseconds(value.GetFeatures().F0!.Length * 5);
            }
        }
    }

    private TimeSpan _selectionTime = TimeSpan.Zero;
    public TimeSpan SelectionTime
    {
        get => this._selectionTime;
        set
        {
            this.RaisePropertyChangedIfSet(ref this._selectionTime, value);
            this.UpdatePlayingTime(value);
        }
    }

    private void UpdatePlayingTime(TimeSpan playingTime)
    {
        this.PlayingTime = playingTime;
        if (this.IsPlaying)
        {
            this.SeekPlayer(playingTime);
        }
    }

    private void SeekPlayer(TimeSpan time)
    {
        this._player?.Seek(time);
    }

    private TimeSpan _playingTime = TimeSpan.Zero;
    public TimeSpan PlayingTime
    {
        get => this._playingTime;
        private set => this.RaisePropertyChangedIfSet(ref this._playingTime, value);
    }

    private TimeSpan _maximumTime = TimeSpan.Zero;
    public TimeSpan MaximumTime
    {
        get => this._maximumTime;
        private set => this.RaisePropertyChangedIfSet(ref this._maximumTime, value);
    }

    private LineType _selectedQuantize = LineType.Note16th;
    public LineType SelectedQuantize
    {
        get => this._selectedQuantize;
        set => this.RaisePropertyChangedIfSet(ref this._selectedQuantize, value);
    }

    private Command _playCommand;
    public Command PlayCommand => this._playCommand ??= this.AddCommand(() => this.StartPlayer());

    private Command _stopCommand;
    public Command StopCommand => this._stopCommand ??= this.AddCommand(() => this.StopPlayer(false));

    private Command _stopRestoreCommand;
    public Command StopRestoreCommand => this._stopRestoreCommand ??= this.AddCommand(() => this.StopPlayer(true));

    /// <summary>
    /// 再生開始する。
    /// </summary>
    /// <param name="beginTime">再生開始位置</param>
    private void StartPlayer(TimeSpan? beginTime = null)
    {
        // 再生開始時間を設定
        if (beginTime != null)
        {
            this._beginPlayTime = beginTime.Value;
            this.PlayingTime = beginTime.Value;
        }
        else
        {
            this._beginPlayTime = this.PlayingTime;
        }

        // 再生開始
        if (this._player is { } player)
        {
            this.SeekPlayer(this.PlayingTime);
            player.Play();

            this.IsPlaying = true;
            // 再生位置監視タイマを開始
            this.StartMonitoringTimer();
        }
    }

    /// <summary>
    /// プレーやを停止する。
    /// </summary>
    /// <param name="restore">再生位置を戻すフラグ</param>
    private void StopPlayer(bool restore)
    {
        if (this._player is { } player)
        {
            // 再生位置監視タイマを停止
            this.StopMonitoringTimer();
            // 再生停止
            player.Stop();

            if (restore && this._beginPlayTime is { } beginTime)
                this.PlayingTime = beginTime;
        }

        this.IsPlaying = false;
    }

    /// <summary>再生位置監視タイマを開始</summary>
    private void StartMonitoringTimer()
        => this._playerTimer.Start();

    /// <summary>再生位置監視タイマを停止</summary>
    private void StopMonitoringTimer()
        => this._playerTimer.Stop();

    private const int Latency = 48 * 2; // 48kHz * 2bytes

    /// <summary>
    /// 再生位置監視タイマのイベント発火時
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnMonitoringTimerTicked(object? sender, EventArgs e)
    {
        if (this.CurrentTrack?.AudioStream is { } stream)
        {
            int millis = Math.Max(0, (int)(stream.Position / (stream.OriginalWaveFormat.AverageBytesPerSecond / 1000d)) - Latency);
            this.PlayingTime = TimeSpan.FromMilliseconds(millis);
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
        this.StopMonitoringTimer();

        this._projectSession?.EndSession();

        var player = this._player;
        if (player != null && player.PlaybackState != PlaybackState.Stopped)
        {
            player.Stop();
        }
    }));

    private Command _togglePlayCommand;
    public Command TogglePlayCommand => this._togglePlayCommand ??= this.AddCommand(() =>
    {
        if (this._player is { } player)
        {
            if (player.PlaybackState != PlaybackState.Playing)
                this.StartPlayer();
            else
                this.StopPlayer(false);
        }
    });

    private Command _togglePlayResumeCommand;
    public Command TogglePlayResumeCommand => this._togglePlayResumeCommand ??= this.AddCommand(() =>
    {
        if (this._player is { } player)
        {
            if (player.PlaybackState != PlaybackState.Playing)
                this.StartPlayer(this._beginPlayTime ?? this.PlayingTime);
            else
                this.StopPlayer(true);
        }
    });

    private EditMode _selectedEditMode;

    /// <summary>編集モード</summary>
    public EditMode SelectedEditMode
    {
        get => this._selectedEditMode;
        set
        {
            if (this.RaisePropertyChangedIfSet(ref this._selectedEditMode, value)
                && this.CurrentTrack is { } track)
            {
                var estimateMode = value switch
                {
                    EditMode.AudioFeatures => EstimateMode.Quality,
                    _ => this._settings.Synthesis.EstimateMode,
                };

                track.ChangeEstimateMode(estimateMode);
            }
        }
    }
    public bool HasAudioTrack { get; private set; }

    private AudioTrackViewModel? _audioTrackViewModel;
    public AudioTrackViewModel? AudioTrackViewModel
    {
        get => this._audioTrackViewModel;
        set
        {
            if (this.RaisePropertyChangedIfSet(ref this._audioTrackViewModel, value))
            {
                this.HasAudioTrack = value is not null;
                this.RaisePropertyChanged(nameof(this.HasAudioTrack));
            }
        }
    }

    private Command _changeAudioFileTrackCommand;
    public Command ChangeAudioFileTrackCommand => this._changeAudioFileTrackCommand ??= this.AddCommand(() =>
    {
        if (this.CurrentProject is not { } project)
            return;

        this.DeleteAudioFileTrack(project);

        var message = new OpeningFileSelectionMessage("OpenProjectFileDialog")
        {
            InitialDirectory = this.GetRecentDirectory(RecentDirectoryType.SelectAudioFile),
            Title = "音楽ファイルを開く",
            Filter = "WAVファイル(*.wav)|*.wav",
        };

        this.SetRecentDirectory(RecentDirectoryType.SelectAudioFile, message.InitialDirectory!);

        this.Messenger.Raise(message);

        var paths = message.Response;
        if (paths is not { Length: > 0 })
            return; // 未選択の場合は処理しない

        var path = paths.First();

        var newTrack = new AudioFileTrack(project, "Audio File", path);
        project.Tracks.Add(newTrack);
        this.AudioTrackViewModel = new(newTrack);

        this._player.Seek(this.PlayingTime);
    });

    private Command _deleteAudioFileTrackCommand;
    public Command DeleteAudioFileTrackCommand => this._deleteAudioFileTrackCommand ??= this.AddCommand(() =>
    {
        if (this.CurrentProject is not { } project)
            return;

        this.DeleteAudioFileTrack(project);
    });

    private void DeleteAudioFileTrack(Project project)
    {
        // オーディオトラックが存在するなら削除する
        var targetTrack = project.Tracks.OfType<AudioFileTrack>().FirstOrDefault();
        if (targetTrack is not null)
        {
            project.Tracks.Remove(targetTrack);
            this.AudioTrackViewModel = null;
        }
    }
}
