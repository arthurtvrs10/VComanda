# Configurações (Etapa 7, parte 1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement RF25 (manter nome do estabelecimento, faixa válida de
números de comanda e pasta de backup externa padrão) sobre a tabela
`configuracao` já existente desde a base técnica.

**Architecture:** Um domínio novo `Configuracao` (pasta/namespace próprio em
todas as camadas). `IConfiguracaoRepository` é um key-value genérico
(`ObterValor`/`Definir`) — a tradução para o objeto de negócio
`ConfiguracaoEstabelecimento` (parsing, valores ausentes) acontece só nos
casos de uso `ObterConfiguracao`/`SalvarConfiguracao`. Dois pontos de
consumo em código já mesclado: `AbrirComanda` (Etapa 3) passa a validar a
faixa configurada, e `CriarBackupAutomatico` (Etapa 6) passa a tentar uma
cópia externa no encerramento do app quando uma pasta estiver configurada.

**Tech Stack:** C#/.NET 10, WPF (CommunityToolkit.Mvvm), EF Core/SQLite,
xUnit.

**Spec:** [specs/2026-09-18-configuracoes-design.md](../specs/2026-09-18-configuracoes-design.md)

## Global Constraints

- Domínio novo `Configuracao`, pasta/namespace próprio em todas as camadas:
  `VarthexComanda.Application.Configuracao`
  (`backend/src/VarthexComanda.Application/Configuracao/`),
  `VarthexComanda.Infrastructure.Configuracao`
  (`backend/src/VarthexComanda.Infrastructure/Configuracao/`),
  `VarthexComanda.Desktop.Configuracao`
  (`backend/src/VarthexComanda.Desktop/Configuracao/`).
- **Colisão de nome conhecida, aplicar a mesma correção já usada para
  `System.Windows.Application` em `App.xaml.cs`/`EncerramentoDialog.cs`:**
  dentro do namespace `VarthexComanda.Infrastructure.Configuracao`, o
  identificador solto `Configuracao` resolve para o **namespace aninhado**
  (o próprio namespace em que o arquivo está), não para a classe
  `VarthexComanda.Domain.Configuracao` — mesmo com
  `using VarthexComanda.Domain;` no topo. Qualificar totalmente
  (`VarthexComanda.Domain.Configuracao`) em todo lugar que instanciar essa
  classe de dentro do namespace `Infrastructure.Configuracao`. As camadas
  `Application.Configuracao`/`Desktop.Configuracao` não têm esse risco nesta
  fatia porque nunca referenciam `Domain.Configuracao` diretamente (o
  contrato é só `string`/`int` primitivos) — mas fique atento se algo mudar.
- `IConfiguracaoRepository` é genérico (chave/valor cru) — nenhuma lógica de
  parsing ou de "valor ausente" no repositório; isso é responsabilidade só
  dos casos de uso `ObterConfiguracao`/`SalvarConfiguracao`.
- `ObterConfiguracao`/`SalvarConfiguracao` não podem ter nenhum
  `using System.Windows`/`Microsoft.Win32` — camada Application permanece
  livre de WPF.
- `AbrirComanda` sem configuração de quantidade máxima mantém o
  comportamento anterior (`numero > 0`, sem teto) — nunca travar a abertura
  de comandas antes de alguém configurar isso.
- `CriarBackupAutomatico` só tenta a cópia externa configurada no ramo
  **incondicional** (encerramento do app) — a abertura do dia nunca tenta,
  mesmo com pasta configurada; falha na cópia externa nunca interrompe o
  encerramento do app (mesmo padrão de tolerância a falha já usado em
  `CriarBackupManual`).
- `ConfiguracaoViewModel` não pode ter nenhum `using System.Windows`/
  `Microsoft.Win32` — esses ficam em `ConfiguracaoView.xaml.cs`, mesmo
  padrão já estabelecido para `BackupViewModel`/`BackupView`.
- Prefixar todo comando `dotnet` com o PATH do SDK: PowerShell
  `$env:Path += ';C:\Program Files\dotnet'; ` / Bash
  `export PATH="$PATH:/c/Program Files/dotnet" && `.

---

## Task 1: `IConfiguracaoRepository` + `EfConfiguracaoRepository` (bookkeeping)

**Files:**
- Create: `backend/src/VarthexComanda.Application/Configuracao/IConfiguracaoRepository.cs`
- Create: `backend/src/VarthexComanda.Infrastructure/Configuracao/EfConfiguracaoRepository.cs`
- Create: `backend/tests/VarthexComanda.Application.Tests/Configuracao/FakeConfiguracaoRepository.cs`
- Test: `backend/tests/VarthexComanda.Infrastructure.Tests/Configuracao/EfConfiguracaoRepositoryTests.cs`

**Interfaces:**
- Consumes: `Configuracao` (`VarthexComanda.Domain`, já existe), `contexto.Configuracoes` (`DbSet<Configuracao>`, já existe em `VarthexComandaDbContext`).
- Produces: `IConfiguracaoRepository.ObterValor(string chave) → string?`, `.Definir(string chave, string valor, DateTime atualizadoEm)` — consumidos pela Task 2 (casos de uso) e, via `FakeConfiguracaoRepository`, pela Task 3 (testes cruzados) e Task 4 (testes da ViewModel).

- [ ] **Step 1: Criar a interface**

Criar `backend/src/VarthexComanda.Application/Configuracao/IConfiguracaoRepository.cs`:

```csharp
namespace VarthexComanda.Application.Configuracao;

public interface IConfiguracaoRepository
{
    string? ObterValor(string chave);
    void Definir(string chave, string valor, DateTime atualizadoEm);
}
```

- [ ] **Step 2: Criar o fake para testes**

Criar `backend/tests/VarthexComanda.Application.Tests/Configuracao/FakeConfiguracaoRepository.cs`:

```csharp
using VarthexComanda.Application.Configuracao;

namespace VarthexComanda.Application.Tests.Configuracao;

public class FakeConfiguracaoRepository : IConfiguracaoRepository
{
    private readonly Dictionary<string, string> _valores = new();

    public string? ObterValor(string chave) => _valores.TryGetValue(chave, out var valor) ? valor : null;

    public void Definir(string chave, string valor, DateTime atualizadoEm) => _valores[chave] = valor;
}
```

- [ ] **Step 3: Escrever os testes de infraestrutura (vão falhar — `EfConfiguracaoRepository` não existe)**

Criar `backend/tests/VarthexComanda.Infrastructure.Tests/Configuracao/EfConfiguracaoRepositoryTests.cs`:

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VarthexComanda.Infrastructure.Configuracao;
using VarthexComanda.Infrastructure.Persistence;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests.Configuracao;

