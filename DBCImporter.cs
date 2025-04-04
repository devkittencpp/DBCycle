using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;
using System.Threading;


namespace DBCycle
{
    public class DBCImporter
    {
        private readonly string _jsonDefinitionPath;
        private readonly string _dbcFolderPath;
        private readonly string _dbcConnectionString;
        private readonly string _db2ConnectionString;
        private readonly bool _developerModeEnabled;
        private readonly Action<string> _logger;

        public DBCImporter(string jsonDefinitionPath, string dbcFolderPath, string dbcConnectionString, string db2ConnectionString, bool developerModeEnabled, Action<string> logger)
        {
            _jsonDefinitionPath = jsonDefinitionPath;
            _dbcFolderPath = dbcFolderPath;
            _dbcConnectionString = dbcConnectionString;
            _db2ConnectionString = db2ConnectionString;
            _developerModeEnabled = developerModeEnabled;
            _logger = logger;
        }

        public void ImportAll(CancellationToken token, ManualResetEventSlim pauseEvent)
        {
            // Read and deserialize the JSON definition.
            string json = File.ReadAllText(_jsonDefinitionPath);
            DbDefinition dbDefinition = JsonConvert.DeserializeObject<DbDefinition>(json);

            foreach (var tableDef in dbDefinition.Tables)
            {
                // Check for cancellation and wait if paused.
                token.ThrowIfCancellationRequested();
                pauseEvent.Wait(token);

                // Build file path for the current table definition.
                string fileName = $"{tableDef.Name}.{tableDef.Extension}";
                string filePath = Path.Combine(_dbcFolderPath, fileName);
                if (!File.Exists(filePath))
                {
                    _logger($"File not found: {filePath}. Skipping table {tableDef.Name}.");
                    continue;
                }

                // Select the appropriate connection string based on the file extension.
                string connectionString = tableDef.Extension.ToLower() == "dbc"
                    ? _dbcConnectionString
                    : _db2ConnectionString;

                // Use the table name without the extension.
                string dbTableName = tableDef.Name;

                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    CreateTable(conn, tableDef, dbTableName);
                    ProcessTable(conn, tableDef, filePath, dbTableName, token, pauseEvent);
                }
            }
            _logger("All tables processed.");
        }


