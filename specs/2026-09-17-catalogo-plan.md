# Catálogo (Etapa 2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver the catalog (categorias e produtos) business feature end to end — domain validation rules, EF Core repositories, and a working WPF screen — replacing the empty `MainWindow` from the base slice, and closing the DI/MVVM wiring gap that slice's final review left open.

**Architecture:** `Application/Catalogo` holds pure validation logic (use case classes + repository interfaces + a `Resultado<T>` result type, no EF Core, no WPF) → `Infrastructure/Persistence/Catalogo` implements the repositories against `VarthexComandaDbContext` via `IDbContextFactory` (short-lived contexts) → `Desktop/Catalogo` holds a CommunityToolkit.Mvvm `ProdutosViewModel` bound to `MainWindow`, composed through a `Microsoft.Extensions.DependencyInjection` container built in `App.xaml.cs`.

**Tech Stack:** C# / .NET 10, EF Core (`IDbContextFactory`), CommunityToolkit.Mvvm, `Microsoft.Extensions.DependencyInjection`, xUnit.

**Spec:** [specs/2026-09-17-catalogo-design.md](2026-09-17-catalogo-design.md)

## Global Constraints

- Money as `long` centavos, never `double`/`decimal` for storage — `decimal` is allowed only transiently in the UI layer to parse/format a `R$` text field, immediately converted to `long` centavos.
- Categoria/Produto names are trimmed before validation; empty or whitespace-only names are rejected.
- A produto can only be created/edited while pointing at an **active** categoria.
- Desativar never deletes — `Ativo = false`, row stays.
- Repositories use `IDbContextFactory<VarthexComandaDbContext>` — one short-lived `DbContext` per operation, never a shared/long-lived instance.
- ViewModels depend only on Application-layer use cases, never on `VarthexComandaDbContext` directly (docs/15).
- Search by name is case-insensitive.
- No automated WPF/ViewModel tests in this slice — verified manually, same as the base slice's `App.xaml.cs`/`MainWindow` work.

---

### Task 1: Application — `Resultado<T>` and repository interfaces

**Files:**
- Create: `backend/src/VarthexComanda.Application/Catalogo/Resultado.cs`
- Create: `backend/src/VarthexComanda.Application/Catalogo/ICategoriaRepository.cs`
- Create: `backend/src/VarthexComanda.Application/Catalogo/IProdutoRepository.cs`
- Test: `backend/tests/VarthexComanda.Application.Tests/Catalogo/ResultadoTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `Resultado<T> { bool Sucesso; T? Valor; IReadOnlyList<string> Erros; static Ok(T); static Falha(params string[]); }`, `ICategoriaRepository { Categoria? BuscarPorId(int); IReadOnlyList<Categoria> ListarAtivas(); bool ExisteNome(string, int? ignorarId = null); Categoria Salvar(Categoria); }`, `IProdutoRepository { Produto? BuscarPorId(int); IReadOnlyList<Produto> Pesquisar(int? categoriaId, string? texto); Produto Salvar(Produto); }` — all consumed by every later task.

- [ ] **Step 1: Write the failing test**

`backend/tests/VarthexComanda.Application.Tests/Catalogo/ResultadoTests.cs`:

```csharp
using VarthexComanda.Application.Catalogo;
using Xunit;

namespace VarthexComanda.Application.Tests.Catalogo;

public class ResultadoTests
{
    [Fact]
    public void Ok_ExpoeValorESucessoVerdadeiro()
    {
        var resultado = Resultado<int>.Ok(42);

        Assert.True(resultado.Sucesso);
        Assert.Equal(42, resultado.Valor);
        Assert.Empty(resultado.Erros);
    }

