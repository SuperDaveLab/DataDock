# DataDock Roadmap

This document tracks future improvements and stretch goals.

---

# 🟦 Tier 1 — In Progress / High Value

### ✔ Additional SQL dialects

* PostgreSQL
* MySQL / MariaDB
* SQLite

### ✔ Workbook–vs–DB diff engine

* Compare workbook with target DB table
* Highlight inserts/updates/deletes before applying
* Optional “dry run” mode for upserts

### ✔ Schema Visualizer

* Graph/table visualization of inferred or DB schemas
* Export diagram as PNG/SVG
* Standalone tool or built into GUI

---

# 🟩 Tier 2 — UX / Quality Enhancements

* Profile editor inside GUI
* Context-aware type inference (better numeric/decimal precision)
* Column heuristics: ID/code detection, text blob hints
* Improved error reporting UI

---

# 🟧 Tier 3 — PWA / Web Version

* WebAssembly build of the GUI
* Browser-based schema visualization
* Local file access + remote DB connections

---

# 🟥 Tier 4 (Long-term Exploration)

* Plugin system for custom converters / mappers
* Data pipeline integrations (Kafka, Blob Storage, S3, etc.)
* Visual mapping tool for ETL-style transformations

---

If you want to contribute to any of these, open an Issue or PR!
