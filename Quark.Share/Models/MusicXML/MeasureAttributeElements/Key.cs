using System.Xml.Serialization;

namespace Quark.Models.MusicXML.MeasureAttributeElements;

public class Key
{
    [XmlElement("fifths")]
    public int Fifths { get; set; }

    public override string ToString()
        => $"Fifths={this.Fifths}";
}
