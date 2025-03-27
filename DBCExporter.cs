using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;

#region JSON Schema Classes
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
#endregion

public class DBCExporter
{
    private readonly string _jsonDefinitionPath;
    private readonly string _exportFolderPath;
    private readonly string _dbcConnectionString;
    private readonly string _db2ConnectionString;

    private readonly Action<string> _logger;
    private readonly string _db2ImportFolderPath; // Optional folder to import DB2 header values

    public DBCExporter(string jsonDefinitionPath, string exportFolderPath, string dbcConnectionString, string db2ConnectionString, Action<string> logger, string db2ImportFolderPath = null)
    {
        _jsonDefinitionPath = jsonDefinitionPath;
        _exportFolderPath = exportFolderPath;
        _dbcConnectionString = dbcConnectionString;
        _db2ConnectionString = db2ConnectionString;
        _logger = logger;
        _db2ImportFolderPath = db2ImportFolderPath;
    }

    public void ExportAll()
    {
        string json = File.ReadAllText(_jsonDefinitionPath);
        DbDefinition dbDefinition = JsonConvert.DeserializeObject<DbDefinition>(json);

        foreach (var tableDef in dbDefinition.Tables)
        {
            // Choose the connection string based on the extension.
            string connectionString = tableDef.Extension.ToLower() == "dbc"
                ? _dbcConnectionString
                : _db2ConnectionString;

            // Use the table name without the extension.
            string dbTableName = tableDef.Name;
            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                ExportTable(conn, tableDef, dbTableName);
            }
        }
    }

    private void ExportTable(MySqlConnection conn, TableDefinition tableDef, string dbTableName)
    {
        string selectSql = $"SELECT * FROM {dbTableName}";
        List<Dictionary<string, object>> records = new List<Dictionary<string, object>>();

        // Read data from MySQL
        using (MySqlCommand cmd = new MySqlCommand(selectSql, conn))
        using (MySqlDataReader reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                Dictionary<string, object> record = new Dictionary<string, object>();
                foreach (var field in tableDef.Fields)
                {
                    int arrayCount = field.ArraySize ?? 1;
                    // If the field is an array, we expect the columns to be suffixed with _1, _2, etc.
                    if (arrayCount > 1)
                    {
                        string[] values = new string[arrayCount];
                        for (int i = 1; i <= arrayCount; i++)
                        {
                            string colName = $"{field.Name}_{i}";
                            // Use empty string if the column is null
                            values[i - 1] = reader[colName]?.ToString() ?? "";
                        }
                        record[field.Name] = values;
                    }
                    else
                    {
                        record[field.Name] = reader[field.Name] ?? "";
                    }
                }
                records.Add(record);
            }
        }

        if (records.Count == 0)
        {
            _logger($"Skipping export for table {dbTableName} as it contains 0 rows.");
            return;
        }

        // Calculate header values
        int totalElements = tableDef.Fields.Sum(f => f.ArraySize ?? 1);
        int recordSize = totalElements * 4;
        int recordCount = records.Count;

        // Prepare streams for record data and string block.
        MemoryStream recordStream = new MemoryStream();
        MemoryStream stringBlockStream = new MemoryStream();
        // Start the string block with a null byte.
        stringBlockStream.WriteByte(0);
        // Cache for duplicate strings (string value -> offset in block)
        Dictionary<string, int> stringOffsets = new Dictionary<string, int>();

        // Write out each record field-by-field.
        using (BinaryWriter recordWriter = new BinaryWriter(recordStream))
        {
            foreach (var record in records)
            {
                foreach (var field in tableDef.Fields)
                {
                    int count = field.ArraySize ?? 1;
                    // Handle array fields and single value fields uniformly.
                    if (count == 1)
                    {
                        WriteFieldValue(recordWriter, field, record[field.Name], stringBlockStream, stringOffsets);
                    }
                    else
                    {
                        // Expect a string array from the record; if not, use empty strings.
                        if (record[field.Name] is string[] arr)
                        {
                            for (int i = 0; i < count; i++)
                            {
                                string value = i < arr.Length ? arr[i].Trim() : "";
                                WriteFieldValue(recordWriter, field, value, stringBlockStream, stringOffsets);
                            }
                        }
                        else
                        {
                            for (int i = 0; i < count; i++)
                            {
                                WriteFieldValue(recordWriter, field, "", stringBlockStream, stringOffsets);
                            }
                        }
                    }
                }
            }
        }

        byte[] recordsData = recordStream.ToArray();
        byte[] stringBlockData = stringBlockStream.ToArray();

        string exportFileName = $"{tableDef.Name}.{tableDef.Extension}";
        string exportPath = Path.Combine(_exportFolderPath, exportFileName);

        using (FileStream fs = new FileStream(exportPath, FileMode.Create, FileAccess.Write))
        using (BinaryWriter bw = new BinaryWriter(fs))
        {
            if (tableDef.Extension.Equals("dbc", StringComparison.OrdinalIgnoreCase))
            {
                // DBC header: Magic, record count, field count, record size, string block size
                bw.Write(Encoding.ASCII.GetBytes("WDBC"));
                bw.Write(recordCount);
                bw.Write(totalElements);
                bw.Write(recordSize);
                bw.Write(stringBlockData.Length);
            }
            else
            {
                // DB2 header: additional header values are written after string block size.
                uint tableHash = 0;
                uint build = 18414;
                uint timestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                int minId = 0;
                int maxId = 0;
                uint locale = 0;
                uint copyTableSize = 0;

                // Attempt to import header values from an existing DB2 file if available.
                if (!string.IsNullOrEmpty(_db2ImportFolderPath))
                {
                    string importFilePath = Path.Combine(_db2ImportFolderPath, exportFileName);
                    if (File.Exists(importFilePath))
                    {
                        try
                        {
                            using (FileStream importFs = new FileStream(importFilePath, FileMode.Open, FileAccess.Read))
                            using (BinaryReader importBr = new BinaryReader(importFs))
                            {
                                string importMagic = new string(importBr.ReadChars(4));
                                if (importMagic == "WDB2")
                                {
                                    // Read header values (skip initial header values already written)
                                    importBr.ReadInt32(); // recordCount
                                    importBr.ReadInt32(); // fieldCount
                                    importBr.ReadInt32(); // recordSize
                                    importBr.ReadInt32(); // stringBlockSize
                                    tableHash = (uint)importBr.ReadInt32();
                                    build = (uint)importBr.ReadInt32();
                                    importBr.ReadInt32(); // unknown/timestamp placeholder
                                    minId = importBr.ReadInt32();
                                    maxId = importBr.ReadInt32();
                                    locale = (uint)importBr.ReadInt32();
                                    copyTableSize = (uint)importBr.ReadInt32();
                                }
                                else
                                {
                                    _logger($"Import file {importFilePath} does not have expected DB2 magic. Using default header values.");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger($"Error reading DB2 header from {importFilePath}: {ex.Message}. Using default header values.");
                        }
                    }
                    else
                    {
                        _logger($"DB2 import file not found at {importFilePath}. Using default header values.");
                    }
                }
                bw.Write(Encoding.ASCII.GetBytes("WDB2"));
                bw.Write(recordCount);
                bw.Write(totalElements);
                bw.Write(recordSize);
                bw.Write(stringBlockData.Length);
                bw.Write(tableHash);
                bw.Write(build);
                bw.Write(timestamp);
                bw.Write(minId);
                bw.Write(maxId);
                bw.Write(locale);
                bw.Write(copyTableSize);
            }
            // Write the record data and string block.
            bw.Write(recordsData);
            bw.Write(stringBlockData);
        }

        _logger($"Exported {recordCount} records from {dbTableName} to {exportPath}");
    }

    /// <summary>
    /// Writes a field value to the record stream.
    /// For string fields, writes the offset into the string block.
    /// For numeric fields, writes the parsed value (or 0 if parsing fails).
    /// </summary>
    private void WriteFieldValue(BinaryWriter writer, FieldDefinition field, object valueObj, MemoryStream stringBlockStream, Dictionary<string, int> stringOffsets)
    {
        if (field.Type.Equals("int", StringComparison.OrdinalIgnoreCase))
        {
            int val = 0;
            if (valueObj != null)
                int.TryParse(valueObj.ToString(), out val);
            writer.Write(val);
        }
        else if (field.Type.Equals("short", StringComparison.OrdinalIgnoreCase))
        {
            short val = 0;
            if (valueObj != null)
                short.TryParse(valueObj.ToString(), out val);
            writer.Write(val);
        }
        else if (field.Type.Equals("long", StringComparison.OrdinalIgnoreCase))
        {
            long val = 0;
            if (valueObj != null)
                long.TryParse(valueObj.ToString(), out val);
            writer.Write(val);
        }
        else if (field.Type.Equals("float", StringComparison.OrdinalIgnoreCase))
        {
            float val = 0;
            if (valueObj != null)
                float.TryParse(valueObj.ToString(), out val);
            writer.Write(val);
        }
        else if (field.Type.Equals("byte", StringComparison.OrdinalIgnoreCase))
        {
            byte val = 0;
            if (valueObj != null)
                byte.TryParse(valueObj.ToString(), out val);
            writer.Write(val);
        }
        else if (field.Type.Equals("string", StringComparison.OrdinalIgnoreCase))
        {
            string strVal = valueObj?.ToString() ?? "";
            if (!stringOffsets.TryGetValue(strVal, out int offset))
            {
                offset = (int)stringBlockStream.Position;
                stringOffsets[strVal] = offset;
                byte[] bytes = Encoding.UTF8.GetBytes(strVal);
                stringBlockStream.Write(bytes, 0, bytes.Length);
                stringBlockStream.WriteByte(0);
            }
            writer.Write(offset);
        }
        else
        {
            // Default to writing 0 for any unknown type.
            int val = 0;
            writer.Write(val);
        }
    }
}