public class EfConfiguracaoRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ServiceProvider _provedor;
    private readonly IDbContextFactory<VarthexComandaDbContext> _fabrica;

    public EfConfiguracaoRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"varthex-configuracao-tests-{Guid.NewGuid()}.db");

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

    [Fact]
    public void ObterValor_ChaveInexistente_RetornaNull()
    {
        var repositorio = new EfConfiguracaoRepository(_fabrica);

        var valor = repositorio.ObterValor("chave.qualquer");

        Assert.Null(valor);
    }

    [Fact]
    public void Definir_GravaEObterValorLeDeVolta()
    {
        var repositorio = new EfConfiguracaoRepository(_fabrica);

        repositorio.Definir("estabelecimento.nome", "Lanchonete do Zé", DateTime.UtcNow);
        var valor = repositorio.ObterValor("estabelecimento.nome");

        Assert.Equal("Lanchonete do Zé", valor);
    }

    [Fact]
    public void Definir_ChamadoDuasVezesNaMesmaChave_AtualizaSemDuplicar()
    {
        var repositorio = new EfConfiguracaoRepository(_fabrica);

        repositorio.Definir("estabelecimento.nome", "Nome Antigo", DateTime.UtcNow);
        repositorio.Definir("estabelecimento.nome", "Nome Novo", DateTime.UtcNow);

        Assert.Equal("Nome Novo", repositorio.ObterValor("estabelecimento.nome"));

        using var contexto = _fabrica.CreateDbContext();
        Assert.Single(contexto.Configuracoes.Where(c => c.Chave == "estabelecimento.nome"));
    }
}
```

- [ ] **Step 4: Rodar os testes e confirmar que falham (erro de compilação)**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet build backend/VarthexComanda.slnx
```

Esperado: falha de build — o tipo `EfConfiguracaoRepository` não existe.

- [ ] **Step 5: Implementar `EfConfiguracaoRepository`**

Criar `backend/src/VarthexComanda.Infrastructure/Configuracao/EfConfiguracaoRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using VarthexComanda.Application.Configuracao;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Persistence;

namespace VarthexComanda.Infrastructure.Configuracao;

public class EfConfiguracaoRepository : IConfiguracaoRepository
{
    private readonly IDbContextFactory<VarthexComandaDbContext> _fabricaContexto;

    public EfConfiguracaoRepository(IDbContextFactory<VarthexComandaDbContext> fabricaContexto)
    {
        _fabricaContexto = fabricaContexto;
    }

    public string? ObterValor(string chave)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        return contexto.Configuracoes.SingleOrDefault(c => c.Chave == chave)?.Valor;
    }

    public void Definir(string chave, string valor, DateTime atualizadoEm)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        var existente = contexto.Configuracoes.SingleOrDefault(c => c.Chave == chave);
        if (existente is null)
        {
            // Qualificado totalmente: dentro deste namespace, "Configuracao" solto
            // resolveria para o namespace VarthexComanda.Infrastructure.Configuracao
            // (o próprio namespace deste arquivo), não para a classe de domínio —
            // mesma colisão já vista com System.Windows.Application em App.xaml.cs.
            contexto.Configuracoes.Add(new VarthexComanda.Domain.Configuracao
            {
                Chave = chave,
                Valor = valor,
                AtualizadoEm = atualizadoEm
            });
        }
        else
        {
            existente.Valor = valor;
            existente.AtualizadoEm = atualizadoEm;
        }

        contexto.SaveChanges();
    }
}
```

- [ ] **Step 6: Rodar os testes e confirmar que passam**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release --filter "FullyQualifiedName~EfConfiguracaoRepositoryTests"
```

Esperado: PASS nos 3 testes.

- [ ] **Step 7: Rodar a suíte inteira e commitar**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release
```

Esperado: todos os projetos PASS (nada ainda consome `IConfiguracaoRepository`
fora dos testes).

```bash
git add backend/src/VarthexComanda.Application/Configuracao/IConfiguracaoRepository.cs backend/src/VarthexComanda.Infrastructure/Configuracao/EfConfiguracaoRepository.cs backend/tests/VarthexComanda.Application.Tests/Configuracao/FakeConfiguracaoRepository.cs backend/tests/VarthexComanda.Infrastructure.Tests/Configuracao/EfConfiguracaoRepositoryTests.cs
git commit -m "feat: adiciona repositorio de configuracoes do estabelecimento"
```

---

## Task 2: Casos de uso (Application) — `ObterConfiguracao` e `SalvarConfiguracao`

**Files:**
- Create: `backend/src/VarthexComanda.Application/Configuracao/ConfiguracaoEstabelecimento.cs`
- Create: `backend/src/VarthexComanda.Application/Configuracao/ObterConfiguracao.cs`
- Create: `backend/src/VarthexComanda.Application/Configuracao/SalvarConfiguracao.cs`
- Test: `backend/tests/VarthexComanda.Application.Tests/Configuracao/ObterConfiguracaoTests.cs`
- Test: `backend/tests/VarthexComanda.Application.Tests/Configuracao/SalvarConfiguracaoTests.cs`

**Interfaces:**
- Consumes: `IConfiguracaoRepository.ObterValor`/`Definir` (Task 1), `FakeConfiguracaoRepository` (Task 1, para os testes), `IClock` (já existe), `Resultado<T>` (`VarthexComanda.Application.Catalogo`, já existe).
- Produces: `ConfiguracaoEstabelecimento { string NomeEstabelecimento; int? QuantidadeMaximaComandas; string? PastaBackupExterna; }`, `ObterConfiguracao.Executar() → ConfiguracaoEstabelecimento`, `SalvarConfiguracao.Executar(ConfiguracaoEstabelecimento) → Resultado<ConfiguracaoEstabelecimento>` — todos consumidos pela Task 3 (`AbrirComanda`/`CriarBackupAutomatico` usam `ObterConfiguracao`) e pela Task 4 (`ConfiguracaoViewModel` usa os dois).

- [ ] **Step 1: Criar o objeto de negócio `ConfiguracaoEstabelecimento`**

Criar `backend/src/VarthexComanda.Application/Configuracao/ConfiguracaoEstabelecimento.cs`:

```csharp
namespace VarthexComanda.Application.Configuracao;

public class ConfiguracaoEstabelecimento
{
    public required string NomeEstabelecimento { get; init; }
    public int? QuantidadeMaximaComandas { get; init; }
    public string? PastaBackupExterna { get; init; }
}
```

