using System.IO;
using System.Linq;
using Quark.Data;
using Quark.DependencyInjection;
using Quark.Factories;
using Quark.Models.MusicXML;
using Quark.Projects;
using Quark.Utils;
using Quark.ViewModels;

namespace Quark.Services;

[Singleton]
internal class ProjectFactory
{
    private ProjectSessionFactory _sessionFactory;

    public ProjectFactory(ProjectSessionFactory sessionFactory)
    {
        this._sessionFactory = sessionFactory;
    }

    public (ScorePartElement Info, Part part)[] ParseParts(string path)
    {
        using var fs = File.OpenRead(path);
        return MusicXmlUtil.EnumerateParts(fs).ToArray();
    }

    public Project CreateFromMusicXml(string name, (ScorePartElement Info, Part Part)[] parts, PartSelectInfo[] selectParts)
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
}
