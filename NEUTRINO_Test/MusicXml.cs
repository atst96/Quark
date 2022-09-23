#nullable enable
#pragma warning disable CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。Null 許容として宣言することをご検討ください。
using System.Xml.Serialization;

namespace MusicXml;

[XmlRoot("score-partwise")]
public record MusicXmlObject
{
    [XmlElement("identification")]
    public Identification? Identification { get; set; }

    [XmlElement("part-list")]
    public PartList PartList { get; set; }

    [XmlElement("part")]
    public List<Part> Parts { get; set; }
}

public record Identification()
{
    [XmlElement("encoding")]
    public ScoreEncoding Encoding { get; set; }

    public record ScoreEncoding
    {
        [XmlElement("software")]
        public string Software { get; set; }
    }
}

public record PartList
{
    [XmlElement("score-part")]
    public List<ScorePartElement> ScorePart { get; set; }

    public record ScorePartElement
    {
        [XmlAttribute("id")]
        public string Id { get; set; }

        [XmlElement("part-name")]
        public string PartName { get; set; }
    }
}

public record Part
{
    [XmlElement("measure")]
    public List<Measure> Measures { get; set; }
}

public record Measure
{
    [XmlAttribute("number")]
    public int Number { get; set; }

    [XmlElement("attributes")]
    public MeasureAttributes Attributes { get; set; }

    [XmlElement("direction")]
    public Direction Direction { get; set; }

    [XmlElement("note")]
    public List<Note> Notes { get; set; }
}

public record MeasureAttributes
{
    [XmlElement("divisions")]
    public int Divisions { get; set; }

    [XmlElement("key")]
    public AttributeKey Key { get; set; }

    [XmlElement("time")]
    public AttributeTime Time { get; set; }

    [XmlElement("clef")]
    public AttributeClef Clef { get; set; }

    public record AttributeKey
    {
        [XmlElement("fifths")]
        public int Fifths { get; set; }
    }

    public record AttributeTime
    {
        [XmlElement("beats")]
        public int Beats { get; set; }

        [XmlElement("beat-type")]
        public int BeatType { get; set; }
    }

    public record AttributeClef
    {
        [XmlElement("sign")]
        public string Sign { get; set; }

        [XmlElement("line")]
        public int Line { get; set; }
    }
}

public record Direction
{
    [XmlElement("direction-type")]
    public Type DirectionType { get; set; }

    [XmlElement("sound")]
    public SoundInfo Sound { get; set; }

    public record Type
    {
        [XmlElement("metronome")]
        public MetronmeType Metronome { get; set; }

        public record MetronmeType
        {
            [XmlElement("beat-unit")]
            public string Quarter { get; set; }

            [XmlElement("per-minute")]
            public int PerMinute { get; set; }
        }
    }

    public record SoundInfo
    {
        [XmlAttribute("tempo")]
        public int Tempo { get; set; }
    }
}

public record Note
{
    [XmlElement("rest")]
    public Rest Rest { get; set; }

    [XmlElement("pitch")]
    public Pitch Pitch { get; set; }

    [XmlElement("duration")]
    public int Duration { get; set; }

    [XmlElement("tie")]
    public Tie Tie { get; set; }

    [XmlElement("voice")]
    public int Voice { get; set; }

    [XmlElement("type")]
    public string Type { get; set; }

    [XmlElement("stem")]
    public string Stem { get; set; }

    [XmlElement("accidental")]
    public string Accidental { get; set; }

    [XmlElement("notations")]
    public List<Notation> Notations { get; set; }

    [XmlElement("lyric")]
    public Lyric Lyric { get; set; }
}

public record Pitch
{
    [XmlElement("step")]
    public string Step { get; set; }

    [XmlElement("octave")]
    public int Octave { get; set; }
}

public record Tie
{
    [XmlAttribute("type")]
    public string Type { get; set; }
}

public record Notation
{
    [XmlElement("tied")]
    public TiedNotation Tied { get; set; }

    public record TiedNotation
    {
        [XmlAttribute("type")]
        public string Type { get; set; }
    }
}

public record Rest
{
}

public record Lyric
{
    [XmlElement("text")]
    public string Text { get; set; }
}

#pragma warning restore CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。Null 許容として宣言することをご検討ください。