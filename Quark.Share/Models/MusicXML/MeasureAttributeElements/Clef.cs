using System.Xml.Serialization;

namespace Quark.Models.MusicXML.MeasureAttributeElements;

public class Clef
{
    [XmlElement("sign")]
    public string Sign { get; set; }

    [XmlElement("line")]
    public int Line { get; set; }

    public override string ToString()
        => $"Sign={this.Sign}, Line={this.Line}";
}
