using System;
using Quark.Models.Neutrino;
using Quark.Projects.Tracks;

namespace Quark.ViewModels;

public abstract class NeutrinoTrackViewModelBase
{
    /// <summary>トラック</summary>
    public INeutrinoTrack Track { get; }

    /// <summary>歌声情報</summary>
    public ModelInfo Singer { get; }

    protected NeutrinoTrackViewModelBase(INeutrinoTrack track)
    {
        this.Track = track;
        this.Singer = track.Singer ?? throw new ArgumentNullException(nameof(track));
    }
}
