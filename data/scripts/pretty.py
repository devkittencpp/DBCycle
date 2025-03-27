import json

# Load the original JSON file.
input_path = "schema.json"
with open(input_path, "r", encoding="utf-8") as infile:
    data = json.load(infile)

def custom_format_schema(data):
    lines = []
    lines.append("{")
    lines.append("  \"Tables\": [")
    tables = data.get("Tables", [])
    for i, table in enumerate(tables):
        # Add a comma between table objects (but not before the first)
        if i > 0:
            lines.append("    ,")
        lines.append("    {")
        # Write "Name" and "Extension" keys with a 6-space indent.
        name = table.get("Name", "")
        extension = table.get("Extension", "")
        lines.append(f'      "Name": {json.dumps(name)},')
        lines.append(f'      "Extension": {json.dumps(extension)},')
        # Write the "Fields" key.
        lines.append('      "Fields": [')
        fields = table.get("Fields", [])
        # For each field, dump it in a compact style with one space after colons.
        for j, field in enumerate(fields):
            field_str = json.dumps(field, separators=(", ", ": "))
            if j < len(fields) - 1:
                lines.append(f"        {field_str},")
            else:
                lines.append(f"        {field_str}")
        lines.append("      ]")
        lines.append("    }")
    lines.append("  ]")
    lines.append("}")
    return "\n".join(lines)

# Generate the formatted output.
formatted_output = custom_format_schema(data)

# Save the output to a new file.
output_path = "schema_line_by_line.json"
with open(output_path, "w", encoding="utf-8") as outfile:
    outfile.write(formatted_output)

print(f"Formatted JSON saved to: {output_path}")