- [ ] **Step 2: Escrever os testes de `ObterConfiguracao` (vão falhar — a classe não existe)**

Criar `backend/tests/VarthexComanda.Application.Tests/Configuracao/ObterConfiguracaoTests.cs`:

```csharp
using VarthexComanda.Application.Configuracao;
using Xunit;

namespace VarthexComanda.Application.Tests.Configuracao;

public class ObterConfiguracaoTests
{
    [Fact]
    public void Executar_NadaConfigurado_RetornaValoresPadrao()
    {
        var repositorio = new FakeConfiguracaoRepository();
        var caso = new ObterConfiguracao(repositorio);

        var configuracao = caso.Executar();

        Assert.Equal(string.Empty, configuracao.NomeEstabelecimento);
        Assert.Null(configuracao.QuantidadeMaximaComandas);
        Assert.Null(configuracao.PastaBackupExterna);
    }

    [Fact]
    public void Executar_TudoConfigurado_RetornaValoresCorretos()
    {
        var repositorio = new FakeConfiguracaoRepository();
        repositorio.Definir("estabelecimento.nome", "Lanchonete do Zé", DateTime.UtcNow);
        repositorio.Definir("comandas.quantidade_maxima", "30", DateTime.UtcNow);
        repositorio.Definir("backup.pasta_externa", "D:\\backups", DateTime.UtcNow);
        var caso = new ObterConfiguracao(repositorio);

        var configuracao = caso.Executar();

        Assert.Equal("Lanchonete do Zé", configuracao.NomeEstabelecimento);
        Assert.Equal(30, configuracao.QuantidadeMaximaComandas);
        Assert.Equal("D:\\backups", configuracao.PastaBackupExterna);
    }

    [Fact]
    public void Executar_QuantidadeComValorInvalido_TratadoComoNulo()
    {
        var repositorio = new FakeConfiguracaoRepository();
        repositorio.Definir("comandas.quantidade_maxima", "abc", DateTime.UtcNow);
        var caso = new ObterConfiguracao(repositorio);

        var configuracao = caso.Executar();

        Assert.Null(configuracao.QuantidadeMaximaComandas);
    }
}
```

Nota: este arquivo de teste está no namespace
`VarthexComanda.Application.Tests.Configuracao` (mesmo namespace de
`FakeConfiguracaoRepository`, da Task 1), por isso não precisa de `using`
para o fake. `ObterConfiguracao` (a classe) resolve normalmente com o
`using VarthexComanda.Application.Configuracao;` acima — a colisão
descrita nas Global Constraints só afeta o identificador solto
`Configuracao` referenciado de dentro de um namespace `...Configuracao`
(caso do `EfConfiguracaoRepository.cs` na Task 1), não nomes de classe
mais longos como `ObterConfiguracao`.

- [ ] **Step 3: Escrever os testes de `SalvarConfiguracao` (mesma classe ainda não existe)**

Criar `backend/tests/VarthexComanda.Application.Tests/Configuracao/SalvarConfiguracaoTests.cs`:

```csharp
using VarthexComanda.Application.Configuracao;
using VarthexComanda.Application.Tests.Catalogo;
using Xunit;

namespace VarthexComanda.Application.Tests.Configuracao;

public class SalvarConfiguracaoTests
{
    [Fact]
    public void Executar_NomeVazio_Falha()
    {
        var repositorio = new FakeConfiguracaoRepository();
        var caso = new SalvarConfiguracao(repositorio, new FakeClock());

        var resultado = caso.Executar(new ConfiguracaoEstabelecimento
        {
            NomeEstabelecimento = "",
            QuantidadeMaximaComandas = null,
            PastaBackupExterna = null
        });

        Assert.False(resultado.Sucesso);
        Assert.Contains("Informe o nome do estabelecimento.", resultado.Erros);
    }

    [Fact]
    public void Executar_QuantidadeZeroOuNegativa_Falha()
    {
        var repositorio = new FakeConfiguracaoRepository();
        var caso = new SalvarConfiguracao(repositorio, new FakeClock());

        var resultado = caso.Executar(new ConfiguracaoEstabelecimento
        {
            NomeEstabelecimento = "Lanchonete",
            QuantidadeMaximaComandas = 0,
            PastaBackupExterna = null
        });

        Assert.False(resultado.Sucesso);
        Assert.Contains("A quantidade de comandas deve ser maior que zero.", resultado.Erros);
    }

    [Fact]
    public void Executar_DadosValidos_GravaAsTresChavesERetornaSucesso()
    {
        var repositorio = new FakeConfiguracaoRepository();
        var caso = new SalvarConfiguracao(repositorio, new FakeClock());

        var resultado = caso.Executar(new ConfiguracaoEstabelecimento
        {
            NomeEstabelecimento = "Lanchonete do Zé",
            QuantidadeMaximaComandas = 30,
            PastaBackupExterna = "D:\\backups"
        });

        Assert.True(resultado.Sucesso);
        Assert.Equal("Lanchonete do Zé", repositorio.ObterValor("estabelecimento.nome"));
        Assert.Equal("30", repositorio.ObterValor("comandas.quantidade_maxima"));
        Assert.Equal("D:\\backups", repositorio.ObterValor("backup.pasta_externa"));
    }
}
```

- [ ] **Step 4: Rodar os testes e confirmar que falham (erro de compilação)**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet build backend/VarthexComanda.slnx
```

Esperado: falha de build — `ObterConfiguracao`/`SalvarConfiguracao`/`ConfiguracaoEstabelecimento` não existem.

- [ ] **Step 5: Implementar `ObterConfiguracao`**

Criar `backend/src/VarthexComanda.Application/Configuracao/ObterConfiguracao.cs`:

```csharp
namespace VarthexComanda.Application.Configuracao;

public class ObterConfiguracao
{
    private readonly IConfiguracaoRepository _configuracoes;

    public ObterConfiguracao(IConfiguracaoRepository configuracoes)
    {
        _configuracoes = configuracoes;
    }

    public ConfiguracaoEstabelecimento Executar()
    {
        var nome = _configuracoes.ObterValor("estabelecimento.nome") ?? string.Empty;

        var quantidadeTexto = _configuracoes.ObterValor("comandas.quantidade_maxima");
        int? quantidade = int.TryParse(quantidadeTexto, out var quantidadeValor) ? quantidadeValor : null;

        var pastaExterna = _configuracoes.ObterValor("backup.pasta_externa");
        if (string.IsNullOrEmpty(pastaExterna))
        {
            pastaExterna = null;
        }

        return new ConfiguracaoEstabelecimento
        {
            NomeEstabelecimento = nome,
            QuantidadeMaximaComandas = quantidade,
            PastaBackupExterna = pastaExterna
        };
    }
}
```

- [ ] **Step 6: Implementar `SalvarConfiguracao`**

Criar `backend/src/VarthexComanda.Application/Configuracao/SalvarConfiguracao.cs`:

```csharp
using VarthexComanda.Application.Abstractions;
using VarthexComanda.Application.Catalogo;

