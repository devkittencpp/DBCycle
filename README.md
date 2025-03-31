# DBCycle

DBCycle is an experimental program that transforms dbc/db2 files into MySQL databases and then converts them back into dbc/db2 files. The tool also includes a built-in dbc/db2 viewer and schema/definitions editor.

> **Note:**  
> Contents from the `doc/` folder will be copied to the bin output directory (where the executable is located) after building. Edit the configuration and update `schema.json` to auto-load the dbc files.

## Overview

- **Import/Export:**  
  - Imports dbc/db2 files into MySQL.
  - Exports data from MySQL back into dbc/db2 binary files.
- **Viewer & Editor:**  
  - Built-in viewer for dbc/db2 files.
  - Schema and definitions editor for customization.
- **Experimental Support:**  
  - Currently supports files defined in `schema.json` (mainly MOP 18414).
  - A new `schema.json` is required for 3.3.5 (12340) support.
