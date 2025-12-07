# DataDock

DataDock is a cross-platform Avalonia desktop app (with a companion CLI) that converts CSV/XLSX workbooks into ready-to-load SQL Server tables. It infers schemas, generates `CREATE TABLE` scripts, and pushes validated rows straight into the database. The GUI is now the primary experience, while the CLI remains available for automated imports and scripting.

## Highlights

- Workbook-first schemagen (`datadock schemagen`) with column-style controls
- Import pipeline with validation, JSON exports, and SQL Server write modes (insert, truncate-insert, upsert)
- Profile support for aliasing, key fields, and DB-first workflows
- Configurable global defaults via `datadock.config.json`
- Tested against SQL Server (local or containerized)

### Why not SSMS?

If you have ever tried to import Excel data with the SQL Server Import/Export Wizard and hit the dreaded
`The 'Microsoft.ACE.OLEDB.16.0' provider is not registered on the local machine` error (or any of the ACE/OLEDB provider
warnings), DataDock is the answer. The CLI uses `Microsoft.Data.SqlClient` plus CsvHelper/NPOI, so it never relies on the
missing ACE/OLEDB driver or the SSIS runtime. No 32-bit providers, no Access Database Engine installs—just .NET 8 and SQL Server.

Full CLI documentation lives in [`DataDock.Core/README`](DataDock.Core/README).

## Quick start (GUI)

1. **Download a release build** (coming soon) or run the GUI directly from source:

  ```bash
  git clone <repo-url>
  cd datadock
  dotnet run --project DataDock.Gui
  ```

2. **Connect**
  - Open the app, fill in the SQL Server host, port, database name, and authentication fields, then click *Test Connection* to verify reachability.
  - You can run the included `docker` compose snippet in the repo to spin up a local SQL Server if needed.

3. **Load a workbook**
  - Drag/drop a `.csv` or `.xlsx` file onto the window (Linux users can use the *Browse* button while native DnD support lands upstream).
  - DataDock infers field types, lets you rename columns, control max lengths, and deselect columns you do not want to import.

4. **Review + import**
  - The preview highlights validation issues; fix them inline or continue to see which rows will be skipped.
  - Click *Import* to create the table (if needed) and load the cleaned dataset. Progress, row counts, and any skipped-row reasons show in the status panel.

5. **Export artifacts**
  - Use the *Save Profile* option to stash your mapping choices for the CLI.
  - Export JSON snapshots, generated `CREATE TABLE` scripts, or the adjusted schema for future runs.

> ⚙️ Prefer automation? The CLI instructions below are still available and fully supported; see [`DataDock.Core/README`](DataDock.Core/README) for all switches.

## Versioning

The project follows [Semantic Versioning](https://semver.org/). Current release: **v0.1.1 (public preview)**.

## Contributing

Pull requests and issues are welcome! See [`CONTRIBUTING.md`](CONTRIBUTING.md) for the workflow, coding guidelines, and testing expectations.

## License

Released under the [MIT License](LICENSE).

---

## Support & sponsorship

If DataDock saves you time, consider tossing a small donation so development keeps rolling:

- **BTC:** bc1qv5jguu4kcfqfgde6aely2n2cs5zkkv4v6g5ma8
- **GitHub Sponsors:** sponsor profile is in the works.
- **Buy Me a Coffee / Ko-fi:** link a page of your choice and we’ll promote it alongside the releases.
