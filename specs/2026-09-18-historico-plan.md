# Histórico e Resumo (Etapa 5) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement RF18-20 (consultar vendas concluídas por data, localizar
pelo número da comanda, ver detalhe de uma venda, resumo diário) sobre a
entidade `Venda` já existente e já escrita pela Etapa 4.

**Architecture:** Novo `IVendaRepository` (leitura, separado do transacional
`IComandaRepository`), dois casos de uso simples sem `Resultado<T>` (mesmo
padrão de `ListarCategoriasAtivas`/`PesquisarProdutos`), uma
`HistoricoViewModel` sem tipos WPF, uma única tela combinando lista+resumo, e
um utilitário compartilhado `FusoBrasilia` para converter entre o horário
local de Brasília (offset fixo, sem horário de verão) e UTC — usado tanto no
filtro por data quanto na exibição de horário, para não duplicar a constante
de offset em três lugares.

**Tech Stack:** C#/.NET 10, WPF (CommunityToolkit.Mvvm), EF Core/SQLite,
xUnit.

**Spec:** [specs/2026-09-18-historico-design.md](../specs/2026-09-18-historico-design.md)

## Global Constraints

- Novo `IVendaRepository`, **separado** de `IComandaRepository` — histórico é
  leitura analítica, não faz parte do fluxo transacional de atendimento.
- `venda.finalizada_em` continua em UTC — **nenhuma mudança** em `IClock` ou
  em qualquer dado já gravado por fatias anteriores.
- Conversão de fuso usa um offset **fixo** de `-03:00` (`America/Sao_Paulo`,
  sem horário de verão atualmente no Brasil), centralizada em
  `FusoBrasilia.ParaUtc`/`FusoBrasilia.ParaLocal` — nunca usar
  `.ToLocalTime()`/`.ToUniversalTime()`/`DateTimeKind.Local`, que dependem do
  fuso configurado no sistema operacional, não do fuso do negócio.
- `ListarVendasPorData`/`BuscarItensDaVenda` são consultas simples — **não**
  retornam `Resultado<T>` (mesmo padrão de `ListarCategoriasAtivas`/
  `PesquisarProdutos` da Etapa 2).
- `HistoricoViewModel` **não pode** ter nenhum `using System.Windows` nem
  referenciar `MessageBox`/`Window` diretamente — mesma restrição já
  estabelecida para `AtendimentoViewModel`/`EncerramentoViewModel`.
- Reaproveitar `CentavosParaMoedaConverter` (já existe em
  `backend/src/VarthexComanda.Desktop/Atendimento/CentavosParaMoedaConverter.cs`)
  para todo valor monetário — não criar um segundo formatador de moeda.
- Uma única tela "Histórico" combina lista + resumo (decisão confirmada com o
  usuário) — não duas telas separadas.
- Busca por número de comanda filtra a lista **já carregada** do dia
  selecionado, sem nova consulta ao banco — não é busca global cross-data.
- Prefixar todo comando `dotnet` com o PATH do SDK: PowerShell
  `$env:Path += ';C:\Program Files\dotnet'; ` / Bash
  `export PATH="$PATH:/c/Program Files/dotnet" && `.

---

## Task 1: Repositório de leitura — `IVendaRepository` + `EfVendaRepository` + `FakeVendaRepository`

**Files:**
- Create: `backend/src/VarthexComanda.Application/Atendimento/VendaResumo.cs`
- Create: `backend/src/VarthexComanda.Application/Atendimento/IVendaRepository.cs`
- Create: `backend/src/VarthexComanda.Infrastructure/Persistence/Atendimento/EfVendaRepository.cs`
- Create: `backend/tests/VarthexComanda.Application.Tests/Atendimento/FakeVendaRepository.cs`
- Test: `backend/tests/VarthexComanda.Infrastructure.Tests/Atendimento/EfVendaRepositoryTests.cs`

**Interfaces:**
- Consumes: `Venda`/`StatusVenda`/`Comanda`/`ItemComanda` (`VarthexComanda.Domain`, já existem), `contexto.Vendas`/`contexto.Comandas`/`contexto.ItensComanda` (`DbSet`s já existentes em `VarthexComandaDbContext`), `EfComandaRepository.AbrirComanda`/`AdicionarItem`/`EncerrarComanda` (já existem, usados só nos testes de infraestrutura para gerar dados realistas).
- Produces: `VendaResumo { Venda Venda; int NumeroComanda; }`,
  `IVendaRepository.ListarPorData(DateTime inicioUtc, DateTime fimUtc) → IReadOnlyList<VendaResumo>`,
  `IVendaRepository.BuscarItensDaVenda(int vendaId) → IReadOnlyList<ItemComanda>?` —
  consumidos pela Task 2 (casos de uso) e, via `FakeVendaRepository`, pela
  Task 3 (testes da ViewModel).

- [ ] **Step 1: Criar o tipo de leitura `VendaResumo`**

Criar `backend/src/VarthexComanda.Application/Atendimento/VendaResumo.cs`:

```csharp
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Atendimento;

public class VendaResumo
{
    public required Venda Venda { get; init; }
    public required int NumeroComanda { get; init; }
}
```

- [ ] **Step 2: Criar a interface `IVendaRepository`**

Criar `backend/src/VarthexComanda.Application/Atendimento/IVendaRepository.cs`:

```csharp
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Atendimento;

public interface IVendaRepository
{
    IReadOnlyList<VendaResumo> ListarPorData(DateTime inicioUtc, DateTime fimUtc);
    IReadOnlyList<ItemComanda>? BuscarItensDaVenda(int vendaId);
}
```

- [ ] **Step 3: Criar o fake para testes (Application.Tests e, depois, Desktop.Tests)**

Criar `backend/tests/VarthexComanda.Application.Tests/Atendimento/FakeVendaRepository.cs`:

```csharp
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Tests.Atendimento;

public class FakeVendaRepository : IVendaRepository
{
    private readonly List<VendaResumo> _vendas = new();
    private readonly List<ItemComanda> _itensPorComanda = new();

    public int ChamadasListarPorData { get; private set; }
    public bool LancarExcecao { get; set; }

    public void AdicionarVenda(Venda venda, int numeroComanda, IEnumerable<ItemComanda> itens)
    {
        _vendas.Add(new VendaResumo { Venda = venda, NumeroComanda = numeroComanda });
        _itensPorComanda.AddRange(itens);
    }

    public IReadOnlyList<VendaResumo> ListarPorData(DateTime inicioUtc, DateTime fimUtc)
    {
        if (LancarExcecao)
        {
            throw new InvalidOperationException("Falha simulada.");
        }

        ChamadasListarPorData++;
        return _vendas
            .Where(vr => vr.Venda.FinalizadaEm >= inicioUtc && vr.Venda.FinalizadaEm < fimUtc)
            .OrderBy(vr => vr.Venda.FinalizadaEm)
            .ToList();
    }

    public IReadOnlyList<ItemComanda>? BuscarItensDaVenda(int vendaId)
    {
        if (LancarExcecao)
        {
            throw new InvalidOperationException("Falha simulada.");
        }

        var vendaResumo = _vendas.FirstOrDefault(vr => vr.Venda.Id == vendaId);
        if (vendaResumo is null)
        {
            return null;
        }

        return _itensPorComanda.Where(i => i.ComandaId == vendaResumo.Venda.ComandaId).ToList();
    }
}
```

