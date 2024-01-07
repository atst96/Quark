using System.Xml.Serialization;

namespace Quark.Models.MusicXML.NotationElements;

/// <summary>
/// タイ
/// </summary>
/// <param name="Type"></param>
public class Tied : INotation
{
    [XmlAttribute("type")]
    public string? Type { get; set; }

    public override string ToString()
        => $"Type={this.Type}";
}
