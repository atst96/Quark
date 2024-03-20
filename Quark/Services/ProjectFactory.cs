using Quark.Data;
using Quark.DependencyInjection;
using Quark.Factories;
using Quark.Models.MusicXML;
using Quark.Projects;
using Quark.ViewModels;

namespace Quark.Services;

[Singleton]
internal class ProjectFactory(ProjectSessionFactory sessionFactory)
{
    private ProjectSessionFactory _sessionFactory = sessionFactory;

    /// <summary>
    /// プロジェクトを作成する
    /// </summary>
    /// <param name="name"></param>
    /// <param name="parts"></param>
    /// <param name="selectParts"></param>
    /// <returns></returns>
    public Project CreateFromMxlPart(string name, (ScorePartElement Info, Part Part)[] parts, PartSelectInfo[] selectParts)
    {
        var project = new Project(name, this._sessionFactory);

        foreach (var selectedPart in selectParts)
        {
            var part = parts[selectedPart.Index];
            var singer = selectedPart.Singer!;
            var modelType = singer.ModelType;

            if (modelType == ModelType.NeutorinoV1)
            {
                project.Tracks.ImportFromMusicXmlV1(part.Part, selectedPart.TrackName, singer);
            }
            else if (modelType == ModelType.NeutorinoV2)
            {
                project.Tracks.ImportFromMusicXmlV2(part.Part, selectedPart.TrackName, singer);
            }
        }

        return project;
    }

    /// <summary>
    /// プロジェクトをファイルから読み込む
    /// </summary>
    /// <param name="filePath">ファイルパス</param>
    public Project LoadFromFile(string filePath)
        => Project.Open(filePath, this._sessionFactory);
}