`ChamadasListarPorData` existe só para a Task 3 provar que a busca por número
filtra a lista já carregada sem disparar nova consulta. `LancarExcecao`
existe só para a Task 3 provar que `HistoricoViewModel` trata falha do
repositório sem propagar a exceção. Nenhum dos dois é usado nesta task.

- [ ] **Step 4: Escrever os testes de infraestrutura (vão falhar — `EfVendaRepository` não existe)**

Criar `backend/tests/VarthexComanda.Infrastructure.Tests/Atendimento/EfVendaRepositoryTests.cs`:

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Persistence;
using VarthexComanda.Infrastructure.Persistence.Atendimento;
using VarthexComanda.Infrastructure.Persistence.Catalogo;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests.Atendimento;

public class EfVendaRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ServiceProvider _provedor;
    private readonly IDbContextFactory<VarthexComandaDbContext> _fabrica;

    public EfVendaRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"varthex-venda-tests-{Guid.NewGuid()}.db");

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

    private Venda CriarVendaConcluida(int numeroComanda, DateTime finalizadaEmUtc)
    {
        var comandas = new EfComandaRepository(_fabrica);
        var categorias = new EfCategoriaRepository(_fabrica);
        var produtos = new EfProdutoRepository(_fabrica);
        var agora = finalizadaEmUtc.AddMinutes(-5);

        var categoria = categorias.Salvar(new Categoria { Id = 0, Nome = "Bebidas", Ativo = true, CriadoEm = agora, AtualizadoEm = agora });
        var produto = produtos.Salvar(new Produto { Id = 0, CategoriaId = categoria.Id, Nome = "Refrigerante", PrecoCentavos = 500, Ativo = true, CriadoEm = agora, AtualizadoEm = agora });
        var comanda = comandas.AbrirComanda(numeroComanda, agora);
        comandas.AdicionarItem(comanda.Id, produto, 2, agora);

        return comandas.EncerrarComanda(comanda.Id, finalizadaEmUtc);
    }

    [Fact]
    public void ListarPorData_VendaDentroDoIntervalo_RetornaComNumeroDaComanda()
    {
        var venda = CriarVendaConcluida(10, new DateTime(2026, 9, 18, 14, 0, 0, DateTimeKind.Utc));
        var repositorio = new EfVendaRepository(_fabrica);

        var resultado = repositorio.ListarPorData(
            new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 19, 0, 0, 0, DateTimeKind.Utc));

        Assert.Single(resultado);
        Assert.Equal(venda.Id, resultado[0].Venda.Id);
        Assert.Equal(10, resultado[0].NumeroComanda);
        Assert.Equal(1000, resultado[0].Venda.TotalCentavos);
    }

    [Fact]
    public void ListarPorData_VendaForaDoIntervalo_NaoRetorna()
    {
        CriarVendaConcluida(10, new DateTime(2026, 9, 17, 23, 59, 0, DateTimeKind.Utc));
        var repositorio = new EfVendaRepository(_fabrica);

        var resultado = repositorio.ListarPorData(
            new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 19, 0, 0, 0, DateTimeKind.Utc));

        Assert.Empty(resultado);
    }

    [Fact]
    public void ListarPorData_VendaExatamenteNoLimiteInicial_Retorna()
    {
        CriarVendaConcluida(10, new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc));
        var repositorio = new EfVendaRepository(_fabrica);

        var resultado = repositorio.ListarPorData(
            new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 19, 0, 0, 0, DateTimeKind.Utc));

        Assert.Single(resultado);
    }

    [Fact]
    public void ListarPorData_VendaExatamenteNoLimiteFinal_NaoRetorna()
    {
        CriarVendaConcluida(10, new DateTime(2026, 9, 19, 0, 0, 0, DateTimeKind.Utc));
        var repositorio = new EfVendaRepository(_fabrica);

        var resultado = repositorio.ListarPorData(
            new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 19, 0, 0, 0, DateTimeKind.Utc));

        Assert.Empty(resultado);
    }

    [Fact]
    public void ListarPorData_SemVendas_RetornaListaVazia()
    {
        var repositorio = new EfVendaRepository(_fabrica);

        var resultado = repositorio.ListarPorData(
            new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 19, 0, 0, 0, DateTimeKind.Utc));

        Assert.Empty(resultado);
    }

    [Fact]
    public void BuscarItensDaVenda_VendaExistente_RetornaItensDaComandaOriginal()
    {
        var venda = CriarVendaConcluida(10, new DateTime(2026, 9, 18, 14, 0, 0, DateTimeKind.Utc));
        var repositorio = new EfVendaRepository(_fabrica);

        var itens = repositorio.BuscarItensDaVenda(venda.Id);

        Assert.NotNull(itens);
        Assert.Single(itens!);
        Assert.Equal(2, itens![0].Quantidade);
        Assert.Equal(1000, itens[0].SubtotalCentavos);
    }

    [Fact]
    public void BuscarItensDaVenda_VendaInexistente_RetornaNull()
    {
        var repositorio = new EfVendaRepository(_fabrica);

        var itens = repositorio.BuscarItensDaVenda(999);

        Assert.Null(itens);
    }
}
```

- [ ] **Step 5: Rodar os testes e confirmar que falham (erro de compilação)**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet build backend/VarthexComanda.slnx
```

Esperado: falha de build — o tipo `EfVendaRepository` não existe.

- [ ] **Step 6: Implementar `EfVendaRepository`**