    [Fact]
    public void Falha_ExpoeErrosESucessoFalso()
    {
        var resultado = Resultado<int>.Falha("Erro 1", "Erro 2");

        Assert.False(resultado.Sucesso);
        Assert.Null(resultado.Valor);
        Assert.Equal(new[] { "Erro 1", "Erro 2" }, resultado.Erros);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```powershell
$env:Path += ';C:\Program Files\dotnet'
dotnet test backend/tests/VarthexComanda.Application.Tests --filter ResultadoTests
```

Expected: compilation error — `Resultado<T>` does not exist.

- [ ] **Step 3: Implement `Resultado<T>`**

`backend/src/VarthexComanda.Application/Catalogo/Resultado.cs`:

```csharp
namespace VarthexComanda.Application.Catalogo;

public class Resultado<T>
{
    private Resultado(bool sucesso, T? valor, IReadOnlyList<string> erros)
    {
        Sucesso = sucesso;
        Valor = valor;
        Erros = erros;
    }

    public bool Sucesso { get; }
    public T? Valor { get; }
    public IReadOnlyList<string> Erros { get; }

    public static Resultado<T> Ok(T valor) => new(true, valor, Array.Empty<string>());
    public static Resultado<T> Falha(params string[] erros) => new(false, default, erros);
}
```

- [ ] **Step 4: Implement the repository interfaces**

`backend/src/VarthexComanda.Application/Catalogo/ICategoriaRepository.cs`:

```csharp
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Catalogo;

public interface ICategoriaRepository
{
    Categoria? BuscarPorId(int id);
    IReadOnlyList<Categoria> ListarAtivas();
    bool ExisteNome(string nome, int? ignorarId = null);
    Categoria Salvar(Categoria categoria);
}
```

`backend/src/VarthexComanda.Application/Catalogo/IProdutoRepository.cs`:

```csharp
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Catalogo;

public interface IProdutoRepository
{
    Produto? BuscarPorId(int id);
    IReadOnlyList<Produto> Pesquisar(int? categoriaId, string? texto);
    Produto Salvar(Produto produto);
}
```

- [ ] **Step 5: Run test to verify it passes**

```powershell
$env:Path += ';C:\Program Files\dotnet'
dotnet test backend/tests/VarthexComanda.Application.Tests --filter ResultadoTests
```

Expected: 2 tests pass.

- [ ] **Step 6: Commit**

```powershell
git add backend/src/VarthexComanda.Application/Catalogo backend/tests/VarthexComanda.Application.Tests/Catalogo/ResultadoTests.cs
git commit -m "feat: adiciona Resultado<T> e contratos de repositorio do catalogo"
```

---

### Task 2: Application — casos de uso de Categoria

**Files:**
- Create: `backend/src/VarthexComanda.Application/Catalogo/CadastrarCategoria.cs`
- Create: `backend/src/VarthexComanda.Application/Catalogo/AlterarCategoria.cs`
- Create: `backend/src/VarthexComanda.Application/Catalogo/ListarCategoriasAtivas.cs`
- Test: `backend/tests/VarthexComanda.Application.Tests/Catalogo/FakeCategoriaRepository.cs`
- Test: `backend/tests/VarthexComanda.Application.Tests/Catalogo/FakeClock.cs`
- Test: `backend/tests/VarthexComanda.Application.Tests/Catalogo/CadastrarCategoriaTests.cs`
- Test: `backend/tests/VarthexComanda.Application.Tests/Catalogo/AlterarCategoriaTests.cs`
- Test: `backend/tests/VarthexComanda.Application.Tests/Catalogo/ListarCategoriasAtivasTests.cs`

**Interfaces:**
- Consumes: `Resultado<T>`, `ICategoriaRepository` from Task 1; `IClock` (`VarthexComanda.Application.Abstractions`, already exists from the base slice).
- Produces: `CadastrarCategoria(ICategoriaRepository, IClock) { Resultado<Categoria> Executar(string nome); }`, `AlterarCategoria(ICategoriaRepository, IClock) { Resultado<Categoria> Executar(int id, string nome, bool ativo); }`, `ListarCategoriasAtivas(ICategoriaRepository) { IReadOnlyList<Categoria> Executar(); }` — `CadastrarCategoria`/`AlterarCategoria` consumed by Task 3's tests (categoria setup) and Task 7's `ProdutosViewModel`; `FakeCategoriaRepository`/`FakeClock` reused verbatim by Task 3.

- [ ] **Step 1: Write the failing tests**

`backend/tests/VarthexComanda.Application.Tests/Catalogo/FakeCategoriaRepository.cs`:

```csharp
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Tests.Catalogo;

public class FakeCategoriaRepository : ICategoriaRepository
{
    private readonly List<Categoria> _categorias = new();
    private int _proximoId = 1;

    public Categoria? BuscarPorId(int id) => _categorias.FirstOrDefault(c => c.Id == id);

    public IReadOnlyList<Categoria> ListarAtivas() =>
        _categorias.Where(c => c.Ativo).OrderBy(c => c.Nome).ToList();

    public bool ExisteNome(string nome, int? ignorarId = null) =>
        _categorias.Any(c => c.Id != (ignorarId ?? 0) && string.Equals(c.Nome, nome, StringComparison.OrdinalIgnoreCase));

    public Categoria Salvar(Categoria categoria)
    {
        if (categoria.Id == 0)
        {
            categoria.Id = _proximoId++;
            _categorias.Add(categoria);
        }
        return categoria;
    }
}
```

`backend/tests/VarthexComanda.Application.Tests/Catalogo/FakeClock.cs`:

```csharp
using VarthexComanda.Application.Abstractions;

namespace VarthexComanda.Application.Tests.Catalogo;

public class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);
}
```

`backend/tests/VarthexComanda.Application.Tests/Catalogo/CadastrarCategoriaTests.cs`:

```csharp
using VarthexComanda.Application.Catalogo;
using Xunit;

namespace VarthexComanda.Application.Tests.Catalogo;

public class CadastrarCategoriaTests
{
    [Fact]
    public void Executar_NomeValido_CriaCategoriaAtiva()
    {
        var caso = new CadastrarCategoria(new FakeCategoriaRepository(), new FakeClock());

        var resultado = caso.Executar("Bebidas");

        Assert.True(resultado.Sucesso);
        Assert.Equal("Bebidas", resultado.Valor!.Nome);
        Assert.True(resultado.Valor.Ativo);
        Assert.True(resultado.Valor.Id > 0);
    }

    [Fact]
    public void Executar_NomeVazioOuComEspacos_Falha()
    {
        var caso = new CadastrarCategoria(new FakeCategoriaRepository(), new FakeClock());

        var resultado = caso.Executar("   ");

        Assert.False(resultado.Sucesso);
        Assert.Contains("Informe o nome da categoria.", resultado.Erros);
    }

    [Fact]
    public void Executar_NomeDuplicado_Falha()
    {
        var repositorio = new FakeCategoriaRepository();
        var caso = new CadastrarCategoria(repositorio, new FakeClock());
        caso.Executar("Bebidas");

        var resultado = caso.Executar("BEBIDAS");

        Assert.False(resultado.Sucesso);
        Assert.Contains("Já existe uma categoria com esse nome.", resultado.Erros);
    }
}
```

`backend/tests/VarthexComanda.Application.Tests/Catalogo/AlterarCategoriaTests.cs`:

```csharp
using VarthexComanda.Application.Catalogo;
using Xunit;

namespace VarthexComanda.Application.Tests.Catalogo;

public class AlterarCategoriaTests
{
    [Fact]
    public void Executar_CategoriaExistente_AtualizaNomeEAtivo()
    {
        var repositorio = new FakeCategoriaRepository();
        var criada = new CadastrarCategoria(repositorio, new FakeClock()).Executar("Bebidas").Valor!;
        var caso = new AlterarCategoria(repositorio, new FakeClock());

        var resultado = caso.Executar(criada.Id, "Bebidas Geladas", ativo: false);

        Assert.True(resultado.Sucesso);
        Assert.Equal("Bebidas Geladas", resultado.Valor!.Nome);
        Assert.False(resultado.Valor.Ativo);
    }

    [Fact]
    public void Executar_CategoriaInexistente_Falha()
    {
        var caso = new AlterarCategoria(new FakeCategoriaRepository(), new FakeClock());

        var resultado = caso.Executar(999, "Qualquer", ativo: true);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Categoria não encontrada.", resultado.Erros);
    }

    [Fact]
    public void Executar_NomeDuplicadoComOutraCategoria_Falha()
    {
        var repositorio = new FakeCategoriaRepository();
        var cadastrar = new CadastrarCategoria(repositorio, new FakeClock());
        cadastrar.Executar("Bebidas");
        var sobremesas = cadastrar.Executar("Sobremesas").Valor!;
        var caso = new AlterarCategoria(repositorio, new FakeClock());

        var resultado = caso.Executar(sobremesas.Id, "BEBIDAS", ativo: true);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Já existe uma categoria com esse nome.", resultado.Erros);
    }
}
```

`backend/tests/VarthexComanda.Application.Tests/Catalogo/ListarCategoriasAtivasTests.cs`:

```csharp
using VarthexComanda.Application.Catalogo;
using Xunit;

namespace VarthexComanda.Application.Tests.Catalogo;

public class ListarCategoriasAtivasTests
{
    [Fact]
    public void Executar_RetornaApenasAtivasOrdenadasPorNome()
    {
        var repositorio = new FakeCategoriaRepository();
        var cadastrar = new CadastrarCategoria(repositorio, new FakeClock());
        var bebidas = cadastrar.Executar("Bebidas").Valor!;
        cadastrar.Executar("Sobremesas");
        new AlterarCategoria(repositorio, new FakeClock()).Executar(bebidas.Id, bebidas.Nome, ativo: false);

        var ativas = new ListarCategoriasAtivas(repositorio).Executar();

        Assert.Single(ativas);
        Assert.Equal("Sobremesas", ativas[0].Nome);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile**

```powershell
$env:Path += ';C:\Program Files\dotnet'
dotnet test backend/tests/VarthexComanda.Application.Tests --filter "FullyQualifiedName~Catalogo"
```

Expected: compilation errors — `CadastrarCategoria`, `AlterarCategoria`, `ListarCategoriasAtivas` do not exist.

- [ ] **Step 3: Implement the use cases**

`backend/src/VarthexComanda.Application/Catalogo/CadastrarCategoria.cs`:

```csharp
using VarthexComanda.Application.Abstractions;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Catalogo;

public class CadastrarCategoria
{
    private readonly ICategoriaRepository _repositorio;
    private readonly IClock _relogio;

    public CadastrarCategoria(ICategoriaRepository repositorio, IClock relogio)
    {
        _repositorio = repositorio;
        _relogio = relogio;
    }

    public Resultado<Categoria> Executar(string nome)
    {
        var nomeNormalizado = (nome ?? string.Empty).Trim();
        if (nomeNormalizado.Length == 0)
        {
            return Resultado<Categoria>.Falha("Informe o nome da categoria.");
        }
        if (_repositorio.ExisteNome(nomeNormalizado))
        {
            return Resultado<Categoria>.Falha("Já existe uma categoria com esse nome.");
        }

        var agora = _relogio.UtcNow;
        var categoria = new Categoria
        {
            Id = 0,
            Nome = nomeNormalizado,
            Ativo = true,
            CriadoEm = agora,
            AtualizadoEm = agora
        };
        return Resultado<Categoria>.Ok(_repositorio.Salvar(categoria));
    }
}
```

`backend/src/VarthexComanda.Application/Catalogo/AlterarCategoria.cs`:

```csharp
using VarthexComanda.Application.Abstractions;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Catalogo;

public class AlterarCategoria
{
    private readonly ICategoriaRepository _repositorio;
    private readonly IClock _relogio;

    public AlterarCategoria(ICategoriaRepository repositorio, IClock relogio)
    {
        _repositorio = repositorio;
        _relogio = relogio;
    }

    public Resultado<Categoria> Executar(int id, string nome, bool ativo)
    {
        var categoria = _repositorio.BuscarPorId(id);
        if (categoria is null)
        {
            return Resultado<Categoria>.Falha("Categoria não encontrada.");
        }

        var nomeNormalizado = (nome ?? string.Empty).Trim();
        if (nomeNormalizado.Length == 0)
        {
            return Resultado<Categoria>.Falha("Informe o nome da categoria.");
        }
        if (_repositorio.ExisteNome(nomeNormalizado, ignorarId: id))
        {
            return Resultado<Categoria>.Falha("Já existe uma categoria com esse nome.");
        }

        categoria.Nome = nomeNormalizado;
        categoria.Ativo = ativo;
        categoria.AtualizadoEm = _relogio.UtcNow;
        return Resultado<Categoria>.Ok(_repositorio.Salvar(categoria));
    }
}
```

`backend/src/VarthexComanda.Application/Catalogo/ListarCategoriasAtivas.cs`:

```csharp
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Catalogo;

public class ListarCategoriasAtivas
{
    private readonly ICategoriaRepository _repositorio;

    public ListarCategoriasAtivas(ICategoriaRepository repositorio)
    {
        _repositorio = repositorio;
    }

    public IReadOnlyList<Categoria> Executar() => _repositorio.ListarAtivas();
}
```

- [ ] **Step 4: Run tests to verify they pass**

```powershell
$env:Path += ';C:\Program Files\dotnet'
dotnet test backend/tests/VarthexComanda.Application.Tests --filter "FullyQualifiedName~Catalogo"
```

Expected: 9 tests pass (2 from `ResultadoTests` + 3 `CadastrarCategoriaTests` + 3 `AlterarCategoriaTests` + 1 `ListarCategoriasAtivasTests`).

- [ ] **Step 5: Commit**

```powershell
git add backend/src/VarthexComanda.Application/Catalogo backend/tests/VarthexComanda.Application.Tests/Catalogo
git commit -m "feat: adiciona casos de uso de categoria (cadastrar, alterar, listar ativas)"
```

---

### Task 3: Application — casos de uso de Produto

**Files:**
- Create: `backend/src/VarthexComanda.Application/Catalogo/CadastrarProduto.cs`
- Create: `backend/src/VarthexComanda.Application/Catalogo/AlterarProduto.cs`
- Create: `backend/src/VarthexComanda.Application/Catalogo/DesativarProduto.cs`
- Create: `backend/src/VarthexComanda.Application/Catalogo/PesquisarProdutos.cs`
- Test: `backend/tests/VarthexComanda.Application.Tests/Catalogo/FakeProdutoRepository.cs`
- Test: `backend/tests/VarthexComanda.Application.Tests/Catalogo/CadastrarProdutoTests.cs`
- Test: `backend/tests/VarthexComanda.Application.Tests/Catalogo/AlterarProdutoTests.cs`
- Test: `backend/tests/VarthexComanda.Application.Tests/Catalogo/DesativarProdutoTests.cs`
- Test: `backend/tests/VarthexComanda.Application.Tests/Catalogo/PesquisarProdutosTests.cs`

**Interfaces:**
- Consumes: `Resultado<T>`, `IProdutoRepository` from Task 1; `ICategoriaRepository`, `CadastrarCategoria`, `AlterarCategoria`, `FakeCategoriaRepository`, `FakeClock` from Task 2 (reuse the exact files at `backend/tests/VarthexComanda.Application.Tests/Catalogo/FakeCategoriaRepository.cs` and `.../FakeClock.cs` — do not redefine them, that would be a duplicate-class compile error).
- Produces: `CadastrarProduto(IProdutoRepository, ICategoriaRepository, IClock) { Resultado<Produto> Executar(string nome, int categoriaId, long precoCentavos); }`, `AlterarProduto(IProdutoRepository, ICategoriaRepository, IClock) { Resultado<Produto> Executar(int id, string nome, int categoriaId, long precoCentavos, bool ativo); }`, `DesativarProduto(IProdutoRepository, IClock) { Resultado<Produto> Executar(int id); }`, `PesquisarProdutos(IProdutoRepository) { IReadOnlyList<Produto> Executar(int? categoriaId, string? texto); }` — all four consumed by Task 5's tests (indirectly, via repository behavior) and Task 7's `ProdutosViewModel`.

- [ ] **Step 1: Write the failing tests**

`backend/tests/VarthexComanda.Application.Tests/Catalogo/FakeProdutoRepository.cs`:

```csharp
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Tests.Catalogo;

public class FakeProdutoRepository : IProdutoRepository
{
    private readonly List<Produto> _produtos = new();
    private int _proximoId = 1;

    public Produto? BuscarPorId(int id) => _produtos.FirstOrDefault(p => p.Id == id);

    public IReadOnlyList<Produto> Pesquisar(int? categoriaId, string? texto)
    {
        var consulta = _produtos.AsEnumerable();
        if (categoriaId is not null)
        {
            consulta = consulta.Where(p => p.CategoriaId == categoriaId);
        }
        if (!string.IsNullOrWhiteSpace(texto))
        {
            consulta = consulta.Where(p => p.Nome.Contains(texto, StringComparison.OrdinalIgnoreCase));
        }
        return consulta.ToList();
    }

    public Produto Salvar(Produto produto)
    {
        if (produto.Id == 0)
        {
            produto.Id = _proximoId++;
            _produtos.Add(produto);
        }
        return produto;
    }
}
```

`backend/tests/VarthexComanda.Application.Tests/Catalogo/CadastrarProdutoTests.cs`:

```csharp
using VarthexComanda.Application.Catalogo;
using Xunit;

namespace VarthexComanda.Application.Tests.Catalogo;

public class CadastrarProdutoTests
{
    private static (FakeCategoriaRepository categorias, int ativaId, int inativaId) PrepararCategorias()
    {
        var categorias = new FakeCategoriaRepository();
        var cadastrar = new CadastrarCategoria(categorias, new FakeClock());
        var ativa = cadastrar.Executar("Bebidas").Valor!;
        var inativa = cadastrar.Executar("Descontinuada").Valor!;
        new AlterarCategoria(categorias, new FakeClock()).Executar(inativa.Id, inativa.Nome, ativo: false);
        return (categorias, ativa.Id, inativa.Id);
    }

    [Fact]
    public void Executar_DadosValidos_CriaProdutoAtivo()
    {
        var (categorias, ativaId, _) = PrepararCategorias();
        var caso = new CadastrarProduto(new FakeProdutoRepository(), categorias, new FakeClock());

        var resultado = caso.Executar("Refrigerante", ativaId, 500);

        Assert.True(resultado.Sucesso);
        Assert.Equal("Refrigerante", resultado.Valor!.Nome);
        Assert.True(resultado.Valor.Ativo);
        Assert.Equal(500, resultado.Valor.PrecoCentavos);
    }

    [Fact]
    public void Executar_NomeVazio_Falha()
    {
        var (categorias, ativaId, _) = PrepararCategorias();
        var caso = new CadastrarProduto(new FakeProdutoRepository(), categorias, new FakeClock());

        var resultado = caso.Executar("   ", ativaId, 500);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Informe o nome do produto.", resultado.Erros);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Executar_PrecoZeroOuNegativo_Falha(long preco)
    {
        var (categorias, ativaId, _) = PrepararCategorias();
        var caso = new CadastrarProduto(new FakeProdutoRepository(), categorias, new FakeClock());

        var resultado = caso.Executar("Refrigerante", ativaId, preco);

        Assert.False(resultado.Sucesso);
        Assert.Contains("O preço deve ser maior que zero.", resultado.Erros);
    }

    [Fact]
    public void Executar_CategoriaInativa_Falha()
    {
        var (categorias, _, inativaId) = PrepararCategorias();
        var caso = new CadastrarProduto(new FakeProdutoRepository(), categorias, new FakeClock());

        var resultado = caso.Executar("Refrigerante", inativaId, 500);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Selecione uma categoria ativa.", resultado.Erros);
    }

    [Fact]
    public void Executar_CategoriaInexistente_Falha()
    {
        var caso = new CadastrarProduto(new FakeProdutoRepository(), new FakeCategoriaRepository(), new FakeClock());

        var resultado = caso.Executar("Refrigerante", 999, 500);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Selecione uma categoria ativa.", resultado.Erros);
    }
}
```

`backend/tests/VarthexComanda.Application.Tests/Catalogo/AlterarProdutoTests.cs`:

```csharp
using VarthexComanda.Application.Catalogo;
using Xunit;

namespace VarthexComanda.Application.Tests.Catalogo;

public class AlterarProdutoTests
{
    [Fact]
    public void Executar_DadosValidos_AtualizaCampos()
    {
        var categorias = new FakeCategoriaRepository();
        var categoria = new CadastrarCategoria(categorias, new FakeClock()).Executar("Bebidas").Valor!;
        var produtos = new FakeProdutoRepository();
        var produto = new CadastrarProduto(produtos, categorias, new FakeClock()).Executar("Refrigerante", categoria.Id, 500).Valor!;
        var caso = new AlterarProduto(produtos, categorias, new FakeClock());

        var resultado = caso.Executar(produto.Id, "Refrigerante Lata", categoria.Id, 600, ativo: false);

        Assert.True(resultado.Sucesso);
        Assert.Equal("Refrigerante Lata", resultado.Valor!.Nome);
        Assert.Equal(600, resultado.Valor.PrecoCentavos);
        Assert.False(resultado.Valor.Ativo);
    }

    [Fact]
    public void Executar_ProdutoInexistente_Falha()
    {
        var caso = new AlterarProduto(new FakeProdutoRepository(), new FakeCategoriaRepository(), new FakeClock());

        var resultado = caso.Executar(999, "Qualquer", 1, 500, ativo: true);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Produto não encontrado.", resultado.Erros);
    }

    [Fact]
    public void Executar_CategoriaInativa_Falha()
    {
        var categorias = new FakeCategoriaRepository();
        var cadastrarCategoria = new CadastrarCategoria(categorias, new FakeClock());
        var ativa = cadastrarCategoria.Executar("Bebidas").Valor!;
        var inativa = cadastrarCategoria.Executar("Descontinuada").Valor!;
        new AlterarCategoria(categorias, new FakeClock()).Executar(inativa.Id, inativa.Nome, ativo: false);
        var produtos = new FakeProdutoRepository();
        var produto = new CadastrarProduto(produtos, categorias, new FakeClock()).Executar("Refrigerante", ativa.Id, 500).Valor!;
        var caso = new AlterarProduto(produtos, categorias, new FakeClock());

        var resultado = caso.Executar(produto.Id, "Refrigerante", inativa.Id, 500, ativo: true);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Selecione uma categoria ativa.", resultado.Erros);
    }
}
```

`backend/tests/VarthexComanda.Application.Tests/Catalogo/DesativarProdutoTests.cs`:

```csharp
using VarthexComanda.Application.Catalogo;
using Xunit;

namespace VarthexComanda.Application.Tests.Catalogo;

public class DesativarProdutoTests
{
    [Fact]
    public void Executar_ProdutoExistente_DesativaSemExcluir()
    {
        var categorias = new FakeCategoriaRepository();
        var categoria = new CadastrarCategoria(categorias, new FakeClock()).Executar("Bebidas").Valor!;
        var produtos = new FakeProdutoRepository();
        var produto = new CadastrarProduto(produtos, categorias, new FakeClock()).Executar("Refrigerante", categoria.Id, 500).Valor!;
        var caso = new DesativarProduto(produtos, new FakeClock());

        var resultado = caso.Executar(produto.Id);

        Assert.True(resultado.Sucesso);
        Assert.False(resultado.Valor!.Ativo);
        Assert.NotNull(produtos.BuscarPorId(produto.Id));
    }

    [Fact]
    public void Executar_ProdutoInexistente_Falha()
    {
        var caso = new DesativarProduto(new FakeProdutoRepository(), new FakeClock());

        var resultado = caso.Executar(999);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Produto não encontrado.", resultado.Erros);
    }
}
```

`backend/tests/VarthexComanda.Application.Tests/Catalogo/PesquisarProdutosTests.cs`:

```csharp
using VarthexComanda.Application.Catalogo;
using Xunit;

namespace VarthexComanda.Application.Tests.Catalogo;

public class PesquisarProdutosTests
{
    [Fact]
    public void Executar_SemFiltro_RetornaTodos()
    {
        var categorias = new FakeCategoriaRepository();
        var categoria = new CadastrarCategoria(categorias, new FakeClock()).Executar("Bebidas").Valor!;
        var produtos = new FakeProdutoRepository();
        var cadastrarProduto = new CadastrarProduto(produtos, categorias, new FakeClock());
        cadastrarProduto.Executar("Refrigerante", categoria.Id, 500);
        cadastrarProduto.Executar("Suco", categoria.Id, 700);

        var resultado = new PesquisarProdutos(produtos).Executar(null, null);

        Assert.Equal(2, resultado.Count);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile**

```powershell
$env:Path += ';C:\Program Files\dotnet'
dotnet test backend/tests/VarthexComanda.Application.Tests --filter "FullyQualifiedName~Catalogo"
```

Expected: compilation errors — `CadastrarProduto`, `AlterarProduto`, `DesativarProduto`, `PesquisarProdutos`, `FakeProdutoRepository` do not exist.

- [ ] **Step 3: Implement the use cases**

`backend/src/VarthexComanda.Application/Catalogo/CadastrarProduto.cs`:

```csharp
using VarthexComanda.Application.Abstractions;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Catalogo;

public class CadastrarProduto
{
    private readonly IProdutoRepository _produtos;
    private readonly ICategoriaRepository _categorias;
    private readonly IClock _relogio;

    public CadastrarProduto(IProdutoRepository produtos, ICategoriaRepository categorias, IClock relogio)
    {
        _produtos = produtos;
        _categorias = categorias;
        _relogio = relogio;
    }

    public Resultado<Produto> Executar(string nome, int categoriaId, long precoCentavos)
    {
        var nomeNormalizado = (nome ?? string.Empty).Trim();
        if (nomeNormalizado.Length == 0)
        {
            return Resultado<Produto>.Falha("Informe o nome do produto.");
        }
        if (precoCentavos <= 0)
        {
            return Resultado<Produto>.Falha("O preço deve ser maior que zero.");
        }
        var categoria = _categorias.BuscarPorId(categoriaId);
        if (categoria is null || !categoria.Ativo)
        {
            return Resultado<Produto>.Falha("Selecione uma categoria ativa.");
        }

        var agora = _relogio.UtcNow;
        var produto = new Produto
        {
            Id = 0,
            CategoriaId = categoriaId,
            Nome = nomeNormalizado,
            PrecoCentavos = precoCentavos,
            Ativo = true,
            CriadoEm = agora,
            AtualizadoEm = agora
        };
        return Resultado<Produto>.Ok(_produtos.Salvar(produto));
    }
}
```

`backend/src/VarthexComanda.Application/Catalogo/AlterarProduto.cs`:

```csharp
using VarthexComanda.Application.Abstractions;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Catalogo;

public class AlterarProduto
{
    private readonly IProdutoRepository _produtos;
    private readonly ICategoriaRepository _categorias;
    private readonly IClock _relogio;

    public AlterarProduto(IProdutoRepository produtos, ICategoriaRepository categorias, IClock relogio)
    {
        _produtos = produtos;
        _categorias = categorias;
        _relogio = relogio;
    }

    public Resultado<Produto> Executar(int id, string nome, int categoriaId, long precoCentavos, bool ativo)
    {
        var produto = _produtos.BuscarPorId(id);
        if (produto is null)
        {
            return Resultado<Produto>.Falha("Produto não encontrado.");
        }

        var nomeNormalizado = (nome ?? string.Empty).Trim();
        if (nomeNormalizado.Length == 0)
        {
            return Resultado<Produto>.Falha("Informe o nome do produto.");
        }
        if (precoCentavos <= 0)
        {
            return Resultado<Produto>.Falha("O preço deve ser maior que zero.");
        }
        var categoria = _categorias.BuscarPorId(categoriaId);
        if (categoria is null || !categoria.Ativo)
        {
            return Resultado<Produto>.Falha("Selecione uma categoria ativa.");
        }

        produto.Nome = nomeNormalizado;
        produto.CategoriaId = categoriaId;
        produto.PrecoCentavos = precoCentavos;
        produto.Ativo = ativo;
        produto.AtualizadoEm = _relogio.UtcNow;
        return Resultado<Produto>.Ok(_produtos.Salvar(produto));
    }
}
```

`backend/src/VarthexComanda.Application/Catalogo/DesativarProduto.cs`:

```csharp
using VarthexComanda.Application.Abstractions;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Catalogo;

public class DesativarProduto
{
    private readonly IProdutoRepository _produtos;
    private readonly IClock _relogio;

    public DesativarProduto(IProdutoRepository produtos, IClock relogio)
    {
        _produtos = produtos;
        _relogio = relogio;
    }

    public Resultado<Produto> Executar(int id)
    {
        var produto = _produtos.BuscarPorId(id);
        if (produto is null)
        {
            return Resultado<Produto>.Falha("Produto não encontrado.");
        }

        produto.Ativo = false;
        produto.AtualizadoEm = _relogio.UtcNow;
        return Resultado<Produto>.Ok(_produtos.Salvar(produto));
    }
}
```

`backend/src/VarthexComanda.Application/Catalogo/PesquisarProdutos.cs`:

```csharp
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Catalogo;

public class PesquisarProdutos
{
    private readonly IProdutoRepository _produtos;

    public PesquisarProdutos(IProdutoRepository produtos)
    {
        _produtos = produtos;
    }

    public IReadOnlyList<Produto> Executar(int? categoriaId, string? texto) => _produtos.Pesquisar(categoriaId, texto);
}
```

- [ ] **Step 4: Run tests to verify they pass**

```powershell
$env:Path += ';C:\Program Files\dotnet'
dotnet test backend/tests/VarthexComanda.Application.Tests
```

Expected: 21 tests pass total (9 from Task 2 + 6 `CadastrarProdutoTests` [including the 2-case `Theory`] + 3 `AlterarProdutoTests` + 2 `DesativarProdutoTests` + 1 `PesquisarProdutosTests`).

- [ ] **Step 5: Commit**

```powershell
git add backend/src/VarthexComanda.Application/Catalogo backend/tests/VarthexComanda.Application.Tests/Catalogo
git commit -m "feat: adiciona casos de uso de produto (cadastrar, alterar, desativar, pesquisar)"
```

---

### Task 4: Infrastructure — `EfCategoriaRepository`

**Files:**
- Create: `backend/src/VarthexComanda.Infrastructure/Persistence/Catalogo/EfCategoriaRepository.cs`
- Test: `backend/tests/VarthexComanda.Infrastructure.Tests/Catalogo/EfCategoriaRepositoryTests.cs`

**Interfaces:**
- Consumes: `ICategoriaRepository` (Task 1), `VarthexComandaDbContext` (base slice).
- Produces: `EfCategoriaRepository(IDbContextFactory<VarthexComandaDbContext>) : ICategoriaRepository` — consumed by Task 7's DI registration.

- [ ] **Step 1: Add the DI package to the test project**

```powershell
$env:Path += ';C:\Program Files\dotnet'
dotnet add backend/tests/VarthexComanda.Infrastructure.Tests package Microsoft.Extensions.DependencyInjection
```

- [ ] **Step 2: Write the failing tests**

`backend/tests/VarthexComanda.Infrastructure.Tests/Catalogo/EfCategoriaRepositoryTests.cs`:

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Persistence;
using VarthexComanda.Infrastructure.Persistence.Catalogo;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests.Catalogo;

public class EfCategoriaRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ServiceProvider _provedor;
    private readonly IDbContextFactory<VarthexComandaDbContext> _fabrica;

    public EfCategoriaRepositoryTests()
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
    public void Salvar_CategoriaNova_AtribuiIdEPersiste()
    {
        var repositorio = new EfCategoriaRepository(_fabrica);
        var agora = DateTime.UtcNow;

        var salva = repositorio.Salvar(new Categoria { Id = 0, Nome = "Bebidas", Ativo = true, CriadoEm = agora, AtualizadoEm = agora });

        Assert.True(salva.Id > 0);
        var carregada = repositorio.BuscarPorId(salva.Id);
        Assert.NotNull(carregada);
        Assert.Equal("Bebidas", carregada!.Nome);
    }

    [Fact]
    public void ListarAtivas_IgnoraInativas_OrdenaPorNome()
    {
        var repositorio = new EfCategoriaRepository(_fabrica);
        var agora = DateTime.UtcNow;
        repositorio.Salvar(new Categoria { Id = 0, Nome = "Sobremesas", Ativo = true, CriadoEm = agora, AtualizadoEm = agora });
        repositorio.Salvar(new Categoria { Id = 0, Nome = "Bebidas", Ativo = true, CriadoEm = agora, AtualizadoEm = agora });
        repositorio.Salvar(new Categoria { Id = 0, Nome = "Descontinuada", Ativo = false, CriadoEm = agora, AtualizadoEm = agora });

        var ativas = repositorio.ListarAtivas();

        Assert.Equal(new[] { "Bebidas", "Sobremesas" }, ativas.Select(c => c.Nome).ToArray());
    }

    [Fact]
    public void ExisteNome_ComparaSemDiferenciarMaiusculas_EIgnoraOProprioId()
    {
        var repositorio = new EfCategoriaRepository(_fabrica);
        var agora = DateTime.UtcNow;
        var categoria = repositorio.Salvar(new Categoria { Id = 0, Nome = "Bebidas", Ativo = true, CriadoEm = agora, AtualizadoEm = agora });

        Assert.True(repositorio.ExisteNome("BEBIDAS"));
        Assert.False(repositorio.ExisteNome("BEBIDAS", ignorarId: categoria.Id));
        Assert.False(repositorio.ExisteNome("Sobremesas"));
    }
}
```

- [ ] **Step 3: Run tests to verify they fail to compile**

```powershell
$env:Path += ';C:\Program Files\dotnet'
dotnet test backend/tests/VarthexComanda.Infrastructure.Tests --filter "FullyQualifiedName~Catalogo"
```

Expected: compilation error — `EfCategoriaRepository` does not exist.

- [ ] **Step 4: Implement `EfCategoriaRepository`**

`backend/src/VarthexComanda.Infrastructure/Persistence/Catalogo/EfCategoriaRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;

namespace VarthexComanda.Infrastructure.Persistence.Catalogo;

public class EfCategoriaRepository : ICategoriaRepository
{
    private readonly IDbContextFactory<VarthexComandaDbContext> _fabricaContexto;

    public EfCategoriaRepository(IDbContextFactory<VarthexComandaDbContext> fabricaContexto)
    {
        _fabricaContexto = fabricaContexto;
    }

    public Categoria? BuscarPorId(int id)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        return contexto.Categorias.SingleOrDefault(c => c.Id == id);
    }

    public IReadOnlyList<Categoria> ListarAtivas()
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        return contexto.Categorias.Where(c => c.Ativo).OrderBy(c => c.Nome).ToList();
    }

    public bool ExisteNome(string nome, int? ignorarId = null)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        return contexto.Categorias.Any(c => c.Nome == nome && c.Id != (ignorarId ?? 0));
    }

    public Categoria Salvar(Categoria categoria)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        if (categoria.Id == 0)
        {
            contexto.Categorias.Add(categoria);
        }
        else
        {
            contexto.Categorias.Update(categoria);
        }
        contexto.SaveChanges();
        return categoria;
    }
}
```

Note: `c.Nome == nome` relies on `categoria.nome`'s `COLLATE NOCASE` (set in `CategoriaConfiguration`) to make the comparison case-insensitive at the SQL level — this is why `ExisteNome_ComparaSemDiferenciarMaiusculas...` above is expected to pass without any `.ToLower()` call here.

- [ ] **Step 5: Run tests to verify they pass**

```powershell
$env:Path += ';C:\Program Files\dotnet'
dotnet test backend/tests/VarthexComanda.Infrastructure.Tests --filter "FullyQualifiedName~Catalogo"
```

Expected: 3 tests pass.

- [ ] **Step 6: Commit**

```powershell
git add backend/src/VarthexComanda.Infrastructure/Persistence/Catalogo backend/tests/VarthexComanda.Infrastructure.Tests/Catalogo/EfCategoriaRepositoryTests.cs backend/tests/VarthexComanda.Infrastructure.Tests/VarthexComanda.Infrastructure.Tests.csproj
git commit -m "feat: adiciona EfCategoriaRepository"
```

---

### Task 5: Infrastructure — `EfProdutoRepository`

**Files:**
- Create: `backend/src/VarthexComanda.Infrastructure/Persistence/Catalogo/EfProdutoRepository.cs`
- Test: `backend/tests/VarthexComanda.Infrastructure.Tests/Catalogo/EfProdutoRepositoryTests.cs`

**Interfaces:**
- Consumes: `IProdutoRepository` (Task 1), `VarthexComandaDbContext` (base slice). Follow the exact same `ServiceCollection`/`IDbContextFactory` test setup pattern as `EfCategoriaRepositoryTests.cs` from Task 4 — that file already exists and the `Microsoft.Extensions.DependencyInjection` package is already added to the test project.
- Produces: `EfProdutoRepository(IDbContextFactory<VarthexComandaDbContext>) : IProdutoRepository` — consumed by Task 7's DI registration.

- [ ] **Step 1: Write the failing tests**

`backend/tests/VarthexComanda.Infrastructure.Tests/Catalogo/EfProdutoRepositoryTests.cs`:

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Persistence;
using VarthexComanda.Infrastructure.Persistence.Catalogo;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests.Catalogo;

public class EfProdutoRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ServiceProvider _provedor;
    private readonly IDbContextFactory<VarthexComandaDbContext> _fabrica;
    private readonly int _categoriaId;

    public EfProdutoRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"varthex-comanda-tests-{Guid.NewGuid()}.db");

        var servicos = new ServiceCollection();
        servicos.AddDbContextFactory<VarthexComandaDbContext>(options =>
            options.UseSqlite($"Data Source={_dbPath};Foreign Keys=True"));
        _provedor = servicos.BuildServiceProvider();
        _fabrica = _provedor.GetRequiredService<IDbContextFactory<VarthexComandaDbContext>>();

        using var contexto = _fabrica.CreateDbContext();
        contexto.Database.Migrate();
        var agora = DateTime.UtcNow;
        var categoria = new Categoria { Id = 0, Nome = "Bebidas", Ativo = true, CriadoEm = agora, AtualizadoEm = agora };
        contexto.Categorias.Add(categoria);
        contexto.SaveChanges();
        _categoriaId = categoria.Id;
    }

    public void Dispose()
    {
        _provedor.Dispose();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public void Salvar_ProdutoNovo_AtribuiIdEPersiste()
    {
        var repositorio = new EfProdutoRepository(_fabrica);
        var agora = DateTime.UtcNow;

        var salvo = repositorio.Salvar(new Produto
        {
            Id = 0, CategoriaId = _categoriaId, Nome = "Refrigerante", PrecoCentavos = 500,
            Ativo = true, CriadoEm = agora, AtualizadoEm = agora
        });

        Assert.True(salvo.Id > 0);
        var carregado = repositorio.BuscarPorId(salvo.Id);
        Assert.NotNull(carregado);
        Assert.Equal("Refrigerante", carregado!.Nome);
    }

    [Fact]
    public void Pesquisar_FiltraPorCategoriaEPorTextoSemDiferenciarMaiusculas()
    {
        var repositorio = new EfProdutoRepository(_fabrica);
        var agora = DateTime.UtcNow;
        repositorio.Salvar(new Produto { Id = 0, CategoriaId = _categoriaId, Nome = "Refrigerante", PrecoCentavos = 500, Ativo = true, CriadoEm = agora, AtualizadoEm = agora });
        repositorio.Salvar(new Produto { Id = 0, CategoriaId = _categoriaId, Nome = "Suco Natural", PrecoCentavos = 700, Ativo = true, CriadoEm = agora, AtualizadoEm = agora });

        var porTexto = repositorio.Pesquisar(null, "REFRI");
        Assert.Single(porTexto);
        Assert.Equal("Refrigerante", porTexto[0].Nome);

        var porCategoria = repositorio.Pesquisar(_categoriaId, null);
        Assert.Equal(2, porCategoria.Count);
    }

    [Fact]
    public void Salvar_ProdutoExistente_AtualizaSemCriarNovoRegistro()
    {
        var repositorio = new EfProdutoRepository(_fabrica);
        var agora = DateTime.UtcNow;
        var produto = repositorio.Salvar(new Produto { Id = 0, CategoriaId = _categoriaId, Nome = "Refrigerante", PrecoCentavos = 500, Ativo = true, CriadoEm = agora, AtualizadoEm = agora });

        produto.PrecoCentavos = 600;
        repositorio.Salvar(produto);

        var carregado = repositorio.BuscarPorId(produto.Id);
        Assert.Equal(600, carregado!.PrecoCentavos);
        Assert.Single(repositorio.Pesquisar(null, null));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile**

```powershell
$env:Path += ';C:\Program Files\dotnet'
dotnet test backend/tests/VarthexComanda.Infrastructure.Tests --filter "FullyQualifiedName~Catalogo"
```

Expected: compilation error — `EfProdutoRepository` does not exist.

- [ ] **Step 3: Implement `EfProdutoRepository`**

`backend/src/VarthexComanda.Infrastructure/Persistence/Catalogo/EfProdutoRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;

