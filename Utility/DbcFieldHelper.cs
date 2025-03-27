using System;
using System.Text;

namespace DBCycle
{
    public static class DbcFieldHelper
    {
        public static string MapToMySqlType(string type)
        {
            switch (type.ToLower())
            {
                case "byte": return "TINYINT UNSIGNED";
                case "short": return "SMALLINT";
                case "int": return "INT";
                case "uint": return "INT UNSIGNED";
                case "long": return "BIGINT";
                case "float": return "FLOAT";
                case "string": return "TEXT";
                default: return "INT";
            }
        }

        public static int GetFieldSize(string type)
        {
            switch (type.ToLower())
            {
                case "byte": return 1;
                case "short": return 2;
                case "int": return 4;
                case "uint": return 4;
                case "long": return 8;
                case "float": return 4;
                case "string": return 4; // Offset to string block
                default: return 4;
            }
        }

        public static object ReadFieldValue(FieldDefinition field, byte[] recordData, int offset, byte[] stringBlock)
        {
            string type = field.Type.ToLower();
            switch (type)
            {
                case "byte":
                    return recordData[offset];
                case "short":
                    return BitConverter.ToInt16(recordData, offset);
                case "int":
                    return BitConverter.ToInt32(recordData, offset);
                case "uint":
                    return BitConverter.ToUInt32(recordData, offset);
                case "long":
                    return BitConverter.ToInt64(recordData, offset);
                case "float":
                    return BitConverter.ToSingle(recordData, offset);
                case "string":
                    int strOffset = BitConverter.ToInt32(recordData, offset);
                    return ReadStringFromBlock(stringBlock, strOffset);
                default:
                    return 0; // Default to 0 for unknown types
            }
        }

        private static string ReadStringFromBlock(byte[] stringBlock, int offset)
        {
            if (offset < 0 || offset >= stringBlock.Length)
                return string.Empty;

            int length = 0;
            while (offset + length < stringBlock.Length && stringBlock[offset + length] != 0)
                length++;

            return Encoding.UTF8.GetString(stringBlock, offset, length);
        }
    }
}
