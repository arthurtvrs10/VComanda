# Backup e Recuperação (Etapa 6) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement RF21-24 (backup automático, backup manual, validação de
backup, restauração) substituindo o `DatabaseBackupService` ingênuo atual por
um motor real baseado na API de backup do SQLite, com checksum, retenção,
validação de formato/versão/integridade, e uma tela mínima para disparar
backup manual e restauração.

**Architecture:** Um novo domínio `Backup` (namespace/pasta própria em todas
as camadas, não misturado com `Atendimento`/`Catalogo`). `IBackupService`
(Infrastructure) concentra toda a mecânica de arquivo/SQLite; os casos de uso
(Application) são orquestradores finos que nunca tocam arquivo ou SQLite
diretamente. `IBackupRegistroRepository` cuida só do bookkeeping na tabela
`backup_registro` (schema já existe desde a Etapa 0+1). Reinício após
restauração é um `Process.Start` + `Environment.Exit` real, reaproveitando a
checagem de integridade que `App.xaml.cs` já faz no startup.

**Tech Stack:** C#/.NET 10, WPF (CommunityToolkit.Mvvm), EF Core/SQLite
(`Microsoft.Data.Sqlite.SqliteConnection.BackupDatabase` para snapshot
nativo), `System.Security.Cryptography.SHA256` para checksum, xUnit.

**Spec:** [specs/2026-09-18-backup-design.md](../specs/2026-09-18-backup-design.md)

## Global Constraints

- Domínio novo `Backup`, separado de `Atendimento`/`Catalogo` em todas as
  camadas: `VarthexComanda.Application.Backup`
  (`backend/src/VarthexComanda.Application/Backup/`),
  `VarthexComanda.Infrastructure.Backup`
  (`backend/src/VarthexComanda.Infrastructure/Backup/`),
  `VarthexComanda.Desktop.Backup`
  (`backend/src/VarthexComanda.Desktop/Backup/`).
- Snapshot de banco usa `SqliteConnection.BackupDatabase` (API nativa do
  SQLite) — **nunca** `File.Copy` direto num banco que pode estar aberto por
  outra conexão.
- Cada backup grava um arquivo `.sha256` companheiro (hash SHA-256 em hex, só
  o hash, sem mais nada) ao lado do `.db` — o checksum viaja com o arquivo,
  não depende só do registro local em `backup_registro`.
- Retenção (30 cópias mais recentes) **só** se aplica à pasta gerenciada
  (`AppPaths.BackupsDirectory`) — nunca poda uma pasta externa escolhida pelo
  usuário para um backup manual.
- Ausência do `.sha256` companheiro durante validação é "não verificável"
  (`ChecksumConfere = null`), **não** é motivo de reprovação — formato +
  versão + `PRAGMA integrity_check` já são o sinal autoritativo de
  corrupção; o checksum é uma camada extra quando disponível.
- Backup automático (`CriarBackupAutomatico`) **nunca** lança exceção para
  fora — qualquer falha interna é registrada e engolida.
- Restauração nunca sobrescreve a base ativa antes de validar o arquivo
  candidato e restaurá-lo primeiro num caminho temporário — falha em
  qualquer etapa preserva a base ativa intocada.
- Reinício após restauração é um `Process.Start` real no próprio executável
  seguido de `Environment.Exit` — não `Application.Shutdown` (evitaria
  disparar `OnExit`'s backup incondicional logo após a restauração) e não uma
  reconstrução de DI/MainWindow na mesma execução (risco de handles SQLite
  presos do pool anterior no Windows).
- `BackupViewModel` e todos os casos de uso **não podem** ter nenhum
  `using System.Windows` nem referenciar `MessageBox`/`Window`/`Process`
  diretamente — essas ações ficam em `BackupView.xaml.cs` (mesmo padrão já
  estabelecido para `IConfirmador`/`EncerramentoView`).
- Reaproveitar `IConfirmador` (já existe) para a confirmação de restauração
  (RN19) — não criar um segundo mecanismo de confirmação.
- Prefixar todo comando `dotnet` com o PATH do SDK: PowerShell
  `$env:Path += ';C:\Program Files\dotnet'; ` / Bash
  `export PATH="$PATH:/c/Program Files/dotnet" && `.

---

## Task 1: `IBackupRegistroRepository` + `EfBackupRegistroRepository` (bookkeeping)

**Files:**
- Create: `backend/src/VarthexComanda.Application/Backup/IBackupRegistroRepository.cs`
- Create: `backend/src/VarthexComanda.Infrastructure/Backup/EfBackupRegistroRepository.cs`
- Create: `backend/tests/VarthexComanda.Application.Tests/Backup/FakeBackupRegistroRepository.cs`
- Test: `backend/tests/VarthexComanda.Infrastructure.Tests/Backup/EfBackupRegistroRepositoryTests.cs`

**Interfaces:**
- Consumes: `BackupRegistro`/`StatusBackup` (`VarthexComanda.Domain`, já existem), `contexto.BackupRegistros` (`DbSet<BackupRegistro>`, já existe em `VarthexComandaDbContext`).
- Produces: `IBackupRegistroRepository.Registrar(BackupRegistro)`, `.ListarRecentes(int) → IReadOnlyList<BackupRegistro>`, `.ExisteBackupHoje(DateTime inicioUtc, DateTime fimUtc) → bool` — consumidos pela Task 2 (`EfBackupService`) e Task 3 (casos de uso, via `FakeBackupRegistroRepository`).

- [ ] **Step 1: Criar a interface**

Criar `backend/src/VarthexComanda.Application/Backup/IBackupRegistroRepository.cs`:

```csharp
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Backup;

public interface IBackupRegistroRepository
{
    void Registrar(BackupRegistro registro);
    IReadOnlyList<BackupRegistro> ListarRecentes(int quantidade);
    bool ExisteBackupHoje(DateTime inicioUtc, DateTime fimUtc);
}
```

- [ ] **Step 2: Criar o fake para testes**

Criar `backend/tests/VarthexComanda.Application.Tests/Backup/FakeBackupRegistroRepository.cs`:

```csharp
using VarthexComanda.Application.Backup;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Tests.Backup;

public class FakeBackupRegistroRepository : IBackupRegistroRepository
{
    private readonly List<BackupRegistro> _registros = new();
    private int _proximoId = 1;

    public bool ExisteBackupHojeRetorno { get; set; }

    public void Registrar(BackupRegistro registro)
    {
        registro.Id = _proximoId++;
        _registros.Add(registro);
    }

    public IReadOnlyList<BackupRegistro> ListarRecentes(int quantidade) =>
        _registros.OrderByDescending(r => r.CriadoEm).Take(quantidade).ToList();

    public bool ExisteBackupHoje(DateTime inicioUtc, DateTime fimUtc) => ExisteBackupHojeRetorno;
}
```

Nota: `ExisteBackupHoje` no fake ignora os parâmetros e devolve um valor
configurável — é o suficiente para os testes das Tasks 3-4, que testam a
lógica de "backup automático" em cima de casos de uso, não a lógica de
intervalo de datas (essa já foi coberta pelo padrão equivalente na Etapa 5,
`FusoBrasilia`/`ListarVendasPorData`, e será testada de verdade contra SQLite
real no Step 4 abaixo).

- [ ] **Step 3: Escrever os testes de infraestrutura (vão falhar — `EfBackupRegistroRepository` não existe)**

Criar `backend/tests/VarthexComanda.Infrastructure.Tests/Backup/EfBackupRegistroRepositoryTests.cs`:

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Backup;
using VarthexComanda.Infrastructure.Persistence;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests.Backup;

public class EfBackupRegistroRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ServiceProvider _provedor;
    private readonly IDbContextFactory<VarthexComandaDbContext> _fabrica;

    public EfBackupRegistroRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"varthex-backup-tests-{Guid.NewGuid()}.db");

        var servicos = new ServiceCollection();
        servicos.AddDbContextFactory<VarthexComandaDbContext>(options =>
            options.UseSqlite($"Data Source={_dbPath};Foreign Keys=True"));
        _provedor = servicos.BuildServiceProvider();
        _fabrica = _provedor.GetRequiredService<IDbContextFactory<VarthexComandaDbContext>>();

        using var contexto = _fabrica.CreateDbContext();
        contexto.Database.Migrate();
    }

    public void Dispose()
    {
        _provedor.Dispose();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private static BackupRegistro CriarRegistro(DateTime criadoEmUtc, StatusBackup status = StatusBackup.Sucesso) => new()
    {
        Id = 0,
        Arquivo = $"varthex-comanda-{criadoEmUtc:yyyy-MM-dd-HHmmss}.db",
        Destino = "C:\\backups",
        CriadoEm = criadoEmUtc,
        Status = status,
        Checksum = "abc123",
        Mensagem = null
    };

    [Fact]
    public void Registrar_AtribuiIdEPersiste()
    {
        var repositorio = new EfBackupRegistroRepository(_fabrica);
        var registro = CriarRegistro(DateTime.UtcNow);

        repositorio.Registrar(registro);

        Assert.True(registro.Id > 0);
        Assert.Single(repositorio.ListarRecentes(10));
    }

    [Fact]
    public void ListarRecentes_OrdenaPorCriadoEmDescendente_ELimitaQuantidade()
    {
        var repositorio = new EfBackupRegistroRepository(_fabrica);
        repositorio.Registrar(CriarRegistro(new DateTime(2026, 9, 16, 8, 0, 0, DateTimeKind.Utc)));
        repositorio.Registrar(CriarRegistro(new DateTime(2026, 9, 18, 8, 0, 0, DateTimeKind.Utc)));
        repositorio.Registrar(CriarRegistro(new DateTime(2026, 9, 17, 8, 0, 0, DateTimeKind.Utc)));

        var recentes = repositorio.ListarRecentes(2);

        Assert.Equal(2, recentes.Count);
        Assert.Equal(new DateTime(2026, 9, 18, 8, 0, 0, DateTimeKind.Utc), recentes[0].CriadoEm);
        Assert.Equal(new DateTime(2026, 9, 17, 8, 0, 0, DateTimeKind.Utc), recentes[1].CriadoEm);
    }

    [Fact]
    public void ExisteBackupHoje_ComRegistroNoIntervalo_RetornaTrue()
    {
        var repositorio = new EfBackupRegistroRepository(_fabrica);
        repositorio.Registrar(CriarRegistro(new DateTime(2026, 9, 18, 14, 0, 0, DateTimeKind.Utc)));

        var existe = repositorio.ExisteBackupHoje(
            new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 19, 0, 0, 0, DateTimeKind.Utc));

        Assert.True(existe);
    }

    [Fact]
    public void ExisteBackupHoje_SemRegistroNoIntervalo_RetornaFalse()
    {
        var repositorio = new EfBackupRegistroRepository(_fabrica);
        repositorio.Registrar(CriarRegistro(new DateTime(2026, 9, 17, 14, 0, 0, DateTimeKind.Utc)));

        var existe = repositorio.ExisteBackupHoje(
            new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 19, 0, 0, 0, DateTimeKind.Utc));

        Assert.False(existe);
    }

    [Fact]
    public void ExisteBackupHoje_ConsideraSomenteRegistrosDeSucesso()
    {
        var repositorio = new EfBackupRegistroRepository(_fabrica);
        repositorio.Registrar(CriarRegistro(new DateTime(2026, 9, 18, 14, 0, 0, DateTimeKind.Utc), StatusBackup.Falha));

        var existe = repositorio.ExisteBackupHoje(
            new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 19, 0, 0, 0, DateTimeKind.Utc));

        Assert.False(existe);
    }
}
```

Nota sobre o último teste: um backup que **falhou** não deve contar como "já
fizemos o backup de hoje" — senão uma falha silenciosa na primeira tentativa
do dia impediria qualquer nova tentativa até o dia seguinte. `ExisteBackupHoje`
deve filtrar por `Status == StatusBackup.Sucesso`.

- [ ] **Step 4: Rodar os testes e confirmar que falham (erro de compilação)**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet build backend/VarthexComanda.slnx
```