namespace VarthexComanda.Infrastructure.Persistence.Catalogo;

public class EfProdutoRepository : IProdutoRepository
{
    private readonly IDbContextFactory<VarthexComandaDbContext> _fabricaContexto;

    public EfProdutoRepository(IDbContextFactory<VarthexComandaDbContext> fabricaContexto)
    {
        _fabricaContexto = fabricaContexto;
    }

    public Produto? BuscarPorId(int id)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        return contexto.Produtos.SingleOrDefault(p => p.Id == id);
    }

    public IReadOnlyList<Produto> Pesquisar(int? categoriaId, string? texto)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        var consulta = contexto.Produtos.AsQueryable();

        if (categoriaId is not null)
        {
            consulta = consulta.Where(p => p.CategoriaId == categoriaId);
        }

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var textoBusca = texto.Trim().ToLower();
            consulta = consulta.Where(p => p.Nome.ToLower().Contains(textoBusca));
        }

        return consulta.OrderBy(p => p.Nome).ToList();
    }

    public Produto Salvar(Produto produto)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        if (produto.Id == 0)
        {
            contexto.Produtos.Add(produto);
        }
        else
        {
            contexto.Produtos.Update(produto);
        }
        contexto.SaveChanges();
        return produto;
    }
}
```

Note: `produto.nome` has no `COLLATE NOCASE` in `docs/database/schema.sql` (unlike `categoria.nome`) — hence the explicit `.ToLower()` on both sides here, unlike `EfCategoriaRepository.ExisteNome`. Use plain ASCII product names in tests (as above) — SQLite's built-in `LOWER()` only lowercases ASCII by default, so an accented test name could produce a false failure unrelated to this feature.

- [ ] **Step 4: Run tests to verify they pass**

```powershell
$env:Path += ';C:\Program Files\dotnet'
dotnet test backend/tests/VarthexComanda.Infrastructure.Tests
```

Expected: all Infrastructure tests pass — 10 from the base slice + 3 `EfCategoriaRepositoryTests` + 3 `EfProdutoRepositoryTests` = 16.

- [ ] **Step 5: Commit**

```powershell
git add backend/src/VarthexComanda.Infrastructure/Persistence/Catalogo/EfProdutoRepository.cs backend/tests/VarthexComanda.Infrastructure.Tests/Catalogo/EfProdutoRepositoryTests.cs
git commit -m "feat: adiciona EfProdutoRepository"
```

---

### Task 6: Desktop — troca a conexão manual por `IDbContextFactory` via DI

**Files:**
- Modify: `backend/src/VarthexComanda.Desktop/App.xaml.cs`

**Interfaces:**
- Consumes: `VarthexComandaDbContext`, `AppPaths`, `IClock`/`SystemClock`, `DatabaseBackupService`, `SingleInstanceGuard`, `LoggingConfigurator` (all from the base slice) — no new production types.
- Produces: a `ServiceProvider? _serviceProvider` field on `App`, built with `IDbContextFactory<VarthexComandaDbContext>` already registered — consumed by Task 7, which adds more registrations to the same `ServiceCollection` before it's built.

This task is a **parity-preserving refactor**: it must not change any user-visible behavior (log lines, message boxes, single-instance behavior) — it only swaps how the `DbContext` is constructed, from a manually-opened `SqliteConnection` to a DI-registered `IDbContextFactory`. There is no new test — verify by manual run, comparing against the base slice's known-good behavior.

- [ ] **Step 1: Replace the manual connection/DbContext construction with a DI container**

Replace the full contents of `backend/src/VarthexComanda.Desktop/App.xaml.cs` with:

```csharp
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    private ServiceProvider? _serviceProvider;

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

        var services = new ServiceCollection();
        services.AddSingleton(paths);
        services.AddSingleton(_logger);
        services.AddSingleton<IClock, SystemClock>();
        services.AddDbContextFactory<VarthexComandaDbContext>(options =>
            options.UseSqlite($"Data Source={paths.DatabasePath};Foreign Keys=True"));

        _serviceProvider = services.BuildServiceProvider();

        try
        {
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

            dbContext.Database.Migrate();

            var linhas = dbContext.Database
                .SqlQueryRaw<string>("PRAGMA integrity_check")
                .AsEnumerable()
                .ToList();
            if (linhas.Count != 1 || linhas[0] != "ok")
            {
                _logger.Error("PRAGMA integrity_check retornou {Linhas}", string.Join("; ", linhas));
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
        _serviceProvider?.Dispose();
        (_logger as IDisposable)?.Dispose();
        _guard?.Dispose();
        base.OnExit(e);
    }
}
```

- [ ] **Step 2: Build and manually verify parity with the base slice**

```powershell
$env:Path += ';C:\Program Files\dotnet'
dotnet build backend/VarthexComanda.slnx --configuration Release
```

Expected: 0 errors, 0 warnings. Then run:

```powershell
dotnet run --project backend/src/VarthexComanda.Desktop --configuration Release
```

Expected: same as the base slice — a window titled "Varthex Comanda" opens; the log file gains the same three lines as before ("Iniciando Varthex Comanda", "Banco pronto em ...", and on close "Encerrando Varthex Comanda"). Close it, then confirm a second concurrent run still shows the "já está aberto" message and exits cleanly.

- [ ] **Step 3: Commit**

```powershell
git add backend/src/VarthexComanda.Desktop/App.xaml.cs
git commit -m "refactor: usa IDbContextFactory via DI no lugar da conexao sqlite manual"
```

---

### Task 7: Desktop — `ProdutosViewModel`, tela "Produtos" e resolução via DI

**Files:**
- Create: `backend/src/VarthexComanda.Desktop/Catalogo/ProdutosViewModel.cs`
- Modify: `backend/src/VarthexComanda.Desktop/App.xaml.cs`
- Modify: `backend/src/VarthexComanda.Desktop/MainWindow.xaml`
- Modify: `backend/src/VarthexComanda.Desktop/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: every Application-layer catalog use case (Tasks 2-3), `EfCategoriaRepository`/`EfProdutoRepository` (Tasks 4-5), the `ServiceProvider`/`ServiceCollection` from Task 6.
- Produces: a working "Produtos" screen; nothing later in this plan depends on this task.

