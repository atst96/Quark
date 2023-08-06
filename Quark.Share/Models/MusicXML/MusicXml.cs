#pragma warning disable CS8625 // null リテラルを null 非許容参照型に変換できません。

using System.Xml.Serialization;
using static Quark.Models.MusicXML.MeasureItemTypes.ArticulationTypes;

namespace Quark.Models.MusicXML;

[XmlRoot("score-partwise")]
public record MusicXmlObject(
    [property: XmlAttribute("version")] string Version,
    [property: XmlElement("identification")] Identification? Identification,
    [property: XmlElement("part-list")] PartList PartList,
    [property: XmlElement("part")] List<Part> Parts)
{
    public MusicXmlObject() : this(default, default, default, default) { }
}

public record Identification(
    [property: XmlElement("encoding")] Identification.ScoreEncoding Encoding)
{
    public Identification() : this(default(ScoreEncoding)) { }

    public record ScoreEncoding(
        [property: XmlElement("software")] string Software,
        [property: XmlElement("encoding-date", DataType = "date")] DateTime? EncodingDate)
    {
        public ScoreEncoding() : this(default, default) { }

        [XmlIgnore]
        public bool EncodingDateSpecified => this.EncodingDate.HasValue;
    }
}

public record PartList(
    [property: XmlElement("score-part")] List<PartList.ScorePartElement> ScorePart)
{
    public PartList() : this(default(List<ScorePartElement>)) { }

    public record ScorePartElement(
        [property: XmlAttribute("id")] string Id,
        [property: XmlElement("part-name")] string PartName)
    {
        public ScorePartElement() : this(default, default) { }
    }
}

public record Part(
    [property: XmlAttribute("id")] string Id,
    [property: XmlElement("measure")] List<Measure> Measures)
{
    public Part() : this(default, default) { }
}

public record Measure(
    [property: XmlAttribute("number")] int Number,
    [property: XmlElement("attributes")] MeasureAttributes Attributes,
    [property:
            XmlElement("direction", typeof(MeasureItemTypes.Direction)),
            XmlElement("note", typeof(MeasureItemTypes.Note))]
        List<object> Items)
{
    public Measure() : this(default, default, default) { }
}

public record MeasureAttributes(
    [property: XmlElement("divisions")] int? Divisions,
    [property: XmlElement("key")] MeasureAttributes.AttributeKey Key,
    [property: XmlElement("time")] MeasureAttributes.AttributeTime Time,
    [property: XmlElement("clef")] MeasureAttributes.AttributeClef Clef)
{
    public MeasureAttributes() : this(default, default, default, default) { }

    [XmlIgnore]
    public bool DivisionsSpecified => this.Divisions.HasValue;

    public record AttributeKey(
        [property: XmlElement("fifths")] int Fifths)
    {
        public AttributeKey() : this(default(int)) { }
    }

    public record AttributeTime(
        [property: XmlElement("beats")] int Beats,
        [property: XmlElement("beat-type")] int BeatType)
    {
        public AttributeTime() : this(default, default) { }
    }

    public record AttributeClef(
        [property: XmlElement("sign")] string Sign,
        [property: XmlElement("line")] int Line)
    {
        public AttributeClef() : this(default, default) { }
    }
}

public static class MeasureItemTypes
{
    public record Direction(
        [property: XmlElement("direction-type")] Direction.Type DirectionType,
        [property: XmlElement("sound")] Direction.SoundInfo Sound)
    {
        public Direction() : this(default, default) { }

        public record Type(
            [property: XmlElement("metronome")] Type.MetronomeType Metronome)
        {
            public Type() : this(default(MetronomeType)) { }

            public record MetronomeType(
                [property: XmlElement("beat-unit")] string BeatUnit,
                [property: XmlElement("beat-unit-dot")] object? BeatUnitDot,
                [property: XmlElement("per-minute")] double PerMinute)
            {
                public MetronomeType() : this(default, default, default) { }
            }
        }

        public record SoundInfo(
            [property: XmlAttribute("tempo")] double Tempo)
        {
            public SoundInfo() : this(default(double)) { }
        }
    }

    public record Note(
        [property: XmlElement("rest")] Rest Rest,
        [property: XmlElement("pitch")] Pitch Pitch,
        [property: XmlElement("duration")] int Duration,
        [property: XmlElement("tie")] List<Tie>? Tie,
        [property: XmlElement("voice")] int Voice,
        [property: XmlElement("type")] string Type,
        [property: XmlElement("stem")] string Stem,
        [property: XmlElement("accidental")] string Accidental,
        [property: XmlElement("notations")] Notations Notations,
        [property: XmlElement("lyric")] Lyric Lyric)
    {
        public Note() : this(
            default, default, default, default, default,
            default, default, default, default, default)
        { }
    }

