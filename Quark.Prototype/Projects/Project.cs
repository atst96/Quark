using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Livet;
using Quark.Data.Project;
using Quark.Models.Neutrino;
using Quark.Projects.Tracks;
using Quark.Utils;

namespace Quark.Projects;

internal class Project : NotificationObject
{
    private string _name;

    public string? ProjectFilePath { get; private set; }

    /// <summary>
    /// プロジェクト名
    /// </summary>
    public string Name
    {
        get => this._name ??= string.Empty;
        set => this.RaisePropertyChangedIfSet(ref this._name, value);
    }

    public TrackCollection Tracks { get; }

    public Project(string name)
    {
        this.Tracks = new(this);
        this._name = name;
    }

    public Project(string projDir, ProjectConfig composition, IEnumerable<ModelInfo> models)
    {
        this.ProjectFilePath = projDir;
        this._name = composition.Name;
        this.Tracks = new(this);
        this.Tracks.Load(composition.Tracks, models);
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

        MemoryPackUtil.WriteFile(projPath, this.GetConfig());
    }

    public static Project Open(string projPath, IEnumerable<ModelInfo> models)
        => new Project(projPath, MemoryPackUtil.ReadFile<ProjectConfig>(projPath)!, models);



    public ProjectConfig GetConfig()
        => new(this.Name, this.Tracks.GetConfig());
}
