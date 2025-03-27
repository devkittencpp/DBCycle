#!/bin/bash

# Output JSON to schema.json
{
  echo '{'
  echo '  "Tables": ['

  first_table=true

  while IFS= read -r line; do
    # Skip empty lines
    [[ -z "$line" ]] && continue

    # Check if the line is a table definition (ends with .dbd)
    if [[ "$line" =~ \.dbd$ ]]; then
      # Close previous table if not the first
      if [ "$first_table" = false ]; then
        echo '    ] },'
      fi
      first_table=false

      # Extract table name by removing .dbd extension
      table_name="${line%.dbd}"
      echo '    { "Name": "'"$table_name"'", "Extension": "dbc", "Fields": ['

      # Extract field types from 18414.xml for this table
      mapfile -t types < <(xmlstarlet sel -t -m "//Table[@Name='$table_name']/Field" -v "@Type" -n 18414.xml | grep -v '^$')
      type_index=0
      first_field=true

    else
      # Parse field definition from results_18414.txt
      if [[ "$line" =~ ^\$id\$ ]]; then
        is_index=true
        field_def="${line#\$id\$}"
      else
        is_index=false
        field_def="$line"
      fi

      # Extract name (before < or [), bit size (<32> or <64>), and array size ([x])
      name=$(echo "$field_def" | sed 's/<.*//; s/\[.*//')
      bit_size=$(echo "$field_def" | grep -o '<[0-9]\+>' | tr -d '<>')
      array_size=$(echo "$field_def" | grep -o '\[[0-9]\+\]' | tr -d '[]')

      # Determine field type and generate JSON
      if [ -z "$array_size" ]; then
        # Non-array field
        if [ "$bit_size" = "64" ]; then
          field_type="long"
        else
          xml_type="${types[$type_index]}"
          # Map uint to int as observed in sample output
          [ "$xml_type" = "uint" ] && field_type="int" || field_type="$xml_type"
        fi

        if [ "$first_field" = false ]; then
          echo '      ,'
        fi
        echo -n '      { "Name": "'"$name"'", "Type": "'"$field_type"'"'
        [ "$is_index" = true ] && echo -n ', "IsIndex": true'
        echo ' }'
        ((type_index++))

      else
        # Array field
        xml_type="${types[$type_index]}"
        [ "$xml_type" = "uint" ] && field_type="int" || field_type="$xml_type"

        if [ "$first_field" = false ]; then
          echo '      ,'
        fi
        echo '      { "Name": "'"$name"'", "Type": "'"$field_type"'", "ArraySize": '"$array_size"' }'
        ((type_index += array_size))
      fi

      first_field=false
    fi
  done < results_18414.txt

  # Close the last table
  echo '    ] }'
  echo '  ]'
  echo '}'
} > schema.json
