using System.Xml.Serialization;

namespace Quark.Models.MusicXML.DirectionElements;

public class DirectionType
{
    [XmlElement("metronome")]
    public MetronomeType? Metronome { get; set; }
}
