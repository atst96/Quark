namespace Quark.Models.Scores;

public record TempoInfo(
    bool IsGenerated, decimal Time, double Tempo, string BeatUnit, bool IsBeatUnitDot, double PerMinute);