Criar `backend/src/VarthexComanda.Infrastructure/Persistence/Atendimento/EfVendaRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Domain;

namespace VarthexComanda.Infrastructure.Persistence.Atendimento;

public class EfVendaRepository : IVendaRepository
{
    private readonly IDbContextFactory<VarthexComandaDbContext> _fabricaContexto;

    public EfVendaRepository(IDbContextFactory<VarthexComandaDbContext> fabricaContexto)
    {
        _fabricaContexto = fabricaContexto;
    }

    public IReadOnlyList<VendaResumo> ListarPorData(DateTime inicioUtc, DateTime fimUtc)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        return contexto.Vendas
            .Where(v => v.FinalizadaEm >= inicioUtc && v.FinalizadaEm < fimUtc)
            .Join(contexto.Comandas, v => v.ComandaId, c => c.Id, (v, c) => new VendaResumo { Venda = v, NumeroComanda = c.Numero })
            .OrderBy(vr => vr.Venda.FinalizadaEm)
            .ToList();
    }

    public IReadOnlyList<ItemComanda>? BuscarItensDaVenda(int vendaId)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        var venda = contexto.Vendas.SingleOrDefault(v => v.Id == vendaId);
        if (venda is null)
        {
            return null;
        }

        return contexto.ItensComanda
            .Where(i => i.ComandaId == venda.ComandaId)
            .OrderBy(i => i.Id)
            .ToList();
    }
}
```

- [ ] **Step 7: Rodar os testes e confirmar que passam**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release --filter "FullyQualifiedName~EfVendaRepositoryTests"
```

Esperado: PASS nos 7 testes.

- [ ] **Step 8: Rodar a suíte inteira e commitar**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release
```

Esperado: todos os projetos PASS (nada ainda consome `IVendaRepository`
fora dos testes).

```bash
git add backend/src/VarthexComanda.Application/Atendimento/VendaResumo.cs backend/src/VarthexComanda.Application/Atendimento/IVendaRepository.cs backend/src/VarthexComanda.Infrastructure/Persistence/Atendimento/EfVendaRepository.cs backend/tests/VarthexComanda.Application.Tests/Atendimento/FakeVendaRepository.cs backend/tests/VarthexComanda.Infrastructure.Tests/Atendimento/EfVendaRepositoryTests.cs
git commit -m "feat: adiciona repositorio de leitura de vendas"
```

---

## Task 2: `FusoBrasilia` + casos de uso `ListarVendasPorData` e `BuscarItensDaVenda`

**Files:**
- Create: `backend/src/VarthexComanda.Application/Atendimento/FusoBrasilia.cs`
- Create: `backend/src/VarthexComanda.Application/Atendimento/ListarVendasPorData.cs`
- Create: `backend/src/VarthexComanda.Application/Atendimento/BuscarItensDaVenda.cs`
- Test: `backend/tests/VarthexComanda.Application.Tests/Atendimento/FusoBrasiliaTests.cs`
- Test: `backend/tests/VarthexComanda.Application.Tests/Atendimento/ListarVendasPorDataTests.cs`
- Test: `backend/tests/VarthexComanda.Application.Tests/Atendimento/BuscarItensDaVendaTests.cs`

**Interfaces:**
- Consumes: `IVendaRepository.ListarPorData`/`BuscarItensDaVenda` (Task 1), `FakeVendaRepository` (Task 1, para os testes).
- Produces: `FusoBrasilia.ParaUtc(DateTime local) → DateTime`, `FusoBrasilia.ParaLocal(DateTime utc) → DateTime`, `ListarVendasPorData.Executar(DateTime dataLocal) → IReadOnlyList<VendaResumo>`, `BuscarItensDaVenda.Executar(int vendaId) → IReadOnlyList<ItemComanda>?` — todos consumidos pela Task 3 (`HistoricoViewModel`) e pela Task 4 (`UtcParaHorarioLocalConverter` consome `FusoBrasilia.ParaLocal`).

- [ ] **Step 1: Escrever os testes de `FusoBrasilia` (vão falhar — a classe não existe)**

Criar `backend/tests/VarthexComanda.Application.Tests/Atendimento/FusoBrasiliaTests.cs`:

```csharp
using VarthexComanda.Application.Atendimento;
using Xunit;

namespace VarthexComanda.Application.Tests.Atendimento;

public class FusoBrasiliaTests
{
    [Fact]
    public void ParaUtc_MeiaNoiteLocal_RetornaTresHorasUtc()
    {
        var meiaNoiteLocal = new DateTime(2026, 9, 18, 0, 0, 0);

        var utc = FusoBrasilia.ParaUtc(meiaNoiteLocal);

        Assert.Equal(new DateTime(2026, 9, 18, 3, 0, 0), utc);
    }

    [Fact]
    public void ParaLocal_TresHorasUtc_RetornaMeiaNoiteLocal()
    {
        var tresHorasUtc = new DateTime(2026, 9, 18, 3, 0, 0);

        var local = FusoBrasilia.ParaLocal(tresHorasUtc);

        Assert.Equal(new DateTime(2026, 9, 18, 0, 0, 0), local);
    }
}
```

- [ ] **Step 2: Rodar e confirmar falha (erro de compilação)**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release --filter "FullyQualifiedName~FusoBrasiliaTests"
```

Esperado: falha de build — o tipo `FusoBrasilia` não existe.

- [ ] **Step 3: Implementar `FusoBrasilia`**

Criar `backend/src/VarthexComanda.Application/Atendimento/FusoBrasilia.cs`:

```csharp
namespace VarthexComanda.Application.Atendimento;

public static class FusoBrasilia
{
    // Offset fixo (Brasil não observa horário de verão atualmente) — nunca usar
    // DateTime.ToLocalTime()/ToUniversalTime(), que dependem do fuso do SO, não
    // do fuso do negócio (RN20).
    public static readonly TimeSpan Offset = TimeSpan.FromHours(-3);

    public static DateTime ParaUtc(DateTime local) => local - Offset;

    public static DateTime ParaLocal(DateTime utc) => utc + Offset;
}
```

- [ ] **Step 4: Rodar e confirmar que os testes de `FusoBrasilia` passam**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release --filter "FullyQualifiedName~FusoBrasiliaTests"
```

Esperado: PASS nos 2 testes.

- [ ] **Step 5: Escrever os testes de `ListarVendasPorData` (vão falhar — a classe não existe)**

Criar `backend/tests/VarthexComanda.Application.Tests/Atendimento/ListarVendasPorDataTests.cs`:

```csharp
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Domain;
using Xunit;

namespace VarthexComanda.Application.Tests.Atendimento;

public class ListarVendasPorDataTests
{
    private static Venda CriarVenda(DateTime finalizadaEmUtc) => new()
    {
        Id = 1,
        ComandaId = 1,
        Numero = 1,
        TotalCentavos = 1000,
        FinalizadaEm = finalizadaEmUtc,
        Status = StatusVenda.Concluida
    };

    [Fact]
    public void Executar_VendaAs23hBrasiliaDoDiaX_ApareceNoDiaLocalX()
    {
        var vendas = new FakeVendaRepository();
        // 23h de 18/09 em Brasília = 02h UTC de 19/09
        vendas.AdicionarVenda(CriarVenda(new DateTime(2026, 9, 19, 2, 0, 0)), 10, new List<ItemComanda>());
        var caso = new ListarVendasPorData(vendas);

        var resultado = caso.Executar(new DateTime(2026, 9, 18, 0, 0, 0));

        Assert.Single(resultado);
    }

    [Fact]
    public void Executar_VendaAMeiaNoiteLocalExata_CaiNoDiaCerto()
    {
        var vendas = new FakeVendaRepository();
        // meia-noite de 18/09 em Brasília = 03h UTC de 18/09
        vendas.AdicionarVenda(CriarVenda(new DateTime(2026, 9, 18, 3, 0, 0)), 10, new List<ItemComanda>());
        var caso = new ListarVendasPorData(vendas);

        var resultado = caso.Executar(new DateTime(2026, 9, 18, 0, 0, 0));

        Assert.Single(resultado);
    }

    [Fact]
    public void Executar_DiaSemVendas_RetornaListaVazia()
    {
        var vendas = new FakeVendaRepository();
        var caso = new ListarVendasPorData(vendas);

        var resultado = caso.Executar(new DateTime(2026, 9, 18, 0, 0, 0));

        Assert.Empty(resultado);
    }
}
```

- [ ] **Step 6: Escrever os testes de `BuscarItensDaVenda` (mesma classe ainda não existe)**

Criar `backend/tests/VarthexComanda.Application.Tests/Atendimento/BuscarItensDaVendaTests.cs`:

```csharp
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Domain;
using Xunit;

namespace VarthexComanda.Application.Tests.Atendimento;

public class BuscarItensDaVendaTests
{
    [Fact]
    public void Executar_VendaExistente_RetornaItensDaComandaOriginal()
    {
        var vendas = new FakeVendaRepository();
        var venda = new Venda { Id = 1, ComandaId = 5, Numero = 1, TotalCentavos = 500, FinalizadaEm = DateTime.UtcNow, Status = StatusVenda.Concluida };
        var item = new ItemComanda { Id = 1, ComandaId = 5, ProdutoId = 1, NomeProduto = "Refrigerante", PrecoUnitarioCentavos = 500, Quantidade = 1, SubtotalCentavos = 500, CriadoEm = DateTime.UtcNow, AtualizadoEm = DateTime.UtcNow };
        vendas.AdicionarVenda(venda, 10, new[] { item });
        var caso = new BuscarItensDaVenda(vendas);

        var itens = caso.Executar(venda.Id);

        Assert.NotNull(itens);
        Assert.Single(itens!);
    }

    [Fact]
    public void Executar_VendaInexistente_RetornaNull()
    {
        var vendas = new FakeVendaRepository();
        var caso = new BuscarItensDaVenda(vendas);

        var itens = caso.Executar(999);

        Assert.Null(itens);
    }
}
```

- [ ] **Step 7: Rodar e confirmar que os dois novos arquivos de teste falham (erro de compilação)**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet build backend/VarthexComanda.slnx
```

Esperado: falha de build — `ListarVendasPorData`/`BuscarItensDaVenda` não existem.

- [ ] **Step 8: Implementar `ListarVendasPorData`**

Criar `backend/src/VarthexComanda.Application/Atendimento/ListarVendasPorData.cs`:

```csharp
namespace VarthexComanda.Application.Atendimento;

public class ListarVendasPorData
{
    private readonly IVendaRepository _vendas;

    public ListarVendasPorData(IVendaRepository vendas)
    {
        _vendas = vendas;
    }

    public IReadOnlyList<VendaResumo> Executar(DateTime dataLocal)
    {
        var inicioLocal = dataLocal.Date;
        var fimLocal = inicioLocal.AddDays(1);
        var inicioUtc = FusoBrasilia.ParaUtc(inicioLocal);
        var fimUtc = FusoBrasilia.ParaUtc(fimLocal);
        return _vendas.ListarPorData(inicioUtc, fimUtc);
    }
}
```

- [ ] **Step 9: Implementar `BuscarItensDaVenda`**

Criar `backend/src/VarthexComanda.Application/Atendimento/BuscarItensDaVenda.cs`:

```csharp
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Atendimento;

public class BuscarItensDaVenda
{
    private readonly IVendaRepository _vendas;

    public BuscarItensDaVenda(IVendaRepository vendas)
    {
        _vendas = vendas;
    }

    public IReadOnlyList<ItemComanda>? Executar(int vendaId) => _vendas.BuscarItensDaVenda(vendaId);
}
```

- [ ] **Step 10: Rodar os testes e confirmar que passam**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release --filter "FullyQualifiedName~ListarVendasPorDataTests|FullyQualifiedName~BuscarItensDaVendaTests"
```

Esperado: PASS nos 5 testes (3 + 2).

- [ ] **Step 11: Rodar a suíte inteira e commitar**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release
```

```bash
git add backend/src/VarthexComanda.Application/Atendimento/FusoBrasilia.cs backend/src/VarthexComanda.Application/Atendimento/ListarVendasPorData.cs backend/src/VarthexComanda.Application/Atendimento/BuscarItensDaVenda.cs backend/tests/VarthexComanda.Application.Tests/Atendimento/FusoBrasiliaTests.cs backend/tests/VarthexComanda.Application.Tests/Atendimento/ListarVendasPorDataTests.cs backend/tests/VarthexComanda.Application.Tests/Atendimento/BuscarItensDaVendaTests.cs
git commit -m "feat: adiciona casos de uso de historico e conversor de fuso"
```

---

## Task 3: `HistoricoViewModel` (Desktop, sem WPF/XAML ainda)

**Files:**
- Create: `backend/src/VarthexComanda.Desktop/Atendimento/HistoricoViewModel.cs`
- Test: `backend/tests/VarthexComanda.Desktop.Tests/Atendimento/HistoricoViewModelTests.cs`

**Interfaces:**
- Consumes: `ListarVendasPorData.Executar(DateTime) → IReadOnlyList<VendaResumo>`, `BuscarItensDaVenda.Executar(int) → IReadOnlyList<ItemComanda>?` (Task 2), `FusoBrasilia.ParaLocal(DateTime) → DateTime` (Task 2), `IClock.UtcNow` (já existe), `FakeVendaRepository` (Task 1, para os testes).
- Produces: `HistoricoViewModel(ListarVendasPorData, BuscarItensDaVenda, IClock)`, propriedades `Vendas` (`ObservableCollection<VendaResumo>`), `ItensDaVendaSelecionada` (`ObservableCollection<ItemComanda>`), `DataSelecionada` (`DateTime`), `TextoBuscaNumero` (`string`), `VendaSelecionada` (`VendaResumo?`), `QuantidadeVendas` (`int`), `TotalDiaCentavos`/`TicketMedioCentavos` (`long`), `Mensagem` (`string`), método público `AtualizarVendas()` — todos consumidos pela Task 4 (`HistoricoView.xaml` e `MainWindow.xaml.cs`).

**Constraint carregada da Etapa 3:** a revisão final daquela fatia apontou
como achado Importante que `AtendimentoViewModel` não tinha nenhum
try/catch em torno das chamadas aos casos de uso (diferente de
`ProdutosViewModel`, que já protegia todos os comandos) — depois corrigido.
`HistoricoViewModel` **não repete essa lacuna**: toda chamada a
`_listarVendasPorData`/`_buscarItensDaVenda` fica em try/catch, com uma
propriedade `Mensagem` para o erro genérico, mesmo padrão das demais
ViewModels do projeto.

- [ ] **Step 1: Escrever os testes (vão falhar — a classe não existe)**

Criar `backend/tests/VarthexComanda.Desktop.Tests/Atendimento/HistoricoViewModelTests.cs`:

```csharp
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Tests.Atendimento;
using VarthexComanda.Application.Tests.Catalogo;
using VarthexComanda.Desktop.Atendimento;
using VarthexComanda.Domain;
using Xunit;