namespace VarthexComanda.Application.Configuracao;

public class SalvarConfiguracao
{
    private readonly IConfiguracaoRepository _configuracoes;
    private readonly IClock _relogio;

    public SalvarConfiguracao(IConfiguracaoRepository configuracoes, IClock relogio)
    {
        _configuracoes = configuracoes;
        _relogio = relogio;
    }

    public Resultado<ConfiguracaoEstabelecimento> Executar(ConfiguracaoEstabelecimento configuracao)
    {
        if (string.IsNullOrWhiteSpace(configuracao.NomeEstabelecimento))
        {
            return Resultado<ConfiguracaoEstabelecimento>.Falha("Informe o nome do estabelecimento.");
        }

        if (configuracao.QuantidadeMaximaComandas is int quantidade && quantidade <= 0)
        {
            return Resultado<ConfiguracaoEstabelecimento>.Falha("A quantidade de comandas deve ser maior que zero.");
        }

        var agora = _relogio.UtcNow;
        _configuracoes.Definir("estabelecimento.nome", configuracao.NomeEstabelecimento, agora);
        _configuracoes.Definir("comandas.quantidade_maxima", configuracao.QuantidadeMaximaComandas?.ToString() ?? string.Empty, agora);
        _configuracoes.Definir("backup.pasta_externa", configuracao.PastaBackupExterna ?? string.Empty, agora);

        return Resultado<ConfiguracaoEstabelecimento>.Ok(configuracao);
    }
}
```

- [ ] **Step 7: Rodar os testes e confirmar que passam**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release --filter "FullyQualifiedName~ObterConfiguracaoTests|FullyQualifiedName~SalvarConfiguracaoTests"
```

Esperado: PASS nos 6 testes (3 + 3).

- [ ] **Step 8: Rodar a suíte inteira e commitar**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release
```

```bash
git add backend/src/VarthexComanda.Application/Configuracao/ConfiguracaoEstabelecimento.cs backend/src/VarthexComanda.Application/Configuracao/ObterConfiguracao.cs backend/src/VarthexComanda.Application/Configuracao/SalvarConfiguracao.cs backend/tests/VarthexComanda.Application.Tests/Configuracao/ObterConfiguracaoTests.cs backend/tests/VarthexComanda.Application.Tests/Configuracao/SalvarConfiguracaoTests.cs
git commit -m "feat: adiciona casos de uso de configuracao do estabelecimento"
```

---

## Task 3: `AbrirComanda` e `CriarBackupAutomatico` passam a consultar `ObterConfiguracao`

**Files:**
- Modify: `backend/src/VarthexComanda.Application/Atendimento/AbrirComanda.cs`
- Modify: `backend/src/VarthexComanda.Application/Backup/CriarBackupAutomatico.cs`
- Modify: `backend/tests/VarthexComanda.Application.Tests/Atendimento/AbrirComandaTests.cs`
- Modify: `backend/tests/VarthexComanda.Application.Tests/Backup/CriarBackupAutomaticoTests.cs`
- Modify: `backend/tests/VarthexComanda.Desktop.Tests/Atendimento/AtendimentoViewModelTests.cs`

**Interfaces:**
- Consumes: `ObterConfiguracao` (Task 2), `FakeConfiguracaoRepository` (Task 1), `IBackupService.CriarBackupExterno` e `FakeBackupService.ChamadasCriarBackupExterno` (já existem desde a Etapa 6).
- Produces: `AbrirComanda` com construtor de 3 parâmetros (`IComandaRepository, IClock, ObterConfiguracao`), `CriarBackupAutomatico` com construtor de 4 parâmetros (`IBackupService, IBackupRegistroRepository, IClock, ObterConfiguracao`) — usados pela Task 5 (registro de DI em `App.xaml.cs`).

- [ ] **Step 1: Atualizar os testes existentes de `AbrirComandaTests` para o novo construtor e adicionar os 3 novos testes (vão falhar — compilação quebrada)**

Em `backend/tests/VarthexComanda.Application.Tests/Atendimento/AbrirComandaTests.cs`, adicionar no topo do arquivo:

```csharp
using VarthexComanda.Application.Configuracao;
using VarthexComanda.Application.Tests.Configuracao;
```

Substituir **todas** as ocorrências (`replace_all`) de:

```csharp
new AbrirComanda(new FakeComandaRepository(), new FakeClock())
```

por:

```csharp
new AbrirComanda(new FakeComandaRepository(), new FakeClock(), new ObterConfiguracao(new FakeConfiguracaoRepository()))
```

Substituir a ocorrência de:

```csharp
new AbrirComanda(repositorio, new FakeClock())
```

por:

```csharp
new AbrirComanda(repositorio, new FakeClock(), new ObterConfiguracao(new FakeConfiguracaoRepository()))
```

Depois, adicionar estes 3 testes novos ao final da classe (antes do `}` de fechamento):

```csharp
    [Fact]
    public void Executar_SemConfiguracao_NumeroAltoContinuaFuncionando()
    {
        var caso = new AbrirComanda(new FakeComandaRepository(), new FakeClock(), new ObterConfiguracao(new FakeConfiguracaoRepository()));

        var resultado = caso.Executar(9999);

        Assert.True(resultado.Sucesso);
    }

    [Fact]
    public void Executar_ComConfiguracao_NumeroAcimaDoLimiteFalha()
    {
        var configuracoes = new FakeConfiguracaoRepository();
        configuracoes.Definir("comandas.quantidade_maxima", "30", DateTime.UtcNow);
        var caso = new AbrirComanda(new FakeComandaRepository(), new FakeClock(), new ObterConfiguracao(configuracoes));

        var resultado = caso.Executar(31);

        Assert.False(resultado.Sucesso);
        Assert.Contains("O número da comanda deve ser no máximo 30.", resultado.Erros);
    }

    [Fact]
    public void Executar_ComConfiguracao_NumeroDentroDoLimitePassa()
    {
        var configuracoes = new FakeConfiguracaoRepository();
        configuracoes.Definir("comandas.quantidade_maxima", "30", DateTime.UtcNow);
        var caso = new AbrirComanda(new FakeComandaRepository(), new FakeClock(), new ObterConfiguracao(configuracoes));

        var resultado = caso.Executar(30);

        Assert.True(resultado.Sucesso);
    }
