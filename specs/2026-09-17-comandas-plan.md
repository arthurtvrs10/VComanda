# Comandas (Etapa 3) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver comandas end to end — grade de números, abertura, catálogo rápido, lançamento/alteração/remoção de itens, cancelamento — replacing the Produtos-only `MainWindow` with a real navigation shell.

**Architecture:** `Application/Atendimento` holds validation + a custom exception pair (`NumeroComandaOcupadoException`, `ComandaNaoAbertaException`) that keep EF Core knowledge out of the Application layer → `Infrastructure/Persistence/Atendimento` implements `IComandaRepository` with one "verb" method per business operation, each opening exactly one `DbContext` and calling `SaveChanges()` once (atomic by construction, no Unit of Work needed) → `Desktop/Atendimento` holds `AtendimentoViewModel`, and `MainWindow` becomes a two-button shell switching between it and the existing `ProdutosView`.

**Tech Stack:** C# / .NET 10, EF Core (`IDbContextFactory`), CommunityToolkit.Mvvm, `Microsoft.Extensions.DependencyInjection`, xUnit.

**Spec:** [specs/2026-09-17-comandas-design.md](2026-09-17-comandas-design.md)

## Global Constraints

- Money as `long` centavos everywhere.
- `IComandaRepository` methods are "verb" operations (whole business transaction per call), not fine CRUD — each opens one `IDbContextFactory`-sourced `DbContext`, mutates everything it needs to, and calls `SaveChanges()` once.
- `AbrirComanda` never pre-checks for a free number — it inserts directly and lets the DB's partial unique index (`uq_comanda_numero_aberta`) be the source of truth, catching the resulting `DbUpdateException` in Infrastructure and re-throwing it as `NumeroComandaOcupadoException` (an Application-layer type) so Application never references `Microsoft.EntityFrameworkCore`.
- RN10 (comanda fechada é imutável) is enforced inside every repository mutation method by throwing `ComandaNaoAbertaException` when `Comanda.Status != StatusComanda.Aberta`.
- `AdicionarItem`'s repository method merges into an existing `ItemComanda` row when the same `produtoId` **and** the same `precoUnitarioCentavos` already exist for that comanda (increment quantity); otherwise it creates a new row. `comanda.TotalCentavos` is always recomputed as the sum of all its items' `SubtotalCentavos` inside the same `SaveChanges()`.
- `Resultado<T> where T : class` (already exists) — `Comanda` and `ComandaComItens` both satisfy this.
- ViewModels depend only on Application-layer use cases/repositories, never on `VarthexComandaDbContext`.
- This slice adds `VarthexComanda.Desktop.Tests` and writes real unit tests for `AtendimentoViewModel` and (retroactively) `ProdutosViewModel` — the Etapa 2 final review found that the "no ViewModel tests" carve-out let two real bugs through 8 task reviews; this slice closes that gap. `Desktop.Tests` references both `VarthexComanda.Desktop` and `VarthexComanda.Application.Tests` (to reuse the existing fakes) — a deliberate, documented exception to "each test project references only the project it verifies."

---

### Task 1: Application — tipos compartilhados + `AbrirComanda` + `CancelarComanda`

**Files:**
- Create: `backend/src/VarthexComanda.Application/Atendimento/ComandaComItens.cs`
- Create: `backend/src/VarthexComanda.Application/Atendimento/IComandaRepository.cs`
- Create: `backend/src/VarthexComanda.Application/Atendimento/NumeroComandaOcupadoException.cs`
- Create: `backend/src/VarthexComanda.Application/Atendimento/ComandaNaoAbertaException.cs`
- Create: `backend/src/VarthexComanda.Application/Atendimento/AbrirComanda.cs`
- Create: `backend/src/VarthexComanda.Application/Atendimento/CancelarComanda.cs`
- Test: `backend/tests/VarthexComanda.Application.Tests/Atendimento/FakeComandaRepository.cs`
- Test: `backend/tests/VarthexComanda.Application.Tests/Atendimento/AbrirComandaTests.cs`
- Test: `backend/tests/VarthexComanda.Application.Tests/Atendimento/CancelarComandaTests.cs`

**Interfaces:**
- Consumes: `Resultado<T>` (`VarthexComanda.Application.Catalogo`), `Comanda`/`ItemComanda`/`StatusComanda` (`VarthexComanda.Domain`), `IClock` (`VarthexComanda.Application.Abstractions`), `FakeClock` (`VarthexComanda.Application.Tests.Catalogo` — same test project, just a different folder, reuse it, do not redefine).
- Produces: `ComandaComItens { Comanda Comanda; IReadOnlyList<ItemComanda> Itens; }` (both `init`-only), `IComandaRepository { IReadOnlyList<Comanda> ListarAbertas(); ComandaComItens? BuscarComItens(int comandaId); Comanda AbrirComanda(int numero, DateTime agora); ComandaComItens AdicionarItem(int comandaId, Produto produto, int quantidade, DateTime agora); ComandaComItens AlterarQuantidade(int itemId, int quantidade, DateTime agora); ComandaComItens RemoverItem(int itemId, DateTime agora); Comanda CancelarComanda(int comandaId, DateTime agora); }`, `NumeroComandaOcupadoException`, `ComandaNaoAbertaException`, `AbrirComanda(IComandaRepository, IClock) { Resultado<Comanda> Executar(int numero); }`, `CancelarComanda(IComandaRepository, IClock) { Resultado<Comanda> Executar(int comandaId); }`, and the fully-implemented `FakeComandaRepository` (all 7 interface methods, even though only 2 use cases exist yet) — consumed by Task 2 (same fake, more use cases), Task 4 (the real `EfComandaRepository` must match this interface exactly), and Task 6 (`AtendimentoViewModel` tests reuse `FakeComandaRepository` via a project reference from `Desktop.Tests`).

- [ ] **Step 1: Write the failing tests**

`backend/tests/VarthexComanda.Application.Tests/Atendimento/FakeComandaRepository.cs`:

```csharp
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Tests.Atendimento;

public class FakeComandaRepository : IComandaRepository
{
    private readonly List<Comanda> _comandas = new();
    private readonly List<ItemComanda> _itens = new();
    private int _proximoComandaId = 1;
    private int _proximoItemId = 1;

    public IReadOnlyList<Comanda> ListarAbertas() =>
        _comandas.Where(c => c.Status == StatusComanda.Aberta).OrderBy(c => c.Numero).ToList();

    public ComandaComItens? BuscarComItens(int comandaId)
    {
        var comanda = _comandas.FirstOrDefault(c => c.Id == comandaId);
        if (comanda is null)
        {
            return null;
        }
        var itens = _itens.Where(i => i.ComandaId == comandaId).ToList();
        return new ComandaComItens { Comanda = comanda, Itens = itens };
    }

    public Comanda AbrirComanda(int numero, DateTime agora)
    {
        if (_comandas.Any(c => c.Numero == numero && c.Status == StatusComanda.Aberta))
        {
            throw new NumeroComandaOcupadoException();
        }

        var comanda = new Comanda
        {
            Id = _proximoComandaId++,
            Numero = numero,
            Status = StatusComanda.Aberta,
            AbertaEm = agora,
            FechadaEm = null,
            TotalCentavos = 0
        };
        _comandas.Add(comanda);
        return comanda;
    }

    public ComandaComItens AdicionarItem(int comandaId, Produto produto, int quantidade, DateTime agora)
    {
        var comanda = _comandas.FirstOrDefault(c => c.Id == comandaId)
            ?? throw new InvalidOperationException("Comanda não encontrada.");
        if (comanda.Status != StatusComanda.Aberta)
        {
            throw new ComandaNaoAbertaException();
        }

        var itens = _itens.Where(i => i.ComandaId == comandaId).ToList();
        var itemExistente = itens.FirstOrDefault(i => i.ProdutoId == produto.Id && i.PrecoUnitarioCentavos == produto.PrecoCentavos);

        if (itemExistente is not null)
        {
            itemExistente.Quantidade += quantidade;
            itemExistente.SubtotalCentavos = itemExistente.PrecoUnitarioCentavos * itemExistente.Quantidade;
            itemExistente.AtualizadoEm = agora;
        }
        else
        {
            var novoItem = new ItemComanda
            {
                Id = _proximoItemId++,
                ComandaId = comandaId,
                ProdutoId = produto.Id,
                NomeProduto = produto.Nome,
                PrecoUnitarioCentavos = produto.PrecoCentavos,
                Quantidade = quantidade,
                SubtotalCentavos = produto.PrecoCentavos * quantidade,
                CriadoEm = agora,
                AtualizadoEm = agora
            };
            _itens.Add(novoItem);
            itens.Add(novoItem);
        }

        comanda.TotalCentavos = itens.Sum(i => i.SubtotalCentavos);
        return new ComandaComItens { Comanda = comanda, Itens = itens };
    }

    public ComandaComItens AlterarQuantidade(int itemId, int quantidade, DateTime agora)
    {
        var item = _itens.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException("Item não encontrado.");
        var comanda = _comandas.FirstOrDefault(c => c.Id == item.ComandaId)
            ?? throw new InvalidOperationException("Comanda não encontrada.");
        if (comanda.Status != StatusComanda.Aberta)
        {
            throw new ComandaNaoAbertaException();
        }

        item.Quantidade = quantidade;
        item.SubtotalCentavos = item.PrecoUnitarioCentavos * quantidade;
        item.AtualizadoEm = agora;

        var itens = _itens.Where(i => i.ComandaId == comanda.Id).ToList();
        comanda.TotalCentavos = itens.Sum(i => i.SubtotalCentavos);
        return new ComandaComItens { Comanda = comanda, Itens = itens };
    }

    public ComandaComItens RemoverItem(int itemId, DateTime agora)
    {
        var item = _itens.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException("Item não encontrado.");
        var comanda = _comandas.FirstOrDefault(c => c.Id == item.ComandaId)
            ?? throw new InvalidOperationException("Comanda não encontrada.");
        if (comanda.Status != StatusComanda.Aberta)
        {
            throw new ComandaNaoAbertaException();
        }

        _itens.Remove(item);
        var itensRestantes = _itens.Where(i => i.ComandaId == comanda.Id).ToList();
        comanda.TotalCentavos = itensRestantes.Sum(i => i.SubtotalCentavos);
        return new ComandaComItens { Comanda = comanda, Itens = itensRestantes };
    }

    public Comanda CancelarComanda(int comandaId, DateTime agora)
    {
        var comanda = _comandas.FirstOrDefault(c => c.Id == comandaId)
            ?? throw new InvalidOperationException("Comanda não encontrada.");
        if (comanda.Status != StatusComanda.Aberta)
        {
            throw new ComandaNaoAbertaException();
        }

        comanda.Status = StatusComanda.Cancelada;
        comanda.FechadaEm = agora;
        return comanda;
    }
}
```

`backend/tests/VarthexComanda.Application.Tests/Atendimento/AbrirComandaTests.cs`:

