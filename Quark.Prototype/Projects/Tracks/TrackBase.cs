using System;
using System.Linq;
using Livet;
using Quark.Data.Projects.Tracks;

namespace Quark.Projects.Tracks;

internal abstract class TrackBase : NotificationObject
{
    protected Project Project { get; }

    public string TrackId { get; }

    private string _trackName;

    public string TrackName
    {
        get => this._trackName;
        set => this.RaisePropertyChangedIfSet(ref this._trackName, value);
    }

    private TrackBase(Project project, string trackId, string trackName)
    {
        this.Project = project;
        this.TrackId = trackId;
        this._trackName = trackName;
    }

    protected TrackBase(Project project, string trackName)
        : this(project, GenerateTrackId(project.Tracks), trackName)
    {
    }

    protected TrackBase(Project project, TrackBaseConfig config)
        : this(project, config.TrackId, config.TrackName)
    {
    }

    /// <summary>
    /// 重複しないトラックIDを生成する
    /// </summary>
    /// <param name="tracks">トラックリスト</param>
    /// <returns></returns>
    public static string GenerateTrackId(TrackCollection tracks)
    {
        string trackId;
        do { trackId = Guid.NewGuid().ToString("D"); }
        while (tracks.Any(t => t.TrackId == trackId));

        return trackId;
    }

    public abstract TrackBaseConfig GetConfig();
}
