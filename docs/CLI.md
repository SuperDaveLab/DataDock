# DataDock CLI Reference

The DataDock CLI provides a scriptable interface for importing CSV/XLSX data, generating table schemas, producing JSON output, and writing validated rows to a SQL Server database.

---

# 📄 Basic Usage

```bash
datadock --input file.csv [options]
```

Run help:

```bash
datadock --help
```

---

# 🚀 Commands

## 1. `import` (default)

Import data from a workbook and optionally write it to SQL Server.

### Syntax

```
datadock [--profile profile.json] --input <file>
         [--output out.json]
         [--table TableName]
         [--column-style style]
         [--write-db]
         [--ensure-table]
         [--connection-string <sql-connection-string>]
         [--db-schema schema]
         [--write-mode insert|truncate-insert|upsert]
         [--key-fields Field1,Field2,...]
```

### Important flags

| Flag                  | Description                                                 |
| --------------------- | ----------------------------------------------------------- |
| `--input`             | Required CSV/XLSX file                                      |
| `--profile`           | Optional mapping/behavior profile                           |
| `--output`            | Export cleaned JSON (default `<input>.out.json`)            |
| `--table`             | Override inferred table name                                |
| `--column-style`      | asis / camelcase / pascalcase / snakecase / titlewithspaces |
| `--write-db`          | Write rows to DB                                            |
| `--ensure-table`      | Create table if missing                                     |
| `--write-mode`        | insert / truncate-insert / upsert                           |
| `--key-fields`        | Required for upsert                                         |
| `--connection-string` | Overrides profile/config                                    |
| `--db-schema`         | Overrides profile/config                                    |

---

## 2. `schemagen`

Generate a SQL schema from a profile or workbook.

### Syntax

```
datadock schemagen 
   [--profile profile.json]
   [--input data.csv|data.xlsx]
   --table TableName
   [--column-style style]
   [--output file.sql]
   [--dialect sqlserver]
```

If no profile is provided, `--input` is required so DataDock can infer the schema.

---

# 🧠 Schema Inference Rules (CLI)

* Types inferred: `int`, `decimal`, `bool`, `datetime`, `string`.
* String lengths bucketed into:
  `50, 100, 255, 500, 1000, 2000, 3000, 4000`.
* DB-first mode: If the target table exists, its schema is authoritative.

---

# 🧪 DB Write Modes

### `insert`

Simple row insert.

### `truncate-insert`

Clears the table before inserting new rows.

### `upsert`

Performs update/insert using key fields.

Requires:

```
--key-fields Field1,Field2
```

---

# 🔒 Precedence Rules

For connection string:

```
--connection-string 
→ profile.tableConnectionString 
→ config.database.defaultConnectionString
```

For schema:

```
--db-schema 
→ profile.tableSchema 
→ config.database.defaultSchema 
→ dbo
```

For column naming style:

```
--column-style 
→ profile.columnNameStyle 
→ config.defaults.columnNameStyle
```

---

# 📚 Examples

Import + write:

```bash
datadock --input tickets.xlsx --table Tickets --write-db
```

Upsert:

```bash
datadock --input tickets.xlsx \
         --write-db --write-mode upsert \
         --key-fields TicketId
```

Infer schema:

```bash
datadock schemagen --input tickets.xlsx --table Tickets --output Tickets.sql
```
