# Profiles in DataDock

Profiles allow you to customize behavior per workbook or per table.
They are optional — DB-first imports often require only aliases or key fields.

---

# 📄 Example Profile

```json
{
  "name": "Tickets",
  "tableName": "Tickets",
  "columnNameStyle": "SnakeCase",
  "aliases": [
    { "targetFieldName": "TicketId", "alias": "Ticket #" },
    { "targetFieldName": "PONumber", "alias": "PO #" }
  ],
  "keyFields": ["TicketId"],
  "tableConnectionString": "Server=prod;Database=Ops;User Id=reporter;Password=****;",
  "tableSchema": "dbo"
}
```

---

# 🧠 Profile Use Cases

### ✔ DB-first workflow

* Existing table → DB schema is authoritative
* Profile only adds:

  * Aliases
  * Key fields
  * Naming overrides
  * TableName/TableSchema if needed

### ✔ Schema-first workflow (new tables)

* Profile defines target fields, types, max lengths
* Useful when you need tight control before creating tables

### ✔ Behavioral overrides

* Special parsing rules
* Field renames
* Different key fields for upserts
* Override column naming style for just one import

---

# 📌 Optional vs Required Fields

Profiles are purposely lightweight — everything except table name is optional.

For full schema definitions (new tables), use:

```
targetFields[]
```

For DB-first, skip them and DataDock will infer from SQL Server.

---

# 🧪 Interaction with CLI

CLI flags always override profile values.

Example:

```bash
datadock --profile tickets.json --table OverrideTable --column-style camelcase
```

The above overrides table name and column naming style just for that run.