namespace VarthexComanda.Desktop.Tests.Atendimento;

public class HistoricoViewModelTests
{
    private static Venda CriarVenda(int id, int comandaId, long totalCentavos, DateTime finalizadaEmUtc) => new()
    {
        Id = id,
        ComandaId = comandaId,
        Numero = id,
        TotalCentavos = totalCentavos,
        FinalizadaEm = finalizadaEmUtc,
        Status = StatusVenda.Concluida
    };

    [Fact]
    public void Construtor_CarregaVendasDeHojeAutomaticamente()
    {
        var relogio = new FakeClock { UtcNow = new DateTime(2026, 9, 18, 14, 0, 0, DateTimeKind.Utc) };
        var vendas = new FakeVendaRepository();
        vendas.AdicionarVenda(CriarVenda(1, 1, 1000, new DateTime(2026, 9, 18, 15, 0, 0, DateTimeKind.Utc)), 10, new List<ItemComanda>());

        var viewModel = new HistoricoViewModel(new ListarVendasPorData(vendas), new BuscarItensDaVenda(vendas), relogio);

        Assert.Single(viewModel.Vendas);
    }

    [Fact]
    public void Resumo_DuasVendas_CalculaQuantidadeTotalETicketMedio()
    {
        var relogio = new FakeClock { UtcNow = new DateTime(2026, 9, 18, 14, 0, 0, DateTimeKind.Utc) };
        var vendas = new FakeVendaRepository();
        vendas.AdicionarVenda(CriarVenda(1, 1, 1000, new DateTime(2026, 9, 18, 15, 0, 0, DateTimeKind.Utc)), 10, new List<ItemComanda>());
        vendas.AdicionarVenda(CriarVenda(2, 2, 2000, new DateTime(2026, 9, 18, 16, 0, 0, DateTimeKind.Utc)), 20, new List<ItemComanda>());

        var viewModel = new HistoricoViewModel(new ListarVendasPorData(vendas), new BuscarItensDaVenda(vendas), relogio);

        Assert.Equal(2, viewModel.QuantidadeVendas);
        Assert.Equal(3000, viewModel.TotalDiaCentavos);
        Assert.Equal(1500, viewModel.TicketMedioCentavos);
    }

    [Fact]
    public void DiaSemVendas_ResumoZeradoSemExcecao()
    {
        var relogio = new FakeClock { UtcNow = new DateTime(2026, 9, 18, 14, 0, 0, DateTimeKind.Utc) };
        var vendas = new FakeVendaRepository();

        var viewModel = new HistoricoViewModel(new ListarVendasPorData(vendas), new BuscarItensDaVenda(vendas), relogio);

        Assert.Empty(viewModel.Vendas);
        Assert.Equal(0, viewModel.QuantidadeVendas);
        Assert.Equal(0, viewModel.TotalDiaCentavos);
        Assert.Equal(0, viewModel.TicketMedioCentavos);
    }

    [Fact]
    public void BuscaPorNumero_FiltraListaCarregadaSemNovaConsulta()
    {
        var relogio = new FakeClock { UtcNow = new DateTime(2026, 9, 18, 14, 0, 0, DateTimeKind.Utc) };
        var vendas = new FakeVendaRepository();
        vendas.AdicionarVenda(CriarVenda(1, 1, 1000, new DateTime(2026, 9, 18, 15, 0, 0, DateTimeKind.Utc)), 10, new List<ItemComanda>());
        vendas.AdicionarVenda(CriarVenda(2, 2, 2000, new DateTime(2026, 9, 18, 16, 0, 0, DateTimeKind.Utc)), 20, new List<ItemComanda>());
        var viewModel = new HistoricoViewModel(new ListarVendasPorData(vendas), new BuscarItensDaVenda(vendas), relogio);
        var chamadasAntes = vendas.ChamadasListarPorData;

        viewModel.TextoBuscaNumero = "20";

        Assert.Single(viewModel.Vendas);
        Assert.Equal(20, viewModel.Vendas[0].NumeroComanda);
        Assert.Equal(chamadasAntes, vendas.ChamadasListarPorData);
    }

    [Fact]
    public void SelecionarVenda_PopulaItensDaVendaSelecionada()
    {
        var relogio = new FakeClock { UtcNow = new DateTime(2026, 9, 18, 14, 0, 0, DateTimeKind.Utc) };
        var vendas = new FakeVendaRepository();
        var item = new ItemComanda { Id = 1, ComandaId = 1, ProdutoId = 1, NomeProduto = "Refrigerante", PrecoUnitarioCentavos = 500, Quantidade = 2, SubtotalCentavos = 1000, CriadoEm = relogio.UtcNow, AtualizadoEm = relogio.UtcNow };
        vendas.AdicionarVenda(CriarVenda(1, 1, 1000, new DateTime(2026, 9, 18, 15, 0, 0, DateTimeKind.Utc)), 10, new[] { item });
        var viewModel = new HistoricoViewModel(new ListarVendasPorData(vendas), new BuscarItensDaVenda(vendas), relogio);

        viewModel.VendaSelecionada = viewModel.Vendas[0];

        Assert.Single(viewModel.ItensDaVendaSelecionada);
        Assert.Equal(2, viewModel.ItensDaVendaSelecionada[0].Quantidade);
    }

    [Fact]
    public void AtualizarVendas_RepositorioFalha_MostraMensagemSemPropagarExcecao()
    {
        var relogio = new FakeClock { UtcNow = new DateTime(2026, 9, 18, 14, 0, 0, DateTimeKind.Utc) };
        var vendas = new FakeVendaRepository { LancarExcecao = true };

        var viewModel = new HistoricoViewModel(new ListarVendasPorData(vendas), new BuscarItensDaVenda(vendas), relogio);

        Assert.Empty(viewModel.Vendas);
        Assert.Equal("Não foi possível carregar as vendas. Tente novamente.", viewModel.Mensagem);
    }

