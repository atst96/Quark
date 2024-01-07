using System.Xml.Serialization;

namespace Quark.Models.MusicXML.NotationElements;
/// <summary>
/// 連符
/// </summary>
public class Tuplet : INotation
{
    [XmlAttribute("type")]
    public StartStop Type { get; set; }

    [XmlAttribute("bracket")]
    public string? Bracket { get; set; }

    public override string ToString()
        => $"Type={this.Type}, Bracket={this.Bracket}";
}
