using Quark.Models.MusicXML.DirectionElements;
using System.Xml.Serialization;

namespace Quark.Models.MusicXML;

public class Direction
{
    [XmlElement("direction-type")]
    public DirectionType? DirectionType { get; set; }

    [XmlElement("sound")]
    public Sound? Sound { get; set; }

    public override string ToString()
        => $"DirectionType={this.DirectionType}, Sound={this.Sound}";
}
