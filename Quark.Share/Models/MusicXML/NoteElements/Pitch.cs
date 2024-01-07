using System.Xml.Serialization;

namespace Quark.Models.MusicXML.NoteElements;

public class Pitch
{
    [XmlElement("step")]
    public string? Step { get; set; }

    [XmlElement("alter")]
    public decimal? Alter { get; set; }

    [XmlIgnore]
    public bool AlterSpecified => this.Alter.HasValue;

    [XmlElement("octave")]
    public int Octave { get; set; }

    public override string ToString()
        => $"Step={this.Step}, Alter={this.Alter}, Octave={this.Octave}";
}
