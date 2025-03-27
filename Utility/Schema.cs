using System.Collections.Generic;

namespace DBCycle
{
    public class FieldDefinition
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public bool IsIndex { get; set; }
        public int? ArraySize { get; set; }
    }

    public class TableDefinition
    {
        public string Name { get; set; }
        public string Extension { get; set; }
        public List<FieldDefinition> Fields { get; set; }
    }

    public class DbDefinition
    {
        public List<TableDefinition> Tables { get; set; }
    }
}