```

- [ ] **Step 2: Atualizar o único call site em `AtendimentoViewModelTests.cs`**

Em `backend/tests/VarthexComanda.Desktop.Tests/Atendimento/AtendimentoViewModelTests.cs`, adicionar no topo:

```csharp
using VarthexComanda.Application.Configuracao;
using VarthexComanda.Application.Tests.Configuracao;
```

Substituir a linha (dentro do helper `CriarViewModel`):

```csharp
new AbrirComanda(comandas, relogio),
```

por:

```csharp
new AbrirComanda(comandas, relogio, new ObterConfiguracao(new FakeConfiguracaoRepository())),
```

- [ ] **Step 3: Atualizar os testes existentes de `CriarBackupAutomaticoTests` para o novo construtor e adicionar os 3 novos testes**

Em `backend/tests/VarthexComanda.Application.Tests/Backup/CriarBackupAutomaticoTests.cs`, adicionar no topo:

```csharp
using VarthexComanda.Application.Configuracao;
using VarthexComanda.Application.Tests.Configuracao;
```

Substituir **todas** as ocorrências (`replace_all`) de:

```csharp
new CriarBackupAutomatico(backupService, registros, new FakeClock())
```

por:

```csharp
new CriarBackupAutomatico(backupService, registros, new FakeClock(), new ObterConfiguracao(new FakeConfiguracaoRepository()))
```

Depois, adicionar estes 3 testes novos ao final da classe:

```csharp
    [Fact]
    public void Executar_IncondicionalComPastaExternaConfigurada_TentaCopiaExterna()
    {
        var registros = new FakeBackupRegistroRepository();
        var backupService = new FakeBackupService();
        var configuracoes = new FakeConfiguracaoRepository();
        configuracoes.Definir("backup.pasta_externa", "D:\\backups", DateTime.UtcNow);
        var caso = new CriarBackupAutomatico(backupService, registros, new FakeClock(), new ObterConfiguracao(configuracoes));

        caso.Executar(incondicional: true);

        Assert.Equal(1, backupService.ChamadasCriarBackupExterno);
    }

    [Fact]
    public void Executar_IncondicionalSemPastaExternaConfigurada_NaoTentaCopiaExterna()
    {
        var registros = new FakeBackupRegistroRepository();
        var backupService = new FakeBackupService();
        var caso = new CriarBackupAutomatico(backupService, registros, new FakeClock(), new ObterConfiguracao(new FakeConfiguracaoRepository()));

        caso.Executar(incondicional: true);

        Assert.Equal(0, backupService.ChamadasCriarBackupExterno);
    }

    [Fact]
    public void Executar_AberturaDoDiaComPastaExternaConfigurada_NuncaTentaCopiaExterna()
    {
        var registros = new FakeBackupRegistroRepository { ExisteBackupHojeRetorno = false };
        var backupService = new FakeBackupService();
        var configuracoes = new FakeConfiguracaoRepository();
        configuracoes.Definir("backup.pasta_externa", "D:\\backups", DateTime.UtcNow);
        var caso = new CriarBackupAutomatico(backupService, registros, new FakeClock(), new ObterConfiguracao(configuracoes));

        caso.Executar();

        Assert.Equal(0, backupService.ChamadasCriarBackupExterno);
    }
```

- [ ] **Step 4: Rodar os testes e confirmar que falham (erro de compilação — construtores ainda não mudaram)**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet build backend/VarthexComanda.slnx
```

Esperado: falha de build — `AbrirComanda`/`CriarBackupAutomatico` não têm
os construtores com o novo parâmetro ainda.

- [ ] **Step 5: Atualizar `AbrirComanda`**

Substituir o conteúdo de `backend/src/VarthexComanda.Application/Atendimento/AbrirComanda.cs` por:

```csharp
using VarthexComanda.Application.Configuracao;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Atendimento;

public class AbrirComanda
{
    private readonly IComandaRepository _comandas;
    private readonly IClock _relogio;
    private readonly ObterConfiguracao _obterConfiguracao;

    public AbrirComanda(IComandaRepository comandas, IClock relogio, ObterConfiguracao obterConfiguracao)
    {
        _comandas = comandas;
        _relogio = relogio;
        _obterConfiguracao = obterConfiguracao;
    }

    public Resultado<Comanda> Executar(int numero)
    {
        if (numero <= 0)
        {
            return Resultado<Comanda>.Falha("Informe um número de comanda válido.");
        }

        var configuracao = _obterConfiguracao.Executar();
        if (configuracao.QuantidadeMaximaComandas is int maximo && numero > maximo)
        {
            return Resultado<Comanda>.Falha($"O número da comanda deve ser no máximo {maximo}.");
        }

        try
        {
            return Resultado<Comanda>.Ok(_comandas.AbrirComanda(numero, _relogio.UtcNow));
        }
        catch (NumeroComandaOcupadoException ex)
        {
            return Resultado<Comanda>.Falha(ex.Message);
        }
    }
}
```

Nota: mantenha os `using`/namespace exatos já existentes no arquivo atual
(este bloco assume os mesmos `using VarthexComanda.Domain;` e a mesma
`Resultado<T>`/`NumeroComandaOcupadoException` já usados hoje — só
adicione `using VarthexComanda.Application.Configuracao;` e o novo campo/
parâmetro/checagem).

- [ ] **Step 6: Atualizar `CriarBackupAutomatico`**

Substituir o conteúdo de `backend/src/VarthexComanda.Application/Backup/CriarBackupAutomatico.cs` por:

```csharp
using VarthexComanda.Application.Configuracao;

namespace VarthexComanda.Application.Backup;

public class CriarBackupAutomatico
{
    private readonly IBackupService _backupService;
    private readonly IBackupRegistroRepository _registros;
    private readonly IClock _relogio;
    private readonly ObterConfiguracao _obterConfiguracao;

    public CriarBackupAutomatico(IBackupService backupService, IBackupRegistroRepository registros, IClock relogio, ObterConfiguracao obterConfiguracao)
    {
        _backupService = backupService;
        _registros = registros;
        _relogio = relogio;
        _obterConfiguracao = obterConfiguracao;
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

            if (incondicional)
            {
                var configuracao = _obterConfiguracao.Executar();
                if (!string.IsNullOrEmpty(configuracao.PastaBackupExterna))
                {
                    try
                    {
                        _backupService.CriarBackupExterno(configuracao.PastaBackupExterna);
                    }
                    catch (Exception)
                    {
                        // falha na cópia externa automática não deve interromper o encerramento do app
                    }
                }
            }
        }
        catch (Exception)
        {
            // backup automático nunca deve interromper o app
        }
    }
}
```

