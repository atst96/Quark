using Quark.Data.Project;
using Quark.Factories;
using Quark.Services;
using Quark.Utils;

namespace Quark.Projects;

internal class Project
{
    public event EventHandler Estimated;

    public string? ProjectFilePath { get; private set; }

    /// <summary>
    /// プロジェクト名
    /// </summary>
    public string Name { get; set; }

    public ProjectSession Session { get; }

    public TrackCollection Tracks { get; }

    public Project(string name, ProjectSessionFactory sessionFactory)
    {
        this.Session = sessionFactory.Create(this);
        this.Tracks = new(this, this.Session);
        this.Name = name;

        this.Session.BeginSession();
    }

    public Project(string projDir, ProjectConfig composition, ProjectSessionFactory sessionFactory)
    {
        this.ProjectFilePath = projDir;
        this.Session = sessionFactory.Create(this);
        this.Name = composition.Name;
        this.Tracks = new(this, this.Session);
        this.Tracks.Load(composition.Tracks);

        this.Session.BeginSession();
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
}