- [ ] **Step 1: Implement `ProdutosViewModel`**

`backend/src/VarthexComanda.Desktop/Catalogo/ProdutosViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;

namespace VarthexComanda.Desktop.Catalogo;

public partial class ProdutosViewModel : ObservableObject
{
    private static readonly CultureInfo CulturaMoeda = CultureInfo.GetCultureInfo("pt-BR");

    private readonly ListarCategoriasAtivas _listarCategoriasAtivas;
    private readonly CadastrarCategoria _cadastrarCategoria;
    private readonly PesquisarProdutos _pesquisarProdutos;
    private readonly CadastrarProduto _cadastrarProduto;
    private readonly AlterarProduto _alterarProduto;
    private readonly DesativarProduto _desativarProduto;

    public ProdutosViewModel(
        ListarCategoriasAtivas listarCategoriasAtivas,
        CadastrarCategoria cadastrarCategoria,
        PesquisarProdutos pesquisarProdutos,
        CadastrarProduto cadastrarProduto,
        AlterarProduto alterarProduto,
        DesativarProduto desativarProduto)
    {
        _listarCategoriasAtivas = listarCategoriasAtivas;
        _cadastrarCategoria = cadastrarCategoria;
        _pesquisarProdutos = pesquisarProdutos;
        _cadastrarProduto = cadastrarProduto;
        _alterarProduto = alterarProduto;
        _desativarProduto = desativarProduto;

        Categorias = new ObservableCollection<Categoria>();
        Produtos = new ObservableCollection<Produto>();

        CarregarCategorias();
        Pesquisar();
    }

    public ObservableCollection<Categoria> Categorias { get; }
    public ObservableCollection<Produto> Produtos { get; }

    [ObservableProperty]
    private Categoria? categoriaFiltro;

    [ObservableProperty]
    private string textoBusca = string.Empty;

    [ObservableProperty]
    private Produto? produtoSelecionado;

    [ObservableProperty]
    private string nomeProduto = string.Empty;

    [ObservableProperty]
    private Categoria? categoriaProduto;

    [ObservableProperty]
    private string precoProdutoReais = string.Empty;

    [ObservableProperty]
    private bool produtoAtivo = true;

    [ObservableProperty]
    private string novaCategoriaNome = string.Empty;

    [ObservableProperty]
    private string mensagem = string.Empty;

    partial void OnCategoriaFiltroChanged(Categoria? value) => Pesquisar();

    partial void OnTextoBuscaChanged(string value) => Pesquisar();

    partial void OnProdutoSelecionadoChanged(Produto? value)
    {
        if (value is null)
        {
            return;
        }

        NomeProduto = value.Nome;
        CategoriaProduto = Categorias.FirstOrDefault(c => c.Id == value.CategoriaId);
        PrecoProdutoReais = (value.PrecoCentavos / 100m).ToString("0.00", CulturaMoeda);
        ProdutoAtivo = value.Ativo;
        Mensagem = string.Empty;
    }

    [RelayCommand]
    private void Pesquisar()
    {
        var resultado = _pesquisarProdutos.Executar(CategoriaFiltro?.Id, TextoBusca);
        Produtos.Clear();
        foreach (var produto in resultado)
        {
            Produtos.Add(produto);
        }
    }

    [RelayCommand]
    private void Novo()
    {
        ProdutoSelecionado = null;
        NomeProduto = string.Empty;
        CategoriaProduto = null;
        PrecoProdutoReais = string.Empty;
        ProdutoAtivo = true;
        Mensagem = string.Empty;
    }

    [RelayCommand]
    private void Salvar()
    {
        if (CategoriaProduto is null)
        {
            Mensagem = "Selecione uma categoria.";
            return;
        }
        if (!TentarConverterPreco(PrecoProdutoReais, out var precoCentavos))
        {
            Mensagem = "Informe um preço válido.";
            return;
        }

        var resultado = ProdutoSelecionado is null
            ? _cadastrarProduto.Executar(NomeProduto, CategoriaProduto.Id, precoCentavos)
            : _alterarProduto.Executar(ProdutoSelecionado.Id, NomeProduto, CategoriaProduto.Id, precoCentavos, ProdutoAtivo);

        if (!resultado.Sucesso)
        {
            Mensagem = string.Join(" ", resultado.Erros);
            return;
        }

        Novo();
        Pesquisar();
    }

    [RelayCommand]
    private void Desativar()
    {
        if (ProdutoSelecionado is null)
        {
            Mensagem = "Selecione um produto para desativar.";
            return;
        }

        var resultado = _desativarProduto.Executar(ProdutoSelecionado.Id);
        if (!resultado.Sucesso)
        {
            Mensagem = string.Join(" ", resultado.Erros);
            return;
        }

        Novo();
        Pesquisar();
    }

    [RelayCommand]
    private void AdicionarCategoria()
    {
        var resultado = _cadastrarCategoria.Executar(NovaCategoriaNome);
        if (!resultado.Sucesso)
        {
            Mensagem = string.Join(" ", resultado.Erros);
            return;
        }

        NovaCategoriaNome = string.Empty;
        Mensagem = string.Empty;
        CarregarCategorias();
    }

    private void CarregarCategorias()
    {
        var categoriaSelecionadaId = CategoriaProduto?.Id;
        Categorias.Clear();
        foreach (var categoria in _listarCategoriasAtivas.Executar())
        {
            Categorias.Add(categoria);
        }
        if (categoriaSelecionadaId is not null)
        {
            CategoriaProduto = Categorias.FirstOrDefault(c => c.Id == categoriaSelecionadaId);
        }
    }

    private static bool TentarConverterPreco(string texto, out long precoCentavos)
    {
        precoCentavos = 0;
        if (!decimal.TryParse(texto, NumberStyles.Number, CulturaMoeda, out var valor))
        {
            return false;
        }
        if (valor <= 0)
        {
            return false;
        }
        precoCentavos = (long)Math.Round(valor * 100m, MidpointRounding.AwayFromZero);
        return true;
    }
}
```