Nota: mantenha o `using`/lógica de `FusoBrasilia`/`IClock` exatamente como
já existe no arquivo atual — só adicione `using
VarthexComanda.Application.Configuracao;`, o novo campo/parâmetro e o bloco
de cópia externa dentro do `if (incondicional)`.

- [ ] **Step 7: Rodar os testes e confirmar que passam**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release --filter "FullyQualifiedName~AbrirComandaTests|FullyQualifiedName~CriarBackupAutomaticoTests|FullyQualifiedName~AtendimentoViewModelTests"
```

Esperado: PASS em todos (3 antigos + 3 novos de `AbrirComandaTests`; 4
antigos + 3 novos de `CriarBackupAutomaticoTests`; todos os antigos de
`AtendimentoViewModelTests` inalterados).

- [ ] **Step 8: Rodar a suíte inteira e commitar**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release
```

```bash
git add backend/src/VarthexComanda.Application/Atendimento/AbrirComanda.cs backend/src/VarthexComanda.Application/Backup/CriarBackupAutomatico.cs backend/tests/VarthexComanda.Application.Tests/Atendimento/AbrirComandaTests.cs backend/tests/VarthexComanda.Application.Tests/Backup/CriarBackupAutomaticoTests.cs backend/tests/VarthexComanda.Desktop.Tests/Atendimento/AtendimentoViewModelTests.cs
git commit -m "feat: aplica faixa de comandas e copia externa automatica configuraveis"
```

---

## Task 4: `ConfiguracaoViewModel` (Desktop, sem XAML)

**Files:**
- Create: `backend/src/VarthexComanda.Desktop/Configuracao/ConfiguracaoViewModel.cs`
- Test: `backend/tests/VarthexComanda.Desktop.Tests/Configuracao/ConfiguracaoViewModelTests.cs`

**Interfaces:**
- Consumes: `ObterConfiguracao`, `SalvarConfiguracao`, `ConfiguracaoEstabelecimento` (Task 2), `FakeConfiguracaoRepository` (Task 1), `FakeClock` (`VarthexComanda.Application.Tests.Catalogo`, já existe).
- Produces: `ConfiguracaoViewModel { NomeEstabelecimento, QuantidadeComandasTexto, PastaBackupExterna, Mensagem, SalvarCommand, Carregar(), DefinirPastaBackupExterna(string?) }` — consumido pela Task 5 (`ConfiguracaoView.xaml.cs` e o registro de DI).

- [ ] **Step 1: Escrever os testes (vão falhar — a classe não existe)**

Criar `backend/tests/VarthexComanda.Desktop.Tests/Configuracao/ConfiguracaoViewModelTests.cs`:

```csharp
using VarthexComanda.Application.Configuracao;
using VarthexComanda.Application.Tests.Catalogo;
using VarthexComanda.Application.Tests.Configuracao;
using VarthexComanda.Desktop.Configuracao;
using Xunit;

namespace VarthexComanda.Desktop.Tests.Configuracao;

public class ConfiguracaoViewModelTests
{
    private static (ConfiguracaoViewModel ViewModel, FakeConfiguracaoRepository Repositorio) CriarViewModel()
    {
        var repositorio = new FakeConfiguracaoRepository();
        var viewModel = new ConfiguracaoViewModel(
            new ObterConfiguracao(repositorio),
            new SalvarConfiguracao(repositorio, new FakeClock()));
        return (viewModel, repositorio);
    }

    [Fact]
    public void Construtor_CarregaConfiguracaoAtual()
    {
        var repositorio = new FakeConfiguracaoRepository();
        repositorio.Definir("estabelecimento.nome", "Lanchonete do Zé", DateTime.UtcNow);
        repositorio.Definir("comandas.quantidade_maxima", "30", DateTime.UtcNow);
        var viewModel = new ConfiguracaoViewModel(
            new ObterConfiguracao(repositorio),
            new SalvarConfiguracao(repositorio, new FakeClock()));

        Assert.Equal("Lanchonete do Zé", viewModel.NomeEstabelecimento);
        Assert.Equal("30", viewModel.QuantidadeComandasTexto);
    }

    [Fact]
    public void Salvar_DadosValidos_MostraMensagemDeSucesso()
    {
        var (viewModel, _) = CriarViewModel();
        viewModel.NomeEstabelecimento = "Lanchonete do Zé";
        viewModel.QuantidadeComandasTexto = "30";

        viewModel.SalvarCommand.Execute(null);

        Assert.Equal("Configurações salvas com sucesso.", viewModel.Mensagem);
    }

    [Fact]
    public void Salvar_NomeVazio_MostraErroDoCasoDeUso()
    {
        var (viewModel, _) = CriarViewModel();
        viewModel.NomeEstabelecimento = "";

        viewModel.SalvarCommand.Execute(null);

        Assert.Equal("Informe o nome do estabelecimento.", viewModel.Mensagem);
    }

    [Fact]
    public void Salvar_QuantidadeTextoInvalido_MostraErroSemGravarNada()
    {
        var (viewModel, repositorio) = CriarViewModel();
        viewModel.NomeEstabelecimento = "Lanchonete do Zé";
        viewModel.QuantidadeComandasTexto = "abc";

        viewModel.SalvarCommand.Execute(null);

        Assert.Equal("Informe uma quantidade de comandas válida.", viewModel.Mensagem);
        Assert.Null(repositorio.ObterValor("estabelecimento.nome"));
    }

    [Fact]
    public void DefinirPastaBackupExterna_AtualizaPropriedade()
    {
        var (viewModel, _) = CriarViewModel();

        viewModel.DefinirPastaBackupExterna("D:\\backups");

        Assert.Equal("D:\\backups", viewModel.PastaBackupExterna);
    }
}
```

- [ ] **Step 2: Rodar os testes e confirmar que falham (erro de compilação)**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet build backend/VarthexComanda.slnx
```

Esperado: falha de build — `ConfiguracaoViewModel` não existe.

- [ ] **Step 3: Implementar `ConfiguracaoViewModel`**

Criar `backend/src/VarthexComanda.Desktop/Configuracao/ConfiguracaoViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VarthexComanda.Application.Configuracao;

namespace VarthexComanda.Desktop.Configuracao;

public partial class ConfiguracaoViewModel : ObservableObject
{
    private readonly ObterConfiguracao _obterConfiguracao;
    private readonly SalvarConfiguracao _salvarConfiguracao;

