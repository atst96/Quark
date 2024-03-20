using System;
using System.Threading.Tasks;
using Quark.DependencyInjection;
using Quark.Models.MusicXML;
using Quark.Projects;
using Quark.ViewModels;

namespace Quark.Services;

/// <summary>
/// プロジェクト管理クラス
/// </summary>
[Singleton]
internal class ProjectManager(ProjectFactory Factory, AudioSessionManager AudioManager)
{
    private ProjectFactory _factory = Factory;
    private AudioSessionManager _audioManager = AudioManager;

    /// <summary>現在のプロジェクト</summary>
    public Project? Current { get; private set; }

    /// <summary>
    /// プロジェクトを作成する
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public async Task<Project> CreateFromMxl(string name, (ScorePartElement Info, Part Part)[] parts, PartSelectInfo[] selectParts)
    {
        this.AssertOpenFile();

        var newProject = this._factory.CreateFromMxlPart(name, parts, selectParts);
        this.ChangeProject(newProject);

        return newProject;
    }

    /// <summary>
    /// ファイルからプロジェクトを開く
    /// </summary>
    /// <param name="filePath">ファイルパス</param>
    /// <returns></returns>
    public async Task<Project> OpenFromFile(string filePath)
    {
        this.AssertOpenFile();

        var newProject = this._factory.LoadFromFile(filePath);
        this.ChangeProject(newProject);

        return newProject;
    }

    private void AssertOpenFile()
    {
        if (this.Current != null)
            throw new InvalidOperationException();
    }

    private void ChangeProject(Project project)
    {
        this.Current = project;
        project.Player.BindDevice(this._audioManager.GetDevice);
    }

    /// <summary>
    /// 現在のプロジェクトを閉じる
    /// </summary>
    /// <returns></returns>
    public async ValueTask CloseCurrentProject()
    {
        var project = this.Current;
        if (project == null)
            return; // 開いていないか既に閉じている場合は何もしない

        project.Player.UnboundDevice();

        await project.CloseAsync().ConfigureAwait(false);
    }
}