- [ ] **Step 2: Register catalog services and resolve `MainWindow` via DI**

In `backend/src/VarthexComanda.Desktop/App.xaml.cs`, add these `using` directives:

```csharp
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Desktop.Catalogo;
using VarthexComanda.Infrastructure.Persistence.Catalogo;
```

Then, right after the existing `services.AddDbContextFactory<VarthexComandaDbContext>(...)` line and before `_serviceProvider = services.BuildServiceProvider();`, add:

```csharp
services.AddTransient<ICategoriaRepository, EfCategoriaRepository>();
services.AddTransient<IProdutoRepository, EfProdutoRepository>();
services.AddTransient<CadastrarCategoria>();
services.AddTransient<AlterarCategoria>();
services.AddTransient<ListarCategoriasAtivas>();
services.AddTransient<CadastrarProduto>();
services.AddTransient<AlterarProduto>();
services.AddTransient<DesativarProduto>();
services.AddTransient<PesquisarProdutos>();
services.AddTransient<ProdutosViewModel>();
services.AddTransient<MainWindow>();
```

Finally, replace the last line of `OnStartup` — `new MainWindow().Show();` — with:

```csharp
_serviceProvider.GetRequiredService<MainWindow>().Show();
```

- [ ] **Step 3: Wire `MainWindow` to the ViewModel**