Esperado: falha de build — `EfBackupRegistroRepository` não existe.

- [ ] **Step 5: Implementar `EfBackupRegistroRepository`**

Criar `backend/src/VarthexComanda.Infrastructure/Backup/EfBackupRegistroRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using VarthexComanda.Application.Backup;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Persistence;

namespace VarthexComanda.Infrastructure.Backup;

public class EfBackupRegistroRepository : IBackupRegistroRepository
{
    private readonly IDbContextFactory<VarthexComandaDbContext> _fabricaContexto;

    public EfBackupRegistroRepository(IDbContextFactory<VarthexComandaDbContext> fabricaContexto)
    {
        _fabricaContexto = fabricaContexto;
    }

    public void Registrar(BackupRegistro registro)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        registro.Id = 0;
        contexto.BackupRegistros.Add(registro);
        contexto.SaveChanges();
    }

    public IReadOnlyList<BackupRegistro> ListarRecentes(int quantidade)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        return contexto.BackupRegistros
            .OrderByDescending(b => b.CriadoEm)
            .Take(quantidade)
            .ToList();
    }

    public bool ExisteBackupHoje(DateTime inicioUtc, DateTime fimUtc)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        return contexto.BackupRegistros.Any(b =>
            b.Status == StatusBackup.Sucesso &&
            b.CriadoEm >= inicioUtc &&
            b.CriadoEm < fimUtc);
    }
}
```

- [ ] **Step 6: Rodar os testes e confirmar que passam**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release --filter "FullyQualifiedName~EfBackupRegistroRepositoryTests"
```

Esperado: PASS nos 5 testes.

- [ ] **Step 7: Rodar a suíte inteira e commitar**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release
```

Esperado: todos os projetos PASS (nada ainda consome `IBackupRegistroRepository`
fora dos testes).

```bash
git add backend/src/VarthexComanda.Application/Backup/IBackupRegistroRepository.cs backend/src/VarthexComanda.Infrastructure/Backup/EfBackupRegistroRepository.cs backend/tests/VarthexComanda.Application.Tests/Backup/FakeBackupRegistroRepository.cs backend/tests/VarthexComanda.Infrastructure.Tests/Backup/EfBackupRegistroRepositoryTests.cs
git commit -m "feat: adiciona repositorio de registros de backup"
```

---

## Task 2: `IBackupService` — criação de backup (snapshot, checksum, retenção)

**Files:**
- Create: `backend/src/VarthexComanda.Application/Backup/IBackupService.cs`
- Create: `backend/src/VarthexComanda.Application/Backup/RelatorioValidacao.cs`
- Create: `backend/src/VarthexComanda.Infrastructure/Backup/EfBackupService.cs`
- Test: `backend/tests/VarthexComanda.Infrastructure.Tests/Backup/EfBackupServiceTests.cs`

**Interfaces:**
- Consumes: `AppPaths` (`VarthexComanda.Infrastructure.Storage`, já existe — `DatabasePath`/`BackupsDirectory`), `IClock.UtcNow` (já existe), `IBackupRegistroRepository.Registrar` (Task 1), `Resultado<T>` (`VarthexComanda.Application.Catalogo`, já existe), `Microsoft.Data.Sqlite.SqliteConnection.BackupDatabase` (API nativa do pacote `Microsoft.EntityFrameworkCore.Sqlite`, já referenciado — não precisa adicionar pacote novo).
- Produces: `IBackupService.CriarBackupGerenciado() → Resultado<BackupRegistro>`, `.CriarBackupExterno(string pastaExterna) → Resultado<BackupRegistro>` (o resto de `IBackupService` — `Validar`/`RestaurarPara` — é implementado na Task 3, na mesma classe) — consumidos pela Task 4 (casos de uso).

Este método `IBackupService` só declara os dois métodos de criação nesta
task; `Validar`/`RestaurarPara` são adicionados à mesma interface e à mesma
classe na Task 3 (a interface completa só fica pronta ao final da Task 3 —
até lá, `EfBackupService` é uma classe concreta que implementa parcialmente,
sem `: IBackupService` ainda, seguindo o mesmo padrão já usado em fatias
anteriores para dividir uma implementação grande em duas tasks).

- [ ] **Step 1: Criar `RelatorioValidacao` (tipo usado pela assinatura completa da interface, mesmo que só implementado na Task 3)**

Criar `backend/src/VarthexComanda.Application/Backup/RelatorioValidacao.cs`:

```csharp
namespace VarthexComanda.Application.Backup;

public class RelatorioValidacao
{
    public required bool FormatoValido { get; init; }
    public required bool VersaoCompativel { get; init; }
    public required bool IntegridadeOk { get; init; }
    public bool? ChecksumConfere { get; init; }
    public required string Motivo { get; init; }

    public bool Aprovado => FormatoValido && VersaoCompativel && IntegridadeOk;
}
```

- [ ] **Step 2: Criar a interface completa `IBackupService`**

Criar `backend/src/VarthexComanda.Application/Backup/IBackupService.cs`:

```csharp
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Backup;

public interface IBackupService
{
    Resultado<BackupRegistro> CriarBackupGerenciado();
    Resultado<BackupRegistro> CriarBackupExterno(string pastaExterna);
    RelatorioValidacao Validar(string caminhoArquivo);
    Resultado<BackupRegistro> RestaurarPara(string caminhoArquivo);
}
```

`Resultado<T>` está em `VarthexComanda.Application.Catalogo` — adicionar
`using VarthexComanda.Application.Catalogo;` no topo do arquivo.

A interface já nasce completa (os 4 métodos), mas `Validar`/`RestaurarPara`
lançam `NotImplementedException` na implementação desta task — a Task 3
substitui os dois pelo código real. Isso é necessário porque C# não permite
implementar uma interface parcialmente com `: IBackupService`; a alternativa
(a classe só ganhar `: IBackupService` na Task 3) reproduziria exatamente o
padrão já usado com sucesso em fatias anteriores (`EfComandaRepository` nas
Etapas 3-4), mas como aqui só há uma classe (não duas tasks tocando arquivos
diferentes), é mais simples a interface nascer completa e a implementação
"crescer" dentro da mesma classe.

- [ ] **Step 3: Escrever os testes de criação de backup (vão falhar — `EfBackupService` não existe)**

Criar `backend/tests/VarthexComanda.Infrastructure.Tests/Backup/EfBackupServiceTests.cs`:

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VarthexComanda.Application.Backup;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Backup;
using VarthexComanda.Infrastructure.Persistence;
using VarthexComanda.Infrastructure.Storage;
using VarthexComanda.Infrastructure.Time;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests.Backup;

public class EfBackupServiceTests : IDisposable
{
    private readonly string _raizTeste;
    private readonly AppPaths _paths;
    private readonly ServiceProvider _provedor;
    private readonly IDbContextFactory<VarthexComandaDbContext> _fabrica;
    private readonly FakeBackupRegistroRepositoryDeIntegracao _registros;

    public EfBackupServiceTests()
    {
        _raizTeste = Path.Combine(Path.GetTempPath(), $"varthex-backupservice-tests-{Guid.NewGuid()}");
        _paths = new AppPaths(_raizTeste);
        _paths.EnsureCreated();

        var servicos = new ServiceCollection();
        servicos.AddDbContextFactory<VarthexComandaDbContext>(options =>
            options.UseSqlite($"Data Source={_paths.DatabasePath};Foreign Keys=True"));
        _provedor = servicos.BuildServiceProvider();
        _fabrica = _provedor.GetRequiredService<IDbContextFactory<VarthexComandaDbContext>>();

        using (var contexto = _fabrica.CreateDbContext())
        {
            contexto.Database.Migrate();
        }

        _registros = new FakeBackupRegistroRepositoryDeIntegracao();
    }

    public void Dispose()
    {
        _provedor.Dispose();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_raizTeste)) Directory.Delete(_raizTeste, recursive: true);
    }

    [Fact]
    public void CriarBackupGerenciado_CriaArquivoDbEChecksumECompanheiro()
    {
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, _registros, relogio);

        var resultado = servico.CriarBackupGerenciado();

        Assert.True(resultado.Sucesso);
        var caminhoDb = Path.Combine(resultado.Valor!.Destino, resultado.Valor.Arquivo);
        Assert.True(File.Exists(caminhoDb));
        Assert.True(File.Exists(caminhoDb + ".sha256"));
        Assert.Equal(resultado.Valor.Checksum, File.ReadAllText(caminhoDb + ".sha256").Trim());
    }

    [Fact]
    public void CriarBackupGerenciado_RegistraSucessoNoRepositorio()
    {
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, _registros, relogio);

        servico.CriarBackupGerenciado();

        var registro = Assert.Single(_registros.ListarRecentes(10));
        Assert.Equal(StatusBackup.Sucesso, registro.Status);
        Assert.NotNull(registro.Checksum);
    }

    [Fact]
    public void CriarBackupGerenciado_BackupPassaNaVerificacaoDeIntegridade()
    {
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, _registros, relogio);

        var resultado = servico.CriarBackupGerenciado();

        var caminhoDb = Path.Combine(resultado.Valor!.Destino, resultado.Valor.Arquivo);
        using var conexao = new SqliteConnection($"Data Source={caminhoDb}");
        conexao.Open();
        using var comando = conexao.CreateCommand();
        comando.CommandText = "PRAGMA integrity_check";
        Assert.Equal("ok", (string?)comando.ExecuteScalar());
    }

    [Fact]
    public void CriarBackupGerenciado_MaisDeTresBackups_RetencaoMantemSomenteOsTresMaisRecentes()
    {
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, _registros, relogio, retencaoMaxima: 3);

        for (var i = 0; i < 5; i++)
        {
            relogio.UtcNow = relogio.UtcNow.AddSeconds(1);
            servico.CriarBackupGerenciado();
        }

        var arquivos = Directory.GetFiles(_paths.BackupsDirectory, "varthex-comanda-*.db");
        Assert.Equal(3, arquivos.Length);
    }

    [Fact]
    public void CriarBackupExterno_NaoAplicaRetencaoNaPastaExterna()
    {
        var pastaExterna = Path.Combine(_raizTeste, "externa");
        Directory.CreateDirectory(pastaExterna);
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, _registros, relogio, retencaoMaxima: 3);

        for (var i = 0; i < 5; i++)
        {
            relogio.UtcNow = relogio.UtcNow.AddSeconds(1);
            servico.CriarBackupExterno(pastaExterna);
        }

        var arquivos = Directory.GetFiles(pastaExterna, "varthex-comanda-*.db");
        Assert.Equal(5, arquivos.Length);
    }

    private class FakeClockDeIntegracao : VarthexComanda.Application.Abstractions.IClock
    {
        public DateTime UtcNow { get; set; } = new DateTime(2026, 9, 18, 12, 0, 0, DateTimeKind.Utc);
    }

    private class FakeBackupRegistroRepositoryDeIntegracao : IBackupRegistroRepository
    {
        private readonly List<BackupRegistro> _registros = new();
        private int _proximoId = 1;

        public void Registrar(BackupRegistro registro)
        {
            registro.Id = _proximoId++;
            _registros.Add(registro);
        }

        public IReadOnlyList<BackupRegistro> ListarRecentes(int quantidade) =>
            _registros.OrderByDescending(r => r.CriadoEm).Take(quantidade).ToList();

        public bool ExisteBackupHoje(DateTime inicioUtc, DateTime fimUtc) =>
            _registros.Any(r => r.Status == StatusBackup.Sucesso && r.CriadoEm >= inicioUtc && r.CriadoEm < fimUtc);
    }
}
```

Nota: este teste usa fakes locais (`FakeClockDeIntegracao`/
`FakeBackupRegistroRepositoryDeIntegracao`) em vez dos fakes de
`Application.Tests` porque `Infrastructure.Tests` não referencia
`Application.Tests` (só `Desktop.Tests` faz essa referência cruzada,
documentada como exceção nas Etapas 3-4) — duplicar um fake pequeno aqui é
mais simples que adicionar uma referência de projeto nova só para isso.

- [ ] **Step 4: Rodar os testes e confirmar que falham (erro de compilação)**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet build backend/VarthexComanda.slnx
```