    [Fact]
    public void SelecionarVenda_RepositorioFalha_MostraMensagemSemPropagarExcecao()
    {
        var relogio = new FakeClock { UtcNow = new DateTime(2026, 9, 18, 14, 0, 0, DateTimeKind.Utc) };
        var vendas = new FakeVendaRepository();
        vendas.AdicionarVenda(CriarVenda(1, 1, 1000, new DateTime(2026, 9, 18, 15, 0, 0, DateTimeKind.Utc)), 10, new List<ItemComanda>());
        var viewModel = new HistoricoViewModel(new ListarVendasPorData(vendas), new BuscarItensDaVenda(vendas), relogio);
        var vendaCarregada = viewModel.Vendas[0];
        vendas.LancarExcecao = true;

        viewModel.VendaSelecionada = vendaCarregada;

        Assert.Empty(viewModel.ItensDaVendaSelecionada);
        Assert.Equal("Não foi possível carregar os itens da venda. Tente novamente.", viewModel.Mensagem);
    }
}
```

- [ ] **Step 2: Rodar os testes e confirmar que falham**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release --filter "FullyQualifiedName~HistoricoViewModelTests"
```

Esperado: falha de build — o tipo `HistoricoViewModel` não existe.

- [ ] **Step 3: Implementar `HistoricoViewModel`**

Criar `backend/src/VarthexComanda.Desktop/Atendimento/HistoricoViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using VarthexComanda.Application.Abstractions;
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Domain;

namespace VarthexComanda.Desktop.Atendimento;

public partial class HistoricoViewModel : ObservableObject
{
    private readonly ListarVendasPorData _listarVendasPorData;
    private readonly BuscarItensDaVenda _buscarItensDaVenda;

    private IReadOnlyList<VendaResumo> _vendasCarregadas = Array.Empty<VendaResumo>();

    public HistoricoViewModel(ListarVendasPorData listarVendasPorData, BuscarItensDaVenda buscarItensDaVenda, IClock relogio)
    {
        _listarVendasPorData = listarVendasPorData;
        _buscarItensDaVenda = buscarItensDaVenda;

        Vendas = new ObservableCollection<VendaResumo>();
        ItensDaVendaSelecionada = new ObservableCollection<ItemComanda>();

        DataSelecionada = FusoBrasilia.ParaLocal(relogio.UtcNow).Date;
    }

    public ObservableCollection<VendaResumo> Vendas { get; }
    public ObservableCollection<ItemComanda> ItensDaVendaSelecionada { get; }

    [ObservableProperty]
    private DateTime dataSelecionada;

    [ObservableProperty]
    private string textoBuscaNumero = string.Empty;

    [ObservableProperty]
    private VendaResumo? vendaSelecionada;

    [ObservableProperty]
    private int quantidadeVendas;

    [ObservableProperty]
    private long totalDiaCentavos;

    [ObservableProperty]
    private long ticketMedioCentavos;

    [ObservableProperty]
    private string mensagem = string.Empty;

    partial void OnDataSelecionadaChanged(DateTime value) => AtualizarVendas();

    partial void OnTextoBuscaNumeroChanged(string value) => AplicarFiltro();

    partial void OnVendaSelecionadaChanged(VendaResumo? value)
    {
        ItensDaVendaSelecionada.Clear();
        if (value is null)
        {
            return;
        }

        try
        {
            var itens = _buscarItensDaVenda.Executar(value.Venda.Id);
            if (itens is null)
            {
                return;
            }

            Mensagem = string.Empty;
            foreach (var item in itens)
            {
                ItensDaVendaSelecionada.Add(item);
            }
        }
        catch (Exception)
        {
            Mensagem = "Não foi possível carregar os itens da venda. Tente novamente.";
        }
    }

    public void AtualizarVendas()
    {
        try
        {
            _vendasCarregadas = _listarVendasPorData.Executar(DataSelecionada);
            Mensagem = string.Empty;
        }
        catch (Exception)
        {
            _vendasCarregadas = Array.Empty<VendaResumo>();
            Mensagem = "Não foi possível carregar as vendas. Tente novamente.";
        }
        AplicarFiltro();
    }

    private void AplicarFiltro()
    {
        var filtro = TextoBuscaNumero.Trim();
        var filtradas = string.IsNullOrEmpty(filtro)
            ? _vendasCarregadas
            : _vendasCarregadas.Where(vr => vr.NumeroComanda.ToString().Contains(filtro)).ToList();

        Vendas.Clear();
        foreach (var venda in filtradas)
        {
            Vendas.Add(venda);
        }

        QuantidadeVendas = filtradas.Count;
        TotalDiaCentavos = filtradas.Sum(vr => vr.Venda.TotalCentavos);
        TicketMedioCentavos = QuantidadeVendas == 0 ? 0 : TotalDiaCentavos / QuantidadeVendas;
    }
}
```

Nota: `DataSelecionada = FusoBrasilia.ParaLocal(...)` no construtor passa
pelo setter gerado da propriedade, o que já dispara
`OnDataSelecionadaChanged` → `AtualizarVendas()` — não é preciso chamar
`AtualizarVendas()` de novo explicitamente no construtor. `TicketMedioCentavos`
usa divisão inteira de `long` (trunca, não arredonda) — mesma convenção de
centavos-como-inteiro já usada em toda a aplicação, sem introduzir `decimal`.

- [ ] **Step 4: Rodar os testes e confirmar que passam**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release --filter "FullyQualifiedName~HistoricoViewModelTests"
```

Esperado: PASS nos 7 testes.

- [ ] **Step 5: Rodar a suíte inteira e commitar**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release
```

```bash
git add backend/src/VarthexComanda.Desktop/Atendimento/HistoricoViewModel.cs backend/tests/VarthexComanda.Desktop.Tests/Atendimento/HistoricoViewModelTests.cs
git commit -m "feat: adiciona HistoricoViewModel"
```

---

## Task 4: `HistoricoView` + conversores + shell de navegação + DI + verificação manual

**Files:**
- Create: `backend/src/VarthexComanda.Desktop/Atendimento/UtcParaHorarioLocalConverter.cs`
- Create: `backend/src/VarthexComanda.Desktop/Atendimento/ContagemParaVisibilidadeConverters.cs`
- Create: `backend/src/VarthexComanda.Desktop/Atendimento/HistoricoView.xaml`
- Create: `backend/src/VarthexComanda.Desktop/Atendimento/HistoricoView.xaml.cs`
- Modify: `backend/src/VarthexComanda.Desktop/MainWindow.xaml`
- Modify: `backend/src/VarthexComanda.Desktop/MainWindow.xaml.cs`
- Modify: `backend/src/VarthexComanda.Desktop/App.xaml.cs`

