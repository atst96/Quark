namespace Quark.Data.Projects;

internal record ProgressReport(
    ProgressReportType ReprotType,
    string? Line, double? Progress);
