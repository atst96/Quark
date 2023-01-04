using System;

namespace Quark.Project;

internal abstract class TrackBase
{
    public string TrackId { get; init; }

    public TrackBase()
    {
        this.TrackId = Guid.NewGuid().ToString();
    }

    public TrackBase(string trackId)
    {
        this.TrackId = trackId;
    }
}
