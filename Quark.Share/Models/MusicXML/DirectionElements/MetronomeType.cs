using System.Xml.Serialization;

namespace Quark.Models.MusicXML.DirectionElements;

public class MetronomeType
{
    [XmlElement("beat-unit")]
    public string? BeatUnit { get; set; }

    [XmlElement("beat-unit-dot")]
    public EmptyObject? BeatUnitDot { get; set; }

    [XmlElement("per-minute")]
    public double PerMinute { get; set; }

    public override string ToString()
        => $"BeatUnit={this.BeatUnit}, BeatUnitDot={this.BeatUnitDot}, PerMinute={this.PerMinute}";
}
