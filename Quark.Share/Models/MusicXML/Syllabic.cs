using System.Xml.Serialization;

namespace Quark.Models.MusicXML;

public enum Syllabic
{
    Unknown = 0,

    [XmlEnum("begin")]
    Begin = 1,

    [XmlEnum("end")]
    End = 2,

    [XmlEnum("middle")]
    Middle = 3,

    [XmlEnum("single")]
    Single = 4,
}