    public ConfiguracaoViewModel(ObterConfiguracao obterConfiguracao, SalvarConfiguracao salvarConfiguracao)
    {
        _obterConfiguracao = obterConfiguracao;
        _salvarConfiguracao = salvarConfiguracao;

        Carregar();
    }

    [ObservableProperty]
    private string nomeEstabelecimento = string.Empty;

    [ObservableProperty]
    private string quantidadeComandasTexto = string.Empty;

    [ObservableProperty]
    private string? pastaBackupExterna;

    [ObservableProperty]
    private string mensagem = string.Empty;

    public void Carregar()
    {
        var configuracao = _obterConfiguracao.Executar();
        NomeEstabelecimento = configuracao.NomeEstabelecimento;
        QuantidadeComandasTexto = configuracao.QuantidadeMaximaComandas?.ToString() ?? string.Empty;
        PastaBackupExterna = configuracao.PastaBackupExterna;
    }

    public void DefinirPastaBackupExterna(string? pasta)
    {
        PastaBackupExterna = pasta;
    }

    [RelayCommand]
    private void Salvar()
    {
        int? quantidade = null;
        if (!string.IsNullOrWhiteSpace(QuantidadeComandasTexto))
        {
            if (!int.TryParse(QuantidadeComandasTexto, out var quantidadeValor))
            {
                Mensagem = "Informe uma quantidade de comandas válida.";
                return;
            }

            quantidade = quantidadeValor;
        }

        try
        {
            var resultado = _salvarConfiguracao.Executar(new ConfiguracaoEstabelecimento
            {
                NomeEstabelecimento = NomeEstabelecimento,
                QuantidadeMaximaComandas = quantidade,
                PastaBackupExterna = PastaBackupExterna
            });

            Mensagem = resultado.Sucesso
                ? "Configurações salvas com sucesso."
                : string.Join(" ", resultado.Erros);
        }
        catch (Exception)
        {
            Mensagem = "Não foi possível salvar as configurações. Tente novamente.";
        }
    }
}
```

- [ ] **Step 4: Rodar os testes e confirmar que passam**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release --filter "FullyQualifiedName~ConfiguracaoViewModelTests"
```

Esperado: PASS nos 5 testes.

- [ ] **Step 5: Rodar a suíte inteira e commitar**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release
```

```bash
git add backend/src/VarthexComanda.Desktop/Configuracao/ConfiguracaoViewModel.cs backend/tests/VarthexComanda.Desktop.Tests/Configuracao/ConfiguracaoViewModelTests.cs
git commit -m "feat: adiciona ConfiguracaoViewModel"
```

---

## Task 5: `ConfiguracaoView` + navegação + registro de DI

**Files:**
- Create: `backend/src/VarthexComanda.Desktop/Configuracao/ConfiguracaoView.xaml`
- Create: `backend/src/VarthexComanda.Desktop/Configuracao/ConfiguracaoView.xaml.cs`
- Modify: `backend/src/VarthexComanda.Desktop/MainWindow.xaml`
- Modify: `backend/src/VarthexComanda.Desktop/MainWindow.xaml.cs`
- Modify: `backend/src/VarthexComanda.Desktop/App.xaml.cs`

**Interfaces:**
- Consumes: `ConfiguracaoViewModel` (Task 4), `IConfiguracaoRepository`/`EfConfiguracaoRepository` (Task 1), `ObterConfiguracao`/`SalvarConfiguracao` (Task 2), o padrão `OpenFolderDialog` já usado em `BackupView.xaml.cs` (Etapa 6).
- Produces: nada consumido por tasks futuras — esta é a última task funcional da fatia.

- [ ] **Step 1: Criar `ConfiguracaoView.xaml`**

Criar `backend/src/VarthexComanda.Desktop/Configuracao/ConfiguracaoView.xaml`:

```xml
<UserControl x:Class="VarthexComanda.Desktop.Configuracao.ConfiguracaoView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d">
    <Grid Margin="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="0,0,0,8">
            <TextBlock Text="Nome do estabelecimento:" Width="180" VerticalAlignment="Center" />
            <TextBox Width="240" Text="{Binding NomeEstabelecimento, UpdateSourceTrigger=PropertyChanged}" />
        </StackPanel>

        <StackPanel Grid.Row="1" Orientation="Horizontal" Margin="0,0,0,8">
            <TextBlock Text="Quantidade de comandas:" Width="180" VerticalAlignment="Center" />
            <TextBox Width="100" Text="{Binding QuantidadeComandasTexto, UpdateSourceTrigger=PropertyChanged}" />
        </StackPanel>

        <StackPanel Grid.Row="2" Orientation="Horizontal" Margin="0,0,0,8">
            <TextBlock Text="Pasta de backup externa:" Width="180" VerticalAlignment="Center" />
            <TextBox Width="240" Text="{Binding PastaBackupExterna}" IsReadOnly="True" Margin="0,0,8,0" />
            <Button Content="Escolher..." Padding="8,4" Margin="0,0,8,0" Click="EscolherPasta_Click" />
            <Button Content="Limpar" Padding="8,4" Click="LimparPasta_Click" />
        </StackPanel>

        <Button Grid.Row="3" Content="Salvar" Padding="12,4" HorizontalAlignment="Left" Command="{Binding SalvarCommand}" Margin="0,4,0,8" />

        <TextBlock Grid.Row="4" Text="{Binding Mensagem}" Foreground="Red" TextWrapping="Wrap" />
    </Grid>
</UserControl>
```

- [ ] **Step 2: Criar `ConfiguracaoView.xaml.cs`**

Criar `backend/src/VarthexComanda.Desktop/Configuracao/ConfiguracaoView.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace VarthexComanda.Desktop.Configuracao;

public partial class ConfiguracaoView : UserControl
{
    public ConfiguracaoViewModel ViewModel { get; }

    public ConfiguracaoView(ConfiguracaoViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
    }

    private void EscolherPasta_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new OpenFolderDialog { Title = "Escolher pasta de backup externa" };
        if (dialogo.ShowDialog() == true)
        {
            ViewModel.DefinirPastaBackupExterna(dialogo.FolderName);
        }
    }

    private void LimparPasta_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.DefinirPastaBackupExterna(null);
    }
}
```

- [ ] **Step 3: Adicionar o 5º botão de navegação em `MainWindow.xaml`**

Em `backend/src/VarthexComanda.Desktop/MainWindow.xaml`, o botão "Backup"
atual (`Padding="12,4"`, sem `Margin` à direita) é o último de 4 botões.
Adicionar `Margin="0,0,8,0"` ao botão "Backup" existente (para ficar igual
aos outros 3, que já têm essa margem) e, logo depois dele, adicionar um
5º botão:

```xml
<Button Content="Configurações" Padding="12,4" Click="MostrarConfiguracao_Click" />
```

- [ ] **Step 4: Atualizar `MainWindow.xaml.cs` para o 5º parâmetro/handler**

Substituir o conteúdo de `backend/src/VarthexComanda.Desktop/MainWindow.xaml.cs` por:

```csharp
using System.Windows;
using VarthexComanda.Desktop.Atendimento;
using VarthexComanda.Desktop.Backup;
using VarthexComanda.Desktop.Catalogo;
using VarthexComanda.Desktop.Configuracao;