Esperado: falha de build — `EfBackupService` não existe.

- [ ] **Step 5: Implementar `EfBackupService` (métodos de criação; `Validar`/`RestaurarPara` ainda como stub)**

Criar `backend/src/VarthexComanda.Infrastructure/Backup/EfBackupService.cs`:

```csharp
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using VarthexComanda.Application.Abstractions;
using VarthexComanda.Application.Backup;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Storage;

namespace VarthexComanda.Infrastructure.Backup;

public class EfBackupService : IBackupService
{
    private readonly AppPaths _paths;
    private readonly IBackupRegistroRepository _registros;
    private readonly IClock _relogio;
    private readonly int _retencaoMaxima;

    public EfBackupService(AppPaths paths, IBackupRegistroRepository registros, IClock relogio, int retencaoMaxima = 30)
    {
        _paths = paths;
        _registros = registros;
        _relogio = relogio;
        _retencaoMaxima = retencaoMaxima;
    }

    public Resultado<BackupRegistro> CriarBackupGerenciado() =>
        CriarBackupInterno(_paths.BackupsDirectory, aplicarRetencao: true);

    public Resultado<BackupRegistro> CriarBackupExterno(string pastaExterna) =>
        CriarBackupInterno(pastaExterna, aplicarRetencao: false);

    private Resultado<BackupRegistro> CriarBackupInterno(string pastaDestino, bool aplicarRetencao)
    {
        var agora = _relogio.UtcNow;
        var nomeArquivo = $"varthex-comanda-{agora:yyyy-MM-dd-HHmmss}.db";
        var destinoFinal = Path.Combine(pastaDestino, nomeArquivo);
        var destinoTemporario = destinoFinal + ".tmp";

        try
        {
            Directory.CreateDirectory(pastaDestino);

            using (var origem = new SqliteConnection($"Data Source={_paths.DatabasePath}"))
            using (var destino = new SqliteConnection($"Data Source={destinoTemporario}"))
            {
                origem.Open();
                destino.Open();
                origem.BackupDatabase(destino);
            }

            if (!VerificarIntegridade(destinoTemporario))
            {
                File.Delete(destinoTemporario);
                var registroFalha = new BackupRegistro
                {
                    Id = 0,
                    Arquivo = nomeArquivo,
                    Destino = pastaDestino,
                    CriadoEm = agora,
                    Status = StatusBackup.Falha,
                    Checksum = null,
                    Mensagem = "Falha na verificação de integridade do backup."
                };
                _registros.Registrar(registroFalha);
                return Resultado<BackupRegistro>.Falha(registroFalha.Mensagem!);
            }

            var checksum = CalcularChecksumSha256(destinoTemporario);
            File.Move(destinoTemporario, destinoFinal);
            File.WriteAllText(destinoFinal + ".sha256", checksum);

            var registro = new BackupRegistro
            {
                Id = 0,
                Arquivo = nomeArquivo,
                Destino = pastaDestino,
                CriadoEm = agora,
                Status = StatusBackup.Sucesso,
                Checksum = checksum,
                Mensagem = null
            };
            _registros.Registrar(registro);

            if (aplicarRetencao)
            {
                AplicarRetencao(pastaDestino);
            }

            return Resultado<BackupRegistro>.Ok(registro);
        }
        catch (Exception ex)
        {
            if (File.Exists(destinoTemporario))
            {
                File.Delete(destinoTemporario);
            }

            var registroFalha = new BackupRegistro
            {
                Id = 0,
                Arquivo = nomeArquivo,
                Destino = pastaDestino,
                CriadoEm = agora,
                Status = StatusBackup.Falha,
                Checksum = null,
                Mensagem = ex.Message
            };
            try { _registros.Registrar(registroFalha); } catch { /* nao mascarar a falha original */ }
            return Resultado<BackupRegistro>.Falha(ex.Message);
        }
    }

    private static bool VerificarIntegridade(string caminhoArquivo)
    {
        using var conexao = new SqliteConnection($"Data Source={caminhoArquivo}");
        conexao.Open();
        using var comando = conexao.CreateCommand();
        comando.CommandText = "PRAGMA integrity_check";
        return (string?)comando.ExecuteScalar() == "ok";
    }

    private void AplicarRetencao(string pasta)
    {
        var arquivos = Directory.GetFiles(pasta, "varthex-comanda-*.db")
            .OrderByDescending(f => f)
            .Skip(_retencaoMaxima)
            .ToList();

        foreach (var arquivo in arquivos)
        {
            File.Delete(arquivo);
            var companheiro = arquivo + ".sha256";
            if (File.Exists(companheiro))
            {
                File.Delete(companheiro);
            }
        }
    }

    private static string CalcularChecksumSha256(string caminhoArquivo)
    {
        using var stream = File.OpenRead(caminhoArquivo);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public RelatorioValidacao Validar(string caminhoArquivo) => throw new NotImplementedException("Implementado na Task 3.");

    public Resultado<BackupRegistro> RestaurarPara(string caminhoArquivo) => throw new NotImplementedException("Implementado na Task 3.");
}
```

- [ ] **Step 6: Rodar os testes e confirmar que passam**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release --filter "FullyQualifiedName~EfBackupServiceTests"
```

Esperado: PASS nos 5 testes.

- [ ] **Step 7: Rodar a suíte inteira e commitar**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release
```

```bash
git add backend/src/VarthexComanda.Application/Backup/IBackupService.cs backend/src/VarthexComanda.Application/Backup/RelatorioValidacao.cs backend/src/VarthexComanda.Infrastructure/Backup/EfBackupService.cs backend/tests/VarthexComanda.Infrastructure.Tests/Backup/EfBackupServiceTests.cs
git commit -m "feat: adiciona criacao de backup (snapshot, checksum, retencao)"
```

---

## Task 3: `IBackupService` — validação e restauração

**Files:**
- Modify: `backend/src/VarthexComanda.Infrastructure/Backup/EfBackupService.cs`
- Test: `backend/tests/VarthexComanda.Infrastructure.Tests/Backup/EfBackupServiceTests.cs`

**Interfaces:**
- Consumes: `CriarBackupGerenciado()` (Task 2, usado internamente por `RestaurarPara` para a cópia preventiva), `Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<VarthexComandaDbContext>` + `Database.GetMigrations()`/`GetAppliedMigrations()` (para checagem de versão contra um arquivo arbitrário, não o banco ativo configurado por DI).
- Produces: `IBackupService.Validar(string) → RelatorioValidacao`, `.RestaurarPara(string) → Resultado<BackupRegistro>` — consumidos pela Task 4 (`ValidarBackup`/`RestaurarBackup`).

- [ ] **Step 1: Escrever os testes de validação e restauração (vão falhar — os métodos ainda lançam `NotImplementedException`)**

Adicionar a `backend/tests/VarthexComanda.Infrastructure.Tests/Backup/EfBackupServiceTests.cs`, antes da última chave de fechamento da classe `EfBackupServiceTests` (depois do último `[Fact]` já existente):