    public record Pitch(
        [property: XmlElement("step")] string Step,
        [property: XmlElement("alter")] decimal? Alter,
        [property: XmlElement("octave")] int Octave)
    {
        public Pitch() : this(default, null, default) { }

        [XmlIgnore]
        public bool AlterSpecified => this.Alter.HasValue;
    }

    public record Tie(
        [property: XmlAttribute("type")] StartStop Type)
    {
        public Tie() : this(default(StartStop)) { }
    }

    public record Notations(
        [property: XmlElement("tied")] NotationTypes.Tied Tied,
        [property: XmlElement("articulations")] NotationTypes.Articulations Articulations,
        [property: XmlElement("slur")] NotationTypes.Slur Slur,
        [property: XmlElement("tuplet")] NotationTypes.Tuplet Tuplet)
    {
        public Notations() : this(default, default, default, default) { }
    }

    /// <summary>
    /// 表記
    /// </summary>
    public static class NotationTypes
    {
        public interface INotation { }

        /// <summary>
        /// 分節
        /// </summary>
        /// <param name="BreathMark"></param>
        public record Articulations(
            [property: XmlElement("breath-mark")] BreathMark BreathMark,
            [property: XmlElement("staccato")] object? Staccato,
            [property: XmlElement("accent")] object? Accent)
            : INotation
        {
            public Articulations() : this(default, default, default) { }
        }

        /// <summary>
        /// タイ
        /// </summary>
        /// <param name="Type"></param>
        public record Tied(
            [property: XmlAttribute("type")] string Type) : INotation
        {
            public Tied() : this(default(string)) { }
        }

        public record Tuplet(
            [property: XmlAttribute("type")] StartStop Type,
            [property: XmlAttribute("bracket")] string? Bracket)
        {
            public Tuplet() : this(default, default) { }
        }

        /// <summary>
        /// スラー
        /// </summary>
        /// <param name="Type"></param>
        /// <param name="Placement"></param>
        /// <param name="Number"></param>
        public record Slur(
            [property: XmlAttribute("type")] StartStop Type,
            [property: XmlAttribute("placement")] string? Placement,
            [property: XmlAttribute("number")] int Number)
        {
            public Slur() : this(default, default, default) { }
        }
    }

    public static class ArticulationTypes
    {
        public interface IArticulation { }

        /// <summary>
        /// ブレスマーク
        /// </summary>
        public record BreathMark() : IArticulation;
    }

    public record Rest();

    public record Lyric(
        [property: XmlElement("syllabic")] Syllabic Syllabic,
        [property: XmlElement("text")] string Text)
    {
        public Lyric() : this(default, default) { }
    }

    /// <summary>
    /// 開始／終了
    /// </summary>
    public enum StartStop
    {
        /// <summary>
        /// 開始
        /// </summary>
        [XmlEnum("start")]
        Start,

        /// <summary>
        /// 終了
        /// </summary>
        [XmlEnum("stop")]
        Stop,

        Unknown,
    }

    public enum Syllabic
    {
        [XmlEnum("begin")]
        Begin,

        [XmlEnum("end")]
        End,

        [XmlEnum("middle")]
        Middle,

        [XmlEnum("single")]
        Single,

        Unknown,
    }

    public static class NoteType
    {
        /// <summary>1024分音符</summary>
        public const string Note1024th = "1024th";
        /// <summary>512分音符</summary>
        public const string Note512th = "512th";
        /// <summary>256分音符</summary>
        public const string Note256th = "256th";
        /// <summary>128分音符</summary>
        public const string Note128th = "128th";
        /// <summary>64分音符</summary>
        public const string Note64th = "64th";
        /// <summary>32分音符</summary>
        public const string Note32nd = "32nd";
        /// <summary>16分音符</summary>
        public const string Note16th = "16th";
        /// <summary>8分音符</summary>
        public const string Eighth = "eighth";
        /// <summary>4分音符</summary>
        public const string Quarter = "quarter";
        /// <summary>2分音符</summary>
        public const string Half = "half";
        /// <summary>全部音符</summary>
        public const string Whole = "whole";
        /// <summary>倍全音符</summary>
        public const string Breve = "breve";
        /// <summary>4倍全音符</summary>
        public const string Long = "long";
        /// <summary>8倍全音符</summary>
        public const string Maxima = "maxima";
    }
}

#pragma warning restore CS8625 // null リテラルを null 非許容参照型に変換できません。
