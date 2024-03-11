using Quark.Projects.Tracks;
using Quark.Services;

namespace Quark.ViewModels;

internal class NeutrinoV1TrackViewModel : NeutrinoTrackViewModelBase
{
    private readonly NeutrinoV1Service _service;

    public NeutrinoV1TrackViewModel(NeutrinoV1Service service, NeutrinoV1Track track)
        : base(track)
    {
        this._service = service;
    }
}