**Interfaces:**
- Consumes: `HistoricoViewModel` (Task 3), `FusoBrasilia.ParaLocal` (Task 2), `CentavosParaMoedaConverter` (já existe).
- Produces: nada — última peça funcional da fatia.

- [ ] **Step 1: Criar o conversor de horário local**

Criar `backend/src/VarthexComanda.Desktop/Atendimento/UtcParaHorarioLocalConverter.cs`:

```csharp
using System.Globalization;
using System.Windows.Data;
using VarthexComanda.Application.Atendimento;

namespace VarthexComanda.Desktop.Atendimento;

public class UtcParaHorarioLocalConverter : IValueConverter
{
    private static readonly CultureInfo CulturaHorario = CultureInfo.GetCultureInfo("pt-BR");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateTime utc ? FusoBrasilia.ParaLocal(utc).ToString("HH:mm", CulturaHorario) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
```

- [ ] **Step 2: Criar os conversores de estado vazio**

Criar `backend/src/VarthexComanda.Desktop/Atendimento/ContagemParaVisibilidadeConverters.cs`:

```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VarthexComanda.Desktop.Atendimento;

public class ContagemParaVisibilidadeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int count && count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class ContagemParaVisibilidadeInversoConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int count && count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
```

- [ ] **Step 3: Criar `HistoricoView.xaml`**

Criar `backend/src/VarthexComanda.Desktop/Atendimento/HistoricoView.xaml`:

```xml
<UserControl x:Class="VarthexComanda.Desktop.Atendimento.HistoricoView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:local="clr-namespace:VarthexComanda.Desktop.Atendimento"
             mc:Ignorable="d">
    <UserControl.Resources>
        <local:CentavosParaMoedaConverter x:Key="Moeda" />
        <local:UtcParaHorarioLocalConverter x:Key="Horario" />
        <local:ContagemParaVisibilidadeConverter x:Key="VisivelSeVazio" />
        <local:ContagemParaVisibilidadeInversoConverter x:Key="VisivelSePreenchido" />
    </UserControl.Resources>
    <Grid Margin="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="0,0,0,8">
            <TextBlock Text="Data:" VerticalAlignment="Center" Margin="0,0,4,0" />
            <DatePicker SelectedDate="{Binding DataSelecionada}" Width="140" />
        </StackPanel>

        <StackPanel Grid.Row="1" Orientation="Horizontal" Margin="0,0,0,8">
            <TextBlock Text="Vendas: " FontWeight="Bold" />
            <TextBlock Text="{Binding QuantidadeVendas}" Margin="0,0,16,0" />
            <TextBlock Text="Total: " FontWeight="Bold" />
            <TextBlock Text="{Binding TotalDiaCentavos, Converter={StaticResource Moeda}}" Margin="0,0,16,0" />
            <TextBlock Text="Ticket médio: " FontWeight="Bold" />
            <TextBlock Text="{Binding TicketMedioCentavos, Converter={StaticResource Moeda}}" />
        </StackPanel>

        <StackPanel Grid.Row="2" Orientation="Horizontal" Margin="0,0,0,8">
            <TextBlock Text="Buscar comanda nº:" VerticalAlignment="Center" Margin="0,0,4,0" />
            <TextBox Width="100" Text="{Binding TextoBuscaNumero, UpdateSourceTrigger=PropertyChanged}" Margin="0,0,16,0" />
            <TextBlock Text="{Binding Mensagem}" Foreground="Red" VerticalAlignment="Center" />
        </StackPanel>

        <Grid Grid.Row="3">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="1*" />
                <ColumnDefinition Width="16" />
                <ColumnDefinition Width="1*" />
            </Grid.ColumnDefinitions>

            <StackPanel Grid.Column="0">
                <TextBlock Text="Ainda não há vendas concluídas nesta data."
                           Visibility="{Binding QuantidadeVendas, Converter={StaticResource VisivelSeVazio}}"
                           Foreground="Gray" Margin="0,12,0,0" />
                <ListView ItemsSource="{Binding Vendas}"
                          SelectedItem="{Binding VendaSelecionada}"
                          Visibility="{Binding QuantidadeVendas, Converter={StaticResource VisivelSePreenchido}}">
                    <ListView.View>
                        <GridView>
                            <GridViewColumn Header="Comanda" DisplayMemberBinding="{Binding NumeroComanda}" Width="80" />
                            <GridViewColumn Header="Total" DisplayMemberBinding="{Binding Venda.TotalCentavos, Converter={StaticResource Moeda}}" Width="90" />
                            <GridViewColumn Header="Horário" DisplayMemberBinding="{Binding Venda.FinalizadaEm, Converter={StaticResource Horario}}" Width="70" />
                        </GridView>
                    </ListView.View>
                </ListView>
            </StackPanel>

            <StackPanel Grid.Column="2">
                <TextBlock Text="Detalhe da venda" FontWeight="Bold" Margin="0,0,0,4" />
                <ListView ItemsSource="{Binding ItensDaVendaSelecionada}">
                    <ListView.View>
                        <GridView>
                            <GridViewColumn Header="Produto" DisplayMemberBinding="{Binding NomeProduto}" Width="160" />
                            <GridViewColumn Header="Qtd" DisplayMemberBinding="{Binding Quantidade}" Width="50" />
                            <GridViewColumn Header="Preço unit." DisplayMemberBinding="{Binding PrecoUnitarioCentavos, Converter={StaticResource Moeda}}" Width="90" />
                            <GridViewColumn Header="Subtotal" DisplayMemberBinding="{Binding SubtotalCentavos, Converter={StaticResource Moeda}}" Width="90" />
                        </GridView>
                    </ListView.View>
                </ListView>
            </StackPanel>
        </Grid>
    </Grid>
</UserControl>
```

Nota: `DatePicker.SelectedDate` é `DateTime?`; `DataSelecionada` é `DateTime`
não-anulável. O WPF converte automaticamente nessa direção; se o operador
limpar o campo de data (deixando em branco), o binding simplesmente rejeita
a atualização e mantém o valor anterior — comportamento aceito, não há
requisito de tratar data em branco nesta fatia.

- [ ] **Step 4: Criar `HistoricoView.xaml.cs`**

Criar `backend/src/VarthexComanda.Desktop/Atendimento/HistoricoView.xaml.cs`:

```csharp
using System.Windows.Controls;

namespace VarthexComanda.Desktop.Atendimento;

public partial class HistoricoView : UserControl
{
    public HistoricoViewModel ViewModel { get; }

    public HistoricoView(HistoricoViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
    }
}
```

- [ ] **Step 5: Adicionar o botão "Histórico" em `MainWindow.xaml`**

Em `backend/src/VarthexComanda.Desktop/MainWindow.xaml`, substituir:

