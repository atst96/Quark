using System;
using Quark.Models.Neutrino;
using Quark.Projects.Tracks;

namespace Quark.ViewModels;

public abstract class NeutrinoTrackViewModelBase : TrackViewModelBase
{
    /// <summary>トラック</summary>
    public INeutrinoTrack Track { get; }

    /// <summary>歌声情報</summary>
    public ModelInfo Singer { get; }

    internal NeutrinoTrackViewModelBase(ProjectViewModel projectViewModel, INeutrinoTrack track) : base(projectViewModel)
    {
        this.Track = track;
        this.Singer = track.Singer ?? throw new ArgumentNullException(nameof(track));
    }
}
