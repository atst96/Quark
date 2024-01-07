using System.Xml.Serialization;

namespace Quark.Models.MusicXML.Metadata;

public class ScoreEncoding
{
    [XmlElement("software")]
    public string? Software { get; set; }

    [XmlElement("encoding-date", DataType = "date")]
    public DateTime? EncodingDate { get; set; }

    public override string ToString()
        => $"Software={this.Software}, EncodingDate={this.EncodingDate}";
}
