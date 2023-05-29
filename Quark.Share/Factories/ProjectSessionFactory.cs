using Quark.DependencyInjection;
using Quark.Projects;
using Quark.Services;

namespace Quark.Factories;

[Singleton]
internal class ProjectSessionFactory
{
    private NeutrinoV1Service _v1Service;
    private NeutrinoV2Service _v2Service;

    public ProjectSessionFactory(NeutrinoV1Service v1Service, NeutrinoV2Service v2Service)
    {
        this._v1Service = v1Service;
        this._v2Service = v2Service;
    }

    public ProjectSession Create(Project project)
        => new(project, this._v1Service, this._v2Service);
}
