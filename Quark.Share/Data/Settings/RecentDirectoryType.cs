namespace Quark.Data.Settings;

/// <summary>ディレクトリの種類</summary>
public enum RecentDirectoryType : int
{
    /// <summary>プロジェクトファイルを開く</summary>
    OpenProjectFile = 0,

    /// <summary>プロジェクトファイルを保存</summary>
    SaveProjectFile = 1,

    /// <summary>MusicXMLインポート</summary>
    ImportMusicXml = 2,

    /// <summary>WAVエクスポート</summary>
    ExportWav = 3,
}
