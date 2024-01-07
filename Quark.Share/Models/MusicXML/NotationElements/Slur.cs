using System.Xml.Serialization;

namespace Quark.Models.MusicXML.NotationElements;

/// <summary>
/// スラー
/// </summary>
/// <param name="Type"></param>
/// <param name="Placement"></param>
/// <param name="Number"></param>
public class Slur : INotation
{
    [XmlAttribute("type")]
    public StartStop Type { get; set; }

    [XmlAttribute("placement")]
    public string? Placement { get; set; }

    [XmlAttribute("number")]
    public int Number { get; set; }

    public override string ToString()
        => $"Type={this.Type}, Placement={this.Placement}, Number={this.Number}";
}
