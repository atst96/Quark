﻿namespace Quark.Models.Scores;

public record TempoInfo(
    bool IsGenerated, decimal Frame, double Tempo, string BeatUnit, bool IsBeatUnitDot, double PerMinute);