`backend/src/VarthexComanda.Desktop/MainWindow.xaml.cs` (replace entire contents):

```csharp
using System.Windows;
using VarthexComanda.Desktop.Catalogo;

namespace VarthexComanda.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(ProdutosViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
```

- [ ] **Step 4: Build the "Produtos" screen layout**

`backend/src/VarthexComanda.Desktop/MainWindow.xaml` (replace entire contents):

```xml
<Window x:Class="VarthexComanda.Desktop.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        mc:Ignorable="d"
        Title="Varthex Comanda" Height="600" Width="1000">
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
</Window>
```

- [ ] **Step 5: Build and manually verify the full flow**

```powershell
$env:Path += ';C:\Program Files\dotnet'
dotnet build backend/VarthexComanda.slnx --configuration Release
dotnet run --project backend/src/VarthexComanda.Desktop --configuration Release
```

Expected, in order: window opens titled "Varthex Comanda" with the Produtos screen; typing a name under "Nova categoria" and clicking "Adicionar categoria" makes it appear in both category combo boxes; with a category selected, filling name/price and clicking "Salvar" adds a row to the product list; selecting that row populates the form (including the price formatted as `0,00`); unchecking "Ativo" and clicking "Salvar" updates its "Ativo" column to `False` without removing the row; clicking "Desativar" on another product does the same in one click; typing part of a name (in a different case than stored) into "Buscar" filters the list; selecting a different category in the top filter narrows the list to that category. Leave one active category and one active product in the database when done — Task 8 doesn't need this, but it's a realistic state for whoever opens the app next.

