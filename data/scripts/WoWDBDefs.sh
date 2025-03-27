#!/bin/bash
# This script searches each file for the marker "18414".
# When found, it extracts the block starting at the first line beginning with "$id"
# and continuing until an empty line is encountered.
# The results, preceded by the filename, are appended to results.txt.
##################################################################################
#
# PARSED FROM WOWDBDEFS TARGETING 18414
#
# WoWDBDefs https://github.com/wowdev/WoWDBDefs/tree/master/definitions

output="results_18414.txt"
# Clear any previous results
> "$output"

# Loop through all files in the current directory
for file in *; do
    # Only process regular files
    if [ -f "$file" ]; then
        # Check if the file contains "18414"
        if grep -q "18414" "$file"; then
            # Write the filename to the output file
            echo "$file" >> "$output"
            # Use awk to extract the block starting with "$id" after the marker
            awk '
            /18414/ { found=1 }
            found && /^\$id/ { printFlag=1 }
            printFlag {
                if (/^$/) exit   # Stop printing when a blank line is encountered
                print
            }
            ' "$file" >> "$output"
            # Add an extra newline to separate results from different files
            echo "" >> "$output"
        fi
    fi
done

