# Base Técnica do Varthex Comanda (Etapas 0+1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up the .NET 10 / WPF / SQLite solution skeleton for Varthex Comanda — no business screens — so the app builds, opens an empty window, persists to a properly-migrated SQLite database under `%LOCALAPPDATA%`, refuses a second concurrent instance, and logs to a rotating file.

**Architecture:** Clean layering — `Domain` (POCO entities, enums, zero dependencies) ← `Application` (ports/interfaces only, e.g. `IClock`) ← `Infrastructure` (EF Core + SQLite, filesystem, mutex, logging) ← `Desktop` (WPF composition root). Tests mirror `src/` one-to-one.

**Tech Stack:** C# / .NET 10 LTS, WPF (XAML, MVVM via `CommunityToolkit.Mvvm`), EF Core + SQLite, `Microsoft.Extensions.DependencyInjection`, Serilog (rolling file sink), xUnit.

**Spec:** [specs/2026-09-16-base-tecnica-design.md](2026-09-16-base-tecnica-design.md)

## Global Constraints

- Money is stored and passed as `long` centavos — never `double`/`decimal` for currency in this slice.
- `PRAGMA foreign_keys = ON` on every SQLite connection.
- Data lives at `%LOCALAPPDATA%\VarthexComanda\{data,logs,backups}` — never inside the published app folder. Resolve the root via `Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)`.
- Only one running instance may hold the database; a second launch must detect this, inform the user, and exit without touching the database.
- No business entities/screens (catalog, comanda, item, sale) beyond the data shape needed for the EF Core mapping — RF/RN implementation starts in the next slice.
- Every project targets `net10.0`. Package versions are whatever `dotnet add package` resolves as latest stable at execution time — do not hand-pick version numbers.
- `Domain` references nothing; `Application` references `Domain`; `Infrastructure` references `Application`+`Domain`; `Desktop` references `Application`+`Infrastructure`; each test project references only the project it verifies.

---

### Task 1: Solution & project scaffold

