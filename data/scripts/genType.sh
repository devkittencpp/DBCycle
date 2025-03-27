#!/bin/bash

# Step 1: Generate types.json from 18414.xml
{
  echo "{"
  first=1
  for name in $(xmlstarlet sel -t -m "//Table" -v "@Name" -n 18414.xml); do
    if [ $first -eq 0 ]; then echo ","; fi
    first=0
    lower_name=$(echo "$name" | tr '[:upper:]' '[:lower:]')
    types=$(xmlstarlet sel -t -m "//Table[@Name='$name']/Field" -v "@Type" -n 18414.xml | sed 's/^/"/; s/$/"/' | paste -sd,)
    echo "\"$lower_name\": [$types]"
  done
  echo "}"
} > types.json

# Step 2: Update schema.json using jq
jq --slurpfile types types.json '
  .Tables |= map(
    . as $table |
    ($table.Name | ascii_downcase) as $lower_name |
    $types[0][$lower_name] as $xml_types |
    if $xml_types then
      reduce .Fields[] as $field (
        {new_fields: [], index: 0};
        .new_fields += [
          if $field.Type == "" then
            $field + {"Type": $xml_types[.index]}
          else
            $field
          end
        ] |
        .index += ($field.ArraySize // 1)
      ) | $table | .Fields = .new_fields
    else
      $table
    end
  )
' schema.json > updated_schema.json

# Step 3: Optional - Replace the original file
# mv updated_schema.json schema.json
