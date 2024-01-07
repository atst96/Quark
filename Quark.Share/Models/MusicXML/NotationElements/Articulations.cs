using System.Xml.Serialization;

namespace Quark.Models.MusicXML.NotationElements;

/// <summary>
/// 分節
/// </summary>
/// <param name="BreathMark"></param>
public class Articulations : INotation
{
    [XmlElement("breath-mark")]
    public EmptyObject? BreathMark { get; set; }

    [XmlElement("staccato")]
    public EmptyObject? Staccato { get; set; }

    [XmlElement("accent")]
    public EmptyObject? Accent { get; set; }

    public override string ToString()
        => $"BreathMark={this.BreathMark}, Staccato={this.Staccato}, Accent={this.Accent}";
}
