using System.Xml.Serialization;

namespace Quark.Models.MusicXML;

public class PartList
{
    [XmlElement("score-part")]
    public List<ScorePartElement>? ScorePart { get; set; }

    public override string ToString()
        => $"ScorePart={this.ScorePart}";
}
