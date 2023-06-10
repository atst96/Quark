namespace Quark.Data.Projects;

public record ProgressReport(
    ProgressReportType ReprotType,
    string? Line, double? Progress);