```csharp
    [Fact]
    public void Validar_ArquivoValido_TodasAsChecagensPassam()
    {
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, _registros, relogio);
        var resultado = servico.CriarBackupGerenciado();
        var caminho = Path.Combine(resultado.Valor!.Destino, resultado.Valor.Arquivo);

        var relatorio = servico.Validar(caminho);

        Assert.True(relatorio.FormatoValido);
        Assert.True(relatorio.VersaoCompativel);
        Assert.True(relatorio.IntegridadeOk);
        Assert.True(relatorio.ChecksumConfere);
        Assert.True(relatorio.Aprovado);
    }

    [Fact]
    public void Validar_ArquivoNaoSqlite_FalhaNoFormato()
    {
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, _registros, relogio);
        var caminhoInvalido = Path.Combine(_raizTeste, "nao-e-um-banco.db");
        File.WriteAllText(caminhoInvalido, "isto nao e um banco sqlite");

        var relatorio = servico.Validar(caminhoInvalido);

        Assert.False(relatorio.FormatoValido);
        Assert.False(relatorio.Aprovado);
        Assert.NotEmpty(relatorio.Motivo);
    }

    [Fact]
    public void Validar_ArquivoInexistente_FalhaNoFormato()
    {
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, _registros, relogio);

        var relatorio = servico.Validar(Path.Combine(_raizTeste, "nao-existe.db"));

        Assert.False(relatorio.FormatoValido);
        Assert.False(relatorio.Aprovado);
    }

    [Fact]
    public void Validar_MigracaoFuturaDesconhecida_FalhaNaVersao()
    {
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, _registros, relogio);
        var resultado = servico.CriarBackupGerenciado();
        var caminho = Path.Combine(resultado.Valor!.Destino, resultado.Valor.Arquivo);

        using (var conexao = new SqliteConnection($"Data Source={caminho}"))
        {
            conexao.Open();
            using var comando = conexao.CreateCommand();
            comando.CommandText =
                "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('99999999999999_MigracaoFutura', '99.0.0')";
            comando.ExecuteNonQuery();
        }

        var relatorio = servico.Validar(caminho);

        Assert.True(relatorio.FormatoValido);
        Assert.False(relatorio.VersaoCompativel);
        Assert.False(relatorio.Aprovado);
    }

    [Fact]
    public void Validar_ArquivoCorrompido_FalhaNaIntegridade()
    {
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, _registros, relogio);
        var resultado = servico.CriarBackupGerenciado();
        var caminho = Path.Combine(resultado.Valor!.Destino, resultado.Valor.Arquivo);

        SqliteConnection.ClearAllPools();
        using (var stream = new FileStream(caminho, FileMode.Open, FileAccess.Write))
        {
            stream.Seek(100, SeekOrigin.Begin);
            var lixo = new byte[200];
            new Random(42).NextBytes(lixo);
            stream.Write(lixo, 0, lixo.Length);
        }

        var relatorio = servico.Validar(caminho);

        Assert.True(relatorio.FormatoValido);
        Assert.True(relatorio.VersaoCompativel);
        Assert.False(relatorio.IntegridadeOk);
        Assert.False(relatorio.Aprovado);
    }

    [Fact]
    public void Validar_SemArquivoSha256Companheiro_ChecksumNaoVerificavelMasAprovado()
    {
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, _registros, relogio);
        var resultado = servico.CriarBackupGerenciado();
        var caminho = Path.Combine(resultado.Valor!.Destino, resultado.Valor.Arquivo);
        File.Delete(caminho + ".sha256");

        var relatorio = servico.Validar(caminho);

        Assert.Null(relatorio.ChecksumConfere);
        Assert.True(relatorio.Aprovado);
    }

    [Fact]
    public void RestaurarPara_ArquivoValido_CriaCopiaPreventivaETrocaABaseAtiva()
    {
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, _registros, relogio);

        using (var contexto = _fabrica.CreateDbContext())
        {
            contexto.Categorias.Add(new Categoria { Id = 0, Nome = "Original", Ativo = true, CriadoEm = relogio.UtcNow, AtualizadoEm = relogio.UtcNow });
            contexto.SaveChanges();
        }
        var backupComOriginal = servico.CriarBackupGerenciado();
        var caminhoBackupOriginal = Path.Combine(backupComOriginal.Valor!.Destino, backupComOriginal.Valor.Arquivo);

        using (var contexto = _fabrica.CreateDbContext())
        {
            contexto.Categorias.Add(new Categoria { Id = 0, Nome = "Nova", Ativo = true, CriadoEm = relogio.UtcNow, AtualizadoEm = relogio.UtcNow });
            contexto.SaveChanges();
        }

        var resultado = servico.RestaurarPara(caminhoBackupOriginal);

        Assert.True(resultado.Sucesso);
        SqliteConnection.ClearAllPools();
        using (var contextoPosRestauracao = _fabrica.CreateDbContext())
        {
            var nomes = contextoPosRestauracao.Categorias.Select(c => c.Nome).ToList();
            Assert.Contains("Original", nomes);
            Assert.DoesNotContain("Nova", nomes);
        }
    }

    [Fact]
    public void RestaurarPara_ArquivoInvalido_NaoAlteraABaseAtiva()
    {
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, _registros, relogio);
        using (var contexto = _fabrica.CreateDbContext())
        {
            contexto.Categorias.Add(new Categoria { Id = 0, Nome = "Preservada", Ativo = true, CriadoEm = relogio.UtcNow, AtualizadoEm = relogio.UtcNow });
            contexto.SaveChanges();
        }
        var caminhoInvalido = Path.Combine(_raizTeste, "invalido.db");
        File.WriteAllText(caminhoInvalido, "nao e um banco");

        var resultado = servico.RestaurarPara(caminhoInvalido);

        Assert.False(resultado.Sucesso);
        SqliteConnection.ClearAllPools();
        using (var contexto = _fabrica.CreateDbContext())
        {
            Assert.Contains("Preservada", contexto.Categorias.Select(c => c.Nome).ToList());
        }
    }
```

Adicionar também os `using` que faltarem no topo do arquivo de teste:
`using VarthexComanda.Domain;` já deve estar presente (usado nos testes de
criação); confirmar que está lá antes de adicionar os novos testes.

- [ ] **Step 2: Rodar os testes e confirmar que falham**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release --filter "FullyQualifiedName~EfBackupServiceTests"
```

Esperado: os testes novos falham com `NotImplementedException` (o stub da
Task 2).

- [ ] **Step 3: Implementar `Validar` e `RestaurarPara`, substituindo os dois stubs no final de `EfBackupService.cs`**

Substituir:

```csharp
    public RelatorioValidacao Validar(string caminhoArquivo) => throw new NotImplementedException("Implementado na Task 3.");

    public Resultado<BackupRegistro> RestaurarPara(string caminhoArquivo) => throw new NotImplementedException("Implementado na Task 3.");
