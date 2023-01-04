using System.IO;

namespace Quark.Services;

internal class ProjectService
{
    public ProjectService()
    {
    }

    public Project.Project Create(string name, string directory)
    {
        var dirInfo = new DirectoryInfo(directory);
        if (!dirInfo.Exists)
        {
            dirInfo.Create();
        }

        return new Project.Project(name, directory);
    }

    public Project.Project Open(string projPath)
        => Project.Project.Open(projPath);
}
