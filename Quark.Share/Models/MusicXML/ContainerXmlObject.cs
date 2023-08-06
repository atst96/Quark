using System.Xml.Serialization;

namespace Quark.Models.MusicXML;

/// <summary>
/// container.xmlのルート要素
/// </summary>
[XmlRoot("container")]
public class ContainerXmlObject
{
    [XmlElement("rootfiles")]
    public RootFilesContainer? RootFiles { get; set; }

    public class RootFilesContainer
    {
        [XmlElement("rootfile")]
        public List<RootFileElement>? RootFile { get; set; }

        public class RootFileElement
        {
            [XmlAttribute("full-path")]
            public string? FullPath { get; set; }

            [XmlAttribute("media-type")]
            public string? MediaType { get; set; }
        }
    }
}
