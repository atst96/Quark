using Quark.Models.MusicXML;

namespace Quark.Models.Scores;

public record PartScore(
    LinkedList<TempoInfo> Tempos,
    LinkedList<TimeSignature> TimeSignatures,
    LinkedList<MusicXmlPhrase.Frame> Phrases);
