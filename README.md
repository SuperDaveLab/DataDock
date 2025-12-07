# DataDock

A .NET 8 CLI that converts CSV/XLSX workbooks into ready-to-load SQL Server tables. It can infer schemas, generate `CREATE TABLE` scripts, and push validated rows straight into the database. The CLI is the foundation for a future GUI experience, but it is already useful for automated imports, scripting, and repeatable data loads.

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

## Quick start

```bash
# clone & build
 git clone <repo-url>
 cd datadock
 dotnet restore
 dotnet build

# generate schema then import
 dotnet run --project DataDock.Cli -- schemagen \
   --input ./samples/Test_Tickets.xlsx \
   --table Test_Tickets \
   --output out/test_tickets.sql

 cat out/test_tickets.sql | docker exec -i mssql-dev \
   /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P 'S3cret!' -d DataDockDev

 dotnet run --project DataDock.Cli -- \
   --input ./samples/Test_Tickets.xlsx \
   --output out/test_tickets.json \
   --table Test_Tickets \
   --write-db --ensure-table \
   --connection-string 'Server=localhost;Database=DataDockDev;User Id=sa;Password=S3cret!;Encrypt=True;TrustServerCertificate=True;'
```

## Versioning

The project follows [Semantic Versioning](https://semver.org/). Current release: **v0.1.0 (public preview)**.

## Contributing

Pull requests and issues are welcome! See [`CONTRIBUTING.md`](CONTRIBUTING.md) for the workflow, coding guidelines, and testing expectations.

## License

Released under the [MIT License](LICENSE).

---

## Support & sponsorship

If DataDock saves you time, consider tossing a small donation so development keeps rolling:

- **GitHub Sponsors:** sponsor profile is in the works.
- **Buy Me a Coffee / Ko-fi:** link a page of your choice and we’ll promote it alongside the releases.
- **BTC:** bc1qv5jguu4kcfqfgde6aely2n2cs5zkkv4v6g5ma8