- [ ] **Step 6: Commit**

```powershell
git add backend/src/VarthexComanda.Desktop
git commit -m "feat: adiciona tela de produtos (ViewModel, XAML e composicao via DI)"
```

---

### Task 8: Verificação final e changelog

**Files:**
- Modify: `docs/CHANGELOG.md`

**Interfaces:**
- Consumes: nothing new — verifies and documents Tasks 1-7.

- [ ] **Step 1: Full build and test pass in Release**

```powershell
$env:Path += ';C:\Program Files\dotnet'
dotnet build backend/VarthexComanda.slnx --configuration Release
dotnet test backend/VarthexComanda.slnx --configuration Release
```

Expected: 0 errors, 0 warnings. Domain.Tests: 4 (unchanged). Application.Tests: 21 (from Tasks 1-3). Infrastructure.Tests: 16 (10 from the base slice + 6 from Tasks 4-5).

- [ ] **Step 2: Add a CHANGELOG entry**

Add to the top of `docs/CHANGELOG.md`, above the existing `## 1.4 - 2026-09-16` entry:

```markdown
## 1.5 - 2026-09-17

- implementado o catálogo (Etapa 2): cadastro, alteração, desativação e busca de
  categorias e produtos (RF01-05);
- casos de uso de catálogo validam nome obrigatório, preço positivo em centavos e
  categoria ativa, sem exceções para erros esperados;
- repositórios de categoria e produto sobre `IDbContextFactory`, com DbContext
  de curta duração por operação;
- composição do Desktop passa a usar `Microsoft.Extensions.DependencyInjection`
  de verdade (container, `IDbContextFactory` registrado, ViewModels resolvidas
  pelo container) — fecha a pendência de DI/MVVM deixada em aberto na Etapa 0+1;
- `MainWindow` deixa de ser uma janela vazia e passa a exibir a tela "Produtos";
- ainda sem comandas, itens ou vendas (RF06+) — entra na próxima fatia.

```

