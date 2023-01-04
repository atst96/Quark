using System;
using System.Runtime.CompilerServices;
using Livet;
using Quark.Data.Project;
using Quark.Utils;

namespace Quark.Project;

internal class Project : NotificationObject
{
    private readonly ProjectComposition _composition;

    public ProjectComposition GetComposition() => this._composition;

    private string _name;

    public string ProjectFilePath { get; private set; }

    /// <summary>
    /// プロジェクト名
    /// </summary>
    public string Name
    {
        get => this._name ??= string.Empty;
        set => this.RaisePropertyChangedIfSet(ref this._name, value);
    }

    /// <summary>
    /// プロジェクトのディレクトリ
    /// </summary>
    public string Directory { get; }

    public Project(string projDir, ProjectComposition composition)
    {
        this.ProjectFilePath = projDir;
        this._composition = composition;
        this._name = composition.Name;
        this.Directory = composition.Directory;
    }

    public Project(string name, string directory)
    {
        this._name = name;
        this.Directory = directory;

        this._composition = new ProjectComposition(name, directory);
    }

    public void SaveToFile(string? filePath = null, bool overrideFielPath = true)
    {
        var projPath = filePath ?? this.ProjectFilePath;
        if (projPath is null)
        {
            throw new Exception("プロジェクトファイルの保存先が指定されていません。");
        }

        if (filePath is not null && overrideFielPath)
        {
            this.ProjectFilePath = projPath;
        }

        MemoryPackUtil.WriteFile(projPath, this.GetComposition());
    }

    public static Project Open(string projPath)
        => new Project(projPath, MemoryPackUtil.ReadFile<ProjectComposition>(projPath)!);
}
