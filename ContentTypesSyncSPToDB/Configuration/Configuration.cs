using System.Collections.Generic;

namespace ContentTypesSyncSPToDB.Configuration
{
    public class Configuration
    {
        public string PWASiteCollectionURL { get; set; }
        public string DBConnectionString { get; set; }
        public List<List> Lists { get; set; }
        public int? MaxDegreeOfParallelism { get; set; }
    }
}
