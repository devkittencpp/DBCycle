#!/bin/bash
# update_schema.sh
# This script reads listfile.txt for filenames with either "dbc" or "db2" extensions
# and updates schema.json accordingly. It assumes schema.json has the structure:
#
# {
#   "Tables": [
#     { "Name": "...", "Extension": "...", "Fields": [ ... ] },
#     ...
#   ]
# }
#
# If a base name appears with both .dbc and .db2, that base is skipped.

# 1. Check required files exist.
if [ ! -f listfile.txt ]; then
  echo "Error: listfile.txt not found!"
  exit 1
fi

if [ ! -f schema.json ]; then
  echo "Error: schema.json not found!"
  exit 1
fi

# 2. Declare associative arrays to store unique extension and duplicates.
declare -A file_ext
declare -A duplicate

# 3. Process listfile.txt line by line.
while read -r line; do
  # Skip empty lines
  [ -z "$line" ] && continue

  # Extract the filename, base name, and extension.
  filename=$(basename "$line")
  base="${filename%.*}"
  ext="${filename##*.}"

  # Convert base name to lower case for case-insensitive matching.
  base_lower=$(echo "$base" | tr '[:upper:]' '[:lower:]')

  # If we already recorded an extension for this base and it differs, mark as duplicate.
  if [[ -n "${file_ext[$base_lower]}" && "${file_ext[$base_lower]}" != "$ext" ]]; then
    duplicate["$base_lower"]=1
  else
    file_ext["$base_lower"]="$ext"
  fi
done < listfile.txt

# 4. For each base name with a unique extension, update schema.json.
for base_lower in "${!file_ext[@]}"; do
  # Only update if there is no duplicate.
  if [ -z "${duplicate[$base_lower]}" ]; then
    ext="${file_ext[$base_lower]}"
    echo "Updating schema entry for '$base_lower' to extension '$ext'..."

    # 5. Use jq to update the JSON.
    #    We assume schema.json is an object with a "Tables" array.
    #    The filter maps over each element in .Tables and, if the "Name" field
    #    matches base_lower (case-insensitive), sets .Extension to $ext.
    tmpfile=$(mktemp)
    jq --arg name "$base_lower" --arg ext "$ext" '
      .Tables |= map(
        if ((.Name | type) == "string" and (.Name | ascii_downcase) == $name)
        then .Extension = $ext
        else .
        end
      )
    ' schema.json > "$tmpfile" && mv "$tmpfile" schema.json
  else
    echo "Skipping '$base_lower' because multiple extensions were found."
  fi
done

echo "schema.json updated successfully."

