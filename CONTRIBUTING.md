# Contributing to DataDock

Thanks for your interest in improving DataDock! Contributions of any size are welcome—from typo fixes to new features. This guide explains how to get started.

## Prerequisites

- .NET 8 SDK
- Docker (optional, but useful for running SQL Server locally)
- SQL client such as `sqlcmd`, Azure Data Studio, or DBeaver
- Git

## Local setup

```bash
 git clone <repo-url>
 cd datadock
 dotnet restore
 dotnet build
 dotnet test
```

If you need SQL Server for integration testing, spin up the official image:

```bash
docker run -e "ACCEPT_EULA=Y" \
  -e "SA_PASSWORD=YourStrong!Passw0rd" \
  -p 1433:1433 \
  --name mssql-dev \
  -d mcr.microsoft.com/mssql/server:2022-latest
```

## Workflow

1. **Fork & branch** – create a feature branch off `main` (e.g., `feature/improve-write-mode`).
2. **Make changes** – keep commits focused and include tests/docs when relevant.
3. **Run tests** – `dotnet test` should pass locally before submitting a PR.
4. **Open a pull request** – describe the problem, solution, and testing performed. Link related issues if applicable.

## Coding guidelines

- Follow existing C# conventions used in the repo (PascalCase for types, CamelCase for locals/fields).
- Keep comments concise; prefer self-explanatory code.
- Add or update documentation (`README`, XML doc comments, etc.) for user-visible changes.
- When introducing new CLI flags or options, update `DataDock.Core/README`.

## Reporting issues

Use GitHub Issues to report bugs or request features. Please include:

- Environment details (OS, .NET version)
- Exact command(s) you ran
- Expected vs. actual behavior
- Logs or stack traces if available

## Code of conduct

We expect everyone to be respectful and follow common open-source etiquette. Be kind, assume positive intent, and help keep the community welcoming.

Thanks again for contributing!
