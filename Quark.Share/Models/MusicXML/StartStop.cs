using System.Xml.Serialization;

namespace Quark.Models.MusicXML;

/// <summary>
/// 開始／終了
/// </summary>
public enum StartStop
{
    Unknown = 0,

    /// <summary>開始</summary>
    [XmlEnum("start")]
    Start = 1,

    /// <summary>終了</summary>
    [XmlEnum("stop")]
    Stop = 2,
}
