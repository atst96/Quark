using System.Collections.Generic;
using System.IO;
using Quark.DependencyInjection;
using Quark.Models.Neutrino;
using Quark.Projects;

namespace Quark.Services;

[Singleton]
internal class ProjectService
{
    public ProjectService()
    {
    }

    public Project Create(string name) => new Project(name);

    public Project Open(string projPath, IEnumerable<ModelInfo> models)
        => Project.Open(projPath, models);
}
