using System.Xml.Serialization;

namespace Quark.Models.MusicXML.MeasureAttributeElements;

public class Time
{
    [XmlElement("beats")]
    public int Beats { get; set; }

    [XmlElement("beat-type")]
    public int BeatType { get; set; }

    public override string ToString()
        => $"Beats={this.Beats}, BeatType={this.BeatType}";
}