```xml
        <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Margin="8">
            <Button Content="Atendimento" Padding="12,4" Margin="0,0,8,0" Click="MostrarAtendimento_Click" />
            <Button Content="Produtos" Padding="12,4" Click="MostrarProdutos_Click" />
        </StackPanel>
```

por:

```xml
        <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Margin="8">
            <Button Content="Atendimento" Padding="12,4" Margin="0,0,8,0" Click="MostrarAtendimento_Click" />
            <Button Content="Produtos" Padding="12,4" Margin="0,0,8,0" Click="MostrarProdutos_Click" />
            <Button Content="Histórico" Padding="12,4" Click="MostrarHistorico_Click" />
        </StackPanel>
```

- [ ] **Step 6: Atualizar `MainWindow.xaml.cs`**

Substituir o conteúdo de
`backend/src/VarthexComanda.Desktop/MainWindow.xaml.cs` por:

```csharp
using System.Windows;
using VarthexComanda.Desktop.Atendimento;
using VarthexComanda.Desktop.Catalogo;

namespace VarthexComanda.Desktop;

public partial class MainWindow : Window
{
    private readonly AtendimentoView _atendimentoView;
    private readonly ProdutosView _produtosView;
    private readonly HistoricoView _historicoView;

    public MainWindow(AtendimentoView atendimentoView, ProdutosView produtosView, HistoricoView historicoView)
    {
        InitializeComponent();
        _atendimentoView = atendimentoView;
        _produtosView = produtosView;
        _historicoView = historicoView;
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
}
```

`AtualizarVendas()` ao trocar de aba segue o mesmo motivo que já levou
`AtualizarCategorias()` a existir para Atendimento (achado da revisão final
da Etapa 3): `MainWindow` resolve cada view **uma única vez** para a vida
inteira do app, então sem esse refresh uma venda fechada há pouco não
apareceria no Histórico até reiniciar o aplicativo.

- [ ] **Step 7: Registrar tudo no DI (`App.xaml.cs`)**

Em `backend/src/VarthexComanda.Desktop/App.xaml.cs`, adicionar logo após
`services.AddTransient<IEncerramentoDialog, EncerramentoDialog>();`:

```csharp
        services.AddTransient<IVendaRepository, EfVendaRepository>();
        services.AddTransient<ListarVendasPorData>();
        services.AddTransient<BuscarItensDaVenda>();
        services.AddTransient<HistoricoViewModel>();
        services.AddTransient<HistoricoView>();
```

Nenhum `using` novo é necessário — `IVendaRepository`/`ListarVendasPorData`/
`BuscarItensDaVenda` resolvem por `VarthexComanda.Application.Atendimento`
(já importado), `EfVendaRepository` por
`VarthexComanda.Infrastructure.Persistence.Atendimento` (já importado),
`HistoricoViewModel`/`HistoricoView` por `VarthexComanda.Desktop.Atendimento`
(já importado).

- [ ] **Step 8: Build completo e suíte inteira**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet build backend/VarthexComanda.slnx
```

Esperado: build limpo (0 erros).

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release
```

Esperado: todos os projetos PASS.

- [ ] **Step 9: Verificação manual na aplicação real**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet run --project backend/src/VarthexComanda.Desktop
```

Roteiro manual:

1. Abrir uma comanda, lançar itens, e encerrá-la pelo fluxo da Etapa 4 (Ver
   total e encerrar → marcar checkbox → Confirmar e encerrar).
2. Clicar em "Histórico" — a data deve abrir em hoje, a venda recém-fechada
   deve aparecer na lista com o total e o horário corretos, e o resumo (1
   venda, total igual ao da venda, ticket médio igual ao total) deve bater.
3. Clicar na venda na lista — o painel de detalhe deve mostrar os mesmos
   itens/preços/subtotal que foram lançados na comanda original.
4. Trocar a data para ontem (ou qualquer dia sem vendas) — deve aparecer
   "Ainda não há vendas concluídas nesta data." e o resumo deve zerar.
5. Voltar a data para hoje — a venda deve reaparecer.
6. Digitar um número de comanda que não está na lista de hoje no campo de
   busca — a lista deve ficar vazia sem erro.
7. Encerrar uma segunda comanda e verificar que a nova venda aparece junto
   da primeira ao reabrir/atualizar a aba Histórico.

- [ ] **Step 10: Commitar**

```bash
git add backend/src/VarthexComanda.Desktop/Atendimento/UtcParaHorarioLocalConverter.cs backend/src/VarthexComanda.Desktop/Atendimento/ContagemParaVisibilidadeConverters.cs backend/src/VarthexComanda.Desktop/Atendimento/HistoricoView.xaml backend/src/VarthexComanda.Desktop/Atendimento/HistoricoView.xaml.cs backend/src/VarthexComanda.Desktop/MainWindow.xaml backend/src/VarthexComanda.Desktop/MainWindow.xaml.cs backend/src/VarthexComanda.Desktop/App.xaml.cs
git commit -m "feat: adiciona tela de historico e resumo ao shell de navegacao"
```

---

## Task 5: Verificação final e changelog

**Files:**
- Modify: `docs/CHANGELOG.md`

**Interfaces:**
- Consumes: nada novo — só confirma o estado final da fatia.
- Produces: nada — última tarefa do plano.

- [ ] **Step 1: Rodar a suíte inteira e conferir a contagem de testes**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release
```

Contagem esperada por projeto (partindo de 99 ao final da Etapa 4): Domain 4
(sem mudança), Application 41 + 7 (`FusoBrasiliaTests` 2 +
`ListarVendasPorDataTests` 3 + `BuscarItensDaVendaTests` 2 = 7) = 48,
Infrastructure 35 + 7 (`EfVendaRepositoryTests`) = 42, Desktop.Tests 19 + 7
(`HistoricoViewModelTests`) = 26. **Total esperado: 120.**

- [ ] **Step 2: Atualizar o changelog**

Em `docs/CHANGELOG.md`, adicionar uma nova seção no topo (mesmo formato das
entradas anteriores — verificar o cabeçalho exato das seções `1.6`/`1.7`
existentes antes de escrever, para manter o estilo):

```markdown
## 1.8 - 2026-09-18

- histórico e resumo (Etapa 5, RF18-20): tela combinando consulta de vendas
  por data, localização pelo número da comanda, detalhe dos itens de uma
  venda e resumo diário (quantidade, total, ticket médio); filtro de data
  respeita o fuso de Brasília (RN20) mesmo com os horários gravados em UTC;
  estado vazio quando não há vendas concluídas na data selecionada.
```

- [ ] **Step 3: Commitar**

```bash
git add docs/CHANGELOG.md
git commit -m "docs: registra historico e resumo no changelog"
```
