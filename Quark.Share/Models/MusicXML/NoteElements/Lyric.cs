using System.Xml.Serialization;

namespace Quark.Models.MusicXML.NoteElements;

public class Lyric
{
    [XmlElement("syllabic")]
    public Syllabic? Syllabic { get; set; }

    [XmlIgnore]
    public bool SyllabicSpecified => this.Syllabic.HasValue;

    [XmlElement("text")]
    public string? Text { get; set; }

    public override string ToString()
        => $"Syllabic={this.Syllabic}, Text={this.Text}";
}
