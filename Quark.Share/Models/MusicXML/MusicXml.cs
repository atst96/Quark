#pragma warning disable CS8625 // null リテラルを null 非許容参照型に変換できません。

using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;
using static Quark.Models.MusicXML.Direction.ArticulationTypes;

namespace Quark.Models.MusicXML;

[XmlRoot("score-partwise")]
public record MusicXmlObject(
    [property: XmlElement("identification")] Identification? Identification,
    [property: XmlElement("part-list")] PartList PartList,
    [property: XmlElement("part")] List<Part> Parts)
{
    public MusicXmlObject() : this(default, default, default) { }
}

public record Identification(
    [property: XmlElement("encoding")] Identification.ScoreEncoding Encoding)
{
    public Identification() : this(default(ScoreEncoding)) { }

    public record ScoreEncoding(
        [property: XmlElement("software")] string Software)
    {
        public ScoreEncoding() : this(default(string)) { }
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
    [property: XmlElement("measure")] List<Measure> Measures)
{
    public Part() : this(default(List<Measure>)) { }
}

public record Measure(
    [property: XmlAttribute("number")] int Number,
    [property: XmlElement("attributes")] MeasureAttributes Attributes,
    [property: XmlElement("direction")] Direction Direction,
    [property: XmlElement("note")] List<Direction.Note> Notes)
{
    public Measure() : this(default, default, default, default) { }
}

public record MeasureAttributes(
    [property: XmlElement("divisions")] int? Divisions,
    [property: XmlElement("key")] MeasureAttributes.AttributeKey Key,
    [property: XmlElement("time")] MeasureAttributes.AttributeTime Time,
    [property: XmlElement("clef")] MeasureAttributes.AttributeClef Clef)
{
    public MeasureAttributes() : this(default, default, default, default) { }

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

public record Direction(
    [property: XmlElement("direction-type")] Direction.Type DirectionType,
    [property: XmlElement("sound")] Direction.SoundInfo Sound)
{
    public Direction() : this(default, default) { }

    public record Type(
        [property: XmlElement("metronome")] Type.MetronmeType Metronome)
    {
        public Type() : this(default(MetronmeType)) { }

        public record MetronmeType(
            [property: XmlElement("beat-unit")] string Quarter,
            [property: XmlElement("per-minute")] double PerMinute)
        {
            public MetronmeType() : this(default, default) { }
        }
    }

    public record SoundInfo(
        [property: XmlAttribute("tempo")] double Tempo)
    {
        public SoundInfo() : this(default(double)) { }
    }

    public record Note(
        [property: XmlElement("rest")] Rest Rest,
        [property: XmlElement("pitch")] Pitch Pitch,
        [property: XmlElement("duration")] int Duration,
        [property: XmlElement("tie")] Tie Tie,
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
        [property: XmlElement("octave")] int Octave)
    {
        public Pitch() : this(default, default) { }
    }

    public record Tie(
        [property: XmlAttribute("type")] StartStop Type)
    {
        public Tie() : this(default(StartStop)) { }
    }

    public record Notations(
        [property: XmlElement("tied")] NotationTypes.Tied Tied,
        [property: XmlElement("articulations")] NotationTypes.Articulations Articulations)
    {
        public Notations() : this(default, default) { }
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
            [property: XmlElement("breath-mark")] BreathMark BreathMark)
            : INotation
        {
            public Articulations() : this(default(BreathMark)) { }
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
}

#pragma warning restore CS8625 // null リテラルを null 非許容参照型に変換できません。
