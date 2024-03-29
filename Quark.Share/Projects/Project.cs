﻿using Quark.Audio;
using Quark.Data.Project;
using Quark.Factories;
using Quark.Services;
using Quark.Utils;

namespace Quark.Projects;

internal class Project
{
    public event EventHandler? Estimated;

    public string? ProjectFilePath { get; private set; }

    /// <summary>
    /// プロジェクト名
    /// </summary>
    public string Name { get; set; }

    public ProjectSession Session { get; }

    public TrackCollection Tracks { get; }

    public ProjectPlayer Player { get; }

    public Project(string name, ProjectSessionFactory sessionFactory)
    {
        this.Name = name;
        this.Session = sessionFactory.Create(this);
        this.Tracks = new(this, this.Session);
        this.Player = new(this);

        this.Session.BeginSession();
    }

    public Project(string projDir, ProjectConfig composition, ProjectSessionFactory sessionFactory)
    {
        this.Name = composition.Name;
        this.ProjectFilePath = projDir;
        this.Session = sessionFactory.Create(this);
        this.Tracks = new(this, this.Session);
        this.Player = new(this);
        this.Tracks.Load(composition.Tracks);

        this.Session.BeginSession();
    }

    public bool IsNewFile()
        => this.ProjectFilePath is null;

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

        MemoryPackUtil.WriteFileCompression(projPath, this.GetConfig());
    }

    public static Project Open(string projPath, ProjectSessionFactory sessionFactory)
        => new Project(projPath, ReadProjectFile(projPath)!, sessionFactory);

    private static ProjectConfig ReadProjectFile(string path)
        => MemoryPackUtil.ReadFileCompressed<ProjectConfig>(path);

    public ProjectConfig GetConfig()
        => new(this.Name, this.Tracks.GetConfig());

    internal void RaiseEstimated()
        => this.Estimated?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// プロジェクトを閉じる
    /// </summary>
    public async Task CloseAsync()
    {
        await this.Session.EndSession().ConfigureAwait(false);
    }
}