```

por:

```csharp
    public RelatorioValidacao Validar(string caminhoArquivo)
    {
        if (!File.Exists(caminhoArquivo) || !caminhoArquivo.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
        {
            return new RelatorioValidacao
            {
                FormatoValido = false,
                VersaoCompativel = false,
                IntegridadeOk = false,
                ChecksumConfere = null,
                Motivo = "Arquivo não encontrado ou não é um banco .db."
            };
        }

        List<string> aplicadas;
        List<string> conhecidas;
        try
        {
            var opcoes = new DbContextOptionsBuilder<VarthexComandaDbContext>()
                .UseSqlite($"Data Source={caminhoArquivo}")
                .Options;
            using var contexto = new VarthexComandaDbContext(opcoes);
            aplicadas = contexto.Database.GetAppliedMigrations().ToList();
            conhecidas = contexto.Database.GetMigrations().ToList();
        }
        catch (Exception)
        {
            return new RelatorioValidacao
            {
                FormatoValido = false,
                VersaoCompativel = false,
                IntegridadeOk = false,
                ChecksumConfere = null,
                Motivo = "Arquivo não é um banco de dados SQLite válido."
            };
        }

        var ultimaAplicada = aplicadas.LastOrDefault();
        var versaoCompativel = ultimaAplicada is null || conhecidas.Contains(ultimaAplicada);
        if (!versaoCompativel)
        {
            return new RelatorioValidacao
            {
                FormatoValido = true,
                VersaoCompativel = false,
                IntegridadeOk = false,
                ChecksumConfere = null,
                Motivo = "Backup de uma versão incompatível do Varthex Comanda."
            };
        }

        if (!VerificarIntegridade(caminhoArquivo))
        {
            return new RelatorioValidacao
            {
                FormatoValido = true,
                VersaoCompativel = true,
                IntegridadeOk = false,
                ChecksumConfere = null,
                Motivo = "Arquivo de backup está corrompido (falhou na verificação de integridade)."
            };
        }

        bool? checksumConfere = null;
        var companheiro = caminhoArquivo + ".sha256";
        if (File.Exists(companheiro))
        {
            var esperado = File.ReadAllText(companheiro).Trim();
            var calculado = CalcularChecksumSha256(caminhoArquivo);
            checksumConfere = string.Equals(esperado, calculado, StringComparison.OrdinalIgnoreCase);
        }

        return new RelatorioValidacao
        {
            FormatoValido = true,
            VersaoCompativel = true,
            IntegridadeOk = true,
            ChecksumConfere = checksumConfere,
            Motivo = string.Empty
        };
    }

    public Resultado<BackupRegistro> RestaurarPara(string caminhoArquivo)
    {
        try
        {
            var preventivo = CriarBackupGerenciado();
            if (!preventivo.Sucesso)
            {
                return Resultado<BackupRegistro>.Falha("Não foi possível criar a cópia preventiva; restauração cancelada.");
            }

            var temporario = _paths.DatabasePath + ".restaurando";
            using (var origem = new SqliteConnection($"Data Source={caminhoArquivo}"))
            using (var destino = new SqliteConnection($"Data Source={temporario}"))
            {
                origem.Open();
                destino.Open();
                origem.BackupDatabase(destino);
            }

            if (!VerificarIntegridade(temporario))
            {
                File.Delete(temporario);
                return Resultado<BackupRegistro>.Falha("A restauração falhou na verificação de integridade; a base ativa não foi alterada.");
            }

            SqliteConnection.ClearAllPools();
            File.Copy(temporario, _paths.DatabasePath, overwrite: true);
            File.Delete(temporario);

            return preventivo;
        }
        catch (Exception ex)
        {
            return Resultado<BackupRegistro>.Falha(ex.Message);
        }
    }
```

Adicionar `using Microsoft.EntityFrameworkCore;` e
`using VarthexComanda.Infrastructure.Persistence;` no topo de
`EfBackupService.cs` (para `DbContextOptionsBuilder`/`VarthexComandaDbContext`
— este último já deveria estar acessível via o mesmo namespace do projeto,
confirmar se precisa do `using` explícito ao compilar).

Nota sobre `RestaurarPara`: a cópia preventiva é criada **antes** de
qualquer tentativa de restaurar o arquivo candidato — se a cópia preventiva
falhar, a restauração é cancelada e nada mais acontece. Isso é
deliberadamente mais conservador que só validar o candidato primeiro,
porque garante que exista uma cópia de segurança recente mesmo se o
candidato acabar sendo rejeitado depois por falha de integridade no arquivo
temporário restaurado.

- [ ] **Step 4: Rodar os testes e confirmar que passam**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release --filter "FullyQualifiedName~EfBackupServiceTests"
```

Esperado: PASS em todos os testes (5 da Task 2 + 8 novos = 13).

- [ ] **Step 5: Rodar a suíte inteira e commitar**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release
```

```bash
git add backend/src/VarthexComanda.Infrastructure/Backup/EfBackupService.cs backend/tests/VarthexComanda.Infrastructure.Tests/Backup/EfBackupServiceTests.cs
git commit -m "feat: adiciona validacao e restauracao de backup"
```

---

## Task 4: Casos de uso (Application) — `CriarBackupAutomatico`, `CriarBackupManual`, `ValidarBackup`, `RestaurarBackup`, `ListarBackupsRecentes`

**Files:**
- Create: `backend/src/VarthexComanda.Application/Backup/CriarBackupAutomatico.cs`
- Create: `backend/src/VarthexComanda.Application/Backup/CriarBackupManual.cs`
- Create: `backend/src/VarthexComanda.Application/Backup/ValidarBackup.cs`
- Create: `backend/src/VarthexComanda.Application/Backup/RestaurarBackup.cs`
- Create: `backend/src/VarthexComanda.Application/Backup/ListarBackupsRecentes.cs`
- Create: `backend/tests/VarthexComanda.Application.Tests/Backup/FakeBackupService.cs`
- Test: `backend/tests/VarthexComanda.Application.Tests/Backup/CriarBackupAutomaticoTests.cs`
- Test: `backend/tests/VarthexComanda.Application.Tests/Backup/CriarBackupManualTests.cs`
- Test: `backend/tests/VarthexComanda.Application.Tests/Backup/ValidarBackupTests.cs`
- Test: `backend/tests/VarthexComanda.Application.Tests/Backup/RestaurarBackupTests.cs`

**Interfaces:**
- Consumes: `IBackupService` (Tasks 2-3), `IBackupRegistroRepository` (Task 1), `IClock` (já existe), `FusoBrasilia` (`VarthexComanda.Application.Atendimento`, já existe — reaproveitado para calcular o intervalo UTC de "hoje").
- Produces: `CriarBackupAutomatico.Executar(bool incondicional = false)`, `CriarBackupManual.Executar(string? pastaExterna) → Resultado<BackupRegistro>`, `ValidarBackup.Executar(string) → RelatorioValidacao`, `RestaurarBackup.Executar(string) → Resultado<BackupRegistro>`, `ListarBackupsRecentes.Executar(int quantidade = 20) → IReadOnlyList<BackupRegistro>` — todos consumidos pela Task 5 (`BackupViewModel`).

- [ ] **Step 1: Criar o fake de `IBackupService` para os testes**

Criar `backend/tests/VarthexComanda.Application.Tests/Backup/FakeBackupService.cs`:

```csharp
using VarthexComanda.Application.Backup;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Tests.Backup;

public class FakeBackupService : IBackupService
{
    public int ChamadasCriarBackupGerenciado { get; private set; }
    public int ChamadasCriarBackupExterno { get; private set; }
    public bool LancarExcecaoAoCriar { get; set; }
    public bool ProximaCriacaoFalha { get; set; }
    public RelatorioValidacao ProximoRelatorio { get; set; } = new()
    {
        FormatoValido = true,
        VersaoCompativel = true,
        IntegridadeOk = true,
        ChecksumConfere = true,
        Motivo = string.Empty
    };
    public bool ProximaRestauracaoFalha { get; set; }

    public Resultado<BackupRegistro> CriarBackupGerenciado()
    {
        ChamadasCriarBackupGerenciado++;
        if (LancarExcecaoAoCriar)
        {
            throw new InvalidOperationException("Falha simulada.");
        }
        return ProximaCriacaoFalha
            ? Resultado<BackupRegistro>.Falha("Falha simulada ao criar backup gerenciado.")
            : Resultado<BackupRegistro>.Ok(NovoRegistro("C:\\backups"));
    }

    public Resultado<BackupRegistro> CriarBackupExterno(string pastaExterna)
    {
        ChamadasCriarBackupExterno++;
        return ProximaCriacaoFalha
            ? Resultado<BackupRegistro>.Falha("Falha simulada ao criar backup externo.")
            : Resultado<BackupRegistro>.Ok(NovoRegistro(pastaExterna));
    }

    public RelatorioValidacao Validar(string caminhoArquivo) => ProximoRelatorio;

    public Resultado<BackupRegistro> RestaurarPara(string caminhoArquivo) =>
        ProximaRestauracaoFalha
            ? Resultado<BackupRegistro>.Falha("Falha simulada ao restaurar.")
            : Resultado<BackupRegistro>.Ok(NovoRegistro("C:\\backups"));

    private static BackupRegistro NovoRegistro(string destino) => new()
    {
        Id = 1,
        Arquivo = "varthex-comanda-2026-09-18-120000.db",
        Destino = destino,
        CriadoEm = new DateTime(2026, 9, 18, 12, 0, 0, DateTimeKind.Utc),
        Status = StatusBackup.Sucesso,
        Checksum = "abc123",
        Mensagem = null
    };
}
```

- [ ] **Step 2: Escrever os testes de `CriarBackupAutomatico` (vão falhar — a classe não existe)**

Criar `backend/tests/VarthexComanda.Application.Tests/Backup/CriarBackupAutomaticoTests.cs`:

```csharp
using VarthexComanda.Application.Backup;
using VarthexComanda.Application.Tests.Catalogo;
using Xunit;

namespace VarthexComanda.Application.Tests.Backup;

public class CriarBackupAutomaticoTests
{
    [Fact]
    public void Executar_JaExisteBackupHoje_NaoCriaNovoBackup()
    {
        var registros = new FakeBackupRegistroRepository { ExisteBackupHojeRetorno = true };
        var backupService = new FakeBackupService();
        var caso = new CriarBackupAutomatico(backupService, registros, new FakeClock());

        caso.Executar();

        Assert.Equal(0, backupService.ChamadasCriarBackupGerenciado);
    }

    [Fact]
    public void Executar_NaoExisteBackupHoje_CriaBackup()
    {
        var registros = new FakeBackupRegistroRepository { ExisteBackupHojeRetorno = false };
        var backupService = new FakeBackupService();
        var caso = new CriarBackupAutomatico(backupService, registros, new FakeClock());

        caso.Executar();

        Assert.Equal(1, backupService.ChamadasCriarBackupGerenciado);
    }

    [Fact]
    public void Executar_Incondicional_CriaBackupMesmoSeJaExisteHoje()
    {
        var registros = new FakeBackupRegistroRepository { ExisteBackupHojeRetorno = true };
        var backupService = new FakeBackupService();
        var caso = new CriarBackupAutomatico(backupService, registros, new FakeClock());

        caso.Executar(incondicional: true);

        Assert.Equal(1, backupService.ChamadasCriarBackupGerenciado);
    }

    [Fact]
    public void Executar_BackupServiceLancaExcecao_NaoPropagaExcecao()
    {
        var registros = new FakeBackupRegistroRepository { ExisteBackupHojeRetorno = false };
        var backupService = new FakeBackupService { LancarExcecaoAoCriar = true };
        var caso = new CriarBackupAutomatico(backupService, registros, new FakeClock());

        var excecao = Record.Exception(() => caso.Executar());

        Assert.Null(excecao);
    }
}
```

- [ ] **Step 3: Escrever os testes de `CriarBackupManual` (mesma classe ainda não existe)**

Criar `backend/tests/VarthexComanda.Application.Tests/Backup/CriarBackupManualTests.cs`:

```csharp
using VarthexComanda.Application.Backup;
using Xunit;

namespace VarthexComanda.Application.Tests.Backup;

public class CriarBackupManualTests
{
    [Fact]
    public void Executar_SemPastaExterna_SoCriaNaGerenciada()
    {
        var backupService = new FakeBackupService();
        var caso = new CriarBackupManual(backupService);

        var resultado = caso.Executar(null);

        Assert.True(resultado.Sucesso);
        Assert.Equal(1, backupService.ChamadasCriarBackupGerenciado);
        Assert.Equal(0, backupService.ChamadasCriarBackupExterno);
    }

    [Fact]
    public void Executar_ComPastaExterna_CriaNasDuas()
    {
        var backupService = new FakeBackupService();
        var caso = new CriarBackupManual(backupService);

        caso.Executar("D:\\pendrive");

        Assert.Equal(1, backupService.ChamadasCriarBackupGerenciado);
        Assert.Equal(1, backupService.ChamadasCriarBackupExterno);
    }

    [Fact]
    public void Executar_GerenciadaFalha_NaoTentaExterna()
    {
        var backupService = new FakeBackupService { ProximaCriacaoFalha = true };
        var caso = new CriarBackupManual(backupService);

        var resultado = caso.Executar("D:\\pendrive");

        Assert.False(resultado.Sucesso);
        Assert.Equal(0, backupService.ChamadasCriarBackupExterno);
    }
}
```

- [ ] **Step 4: Escrever os testes de `ValidarBackup` e `RestaurarBackup`**

Criar `backend/tests/VarthexComanda.Application.Tests/Backup/ValidarBackupTests.cs`:

```csharp
using VarthexComanda.Application.Backup;
using Xunit;

namespace VarthexComanda.Application.Tests.Backup;

public class ValidarBackupTests
{
    [Fact]
    public void Executar_RepassaORelatorioDoBackupService()
    {
        var backupService = new FakeBackupService();
        var caso = new ValidarBackup(backupService);

        var relatorio = caso.Executar("C:\\qualquer.db");

        Assert.Equal(backupService.ProximoRelatorio, relatorio);
    }
}
```

Criar `backend/tests/VarthexComanda.Application.Tests/Backup/RestaurarBackupTests.cs`:

```csharp
using VarthexComanda.Application.Backup;
using Xunit;

namespace VarthexComanda.Application.Tests.Backup;

public class RestaurarBackupTests
{
    [Fact]
    public void Executar_RelatorioAprovado_ChamaRestaurarPara()
    {
        var backupService = new FakeBackupService();
        var caso = new RestaurarBackup(backupService);

        var resultado = caso.Executar("C:\\backup-valido.db");

        Assert.True(resultado.Sucesso);
    }

    [Fact]
    public void Executar_RelatorioReprovado_NaoChamaRestaurarParaERetornaFalha()
    {
        var backupService = new FakeBackupService
        {
            ProximoRelatorio = new RelatorioValidacao
            {
                FormatoValido = false,
                VersaoCompativel = false,
                IntegridadeOk = false,
                ChecksumConfere = null,
                Motivo = "Arquivo corrompido."
            },
            ProximaRestauracaoFalha = true // se RestaurarPara for chamado por engano, o teste também falharia por outro motivo
        };
        var caso = new RestaurarBackup(backupService);

        var resultado = caso.Executar("C:\\backup-invalido.db");

        Assert.False(resultado.Sucesso);
        Assert.Equal("Arquivo corrompido.", resultado.Erros[0]);
    }
}
```

- [ ] **Step 5: Rodar os testes e confirmar que falham (erro de compilação)**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet build backend/VarthexComanda.slnx
```

Esperado: falha de build — nenhum dos 5 casos de uso existe ainda.

- [ ] **Step 6: Implementar os 5 casos de uso**

Criar `backend/src/VarthexComanda.Application/Backup/CriarBackupAutomatico.cs`:

```csharp
using VarthexComanda.Application.Abstractions;
using VarthexComanda.Application.Atendimento;

namespace VarthexComanda.Application.Backup;

public class CriarBackupAutomatico
{
    private readonly IBackupService _backupService;
    private readonly IBackupRegistroRepository _registros;
    private readonly IClock _relogio;

    public CriarBackupAutomatico(IBackupService backupService, IBackupRegistroRepository registros, IClock relogio)
    {
        _backupService = backupService;
        _registros = registros;
        _relogio = relogio;
    }

    public void Executar(bool incondicional = false)
    {
        try
        {
            if (!incondicional)
            {
                var hoje = FusoBrasilia.ParaLocal(_relogio.UtcNow).Date;
                var inicioUtc = FusoBrasilia.ParaUtc(hoje);
                var fimUtc = FusoBrasilia.ParaUtc(hoje.AddDays(1));
                if (_registros.ExisteBackupHoje(inicioUtc, fimUtc))
                {
                    return;
                }
            }

            _backupService.CriarBackupGerenciado();
        }
        catch (Exception)
        {
            // backup automático nunca deve interromper o app
        }
    }
}
```

`FusoBrasilia` está em `VarthexComanda.Application.Atendimento` (criado na
Etapa 5) — reaproveitado aqui, não duplicado.

Criar `backend/src/VarthexComanda.Application/Backup/CriarBackupManual.cs`:

```csharp
namespace VarthexComanda.Application.Backup;

public class CriarBackupManual
{
    private readonly IBackupService _backupService;

    public CriarBackupManual(IBackupService backupService)
    {
        _backupService = backupService;
    }

    public Resultado<Domain.BackupRegistro> Executar(string? pastaExterna)
    {
        var resultado = _backupService.CriarBackupGerenciado();
        if (pastaExterna is not null && resultado.Sucesso)
        {
            try
            {
                _backupService.CriarBackupExterno(pastaExterna);
            }
            catch (Exception)
            {
                // falha na cópia externa não invalida o backup gerenciado, que já teve sucesso
            }
        }

        return resultado;
    }
}
```

Nota: usar `Domain.BackupRegistro` totalmente qualificado (ou adicionar
`using VarthexComanda.Domain;`) evita ambiguidade — escolher a forma que
achar mais limpa, ambas funcionam.

Criar `backend/src/VarthexComanda.Application/Backup/ValidarBackup.cs`:

```csharp
namespace VarthexComanda.Application.Backup;

public class ValidarBackup
{
    private readonly IBackupService _backupService;

    public ValidarBackup(IBackupService backupService)
    {
        _backupService = backupService;
    }

    public RelatorioValidacao Executar(string caminhoArquivo) => _backupService.Validar(caminhoArquivo);
}
```

Criar `backend/src/VarthexComanda.Application/Backup/RestaurarBackup.cs`:

```csharp
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Backup;

public class RestaurarBackup
{
    private readonly IBackupService _backupService;

    public RestaurarBackup(IBackupService backupService)
    {
        _backupService = backupService;
    }

    public Resultado<BackupRegistro> Executar(string caminhoArquivo)
    {
        var relatorio = _backupService.Validar(caminhoArquivo);
        if (!relatorio.Aprovado)
        {
            return Resultado<BackupRegistro>.Falha(relatorio.Motivo);
        }

        return _backupService.RestaurarPara(caminhoArquivo);
    }
}
```

Criar `backend/src/VarthexComanda.Application/Backup/ListarBackupsRecentes.cs`:

```csharp
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Backup;

public class ListarBackupsRecentes
{
    private readonly IBackupRegistroRepository _registros;

    public ListarBackupsRecentes(IBackupRegistroRepository registros)
    {
        _registros = registros;
    }

    public IReadOnlyList<BackupRegistro> Executar(int quantidade = 20) => _registros.ListarRecentes(quantidade);
}
```

- [ ] **Step 7: Rodar os testes e confirmar que passam**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release --filter "FullyQualifiedName~CriarBackupAutomaticoTests|FullyQualifiedName~CriarBackupManualTests|FullyQualifiedName~ValidarBackupTests|FullyQualifiedName~RestaurarBackupTests"
```

Esperado: PASS nos 9 testes (4 + 3 + 1 + 1).

- [ ] **Step 8: Rodar a suíte inteira e commitar**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release
```

```bash
git add backend/src/VarthexComanda.Application/Backup/CriarBackupAutomatico.cs backend/src/VarthexComanda.Application/Backup/CriarBackupManual.cs backend/src/VarthexComanda.Application/Backup/ValidarBackup.cs backend/src/VarthexComanda.Application/Backup/RestaurarBackup.cs backend/src/VarthexComanda.Application/Backup/ListarBackupsRecentes.cs backend/tests/VarthexComanda.Application.Tests/Backup/FakeBackupService.cs backend/tests/VarthexComanda.Application.Tests/Backup/CriarBackupAutomaticoTests.cs backend/tests/VarthexComanda.Application.Tests/Backup/CriarBackupManualTests.cs backend/tests/VarthexComanda.Application.Tests/Backup/ValidarBackupTests.cs backend/tests/VarthexComanda.Application.Tests/Backup/RestaurarBackupTests.cs
git commit -m "feat: adiciona casos de uso de backup"
```

---

## Task 5: `BackupViewModel` (Desktop, sem WPF/XAML ainda)

**Files:**
- Create: `backend/src/VarthexComanda.Desktop/Backup/BackupViewModel.cs`
- Create: `backend/tests/VarthexComanda.Desktop.Tests/Backup/FakeConfirmadorDeBackup.cs`
- Test: `backend/tests/VarthexComanda.Desktop.Tests/Backup/BackupViewModelTests.cs`

**Interfaces:**
- Consumes: `CriarBackupManual.Executar(string?) → Resultado<BackupRegistro>`, `ValidarBackup.Executar(string) → RelatorioValidacao` (injetado mas não usado diretamente pelo ViewModel — a validação acontece dentro de `RestaurarBackup`; mantido no construtor só se a Task 6 precisar exibir um preview de validação antes de confirmar — **decisão: não injetar `ValidarBackup` no `BackupViewModel` se não for usado, para não violar YAGNI; usar só `RestaurarBackup`, que já valida internamente**), `RestaurarBackup.Executar(string) → Resultado<BackupRegistro>`, `ListarBackupsRecentes.Executar(int) → IReadOnlyList<BackupRegistro>` (Task 4), `IConfirmador` (`VarthexComanda.Desktop.Atendimento`, já existe — reaproveitado, não duplicado).
- Produces: `BackupViewModel(CriarBackupManual, RestaurarBackup, ListarBackupsRecentes, IConfirmador)`, propriedade `Backups` (`ObservableCollection<BackupRegistro>`), `BackupSelecionado` (`BackupRegistro?`), `Mensagem` (`string`), comandos `CriarBackupCommand` (`RelayCommand<string?>` — aceita a pasta externa opcional escolhida pelo diálogo da Task 6; `null` grava só na pasta gerenciada) e `RestaurarCommand`, método público `AtualizarLista()`, evento `event EventHandler? SolicitouReinicio` — todos consumidos pela Task 6 (`BackupView.xaml`/`.xaml.cs`).

- [ ] **Step 1: Criar o dublê de `IConfirmador` para os testes**

Criar `backend/tests/VarthexComanda.Desktop.Tests/Backup/FakeConfirmadorDeBackup.cs`:

```csharp
using VarthexComanda.Desktop.Atendimento;

namespace VarthexComanda.Desktop.Tests.Backup;

public class FakeConfirmadorDeBackup : IConfirmador
{
    public bool ProximaResposta { get; set; } = true;

    public bool Confirmar(string titulo, string mensagem) => ProximaResposta;
}
```

Nota: este é um dublê separado de `FakeConfirmador` (já existe em
`VarthexComanda.Desktop.Tests.Atendimento`, criado na Etapa 3) porque está em
um namespace de teste diferente (`...Tests.Backup`); duplicar uma classe de
9 linhas é mais simples que criar uma referência cruzada entre pastas de
teste só por isso.

- [ ] **Step 2: Escrever os testes (vão falhar — `BackupViewModel` não existe)**

Criar `backend/tests/VarthexComanda.Desktop.Tests/Backup/BackupViewModelTests.cs`:

```csharp
using VarthexComanda.Application.Backup;
using VarthexComanda.Application.Tests.Backup;
using VarthexComanda.Desktop.Backup;
using VarthexComanda.Domain;
using Xunit;

namespace VarthexComanda.Desktop.Tests.Backup;

public class BackupViewModelTests
{
    private static (BackupViewModel viewModel, FakeBackupService backupService, FakeConfirmadorDeBackup confirmador) CriarViewModel(bool confirmar = true)
    {
        var backupService = new FakeBackupService();
        var registros = new FakeBackupRegistroRepository();
        registros.Registrar(new BackupRegistro
        {
            Id = 0,
            Arquivo = "varthex-comanda-2026-09-17-080000.db",
            Destino = "C:\\backups",
            CriadoEm = new DateTime(2026, 9, 17, 8, 0, 0, DateTimeKind.Utc),
            Status = StatusBackup.Sucesso,
            Checksum = "xyz",
            Mensagem = null
        });
        var confirmador = new FakeConfirmadorDeBackup { ProximaResposta = confirmar };

        var viewModel = new BackupViewModel(
            new CriarBackupManual(backupService),
            new RestaurarBackup(backupService),
            new ListarBackupsRecentes(registros),
            confirmador);

        return (viewModel, backupService, confirmador);
    }

    [Fact]
    public void Construtor_CarregaListaAutomaticamente()
    {
        var (viewModel, _, _) = CriarViewModel();

        Assert.Single(viewModel.Backups);
    }

    [Fact]
    public void CriarBackup_Sucesso_AtualizaListaEMensagem()
    {
        var (viewModel, backupService, _) = CriarViewModel();

        viewModel.CriarBackupCommand.Execute(null);

        Assert.Equal(1, backupService.ChamadasCriarBackupGerenciado);
        Assert.Contains("sucesso", viewModel.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CriarBackup_Falha_MostraMensagemDeErro()
    {
        var backupService = new FakeBackupService { ProximaCriacaoFalha = true };
        var registros = new FakeBackupRegistroRepository();
        var viewModel = new BackupViewModel(
            new CriarBackupManual(backupService),
            new RestaurarBackup(backupService),
            new ListarBackupsRecentes(registros),
            new FakeConfirmadorDeBackup());

        viewModel.CriarBackupCommand.Execute(null);

        Assert.Contains("Falha simulada", viewModel.Mensagem);
    }

    [Fact]
    public void Restaurar_SemBackupSelecionado_ComandoDesabilitado()
    {
        var (viewModel, _, _) = CriarViewModel();

        Assert.False(viewModel.RestaurarCommand.CanExecute(null));
    }

    [Fact]
    public void Restaurar_ConfirmadorRecusa_NaoChamaRestaurarBackup()
    {
        var (viewModel, backupService, _) = CriarViewModel(confirmar: false);
        viewModel.BackupSelecionado = viewModel.Backups[0];

        viewModel.RestaurarCommand.Execute(null);

        Assert.False(backupService.ProximaRestauracaoFalha); // nada mudou; nenhuma chamada de restauração foi feita
    }

    [Fact]
    public void Restaurar_ConfirmadorAceitaESucesso_DisparaSolicitouReinicio()
    {
        var (viewModel, _, _) = CriarViewModel(confirmar: true);
        viewModel.BackupSelecionado = viewModel.Backups[0];
        var reinicioSolicitado = false;
        viewModel.SolicitouReinicio += (_, _) => reinicioSolicitado = true;

        viewModel.RestaurarCommand.Execute(null);

        Assert.True(reinicioSolicitado);
    }

    [Fact]
    public void Restaurar_ConfirmadorAceitaMasFalha_MostraMensagemSemDispararReinicio()
    {
        var backupService = new FakeBackupService { ProximaRestauracaoFalha = true };
        var registros = new FakeBackupRegistroRepository();
        registros.Registrar(new BackupRegistro
        {
            Id = 0,
            Arquivo = "a.db",
            Destino = "C:\\backups",
            CriadoEm = DateTime.UtcNow,
            Status = StatusBackup.Sucesso,
            Checksum = "x",
            Mensagem = null
        });
        var viewModel = new BackupViewModel(
            new CriarBackupManual(backupService),
            new RestaurarBackup(backupService),
            new ListarBackupsRecentes(registros),
            new FakeConfirmadorDeBackup { ProximaResposta = true });
        viewModel.BackupSelecionado = viewModel.Backups[0];
        var reinicioSolicitado = false;
        viewModel.SolicitouReinicio += (_, _) => reinicioSolicitado = true;

        viewModel.RestaurarCommand.Execute(null);

        Assert.False(reinicioSolicitado);
        Assert.Contains("Falha simulada", viewModel.Mensagem);
    }
}
```

- [ ] **Step 3: Rodar os testes e confirmar que falham**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release --filter "FullyQualifiedName~BackupViewModelTests"
```

Esperado: falha de build — o tipo `BackupViewModel` não existe.

- [ ] **Step 4: Implementar `BackupViewModel`**

Criar `backend/src/VarthexComanda.Desktop/Backup/BackupViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VarthexComanda.Application.Backup;
using VarthexComanda.Desktop.Atendimento;
using VarthexComanda.Domain;

namespace VarthexComanda.Desktop.Backup;

public partial class BackupViewModel : ObservableObject
{
    private readonly CriarBackupManual _criarBackupManual;
    private readonly RestaurarBackup _restaurarBackup;
    private readonly ListarBackupsRecentes _listarBackupsRecentes;
    private readonly IConfirmador _confirmador;

    public event EventHandler? SolicitouReinicio;

    public BackupViewModel(
        CriarBackupManual criarBackupManual,
        RestaurarBackup restaurarBackup,
        ListarBackupsRecentes listarBackupsRecentes,
        IConfirmador confirmador)
    {
        _criarBackupManual = criarBackupManual;
        _restaurarBackup = restaurarBackup;
        _listarBackupsRecentes = listarBackupsRecentes;
        _confirmador = confirmador;

        Backups = new ObservableCollection<BackupRegistro>();
        AtualizarLista();
    }

    public ObservableCollection<BackupRegistro> Backups { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestaurarCommand))]
    private BackupRegistro? backupSelecionado;

    [ObservableProperty]
    private string mensagem = string.Empty;

    public void AtualizarLista()
    {
        Backups.Clear();
        foreach (var registro in _listarBackupsRecentes.Executar(20))
        {
            Backups.Add(registro);
        }
    }

    [RelayCommand]
    private void CriarBackup(string? pastaExterna)
    {
        try
        {
            var resultado = _criarBackupManual.Executar(pastaExterna);
            Mensagem = resultado.Sucesso
                ? "Backup criado com sucesso."
                : string.Join(" ", resultado.Erros);
            AtualizarLista();
        }
        catch (Exception)
        {
            Mensagem = "Não foi possível criar o backup. Tente novamente.";
        }
    }

    [RelayCommand(CanExecute = nameof(PodeRestaurar))]
    private void Restaurar()
    {
        if (BackupSelecionado is null)
        {
            return;
        }

        var caminho = Path.Combine(BackupSelecionado.Destino, BackupSelecionado.Arquivo);
        var mensagemConfirmacao =
            $"Isso vai substituir todos os dados atuais pelo backup de {BackupSelecionado.CriadoEm:dd/MM/yyyy HH:mm}. " +
            "Uma cópia de segurança da base atual será criada antes.";
        if (!_confirmador.Confirmar("Restaurar backup", mensagemConfirmacao))
        {
            return;
        }

        try
        {
            var resultado = _restaurarBackup.Executar(caminho);
            if (!resultado.Sucesso)
            {
                Mensagem = string.Join(" ", resultado.Erros);
                return;
            }

            Mensagem = string.Empty;
            SolicitouReinicio?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception)
        {
            Mensagem = "Não foi possível restaurar o backup. Tente novamente.";
        }
    }

    private bool PodeRestaurar() => BackupSelecionado is not null;
}
```

- [ ] **Step 5: Rodar os testes e confirmar que passam**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release --filter "FullyQualifiedName~BackupViewModelTests"
```

