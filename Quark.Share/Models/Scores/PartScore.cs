using Quark.Models.MusicXML;

namespace Quark.Models.Scores;

public record PartScore(
    int BeginMeasureTime,
    LinkedList<TempoInfo> Tempos,
    LinkedList<TimeSignature> TimeSignatures,
    LinkedList<MusicXmlPhrase.Frame> Phrases);