- [ ] **Step 3: Commit**

```powershell
git add docs/CHANGELOG.md
git commit -m "docs: registra o catalogo no changelog"
```

## Self-Review Notes

- **Spec coverage:** every bullet in [specs/2026-09-17-catalogo-design.md](2026-09-17-catalogo-design.md) maps to a task — `Resultado<T>`/interfaces → Task 1; categoria use cases → Task 2; produto use cases → Task 3; `EfCategoriaRepository`/`EfProdutoRepository` → Tasks 4-5; DI/`IDbContextFactory` composition → Task 6; `ProdutosViewModel` + screen + DI wiring → Task 7; build/test/changelog → Task 8. The spec's "reativar via `AlterarProduto` com `ativo = true`" is implemented by `AlterarProduto`'s `ativo` parameter and exercised by the UI's checkbox, not a separate use case, matching the spec's own text.
- **Type consistency:** `ICategoriaRepository`/`IProdutoRepository` method signatures are identical everywhere they're declared (Task 1), implemented (Tasks 4-5), and consumed (Tasks 2-3, 7). `AlterarProduto.Executar`'s 5-parameter signature (including `ativo`) is consistent between Task 3's implementation, its tests, and Task 7's `ProdutosViewModel` call site.
- **Out of scope confirmed:** no comanda/item/venda code touched; no automated WPF/ViewModel tests, matching the spec's explicit choice and the base slice's precedent.