Esperado: PASS nos 7 testes.

- [ ] **Step 6: Rodar a suíte inteira e commitar**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release
```

```bash
git add backend/src/VarthexComanda.Desktop/Backup/BackupViewModel.cs backend/tests/VarthexComanda.Desktop.Tests/Backup/FakeConfirmadorDeBackup.cs backend/tests/VarthexComanda.Desktop.Tests/Backup/BackupViewModelTests.cs
git commit -m "feat: adiciona BackupViewModel"
```

---

## Task 6: `BackupView` + diálogos nativos + reinício + shell de navegação + DI + remoção do serviço antigo + verificação manual

**Files:**
- Create: `backend/src/VarthexComanda.Desktop/Backup/UtcParaDataHoraLocalConverter.cs`
- Create: `backend/src/VarthexComanda.Desktop/Backup/BackupView.xaml`
- Create: `backend/src/VarthexComanda.Desktop/Backup/BackupView.xaml.cs`
- Modify: `backend/src/VarthexComanda.Desktop/Backup/BackupViewModel.cs`
- Modify: `backend/src/VarthexComanda.Desktop/MainWindow.xaml`
- Modify: `backend/src/VarthexComanda.Desktop/MainWindow.xaml.cs`
- Modify: `backend/src/VarthexComanda.Desktop/App.xaml.cs`
- Delete: `backend/src/VarthexComanda.Infrastructure/Storage/DatabaseBackupService.cs`
- Delete: `backend/tests/VarthexComanda.Infrastructure.Tests/DatabaseBackupServiceTests.cs`

**Interfaces:**
- Consumes: `BackupViewModel` (Task 5), `FusoBrasilia.ParaLocal` (Etapa 5), `CriarBackupAutomatico`/`IBackupService` (Tasks 2-4).
- Produces: nada — última peça funcional da fatia.

- [ ] **Step 1: Criar o conversor de data/hora local (formato completo, diferente do `UtcParaHorarioLocalConverter` da Etapa 5, que só mostra `HH:mm`)**

Criar `backend/src/VarthexComanda.Desktop/Backup/UtcParaDataHoraLocalConverter.cs`:

```csharp
using System.Globalization;
using System.Windows.Data;
using VarthexComanda.Application.Atendimento;

