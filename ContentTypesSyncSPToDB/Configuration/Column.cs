using System.Xml.Serialization;

namespace ContentTypesSyncSPToDB.Configuration
{
    public class Column
    {
        [XmlAttribute]
        public string SPName { get; set; }
        [XmlAttribute]
        public string DBTableName { get; set; }
    }
}