```csharp
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Tests.Catalogo;
using Xunit;

namespace VarthexComanda.Application.Tests.Atendimento;

public class AbrirComandaTests
{
    [Fact]
    public void Executar_NumeroValido_AbreComanda()
    {
        var caso = new AbrirComanda(new FakeComandaRepository(), new FakeClock());

        var resultado = caso.Executar(10);

        Assert.True(resultado.Sucesso);
        Assert.Equal(10, resultado.Valor!.Numero);
        Assert.Equal(StatusComanda.Aberta, resultado.Valor.Status);
    }

    [Fact]
    public void Executar_NumeroZeroOuNegativo_Falha()
    {
        var caso = new AbrirComanda(new FakeComandaRepository(), new FakeClock());

        var resultado = caso.Executar(0);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Informe um número de comanda válido.", resultado.Erros);
    }

    [Fact]
    public void Executar_NumeroJaAberto_Falha()
    {
        var repositorio = new FakeComandaRepository();
        var caso = new AbrirComanda(repositorio, new FakeClock());
        caso.Executar(10);

        var resultado = caso.Executar(10);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Já existe uma comanda aberta com esse número.", resultado.Erros);
    }
}
```

`backend/tests/VarthexComanda.Application.Tests/Atendimento/CancelarComandaTests.cs`:

```csharp
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Tests.Catalogo;
using Xunit;

namespace VarthexComanda.Application.Tests.Atendimento;

public class CancelarComandaTests
{
    [Fact]
    public void Executar_ComandaAberta_CancelaELiberaNumero()
    {
        var repositorio = new FakeComandaRepository();
        var abrir = new AbrirComanda(repositorio, new FakeClock());
        var comanda = abrir.Executar(10).Valor!;
        var caso = new CancelarComanda(repositorio, new FakeClock());

        var resultado = caso.Executar(comanda.Id);

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusComanda.Cancelada, resultado.Valor!.Status);
        Assert.NotNull(resultado.Valor.FechadaEm);
        Assert.Empty(repositorio.ListarAbertas());

        var reabertura = abrir.Executar(10);
        Assert.True(reabertura.Sucesso);
    }

    [Fact]
    public void Executar_ComandaInexistente_Falha()
    {
        var caso = new CancelarComanda(new FakeComandaRepository(), new FakeClock());

        var resultado = caso.Executar(999);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Comanda não encontrada.", resultado.Erros);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile**

```powershell
$env:Path += ';C:\Program Files\dotnet'
dotnet test backend/tests/VarthexComanda.Application.Tests --filter "FullyQualifiedName~Atendimento"
```

Expected: compilation errors — `IComandaRepository`, `ComandaComItens`, `NumeroComandaOcupadoException`, `ComandaNaoAbertaException`, `AbrirComanda`, `CancelarComanda` do not exist.

- [ ] **Step 3: Implement the shared types and exceptions**

`backend/src/VarthexComanda.Application/Atendimento/ComandaComItens.cs`:

```csharp
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Atendimento;

public class ComandaComItens
{
    public required Comanda Comanda { get; init; }
    public required IReadOnlyList<ItemComanda> Itens { get; init; }
}
```

`backend/src/VarthexComanda.Application/Atendimento/IComandaRepository.cs`:

```csharp
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Atendimento;

public interface IComandaRepository
{
    IReadOnlyList<Comanda> ListarAbertas();
    ComandaComItens? BuscarComItens(int comandaId);
    Comanda AbrirComanda(int numero, DateTime agora);
    ComandaComItens AdicionarItem(int comandaId, Produto produto, int quantidade, DateTime agora);
    ComandaComItens AlterarQuantidade(int itemId, int quantidade, DateTime agora);
    ComandaComItens RemoverItem(int itemId, DateTime agora);
    Comanda CancelarComanda(int comandaId, DateTime agora);
}
```

`backend/src/VarthexComanda.Application/Atendimento/NumeroComandaOcupadoException.cs`:

```csharp
namespace VarthexComanda.Application.Atendimento;

public class NumeroComandaOcupadoException : Exception
{
    public NumeroComandaOcupadoException() : base("Já existe uma comanda aberta com esse número.")
    {
    }
}
```

`backend/src/VarthexComanda.Application/Atendimento/ComandaNaoAbertaException.cs`:

```csharp
namespace VarthexComanda.Application.Atendimento;

public class ComandaNaoAbertaException : Exception
{
    public ComandaNaoAbertaException() : base("A comanda não está aberta.")
    {
    }
}
```

- [ ] **Step 4: Implement `AbrirComanda` and `CancelarComanda`**

`backend/src/VarthexComanda.Application/Atendimento/AbrirComanda.cs`:

```csharp
using VarthexComanda.Application.Abstractions;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Atendimento;

public class AbrirComanda
{
    private readonly IComandaRepository _comandas;
    private readonly IClock _relogio;

    public AbrirComanda(IComandaRepository comandas, IClock relogio)
    {
        _comandas = comandas;
        _relogio = relogio;
    }