        private void CreateTable(MySqlConnection conn, TableDefinition tableDef, string dbTableName)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"CREATE TABLE IF NOT EXISTS {dbTableName} (");

            bool first = true;
            string primaryKey = null;

            foreach (var field in tableDef.Fields)
            {
                int arrayCount = field.ArraySize ?? 1;
                for (int i = 0; i < arrayCount; i++)
                {
                    if (!first)
                        sb.Append(", ");
                    first = false;

                    // Expand array names if needed.
                    string columnName = arrayCount > 1 ? $"{field.Name}_{i + 1}" : field.Name;
                    string mysqlType = DbcFieldHelper.MapToMySqlType(field.Type);
                    if (field.Type.Equals("string", StringComparison.OrdinalIgnoreCase))
                        mysqlType = "TEXT";

                    sb.Append($"{columnName} {mysqlType}");
                    if (field.IsIndex && i == 0)
                        primaryKey = columnName;
                }
            }

            if (!string.IsNullOrEmpty(primaryKey))
                sb.Append($", PRIMARY KEY({primaryKey})");

            sb.Append(") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");
            using (MySqlCommand cmd = new MySqlCommand(sb.ToString(), conn))
            {
                cmd.ExecuteNonQuery();
            }
            _logger($"Table {dbTableName} ensured in MySQL.");
        }

        private void ProcessTable(MySqlConnection conn, TableDefinition tableDef, string filePath, string dbTableName, CancellationToken token, ManualResetEventSlim pauseEvent)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (BinaryReader br = new BinaryReader(fs))
            {
                // Determine expected magic based on file extension.
                string expectedMagic = tableDef.Extension.ToLower() == "dbc" ? "WDBC" : "WDB2";
                string magic = new string(br.ReadChars(4));
                if (magic != expectedMagic)
                {
                    _logger($"Invalid file format in {filePath}. Expected {expectedMagic}, got {magic}.");
                    return;
                }

                // Read header information.
                int recordCount = br.ReadInt32();
                int headerFieldCount = br.ReadInt32();
                int recordSize = br.ReadInt32();
                int stringBlockSize = br.ReadInt32();

                // For DB2 files, read additional header fields.
                if (tableDef.Extension.ToLower() == "db2")
                {
                    int tableHash = br.ReadInt32();
                    int build = br.ReadInt32();
                    int unknown = br.ReadInt32();
                    int minId = br.ReadInt32();
                    int maxId = br.ReadInt32();
                    int locale = br.ReadInt32();
                    int copyTableSize = br.ReadInt32();
                }

                // Log header details if needed.
                _logger($"Header: [{tableDef.Name}] recordCount={recordCount}, fieldCount={headerFieldCount}, recordSize={recordSize}");

                // Validate field count and record size.
                int expectedFieldCount = tableDef.Fields.Sum(f => f.ArraySize.HasValue ? f.ArraySize.Value : 1);
                if (headerFieldCount != expectedFieldCount)
                    _logger($"Warning: Field count mismatch in {tableDef.Name}. Expected {expectedFieldCount}, got {headerFieldCount}.");
                if (recordSize != expectedFieldCount * 4)
                    _logger($"Warning: Record size mismatch in {tableDef.Name}. Expected {expectedFieldCount * 4}, got {recordSize}.");

                // Read record data and the string block.
                byte[] recordsData = br.ReadBytes(recordCount * recordSize);
                byte[] stringBlock = br.ReadBytes(stringBlockSize);

                // Additional validation for record size.
                int expectedByteSize = tableDef.Fields.Sum(f => (f.ArraySize ?? 1) * DbcFieldHelper.GetFieldSize(f.Type));
                if (recordSize != expectedByteSize)
                {
                    _logger($"Record size mismatch for {tableDef.Name}. Expected {expectedByteSize} bytes, got {recordSize} bytes.");
                }

                // Build the list of columns and parameters.
                List<string> columnList = new List<string>();
                List<string> paramList = new List<string>();
                foreach (var field in tableDef.Fields)
                {
                    int arrayCount = field.ArraySize ?? 1;
                    for (int i = 0; i < arrayCount; i++)
                    {
                        string colName = arrayCount > 1 ? $"{field.Name}_{i + 1}" : field.Name;
                        columnList.Add(colName);
                        paramList.Add($"@{colName}");
                    }
                }

                string columns = string.Join(", ", columnList);
                string paramNames = string.Join(", ", paramList);
                string insertSql = $"INSERT INTO {dbTableName} ({columns}) VALUES ({paramNames});";

                // Start a transaction and prepare the insert command.
                using (MySqlTransaction transaction = conn.BeginTransaction())
                using (MySqlCommand cmd = new MySqlCommand(insertSql, conn, transaction))
                {
                    // Add parameters for each column.
                    foreach (var col in columnList)
                        cmd.Parameters.Add(new MySqlParameter($"@{col}", null));

                    // Process each record.
                    for (int i = 0; i < recordCount; i++)
                    {
                        // Check for cancellation and wait if paused.
                        token.ThrowIfCancellationRequested();
                        pauseEvent.Wait(token);

                        int recordOffset = i * recordSize;
                        int currentByteOffset = recordOffset;

                        foreach (var field in tableDef.Fields)
                        {
                            int arrayCount = field.ArraySize ?? 1;
                            int fieldSize = DbcFieldHelper.GetFieldSize(field.Type);
                            for (int a = 0; a < arrayCount; a++)
                            {
                                // Ensure we don't read past the record.
                                object value = currentByteOffset + fieldSize <= recordOffset + recordSize
                                    ? DbcFieldHelper.ReadFieldValue(field, recordsData, currentByteOffset, stringBlock)
                                    : (field.Type.Equals("string", StringComparison.OrdinalIgnoreCase) ? "" : 0);
                                string paramName = arrayCount > 1 ? $"@{field.Name}_{a + 1}" : $"@{field.Name}";
                                cmd.Parameters[paramName].Value = value;
                                currentByteOffset += fieldSize;
                            }
                        }
                        cmd.ExecuteNonQuery();
                    }
                    transaction.Commit();
                    _logger($"Successfully imported {recordCount} records into {dbTableName}.");
                }
            }
        }

    }
}
