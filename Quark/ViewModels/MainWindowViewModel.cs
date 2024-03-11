using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Quark.Components;
using Quark.Controls;
using Quark.Data.Settings;
using Quark.DependencyInjection;
using Quark.Drawing;
using Quark.Mvvm;
using Quark.Projects;
using Quark.Projects.Tracks;
using Quark.Services;
using Quark.UI.Mvvm;

namespace Quark.ViewModels;

[Prototype]
internal partial class MainWindowViewModel : ViewModelBase, IDialogServiceViewModel
{
    /// <inheritdoc/>
    public DialogService DialogService { get; }

    private readonly Settings _settings;
    private readonly ProjectService _projectService;
    private readonly TrackViewModelFactory _trackViewModelFactory;

    public MainWindowViewModel(DialogService dialogService, SettingService settingService, ProjectService projectService,
        TrackViewModelFactory trackViewModelFactory) : base()
    {
        this.DialogService = dialogService;
        this._settings = settingService.Settings;
        this._projectService = projectService;
        this._trackViewModelFactory = trackViewModelFactory;

        this.SelectProjectFileCommand = new AsyncRelayCommand(this.OnSelectProjectFileCommand);
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

    private string _title = App.AppName;

    /// <summary>
    /// ウィンドウタイトル
    /// </summary>
    public string Title
    {
        get => this._title;
        private set => this.SetProperty(ref this._title, value);
    }

    private Command? _openSettingWindowCommand;

    /// <summary>
    /// 設定ウィンドウを表示するコマンド
    /// </summary>
    public Command OpenSettingWindowCommand => this._openSettingWindowCommand ??= this.AddCommand(
        () => this.DialogService.ShowSettingWindow());

    /// <summary>
    /// クォンタイズの選択肢
    /// </summary>
    public ImmutableArray<ItemValueViewModel<LineType>> Quantizes { get; } = [
        new(LineType.Whole,           "1/1"),
        new(LineType.Note2th,         "1/2"),
        new(LineType.Note4th,         "1/4"),
        new(LineType.Note8th,         "1/8"),
        new(LineType.Note16th,        "1/16"),
        new(LineType.Note32th,        "1/32"),
        new(LineType.Note64th,        "1/64"),
        new(LineType.Note128th,       "1/128"),
        new(LineType.Note2thTriplet,  "1/2 連符"),
        new(LineType.Note4thTriplet,  "1/4 連符"),
        new(LineType.Note8thTriplet,  "1/8 連符"),
        new(LineType.Note16thTriplet, "1/16 連符"),
        new(LineType.Note32thTriplet, "1/32 連符"),
        new(LineType.Note64thTriplet, "1/64 連符"),
        new(LineType.Note2thDotted,   "1/2 付点"),
        new(LineType.Note4thDotted,   "1/4 付点"),
        new(LineType.Note8thDotted,   "1/8 付点"),
        new(LineType.Note16thDotted,  "1/16 付点"),
        new(LineType.Note32thDotted,  "1/32 付点"),
        new(LineType.Note64thDotted,  "1/64 付点"),
    ];

    private LineType _selectedQuantize = LineType.Note16th;

    /// <summary>表示するクォンタイズを取得または設定する</summary>
    public LineType SelectedQuantize
    {
        get => this._selectedQuantize;
        set => this.SetProperty(ref this._selectedQuantize, value);
    }

    /// <summary>
    /// 編集モードの選択肢リスト
    /// </summary>
    public ImmutableArray<ItemValueViewModel<EditMode>> EditModes { get; } = [
        new(EditMode.ScoreAndTiming, "スコア・音素タイミング編集"),
        new(EditMode.AudioFeatures, "音響パラメータ編集"),
    ];

    public NeutrinoTrackViewModelBase? TrackViewModel { get; private set; }

    private EditMode _selectedEditMode = EditMode.ScoreAndTiming;

    /// <summary>
    /// 編集モードを取得または設定する
    /// </summary>
    public EditMode SelectedEditMode
    {
        get => this._selectedEditMode;
        set
        {
            if (this.SetProperty(ref this._selectedEditMode, value)
                && this.TrackViewModel is { } track)
            {
                var estiamteMode = value switch
                {
                    // 編集モード以降は常に品質優先
                    EditMode.AudioFeatures => EstimateMode.Quality,
                    // スコア・音素タイミング編集の場合は設定に従う
                    _ => this._settings.Synthesis.EstimateMode,
                };

                // TODO:
                // track.ChangeEstimateMode(estimateMode);
            }
        }
    }

    private ProjectViewModel? _projectViewModel;

    public ProjectViewModel? ProjectViewModel
    {
        get => this._projectViewModel;
        private set => this.SetProperty(ref this._projectViewModel, value);
    }

    public ICommand SelectProjectFileCommand { get; }

    public async Task OnSelectProjectFileCommand()
    {
        // TODO: 既に開いているプロジェクトがあれば確認ダイアログで保存を促す

        var path = await this.DialogService.SelectOpenFileAsync(
           title: "プロジェクトファイルを開く",
           initialDirectory: GetRecentDirectory(RecentDirectoryType.OpenProjectFile),
           fileTypeFilters: [new("Quarkプロジェクト") { Patterns = ["*.qprj"] }]
           ).ConfigureAwait(false);

        if (path == null)
            return;

        this.SetRecentDirectory(RecentDirectoryType.OpenProjectFile, Path.GetDirectoryName(path)!);

        var project = this._projectService.Open(path);

        this.ChangeProject(project);
    }

    private void ChangeProject(Project project)
    {
        this.Title = $"{App.AppName} - {project.Name}";

        var projectViewModel = new ProjectViewModel(this._trackViewModelFactory, project);
        projectViewModel.SelectTrack(project.Tracks.OfType<INeutrinoTrack>().LastOrDefault()!);

        this.ProjectViewModel = projectViewModel;
        this.TrackViewModel = projectViewModel.SelectedTrack;
        this.OnPropertyChanged(nameof(this.TrackViewModel));


        //// 編集トラックを設定する
        //// NOTE: 現時点では必ず存在する
        //var track = project.Tracks.OfType<INeutrinoTrack>().LastOrDefault();
        //this.SetTrack(track);

        project.Player.BindDevice(() => new WasapiOut(AudioClientShareMode.Shared, 48 * 2));

        //// オーディオトラックを設定する
        //var audioFileTrack = project.Tracks.OfType<AudioFileTrack>().FirstOrDefault();
        //this.AudioTrackViewModel = audioFileTrack is not null ? new(audioFileTrack) : null;

        //this.RefreshAudio();
    }
}
