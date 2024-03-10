using Quark.Models.MusicXML;
using Quark.Projects.Tracks;
using Quark.Utils;

namespace Quark.Extensions;

public static class TrackExtensions
{
    public static string CreateMusicXml<TTrack>(this TTrack track)
        where TTrack : INeutrinoTrack
    {
        var part = new Part()
        {
            Id = "1",
            Measures = track.Score.Measures.ToList()
        };

        return MusicXmlUtil.ToXmlString(part, track.TrackName);
    }
}
