using Quark.Models.MusicXML.NoteElements;
using System.Xml.Serialization;

namespace Quark.Models.MusicXML;

public class Note
{
    [XmlElement("rest")]
    public EmptyObject? Rest { get; set; }

    [XmlElement("pitch")]
    public Pitch? Pitch { get; set; }

    [XmlElement("duration")]
    public int Duration { get; set; }

    [XmlElement("tie")]
    public List<Tie>? Tie { get; set; }

    [XmlElement("voice")]
    public int Voice { get; set; }

    [XmlElement("type")]
    public string? Type { get; set; }

    [XmlElement("stem")]
    public string? Stem { get; set; }

    [XmlElement("accidental")]
    public string? Accidental { get; set; }

    [XmlElement("notations")]
    public Notations? Notations { get; set; }

    [XmlElement("lyric")]
    public Lyric? Lyric { get; set; }

    public override string ToString()
        => $"Rest={this.Rest}, Pitch={this.Pitch}, Duration={this.Duration}, Tie={this.Tie}, Voice={this.Voice}, Type={this.Type}, Stem={this.Stem}, Accidental={this.Accidental}, Notations={this.Notations}, Lyric={this.Lyric}";
}