    public Resultado<Comanda> Executar(int numero)
    {
        if (numero <= 0)
        {
            return Resultado<Comanda>.Falha("Informe um número de comanda válido.");
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

`backend/src/VarthexComanda.Application/Atendimento/CancelarComanda.cs`:

```csharp
using VarthexComanda.Application.Abstractions;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Atendimento;

public class CancelarComanda
{
    private readonly IComandaRepository _comandas;
    private readonly IClock _relogio;

    public CancelarComanda(IComandaRepository comandas, IClock relogio)
    {
        _comandas = comandas;
        _relogio = relogio;
    }

    public Resultado<Comanda> Executar(int comandaId)
    {
        try
        {
            return Resultado<Comanda>.Ok(_comandas.CancelarComanda(comandaId, _relogio.UtcNow));
        }
        catch (ComandaNaoAbertaException ex)
        {
            return Resultado<Comanda>.Falha(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Resultado<Comanda>.Falha(ex.Message);
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

```powershell
$env:Path += ';C:\Program Files\dotnet'
dotnet test backend/tests/VarthexComanda.Application.Tests --filter "FullyQualifiedName~Atendimento"
```

Expected: 5 tests pass (3 `AbrirComandaTests` + 2 `CancelarComandaTests`).

- [ ] **Step 6: Commit**

```powershell
git add backend/src/VarthexComanda.Application/Atendimento backend/tests/VarthexComanda.Application.Tests/Atendimento
git commit -m "feat: adiciona tipos de atendimento e casos de uso abrir/cancelar comanda"
```

---

### Task 2: Application — `AdicionarItem`, `AlterarQuantidade`, `RemoverItem`

**Files:**
- Create: `backend/src/VarthexComanda.Application/Atendimento/AdicionarItem.cs`
- Create: `backend/src/VarthexComanda.Application/Atendimento/AlterarQuantidade.cs`
- Create: `backend/src/VarthexComanda.Application/Atendimento/RemoverItem.cs`
- Test: `backend/tests/VarthexComanda.Application.Tests/Atendimento/AdicionarItemTests.cs`
- Test: `backend/tests/VarthexComanda.Application.Tests/Atendimento/AlterarQuantidadeTests.cs`
- Test: `backend/tests/VarthexComanda.Application.Tests/Atendimento/RemoverItemTests.cs`

**Interfaces:**
- Consumes: `IComandaRepository`, `ComandaComItens`, `ComandaNaoAbertaException`, `FakeComandaRepository` from Task 1 (reuse the exact file, do not redefine); `IProdutoRepository`, `FakeCategoriaRepository`, `FakeProdutoRepository`, `CadastrarCategoria`, `CadastrarProduto` from the catalog slice (`VarthexComanda.Application.Tests.Catalogo` — same test project, different folder).
- Produces: `AdicionarItem(IComandaRepository, IProdutoRepository, IClock) { Resultado<ComandaComItens> Executar(int comandaId, int produtoId, int quantidade); }`, `AlterarQuantidade(IComandaRepository, IClock) { Resultado<ComandaComItens> Executar(int itemId, int quantidade); }`, `RemoverItem(IComandaRepository, IClock) { Resultado<ComandaComItens> Executar(int itemId); }` — consumed by Task 6's `AtendimentoViewModel`.

- [ ] **Step 1: Write the failing tests**

`backend/tests/VarthexComanda.Application.Tests/Atendimento/AdicionarItemTests.cs`:

```csharp
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Application.Tests.Catalogo;
using Xunit;

namespace VarthexComanda.Application.Tests.Atendimento;

public class AdicionarItemTests
{
    private static (FakeComandaRepository comandas, FakeProdutoRepository produtos, int comandaId, int produtoAtivoId, int produtoInativoId) Preparar()
    {
        var comandas = new FakeComandaRepository();
        var comanda = new AbrirComanda(comandas, new FakeClock()).Executar(10).Valor!;

        var categorias = new FakeCategoriaRepository();
        var categoria = new CadastrarCategoria(categorias, new FakeClock()).Executar("Bebidas").Valor!;
        var produtos = new FakeProdutoRepository();
        var cadastrarProduto = new CadastrarProduto(produtos, categorias, new FakeClock());
        var ativo = cadastrarProduto.Executar("Refrigerante", categoria.Id, 500).Valor!;
        var inativo = cadastrarProduto.Executar("Descontinuado", categoria.Id, 300).Valor!;
        new DesativarProduto(produtos, new FakeClock()).Executar(inativo.Id);

        return (comandas, produtos, comanda.Id, ativo.Id, inativo.Id);
    }

    [Fact]
    public void Executar_ProdutoAtivo_AdicionaItemERecalculaTotal()
    {
        var (comandas, produtos, comandaId, produtoId, _) = Preparar();
        var caso = new AdicionarItem(comandas, produtos, new FakeClock());

        var resultado = caso.Executar(comandaId, produtoId, 2);

        Assert.True(resultado.Sucesso);
        Assert.Single(resultado.Valor!.Itens);
        Assert.Equal(2, resultado.Valor.Itens[0].Quantidade);
        Assert.Equal(1000, resultado.Valor.Itens[0].SubtotalCentavos);
        Assert.Equal(1000, resultado.Valor.Comanda.TotalCentavos);
    }

    [Fact]
    public void Executar_DuasVezesMesmoProduto_IncrementaQuantidadeEmVezDeDuplicar()
    {
        var (comandas, produtos, comandaId, produtoId, _) = Preparar();
        var caso = new AdicionarItem(comandas, produtos, new FakeClock());
        caso.Executar(comandaId, produtoId, 1);

        var resultado = caso.Executar(comandaId, produtoId, 1);

        Assert.Single(resultado.Valor!.Itens);
        Assert.Equal(2, resultado.Valor.Itens[0].Quantidade);
        Assert.Equal(1000, resultado.Valor.Comanda.TotalCentavos);
    }

    [Fact]
    public void Executar_QuantidadeZeroOuNegativa_Falha()
    {
        var (comandas, produtos, comandaId, produtoId, _) = Preparar();
        var caso = new AdicionarItem(comandas, produtos, new FakeClock());

        var resultado = caso.Executar(comandaId, produtoId, 0);

        Assert.False(resultado.Sucesso);
        Assert.Contains("A quantidade deve ser maior que zero.", resultado.Erros);
    }

    [Fact]
    public void Executar_ProdutoInativo_Falha()
    {
        var (comandas, produtos, comandaId, _, produtoInativoId) = Preparar();
        var caso = new AdicionarItem(comandas, produtos, new FakeClock());

        var resultado = caso.Executar(comandaId, produtoInativoId, 1);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Produto indisponível.", resultado.Erros);
    }

    [Fact]
    public void Executar_ComandaCancelada_Falha()
    {
        var (comandas, produtos, comandaId, produtoId, _) = Preparar();
        new CancelarComanda(comandas, new FakeClock()).Executar(comandaId);
        var caso = new AdicionarItem(comandas, produtos, new FakeClock());

        var resultado = caso.Executar(comandaId, produtoId, 1);

        Assert.False(resultado.Sucesso);
        Assert.Contains("A comanda não está aberta.", resultado.Erros);
    }
}
```

`backend/tests/VarthexComanda.Application.Tests/Atendimento/AlterarQuantidadeTests.cs`:

```csharp
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Application.Tests.Catalogo;
using Xunit;

namespace VarthexComanda.Application.Tests.Atendimento;

public class AlterarQuantidadeTests
{
    [Fact]
    public void Executar_QuantidadeValida_RecalculaSubtotalETotal()
    {
        var comandas = new FakeComandaRepository();
        var comanda = new AbrirComanda(comandas, new FakeClock()).Executar(10).Valor!;
        var categorias = new FakeCategoriaRepository();
        var categoria = new CadastrarCategoria(categorias, new FakeClock()).Executar("Bebidas").Valor!;
        var produtos = new FakeProdutoRepository();
        var produto = new CadastrarProduto(produtos, categorias, new FakeClock()).Executar("Refrigerante", categoria.Id, 500).Valor!;
        var item = new AdicionarItem(comandas, produtos, new FakeClock()).Executar(comanda.Id, produto.Id, 3).Valor!.Itens[0];
        var caso = new AlterarQuantidade(comandas, new FakeClock());

        var resultado = caso.Executar(item.Id, 2);

        Assert.True(resultado.Sucesso);
        Assert.Equal(2, resultado.Valor!.Itens[0].Quantidade);
        Assert.Equal(1000, resultado.Valor.Itens[0].SubtotalCentavos);
        Assert.Equal(1000, resultado.Valor.Comanda.TotalCentavos);
    }

    [Fact]
    public void Executar_QuantidadeZeroOuNegativa_Falha()
    {
        var caso = new AlterarQuantidade(new FakeComandaRepository(), new FakeClock());

        var resultado = caso.Executar(1, 0);

        Assert.False(resultado.Sucesso);
        Assert.Contains("A quantidade deve ser maior que zero.", resultado.Erros);
    }

    [Fact]
    public void Executar_ItemInexistente_Falha()
    {
        var caso = new AlterarQuantidade(new FakeComandaRepository(), new FakeClock());

        var resultado = caso.Executar(999, 1);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Item não encontrado.", resultado.Erros);
    }
}
```

`backend/tests/VarthexComanda.Application.Tests/Atendimento/RemoverItemTests.cs`:

```csharp
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Application.Tests.Catalogo;
using Xunit;

namespace VarthexComanda.Application.Tests.Atendimento;

public class RemoverItemTests
{
    [Fact]
    public void Executar_ItemExistente_RemoveERecalculaTotal()
    {
        var comandas = new FakeComandaRepository();
        var comanda = new AbrirComanda(comandas, new FakeClock()).Executar(10).Valor!;
        var categorias = new FakeCategoriaRepository();
        var categoria = new CadastrarCategoria(categorias, new FakeClock()).Executar("Bebidas").Valor!;
        var produtos = new FakeProdutoRepository();
        var produto = new CadastrarProduto(produtos, categorias, new FakeClock()).Executar("Refrigerante", categoria.Id, 500).Valor!;
        var item = new AdicionarItem(comandas, produtos, new FakeClock()).Executar(comanda.Id, produto.Id, 1).Valor!.Itens[0];
        var caso = new RemoverItem(comandas, new FakeClock());

        var resultado = caso.Executar(item.Id);

        Assert.True(resultado.Sucesso);
        Assert.Empty(resultado.Valor!.Itens);
        Assert.Equal(0, resultado.Valor.Comanda.TotalCentavos);
    }

    [Fact]
    public void Executar_ItemInexistente_Falha()
    {
        var caso = new RemoverItem(new FakeComandaRepository(), new FakeClock());

        var resultado = caso.Executar(999);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Item não encontrado.", resultado.Erros);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile**

```powershell
$env:Path += ';C:\Program Files\dotnet'
dotnet test backend/tests/VarthexComanda.Application.Tests --filter "FullyQualifiedName~Atendimento"
```

Expected: compilation errors — `AdicionarItem`, `AlterarQuantidade`, `RemoverItem` do not exist.

- [ ] **Step 3: Implement the use cases**

`backend/src/VarthexComanda.Application/Atendimento/AdicionarItem.cs`:

```csharp
using VarthexComanda.Application.Abstractions;
using VarthexComanda.Application.Catalogo;

namespace VarthexComanda.Application.Atendimento;

public class AdicionarItem
{
    private readonly IComandaRepository _comandas;
    private readonly IProdutoRepository _produtos;
    private readonly IClock _relogio;

    public AdicionarItem(IComandaRepository comandas, IProdutoRepository produtos, IClock relogio)
    {
        _comandas = comandas;
        _produtos = produtos;
        _relogio = relogio;
    }

    public Resultado<ComandaComItens> Executar(int comandaId, int produtoId, int quantidade)
    {
        if (quantidade <= 0)
        {
            return Resultado<ComandaComItens>.Falha("A quantidade deve ser maior que zero.");
        }

        var produto = _produtos.BuscarPorId(produtoId);
        if (produto is null || !produto.Ativo)
        {
            return Resultado<ComandaComItens>.Falha("Produto indisponível.");
        }

        try
        {
            return Resultado<ComandaComItens>.Ok(_comandas.AdicionarItem(comandaId, produto, quantidade, _relogio.UtcNow));
        }
        catch (ComandaNaoAbertaException ex)
        {
            return Resultado<ComandaComItens>.Falha(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Resultado<ComandaComItens>.Falha(ex.Message);
        }
    }
}
```

`backend/src/VarthexComanda.Application/Atendimento/AlterarQuantidade.cs`:

```csharp
using VarthexComanda.Application.Abstractions;

namespace VarthexComanda.Application.Atendimento;

public class AlterarQuantidade
{
    private readonly IComandaRepository _comandas;
    private readonly IClock _relogio;

    public AlterarQuantidade(IComandaRepository comandas, IClock relogio)
    {
        _comandas = comandas;
        _relogio = relogio;
    }

    public Resultado<ComandaComItens> Executar(int itemId, int quantidade)
    {
        if (quantidade <= 0)
        {
            return Resultado<ComandaComItens>.Falha("A quantidade deve ser maior que zero.");
        }

        try
        {
            return Resultado<ComandaComItens>.Ok(_comandas.AlterarQuantidade(itemId, quantidade, _relogio.UtcNow));
        }
        catch (ComandaNaoAbertaException ex)
        {
            return Resultado<ComandaComItens>.Falha(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Resultado<ComandaComItens>.Falha(ex.Message);
        }
    }
}
```

`backend/src/VarthexComanda.Application/Atendimento/RemoverItem.cs`:

```csharp
using VarthexComanda.Application.Abstractions;

namespace VarthexComanda.Application.Atendimento;

public class RemoverItem
{
    private readonly IComandaRepository _comandas;
    private readonly IClock _relogio;

    public RemoverItem(IComandaRepository comandas, IClock relogio)
    {
        _comandas = comandas;
        _relogio = relogio;
    }

    public Resultado<ComandaComItens> Executar(int itemId)
    {
        try
        {
            return Resultado<ComandaComItens>.Ok(_comandas.RemoverItem(itemId, _relogio.UtcNow));
        }
        catch (ComandaNaoAbertaException ex)
        {
            return Resultado<ComandaComItens>.Falha(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Resultado<ComandaComItens>.Falha(ex.Message);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```powershell
$env:Path += ';C:\Program Files\dotnet'
dotnet test backend/tests/VarthexComanda.Application.Tests
```

Expected: 37 tests pass total (22 from the catalog slice + 5 from Task 1 + 5 `AdicionarItemTests` + 3 `AlterarQuantidadeTests` + 2 `RemoverItemTests` = 37).

- [ ] **Step 5: Commit**

```powershell
git add backend/src/VarthexComanda.Application/Atendimento backend/tests/VarthexComanda.Application.Tests/Atendimento
git commit -m "feat: adiciona casos de uso de item (adicionar, alterar quantidade, remover)"
```

---

### Task 3: Infrastructure — `EfComandaRepository` (leitura + abertura) e teste de concorrência

**Files:**
- Create: `backend/src/VarthexComanda.Infrastructure/Persistence/Atendimento/EfComandaRepository.cs`
- Test: `backend/tests/VarthexComanda.Infrastructure.Tests/Atendimento/EfComandaRepositoryTests.cs`

**Interfaces:**
- Consumes: `IComandaRepository`, `ComandaComItens`, `NumeroComandaOcupadoException` (Task 1), `VarthexComandaDbContext` (base slice).
- Produces: `EfComandaRepository(IDbContextFactory<VarthexComandaDbContext>)` implementing `ListarAbertas`, `BuscarComItens`, `AbrirComanda` (the other 4 interface methods are added in Task 4, in the same file — this task's class is intentionally incomplete and won't compile as a full `IComandaRepository` until Task 4; that's fine, this task only needs its own 3 methods to compile and its own tests to pass, so implement the class as a plain class in this task, not yet declaring `: IComandaRepository` — Task 4 adds the interface declaration once all 7 methods exist. This avoids a task that can't build on its own).

- [ ] **Step 1: Write the failing tests**

`backend/tests/VarthexComanda.Infrastructure.Tests/Atendimento/EfComandaRepositoryTests.cs`:

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Persistence;
using VarthexComanda.Infrastructure.Persistence.Atendimento;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests.Atendimento;

public class EfComandaRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ServiceProvider _provedor;
    private readonly IDbContextFactory<VarthexComandaDbContext> _fabrica;

    public EfComandaRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"varthex-comanda-tests-{Guid.NewGuid()}.db");

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
    public void AbrirComanda_NumeroLivre_CriaComandaAberta()
    {
        var repositorio = new EfComandaRepository(_fabrica);

        var comanda = repositorio.AbrirComanda(10, DateTime.UtcNow);

        Assert.True(comanda.Id > 0);
        Assert.Equal(StatusComanda.Aberta, comanda.Status);
        Assert.Equal(0, comanda.TotalCentavos);
    }

    [Fact]
    public void AbrirComanda_MesmoNumeroDuasVezes_SegundaLancaExcecao()
    {
        var repositorio = new EfComandaRepository(_fabrica);
        repositorio.AbrirComanda(10, DateTime.UtcNow);

        Assert.Throws<VarthexComanda.Application.Atendimento.NumeroComandaOcupadoException>(
            () => repositorio.AbrirComanda(10, DateTime.UtcNow));
    }

    [Fact]
    public void ListarAbertas_RetornaSomenteAbertasOrdenadasPorNumero()
    {
        var repositorio = new EfComandaRepository(_fabrica);
        repositorio.AbrirComanda(20, DateTime.UtcNow);
        repositorio.AbrirComanda(10, DateTime.UtcNow);

        var abertas = repositorio.ListarAbertas();

        Assert.Equal(new[] { 10, 20 }, abertas.Select(c => c.Numero).ToArray());
    }

    [Fact]
    public void BuscarComItens_ComandaSemItens_RetornaListaVazia()
    {
        var repositorio = new EfComandaRepository(_fabrica);
        var comanda = repositorio.AbrirComanda(10, DateTime.UtcNow);

        var detalhe = repositorio.BuscarComItens(comanda.Id);

        Assert.NotNull(detalhe);
        Assert.Empty(detalhe!.Itens);
    }

    [Fact]
    public void BuscarComItens_ComandaInexistente_RetornaNull()
    {
        var repositorio = new EfComandaRepository(_fabrica);

        var detalhe = repositorio.BuscarComItens(999);

        Assert.Null(detalhe);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile**

```powershell
$env:Path += ';C:\Program Files\dotnet'
dotnet test backend/tests/VarthexComanda.Infrastructure.Tests --filter "FullyQualifiedName~Atendimento"
```

Expected: compilation error — `EfComandaRepository` does not exist.

- [ ] **Step 3: Implement `EfComandaRepository` (partial — 3 methods)**

`backend/src/VarthexComanda.Infrastructure/Persistence/Atendimento/EfComandaRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Domain;

namespace VarthexComanda.Infrastructure.Persistence.Atendimento;

public class EfComandaRepository
{
    private readonly IDbContextFactory<VarthexComandaDbContext> _fabricaContexto;

    public EfComandaRepository(IDbContextFactory<VarthexComandaDbContext> fabricaContexto)
    {
        _fabricaContexto = fabricaContexto;
    }

    public IReadOnlyList<Comanda> ListarAbertas()
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        return contexto.Comandas
            .Where(c => c.Status == StatusComanda.Aberta)
            .OrderBy(c => c.Numero)
            .ToList();
    }

    public ComandaComItens? BuscarComItens(int comandaId)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        var comanda = contexto.Comandas.SingleOrDefault(c => c.Id == comandaId);
        if (comanda is null)
        {
            return null;
        }

        var itens = contexto.ItensComanda
            .Where(i => i.ComandaId == comandaId)
            .OrderBy(i => i.Id)
            .ToList();
        return new ComandaComItens { Comanda = comanda, Itens = itens };
    }

    public Comanda AbrirComanda(int numero, DateTime agora)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        var comanda = new Comanda
        {
            Id = 0,
            Numero = numero,
            Status = StatusComanda.Aberta,
            AbertaEm = agora,
            FechadaEm = null,
            TotalCentavos = 0
        };
        contexto.Comandas.Add(comanda);
        try
        {
            contexto.SaveChanges();
        }
        catch (DbUpdateException)
        {
            throw new NumeroComandaOcupadoException();
        }
        return comanda;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```powershell
$env:Path += ';C:\Program Files\dotnet'
dotnet test backend/tests/VarthexComanda.Infrastructure.Tests --filter "FullyQualifiedName~Atendimento"
```

Expected: 5 tests pass.

- [ ] **Step 5: Commit**

```powershell
git add backend/src/VarthexComanda.Infrastructure/Persistence/Atendimento backend/tests/VarthexComanda.Infrastructure.Tests/Atendimento
git commit -m "feat: adiciona EfComandaRepository (listar, buscar, abrir) com teste de concorrencia"
```

---

### Task 4: Infrastructure — `EfComandaRepository` (itens e cancelamento)

**Files:**
- Modify: `backend/src/VarthexComanda.Infrastructure/Persistence/Atendimento/EfComandaRepository.cs`
- Modify: `backend/tests/VarthexComanda.Infrastructure.Tests/Atendimento/EfComandaRepositoryTests.cs`

**Interfaces:**
- Consumes: `Produto` (`VarthexComanda.Domain`), `ComandaNaoAbertaException` (Task 1), the 3 methods and test fixture from Task 3 (same file, same class — this task completes it).
- Produces: the completed `EfComandaRepository : IComandaRepository` (all 7 methods) — consumed by Task 8's DI registration.

- [ ] **Step 1: Add the failing tests to the existing test file**

Append these `[Fact]` methods inside the existing `EfComandaRepositoryTests` class in `backend/tests/VarthexComanda.Infrastructure.Tests/Atendimento/EfComandaRepositoryTests.cs` (before the closing `}` of the class), and add `using VarthexComanda.Application.Catalogo;` and `using VarthexComanda.Infrastructure.Persistence.Catalogo;` to the file's `using` list (needed to seed a `Categoria`/`Produto` via `EfCategoriaRepository`/`EfProdutoRepository`, already built in the catalog slice):

```csharp
    private (Produto produto, int comandaId) PrepararComandaEProduto(EfComandaRepository comandas)
    {
        var categoriaRepositorio = new EfCategoriaRepository(_fabrica);
        var agora = DateTime.UtcNow;
        var categoria = categoriaRepositorio.Salvar(new Categoria { Id = 0, Nome = "Bebidas", Ativo = true, CriadoEm = agora, AtualizadoEm = agora });
        var produtoRepositorio = new EfProdutoRepository(_fabrica);
        var produto = produtoRepositorio.Salvar(new Produto { Id = 0, CategoriaId = categoria.Id, Nome = "Refrigerante", PrecoCentavos = 500, Ativo = true, CriadoEm = agora, AtualizadoEm = agora });
        var comanda = comandas.AbrirComanda(10, agora);
        return (produto, comanda.Id);
    }

    [Fact]
    public void AdicionarItem_ProdutoNovo_CriaItemERecalculaTotal()
    {
        var repositorio = new EfComandaRepository(_fabrica);
        var (produto, comandaId) = PrepararComandaEProduto(repositorio);

        var detalhe = repositorio.AdicionarItem(comandaId, produto, 2, DateTime.UtcNow);

        Assert.Single(detalhe.Itens);
        Assert.Equal(2, detalhe.Itens[0].Quantidade);
        Assert.Equal(1000, detalhe.Itens[0].SubtotalCentavos);
        Assert.Equal(1000, detalhe.Comanda.TotalCentavos);
    }

    [Fact]
    public void AdicionarItem_MesmoProdutoMesmoPreco_IncrementaQuantidadeEmVezDeDuplicar()
    {
        var repositorio = new EfComandaRepository(_fabrica);
        var (produto, comandaId) = PrepararComandaEProduto(repositorio);
        repositorio.AdicionarItem(comandaId, produto, 1, DateTime.UtcNow);

        var detalhe = repositorio.AdicionarItem(comandaId, produto, 1, DateTime.UtcNow);

        Assert.Single(detalhe.Itens);
        Assert.Equal(2, detalhe.Itens[0].Quantidade);
        Assert.Equal(1000, detalhe.Comanda.TotalCentavos);
    }

    [Fact]
    public void AdicionarItem_MesmoProdutoPrecoDiferente_CriaLinhaNova()
    {
        var repositorio = new EfComandaRepository(_fabrica);
        var (produto, comandaId) = PrepararComandaEProduto(repositorio);
        repositorio.AdicionarItem(comandaId, produto, 1, DateTime.UtcNow);

        var produtoComNovoPreco = new Produto
        {
            Id = produto.Id, CategoriaId = produto.CategoriaId, Nome = produto.Nome,
            PrecoCentavos = 700, Ativo = true, CriadoEm = produto.CriadoEm, AtualizadoEm = DateTime.UtcNow
        };
        var detalhe = repositorio.AdicionarItem(comandaId, produtoComNovoPreco, 1, DateTime.UtcNow);

        Assert.Equal(2, detalhe.Itens.Count);
        Assert.Equal(500 + 700, detalhe.Comanda.TotalCentavos);
    }

    [Fact]
    public void AlterarQuantidade_ItemExistente_RecalculaSubtotalETotal()
    {
        var repositorio = new EfComandaRepository(_fabrica);
        var (produto, comandaId) = PrepararComandaEProduto(repositorio);
        var item = repositorio.AdicionarItem(comandaId, produto, 3, DateTime.UtcNow).Itens[0];

        var detalhe = repositorio.AlterarQuantidade(item.Id, 1, DateTime.UtcNow);

        Assert.Equal(1, detalhe.Itens[0].Quantidade);
        Assert.Equal(500, detalhe.Itens[0].SubtotalCentavos);
        Assert.Equal(500, detalhe.Comanda.TotalCentavos);
    }

    [Fact]
    public void RemoverItem_ItemExistente_RemoveERecalculaTotal()
    {
        var repositorio = new EfComandaRepository(_fabrica);
        var (produto, comandaId) = PrepararComandaEProduto(repositorio);
        var item = repositorio.AdicionarItem(comandaId, produto, 1, DateTime.UtcNow).Itens[0];

        var detalhe = repositorio.RemoverItem(item.Id, DateTime.UtcNow);

        Assert.Empty(detalhe.Itens);
        Assert.Equal(0, detalhe.Comanda.TotalCentavos);
    }

    [Fact]
    public void CancelarComanda_ComandaAberta_MarcaCanceladaEDefineFechadaEm()
    {
        var repositorio = new EfComandaRepository(_fabrica);
        var comanda = repositorio.AbrirComanda(20, DateTime.UtcNow);

        var cancelada = repositorio.CancelarComanda(comanda.Id, DateTime.UtcNow);

        Assert.Equal(StatusComanda.Cancelada, cancelada.Status);
        Assert.NotNull(cancelada.FechadaEm);
    }

    [Fact]
    public void AdicionarItem_ComandaCancelada_LancaExcecao()
    {
        var repositorio = new EfComandaRepository(_fabrica);
        var (produto, comandaId) = PrepararComandaEProduto(repositorio);
        repositorio.CancelarComanda(comandaId, DateTime.UtcNow);

        Assert.Throws<VarthexComanda.Application.Atendimento.ComandaNaoAbertaException>(
            () => repositorio.AdicionarItem(comandaId, produto, 1, DateTime.UtcNow));
    }
```

- [ ] **Step 2: Run tests to verify they fail to compile**

```powershell
$env:Path += ';C:\Program Files\dotnet'
dotnet test backend/tests/VarthexComanda.Infrastructure.Tests --filter "FullyQualifiedName~Atendimento"
```

Expected: compilation errors — `AdicionarItem`, `AlterarQuantidade`, `RemoverItem`, `CancelarComanda` do not exist on `EfComandaRepository`.

- [ ] **Step 3: Complete `EfComandaRepository`**

In `backend/src/VarthexComanda.Infrastructure/Persistence/Atendimento/EfComandaRepository.cs`, change the class declaration to implement the interface, and add the 4 remaining methods (insert them after the existing `AbrirComanda` method, before the closing `}` of the class):

```csharp
public class EfComandaRepository : IComandaRepository
```

```csharp
    public ComandaComItens AdicionarItem(int comandaId, Produto produto, int quantidade, DateTime agora)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        var comanda = contexto.Comandas.SingleOrDefault(c => c.Id == comandaId)
            ?? throw new InvalidOperationException("Comanda não encontrada.");
        if (comanda.Status != StatusComanda.Aberta)
        {
            throw new ComandaNaoAbertaException();
        }

        var itens = contexto.ItensComanda.Where(i => i.ComandaId == comandaId).ToList();
        var itemExistente = itens.FirstOrDefault(i => i.ProdutoId == produto.Id && i.PrecoUnitarioCentavos == produto.PrecoCentavos);

        if (itemExistente is not null)
        {
            itemExistente.Quantidade += quantidade;
            itemExistente.SubtotalCentavos = itemExistente.PrecoUnitarioCentavos * itemExistente.Quantidade;
            itemExistente.AtualizadoEm = agora;
        }
        else
        {
            var novoItem = new ItemComanda
            {
                Id = 0,
                ComandaId = comandaId,
                ProdutoId = produto.Id,
                NomeProduto = produto.Nome,
                PrecoUnitarioCentavos = produto.PrecoCentavos,
                Quantidade = quantidade,
                SubtotalCentavos = produto.PrecoCentavos * quantidade,
                CriadoEm = agora,
                AtualizadoEm = agora
            };
            contexto.ItensComanda.Add(novoItem);
            itens.Add(novoItem);
        }

        comanda.TotalCentavos = itens.Sum(i => i.SubtotalCentavos);
        contexto.SaveChanges();

        return new ComandaComItens { Comanda = comanda, Itens = itens };
    }

    public ComandaComItens AlterarQuantidade(int itemId, int quantidade, DateTime agora)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        var item = contexto.ItensComanda.SingleOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException("Item não encontrado.");
        var comanda = contexto.Comandas.SingleOrDefault(c => c.Id == item.ComandaId)
            ?? throw new InvalidOperationException("Comanda não encontrada.");
        if (comanda.Status != StatusComanda.Aberta)
        {
            throw new ComandaNaoAbertaException();
        }

        item.Quantidade = quantidade;
        item.SubtotalCentavos = item.PrecoUnitarioCentavos * quantidade;
        item.AtualizadoEm = agora;

        var itens = contexto.ItensComanda.Where(i => i.ComandaId == comanda.Id).ToList();
        comanda.TotalCentavos = itens.Sum(i => i.SubtotalCentavos);
        contexto.SaveChanges();

        return new ComandaComItens { Comanda = comanda, Itens = itens };
    }

    public ComandaComItens RemoverItem(int itemId, DateTime agora)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        var item = contexto.ItensComanda.SingleOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException("Item não encontrado.");
        var comanda = contexto.Comandas.SingleOrDefault(c => c.Id == item.ComandaId)
            ?? throw new InvalidOperationException("Comanda não encontrada.");
        if (comanda.Status != StatusComanda.Aberta)
        {
            throw new ComandaNaoAbertaException();
        }

        contexto.ItensComanda.Remove(item);

        var itensRestantes = contexto.ItensComanda
            .Where(i => i.ComandaId == comanda.Id && i.Id != itemId)
            .ToList();
        comanda.TotalCentavos = itensRestantes.Sum(i => i.SubtotalCentavos);
        contexto.SaveChanges();

        return new ComandaComItens { Comanda = comanda, Itens = itensRestantes };
    }

    public Comanda CancelarComanda(int comandaId, DateTime agora)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        var comanda = contexto.Comandas.SingleOrDefault(c => c.Id == comandaId)
            ?? throw new InvalidOperationException("Comanda não encontrada.");
        if (comanda.Status != StatusComanda.Aberta)
        {
            throw new ComandaNaoAbertaException();
        }

        comanda.Status = StatusComanda.Cancelada;
        comanda.FechadaEm = agora;
        contexto.SaveChanges();
        return comanda;
    }
```

- [ ] **Step 4: Run tests to verify they pass**

```powershell
$env:Path += ';C:\Program Files\dotnet'
dotnet test backend/tests/VarthexComanda.Infrastructure.Tests
```

Expected: all Infrastructure tests pass — 16 from the catalog slice + 5 from Task 3 + 7 new = 28.

- [ ] **Step 5: Commit**

```powershell
git add backend/src/VarthexComanda.Infrastructure/Persistence/Atendimento/EfComandaRepository.cs backend/tests/VarthexComanda.Infrastructure.Tests/Atendimento/EfComandaRepositoryTests.cs
git commit -m "feat: completa EfComandaRepository (itens e cancelamento)"
```

---

### Task 5: Desktop — projeto `Desktop.Tests` + testes retroativos do `ProdutosViewModel`

**Files:**
- Create: `backend/tests/VarthexComanda.Desktop.Tests/VarthexComanda.Desktop.Tests.csproj`
- Create: `backend/tests/VarthexComanda.Desktop.Tests/Catalogo/ProdutosViewModelTests.cs`

**Interfaces:**
- Consumes: `ProdutosViewModel` (`VarthexComanda.Desktop.Catalogo`), `FakeCategoriaRepository`/`FakeProdutoRepository`/`FakeClock` (`VarthexComanda.Application.Tests.Catalogo` — via the new cross-project test reference), `CadastrarCategoria`/`CadastrarProduto`/`AlterarProduto`/`DesativarProduto`/`ListarCategoriasAtivas`/`PesquisarProdutos` (`VarthexComanda.Application.Catalogo`).
- Produces: `VarthexComanda.Desktop.Tests` project wired into the solution — consumed by Task 6 (same project, `Atendimento` folder added there).

- [ ] **Step 1: Create the test project and wire references**

```powershell
$env:Path += ';C:\Program Files\dotnet'
dotnet new xunit -n VarthexComanda.Desktop.Tests -o backend/tests/VarthexComanda.Desktop.Tests -f net10.0
dotnet sln backend/VarthexComanda.slnx add backend/tests/VarthexComanda.Desktop.Tests/VarthexComanda.Desktop.Tests.csproj
dotnet add backend/tests/VarthexComanda.Desktop.Tests reference backend/src/VarthexComanda.Desktop
dotnet add backend/tests/VarthexComanda.Desktop.Tests reference backend/tests/VarthexComanda.Application.Tests
```

Delete the placeholder `UnitTest1.cs` that `dotnet new xunit` generates (this project's own tests, not the template's).

Note: `VarthexComanda.Desktop.csproj` targets `net10.0-windows` with `UseWPF` enabled — referencing it from a plain `net10.0` test project works fine for testing non-XAML classes like ViewModels (the test project itself never touches WPF types), no target-framework changes needed on either project.

- [ ] **Step 2: Write the failing tests**

`backend/tests/VarthexComanda.Desktop.Tests/Catalogo/ProdutosViewModelTests.cs`:

```csharp
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Application.Tests.Catalogo;
using VarthexComanda.Desktop.Catalogo;
using Xunit;

namespace VarthexComanda.Desktop.Tests.Catalogo;

public class ProdutosViewModelTests
{
    private static ProdutosViewModel CriarViewModel(FakeCategoriaRepository categorias, FakeProdutoRepository produtos)
    {
        var relogio = new FakeClock();
        return new ProdutosViewModel(
            new ListarCategoriasAtivas(categorias),
            new CadastrarCategoria(categorias, relogio),
            new PesquisarProdutos(produtos),
            new CadastrarProduto(produtos, categorias, relogio),
            new AlterarProduto(produtos, categorias, relogio),
            new DesativarProduto(produtos, relogio));
    }

    [Fact]
    public void PerderSelecao_LimpaFormulario_NaoDuplicaAoSalvarDeNovo()
    {
        var categorias = new FakeCategoriaRepository();
        var categoria = new CadastrarCategoria(categorias, new FakeClock()).Executar("Bebidas").Valor!;
        var produtos = new FakeProdutoRepository();
        var viewModel = CriarViewModel(categorias, produtos);
        viewModel.CategoriaProduto = categoria;
        viewModel.NomeProduto = "Refrigerante";
        viewModel.PrecoProdutoReais = "5,00";
        viewModel.SalvarCommand.Execute(null);

        viewModel.ProdutoSelecionado = viewModel.Produtos[0];
        viewModel.ProdutoSelecionado = null;
        viewModel.SalvarCommand.Execute(null);

        Assert.Single(produtos.Pesquisar(null, null));
    }

    [Fact]
    public void PrecoComPonto_NaoEhInterpretadoComoMilhar()
    {
        var categorias = new FakeCategoriaRepository();
        var categoria = new CadastrarCategoria(categorias, new FakeClock()).Executar("Bebidas").Valor!;
        var produtos = new FakeProdutoRepository();
        var viewModel = CriarViewModel(categorias, produtos);
        viewModel.CategoriaProduto = categoria;
        viewModel.NomeProduto = "Refrigerante";
        viewModel.PrecoProdutoReais = "10.50";

        viewModel.SalvarCommand.Execute(null);

        Assert.Empty(produtos.Pesquisar(null, null));
        Assert.Equal("Informe um preço válido.", viewModel.Mensagem);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail to compile, then pass**

```powershell
$env:Path += ';C:\Program Files\dotnet'
dotnet test backend/tests/VarthexComanda.Desktop.Tests
```

Expected first run (before Step 1's project actually exists — skip if you already created it in Step 1): the project/tests don't exist. After Step 1 creates the project and Step 2 adds the tests: 2 tests pass immediately (no new production code needed — this task only adds regression coverage for behavior that already exists from the catalog slice's final-review fixes).

- [ ] **Step 4: Commit**

```powershell
git add backend/tests/VarthexComanda.Desktop.Tests backend/VarthexComanda.slnx
git commit -m "test: cria projeto Desktop.Tests e cobre regressao do ProdutosViewModel"
```

---

### Task 6: Desktop — `AtendimentoViewModel`

**Files:**
- Create: `backend/src/VarthexComanda.Desktop/Atendimento/AtendimentoViewModel.cs`
- Test: `backend/tests/VarthexComanda.Desktop.Tests/Atendimento/AtendimentoViewModelTests.cs`

**Interfaces:**
- Consumes: `AbrirComanda`, `AdicionarItem`, `AlterarQuantidade`, `RemoverItem`, `CancelarComanda`, `IComandaRepository`, `ComandaComItens` (`VarthexComanda.Application.Atendimento`); `ListarCategoriasAtivas`, `PesquisarProdutos` (`VarthexComanda.Application.Catalogo`); `FakeComandaRepository` (`VarthexComanda.Application.Tests.Atendimento`, via the cross-project test reference from Task 5), `FakeCategoriaRepository`/`FakeProdutoRepository`/`FakeClock` (`VarthexComanda.Application.Tests.Catalogo`).
- Produces: `AtendimentoViewModel` — consumed by Task 8's DI registration and `AtendimentoView.xaml`.

**Important naming note:** the `[RelayCommand]` methods below are named `Abrir()` and `Remover(ItemComanda)` — **not** `AbrirComanda()`/`RemoverItem(...)` — because this class also holds injected fields of type `AbrirComanda` and `RemoverItem` (the use case classes from Task 1-2). Naming a method identically to an injected type in the same class scope is legal C# but needlessly confusing; use the exact names below.

- [ ] **Step 1: Write the failing tests**

`backend/tests/VarthexComanda.Desktop.Tests/Atendimento/AtendimentoViewModelTests.cs`:

```csharp
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Application.Tests.Atendimento;
using VarthexComanda.Application.Tests.Catalogo;
using VarthexComanda.Desktop.Atendimento;
using Xunit;

namespace VarthexComanda.Desktop.Tests.Atendimento;

public class AtendimentoViewModelTests
{
    private static (AtendimentoViewModel viewModel, FakeProdutoRepository produtos) CriarViewModel()
    {
        var categorias = new FakeCategoriaRepository();
        var relogio = new FakeClock();
        var categoria = new CadastrarCategoria(categorias, relogio).Executar("Bebidas").Valor!;
        var produtos = new FakeProdutoRepository();
        new CadastrarProduto(produtos, categorias, relogio).Executar("Refrigerante", categoria.Id, 500);
        var comandas = new FakeComandaRepository();

        var viewModel = new AtendimentoViewModel(
            new AbrirComanda(comandas, relogio),
            new AdicionarItem(comandas, produtos, relogio),
            new AlterarQuantidade(comandas, relogio),
            new RemoverItem(comandas, relogio),
            new CancelarComanda(comandas, relogio),
            comandas,
            new ListarCategoriasAtivas(categorias),
            new PesquisarProdutos(produtos));

        return (viewModel, produtos);
    }

    [Fact]
    public void Abrir_NumeroValido_CriaComandaEMostraNaGrade()
    {
        var (viewModel, _) = CriarViewModel();
        viewModel.NovoNumero = "10";

        viewModel.AbrirCommand.Execute(null);

        Assert.Single(viewModel.ComandasAbertas);
        Assert.NotNull(viewModel.ComandaAtual);
        Assert.Equal(10, viewModel.ComandaAtual!.Numero);
    }

    [Fact]
    public void Abrir_MesmoNumeroDuasVezes_SegundaFalhaSemDuplicarNaGrade()
    {
        var (viewModel, _) = CriarViewModel();
        viewModel.NovoNumero = "10";
        viewModel.AbrirCommand.Execute(null);

        viewModel.NovoNumero = "10";
        viewModel.AbrirCommand.Execute(null);

        Assert.Equal("Já existe uma comanda aberta com esse número.", viewModel.Mensagem);
        Assert.Single(viewModel.ComandasAbertas);
    }

    [Fact]
    public void AdicionarProdutoDuasVezes_IncrementaQuantidadeEmVezDeDuplicar()
    {
        var (viewModel, produtos) = CriarViewModel();
        viewModel.NovoNumero = "10";
        viewModel.AbrirCommand.Execute(null);
        var produto = produtos.Pesquisar(null, null)[0];

        viewModel.AdicionarProdutoAoItemCommand.Execute(produto);
        viewModel.AdicionarProdutoAoItemCommand.Execute(produto);

        Assert.Single(viewModel.Itens);
        Assert.Equal(2, viewModel.Itens[0].Quantidade);
        Assert.Equal(1000, viewModel.ComandaAtual!.TotalCentavos);
    }

    [Fact]
    public void DiminuirQuantidadeAteZero_RemoveItem()
    {
        var (viewModel, produtos) = CriarViewModel();
        viewModel.NovoNumero = "10";
        viewModel.AbrirCommand.Execute(null);
        var produto = produtos.Pesquisar(null, null)[0];
        viewModel.AdicionarProdutoAoItemCommand.Execute(produto);

        viewModel.DiminuirQuantidadeCommand.Execute(viewModel.Itens[0]);

        Assert.Empty(viewModel.Itens);
        Assert.Equal(0, viewModel.ComandaAtual!.TotalCentavos);
    }

    [Fact]
    public void CancelarComandaAtual_LiberaNumeroNaGrade()
    {
        var (viewModel, _) = CriarViewModel();
        viewModel.NovoNumero = "10";
        viewModel.AbrirCommand.Execute(null);

        viewModel.CancelarComandaAtualCommand.Execute(null);

        Assert.Empty(viewModel.ComandasAbertas);
        Assert.Null(viewModel.ComandaAtual);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile**

```powershell
$env:Path += ';C:\Program Files\dotnet'
dotnet test backend/tests/VarthexComanda.Desktop.Tests --filter "FullyQualifiedName~Atendimento"
```

Expected: compilation error — `AtendimentoViewModel` does not exist.

- [ ] **Step 3: Implement `AtendimentoViewModel`**

`backend/src/VarthexComanda.Desktop/Atendimento/AtendimentoViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;

namespace VarthexComanda.Desktop.Atendimento;

public partial class AtendimentoViewModel : ObservableObject
{
    private readonly AbrirComanda _abrirComanda;
    private readonly AdicionarItem _adicionarItem;
    private readonly AlterarQuantidade _alterarQuantidade;
    private readonly RemoverItem _removerItem;
    private readonly CancelarComanda _cancelarComanda;
    private readonly IComandaRepository _comandas;
    private readonly ListarCategoriasAtivas _listarCategoriasAtivas;
    private readonly PesquisarProdutos _pesquisarProdutos;

    public AtendimentoViewModel(
        AbrirComanda abrirComanda,
        AdicionarItem adicionarItem,
        AlterarQuantidade alterarQuantidade,
        RemoverItem removerItem,
        CancelarComanda cancelarComanda,
        IComandaRepository comandas,
        ListarCategoriasAtivas listarCategoriasAtivas,
        PesquisarProdutos pesquisarProdutos)
    {
        _abrirComanda = abrirComanda;
        _adicionarItem = adicionarItem;
        _alterarQuantidade = alterarQuantidade;
        _removerItem = removerItem;
        _cancelarComanda = cancelarComanda;
        _comandas = comandas;
        _listarCategoriasAtivas = listarCategoriasAtivas;
        _pesquisarProdutos = pesquisarProdutos;

        ComandasAbertas = new ObservableCollection<Comanda>();
        Categorias = new ObservableCollection<Categoria>();
        Itens = new ObservableCollection<ItemComanda>();
        ProdutosCatalogo = new ObservableCollection<Produto>();

        AtualizarComandasAbertas();
        CarregarCategorias();
    }

    public ObservableCollection<Comanda> ComandasAbertas { get; }
    public ObservableCollection<Categoria> Categorias { get; }
    public ObservableCollection<ItemComanda> Itens { get; }
    public ObservableCollection<Produto> ProdutosCatalogo { get; }

    [ObservableProperty]
    private string novoNumero = string.Empty;

    [ObservableProperty]
    private Comanda? comandaAtual;

    [ObservableProperty]
    private Categoria? categoriaCatalogo;

    [ObservableProperty]
    private string textoBuscaCatalogo = string.Empty;

    [ObservableProperty]
    private string mensagem = string.Empty;

    partial void OnCategoriaCatalogoChanged(Categoria? value) => PesquisarCatalogo();

    partial void OnTextoBuscaCatalogoChanged(string value) => PesquisarCatalogo();

    [RelayCommand]
    private void PesquisarCatalogo()
    {
        var resultado = _pesquisarProdutos.Executar(CategoriaCatalogo?.Id, TextoBuscaCatalogo);
        ProdutosCatalogo.Clear();
        foreach (var produto in resultado.Where(p => p.Ativo))
        {
            ProdutosCatalogo.Add(produto);
        }
    }

    [RelayCommand]
    private void Abrir()
    {
        if (!int.TryParse(NovoNumero, out var numero))
        {
            Mensagem = "Informe um número de comanda válido.";
            return;
        }

        var resultado = _abrirComanda.Executar(numero);
        if (!resultado.Sucesso)
        {
            Mensagem = string.Join(" ", resultado.Erros);
            return;
        }

        NovoNumero = string.Empty;
        Mensagem = string.Empty;
        AtualizarComandasAbertas();
        AbrirParaEdicao(resultado.Valor!.Id);
    }

    [RelayCommand]
    private void SelecionarComanda(Comanda comanda) => AbrirParaEdicao(comanda.Id);

    private void AbrirParaEdicao(int comandaId)
    {
        var detalhe = _comandas.BuscarComItens(comandaId);
        if (detalhe is null)
        {
            Mensagem = "Comanda não encontrada.";
            return;
        }

        ComandaAtual = detalhe.Comanda;
        Itens.Clear();
        foreach (var item in detalhe.Itens)
        {
            Itens.Add(item);
        }
    }

    [RelayCommand]
    private void FecharEdicao()
    {
        ComandaAtual = null;
        Itens.Clear();
    }

    [RelayCommand]
    private void AdicionarProdutoAoItem(Produto produto)
    {
        if (ComandaAtual is null)
        {
            return;
        }

        AplicarResultado(_adicionarItem.Executar(ComandaAtual.Id, produto.Id, 1));
    }

    [RelayCommand]
    private void AumentarQuantidade(ItemComanda item) =>
        AplicarResultado(_alterarQuantidade.Executar(item.Id, item.Quantidade + 1));

    [RelayCommand]
    private void DiminuirQuantidade(ItemComanda item)
    {
        if (item.Quantidade <= 1)
        {
            AplicarResultado(_removerItem.Executar(item.Id));
            return;
        }

        AplicarResultado(_alterarQuantidade.Executar(item.Id, item.Quantidade - 1));
    }

    [RelayCommand]
    private void Remover(ItemComanda item) => AplicarResultado(_removerItem.Executar(item.Id));

    [RelayCommand]
    private void CancelarComandaAtual()
    {
        if (ComandaAtual is null)
        {
            return;
        }

        var resultado = _cancelarComanda.Executar(ComandaAtual.Id);
        if (!resultado.Sucesso)
        {
            Mensagem = string.Join(" ", resultado.Erros);
            return;
        }

        Mensagem = string.Empty;
        FecharEdicao();
        AtualizarComandasAbertas();
    }

    private void AplicarResultado(Resultado<ComandaComItens> resultado)
    {
        if (!resultado.Sucesso)
        {
            Mensagem = string.Join(" ", resultado.Erros);
            return;
        }

        Mensagem = string.Empty;
        ComandaAtual = resultado.Valor!.Comanda;
        Itens.Clear();
        foreach (var item in resultado.Valor.Itens)
        {
            Itens.Add(item);
        }
        AtualizarComandasAbertas();
    }

    private void AtualizarComandasAbertas()
    {
        ComandasAbertas.Clear();
        foreach (var comanda in _comandas.ListarAbertas())
        {
            ComandasAbertas.Add(comanda);
        }
    }

    private void CarregarCategorias()
    {
        Categorias.Clear();
        foreach (var categoria in _listarCategoriasAtivas.Executar())
        {
            Categorias.Add(categoria);
        }
        PesquisarCatalogo();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```powershell
$env:Path += ';C:\Program Files\dotnet'
dotnet test backend/tests/VarthexComanda.Desktop.Tests
```

Expected: 7 tests pass (2 `ProdutosViewModelTests` from Task 5 + 5 `AtendimentoViewModelTests`).

- [ ] **Step 5: Commit**

```powershell
git add backend/src/VarthexComanda.Desktop/Atendimento backend/tests/VarthexComanda.Desktop.Tests/Atendimento
git commit -m "feat: adiciona AtendimentoViewModel"
```

---

### Task 7: Desktop — extrai `ProdutosView` de `MainWindow` (refatoração sem mudar comportamento)

**Files:**
- Create: `backend/src/VarthexComanda.Desktop/Catalogo/ProdutosView.xaml`
- Create: `backend/src/VarthexComanda.Desktop/Catalogo/ProdutosView.xaml.cs`
- Modify: `backend/src/VarthexComanda.Desktop/MainWindow.xaml` (todo o conteúdo)
- Modify: `backend/src/VarthexComanda.Desktop/MainWindow.xaml.cs` (todo o conteúdo)
- Modify: `backend/src/VarthexComanda.Desktop/App.xaml.cs`

**Interfaces:**
- Consumes: `ProdutosViewModel` (já existe).
- Produces: `ProdutosView(ProdutosViewModel) : UserControl` — consumido pela Task 8's shell.

Esta é uma refatoração que preserva comportamento — sem teste novo, verificação manual apenas (a tela de Produtos deve continuar idêntica).

- [ ] **Step 1: Mover o conteúdo de `MainWindow.xaml` para `ProdutosView.xaml`**

`backend/src/VarthexComanda.Desktop/Catalogo/ProdutosView.xaml` (o `Grid` inteiro que hoje está em `MainWindow.xaml`, sem alterar nenhum binding):

```xml
<UserControl x:Class="VarthexComanda.Desktop.Catalogo.ProdutosView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d">
    <Grid Margin="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="2*" />
            <ColumnDefinition Width="16" />
            <ColumnDefinition Width="1*" />
        </Grid.ColumnDefinitions>

        <StackPanel Grid.Row="0" Grid.Column="0" Orientation="Horizontal" Margin="0,0,0,8">
            <TextBlock Text="Categoria:" VerticalAlignment="Center" Margin="0,0,4,0" />
            <ComboBox Width="180" ItemsSource="{Binding Categorias}" DisplayMemberPath="Nome"
                      SelectedItem="{Binding CategoriaFiltro}" Margin="0,0,12,0" />
            <Button Content="Limpar filtro" Command="{Binding LimparFiltroCommand}" Margin="0,0,12,0" />
            <TextBlock Text="Buscar:" VerticalAlignment="Center" Margin="0,0,4,0" />
            <TextBox Width="220" Text="{Binding TextoBusca, UpdateSourceTrigger=PropertyChanged}" />
        </StackPanel>

        <ListView Grid.Row="1" Grid.Column="0"
                  ItemsSource="{Binding Produtos}"
                  SelectedItem="{Binding ProdutoSelecionado}">
            <ListView.View>
                <GridView>
                    <GridViewColumn Header="Nome" DisplayMemberBinding="{Binding Nome}" Width="260" />
                    <GridViewColumn Header="Preço (centavos)" DisplayMemberBinding="{Binding PrecoCentavos}" Width="140" />
                    <GridViewColumn Header="Ativo" DisplayMemberBinding="{Binding Ativo}" Width="80" />
                </GridView>
            </ListView.View>
        </ListView>

        <StackPanel Grid.Row="0" Grid.RowSpan="2" Grid.Column="2">
            <TextBlock Text="Nome do produto" />
            <TextBox Text="{Binding NomeProduto, UpdateSourceTrigger=PropertyChanged}" Margin="0,0,0,8" />

            <TextBlock Text="Categoria" />
            <ComboBox ItemsSource="{Binding Categorias}" DisplayMemberPath="Nome"
                      SelectedItem="{Binding CategoriaProduto}" Margin="0,0,0,8" />

            <TextBlock Text="Preço (R$)" />
            <TextBox Text="{Binding PrecoProdutoReais, UpdateSourceTrigger=PropertyChanged}" Margin="0,0,0,8" />

            <CheckBox Content="Ativo" IsChecked="{Binding ProdutoAtivo}" Margin="0,0,0,8" />

            <StackPanel Orientation="Horizontal" Margin="0,0,0,8">
                <Button Content="Novo" Command="{Binding NovoCommand}" Margin="0,0,8,0" />
                <Button Content="Salvar" Command="{Binding SalvarCommand}" Margin="0,0,8,0" />
                <Button Content="Desativar" Command="{Binding DesativarCommand}" />
            </StackPanel>

            <Separator Margin="0,8,0,8" />

            <TextBlock Text="Nova categoria" />
            <TextBox Text="{Binding NovaCategoriaNome, UpdateSourceTrigger=PropertyChanged}" Margin="0,0,0,8" />
            <Button Content="Adicionar categoria" Command="{Binding AdicionarCategoriaCommand}" HorizontalAlignment="Left" />

            <TextBlock Text="{Binding Mensagem}" Foreground="Red" TextWrapping="Wrap" Margin="0,12,0,0" />
        </StackPanel>
    </Grid>
</UserControl>
```

`backend/src/VarthexComanda.Desktop/Catalogo/ProdutosView.xaml.cs`:

```csharp
using System.Windows.Controls;

namespace VarthexComanda.Desktop.Catalogo;

public partial class ProdutosView : UserControl
{
    public ProdutosView(ProdutosViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
```

- [ ] **Step 2: Reduzir `MainWindow` a um host simples de conteúdo (estado transitório — a Task 8 substitui isto pelo shell completo)**

`backend/src/VarthexComanda.Desktop/MainWindow.xaml` (substituir todo o conteúdo):

```xml
<Window x:Class="VarthexComanda.Desktop.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        mc:Ignorable="d"
        Title="Varthex Comanda" Height="600" Width="1000">
    <ContentControl x:Name="ConteudoPrincipal" />
</Window>
```

`backend/src/VarthexComanda.Desktop/MainWindow.xaml.cs` (substituir todo o conteúdo):

```csharp
using System.Windows;
using VarthexComanda.Desktop.Catalogo;

namespace VarthexComanda.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(ProdutosView produtosView)
    {
        InitializeComponent();
        ConteudoPrincipal.Content = produtosView;
    }
}
```

- [ ] **Step 3: Registrar `ProdutosView` no container de DI**

Em `backend/src/VarthexComanda.Desktop/App.xaml.cs`, adicione esta linha logo após `services.AddTransient<ProdutosViewModel>();`:

```csharp
services.AddTransient<ProdutosView>();
```

- [ ] **Step 4: Build e verificação manual de paridade**

```powershell
$env:Path += ';C:\Program Files\dotnet'
dotnet build backend/VarthexComanda.slnx --configuration Release
dotnet run --project backend/src/VarthexComanda.Desktop --configuration Release
```

Esperado: o comportamento é idêntico ao de antes desta task — a janela abre já mostrando a tela de Produtos, com filtro, busca, formulário e cadastro de categoria funcionando exatamente como antes.

- [ ] **Step 5: Commit**

```powershell
git add backend/src/VarthexComanda.Desktop
git commit -m "refactor: extrai ProdutosView como UserControl"
```

---

### Task 8: Desktop — `AtendimentoView` + shell de navegação + registro completo de DI

**Files:**
- Create: `backend/src/VarthexComanda.Desktop/Atendimento/NuloParaVisibilidadeConverters.cs`
- Create: `backend/src/VarthexComanda.Desktop/Atendimento/AtendimentoView.xaml`
- Create: `backend/src/VarthexComanda.Desktop/Atendimento/AtendimentoView.xaml.cs`
- Modify: `backend/src/VarthexComanda.Desktop/MainWindow.xaml` (todo o conteúdo)
- Modify: `backend/src/VarthexComanda.Desktop/MainWindow.xaml.cs` (todo o conteúdo)
- Modify: `backend/src/VarthexComanda.Desktop/App.xaml.cs`

**Interfaces:**
- Consumes: `AtendimentoViewModel` (Task 6), `EfComandaRepository`/`IComandaRepository` (Task 4), todos os casos de uso de atendimento (Tasks 1-2), `ProdutosView` (Task 7).
- Produces: app funcional com shell; nada depende disto depois (Task 9 é só verificação).

- [ ] **Step 1: Conversores de visibilidade**

`backend/src/VarthexComanda.Desktop/Atendimento/NuloParaVisibilidadeConverters.cs`:

```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VarthexComanda.Desktop.Atendimento;

public class NuloParaVisibilidadeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class NuloParaVisibilidadeInversoConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
```

- [ ] **Step 2: `AtendimentoView`**

`backend/src/VarthexComanda.Desktop/Atendimento/AtendimentoView.xaml`:

```xml
<UserControl x:Class="VarthexComanda.Desktop.Atendimento.AtendimentoView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:local="clr-namespace:VarthexComanda.Desktop.Atendimento"
             mc:Ignorable="d">
    <UserControl.Resources>
        <local:NuloParaVisibilidadeConverter x:Key="VisivelSeComandaAberta" />
        <local:NuloParaVisibilidadeInversoConverter x:Key="VisivelSeGrade" />
    </UserControl.Resources>
    <Grid Margin="12">

        <StackPanel Visibility="{Binding ComandaAtual, Converter={StaticResource VisivelSeGrade}}">
            <StackPanel Orientation="Horizontal" Margin="0,0,0,12">
                <TextBlock Text="Número da comanda:" VerticalAlignment="Center" Margin="0,0,4,0" />
                <TextBox Width="80" Text="{Binding NovoNumero, UpdateSourceTrigger=PropertyChanged}" Margin="0,0,8,0" />
                <Button Content="Abrir" Command="{Binding AbrirCommand}" />
            </StackPanel>
            <TextBlock Text="Comandas abertas" FontWeight="Bold" Margin="0,0,0,4" />
            <ListView ItemsSource="{Binding ComandasAbertas}" MaxHeight="400">
                <ListView.ItemTemplate>
                    <DataTemplate>
                        <Button Content="{Binding Numero}" Width="80" Height="60"
                                Command="{Binding DataContext.SelecionarComandaCommand, RelativeSource={RelativeSource AncestorType=ListView}}"
                                CommandParameter="{Binding}" />
                    </DataTemplate>
                </ListView.ItemTemplate>
                <ListView.ItemsPanel>
                    <ItemsPanelTemplate>
                        <WrapPanel />
                    </ItemsPanelTemplate>
                </ListView.ItemsPanel>
            </ListView>
            <TextBlock Text="{Binding Mensagem}" Foreground="Red" Margin="0,12,0,0" />
        </StackPanel>

        <Grid Visibility="{Binding ComandaAtual, Converter={StaticResource VisivelSeComandaAberta}}">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto" />
                <RowDefinition Height="*" />
            </Grid.RowDefinitions>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="1*" />
                <ColumnDefinition Width="16" />
                <ColumnDefinition Width="1*" />
            </Grid.ColumnDefinitions>

            <StackPanel Grid.Row="0" Grid.Column="0" Grid.ColumnSpan="3" Orientation="Horizontal" Margin="0,0,0,8">
                <TextBlock Text="{Binding ComandaAtual.Numero, StringFormat='Comanda {0}'}" FontWeight="Bold" FontSize="16" VerticalAlignment="Center" Margin="0,0,16,0" />
                <Button Content="Voltar para a grade" Command="{Binding FecharEdicaoCommand}" Margin="0,0,8,0" />
                <Button Content="Cancelar comanda" Command="{Binding CancelarComandaAtualCommand}" />
            </StackPanel>

            <StackPanel Grid.Row="1" Grid.Column="0">
                <StackPanel Orientation="Horizontal" Margin="0,0,0,8">
                    <ComboBox Width="160" ItemsSource="{Binding Categorias}" DisplayMemberPath="Nome"
                              SelectedItem="{Binding CategoriaCatalogo}" Margin="0,0,8,0" />
                    <TextBox Width="180" Text="{Binding TextoBuscaCatalogo, UpdateSourceTrigger=PropertyChanged}" />
                </StackPanel>
                <ItemsControl ItemsSource="{Binding ProdutosCatalogo}">
                    <ItemsControl.ItemsPanel>
                        <ItemsPanelTemplate>
                            <WrapPanel />
                        </ItemsPanelTemplate>
                    </ItemsControl.ItemsPanel>
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <Button Margin="0,0,8,8" Padding="8" Width="140" Height="60"
                                    Command="{Binding DataContext.AdicionarProdutoAoItemCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                    CommandParameter="{Binding}">
                                <StackPanel>
                                    <TextBlock Text="{Binding Nome}" TextWrapping="Wrap" />
                                    <TextBlock Text="{Binding PrecoCentavos}" />
                                </StackPanel>
                            </Button>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>
            </StackPanel>

            <StackPanel Grid.Row="1" Grid.Column="2">
                <ListView ItemsSource="{Binding Itens}">
                    <ListView.View>
                        <GridView>
                            <GridViewColumn Header="Produto" DisplayMemberBinding="{Binding NomeProduto}" Width="160" />
                            <GridViewColumn Header="Qtd" DisplayMemberBinding="{Binding Quantidade}" Width="50" />
                            <GridViewColumn Header="Subtotal" DisplayMemberBinding="{Binding SubtotalCentavos}" Width="90" />
                            <GridViewColumn Width="150">
                                <GridViewColumn.CellTemplate>
                                    <DataTemplate>
                                        <StackPanel Orientation="Horizontal">
                                            <Button Content="+" Width="28"
                                                    Command="{Binding DataContext.AumentarQuantidadeCommand, RelativeSource={RelativeSource AncestorType=ListView}}"
                                                    CommandParameter="{Binding}" Margin="0,0,4,0" />
                                            <Button Content="-" Width="28"
                                                    Command="{Binding DataContext.DiminuirQuantidadeCommand, RelativeSource={RelativeSource AncestorType=ListView}}"
                                                    CommandParameter="{Binding}" Margin="0,0,4,0" />
                                            <Button Content="Remover"
                                                    Command="{Binding DataContext.RemoverCommand, RelativeSource={RelativeSource AncestorType=ListView}}"
                                                    CommandParameter="{Binding}" />
                                        </StackPanel>
                                    </DataTemplate>
                                </GridViewColumn.CellTemplate>
                            </GridViewColumn>
                        </GridView>
                    </ListView.View>
                </ListView>
                <TextBlock Text="{Binding ComandaAtual.TotalCentavos, StringFormat='Total: {0} centavos'}" FontWeight="Bold" FontSize="16" Margin="0,12,0,0" />
                <TextBlock Text="{Binding Mensagem}" Foreground="Red" TextWrapping="Wrap" Margin="0,12,0,0" />
            </StackPanel>
        </Grid>
    </Grid>
</UserControl>
```

`backend/src/VarthexComanda.Desktop/Atendimento/AtendimentoView.xaml.cs`:

```csharp
using System.Windows.Controls;

namespace VarthexComanda.Desktop.Atendimento;

public partial class AtendimentoView : UserControl
{
    public AtendimentoView(AtendimentoViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
```

- [ ] **Step 3: Shell de navegação em `MainWindow`**

`backend/src/VarthexComanda.Desktop/MainWindow.xaml` (substituir todo o conteúdo):

```xml
<Window x:Class="VarthexComanda.Desktop.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        mc:Ignorable="d"
        Title="Varthex Comanda" Height="700" Width="1200">
    <DockPanel>
        <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Margin="8">
            <Button Content="Atendimento" Padding="12,4" Margin="0,0,8,0" Click="MostrarAtendimento_Click" />
            <Button Content="Produtos" Padding="12,4" Click="MostrarProdutos_Click" />
        </StackPanel>
        <ContentControl x:Name="ConteudoPrincipal" />
    </DockPanel>
</Window>
```

`backend/src/VarthexComanda.Desktop/MainWindow.xaml.cs` (substituir todo o conteúdo):

```csharp
using System.Windows;
using VarthexComanda.Desktop.Atendimento;
using VarthexComanda.Desktop.Catalogo;

namespace VarthexComanda.Desktop;

public partial class MainWindow : Window
{
    private readonly AtendimentoView _atendimentoView;
    private readonly ProdutosView _produtosView;

    public MainWindow(AtendimentoView atendimentoView, ProdutosView produtosView)
    {
        InitializeComponent();
        _atendimentoView = atendimentoView;
        _produtosView = produtosView;
        ConteudoPrincipal.Content = _atendimentoView;
    }

    private void MostrarAtendimento_Click(object sender, RoutedEventArgs e)
    {
        ConteudoPrincipal.Content = _atendimentoView;
    }

    private void MostrarProdutos_Click(object sender, RoutedEventArgs e)
    {
        ConteudoPrincipal.Content = _produtosView;
    }
}
```

- [ ] **Step 4: Registrar Atendimento no container de DI**

Em `backend/src/VarthexComanda.Desktop/App.xaml.cs`, adicione estes `using`:

```csharp
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Desktop.Atendimento;
using VarthexComanda.Infrastructure.Persistence.Atendimento;
```

E estas linhas ao `ServiceCollection`, logo após `services.AddTransient<PesquisarProdutos>();` (antes de `services.AddTransient<ProdutosViewModel>();`):

```csharp
services.AddTransient<IComandaRepository, EfComandaRepository>();
services.AddTransient<AbrirComanda>();
services.AddTransient<AdicionarItem>();
services.AddTransient<AlterarQuantidade>();
services.AddTransient<RemoverItem>();
services.AddTransient<CancelarComanda>();
services.AddTransient<AtendimentoViewModel>();
services.AddTransient<AtendimentoView>();
```

- [ ] **Step 5: Build e verificação manual do fluxo completo**

```powershell
$env:Path += ';C:\Program Files\dotnet'
dotnet build backend/VarthexComanda.slnx --configuration Release
dotnet run --project backend/src/VarthexComanda.Desktop --configuration Release
```

Esperado, em ordem: a janela abre no shell, já mostrando "Atendimento" (grade vazia); digitar um número e clicar "Abrir" cria a comanda e troca para a tela de itens; clicar num produto do catálogo rápido duas vezes soma quantidade 2 em uma única linha (não duas); os botões `+`/`-` recalculam subtotal e total; "Remover" tira o item e recalcula o total; "Voltar para a grade" mostra a comanda como ocupada; clicar nela de novo reabre para edição; "Cancelar comanda" volta para a grade com o número livre de novo; abrir o mesmo número duas vezes (a segunda enquanto a primeira ainda está aberta) mostra a mensagem de erro; o botão "Produtos" no topo troca para a tela de catálogo e continua funcionando como antes.

- [ ] **Step 6: Commit**

```powershell
git add backend/src/VarthexComanda.Desktop
git commit -m "feat: adiciona AtendimentoView e shell de navegacao"
```

---

### Task 9: Verificação final e changelog

**Files:**
- Modify: `docs/CHANGELOG.md`

**Interfaces:**
- Consumes: nothing new — verifica e documenta as Tasks 1-8.

- [ ] **Step 1: Full build and test pass in Release**

```powershell
$env:Path += ';C:\Program Files\dotnet'
dotnet build backend/VarthexComanda.slnx --configuration Release
dotnet test backend/VarthexComanda.slnx --configuration Release
```

Expected: 0 errors, 0 warnings. Domain.Tests: 4. Application.Tests: 37 (22 do catálogo + 15 desta fatia). Infrastructure.Tests: 28 (16 do catálogo + 12 desta fatia). Desktop.Tests: 7 (2 `ProdutosViewModelTests` + 5 `AtendimentoViewModelTests`).

- [ ] **Step 2: Add a CHANGELOG entry**

Add to the top of `docs/CHANGELOG.md`, above the existing `## 1.5 - 2026-09-17` entry:

```markdown
## 1.6 - 2026-09-17

- implementadas comandas (Etapa 3): grade de números abertos, abertura,
  catálogo rápido, lançamento/alteração/remoção de itens e cancelamento
  (RF06-13);
- lançar o mesmo produto duas vezes na mesma comanda incrementa a
  quantidade de uma única linha em vez de duplicar, desde que o preço
  não tenha mudado entre os dois lançamentos;
- abrir comanda não faz pré-checagem de número livre — insere direto e
  deixa o índice único do banco ser a fonte da verdade, cobrindo o caso
  de concorrência (RN01);
- `MainWindow` deixa de mostrar só Produtos e vira um shell com dois
  botões (Atendimento, Produtos) — Atendimento abre por padrão, conforme
  a tela inicial esperada;
- criado o projeto `VarthexComanda.Desktop.Tests`, com testes de unidade
  reais para `AtendimentoViewModel` e (retroativamente) `ProdutosViewModel`
  — fecha a lacuna que a Etapa 2 deixou aberta e que tinha deixado passar
  dois bugs reais;
- ainda sem encerramento nem venda (RF14-17) — entra na próxima fatia.

```

- [ ] **Step 3: Commit**

```powershell
git add docs/CHANGELOG.md
git commit -m "docs: registra comandas no changelog"
```

## Self-Review Notes

- **Spec coverage:** every bullet in [specs/2026-09-17-comandas-design.md](2026-09-17-comandas-design.md) maps to a task — shared types/exceptions → Task 1; `AbrirComanda`/`CancelarComanda` → Task 1; item use cases → Task 2; `EfComandaRepository` (read+abrir, concurrency test) → Task 3; `EfComandaRepository` (items+cancel) → Task 4; `Desktop.Tests` project + retroactive `ProdutosViewModel` coverage → Task 5; `AtendimentoViewModel` → Task 6; `ProdutosView` extraction → Task 7; `AtendimentoView` + navigation shell + full DI → Task 8; build/test/changelog → Task 9.
- **Type consistency:** `IComandaRepository`'s 7 method signatures are identical everywhere they're declared (Task 1), implemented (Tasks 3-4), and consumed (Tasks 2, 6). `AtendimentoViewModel`'s command method names (`Abrir`, `Remover`) were deliberately chosen to avoid colliding with the injected `AbrirComanda`/`RemoverItem` type names — checked against every XAML binding in Task 8 (`AbrirCommand`, `RemoverCommand`) to confirm they match.
- **Out of scope confirmed:** no encerramento/venda code touched (RF14-17 stays for the next slice); no changes to the already-existing Domain entities or EF migration (this slice's schema was already in place since the base slice).

