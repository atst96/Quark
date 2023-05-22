using Quark.Data.Project;
using Quark.Models.Neutrino;
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

    public TrackCollection Tracks { get; }

    public Project(string name)
    {
        this.Tracks = new(this);
        this.Name = name;
    }

    public Project(string projDir, ProjectConfig composition, IEnumerable<ModelInfo> models)
    {
        this.ProjectFilePath = projDir;
        this.Name = composition.Name;
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

        MemoryPackUtil.WriteFileCompression(projPath, this.GetConfig());
    }

    public static Project Open(string projPath, IEnumerable<ModelInfo> models)
        => new Project(projPath, MemoryPackUtil.ReadFileCompressed<ProjectConfig>(projPath)!, models);

    public ProjectConfig GetConfig()
        => new(this.Name, this.Tracks.GetConfig());

    internal void RaiseEstimated()
        => this.Estimated?.Invoke(this, EventArgs.Empty);
}
