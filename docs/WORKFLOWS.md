# Common Workflows

This guide walks through real-world scenarios:
new tables, existing tables, upserts, and truncation imports.

---

# 1️⃣ Create a New Table from a Workbook

```bash
datadock schemagen \
  --input NewReport.xlsx \
  --table NewReport \
  --output out/NewReport.sql
```

Review + run the SQL against your DB.

Import rows:

```bash
datadock --input NewReport.xlsx \
         --table NewReport \
         --write-db --ensure-table
```

---

# 2️⃣ Re-import into an Existing Table

Either truncate manually or let DataDock do it:

```bash
datadock --input WeeklyReport.xlsx \
         --table ACVTickets \
         --write-db --write-mode truncate-insert
```

---

# 3️⃣ Upsert (Update + Insert)

```bash
datadock --input WeeklyReport.xlsx \
         --table ACVTickets \
         --write-db --write-mode upsert \
         --key-fields TicketId
```

---

# 4️⃣ DB-First Import with Aliases

Minimal profile:

```json
{
  "tableName": "ACVTickets",
  "aliases": [
    { "targetFieldName": "TicketId", "alias": "Ticket #" }
  ]
}
```

Usage:

```bash
datadock --profile profiles/acv.json --input ACV.xlsx --write-db
```

---

# 5️⃣ Export JSON for Data Pipelines

```bash
datadock --input RawData.csv \
         --output out/RawData.json
```

---

# 6️⃣ Test with Local SQL Server

```bash
docker compose up sqlserver
datadock --input sample.xlsx --write-db
```

See `docs/CONFIG.md` for setting default connection strings.
