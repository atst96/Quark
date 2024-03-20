using System;
using System.Collections.Generic;
using Quark.DependencyInjection;
using Quark.Models.MusicXML;
using Quark.Projects;
using Quark.Projects.Tracks;
using Quark.ViewModels;

namespace Quark.Services;

[Singleton]
internal class ViewModelFactory
{
    private readonly NeutrinoV1Service _v1Service;
    private readonly NeutrinoV2Service _v2Service;

    /// <summary>
    /// </summary>
    /// <param name="v1Service"></param>
    /// <param name="v2Service"></param>
    public ViewModelFactory(NeutrinoV1Service v1Service, NeutrinoV2Service v2Service)
    {
        this._v1Service = v1Service;
        this._v2Service = v2Service;
    }

    /// <summary>
    /// プロジェクトのViewModelを取得する
    /// </summary>
    /// <param name="project">プロジェクト</param>
    public ProjectViewModel GetProjectViewModel(Project project)
        => new(this, project);

    /// <summary>
    /// トラックのViewModelを取得する
    /// </summary>
    /// <param name="track">トラック</param>
    public NeutrinoTrackViewModelBase GetTrackViewModel(ProjectViewModel project, INeutrinoTrack track)
        => track switch
        {
            NeutrinoV1Track v1Track => this.GetTrackViewModel(project, v1Track),
            NeutrinoV2Track v2Track => this.GetTrackViewModel(project, v2Track),
            _ => throw new NotImplementedException()
        };

    /// <summary>
    /// MusicXMLインポート用のViewModelを生成する
    /// </summary>
    /// <param name="path"></param>
    /// <param name="partInfoList"></param>
    /// <returns></returns>
    public MusicXMLImportWindowViewModel GetMusicXmlImportViewModel(string path, IEnumerable<ScorePartElement> partInfoList)
        => new(new DialogService(), this._v1Service, this._v2Service, path, partInfoList);

    /// <summary>
    /// トラックのViewModelを取得する
    /// </summary>
    /// <param name="track">トラック</param>
    public NeutrinoV1TrackViewModel GetTrackViewModel(ProjectViewModel project, NeutrinoV1Track track)
        => new(this._v1Service, project, track);

    /// <summary>
    /// トラックのViewModelを取得する
    /// </summary>
    /// <param name="track">トラック</param>
    public NeutrinoV2TrackViewModel GetTrackViewModel(ProjectViewModel project, NeutrinoV2Track track)
        => new(this._v2Service, project, track);
}
