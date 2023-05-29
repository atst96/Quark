using Quark.DependencyInjection;
using Quark.Factories;
using Quark.Projects;

namespace Quark.Services;

[Singleton]
internal class ProjectService
{
    private ProjectSessionFactory _sessionFactory;

    public ProjectService(ProjectSessionFactory sessionFactory)
    {
        this._sessionFactory = sessionFactory;
    }

    public Project Create(string name) => new(name, this._sessionFactory);

    public Project Open(string projPath)
        => Project.Open(projPath, this._sessionFactory);
}
