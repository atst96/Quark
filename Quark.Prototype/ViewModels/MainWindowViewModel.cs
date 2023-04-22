using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Livet.Messaging;
using Livet.Messaging.IO;
using Quark.Data.Projects;
using Quark.Drawing;
using Quark.Models.Neutrino;
using Quark.Mvvm;
using Quark.Projects;
using Quark.Projects.Tracks;
using Quark.Services;

namespace Quark.ViewModels;

internal class MainWindowViewModel : ViewModelBase, IProgress<ProgressReport>
{
    private NeutrinoV1Service _neutrino;
    private ProjectService _projects;

    private Project? _currentProject;

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

    private ModelInfo? _selectedModelInfo;
    /// <summary>
    /// 選択中のモデル
    /// </summary>
    public ModelInfo? SelectedModelInfo
    {
        get => this._selectedModelInfo;
        set => this.RaisePropertyChangedIfSet(ref this._selectedModelInfo, value);
    }

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
                this.SetTitle(value!.Name);
                if (value.Tracks.FirstOrDefault() is NeutrinoV1Track track)
                {
                    var foundModel = this.Models.FirstOrDefault(i => i.Id == track.Singer!.Id);
                    if (foundModel is not null)
                    {
                        this.SelectedModelInfo = foundModel;
                    }
                }
                this._saveCommand?.RaiseCanExecute();
            }
        }
    }

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

    public MainWindowViewModel(NeutrinoV1Service neutrino, ProjectService projects) : base()
    {
        this._neutrino = neutrino;
        this._projects = projects;
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
            this.CurrentProject = this._projects.Open(msg.Response[0], this._neutrino.GetModels());

            if (this.CurrentProject.Tracks.LastOrDefault() is NeutrinoV1Track t)
            {
                this.LoadTrack(this.CurrentProject, t);
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
        var track = this.CurrentProject!.Tracks.ImportFromMusicXml(path, Path.GetFileNameWithoutExtension(path), this.SelectedModelInfo!);
        this.LoadTrack(this.CurrentProject!, track);
    }

    /// <summary>初期選択のモデルID</summary>
    private const string TempDefaultModelId = "KIRITAN";

    private void UpdateModels()
    {
        // 前回選択のモデルID
        var model = this.SelectedModelInfo;

        // モデル一覧を取得
        this.Models = this._neutrino.GetModels();


        if (model is not null)
        {
            // 前回選択済みなら同じモデルを選択する
            this.SelectedModelInfo = this.Models.FirstOrDefault(m => m.Id == model.Id);
        }

        if (this.SelectedModelInfo is null)
        {
            // 前回未選択または前回のモデルが見つからない場合は既定のモデルを選択
            // それも見つからなければ最初のモデルを選択する
            this.SelectedModelInfo = this.Models.FirstOrDefault(m => m.Id == TempDefaultModelId)
                ?? this.Models.FirstOrDefault();
        }
    }

    private NeutrinoV1Track _currentTrack;
    public NeutrinoV1Track CurrentTrack
    {
        get => this._currentTrack;
        set
        {
            if (this.RaisePropertyChangedIfSet(ref this._currentTrack, value))
            {
                this.CurrentTime = TimeSpan.Zero;
                this.MaximumTime = TimeSpan.FromMilliseconds(value.GetFeatures().F0!.Length * 5);
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

    public async void LoadTrack(Project project, NeutrinoV1Track track)
    {
        // Label
        if (!track.HasScoreTiming())
        {
            var result = await this._neutrino.ConvertMusicXmlToTiming(track);
            if (result is null)
            {
                // TODO: 実行失敗時
                return;
            }

            track.FullTiming = result.FullTiming;
            track.MonoTiming = result.MonoTiming;
        }

        var features = track.GetFeatures(track.Singer!.Id);
        if (!features.HasTiming())
        {
            var result = await this._neutrino.GetTiming(track, features);
            if (result is null)
            {
                // TODO: 実行失敗時
                return;
            }

            features.Timing = result.Timing;
            features.F0 = null;
            features.Mgc = null;
            features.Bap = null;
        }

        // 推論
        if (!features.HasFeatures())
        {
            var vm = this.ProgressWindowViewModel;
            vm.Clear(closeable: false);

            _ = this.Messenger.RaiseAsync(new("OpenProgressWindow"));
            var result = await this._neutrino.EstimateFeatures(track, features, progress: vm);

            vm.CanClose();
            if (result is null)
            {
                // TODO: 実行失敗時
                return;
            }
            else
            {
                features.F0 = result.F0;
                features.Mgc = result.Mgc;
                features.Bap = result.Bap;

                // 進捗ウィンドウを閉じる
                vm.Close();
            }
        }

        App.Instance.Dispatcher.Invoke(() => this.CurrentTrack = track);
    }

    public void Report(ProgressReport value)
    {
        Debug.WriteLine(value);
    }
}
