using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Quark.Components;
using Quark.Controls;
using Quark.Data.Settings;
using Quark.DependencyInjection;
using Quark.Drawing;
using Quark.Models.MusicXML;
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
    private readonly ProjectManager _projectManager;
    private readonly ViewModelFactory _trackViewModelFactory;

    public MainWindowViewModel(DialogService dialogService, SettingService settingService,
        ViewModelFactory trackViewModelFactory, ProjectFactory projectFactory) : base()
    {
        this.DialogService = dialogService;
        this._settings = settingService.Settings;
        this._trackViewModelFactory = trackViewModelFactory;
        this._projectManager = projectManager;

        this.SelectProjectFileCommand = new AsyncRelayCommand(this.OnSelectProjectFileCommand);
        this.SelectMusicXmlForNewProjectCommand = new AsyncRelayCommand(this.SelectMusicXmlForNewProject);
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

    /// <summary>プロジェクトを読み込み済みかどうかを取得する</summary>
    public bool HasProject { get; private set; }

    /// <summary>プロジェクトのViewModel</summary>
    public ProjectViewModel? ProjectViewModel { get; private set; }

    /// <summary>トラックのViewModel</summary>
    public NeutrinoTrackViewModelBase? TrackViewModel { get; private set; }


    /// <summary>
    /// プロジェクトを名前を付けて保存
    /// </summary>
    /// <param name="project">プロジェクト</param>
    /// <returns></returns>
    public async Task<bool> SaveProjectAs(Project project)
    {
        var path = await this.DialogService.SelectSaveFileAsync(
           title: "プロジェクトファイルを保存",
           initialDirectory: this.GetRecentDirectory(RecentDirectoryType.SaveProjectFile),
           fileTypeFilters: [new("Quarkプロジェクト") { Patterns = ["*.qprj"] }]
           ).ConfigureAwait(false);

        if (path == null)
            return false; // キャンセルされた場合は処理を終了する

        this.SetRecentDirectory(RecentDirectoryType.SaveProjectFile, Path.GetDirectoryName(path)!);

        project.SaveToFile(path, true);

        // TODO: エラーハンドリング。エラー発生時は例外をラップしたい
        return true;
    }

    private AsyncRelayCommand? _saveCommand;
    public AsyncRelayCommand SaveCommand => this._saveCommand ??= new AsyncRelayCommand(
        () => this.SaveProject(this.ProjectViewModel!.Project),
        () => this.ProjectViewModel != null);

    /// <summary>
    /// プロジェクトを保存する
    /// </summary>
    /// <param name="project"></param>
    /// <returns>プロジェクト</returns>
    public async Task<bool> SaveProject(Project project)
    {
        // TODO: エラーハンドリング
        if (project.IsNewFile())
        {
            // 新しいファイルの場合(ファイル名が不明)の場合は名前をつけて保存
            return await this.SaveProjectAs(project).ConfigureAwait(false);
        }
        else
        {
            project.SaveToFile();
            return true;
        }
    }

    /// <summary>
    /// プロジェクトを閉じられる状態にする
    /// </summary>
    /// <returns>後続処理実行の可否</returns>
    public async Task<bool> ConfirmProjecttClosable()
    {
        var project = this.ProjectViewModel?.Project;
        if (project == null)
            // プロジェクトが開かれていない場合は何もしない
            return true;

        // TODO: 変更点監視処理を実装する
        bool isProjectChanged = false; // project.IsChanged; 
        if (isProjectChanged)
        {
            // TODO: 保存するかどうかを尋ねるダイアログを実装する
            // 保存しますか？→はい／いいえ／キャンセル
            bool saveRequested = true;
            if (saveRequested)
            {
                // TODO: エラーハンドリング
                await this.SaveProject(project).ConfigureAwait(false);
                return true;
            }
            else
            {
                // TODO: 保存しない場合はtrueを返す。
                // キャンセルした場合はfalaseを返す。
                return false;
            }
        }
        else
        {
            // プロジェクトに変更がない場合
            return true;
        }
    }

    private void UpdateCommands() => Dispatcher.UIThread.Invoke(() =>
    {
        this.SaveCommand.NotifyCanExecuteChanged();
    });

    /// <summary>
    /// プロジェクトを閉じる
    /// </summary>
    /// <returns></returns>
    public async Task CloseProject()
    {
        var currentProject = this.ProjectViewModel?.Project;
        if (currentProject is null)
            // 開いているプロジェクトがなければ処理終了
            return;

        // プロジェクトを閉じる。
        // 子プロセスが稼働している可能性があるので、終了するまで待機する。
        // TODO: 処理に時間がかかる場合(200ms以上くらい?)、ダイアログを表示してクローズ処理中である旨を表示させたい
        await this._projectManager.CloseCurrentProject();

        // ViewModelを破棄・更新する
        this.ProjectViewModel?.Dispose();
        this.ProjectViewModel = null;
        this.TrackViewModel = null;
        this.HasProject = false;
        this.OnPropertyChanged(nameof(this.HasProject));
        this.OnPropertyChanged(nameof(this.ProjectViewModel));
        this.OnPropertyChanged(nameof(this.TrackViewModel));

        this.UpdateCommands();
    }

    /// <summary>MusicXMLを選択する</summary>
    public ICommand SelectMusicXmlForNewProjectCommand { get; }

    public async Task SelectMusicXmlForNewProject()
    {
        // 開いているプロジェクトがあれば閉じる
        if (!await this.ConfirmProjecttClosable().ConfigureAwait(false))
            return;

        var path = await this.DialogService.SelectOpenFileAsync(
           title: "インポートするMusicXMLを選択",
           initialDirectory: this.GetRecentDirectory(RecentDirectoryType.ImportMusicXml),
           fileTypeFilters: [new("MusicXMLファイル") { Patterns = ["*.musicxml", "*.xml", "*.mxl"] }]
           ).ConfigureAwait(false);

        if (path == null)
            // キャンセルされた場合は処理を終了する
            return;

        this.SetRecentDirectory(RecentDirectoryType.ImportMusicXml, Path.GetDirectoryName(path)!);

        (ScorePartElement Info, Models.MusicXML.Part Part)[] parts;
        using (var fs = File.OpenRead(path))
        {
            parts = MusicXmlUtil.EnumerateParts(fs).ToArray();
        }

        var viewModel = this._trackViewModelFactory.GetMusicXmlImportViewModel(path, parts.Select(p => p.Info));

        await this.DialogService.ImportMusicXmlAsync(viewModel).ConfigureAwait(false);

        if (viewModel.Response is not { Length: > 0 } selectParts)
            // 未選択の場合は特に何もしない
            return;

        // 新しいプロジェクトが読み込めたら、現在のプロジェクトを閉じる
        await this.CloseProject().ConfigureAwait(false);

        var project = await this._projectManager.CreateFromMxl(viewModel.ProjectName, parts, selectParts)
            .ConfigureAwait(false);

        this.ChangeProject(project);
    }

    public ICommand SelectProjectFileCommand { get; }

    public async Task OnSelectProjectFileCommand()
    {
        // 開いているプロジェクトがあれば閉じる
        if (!await this.ConfirmProjecttClosable().ConfigureAwait(false))
            return;

        var path = await this.DialogService.SelectOpenFileAsync(
           title: "プロジェクトファイルを開く",
           initialDirectory: this.GetRecentDirectory(RecentDirectoryType.OpenProjectFile),
           fileTypeFilters: [new("Quarkプロジェクト") { Patterns = ["*.qprj"] }]
           ).ConfigureAwait(false);

        if (path == null)
            return;

        this.SetRecentDirectory(RecentDirectoryType.OpenProjectFile, Path.GetDirectoryName(path)!);

        var project = await this._projectManager.OpenFromFile(path).ConfigureAwait(false);

        // 新しいプロジェクトが読み込めたら、現在のプロジェクトを閉じる
        await this.CloseProject().ConfigureAwait(false);

        this.ChangeProject(project);
    }

    private async void ChangeProject(Project project)
    {
        this.Title = $"{App.AppName} - {project.Name}";

        var projectViewModel = this._trackViewModelFactory.GetProjectViewModel(project);
        projectViewModel.SelectTrack(project.Tracks.OfType<INeutrinoTrack>().LastOrDefault()!);

        this.HasProject = true;
        this.ProjectViewModel = projectViewModel;
        this.TrackViewModel = projectViewModel.SelectedTrack;
        this.OnPropertyChanged(nameof(this.HasProject));
        this.OnPropertyChanged(nameof(this.TrackViewModel));
        this.OnPropertyChanged(nameof(this.ProjectViewModel));


        //// 編集トラックを設定する
        //// NOTE: 現時点では必ず存在する
        //var track = project.Tracks.OfType<INeutrinoTrack>().LastOrDefault();
        //this.SetTrack(track);

        project.Player.BindDevice(() => new WasapiOut(AudioClientShareMode.Shared, 48 * 2));

        //// オーディオトラックを設定する
        //var audioFileTrack = project.Tracks.OfType<AudioFileTrack>().FirstOrDefault();
        //this.AudioTrackViewModel = audioFileTrack is not null ? new(audioFileTrack) : null;

        //this.RefreshAudio();

        this.UpdateCommands();
    }

    private ICommand? _closingCommand;
    public ICommand ClosingCommand => this._closingCommand ??= new AsyncRelayCommand<WindowClosingEventArgs>(this.OnClosing);

    private async Task OnClosing(WindowClosingEventArgs e)
    {
        if (!await this.ConfirmProjecttClosable().ConfigureAwait(false))
        {
            // 何らかの理由で拒否された場合はウィンドウを閉じないようにする
            e.Cancel = true;
        }
        else
        {
            await this.CloseProject().ConfigureAwait(false);
        }
    }
    }

            await this.CloseProject().ConfigureAwait(false);
        }
    }
}
