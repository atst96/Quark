using System.Collections.Generic;
using System.IO;
using Quark.Models.Neutrino;
using Quark.Projects;

namespace Quark.Services;

internal class ProjectService
{
    public ProjectService()
    {
    }

    public Project Create(string name, string directory)
    {
        var dirInfo = new DirectoryInfo(directory);
        if (!dirInfo.Exists)
        {
            dirInfo.Create();
        }

        return new Project(name, directory);
    }

    public Project Open(string projPath, IEnumerable<ModelInfo> models)
        => Project.Open(projPath, models);
}
