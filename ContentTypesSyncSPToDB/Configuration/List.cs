using System.Collections.Generic;
using System.Xml.Serialization;
using ContentTypesSyncSPToDB.Configuration;

namespace ContentTypesSyncSPToDB.Configuration
{
    public class List
    {
        [XmlAttribute]
        public string SPName { get; set; }
        [XmlAttribute]
        public string DBTableName { get; set; }
        [XmlAttribute]
        public string ContentType { get; set; }
        public List<Column> Columns { get; set; }
    }
}