namespace VarthexComanda.Desktop.Backup;

public class UtcParaDataHoraLocalConverter : IValueConverter
{
    private static readonly CultureInfo CulturaData = CultureInfo.GetCultureInfo("pt-BR");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateTime utc ? FusoBrasilia.ParaLocal(utc).ToString("dd/MM/yyyy HH:mm", CulturaData) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
```

- [ ] **Step 2: Adicionar restauração por arquivo externo a `BackupViewModel.cs` (modifica a Task 5)**

Em `backend/src/VarthexComanda.Desktop/Backup/BackupViewModel.cs`, o arquivo
da Task 5 termina com dois métodos consecutivos, nesta ordem:
`private void Restaurar() { ... }` seguido de
`private bool PodeRestaurar() => BackupSelecionado is not null;`. Substituir
esses **dois métodos juntos** (do `[RelayCommand(CanExecute = ...)]` que
antecede `Restaurar()` até o `;` final de `PodeRestaurar()`, inclusive) pelo
bloco de 4 métodos abaixo — ele já inclui uma nova versão de `PodeRestaurar()`
ao final; não deixar duas definições desse método no arquivo (isso não
compilaria, "membro duplicado"):

```csharp
    [RelayCommand(CanExecute = nameof(PodeRestaurar))]
    private void Restaurar()
    {
        if (BackupSelecionado is null)
        {
            return;
        }

        var caminho = Path.Combine(BackupSelecionado.Destino, BackupSelecionado.Arquivo);
        RestaurarCaminho(caminho, BackupSelecionado.CriadoEm);
    }

    public void RestaurarArquivoExterno(string caminho)
    {
        RestaurarCaminho(caminho, null);
    }

    private void RestaurarCaminho(string caminho, DateTime? criadoEm)
    {
        var mensagemConfirmacao = criadoEm is not null
            ? $"Isso vai substituir todos os dados atuais pelo backup de {criadoEm:dd/MM/yyyy HH:mm}. Uma cópia de segurança da base atual será criada antes."
            : "Isso vai substituir todos os dados atuais pelo backup selecionado. Uma cópia de segurança da base atual será criada antes.";
        if (!_confirmador.Confirmar("Restaurar backup", mensagemConfirmacao))
        {
            return;
        }

        try
        {
            var resultado = _restaurarBackup.Executar(caminho);
            if (!resultado.Sucesso)
            {
                Mensagem = string.Join(" ", resultado.Erros);
                return;
            }

            Mensagem = string.Empty;
            SolicitouReinicio?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception)
        {
            Mensagem = "Não foi possível restaurar o backup. Tente novamente.";
        }
    }

    private bool PodeRestaurar() => BackupSelecionado is not null;
```

- [ ] **Step 3: Criar `BackupView.xaml`**

Criar `backend/src/VarthexComanda.Desktop/Backup/BackupView.xaml`:

```xml
<UserControl x:Class="VarthexComanda.Desktop.Backup.BackupView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:local="clr-namespace:VarthexComanda.Desktop.Backup"
             mc:Ignorable="d">
    <UserControl.Resources>
        <local:UtcParaDataHoraLocalConverter x:Key="DataHora" />
    </UserControl.Resources>
    <Grid Margin="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="0,0,0,12">
            <Button Content="Criar backup agora" Padding="8,4" Margin="0,0,8,0" Command="{Binding CriarBackupCommand}" />
            <Button Content="Escolher pasta externa e criar backup..." Padding="8,4" Click="EscolherPastaExterna_Click" />
        </StackPanel>

        <ListView Grid.Row="1" ItemsSource="{Binding Backups}" SelectedItem="{Binding BackupSelecionado}">
            <ListView.View>
                <GridView>
                    <GridViewColumn Header="Arquivo" DisplayMemberBinding="{Binding Arquivo}" Width="220" />
                    <GridViewColumn Header="Destino" DisplayMemberBinding="{Binding Destino}" Width="180" />
                    <GridViewColumn Header="Data" DisplayMemberBinding="{Binding CriadoEm, Converter={StaticResource DataHora}}" Width="140" />
                    <GridViewColumn Header="Status" DisplayMemberBinding="{Binding Status}" Width="80" />
                    <GridViewColumn Header="Checksum" DisplayMemberBinding="{Binding Checksum}" Width="220" />
                </GridView>
            </ListView.View>
        </ListView>

        <StackPanel Grid.Row="2" Orientation="Horizontal" Margin="0,12,0,0">
            <Button Content="Restaurar backup selecionado" Padding="8,4" Margin="0,0,8,0" Command="{Binding RestaurarCommand}" />
            <Button Content="Selecionar arquivo..." Padding="8,4" Click="SelecionarArquivo_Click" />
        </StackPanel>

        <TextBlock Grid.Row="3" Text="{Binding Mensagem}" Foreground="Red" TextWrapping="Wrap" Margin="0,12,0,0" />
    </Grid>
</UserControl>
```

- [ ] **Step 4: Criar `BackupView.xaml.cs`**

Criar `backend/src/VarthexComanda.Desktop/Backup/BackupView.xaml.cs`:

```csharp
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace VarthexComanda.Desktop.Backup;

public partial class BackupView : UserControl
{
    public BackupViewModel ViewModel { get; }

    public BackupView(BackupViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        ViewModel.SolicitouReinicio += ViewModel_SolicitouReinicio;
    }

    private void EscolherPastaExterna_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new OpenFolderDialog { Title = "Escolher pasta externa para o backup" };
        if (dialogo.ShowDialog() == true)
        {
            ViewModel.CriarBackupCommand.Execute(dialogo.FolderName);
        }
    }

    private void SelecionarArquivo_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new OpenFileDialog
        {
            Title = "Selecionar arquivo de backup",
            Filter = "Banco de dados (*.db)|*.db|Todos os arquivos (*.*)|*.*"
        };
        if (dialogo.ShowDialog() == true)
        {
            ViewModel.RestaurarArquivoExterno(dialogo.FileName);
        }
    }

    private void ViewModel_SolicitouReinicio(object? sender, EventArgs e)
    {
        MessageBox.Show(
            "Backup restaurado com sucesso. O Varthex Comanda vai reiniciar agora.",
            "Varthex Comanda",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        var caminhoExecutavel = Environment.ProcessPath;
        if (caminhoExecutavel is not null)
        {
            Process.Start(caminhoExecutavel);
        }

        Environment.Exit(0);
    }
}
```

`Microsoft.Win32.OpenFolderDialog`/`OpenFileDialog` já vêm com o WPF do .NET
10 — não precisa adicionar pacote novo (não é `System.Windows.Forms`).

- [ ] **Step 5: Adicionar o botão "Backup" em `MainWindow.xaml`**

Em `backend/src/VarthexComanda.Desktop/MainWindow.xaml`, substituir:

```xml
        <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Margin="8">
            <Button Content="Atendimento" Padding="12,4" Margin="0,0,8,0" Click="MostrarAtendimento_Click" />
            <Button Content="Produtos" Padding="12,4" Margin="0,0,8,0" Click="MostrarProdutos_Click" />
            <Button Content="Histórico" Padding="12,4" Click="MostrarHistorico_Click" />
        </StackPanel>
