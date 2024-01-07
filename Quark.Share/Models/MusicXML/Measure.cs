using System.Xml.Serialization;

namespace Quark.Models.MusicXML;

public class Measure
{
    [XmlAttribute("number")]
    public int Number { get; set; }

    [XmlElement("attributes")]
    public MeasureAttributes? Attributes { get; set; }

    [XmlElement("direction", typeof(Direction)),
     XmlElement("note", typeof(Note))]
    public List<object>? Items { get; set; }

    public override string ToString()
        => $"Number={this.Number}, Attributes={this.Attributes}, Items={this.Items}";
}
