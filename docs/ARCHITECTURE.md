# DataDock Architecture Overview

DataDock is split into three main components:

```
DataDock.Core   (shared engine)
DataDock.Cli    (command-line interface)
DataDock.Gui    (Avalonia desktop app)
```

---

# 🧩 Core Concepts

### 1. Schema Inference

`FileSchemaInferenceService` scans workbook rows to infer:

* field names
* data types
* max observed lengths → bucketized

### 2. DB Schema Introspection

`IDatabaseSchemaInspector` + `SqlServerSchemaInspector` read:

* column names
* SQL types
* max lengths
* nullability

Used for DB-first workflows.

---

# 🧠 Mapping & Conversions

* `DefaultColumnMapper` maps source headers → target fields
* `ValueConverter` handles typed conversions (int/decimal/bool/datetime/string)
* `ImportRowResult` holds success/failure details per row

---

# 📝 SQL Generation

* `TableSchemaBuilder` builds internal schema models
* `ISqlDialect` defines how columns/types are emitted
* `SqlServerDialect` outputs SQL Server DDL

---

# 🗄️ Database Writes

`IDataWriter` abstraction powers insert/truncate-insert/upsert.

`SqlServerDataWriter` uses parameterized commands via `Microsoft.Data.SqlClient`.

---

# 🖥 GUI Layer (Avalonia)

GUI consumes the same Core services for:

* workbook preview
* schema editing
* validation visualization
* DB writes
* profile export

---

# 🔧 CLI Layer

Thin wrapper that:

* loads config
* loads profile
* resolves precedence
* calls appropriate Core services
* prints summaries and diagnostics

---

# 🧪 Testing

Test coverage includes:

* bucketizer
* schema inference
* dialect behavior
* upsert / insert logic
* CLI parsing
* profile resolution

The architecture supports future DB providers by implementing:

```
IDatabaseSchemaInspector
ISqlDialect
IDataWriter
```
