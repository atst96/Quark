using Quark.Projects.Tracks;
using Quark.Services;

namespace Quark.ViewModels;

internal class NeutrinoV2TrackViewModel : NeutrinoTrackViewModelBase
{
    private readonly NeutrinoV2Service _service;

    public NeutrinoV2TrackViewModel(NeutrinoV2Service service, ProjectViewModel projectViewModel, NeutrinoV2Track track)
        : base(projectViewModel, track)
    {
        this._service = service;
    }
}
