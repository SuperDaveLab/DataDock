# Global Configuration (`datadock.config.json`)

DataDock supports a global configuration file that defines default behaviors for database access, schema inference, and naming conventions.

Config file search order (nearest wins):

1. Working directory
2. Parent directories
3. `$HOME/.datadock/config.json`
4. `/etc/datadock/config.json`

---

# 📄 Example Config

```json
{
  "database": {
    "defaultConnectionString": "Server=localhost;Database=DataDockDev;User Id=sa;Password=Pass123!;Encrypt=True;TrustServerCertificate=True;",
    "defaultSchema": "dbo"
  },
  "defaults": {
    "columnNameStyle": "SnakeCase",
    "stringLengthStrategy": "MaxObservedRounded"
  }
}
```

---

# 🏆 Precedence Rules

### Connection String

1. CLI `--connection-string`
2. Profile `tableConnectionString`
3. Config `database.defaultConnectionString`

### Schema

1. CLI `--db-schema`
2. Profile `tableSchema`
3. Config `database.defaultSchema`
4. `"dbo"`

### Column Naming Style

1. CLI `--column-style`
2. Profile `columnNameStyle`
3. Config `defaults.columnNameStyle`

---

# 📁 Config Tips

* Use `$HOME/.datadock/config.json` for personal developer settings.
* Use `/etc/datadock/config.json` for system-wide settings on shared machines.
* Config does **not** need to include credentials; environment variables or CLI overrides are fine.
