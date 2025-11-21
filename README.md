# DataDock

A .NET 8 CLI that converts CSV/XLSX workbooks into ready-to-load SQL Server tables. It can infer schemas, generate `CREATE TABLE` scripts, and push validated rows straight into the database. The CLI is the foundation for a future GUI experience, but it is already useful for automated imports, scripting, and repeatable data loads.

## Highlights

- Workbook-first schemagen (`datadock schemagen`) with column-style controls
- Import pipeline with validation, JSON exports, and SQL Server write modes (insert, truncate-insert, upsert)
- Profile support for aliasing, key fields, and DB-first workflows
- Configurable global defaults via `datadock.config.json`
- Tested against SQL Server (local or containerized)

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
