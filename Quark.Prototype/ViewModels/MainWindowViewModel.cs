using System;
using System.Collections.Generic;
using System.Windows.Input;
using Livet.Messaging;
using Livet.Messaging.IO;
using Quark.Models.Neutrino;
using Quark.Mvvm;
using Quark.Projects;
using Quark.Services;

namespace Quark.ViewModels;

internal class MainWindowViewModel : ViewModelBase
{
    private NeutrinoService _neutrino;
    private ProjectService _projects;

    private Project? _currentProject;

    public bool HasProject => this._currentProject is not null;

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
                this._saveCommand?.RaiseCanExecute();
            }
        }
    }

    public IList<ModelInfo> Models { get; }

    private NewProjectWindowViewModel? _newProjectViewModel;
    public NewProjectWindowViewModel NewProjectViewModel
        => this._newProjectViewModel ??= this.AddDisposable(new NewProjectWindowViewModel());

    public MainWindowViewModel(NeutrinoService neutrino, ProjectService projects) : base()
    {
        this._neutrino = neutrino;
        this._projects = projects;
        this.Models = neutrino.GetModels();
    }

    private ICommand? _openSettingWindowCommand;
    public ICommand OpenSettingWindowCommand
    {
        get => this._openSettingWindowCommand ??= new DelegateCommand(() =>
        {
            this.Messenger.Raise(new TransitionMessage("OpenSettingWindow"));
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
            this.CurrentProject = this._projects.Create(viewModel.ProjectName, viewModel.WorkingDirectory);
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
        this.CurrentProject!.Tracks.ImportFromMusicXml(path, System.IO.Path.GetFileNameWithoutExtension(path));
    }
}
