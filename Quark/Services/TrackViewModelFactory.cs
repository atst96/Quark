using Quark.DependencyInjection;
using Quark.Projects.Tracks;
using Quark.ViewModels;

namespace Quark.Services;

[Singleton]
internal class TrackViewModelFactory
{
    private readonly NeutrinoV1Service _v1Service;
    private readonly NeutrinoV2Service _v2Service;

    /// <summary>
    /// constructor
    /// </summary>
    /// <param name="v1Service"></param>
    /// <param name="v2Service"></param>
    public TrackViewModelFactory(NeutrinoV1Service v1Service, NeutrinoV2Service v2Service)
    {
        this._v1Service = v1Service;
        this._v2Service = v2Service;
    }

    /// <summary>ViewModelを取得する</summary>
    public NeutrinoV1TrackViewModel GetViewModel(NeutrinoV1Track track)
        => new(this._v1Service, track);

    /// <summary>ViewModelを取得する</summary>
    public NeutrinoV2TrackViewModel GetViewModel(NeutrinoV2Track track)
        => new(this._v2Service, track);
}