**Files:**
- Create: `backend/VarthexComanda.slnx` (this SDK's `dotnet new sln` defaults to the .slnx format; do not force legacy .sln)
- Create: `backend/src/VarthexComanda.Domain/VarthexComanda.Domain.csproj`
- Create: `backend/src/VarthexComanda.Application/VarthexComanda.Application.csproj`
- Create: `backend/src/VarthexComanda.Infrastructure/VarthexComanda.Infrastructure.csproj`
- Create: `backend/src/VarthexComanda.Desktop/VarthexComanda.Desktop.csproj` (+ default `App.xaml`, `MainWindow.xaml` from the `wpf` template)
- Create: `backend/tests/VarthexComanda.Domain.Tests/VarthexComanda.Domain.Tests.csproj`
- Create: `backend/tests/VarthexComanda.Application.Tests/VarthexComanda.Application.Tests.csproj`
- Create: `backend/tests/VarthexComanda.Infrastructure.Tests/VarthexComanda.Infrastructure.Tests.csproj`

**Interfaces:**
- Produces: four buildable src projects and three buildable (empty) test projects, wired into one solution, with the reference graph in Global Constraints.

- [ ] **Step 1: Create the solution and every project**

Run from `backend/` (create the directory first):

```powershell
mkdir backend
cd backend
dotnet new sln -n VarthexComanda
dotnet new classlib -n VarthexComanda.Domain -o src/VarthexComanda.Domain -f net10.0
dotnet new classlib -n VarthexComanda.Application -o src/VarthexComanda.Application -f net10.0
dotnet new classlib -n VarthexComanda.Infrastructure -o src/VarthexComanda.Infrastructure -f net10.0
dotnet new wpf -n VarthexComanda.Desktop -o src/VarthexComanda.Desktop -f net10.0
dotnet new xunit -n VarthexComanda.Domain.Tests -o tests/VarthexComanda.Domain.Tests -f net10.0
dotnet new xunit -n VarthexComanda.Application.Tests -o tests/VarthexComanda.Application.Tests -f net10.0
dotnet new xunit -n VarthexComanda.Infrastructure.Tests -o tests/VarthexComanda.Infrastructure.Tests -f net10.0
dotnet sln add (Get-ChildItem -Recurse -Filter *.csproj)
```

Delete the placeholder `Class1.cs` that `classlib` templates generate in Domain, Application and Infrastructure (Task 2+ replace them). Also delete the placeholder `UnitTest1.cs` that `dotnet new xunit` generates in each of the 3 test projects — they assert nothing and would throw off Task 9's exact per-project test counts.

- [ ] **Step 2: Wire project references**

```powershell
dotnet add src/VarthexComanda.Application reference src/VarthexComanda.Domain
dotnet add src/VarthexComanda.Infrastructure reference src/VarthexComanda.Domain
dotnet add src/VarthexComanda.Infrastructure reference src/VarthexComanda.Application
dotnet add src/VarthexComanda.Desktop reference src/VarthexComanda.Application
dotnet add src/VarthexComanda.Desktop reference src/VarthexComanda.Infrastructure
dotnet add tests/VarthexComanda.Domain.Tests reference src/VarthexComanda.Domain
dotnet add tests/VarthexComanda.Application.Tests reference src/VarthexComanda.Application
dotnet add tests/VarthexComanda.Infrastructure.Tests reference src/VarthexComanda.Infrastructure
```

- [ ] **Step 3: Verify the empty solution builds and tests run**

```powershell
dotnet build
dotnet test
```

Expected: build succeeds (0 errors); `dotnet test` reports 0 tests found in each project (they're still empty) and exits 0.

- [ ] **Step 4: Commit**

```powershell
git add backend
git commit -m "chore: cria solução .NET 10 e esqueleto dos projetos"
```

---

### Task 2: Domain entities & enums

**Files:**
- Create: `backend/src/VarthexComanda.Domain/StatusComanda.cs`
- Create: `backend/src/VarthexComanda.Domain/StatusVenda.cs`
- Create: `backend/src/VarthexComanda.Domain/StatusBackup.cs`
- Create: `backend/src/VarthexComanda.Domain/Categoria.cs`
- Create: `backend/src/VarthexComanda.Domain/Produto.cs`
- Create: `backend/src/VarthexComanda.Domain/Comanda.cs`
- Create: `backend/src/VarthexComanda.Domain/ItemComanda.cs`
- Create: `backend/src/VarthexComanda.Domain/Venda.cs`
- Create: `backend/src/VarthexComanda.Domain/Configuracao.cs`
- Create: `backend/src/VarthexComanda.Domain/BackupRegistro.cs`
- Test: `backend/tests/VarthexComanda.Domain.Tests/EntityShapeTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: the 7 entity classes + 3 enums below, one field per column in [docs/database/schema.sql](../docs/database/schema.sql), consumed by Task 5's EF Core configurations.
  - `Comanda { int Id; int Numero; StatusComanda Status; DateTime AbertaEm; DateTime? FechadaEm; long TotalCentavos; string? Observacao; }`
  - `ItemComanda { int Id; int ComandaId; int ProdutoId; string NomeProduto; long PrecoUnitarioCentavos; int Quantidade; long SubtotalCentavos; string? Observacao; DateTime CriadoEm; DateTime AtualizadoEm; }`
  - `Venda { int Id; int ComandaId; int Numero; long TotalCentavos; DateTime FinalizadaEm; StatusVenda Status; }`
  - `Categoria { int Id; string Nome; bool Ativo; DateTime CriadoEm; DateTime AtualizadoEm; }`
  - `Produto { int Id; int CategoriaId; string Nome; long PrecoCentavos; bool Ativo; DateTime CriadoEm; DateTime AtualizadoEm; }`
  - `Configuracao { string Chave; string Valor; DateTime AtualizadoEm; }`
  - `BackupRegistro { int Id; string Arquivo; string Destino; DateTime CriadoEm; StatusBackup Status; string? Checksum; string? Mensagem; }`

- [ ] **Step 1: Write the failing shape tests**

`backend/tests/VarthexComanda.Domain.Tests/EntityShapeTests.cs`:

```csharp
using VarthexComanda.Domain;
using Xunit;

namespace VarthexComanda.Domain.Tests;

public class EntityShapeTests
{
    [Fact]
    public void Comanda_AbertaSemFechadaEm_ExpoeCamposObrigatorios()
    {
        var comanda = new Comanda
        {
            Id = 1,
            Numero = 42,
            Status = StatusComanda.Aberta,
            AbertaEm = new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc),
            FechadaEm = null,
            TotalCentavos = 0,
            Observacao = null
        };

        Assert.Equal(42, comanda.Numero);
        Assert.Equal(StatusComanda.Aberta, comanda.Status);
        Assert.Null(comanda.FechadaEm);
    }

    [Fact]
    public void ItemComanda_ExpoeSnapshotDeNomeEPreco()
    {
        var item = new ItemComanda
        {
            Id = 1,
            ComandaId = 1,
            ProdutoId = 7,
            NomeProduto = "Refrigerante",
            PrecoUnitarioCentavos = 500,
            Quantidade = 2,
            SubtotalCentavos = 1000,
            Observacao = null,
            CriadoEm = DateTime.UtcNow,
            AtualizadoEm = DateTime.UtcNow
        };

        Assert.Equal("Refrigerante", item.NomeProduto);
        Assert.Equal(1000, item.SubtotalCentavos);
    }

    [Fact]
    public void Venda_VinculaComandaENumeroUnico()
    {
        var venda = new Venda
        {
            Id = 1,
            ComandaId = 5,
            Numero = 5,
            TotalCentavos = 5400,
            FinalizadaEm = DateTime.UtcNow,
            Status = StatusVenda.Concluida
        };

        Assert.Equal(StatusVenda.Concluida, venda.Status);
        Assert.Equal(5400, venda.TotalCentavos);
    }

    [Fact]
    public void BackupRegistro_RegistraSucessoOuFalha()
    {
        var registro = new BackupRegistro
        {
            Id = 1,
            Arquivo = "varthex-comanda-2026-09-16.db",
            Destino = "D:\\backups",
            CriadoEm = DateTime.UtcNow,
            Status = StatusBackup.Sucesso,
            Checksum = "abc123",
            Mensagem = null
        };

        Assert.Equal(StatusBackup.Sucesso, registro.Status);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile**

```powershell
dotnet test backend/tests/VarthexComanda.Domain.Tests
```

Expected: compilation errors — `Comanda`, `ItemComanda`, `Venda`, `BackupRegistro`, `StatusComanda`, `StatusVenda`, `StatusBackup` do not exist yet.

- [ ] **Step 3: Implement the enums**

`backend/src/VarthexComanda.Domain/StatusComanda.cs`:

```csharp
namespace VarthexComanda.Domain;

public enum StatusComanda
{
    Aberta,
    Fechada,
    Cancelada
}
```

`backend/src/VarthexComanda.Domain/StatusVenda.cs`:

```csharp
namespace VarthexComanda.Domain;

public enum StatusVenda
{
    Concluida
}
```

`backend/src/VarthexComanda.Domain/StatusBackup.cs`:

```csharp
namespace VarthexComanda.Domain;

public enum StatusBackup
{
    Sucesso,
    Falha
}
```

- [ ] **Step 4: Implement the entities**

`backend/src/VarthexComanda.Domain/Categoria.cs`:

```csharp
namespace VarthexComanda.Domain;

public class Categoria
{
    public required int Id { get; set; }
    public required string Nome { get; set; }
    public required bool Ativo { get; set; }
    public required DateTime CriadoEm { get; set; }
    public required DateTime AtualizadoEm { get; set; }
}
```

`backend/src/VarthexComanda.Domain/Produto.cs`:

```csharp
namespace VarthexComanda.Domain;

public class Produto
{
    public required int Id { get; set; }
    public required int CategoriaId { get; set; }
    public required string Nome { get; set; }
    public required long PrecoCentavos { get; set; }
    public required bool Ativo { get; set; }
    public required DateTime CriadoEm { get; set; }
    public required DateTime AtualizadoEm { get; set; }
}
```

`backend/src/VarthexComanda.Domain/Comanda.cs`:

```csharp
namespace VarthexComanda.Domain;

public class Comanda
{
    public required int Id { get; set; }
    public required int Numero { get; set; }
    public required StatusComanda Status { get; set; }
    public required DateTime AbertaEm { get; set; }
    public DateTime? FechadaEm { get; set; }
    public required long TotalCentavos { get; set; }
    public string? Observacao { get; set; }
}
```

`backend/src/VarthexComanda.Domain/ItemComanda.cs`:

```csharp
namespace VarthexComanda.Domain;

public class ItemComanda
{
    public required int Id { get; set; }
    public required int ComandaId { get; set; }
    public required int ProdutoId { get; set; }
    public required string NomeProduto { get; set; }
    public required long PrecoUnitarioCentavos { get; set; }
    public required int Quantidade { get; set; }
    public required long SubtotalCentavos { get; set; }
    public string? Observacao { get; set; }
    public required DateTime CriadoEm { get; set; }
    public required DateTime AtualizadoEm { get; set; }
}
```

`backend/src/VarthexComanda.Domain/Venda.cs`:

```csharp
namespace VarthexComanda.Domain;

public class Venda
{
    public required int Id { get; set; }
    public required int ComandaId { get; set; }
    public required int Numero { get; set; }
    public required long TotalCentavos { get; set; }
    public required DateTime FinalizadaEm { get; set; }
    public required StatusVenda Status { get; set; }
}
```

`backend/src/VarthexComanda.Domain/Configuracao.cs`:

```csharp
namespace VarthexComanda.Domain;

public class Configuracao
{
    public required string Chave { get; set; }
    public required string Valor { get; set; }
    public required DateTime AtualizadoEm { get; set; }
}
```

`backend/src/VarthexComanda.Domain/BackupRegistro.cs`:

```csharp
namespace VarthexComanda.Domain;

public class BackupRegistro
{
    public required int Id { get; set; }
    public required string Arquivo { get; set; }
    public required string Destino { get; set; }
    public required DateTime CriadoEm { get; set; }
    public required StatusBackup Status { get; set; }
    public string? Checksum { get; set; }
    public string? Mensagem { get; set; }
}
```

- [ ] **Step 5: Run tests to verify they pass**

```powershell
dotnet test backend/tests/VarthexComanda.Domain.Tests
```

Expected: 4 tests pass.

- [ ] **Step 6: Commit**

```powershell
git add backend/src/VarthexComanda.Domain backend/tests/VarthexComanda.Domain.Tests
git commit -m "feat: adiciona entidades de domínio conforme schema.sql"
```

---

### Task 3: Infrastructure — caminhos locais e backup preventivo

**Files:**
- Create: `backend/src/VarthexComanda.Infrastructure/Storage/AppPaths.cs`
- Create: `backend/src/VarthexComanda.Infrastructure/Storage/DatabaseBackupService.cs`
- Test: `backend/tests/VarthexComanda.Infrastructure.Tests/AppPathsTests.cs`
- Test: `backend/tests/VarthexComanda.Infrastructure.Tests/DatabaseBackupServiceTests.cs`

**Interfaces:**
- Consumes: nothing yet (Task 5 will consume `AppPaths.DatabasePath`; Task 7 will consume `AppPaths.LogsDirectory`).
- Produces:
  - `AppPaths(string? rootOverride = null)` with `string Root { get; }`, `string DataDirectory { get; }`, `string LogsDirectory { get; }`, `string BackupsDirectory { get; }`, `string DatabasePath { get; }` (`= Path.Combine(DataDirectory, "varthex-comanda.db")`), and `void EnsureCreated()`.
  - `DatabaseBackupService.BackupIfExists(string databasePath, string backupsDirectory, DateTime timestampUtc) : string?` — returns the created backup file path, or `null` when there was nothing to back up.

- [ ] **Step 1: Write the failing tests**

`backend/tests/VarthexComanda.Infrastructure.Tests/AppPathsTests.cs`:

```csharp
using VarthexComanda.Infrastructure.Storage;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests;

public class AppPathsTests
{
    [Fact]
    public void EnsureCreated_CriaAsTresPastasSobOTerritorioIndicado()
    {
        var root = Path.Combine(Path.GetTempPath(), "VarthexComandaTests_" + Guid.NewGuid());
        try
        {
            var paths = new AppPaths(root);

            paths.EnsureCreated();

            Assert.True(Directory.Exists(paths.DataDirectory));
            Assert.True(Directory.Exists(paths.LogsDirectory));
            Assert.True(Directory.Exists(paths.BackupsDirectory));
            Assert.Equal(Path.Combine(paths.DataDirectory, "varthex-comanda.db"), paths.DatabasePath);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
```

`backend/tests/VarthexComanda.Infrastructure.Tests/DatabaseBackupServiceTests.cs`:

```csharp
using VarthexComanda.Infrastructure.Storage;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests;

public class DatabaseBackupServiceTests
{
    [Fact]
    public void BackupIfExists_QuandoBancoNaoExiste_NaoCriaCopiaERetornaNull()
    {
        var root = Path.Combine(Path.GetTempPath(), "VarthexComandaTests_" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var dbPath = Path.Combine(root, "varthex-comanda.db");
            var backupsDir = Path.Combine(root, "backups");
            Directory.CreateDirectory(backupsDir);

            var resultado = DatabaseBackupService.BackupIfExists(dbPath, backupsDir, DateTime.UtcNow);

            Assert.Null(resultado);
            Assert.Empty(Directory.GetFiles(backupsDir));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void BackupIfExists_QuandoBancoExiste_CopiaComNomeCarimbadoNoHorario()
    {
        var root = Path.Combine(Path.GetTempPath(), "VarthexComandaTests_" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var dbPath = Path.Combine(root, "varthex-comanda.db");
            File.WriteAllText(dbPath, "conteudo-fake-do-banco");
            var backupsDir = Path.Combine(root, "backups");
            Directory.CreateDirectory(backupsDir);
            var timestamp = new DateTime(2026, 9, 16, 14, 30, 0, DateTimeKind.Utc);

            var resultado = DatabaseBackupService.BackupIfExists(dbPath, backupsDir, timestamp);

            Assert.NotNull(resultado);
            Assert.True(File.Exists(resultado));
            Assert.Equal("varthex-comanda-2026-09-16-143000.db", Path.GetFileName(resultado));
            Assert.Equal("conteudo-fake-do-banco", File.ReadAllText(resultado));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```powershell
dotnet test backend/tests/VarthexComanda.Infrastructure.Tests
```

Expected: compilation errors — `AppPaths` and `DatabaseBackupService` do not exist.

- [ ] **Step 3: Implement `AppPaths`**

`backend/src/VarthexComanda.Infrastructure/Storage/AppPaths.cs`:

```csharp
namespace VarthexComanda.Infrastructure.Storage;

public class AppPaths
{
    public AppPaths(string? rootOverride = null)
    {
        var baseDir = rootOverride
            ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        Root = rootOverride is null ? Path.Combine(baseDir, "VarthexComanda") : baseDir;
        DataDirectory = Path.Combine(Root, "data");
        LogsDirectory = Path.Combine(Root, "logs");
        BackupsDirectory = Path.Combine(Root, "backups");
        DatabasePath = Path.Combine(DataDirectory, "varthex-comanda.db");
    }

    public string Root { get; }
    public string DataDirectory { get; }
    public string LogsDirectory { get; }
    public string BackupsDirectory { get; }
    public string DatabasePath { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(BackupsDirectory);
    }
}
```

- [ ] **Step 4: Implement `DatabaseBackupService`**

`backend/src/VarthexComanda.Infrastructure/Storage/DatabaseBackupService.cs`:

```csharp
namespace VarthexComanda.Infrastructure.Storage;

public static class DatabaseBackupService
{
    public static string? BackupIfExists(string databasePath, string backupsDirectory, DateTime timestampUtc)
    {
        if (!File.Exists(databasePath))
        {
            return null;
        }

        var fileName = $"varthex-comanda-{timestampUtc:yyyy-MM-dd-HHmmss}.db";
        var destination = Path.Combine(backupsDirectory, fileName);
        File.Copy(databasePath, destination, overwrite: false);
        return destination;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

```powershell
dotnet test backend/tests/VarthexComanda.Infrastructure.Tests
```

Expected: 3 tests pass (1 from `AppPathsTests`, 2 from `DatabaseBackupServiceTests`).

- [ ] **Step 6: Commit**

```powershell
git add backend/src/VarthexComanda.Infrastructure/Storage backend/tests/VarthexComanda.Infrastructure.Tests
git commit -m "feat: adiciona resolução de caminhos locais e backup preventivo"
```

---

### Task 4: Infrastructure — abstração de relógio

**Files:**
- Create: `backend/src/VarthexComanda.Application/Abstractions/IClock.cs`
- Create: `backend/src/VarthexComanda.Infrastructure/Time/SystemClock.cs`
- Test: `backend/tests/VarthexComanda.Infrastructure.Tests/SystemClockTests.cs`

**Interfaces:**
- Produces: `IClock { DateTime UtcNow { get; } }` (Application) and `SystemClock : IClock` (Infrastructure) — consumed by Task 3's `DatabaseBackupService` caller in Task 8 (Desktop composition) to supply the real timestamp.

- [ ] **Step 1: Write the failing test**

`backend/tests/VarthexComanda.Infrastructure.Tests/SystemClockTests.cs`:

```csharp
using VarthexComanda.Infrastructure.Time;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests;

public class SystemClockTests
{
    [Fact]
    public void UtcNow_RetornaHorarioUtcProximoDoAtual()
    {
        var clock = new SystemClock();

        var antes = DateTime.UtcNow;
        var agora = clock.UtcNow;
        var depois = DateTime.UtcNow;

        Assert.Equal(DateTimeKind.Utc, agora.Kind);
        Assert.InRange(agora, antes.AddSeconds(-1), depois.AddSeconds(1));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```powershell
dotnet test backend/tests/VarthexComanda.Infrastructure.Tests --filter SystemClockTests
```

Expected: compilation error — `SystemClock` does not exist.

- [ ] **Step 3: Implement `IClock` and `SystemClock`**

`backend/src/VarthexComanda.Application/Abstractions/IClock.cs`:

```csharp
namespace VarthexComanda.Application.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
}
```

`backend/src/VarthexComanda.Infrastructure/Time/SystemClock.cs`:

```csharp
using VarthexComanda.Application.Abstractions;

namespace VarthexComanda.Infrastructure.Time;

public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
```

- [ ] **Step 4: Run test to verify it passes**

```powershell
dotnet test backend/tests/VarthexComanda.Infrastructure.Tests --filter SystemClockTests
```

Expected: 1 test passes.

- [ ] **Step 5: Commit**

```powershell
git add backend/src/VarthexComanda.Application/Abstractions backend/src/VarthexComanda.Infrastructure/Time backend/tests/VarthexComanda.Infrastructure.Tests/SystemClockTests.cs
git commit -m "feat: adiciona abstração de relógio (IClock/SystemClock)"
```

---

### Task 5: Infrastructure — DbContext, mapeamento EF Core e migração inicial

**Files:**
- Create: `backend/src/VarthexComanda.Infrastructure/Persistence/VarthexComandaDbContext.cs`
- Create: `backend/src/VarthexComanda.Infrastructure/Persistence/DesignTimeDbContextFactory.cs`
- Create: `backend/src/VarthexComanda.Infrastructure/Persistence/Configurations/CategoriaConfiguration.cs`
- Create: `backend/src/VarthexComanda.Infrastructure/Persistence/Configurations/ProdutoConfiguration.cs`
- Create: `backend/src/VarthexComanda.Infrastructure/Persistence/Configurations/ComandaConfiguration.cs`
- Create: `backend/src/VarthexComanda.Infrastructure/Persistence/Configurations/ItemComandaConfiguration.cs`
- Create: `backend/src/VarthexComanda.Infrastructure/Persistence/Configurations/VendaConfiguration.cs`
- Create: `backend/src/VarthexComanda.Infrastructure/Persistence/Configurations/ConfiguracaoConfiguration.cs`
- Create: `backend/src/VarthexComanda.Infrastructure/Persistence/Configurations/BackupRegistroConfiguration.cs`
- Create: `backend/src/VarthexComanda.Infrastructure/Persistence/Migrations/*` (generated by `dotnet ef`)
- Test: `backend/tests/VarthexComanda.Infrastructure.Tests/VarthexComandaDbContextTests.cs`

**Interfaces:**
- Consumes: the 7 Domain entities from Task 2.
- Produces: `VarthexComandaDbContext(DbContextOptions<VarthexComandaDbContext> options) : DbContext` with `DbSet<Categoria> Categorias`, `DbSet<Produto> Produtos`, `DbSet<Comanda> Comandas`, `DbSet<ItemComanda> ItensComanda`, `DbSet<Venda> Vendas`, `DbSet<Configuracao> Configuracoes`, `DbSet<BackupRegistro> BackupRegistros` — consumed by Task 8 (Desktop composition) to call `Database.Migrate()`.

- [ ] **Step 1: Add the EF Core SQLite packages**

```powershell
dotnet add backend/src/VarthexComanda.Infrastructure package Microsoft.EntityFrameworkCore.Sqlite
dotnet add backend/src/VarthexComanda.Infrastructure package Microsoft.EntityFrameworkCore.Design
```

- [ ] **Step 2: Write the failing persistence tests**

`backend/tests/VarthexComanda.Infrastructure.Tests/VarthexComandaDbContextTests.cs`:

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Persistence;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests;

public class VarthexComandaDbContextTests : IDisposable
{
    private readonly string _dbPath;

    public VarthexComandaDbContextTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"varthex-comanda-tests-{Guid.NewGuid()}.db");
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private VarthexComandaDbContext CriarContexto()
    {
        var options = new DbContextOptionsBuilder<VarthexComandaDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        return new VarthexComandaDbContext(options);
    }

    [Fact]
    public void Migrate_EmBancoVazio_CriaTodasAsSeteTabelas()
    {
        using var contexto = CriarContexto();

        contexto.Database.Migrate();

        using var conexao = new SqliteConnection($"Data Source={_dbPath}");
        conexao.Open();
        using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' AND name NOT LIKE '__EFMigrationsHistory'";
        using var leitor = comando.ExecuteReader();
        var tabelas = new List<string>();
        while (leitor.Read()) tabelas.Add(leitor.GetString(0));

        Assert.Equal(
            new[] { "backup_registro", "categoria", "comanda", "configuracao", "item_comanda", "produto", "venda" },
            tabelas.OrderBy(t => t).ToArray());
    }

    [Fact]
    public void Migrate_ChamadoDuasVezes_NaoFalhaNemDuplicaEstrutura()
    {
        using (var primeiraExecucao = CriarContexto())
        {
            primeiraExecucao.Database.Migrate();
        }

        using var segundaExecucao = CriarContexto();
        segundaExecucao.Database.Migrate();

        var integridade = segundaExecucao.Database
            .SqlQueryRaw<string>("PRAGMA integrity_check")
            .AsEnumerable()
            .Single();
        Assert.Equal("ok", integridade);
    }

    [Fact]
    public void ComandaNumero_PermiteRepetirQuandoNaoEstaAberta_MasNaoQuandoDuasEstaoAbertas()
    {
        using var contexto = CriarContexto();
        contexto.Database.Migrate();

        contexto.Comandas.Add(new Comanda
        {
            Id = 1,
            Numero = 10,
            Status = StatusComanda.Fechada,
            AbertaEm = DateTime.UtcNow.AddHours(-2),
            FechadaEm = DateTime.UtcNow.AddHours(-1),
            TotalCentavos = 1000
        });
        contexto.Comandas.Add(new Comanda
        {
            Id = 2,
            Numero = 10,
            Status = StatusComanda.Aberta,
            AbertaEm = DateTime.UtcNow,
            FechadaEm = null,
            TotalCentavos = 0
        });
        contexto.SaveChanges();

        contexto.Comandas.Add(new Comanda
        {
            Id = 3,
            Numero = 10,
            Status = StatusComanda.Aberta,
            AbertaEm = DateTime.UtcNow,
            FechadaEm = null,
            TotalCentavos = 0
        });

        Assert.Throws<DbUpdateException>(() => contexto.SaveChanges());
    }
}
```

- [ ] **Step 3: Run tests to verify they fail to compile**

```powershell
dotnet test backend/tests/VarthexComanda.Infrastructure.Tests --filter VarthexComandaDbContextTests
```

Expected: compilation errors — `VarthexComandaDbContext` does not exist.

- [ ] **Step 4: Implement the `DbContext`**

`backend/src/VarthexComanda.Infrastructure/Persistence/VarthexComandaDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using VarthexComanda.Domain;

namespace VarthexComanda.Infrastructure.Persistence;

public class VarthexComandaDbContext : DbContext
{
    public VarthexComandaDbContext(DbContextOptions<VarthexComandaDbContext> options)
        : base(options)
    {
    }

    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<Comanda> Comandas => Set<Comanda>();
    public DbSet<ItemComanda> ItensComanda => Set<ItemComanda>();
    public DbSet<Venda> Vendas => Set<Venda>();
    public DbSet<Configuracao> Configuracoes => Set<Configuracao>();
    public DbSet<BackupRegistro> BackupRegistros => Set<BackupRegistro>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VarthexComandaDbContext).Assembly);
    }
}
```

There is no `OnConfiguring` override: the connection string and provider are always supplied by the caller through `DbContextOptions` (tests, the design-time factory in Step 5, and Task 8's composition root all build their own `new DbContextOptionsBuilder<VarthexComandaDbContext>().UseSqlite(...)`).

Every SQLite connection must run with foreign keys on; add this immediately after `UseSqlite` wherever the connection is opened — in the design-time factory (Step 5) and in Task 8's composition root — via `connection.Open(); using var pragma = connection.CreateCommand(); pragma.CommandText = "PRAGMA foreign_keys = ON;"; pragma.ExecuteNonQuery();` before constructing `DbContextOptions` from an already-open `SqliteConnection`.

- [ ] **Step 5: Implement the design-time factory**

`backend/src/VarthexComanda.Infrastructure/Persistence/DesignTimeDbContextFactory.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VarthexComanda.Infrastructure.Persistence;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<VarthexComandaDbContext>
{
    public VarthexComandaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<VarthexComandaDbContext>()
            .UseSqlite("Data Source=varthex-comanda.design.db")
            .Options;
        return new VarthexComandaDbContext(options);
    }
}
```

- [ ] **Step 6: Implement the entity configurations**

`backend/src/VarthexComanda.Infrastructure/Persistence/Configurations/CategoriaConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VarthexComanda.Domain;

namespace VarthexComanda.Infrastructure.Persistence.Configurations;

public class CategoriaConfiguration : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> builder)
    {
        builder.ToTable("categoria", t => t.HasCheckConstraint("CK_categoria_ativo", "ativo IN (0, 1)"));
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.Nome).HasColumnName("nome").UseCollation("NOCASE").IsRequired();
        builder.Property(c => c.Ativo).HasColumnName("ativo").HasConversion<int>().IsRequired();
        builder.Property(c => c.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(c => c.AtualizadoEm).HasColumnName("atualizado_em").IsRequired();
        builder.HasIndex(c => c.Nome).IsUnique();
    }
}
```

`backend/src/VarthexComanda.Infrastructure/Persistence/Configurations/ProdutoConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VarthexComanda.Domain;

namespace VarthexComanda.Infrastructure.Persistence.Configurations;

public class ProdutoConfiguration : IEntityTypeConfiguration<Produto>
{
    public void Configure(EntityTypeBuilder<Produto> builder)
    {
        builder.ToTable("produto", t =>
        {
            t.HasCheckConstraint("CK_produto_preco_centavos_positivo", "preco_centavos > 0");
            t.HasCheckConstraint("CK_produto_ativo", "ativo IN (0, 1)");
        });
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.CategoriaId).HasColumnName("categoria_id").IsRequired();
        builder.Property(p => p.Nome).HasColumnName("nome").IsRequired();
        builder.Property(p => p.PrecoCentavos).HasColumnName("preco_centavos").IsRequired();
        builder.Property(p => p.Ativo).HasColumnName("ativo").HasConversion<int>().IsRequired();
        builder.Property(p => p.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(p => p.AtualizadoEm).HasColumnName("atualizado_em").IsRequired();

        builder.HasOne<Categoria>()
            .WithMany()
            .HasForeignKey(p => p.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.CategoriaId, p.Ativo }).HasDatabaseName("idx_produto_categoria_ativo");
    }
}
```

`backend/src/VarthexComanda.Infrastructure/Persistence/Configurations/ComandaConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VarthexComanda.Domain;

namespace VarthexComanda.Infrastructure.Persistence.Configurations;

public class ComandaConfiguration : IEntityTypeConfiguration<Comanda>
{
    public void Configure(EntityTypeBuilder<Comanda> builder)
    {
        builder.ToTable("comanda", t =>
        {
            t.HasCheckConstraint("CK_comanda_numero_positivo", "numero > 0");
            t.HasCheckConstraint("CK_comanda_total_centavos_nao_negativo", "total_centavos >= 0");
            t.HasCheckConstraint(
                "CK_comanda_status_fechada_em",
                "(status = 'ABERTA' AND fechada_em IS NULL) OR (status IN ('FECHADA', 'CANCELADA') AND fechada_em IS NOT NULL)");
        });

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.Numero).HasColumnName("numero").IsRequired();
        builder.Property(c => c.Status)
            .HasColumnName("status")
            .HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<StatusComanda>(v, ignoreCase: true))
            .IsRequired();
        builder.Property(c => c.AbertaEm).HasColumnName("aberta_em").IsRequired();
        builder.Property(c => c.FechadaEm).HasColumnName("fechada_em");
        builder.Property(c => c.TotalCentavos).HasColumnName("total_centavos").HasDefaultValue(0L).IsRequired();
        builder.Property(c => c.Observacao).HasColumnName("observacao");

        builder.HasIndex(c => c.Numero)
            .IsUnique()
            .HasDatabaseName("uq_comanda_numero_aberta")
            .HasFilter("status = 'ABERTA'");
    }
}
```

`backend/src/VarthexComanda.Infrastructure/Persistence/Configurations/ItemComandaConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VarthexComanda.Domain;

namespace VarthexComanda.Infrastructure.Persistence.Configurations;

public class ItemComandaConfiguration : IEntityTypeConfiguration<ItemComanda>
{
    public void Configure(EntityTypeBuilder<ItemComanda> builder)
    {
        builder.ToTable("item_comanda", t =>
        {
            t.HasCheckConstraint("CK_item_comanda_preco_unitario_positivo", "preco_unitario_centavos > 0");
            t.HasCheckConstraint("CK_item_comanda_quantidade_positiva", "quantidade > 0");
            t.HasCheckConstraint("CK_item_comanda_subtotal_positivo", "subtotal_centavos > 0");
            t.HasCheckConstraint("CK_item_comanda_subtotal_igual_preco_vezes_quantidade", "subtotal_centavos = preco_unitario_centavos * quantidade");
        });

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.ComandaId).HasColumnName("comanda_id").IsRequired();
        builder.Property(i => i.ProdutoId).HasColumnName("produto_id").IsRequired();
        builder.Property(i => i.NomeProduto).HasColumnName("nome_produto").IsRequired();
        builder.Property(i => i.PrecoUnitarioCentavos).HasColumnName("preco_unitario_centavos").IsRequired();
        builder.Property(i => i.Quantidade).HasColumnName("quantidade").IsRequired();
        builder.Property(i => i.SubtotalCentavos).HasColumnName("subtotal_centavos").IsRequired();
        builder.Property(i => i.Observacao).HasColumnName("observacao");
        builder.Property(i => i.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(i => i.AtualizadoEm).HasColumnName("atualizado_em").IsRequired();

        builder.HasOne<Comanda>().WithMany().HasForeignKey(i => i.ComandaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Produto>().WithMany().HasForeignKey(i => i.ProdutoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(i => i.ComandaId).HasDatabaseName("idx_item_comanda_comanda");
    }
}
```

`backend/src/VarthexComanda.Infrastructure/Persistence/Configurations/VendaConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VarthexComanda.Domain;

namespace VarthexComanda.Infrastructure.Persistence.Configurations;

public class VendaConfiguration : IEntityTypeConfiguration<Venda>
{
    public void Configure(EntityTypeBuilder<Venda> builder)
    {
        builder.ToTable("venda", t =>
        {
            t.HasCheckConstraint("CK_venda_total_centavos_positivo", "total_centavos > 0");
            t.HasCheckConstraint("CK_venda_status_concluida", "status = 'CONCLUIDA'");
        });
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id");
        builder.Property(v => v.ComandaId).HasColumnName("comanda_id").IsRequired();
        builder.Property(v => v.Numero).HasColumnName("numero").IsRequired();
        builder.Property(v => v.TotalCentavos).HasColumnName("total_centavos").IsRequired();
        builder.Property(v => v.FinalizadaEm).HasColumnName("finalizada_em").IsRequired();
        builder.Property(v => v.Status)
            .HasColumnName("status")
            .HasConversion(
                s => s.ToString().ToUpperInvariant(),
                s => Enum.Parse<StatusVenda>(s, ignoreCase: true))
            .HasDefaultValue(StatusVenda.Concluida)
            .IsRequired();

        builder.HasOne<Comanda>().WithMany().HasForeignKey(v => v.ComandaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(v => v.ComandaId).IsUnique();
        builder.HasIndex(v => v.Numero).IsUnique();
        builder.HasIndex(v => v.FinalizadaEm).HasDatabaseName("idx_venda_finalizada_em");
    }
}
```

`backend/src/VarthexComanda.Infrastructure/Persistence/Configurations/ConfiguracaoConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using VarthexComanda.Domain;

namespace VarthexComanda.Infrastructure.Persistence.Configurations;

public class ConfiguracaoConfiguration : IEntityTypeConfiguration<Configuracao>
{
    public void Configure(EntityTypeBuilder<Configuracao> builder)
    {
        builder.ToTable("configuracao");
        builder.HasKey(c => c.Chave);
        builder.Property(c => c.Chave).HasColumnName("chave");
        builder.Property(c => c.Valor).HasColumnName("valor").IsRequired();
        builder.Property(c => c.AtualizadoEm).HasColumnName("atualizado_em").IsRequired();
    }
}
```

`backend/src/VarthexComanda.Infrastructure/Persistence/Configurations/BackupRegistroConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VarthexComanda.Domain;

namespace VarthexComanda.Infrastructure.Persistence.Configurations;

public class BackupRegistroConfiguration : IEntityTypeConfiguration<BackupRegistro>
{
    public void Configure(EntityTypeBuilder<BackupRegistro> builder)
    {
        builder.ToTable("backup_registro", t => t.HasCheckConstraint("CK_backup_registro_status", "status IN ('SUCESSO', 'FALHA')"));
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("id");
        builder.Property(b => b.Arquivo).HasColumnName("arquivo").IsRequired();
        builder.Property(b => b.Destino).HasColumnName("destino").IsRequired();
        builder.Property(b => b.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(b => b.Status)
            .HasColumnName("status")
            .HasConversion(
                s => s.ToString().ToUpperInvariant(),
                s => Enum.Parse<StatusBackup>(s, ignoreCase: true))
            .IsRequired();
        builder.Property(b => b.Checksum).HasColumnName("checksum");
        builder.Property(b => b.Mensagem).HasColumnName("mensagem");
    }
}
```

- [ ] **Step 7: Generate the initial migration**

```powershell
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate --project backend/src/VarthexComanda.Infrastructure --startup-project backend/src/VarthexComanda.Infrastructure --output-dir Persistence/Migrations
```

Use `VarthexComanda.Infrastructure` as both `--project` and `--startup-project` — not `VarthexComanda.Desktop`. Until Task 8 fixes `App.xaml.cs`'s base-class collision (see Task 8's note), the Desktop project does not build, and `dotnet ef` needs its startup project to build. `--output-dir Persistence/Migrations` is required too — without it, `dotnet ef` generates migrations under a top-level `Migrations/` folder instead of `Persistence/Migrations/`.

Expected: a `Persistence/Migrations/<timestamp>_InitialCreate.cs` (+ `.Designer.cs` + `VarthexComandaDbContextModelSnapshot.cs`) is generated, creating the 7 tables, their indexes and their check constraints.

- [ ] **Step 8: Run tests to verify they pass**

```powershell
dotnet test backend/tests/VarthexComanda.Infrastructure.Tests --filter VarthexComandaDbContextTests
```

Expected: 3 tests pass.

- [ ] **Step 9: Commit**

```powershell
git add backend/src/VarthexComanda.Infrastructure/Persistence backend/tests/VarthexComanda.Infrastructure.Tests/VarthexComandaDbContextTests.cs
git commit -m "feat: mapeia entidades no EF Core e gera migração inicial"
```

---

### Task 6: Infrastructure — instância única (mutex nomeado)

**Files:**
- Create: `backend/src/VarthexComanda.Infrastructure/Concurrency/SingleInstanceGuard.cs`
- Test: `backend/tests/VarthexComanda.Infrastructure.Tests/SingleInstanceGuardTests.cs`

**Interfaces:**
- Produces: `SingleInstanceGuard(string mutexName) : IDisposable` with `bool TryAcquire()` and `void Release()` — consumed by Task 8's startup sequence.

- [ ] **Step 1: Write the failing test**

`backend/tests/VarthexComanda.Infrastructure.Tests/SingleInstanceGuardTests.cs`:

```csharp
using VarthexComanda.Infrastructure.Concurrency;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests;

public class SingleInstanceGuardTests
{
    [Fact]
    public void TryAcquire_SegundaGuardaComMesmoNome_FalhaAteAPrimeiraLiberar()
    {
        var nomeMutex = "VarthexComandaTests_" + Guid.NewGuid();
        using var primeira = new SingleInstanceGuard(nomeMutex);
        using var segunda = new SingleInstanceGuard(nomeMutex);

        Assert.True(primeira.TryAcquire());
        Assert.False(segunda.TryAcquire());

        primeira.Release();

        using var terceira = new SingleInstanceGuard(nomeMutex);
        Assert.True(terceira.TryAcquire());
        terceira.Release();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```powershell
dotnet test backend/tests/VarthexComanda.Infrastructure.Tests --filter SingleInstanceGuardTests
```

Expected: compilation error — `SingleInstanceGuard` does not exist.

- [ ] **Step 3: Implement `SingleInstanceGuard`**

`backend/src/VarthexComanda.Infrastructure/Concurrency/SingleInstanceGuard.cs`:

```csharp
namespace VarthexComanda.Infrastructure.Concurrency;

public class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;
    private bool _acquired;

    public SingleInstanceGuard(string mutexName)
    {
        _mutex = new Mutex(initiallyOwned: false, name: $"Global\\{mutexName}");
    }

    public bool TryAcquire()
    {
        _acquired = _mutex.WaitOne(TimeSpan.Zero);
        return _acquired;
    }

    public void Release()
    {
        if (_acquired)
        {
            _mutex.ReleaseMutex();
            _acquired = false;
        }
    }

    public void Dispose()
    {
        Release();
        _mutex.Dispose();
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

```powershell
dotnet test backend/tests/VarthexComanda.Infrastructure.Tests --filter SingleInstanceGuardTests
```

Expected: 1 test passes.

- [ ] **Step 5: Commit**

```powershell
git add backend/src/VarthexComanda.Infrastructure/Concurrency backend/tests/VarthexComanda.Infrastructure.Tests/SingleInstanceGuardTests.cs
git commit -m "feat: adiciona guarda de instância única via mutex nomeado"
```

---

### Task 7: Infrastructure — logging com rotação diária

**Files:**
- Create: `backend/src/VarthexComanda.Infrastructure/Logging/LoggingConfigurator.cs`
- Test: `backend/tests/VarthexComanda.Infrastructure.Tests/LoggingConfiguratorTests.cs`

**Interfaces:**
- Consumes: `AppPaths.LogsDirectory` from Task 3.
- Produces: `LoggingConfigurator.CreateLogger(string logsDirectory) : Serilog.ILogger` — consumed by Task 8's composition root.

- [ ] **Step 1: Add the Serilog packages**

```powershell
dotnet add backend/src/VarthexComanda.Infrastructure package Serilog
dotnet add backend/src/VarthexComanda.Infrastructure package Serilog.Sinks.File
```

- [ ] **Step 2: Write the failing test**

`backend/tests/VarthexComanda.Infrastructure.Tests/LoggingConfiguratorTests.cs`:

```csharp
using VarthexComanda.Infrastructure.Logging;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests;

public class LoggingConfiguratorTests
{
    [Fact]
    public void CreateLogger_EscreveArquivoDeLogNaPastaIndicada()
    {
        var logsDir = Path.Combine(Path.GetTempPath(), "VarthexComandaTests_" + Guid.NewGuid());
        Directory.CreateDirectory(logsDir);
        try
        {
            using var logger = LoggingConfigurator.CreateLogger(logsDir);

            logger.Information("mensagem de teste {Marcador}", "abc123");
            (logger as IDisposable)?.Dispose();

            var arquivos = Directory.GetFiles(logsDir, "varthex-comanda-*.log");
            Assert.Single(arquivos);
            Assert.Contains("abc123", File.ReadAllText(arquivos[0]));
        }
        finally
        {
            Directory.Delete(logsDir, recursive: true);
        }
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

```powershell
dotnet test backend/tests/VarthexComanda.Infrastructure.Tests --filter LoggingConfiguratorTests
```

Expected: compilation error — `LoggingConfigurator` does not exist.

- [ ] **Step 4: Implement `LoggingConfigurator`**

`backend/src/VarthexComanda.Infrastructure/Logging/LoggingConfigurator.cs`:

```csharp
using Serilog;

namespace VarthexComanda.Infrastructure.Logging;

public static class LoggingConfigurator
{
    public static ILogger CreateLogger(string logsDirectory)
    {
        return new LoggerConfiguration()
            .WriteTo.File(
                path: Path.Combine(logsDirectory, "varthex-comanda-.log"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

```powershell
dotnet test backend/tests/VarthexComanda.Infrastructure.Tests --filter LoggingConfiguratorTests
```

Expected: 1 test passes (note: Serilog's file sink flushes on a background timer/dispose — the `Dispose()` call before asserting is what guarantees the file is flushed to disk).

- [ ] **Step 6: Commit**

```powershell
git add backend/src/VarthexComanda.Infrastructure/Logging backend/tests/VarthexComanda.Infrastructure.Tests/LoggingConfiguratorTests.cs
git commit -m "feat: adiciona logging com rotação diária via Serilog"
```

---

### Task 8: Desktop — composição, sequência segura de inicialização e janela vazia

**Files:**
- Modify: `backend/src/VarthexComanda.Desktop/App.xaml`
- Modify: `backend/src/VarthexComanda.Desktop/App.xaml.cs`
- Modify: `backend/src/VarthexComanda.Desktop/MainWindow.xaml`
- Modify: `backend/src/VarthexComanda.Desktop/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `AppPaths`, `DatabaseBackupService`, `SystemClock`, `VarthexComandaDbContext`, `DesignTimeDbContextFactory`-equivalent options building, `SingleInstanceGuard`, `LoggingConfigurator` — all from Tasks 3-7.
- Produces: a running WPF app; nothing later depends on this task.

- [ ] **Step 1: Add the DI and MVVM packages**

```powershell
dotnet add backend/src/VarthexComanda.Desktop package CommunityToolkit.Mvvm
dotnet add backend/src/VarthexComanda.Desktop package Microsoft.Extensions.DependencyInjection
```

- [ ] **Step 2: Implement the startup sequence in `App.xaml.cs`**

Note: the base class below is fully qualified as `System.Windows.Application`, not bare `Application`. Since Task 4 declared `namespace VarthexComanda.Application.Abstractions`, the `VarthexComanda.Application` segment is now a real namespace, and `VarthexComanda.Desktop` is its sibling under the shared `VarthexComanda` root — so an unqualified `Application` in this file resolves ambiguously (CS0118: "Application" is a namespace but is used as a type) even though the stock WPF template's default `App.xaml.cs` uses the bare name. Always write the fully-qualified base class here.

`backend/src/VarthexComanda.Desktop/App.xaml.cs`:

```csharp
using System.Windows;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Serilog;
using VarthexComanda.Application.Abstractions;
using VarthexComanda.Infrastructure.Concurrency;
using VarthexComanda.Infrastructure.Logging;
using VarthexComanda.Infrastructure.Persistence;
using VarthexComanda.Infrastructure.Storage;
using VarthexComanda.Infrastructure.Time;

namespace VarthexComanda.Desktop;

public partial class App : System.Windows.Application
{
    private SingleInstanceGuard? _guard;
    private ILogger? _logger;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _guard = new SingleInstanceGuard("VarthexComanda.SingleInstance");
        if (!_guard.TryAcquire())
        {
            MessageBox.Show(
                "O Varthex Comanda já está aberto neste computador.",
                "Varthex Comanda",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        var paths = new AppPaths();
        paths.EnsureCreated();

        _logger = LoggingConfigurator.CreateLogger(paths.LogsDirectory);
        _logger.Information("Iniciando Varthex Comanda");

        IClock clock = new SystemClock();

        try
        {
            DatabaseBackupService.BackupIfExists(paths.DatabasePath, paths.BackupsDirectory, clock.UtcNow);

            var connection = new SqliteConnection($"Data Source={paths.DatabasePath}");
            connection.Open();
            using (var pragma = connection.CreateCommand())
            {
                pragma.CommandText = "PRAGMA foreign_keys = ON;";
                pragma.ExecuteNonQuery();
            }

            var options = new DbContextOptionsBuilder<VarthexComandaDbContext>()
                .UseSqlite(connection)
                .Options;
            using var dbContext = new VarthexComandaDbContext(options);
            dbContext.Database.Migrate();

            var integridade = dbContext.Database
                .SqlQueryRaw<string>("PRAGMA integrity_check")
                .AsEnumerable()
                .Single();
            if (integridade != "ok")
            {
                _logger.Error("PRAGMA integrity_check retornou {Resultado}", integridade);
                MessageBox.Show(
                    "O banco de dados do Varthex Comanda está corrompido. Restaure um backup antes de continuar.",
                    "Varthex Comanda",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
                return;
            }

            _logger.Information("Banco pronto em {Caminho}", paths.DatabasePath);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Falha ao preparar o banco de dados");
            MessageBox.Show(
                "Não foi possível preparar o banco de dados do Varthex Comanda. Consulte os logs.",
                "Varthex Comanda",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
            return;
        }

        new MainWindow().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.Information("Encerrando Varthex Comanda");
        (_logger as IDisposable)?.Dispose();
        _guard?.Dispose();
        base.OnExit(e);
    }
}
```

- [ ] **Step 3: Set the window title**

`backend/src/VarthexComanda.Desktop/MainWindow.xaml` — set the root `Window` element's `Title` attribute:

```xml
Title="Varthex Comanda" Height="450" Width="800"
```

- [ ] **Step 4: Build in Release and verify manually**

```powershell
dotnet build backend/VarthexComanda.slnx --configuration Release
dotnet run --project backend/src/VarthexComanda.Desktop --configuration Release
```

Expected: an empty window titled "Varthex Comanda" opens; `%LOCALAPPDATA%\VarthexComanda\data\varthex-comanda.db`, `logs\varthex-comanda-<data>.log` and an empty `backups\` folder now exist. Close the window, then run the same command again in a second terminal *while the first is still open* — expected: a message box says the app is already open, and the second process exits without writing to the log or touching the database's migrations history further. This step is manual: WPF startup wiring is an integration of already-unit-tested pieces (Tasks 3-7), not something to re-test in isolation here.

- [ ] **Step 5: Commit**

```powershell
git add backend/src/VarthexComanda.Desktop
git commit -m "feat: liga composição raiz do Desktop (instância única, migração, logging)"
```

---

### Task 9: Verificação final e changelog

**Files:**
- Modify: `docs/CHANGELOG.md`

**Interfaces:**
- Consumes: nothing new — this task only verifies and documents Tasks 1-8.

- [ ] **Step 1: Full build and test pass in Release**

```powershell
dotnet build backend/VarthexComanda.slnx --configuration Release
dotnet test backend/VarthexComanda.slnx --configuration Release
```

Expected: build succeeds; all tests from Tasks 2-7 pass (Domain.Tests: 4, Infrastructure.Tests: 3 + 1 + 3 + 1 + 1 = 9, Application.Tests: 0 — still empty in this slice).

- [ ] **Step 2: Add a CHANGELOG entry**

Add to the top of `docs/CHANGELOG.md`, above the existing `## 1.3 - 2026-09-13` entry:

```markdown
## 1.4 - 2026-09-16

- criada a base técnica do código em `backend/` (Etapas 0 e 1 do guia de implementação);
- solução .NET 10 com Domain, Application, Infrastructure e Desktop (WPF);
- SQLite + EF Core mapeados a partir de `database/schema.sql`, com migração inicial;
- instância única via mutex, backup preventivo antes de migrar e logging rotativo com Serilog;
- ainda sem telas ou regras de negócio (RF01+) — entra na próxima fatia.

```

- [ ] **Step 3: Commit**

```powershell
git add docs/CHANGELOG.md
git commit -m "docs: registra a base técnica no changelog"
```

## Self-Review Notes

- **Spec coverage:** every bullet in [specs/2026-09-16-base-tecnica-design.md](2026-09-16-base-tecnica-design.md)'s "Escopo desta entrega" maps to a task — solution/projects → Task 1; entities → Task 2; `%LOCALAPPDATA%` paths → Task 3; EF Core + migration + `foreign_keys` → Task 5; Serilog → Task 7; single-instance mutex → Task 6; safe startup sequence → Task 8; xUnit criteria (empty-db migrate, repeat migrate, single-instance, integrity check) → Tasks 5 & 6 tests; Release build/test → Task 9.
- **Type consistency:** `AppPaths`, `DatabaseBackupService`, `IClock`/`SystemClock`, `VarthexComandaDbContext`, `SingleInstanceGuard`, `LoggingConfigurator` are named and typed identically everywhere they're produced (Tasks 3-7) and consumed (Task 8).
- **Out of scope confirmed:** no catalog/comanda/venda services, ViewModels or screens beyond the empty `MainWindow` — matches the spec's exclusions.
