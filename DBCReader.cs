using System;
using System.Data;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace DBCycle
{
    public class SingleFileImporter
    {
        public DataTable ImportFile(string filePath, TableDefinition tableDef)
        {
            DataTable table = new DataTable();

            // Define columns based on schema
            foreach (var field in tableDef.Fields)
            {
                int arrayCount = field.ArraySize ?? 1;
                for (int i = 0; i < arrayCount; i++)
                {
                    string colName = arrayCount > 1 ? $"{field.Name}_{i + 1}" : field.Name;
                    table.Columns.Add(colName, typeof(string));  // Use string for easy display
                }
            }

            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (BinaryReader br = new BinaryReader(fs))
            {
                string expectedMagic = tableDef.Extension.ToLower() == "dbc" ? "WDBC" : "WDB2";
                string magic = new string(br.ReadChars(4));
                if (magic != expectedMagic)
                {
                    throw new Exception($"Invalid file format. Expected {expectedMagic}, got {magic}.");
                }

                int recordCount = br.ReadInt32();
                int headerFieldCount = br.ReadInt32();
                int recordSize = br.ReadInt32();
                int stringBlockSize = br.ReadInt32();

                if (tableDef.Extension.ToLower() == "db2")
                {
                    br.BaseStream.Seek(28, SeekOrigin.Current); // Skip DB2 extra header fields
                }

                byte[] recordsData = br.ReadBytes(recordCount * recordSize);
                byte[] stringBlock = br.ReadBytes(stringBlockSize);

                for (int i = 0; i < recordCount; i++)
                {
                    object[] rowValues = new object[table.Columns.Count];
                    int fieldIndex = 0;
                    int recordOffset = i * recordSize;
                    int currentByteOffset = recordOffset;

                    foreach (var field in tableDef.Fields)
                    {
                        int arrayCount = field.ArraySize ?? 1;
                        int fieldSize = DbcFieldHelper.GetFieldSize(field.Type);

                        for (int a = 0; a < arrayCount; a++)
                        {
                            object value = DbcFieldHelper.ReadFieldValue(field, recordsData, currentByteOffset, stringBlock);
                            rowValues[fieldIndex++] = value.ToString();
                            currentByteOffset += fieldSize;
                        }
                    }
                    table.Rows.Add(rowValues);
                }
            }

            return table;
        }
    }
}
