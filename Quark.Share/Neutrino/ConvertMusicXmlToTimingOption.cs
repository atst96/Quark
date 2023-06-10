namespace Quark.Neutrino;

/// <summary>
/// MusicXmlからタイミング情報への変換オプション
/// </summary>
public record ConvertMusicXmlToTimingOption
{
    /// <summary>MusicXml</summary>
    public required string MusicXml { get; init; }

    /// <summary>ディレクトリ</summary>
    public string? Directory { get; init; }
}
