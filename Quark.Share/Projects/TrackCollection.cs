using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using Quark.Data.Projects.Tracks;
using Quark.Models.Neutrino;
using Quark.Projects.Tracks;
using Quark.Services;

namespace Quark.Projects;

internal class TrackCollection : ObservableCollection<TrackBase>
{
    private Project _project;
    private ProjectSession _session;

    public TrackCollection(Project project, ProjectSession session)
    {
        this._project = project;
        this._session = session;
    }

    /// <summary>
    /// インポートするファイルのパス
    /// </summary>
    /// <param name="path">パス</param>
    /// <param name="trackName">トラック名</param>
    public NeutrinoV1Track ImportFromMusicXml(string path, string trackName, ModelInfo model)
    {
        var newTrack = new NeutrinoV1Track(this._project, trackName, File.ReadAllText(path, Encoding.UTF8), model);

        this.Add(newTrack);

        return newTrack;
    }

    public IEnumerable<TrackBaseConfig> GetConfig()
        => this.Select(t => t.GetConfig());

    public void Load(IEnumerable<TrackBaseConfig> tracks)
    {
        var session = this._session;

        foreach (var track in tracks)
        {
            switch (track)
            {
                case NeutrinoV1TrackConfig t:
                    this.Add(new NeutrinoV1Track(this._project, t, session.NeutrinoV1.GetModels()));
                    break;


                case NeutrinoV2TrackConfig t:
                    this.Add(new NeutrinoV2Track(this._project, t, session.NeutrinoV2.GetModels()));
                    break;
            }
        }
    }
}
