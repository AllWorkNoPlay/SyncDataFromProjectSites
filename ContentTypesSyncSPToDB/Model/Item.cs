using System.Collections.Generic;

namespace ContentTypesSyncSPToDB.Model
{
    public class Item
    {
        public string ContentType { get; set; }
        public string DBTableName { get; set; }
        public List<Property> Properties { get; set; }
    }
}
    