```

por:

```xml
        <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Margin="8">
            <Button Content="Atendimento" Padding="12,4" Margin="0,0,8,0" Click="MostrarAtendimento_Click" />
            <Button Content="Produtos" Padding="12,4" Margin="0,0,8,0" Click="MostrarProdutos_Click" />
            <Button Content="Histórico" Padding="12,4" Margin="0,0,8,0" Click="MostrarHistorico_Click" />
            <Button Content="Backup" Padding="12,4" Click="MostrarBackup_Click" />
        </StackPanel>
```

- [ ] **Step 6: Atualizar `MainWindow.xaml.cs`**

Substituir o conteúdo de
`backend/src/VarthexComanda.Desktop/MainWindow.xaml.cs` por:

```csharp
using System.Windows;
using VarthexComanda.Desktop.Atendimento;
using VarthexComanda.Desktop.Backup;
using VarthexComanda.Desktop.Catalogo;

namespace VarthexComanda.Desktop;

public partial class MainWindow : Window
{
    private readonly AtendimentoView _atendimentoView;
    private readonly ProdutosView _produtosView;
    private readonly HistoricoView _historicoView;
    private readonly BackupView _backupView;

    public MainWindow(AtendimentoView atendimentoView, ProdutosView produtosView, HistoricoView historicoView, BackupView backupView)
    {
        InitializeComponent();
        _atendimentoView = atendimentoView;
        _produtosView = produtosView;
        _historicoView = historicoView;
        _backupView = backupView;
        ConteudoPrincipal.Content = _atendimentoView;
    }

    private void MostrarAtendimento_Click(object sender, RoutedEventArgs e)
    {
        _atendimentoView.ViewModel.AtualizarCategorias();
        ConteudoPrincipal.Content = _atendimentoView;
    }

    private void MostrarProdutos_Click(object sender, RoutedEventArgs e)
    {
        ConteudoPrincipal.Content = _produtosView;
    }

    private void MostrarHistorico_Click(object sender, RoutedEventArgs e)
    {
        _historicoView.ViewModel.AtualizarVendas();
        ConteudoPrincipal.Content = _historicoView;
    }

    private void MostrarBackup_Click(object sender, RoutedEventArgs e)
    {
        _backupView.ViewModel.AtualizarLista();
        ConteudoPrincipal.Content = _backupView;
    }
}
```

- [ ] **Step 7: Remover o serviço antigo de backup**

```bash
rm backend/src/VarthexComanda.Infrastructure/Storage/DatabaseBackupService.cs
rm backend/tests/VarthexComanda.Infrastructure.Tests/DatabaseBackupServiceTests.cs
```

- [ ] **Step 8: Reescrever `App.xaml.cs`**

Adicionar aos `using`s do topo de `backend/src/VarthexComanda.Desktop/App.xaml.cs`:

```csharp
using VarthexComanda.Application.Backup;
using VarthexComanda.Desktop.Backup;
using VarthexComanda.Infrastructure.Backup;
```

Substituir o bloco de registros de DI — encontrar
`services.AddTransient<HistoricoView>();` e adicionar logo depois:

```csharp
        services.AddTransient<IBackupRegistroRepository, EfBackupRegistroRepository>();
        services.AddTransient<IBackupService, EfBackupService>();
        services.AddTransient<CriarBackupAutomatico>();
        services.AddTransient<CriarBackupManual>();
        services.AddTransient<ValidarBackup>();
        services.AddTransient<RestaurarBackup>();
        services.AddTransient<ListarBackupsRecentes>();
        services.AddTransient<BackupViewModel>();
        services.AddTransient<BackupView>();
```

Substituir o bloco do backup preventivo pré-migração — encontrar:

```csharp
            var factory = _serviceProvider.GetRequiredService<IDbContextFactory<VarthexComandaDbContext>>();
            var clock = _serviceProvider.GetRequiredService<IClock>();
            using var dbContext = factory.CreateDbContext();

            var pendentes = dbContext.Database.GetPendingMigrations().ToList();
            if (pendentes.Count > 0)
            {
                var backupCriado = DatabaseBackupService.BackupIfExists(paths.DatabasePath, paths.BackupsDirectory, clock.UtcNow);
                if (backupCriado is not null)
                {
                    _logger.Information("Backup preventivo criado em {Caminho} antes de aplicar {Quantidade} migração(ões) pendente(s)", backupCriado, pendentes.Count);
                }
            }
```

por:

```csharp
            var factory = _serviceProvider.GetRequiredService<IDbContextFactory<VarthexComandaDbContext>>();
            using var dbContext = factory.CreateDbContext();

            var pendentes = dbContext.Database.GetPendingMigrations().ToList();
            if (pendentes.Count > 0)
            {
                var backupService = _serviceProvider.GetRequiredService<IBackupService>();
                var resultadoPreventivo = backupService.CriarBackupGerenciado();
                if (resultadoPreventivo.Sucesso)
                {
                    _logger.Information("Backup preventivo criado antes de aplicar {Quantidade} migração(ões) pendente(s)", pendentes.Count);
                }
                else
                {
                    _logger.Warning("Backup preventivo antes da migração falhou: {Mensagem}", string.Join(" ", resultadoPreventivo.Erros));
                }
            }
```

(`clock` foi removido porque, depois desta mudança, nada mais no método o
usa — `IClock` continua registrado no DI e usado normalmente dentro de
`EfBackupService`/`CriarBackupAutomatico`.)

Substituir o disparo do backup automático de startup — encontrar:

```csharp
            _logger.Information("Banco pronto em {Caminho}", paths.DatabasePath);

            _serviceProvider.GetRequiredService<MainWindow>().Show();
```

por:

```csharp
            _logger.Information("Banco pronto em {Caminho}", paths.DatabasePath);

            _serviceProvider.GetRequiredService<CriarBackupAutomatico>().Executar();

            _serviceProvider.GetRequiredService<MainWindow>().Show();
```

Substituir `OnExit` — encontrar:

```csharp
    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.Information("Encerrando Varthex Comanda");
        _serviceProvider?.Dispose();
        (_logger as IDisposable)?.Dispose();
        _guard?.Dispose();
        base.OnExit(e);
    }
```

por:

```csharp
    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.GetRequiredService<CriarBackupAutomatico>().Executar(incondicional: true);
        _logger?.Information("Encerrando Varthex Comanda");
        _serviceProvider?.Dispose();
        (_logger as IDisposable)?.Dispose();
        _guard?.Dispose();
        base.OnExit(e);
    }
```

- [ ] **Step 9: Build completo e suíte inteira**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet build backend/VarthexComanda.slnx
```

Esperado: build limpo (0 erros) — confirmar que a remoção de
`DatabaseBackupService` não deixou nenhuma referência solta.

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release
```

Esperado: todos os projetos PASS.

- [ ] **Step 10: Verificação manual na aplicação real**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet run --project backend/src/VarthexComanda.Desktop
```

Roteiro manual:

1. Abrir o app — no log (`%LOCALAPPDATA%\VarthexComanda\logs\`) ou na aba
   "Backup", confirmar que um backup automático foi criado (primeira
   abertura do dia).
2. Clicar em "Criar backup agora" — um novo backup aparece na lista com
   checksum preenchido.
3. Selecionar um backup da lista e clicar "Restaurar backup selecionado" —
   deve aparecer a confirmação mostrando a data do backup; confirmar.
4. Confirmar que o app fecha e reabre sozinho (o processo reinicia), e que
   sobe normalmente (passa pela checagem de integridade do `App.xaml.cs`).
5. Clicar "Selecionar arquivo..." e escolher um arquivo `.txt` qualquer (não
   um backup válido) — deve ser recusado com mensagem clara, sem travar a
   tela.
6. Fechar e reabrir o app novamente no mesmo dia — confirmar que **não** foi
   criado um segundo backup "de hoje" (a lista não ganha uma nova entrada
   automática, só a manual do passo 2 e a preventiva da restauração do passo
   3, se houver).
7. Clicar em "Escolher pasta externa e criar backup..." e escolher uma
   pasta qualquer — confirmar que aparecem dois backups novos na lista (um
   na pasta gerenciada, um na externa).

- [ ] **Step 11: Commitar**

```bash
git add backend/src/VarthexComanda.Desktop/Backup/UtcParaDataHoraLocalConverter.cs backend/src/VarthexComanda.Desktop/Backup/BackupView.xaml backend/src/VarthexComanda.Desktop/Backup/BackupView.xaml.cs backend/src/VarthexComanda.Desktop/Backup/BackupViewModel.cs backend/src/VarthexComanda.Desktop/MainWindow.xaml backend/src/VarthexComanda.Desktop/MainWindow.xaml.cs backend/src/VarthexComanda.Desktop/App.xaml.cs backend/src/VarthexComanda.Infrastructure/Storage/DatabaseBackupService.cs backend/tests/VarthexComanda.Infrastructure.Tests/DatabaseBackupServiceTests.cs
git commit -m "feat: adiciona tela de backup, reinicio e remove servico antigo"
```

---

## Task 7: Verificação final e changelog

**Files:**
- Modify: `docs/CHANGELOG.md`

**Interfaces:**
- Consumes: nada novo — só confirma o estado final da fatia.
- Produces: nada — última tarefa do plano.

- [ ] **Step 1: Rodar a suíte inteira e conferir a contagem de testes**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release
```

Contagem esperada por projeto (partindo de 121 ao final da Etapa 5): Domain 4
(sem mudança), Application 48 + 9 (Task 4: 4 `CriarBackupAutomaticoTests` +
3 `CriarBackupManualTests` + 1 `ValidarBackupTests` + 1 `RestaurarBackupTests`)
= 57, Infrastructure 42 + 5 (Task 1) + 5 (Task 2) + 8 (Task 3) − 2
(`DatabaseBackupServiceTests` removido na Task 6) = 58, Desktop.Tests 27 + 7
(Task 5) = 34. **Total esperado: 153.**

- [ ] **Step 2: Atualizar o changelog**

Em `docs/CHANGELOG.md`, adicionar uma nova seção no topo (mesmo formato das
entradas anteriores — verificar o cabeçalho exato das seções `1.7`/`1.8`
existentes antes de escrever, para manter o estilo):

```markdown
## 1.9 - 2026-09-18

- backup e recuperação (Etapa 6, RF21-24): backup automático na primeira
  abertura do dia e ao encerrar o app, backup manual com opção de pasta
  externa, validação de formato/versão/integridade/checksum, e restauração
  com cópia preventiva da base atual e reinício automático do aplicativo;
  motor de backup passa a usar a API de snapshot nativa do SQLite em vez de
  cópia de arquivo direta; retenção mantém as 30 cópias mais recentes na
  pasta gerenciada.
```

- [ ] **Step 3: Commitar**

```bash
git add docs/CHANGELOG.md
git commit -m "docs: registra backup e recuperacao no changelog"
```