namespace VarthexComanda.Desktop;

public partial class MainWindow : Window
{
    private readonly AtendimentoView _atendimentoView;
    private readonly ProdutosView _produtosView;
    private readonly HistoricoView _historicoView;
    private readonly BackupView _backupView;
    private readonly ConfiguracaoView _configuracaoView;

    public MainWindow(AtendimentoView atendimentoView, ProdutosView produtosView, HistoricoView historicoView, BackupView backupView, ConfiguracaoView configuracaoView)
    {
        InitializeComponent();
        _atendimentoView = atendimentoView;
        _produtosView = produtosView;
        _historicoView = historicoView;
        _backupView = backupView;
        _configuracaoView = configuracaoView;
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

    private void MostrarConfiguracao_Click(object sender, RoutedEventArgs e)
    {
        _configuracaoView.ViewModel.Carregar();
        ConteudoPrincipal.Content = _configuracaoView;
    }
}
```

- [ ] **Step 5: Registrar tudo em `App.xaml.cs`**

Em `backend/src/VarthexComanda.Desktop/App.xaml.cs`, adicionar aos
`using`s existentes:

```csharp
using VarthexComanda.Application.Configuracao;
using VarthexComanda.Desktop.Configuracao;
using VarthexComanda.Infrastructure.Configuracao;
```

E, no bloco de registro de serviços (`ConfigureServices` ou equivalente,
junto dos outros `services.AddTransient<...>(...)` já existentes),
adicionar:

```csharp
services.AddTransient<IConfiguracaoRepository, EfConfiguracaoRepository>();
services.AddTransient<ObterConfiguracao>();
services.AddTransient<SalvarConfiguracao>();
services.AddTransient<ConfiguracaoViewModel>();
services.AddTransient<ConfiguracaoView>();
```

A ordem de registro não importa para o container de DI — pode ser
adicionado em qualquer ponto do bloco, perto dos registros de `Backup` por
proximidade de assunto.

Por fim, atualizar a resolução de `MainWindow` (o `_serviceProvider.GetRequiredService<MainWindow>()` ou construção equivalente já existente) — nenhuma mudança de código é necessária aqui além do que já existe, já que o container resolve o novo parâmetro do construtor de `MainWindow` automaticamente a partir dos registros acima.

- [ ] **Step 6: Build completo (WPF não tem teste automatizado de XAML nesta fatia — validação é manual)**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet build backend/VarthexComanda.slnx
```

Esperado: build limpo, sem erros.

- [ ] **Step 7: Rodar a suíte inteira (nenhuma quebra esperada) e commitar**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release
```

```bash
git add backend/src/VarthexComanda.Desktop/Configuracao/ConfiguracaoView.xaml backend/src/VarthexComanda.Desktop/Configuracao/ConfiguracaoView.xaml.cs backend/src/VarthexComanda.Desktop/MainWindow.xaml backend/src/VarthexComanda.Desktop/MainWindow.xaml.cs backend/src/VarthexComanda.Desktop/App.xaml.cs
git commit -m "feat: adiciona tela de Configuracoes e liga na navegacao"
```

- [ ] **Step 8: Roteiro de verificação manual (executar o app real)**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet run --project backend/src/VarthexComanda.Desktop
```

1. Abrir o app, clicar em "Configurações" — os três campos aparecem vazios
   (nunca configurado nesta instalação).
2. Preencher "Nome do estabelecimento" e "Quantidade de comandas" (ex.:
   `5`), clicar "Salvar" — aparece "Configurações salvas com sucesso.".
3. Ir em "Atendimento", tentar abrir a comanda `6` — recusado com "O número
   da comanda deve ser no máximo 5.". Abrir a comanda `3` — funciona
   normalmente.
4. Voltar em "Configurações", clicar "Escolher..." na pasta de backup
   externa, escolher uma pasta qualquer, clicar "Salvar".
5. Fechar o app (dispara o backup incondicional no `OnExit`) e reabrir —
   ir em "Backup" e confirmar que apareceram backups novos tanto na pasta
   gerenciada quanto na pasta externa escolhida.
6. Reabrir "Configurações" — nome, quantidade e pasta continuam exatamente
   como foram salvos no passo 4.

---

## Task 6: Verificação final e changelog

**Files:**
- Modify: `docs/CHANGELOG.md`

**Interfaces:**
- Consumes: nada de código — task de fechamento.
- Produces: nada — última task da fatia.

- [ ] **Step 1: Rodar a suíte completa uma última vez**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release
```

Esperado: todos os projetos PASS. Contagem total esperada nesta fatia:
Domain 4 (inalterado) + Application 71 (59 + 6 da Task 2 + 6 da Task 3) +
Infrastructure 63 (60 + 3 da Task 1) + Desktop 38 (34 + 4 da Task 4) = 176
testes.

- [ ] **Step 2: Atualizar o changelog**

Em `docs/CHANGELOG.md`, adicionar uma nova entrada `1.10` (seguindo o
mesmo formato das entradas `1.6`–`1.9` já existentes):

```markdown
## 1.10 — Configurações do estabelecimento (RF25)

- Nova tela "Configurações": nome do estabelecimento, quantidade máxima de
  comandas e pasta de backup externa padrão, persistidos na tabela
  `configuracao` já existente desde a base técnica.
- `AbrirComanda` passa a respeitar a quantidade máxima configurada (sem
  teto enquanto nada for configurado, preservando o comportamento
  anterior) — resolve QV02.
- O backup automático do encerramento do app (`CriarBackupAutomatico`,
  ramo incondicional) passa a também copiar para a pasta externa
  configurada, quando houver uma — cumprindo a política de cópia externa
  ao fim do dia (docs/08) sem exigir clique manual. A abertura do dia
  continua só na pasta gerenciada.
```

- [ ] **Step 3: Commitar**

```bash
git add docs/CHANGELOG.md
git commit -m "docs: registra configuracoes do estabelecimento no changelog"
```