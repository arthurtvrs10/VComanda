# Foto do Produto Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Permitir cadastrar, trocar e remover uma foto por produto e
exibi-la nos cards do menu do Atendimento e num preview na tela de
Produtos.

**Architecture:** `Produto.FotoArquivo` guarda só o nome do arquivo; a
imagem é copiada para `%LOCALAPPDATA%\VarthexComanda\fotos` por um
`IFotoStorage` (Application) implementado em Infrastructure; dois casos de
uso (`DefinirFotoProduto`/`RemoverFotoProduto`) orquestram banco + arquivo;
o Desktop ganha comandos no `ProdutosViewModel`, um conversor XAML que
carrega a imagem sem travar o arquivo, e os XAMLs de Produtos e do menu.

**Tech Stack:** C#/.NET 10, WPF (CommunityToolkit.Mvvm), EF Core/SQLite,
Serilog, xUnit.

**Spec:** [specs/2026-09-19-foto-produto-design.md](../specs/2026-09-19-foto-produto-design.md)

## Global Constraints

- `Produto.FotoArquivo` é `string?` **sem** `required` (senão todo
  `new Produto { ... }` existente quebra). Guarda só o nome
  (`<guid>.<ext>`), **nunca** caminho absoluto.
- Formatos: `.jpg`, `.jpeg`, `.png`, `.bmp` (comparação sem diferenciar
  maiúsculas; nome guardado em minúsculas). Limite: 10 MB.
- Mensagens de erro exatas: `"Arquivo de imagem não encontrado."`,
  `"Formato não suportado. Use JPG, PNG ou BMP."`,
  `"A imagem excede o tamanho máximo de {N} MB."`,
  `"Produto não encontrado."`.
- O arquivo original escolhido pelo operador **nunca** é modificado nem
  apagado — o app guarda uma cópia.
- `ArquivoFotoStorage.Excluir` usa só `Path.GetFileName(nome)` (sem
  travessia de diretório) e nunca lança por IO: registra `Warning` no
  Serilog.
- ViewModels sem `System.Windows`/`Microsoft.Win32`: `DefinirFoto` recebe
  uma string de caminho; o `OpenFileDialog` fica no code-behind.
- O conversor de imagem usa `BitmapCacheOption.OnLoad` + `Freeze()` +
  `DecodePixelWidth = 320` (o `OnLoad` é obrigatório: sem ele o arquivo
  fica travado e trocar/remover a foto falha no Windows). Imagem ausente
  ou corrompida → `null` (o placeholder aparece), sem exceção.
- Os botões de foto valem só para produto já salvo e selecionado.
- Backup **não** inclui fotos nesta fatia (fora de escopo, documentado).
- Prefixar todo comando `dotnet` com o PATH do SDK: `export
  PATH="$PATH:/c/Program Files/dotnet" && dotnet ...` (Git Bash).

---

## Task 1: `Produto.FotoArquivo`, migração e `AppPaths.FotosDirectory`

**Files:**
- Modify: `backend/src/VarthexComanda.Domain/Produto.cs`
- Modify: `backend/src/VarthexComanda.Infrastructure/Persistence/Configurations/ProdutoConfiguration.cs`
- Create (gerados): migração `AddProdutoFoto` em `backend/src/VarthexComanda.Infrastructure/Persistence/Migrations/` (+ atualização de `VarthexComandaDbContextModelSnapshot.cs`)
- Modify: `backend/src/VarthexComanda.Infrastructure/Storage/AppPaths.cs`
- Test: `backend/tests/VarthexComanda.Infrastructure.Tests/Catalogo/EfProdutoRepositoryTests.cs`
- Test: `backend/tests/VarthexComanda.Infrastructure.Tests/AppPathsTests.cs`

**Interfaces:**
- Consumes: nada de tasks anteriores.
- Produces: `Produto.FotoArquivo` (`string?`), coluna `produto.foto_arquivo`,
  `AppPaths.FotosDirectory` — consumidos pelas Tasks 2-5.

- [ ] **Step 1: Escrever os testes (vão falhar — a propriedade não existe)**

Em `EfProdutoRepositoryTests.cs`, ler o arquivo e seguir o estilo dos
testes existentes (campos `_fabrica` e `_categoriaId`). Adicionar:

```csharp
    [Fact]
    public void Salvar_ProdutoComFoto_PersisteNomeDoArquivo()
    {
        var repositorio = new EfProdutoRepository(_fabrica);

        var salvo = repositorio.Salvar(new Produto
        {
            Id = 0,
            CategoriaId = _categoriaId,
            Nome = "X-Burguer",
            PrecoCentavos = 1800,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
            AtualizadoEm = DateTime.UtcNow,
            FotoArquivo = "abc123.jpg"
        });

        var lido = repositorio.BuscarPorId(salvo.Id);
        Assert.Equal("abc123.jpg", lido!.FotoArquivo);
    }

    [Fact]
    public void Salvar_ProdutoSemFoto_FotoArquivoFicaNula()
    {
        var repositorio = new EfProdutoRepository(_fabrica);

        var salvo = repositorio.Salvar(new Produto
        {
            Id = 0,
            CategoriaId = _categoriaId,
            Nome = "Coca-Cola",
            PrecoCentavos = 500,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
            AtualizadoEm = DateTime.UtcNow
        });

        Assert.Null(repositorio.BuscarPorId(salvo.Id)!.FotoArquivo);
    }

    [Fact]
    public void Salvar_LimparFotoDeProdutoExistente_PersisteNulo()
    {
        var repositorio = new EfProdutoRepository(_fabrica);
        var salvo = repositorio.Salvar(new Produto
        {
            Id = 0,
            CategoriaId = _categoriaId,
            Nome = "Suco",
            PrecoCentavos = 800,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
            AtualizadoEm = DateTime.UtcNow,
            FotoArquivo = "velha.png"
        });

        var carregado = repositorio.BuscarPorId(salvo.Id)!;
        carregado.FotoArquivo = null;
        repositorio.Salvar(carregado);

        Assert.Null(repositorio.BuscarPorId(salvo.Id)!.FotoArquivo);
    }
```

Em `AppPathsTests.cs`, ler o arquivo e seguir o estilo dos testes
existentes (raiz temporária via `new AppPaths(raiz)`). Adicionar um teste
equivalente a:

```csharp
    [Fact]
    public void FotosDirectory_FicaSobARaiz_ECriadoPorEnsureCreated()
    {
        var raiz = Path.Combine(Path.GetTempPath(), $"varthex-apppaths-{Guid.NewGuid()}");
        try
        {
            var paths = new AppPaths(raiz);

            Assert.Equal(Path.Combine(raiz, "fotos"), paths.FotosDirectory);

            paths.EnsureCreated();

            Assert.True(Directory.Exists(paths.FotosDirectory));
        }
        finally
        {
            if (Directory.Exists(raiz)) Directory.Delete(raiz, true);
        }
    }
```

- [ ] **Step 2: Confirmar que falha (erro de compilação)**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet build backend/VarthexComanda.slnx
```

Esperado: falha — `Produto.FotoArquivo` e `AppPaths.FotosDirectory` não existem.

- [ ] **Step 3: Adicionar a propriedade e o mapeamento**

Em `Produto.cs`, adicionar (sem `required`), junto das demais propriedades:

```csharp
    public string? FotoArquivo { get; set; }
```

Em `ProdutoConfiguration.cs`, dentro de `Configure`, junto dos outros
`builder.Property(...)`:

```csharp
        builder.Property(p => p.FotoArquivo).HasColumnName("foto_arquivo");
```

- [ ] **Step 4: Adicionar `FotosDirectory` em `AppPaths`**

Em `AppPaths.cs`, seguir o estilo das propriedades existentes
(`LogsDirectory`, `BackupsDirectory`) e adicionar:

```csharp
    public string FotosDirectory => Path.Combine(Root, "fotos");
```

e, em `EnsureCreated()`, criar também esse diretório
(`Directory.CreateDirectory(FotosDirectory);`), junto das demais chamadas.

- [ ] **Step 5: Gerar a migração**

Primeiro conferir que a ferramenta existe: `dotnet ef --version`. Se
não existir, **pare e reporte NEEDS_CONTEXT** — não instale ferramentas
globais por conta própria.

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet ef migrations add AddProdutoFoto --project backend/src/VarthexComanda.Infrastructure --startup-project backend/src/VarthexComanda.Infrastructure --output-dir Persistence/Migrations
```

Abrir o arquivo `..._AddProdutoFoto.cs` gerado e **confirmar** que o
`Up()` contém apenas um `AddColumn<string>(name: "foto_arquivo", table:
"produto", type: "TEXT", nullable: true)` (e o `Down()` o `DropColumn`
correspondente). Se aparecer qualquer outra operação (drift de
snapshot), pare e reporte — não commite uma migração com efeitos extras.

- [ ] **Step 6: Rodar os testes e commitar**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release
```

Esperado: todos PASS (baseline 187 + 4 novos = 191).

```bash
git add backend/src/VarthexComanda.Domain/Produto.cs backend/src/VarthexComanda.Infrastructure backend/tests/VarthexComanda.Infrastructure.Tests
git commit -m "feat: adiciona campo de foto ao produto e pasta de fotos"
```

---

## Task 2: `IFotoStorage`, `ArquivoFotoStorage` e `FakeFotoStorage`

**Files:**
- Create: `backend/src/VarthexComanda.Application/Catalogo/IFotoStorage.cs`
- Create: `backend/src/VarthexComanda.Application/Catalogo/FotoInvalidaException.cs`
- Create: `backend/src/VarthexComanda.Infrastructure/Storage/ArquivoFotoStorage.cs`
- Create: `backend/tests/VarthexComanda.Application.Tests/Catalogo/FakeFotoStorage.cs`
- Test: `backend/tests/VarthexComanda.Infrastructure.Tests/Storage/ArquivoFotoStorageTests.cs`

**Interfaces:**
- Consumes: `AppPaths.FotosDirectory` (Task 1), `Serilog.ILogger`.
- Produces: `IFotoStorage { string Importar(string caminhoOrigem); void Excluir(string nomeArquivo); }`,
  `FotoInvalidaException(string message)`, `ArquivoFotoStorage(AppPaths, ILogger, long tamanhoMaximoBytes = ...)`,
  `FakeFotoStorage { Importados, Excluidos, MensagemRejeicao }` — consumidos pelas Tasks 3-4.

- [ ] **Step 1: Criar a interface e a exceção**

`backend/src/VarthexComanda.Application/Catalogo/IFotoStorage.cs`:

```csharp
namespace VarthexComanda.Application.Catalogo;

public interface IFotoStorage
{
    /// <summary>Copia a imagem para o armazenamento do app e devolve o nome do arquivo guardado.</summary>
    /// <exception cref="FotoInvalidaException">Arquivo inexistente, formato não suportado ou grande demais.</exception>
    string Importar(string caminhoOrigem);

    /// <summary>Apaga a foto guardada. Nunca lança por falha de IO.</summary>
    void Excluir(string nomeArquivo);
}
```

`backend/src/VarthexComanda.Application/Catalogo/FotoInvalidaException.cs`:

```csharp
namespace VarthexComanda.Application.Catalogo;

public class FotoInvalidaException : Exception
{
    public FotoInvalidaException(string message) : base(message)
    {
    }
}
```

- [ ] **Step 2: Criar o fake para testes**

`backend/tests/VarthexComanda.Application.Tests/Catalogo/FakeFotoStorage.cs`:

```csharp
using VarthexComanda.Application.Catalogo;

namespace VarthexComanda.Application.Tests.Catalogo;

public class FakeFotoStorage : IFotoStorage
{
    private int _contador;

    public List<string> Importados { get; } = new();
    public List<string> Excluidos { get; } = new();
    public string? MensagemRejeicao { get; set; }

    public string Importar(string caminhoOrigem)
    {
        if (MensagemRejeicao is not null)
        {
            throw new FotoInvalidaException(MensagemRejeicao);
        }

        var nome = $"foto{++_contador}.jpg";
        Importados.Add(nome);
        return nome;
    }

    public void Excluir(string nomeArquivo) => Excluidos.Add(nomeArquivo);
}
```

- [ ] **Step 3: Escrever os testes do armazenamento real (vão falhar — a classe não existe)**

`backend/tests/VarthexComanda.Infrastructure.Tests/Storage/ArquivoFotoStorageTests.cs`:

```csharp
using Serilog;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Infrastructure.Storage;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests.Storage;

public class ArquivoFotoStorageTests : IDisposable
{
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), $"varthex-fotos-tests-{Guid.NewGuid()}");
    private readonly AppPaths _paths;
    private readonly ILogger _logger = new LoggerConfiguration().CreateLogger();

    public ArquivoFotoStorageTests()
    {
        _paths = new AppPaths(_raiz);
        _paths.EnsureCreated();
    }

    public void Dispose()
    {
        if (Directory.Exists(_raiz))
        {
            Directory.Delete(_raiz, true);
        }
    }

    private string CriarArquivoOrigem(string nome, int bytes = 16)
    {
        var caminho = Path.Combine(_raiz, nome);
        File.WriteAllBytes(caminho, new byte[bytes]);
        return caminho;
    }

    [Fact]
    public void Importar_ArquivoValido_CopiaParaPastaDeFotosERetornaNome()
    {
        var storage = new ArquivoFotoStorage(_paths, _logger);
        var origem = CriarArquivoOrigem("foto.png");

        var nome = storage.Importar(origem);

        Assert.EndsWith(".png", nome);
        Assert.True(File.Exists(Path.Combine(_paths.FotosDirectory, nome)));
        Assert.True(File.Exists(origem));
    }

    [Fact]
    public void Importar_ExtensaoMaiuscula_NomeGuardadoEmMinusculas()
    {
        var storage = new ArquivoFotoStorage(_paths, _logger);
        var origem = CriarArquivoOrigem("FOTO.JPG");

        var nome = storage.Importar(origem);

        Assert.EndsWith(".jpg", nome);
    }

    [Fact]
    public void Importar_MesmoArquivoDuasVezes_GeraNomesDiferentes()
    {
        var storage = new ArquivoFotoStorage(_paths, _logger);
        var origem = CriarArquivoOrigem("foto.png");

        var primeiro = storage.Importar(origem);
        var segundo = storage.Importar(origem);

        Assert.NotEqual(primeiro, segundo);
    }

    [Fact]
    public void Importar_ExtensaoNaoSuportada_LancaFotoInvalida()
    {
        var storage = new ArquivoFotoStorage(_paths, _logger);
        var origem = CriarArquivoOrigem("documento.pdf");

        var excecao = Assert.Throws<FotoInvalidaException>(() => storage.Importar(origem));

        Assert.Equal("Formato não suportado. Use JPG, PNG ou BMP.", excecao.Message);
    }

    [Fact]
    public void Importar_ArquivoInexistente_LancaFotoInvalida()
    {
        var storage = new ArquivoFotoStorage(_paths, _logger);

        var excecao = Assert.Throws<FotoInvalidaException>(() => storage.Importar(Path.Combine(_raiz, "nao-existe.png")));

        Assert.Equal("Arquivo de imagem não encontrado.", excecao.Message);
    }

    [Fact]
    public void Importar_ArquivoMaiorQueOLimite_LancaFotoInvalida()
    {
        var storage = new ArquivoFotoStorage(_paths, _logger, tamanhoMaximoBytes: 8);
        var origem = CriarArquivoOrigem("grande.png", bytes: 16);

        Assert.Throws<FotoInvalidaException>(() => storage.Importar(origem));
    }

    [Fact]
    public void Excluir_ArquivoExistente_Remove()
    {
        var storage = new ArquivoFotoStorage(_paths, _logger);
        var nome = storage.Importar(CriarArquivoOrigem("foto.png"));

        storage.Excluir(nome);

        Assert.False(File.Exists(Path.Combine(_paths.FotosDirectory, nome)));
    }

    [Fact]
    public void Excluir_ArquivoInexistente_NaoLanca()
    {
        var storage = new ArquivoFotoStorage(_paths, _logger);

        var excecao = Record.Exception(() => storage.Excluir("nao-existe.png"));

        Assert.Null(excecao);
    }

    [Fact]
    public void Excluir_NomeComTravessiaDeDiretorio_UsaSoONomeDoArquivo()
    {
        var storage = new ArquivoFotoStorage(_paths, _logger);
        var foraDaPasta = CriarArquivoOrigem("fora.jpg");

        storage.Excluir("..\\fora.jpg");

        Assert.True(File.Exists(foraDaPasta));
    }
}
```

- [ ] **Step 4: Confirmar que falha (erro de compilação)**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet build backend/VarthexComanda.slnx
```

Esperado: falha — `ArquivoFotoStorage` não existe.

- [ ] **Step 5: Implementar `ArquivoFotoStorage`**

`backend/src/VarthexComanda.Infrastructure/Storage/ArquivoFotoStorage.cs`:

```csharp
using System.Globalization;
using Serilog;
using VarthexComanda.Application.Catalogo;

namespace VarthexComanda.Infrastructure.Storage;

public class ArquivoFotoStorage : IFotoStorage
{
    public const long TamanhoMaximoPadraoBytes = 10L * 1024 * 1024;

    private static readonly HashSet<string> ExtensoesPermitidas =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".bmp" };

    private readonly AppPaths _paths;
    private readonly ILogger _logger;
    private readonly long _tamanhoMaximoBytes;

    public ArquivoFotoStorage(AppPaths paths, ILogger logger, long tamanhoMaximoBytes = TamanhoMaximoPadraoBytes)
    {
        _paths = paths;
        _logger = logger;
        _tamanhoMaximoBytes = tamanhoMaximoBytes;
    }

    public string Importar(string caminhoOrigem)
    {
        if (string.IsNullOrWhiteSpace(caminhoOrigem) || !File.Exists(caminhoOrigem))
        {
            throw new FotoInvalidaException("Arquivo de imagem não encontrado.");
        }

        var extensao = Path.GetExtension(caminhoOrigem);
        if (!ExtensoesPermitidas.Contains(extensao))
        {
            throw new FotoInvalidaException("Formato não suportado. Use JPG, PNG ou BMP.");
        }

        if (new FileInfo(caminhoOrigem).Length > _tamanhoMaximoBytes)
        {
            var megabytes = (_tamanhoMaximoBytes / 1024d / 1024d).ToString("0.##", CultureInfo.GetCultureInfo("pt-BR"));
            throw new FotoInvalidaException($"A imagem excede o tamanho máximo de {megabytes} MB.");
        }

        var nome = $"{Guid.NewGuid():N}{extensao.ToLowerInvariant()}";
        Directory.CreateDirectory(_paths.FotosDirectory);
        File.Copy(caminhoOrigem, Path.Combine(_paths.FotosDirectory, nome));
        return nome;
    }

    public void Excluir(string nomeArquivo)
    {
        if (string.IsNullOrWhiteSpace(nomeArquivo))
        {
            return;
        }

        var caminho = Path.Combine(_paths.FotosDirectory, Path.GetFileName(nomeArquivo));
        try
        {
            if (File.Exists(caminho))
            {
                File.Delete(caminho);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Warning(ex, "Não foi possível excluir a foto {Arquivo}", nomeArquivo);
        }
    }
}
```

- [ ] **Step 6: Rodar os testes e commitar**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release
```

Esperado: todos PASS (191 + 9 novos = 200).

```bash
git add backend/src/VarthexComanda.Application/Catalogo/IFotoStorage.cs backend/src/VarthexComanda.Application/Catalogo/FotoInvalidaException.cs backend/src/VarthexComanda.Infrastructure/Storage/ArquivoFotoStorage.cs backend/tests/VarthexComanda.Application.Tests/Catalogo/FakeFotoStorage.cs backend/tests/VarthexComanda.Infrastructure.Tests/Storage/ArquivoFotoStorageTests.cs
git commit -m "feat: adiciona armazenamento de fotos de produto"
```

---

## Task 3: Casos de uso `DefinirFotoProduto` e `RemoverFotoProduto`

**Files:**
- Create: `backend/src/VarthexComanda.Application/Catalogo/DefinirFotoProduto.cs`
- Create: `backend/src/VarthexComanda.Application/Catalogo/RemoverFotoProduto.cs`
- Test: `backend/tests/VarthexComanda.Application.Tests/Catalogo/DefinirFotoProdutoTests.cs`
- Test: `backend/tests/VarthexComanda.Application.Tests/Catalogo/RemoverFotoProdutoTests.cs`

**Interfaces:**
- Consumes: `IProdutoRepository` (`BuscarPorId`, `Salvar`), `IFotoStorage`,
  `FotoInvalidaException`, `IClock`, `Resultado<T>`, `Produto.FotoArquivo`,
  `FakeFotoStorage`, `FakeProdutoRepository`, `FakeCategoriaRepository`,
  `CadastrarCategoria`, `CadastrarProduto`, `FakeClock`.
- Produces: `DefinirFotoProduto(IProdutoRepository, IFotoStorage, IClock).Executar(int produtoId, string caminhoOrigem) → Resultado<Produto>`,
  `RemoverFotoProduto(IProdutoRepository, IFotoStorage, IClock).Executar(int produtoId) → Resultado<Produto>` — consumidos pela Task 4.

- [ ] **Step 1: Escrever os testes de `DefinirFotoProduto` (vão falhar — a classe não existe)**

`backend/tests/VarthexComanda.Application.Tests/Catalogo/DefinirFotoProdutoTests.cs`:

```csharp
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;
using Xunit;

namespace VarthexComanda.Application.Tests.Catalogo;

public class DefinirFotoProdutoTests
{
    private static (FakeProdutoRepository Produtos, Produto Produto) CriarProduto()
    {
        var categorias = new FakeCategoriaRepository();
        var produtos = new FakeProdutoRepository();
        var relogio = new FakeClock();
        var categoria = new CadastrarCategoria(categorias, relogio).Executar("Bebidas").Valor!;
        var produto = new CadastrarProduto(produtos, categorias, relogio).Executar("Refrigerante", categoria.Id, 500).Valor!;
        return (produtos, produto);
    }

    [Fact]
    public void Executar_ProdutoInexistente_FalhaSemImportarNada()
    {
        var (produtos, _) = CriarProduto();
        var fotos = new FakeFotoStorage();
        var caso = new DefinirFotoProduto(produtos, fotos, new FakeClock());

        var resultado = caso.Executar(999, "C:\\foto.jpg");

        Assert.False(resultado.Sucesso);
        Assert.Contains("Produto não encontrado.", resultado.Erros);
        Assert.Empty(fotos.Importados);
    }

    [Fact]
    public void Executar_FotoValida_GravaONomeDoArquivoNoProduto()
    {
        var (produtos, produto) = CriarProduto();
        var fotos = new FakeFotoStorage();
        var caso = new DefinirFotoProduto(produtos, fotos, new FakeClock());

        var resultado = caso.Executar(produto.Id, "C:\\foto.jpg");

        Assert.True(resultado.Sucesso);
        Assert.Equal("foto1.jpg", resultado.Valor!.FotoArquivo);
        Assert.Equal("foto1.jpg", produtos.BuscarPorId(produto.Id)!.FotoArquivo);
        Assert.Empty(fotos.Excluidos);
    }

    [Fact]
    public void Executar_ProdutoJaTinhaFoto_ExcluiOArquivoAntigo()
    {
        var (produtos, produto) = CriarProduto();
        var fotos = new FakeFotoStorage();
        var caso = new DefinirFotoProduto(produtos, fotos, new FakeClock());
        caso.Executar(produto.Id, "C:\\primeira.jpg");

        var resultado = caso.Executar(produto.Id, "C:\\segunda.jpg");

        Assert.True(resultado.Sucesso);
        Assert.Equal("foto2.jpg", produtos.BuscarPorId(produto.Id)!.FotoArquivo);
        Assert.Equal(new[] { "foto1.jpg" }, fotos.Excluidos);
    }

    [Fact]
    public void Executar_StorageRejeitaAFoto_FalhaComMensagemESemAlterarOProduto()
    {
        var (produtos, produto) = CriarProduto();
        var fotos = new FakeFotoStorage { MensagemRejeicao = "Formato não suportado. Use JPG, PNG ou BMP." };
        var caso = new DefinirFotoProduto(produtos, fotos, new FakeClock());

        var resultado = caso.Executar(produto.Id, "C:\\documento.pdf");

        Assert.False(resultado.Sucesso);
        Assert.Contains("Formato não suportado. Use JPG, PNG ou BMP.", resultado.Erros);
        Assert.Null(produtos.BuscarPorId(produto.Id)!.FotoArquivo);
    }

    [Fact]
    public void Executar_FalhaAoSalvar_ExcluiOArquivoNovoEPropagaAExcecao()
    {
        var (produtos, produto) = CriarProduto();
        var fotos = new FakeFotoStorage();
        var caso = new DefinirFotoProduto(new RepositorioQueFalhaAoSalvar(produtos), fotos, new FakeClock());

        Assert.Throws<InvalidOperationException>(() => caso.Executar(produto.Id, "C:\\foto.jpg"));

        Assert.Equal(new[] { "foto1.jpg" }, fotos.Excluidos);
    }

    private sealed class RepositorioQueFalhaAoSalvar : IProdutoRepository
    {
        private readonly FakeProdutoRepository _interno;

        public RepositorioQueFalhaAoSalvar(FakeProdutoRepository interno) => _interno = interno;

        public Produto? BuscarPorId(int id) => _interno.BuscarPorId(id);

        public IReadOnlyList<Produto> Pesquisar(int? categoriaId, string? texto) => _interno.Pesquisar(categoriaId, texto);

        public Produto Salvar(Produto produto) => throw new InvalidOperationException("falha simulada");
    }
}
```

- [ ] **Step 2: Escrever os testes de `RemoverFotoProduto` (vão falhar)**

`backend/tests/VarthexComanda.Application.Tests/Catalogo/RemoverFotoProdutoTests.cs`:

```csharp
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;
using Xunit;

namespace VarthexComanda.Application.Tests.Catalogo;

public class RemoverFotoProdutoTests
{
    private static (FakeProdutoRepository Produtos, Produto Produto) CriarProduto()
    {
        var categorias = new FakeCategoriaRepository();
        var produtos = new FakeProdutoRepository();
        var relogio = new FakeClock();
        var categoria = new CadastrarCategoria(categorias, relogio).Executar("Bebidas").Valor!;
        var produto = new CadastrarProduto(produtos, categorias, relogio).Executar("Refrigerante", categoria.Id, 500).Valor!;
        return (produtos, produto);
    }

    [Fact]
    public void Executar_ProdutoInexistente_Falha()
    {
        var (produtos, _) = CriarProduto();
        var caso = new RemoverFotoProduto(produtos, new FakeFotoStorage(), new FakeClock());

        var resultado = caso.Executar(999);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Produto não encontrado.", resultado.Erros);
    }

    [Fact]
    public void Executar_ProdutoSemFoto_SucessoSemExcluirNada()
    {
        var (produtos, produto) = CriarProduto();
        var fotos = new FakeFotoStorage();
        var caso = new RemoverFotoProduto(produtos, fotos, new FakeClock());

        var resultado = caso.Executar(produto.Id);

        Assert.True(resultado.Sucesso);
        Assert.Empty(fotos.Excluidos);
    }

    [Fact]
    public void Executar_ProdutoComFoto_LimpaOCampoEExcluiOArquivo()
    {
        var (produtos, produto) = CriarProduto();
        var fotos = new FakeFotoStorage();
        new DefinirFotoProduto(produtos, fotos, new FakeClock()).Executar(produto.Id, "C:\\foto.jpg");
        var caso = new RemoverFotoProduto(produtos, fotos, new FakeClock());

        var resultado = caso.Executar(produto.Id);

        Assert.True(resultado.Sucesso);
        Assert.Null(produtos.BuscarPorId(produto.Id)!.FotoArquivo);
        Assert.Equal(new[] { "foto1.jpg" }, fotos.Excluidos);
    }
}
```

- [ ] **Step 3: Confirmar que falha (erro de compilação)**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet build backend/VarthexComanda.slnx
```

Esperado: falha — `DefinirFotoProduto`/`RemoverFotoProduto` não existem.

- [ ] **Step 4: Implementar os casos de uso**

`backend/src/VarthexComanda.Application/Catalogo/DefinirFotoProduto.cs`:

```csharp
using VarthexComanda.Application.Abstractions;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Catalogo;

public class DefinirFotoProduto
{
    private readonly IProdutoRepository _produtos;
    private readonly IFotoStorage _fotos;
    private readonly IClock _relogio;

    public DefinirFotoProduto(IProdutoRepository produtos, IFotoStorage fotos, IClock relogio)
    {
        _produtos = produtos;
        _fotos = fotos;
        _relogio = relogio;
    }

    public Resultado<Produto> Executar(int produtoId, string caminhoOrigem)
    {
        var produto = _produtos.BuscarPorId(produtoId);
        if (produto is null)
        {
            return Resultado<Produto>.Falha("Produto não encontrado.");
        }

        string novoArquivo;
        try
        {
            novoArquivo = _fotos.Importar(caminhoOrigem);
        }
        catch (FotoInvalidaException ex)
        {
            return Resultado<Produto>.Falha(ex.Message);
        }

        var arquivoAnterior = produto.FotoArquivo;
        produto.FotoArquivo = novoArquivo;
        produto.AtualizadoEm = _relogio.UtcNow;

        try
        {
            _produtos.Salvar(produto);
        }
        catch
        {
            _fotos.Excluir(novoArquivo);
            throw;
        }

        if (!string.IsNullOrEmpty(arquivoAnterior))
        {
            _fotos.Excluir(arquivoAnterior);
        }

        return Resultado<Produto>.Ok(produto);
    }
}
```

`backend/src/VarthexComanda.Application/Catalogo/RemoverFotoProduto.cs`:

```csharp
using VarthexComanda.Application.Abstractions;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Catalogo;

public class RemoverFotoProduto
{
    private readonly IProdutoRepository _produtos;
    private readonly IFotoStorage _fotos;
    private readonly IClock _relogio;

    public RemoverFotoProduto(IProdutoRepository produtos, IFotoStorage fotos, IClock relogio)
    {
        _produtos = produtos;
        _fotos = fotos;
        _relogio = relogio;
    }

    public Resultado<Produto> Executar(int produtoId)
    {
        var produto = _produtos.BuscarPorId(produtoId);
        if (produto is null)
        {
            return Resultado<Produto>.Falha("Produto não encontrado.");
        }

        var arquivo = produto.FotoArquivo;
        if (string.IsNullOrEmpty(arquivo))
        {
            return Resultado<Produto>.Ok(produto);
        }

        produto.FotoArquivo = null;
        produto.AtualizadoEm = _relogio.UtcNow;
        _produtos.Salvar(produto);
        _fotos.Excluir(arquivo);

        return Resultado<Produto>.Ok(produto);
    }
}
```

Nota: confira no arquivo `CadastrarProduto.cs` os `using`s reais usados para
`Resultado`, `IClock` e `Produto` e mantenha os mesmos — este bloco assume
`VarthexComanda.Application.Abstractions` (IClock) e `VarthexComanda.Domain`
(Produto), com `Resultado<T>` no próprio namespace `Catalogo`.

- [ ] **Step 5: Rodar os testes e commitar**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release
```

Esperado: todos PASS (200 + 8 novos = 208).

```bash
git add backend/src/VarthexComanda.Application/Catalogo/DefinirFotoProduto.cs backend/src/VarthexComanda.Application/Catalogo/RemoverFotoProduto.cs backend/tests/VarthexComanda.Application.Tests/Catalogo/DefinirFotoProdutoTests.cs backend/tests/VarthexComanda.Application.Tests/Catalogo/RemoverFotoProdutoTests.cs
git commit -m "feat: adiciona casos de uso de foto do produto"
```

---

## Task 4: `ProdutosViewModel` (foto) e registro de DI

**Files:**
- Modify: `backend/src/VarthexComanda.Desktop/Catalogo/ProdutosViewModel.cs`
- Modify: `backend/src/VarthexComanda.Desktop/App.xaml.cs`
- Modify: `backend/tests/VarthexComanda.Desktop.Tests/Catalogo/ProdutosViewModelTests.cs`

**Interfaces:**
- Consumes: `DefinirFotoProduto`, `RemoverFotoProduto` (Task 3), `IFotoStorage`/`ArquivoFotoStorage` (Task 2), `AppPaths` e `Serilog.ILogger` (já registrados como singleton em `App.xaml.cs`), `FakeFotoStorage`.
- Produces: `ProdutosViewModel.FotoArquivoAtual` (`string?`),
  `ProdutosViewModel.TemProdutoSelecionado` (`bool`),
  `ProdutosViewModel.DefinirFoto(string caminhoOrigem)` (público),
  `ProdutosViewModel.RemoverFotoCommand` — consumidos pela Task 5.

- [ ] **Step 1: Atualizar o helper e escrever os testes (vão falhar)**

Em `ProdutosViewModelTests.cs`, ler o arquivo. Atualizar o helper
`CriarViewModel` para receber (opcional) um `FakeFotoStorage` e passar os
dois novos casos de uso ao construtor (dois argumentos novos no fim):

```csharp
    private static ProdutosViewModel CriarViewModel(FakeCategoriaRepository categorias, FakeProdutoRepository produtos, FakeFotoStorage? fotos = null)
    {
        var relogio = new FakeClock();
        fotos ??= new FakeFotoStorage();
        return new ProdutosViewModel(
            new ListarCategoriasAtivas(categorias),
            new CadastrarCategoria(categorias, relogio),
            new PesquisarProdutos(produtos),
            new CadastrarProduto(produtos, categorias, relogio),
            new AlterarProduto(produtos, categorias, relogio),
            new DesativarProduto(produtos, relogio),
            new DefinirFotoProduto(produtos, fotos, relogio),
            new RemoverFotoProduto(produtos, fotos, relogio));
    }
```

(Adapte ao formato real do helper existente — o essencial é o construtor
receber `DefinirFotoProduto` e `RemoverFotoProduto` como últimos dois
argumentos, mantendo a ordem atual dos seis primeiros.) Adicionar
`using VarthexComanda.Domain;` se ainda não houver, e os testes:

```csharp
    private static (ProdutosViewModel ViewModel, FakeFotoStorage Fotos, Produto Produto) CriarComProdutoSelecionado()
    {
        var categorias = new FakeCategoriaRepository();
        var produtos = new FakeProdutoRepository();
        var relogio = new FakeClock();
        var categoria = new CadastrarCategoria(categorias, relogio).Executar("Bebidas").Valor!;
        var criado = new CadastrarProduto(produtos, categorias, relogio).Executar("Refrigerante", categoria.Id, 500).Valor!;
        var fotos = new FakeFotoStorage();
        var viewModel = CriarViewModel(categorias, produtos, fotos);
        var selecionado = viewModel.Produtos.Single(p => p.Id == criado.Id);
        viewModel.ProdutoSelecionado = selecionado;
        return (viewModel, fotos, selecionado);
    }

    [Fact]
    public void DefinirFoto_SemProdutoSelecionado_MostraMensagem()
    {
        var viewModel = CriarViewModel(new FakeCategoriaRepository(), new FakeProdutoRepository());

        viewModel.DefinirFoto("C:\\foto.jpg");

        Assert.Equal("Selecione um produto para definir a foto.", viewModel.Mensagem);
        Assert.Null(viewModel.FotoArquivoAtual);
    }

    [Fact]
    public void DefinirFoto_ProdutoSelecionado_AtualizaAFotoAtual()
    {
        var (viewModel, _, produto) = CriarComProdutoSelecionado();

        viewModel.DefinirFoto("C:\\foto.jpg");

        Assert.Equal("foto1.jpg", viewModel.FotoArquivoAtual);
        Assert.Equal("foto1.jpg", produto.FotoArquivo);
        Assert.Equal(string.Empty, viewModel.Mensagem);
    }

    [Fact]
    public void DefinirFoto_StorageRejeita_MostraAMensagemDoStorage()
    {
        var (viewModel, fotos, _) = CriarComProdutoSelecionado();
        fotos.MensagemRejeicao = "Formato não suportado. Use JPG, PNG ou BMP.";

        viewModel.DefinirFoto("C:\\documento.pdf");

        Assert.Equal("Formato não suportado. Use JPG, PNG ou BMP.", viewModel.Mensagem);
        Assert.Null(viewModel.FotoArquivoAtual);
    }

    [Fact]
    public void RemoverFoto_ComFoto_LimpaAFotoAtual()
    {
        var (viewModel, _, produto) = CriarComProdutoSelecionado();
        viewModel.DefinirFoto("C:\\foto.jpg");

        viewModel.RemoverFotoCommand.Execute(null);

        Assert.Null(viewModel.FotoArquivoAtual);
        Assert.Null(produto.FotoArquivo);
    }

    [Fact]
    public void RemoverFotoCommand_SemFoto_FicaDesabilitado()
    {
        var (viewModel, _, _) = CriarComProdutoSelecionado();

        Assert.False(viewModel.RemoverFotoCommand.CanExecute(null));
    }

    [Fact]
    public void SelecionarProduto_CarregaAFotoDoProdutoEPerdeSelecaoLimpa()
    {
        var (viewModel, _, _) = CriarComProdutoSelecionado();
        viewModel.DefinirFoto("C:\\foto.jpg");
        Assert.True(viewModel.RemoverFotoCommand.CanExecute(null));
        Assert.True(viewModel.TemProdutoSelecionado);

        viewModel.ProdutoSelecionado = null;

        Assert.Null(viewModel.FotoArquivoAtual);
        Assert.False(viewModel.TemProdutoSelecionado);
    }
```

- [ ] **Step 2: Confirmar que falha (erro de compilação)**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet build backend/VarthexComanda.slnx
```

Esperado: falha — construtor sem os 2 novos parâmetros, `DefinirFoto`/`FotoArquivoAtual`/`RemoverFotoCommand`/`TemProdutoSelecionado` inexistentes.

- [ ] **Step 3: Atualizar `ProdutosViewModel`**

Ler `ProdutosViewModel.cs` inteiro antes de editar; a edição é
**aditiva**, não reescreva o que já existe. Adicionar:

1. Campos e construtor: dois campos privados
   (`DefinirFotoProduto _definirFotoProduto`,
   `RemoverFotoProduto _removerFotoProduto`) e dois parâmetros no **fim** da
   lista do construtor, atribuindo-os.
2. Na propriedade observável já existente `produtoSelecionado`, adicionar
   o atributo `[NotifyPropertyChangedFor(nameof(TemProdutoSelecionado))]`
   (junto do `[ObservableProperty]`), e a propriedade:

```csharp
    public bool TemProdutoSelecionado => ProdutoSelecionado is not null;
```

3. Nova propriedade observável:

```csharp
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoverFotoCommand))]
    private string? fotoArquivoAtual;
```

4. Em `OnProdutoSelecionadoChanged(Produto? value)` (já existe), acrescentar
   `FotoArquivoAtual = value?.FotoArquivo;` — no ramo `null` (junto da
   limpeza do formulário) e no ramo com produto (junto do preenchimento).
5. Os dois membros novos:

```csharp
    public void DefinirFoto(string caminhoOrigem)
    {
        if (ProdutoSelecionado is null)
        {
            Mensagem = "Selecione um produto para definir a foto.";
            return;
        }

        try
        {
            var resultado = _definirFotoProduto.Executar(ProdutoSelecionado.Id, caminhoOrigem);
            if (!resultado.Sucesso)
            {
                Mensagem = string.Join(" ", resultado.Erros);
                return;
            }

            ProdutoSelecionado.FotoArquivo = resultado.Valor!.FotoArquivo;
            FotoArquivoAtual = resultado.Valor.FotoArquivo;
            Mensagem = string.Empty;
        }
        catch (Exception)
        {
            Mensagem = "Não foi possível salvar a foto. Tente novamente.";
        }
    }

    [RelayCommand(CanExecute = nameof(PodeRemoverFoto))]
    private void RemoverFoto()
    {
        if (ProdutoSelecionado is null)
        {
            return;
        }

        try
        {
            var resultado = _removerFotoProduto.Executar(ProdutoSelecionado.Id);
            if (!resultado.Sucesso)
            {
                Mensagem = string.Join(" ", resultado.Erros);
                return;
            }

            ProdutoSelecionado.FotoArquivo = null;
            FotoArquivoAtual = null;
            Mensagem = string.Empty;
        }
        catch (Exception)
        {
            Mensagem = "Não foi possível remover a foto. Tente novamente.";
        }
    }

    private bool PodeRemoverFoto() => FotoArquivoAtual is not null;
```

Adicionar `using VarthexComanda.Application.Catalogo;` se ainda não houver.

- [ ] **Step 4: Registrar no DI (`App.xaml.cs`)**

Ler `App.xaml.cs`, e junto dos registros do catálogo
(`services.AddTransient<CadastrarProduto>()` etc.) adicionar:

```csharp
        services.AddSingleton<IFotoStorage>(sp => new ArquivoFotoStorage(
            sp.GetRequiredService<AppPaths>(),
            sp.GetRequiredService<ILogger>()));
        services.AddTransient<DefinirFotoProduto>();
        services.AddTransient<RemoverFotoProduto>();
```

(`ILogger` aqui é `Serilog.ILogger`, já registrado como singleton nesse
arquivo; `using Serilog;` já existe. Confirme que
`using VarthexComanda.Infrastructure.Storage;` e
`using VarthexComanda.Application.Catalogo;` existem; adicione se faltar.)

- [ ] **Step 5: Rodar os testes e commitar**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release
```

Esperado: todos PASS (208 + 6 novos = 214).

```bash
git add backend/src/VarthexComanda.Desktop/Catalogo/ProdutosViewModel.cs backend/src/VarthexComanda.Desktop/App.xaml.cs backend/tests/VarthexComanda.Desktop.Tests/Catalogo/ProdutosViewModelTests.cs
git commit -m "feat: adiciona foto do produto ao ProdutosViewModel"
```

---

## Task 5: Conversor de imagem, telas de Produtos e do menu

**Files:**
- Create: `backend/src/VarthexComanda.Desktop/Catalogo/FotoArquivoParaImagemConverter.cs`
- Modify: `backend/src/VarthexComanda.Desktop/Catalogo/ProdutosView.xaml`
- Modify: `backend/src/VarthexComanda.Desktop/Catalogo/ProdutosView.xaml.cs`
- Modify: `backend/src/VarthexComanda.Desktop/Atendimento/AtendimentoView.xaml`
- Modify: `backend/src/VarthexComanda.Desktop/App.xaml.cs`
- Test: `backend/tests/VarthexComanda.Desktop.Tests/Catalogo/FotoArquivoParaImagemConverterTests.cs`

**Interfaces:**
- Consumes: `ProdutosViewModel.FotoArquivoAtual`/`.TemProdutoSelecionado`/`.DefinirFoto(string)`/`.RemoverFotoCommand` (Task 4), `AppPaths.FotosDirectory` (Task 1), `Produto.FotoArquivo`.
- Produces: nada consumido por tasks futuras.

- [ ] **Step 1: Escrever os testes do conversor (vão falhar — a classe não existe)**

`backend/tests/VarthexComanda.Desktop.Tests/Catalogo/FotoArquivoParaImagemConverterTests.cs`:

```csharp
using System.Globalization;
using System.Windows.Media.Imaging;
using VarthexComanda.Desktop.Catalogo;
using Xunit;

namespace VarthexComanda.Desktop.Tests.Catalogo;

public class FotoArquivoParaImagemConverterTests : IDisposable
{
    private const string PngUmPixelBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

    private readonly string _pasta = Path.Combine(Path.GetTempPath(), $"varthex-conv-tests-{Guid.NewGuid()}");
    private readonly string? _diretorioOriginal = FotoArquivoParaImagemConverter.DiretorioFotos;
    private readonly FotoArquivoParaImagemConverter _conversor = new();

    public FotoArquivoParaImagemConverterTests()
    {
        Directory.CreateDirectory(_pasta);
        FotoArquivoParaImagemConverter.DiretorioFotos = _pasta;
    }

    public void Dispose()
    {
        FotoArquivoParaImagemConverter.DiretorioFotos = _diretorioOriginal;
        if (Directory.Exists(_pasta))
        {
            Directory.Delete(_pasta, true);
        }
    }

    private object? Converter(object? valor) =>
        _conversor.Convert(valor, typeof(System.Windows.Media.ImageSource), null!, CultureInfo.InvariantCulture);

    [Fact]
    public void Convert_ValorNuloOuVazio_RetornaNulo()
    {
        Assert.Null(Converter(null));
        Assert.Null(Converter(string.Empty));
    }

    [Fact]
    public void Convert_DiretorioNaoDefinido_RetornaNulo()
    {
        FotoArquivoParaImagemConverter.DiretorioFotos = null;

        Assert.Null(Converter("foto.png"));
    }

    [Fact]
    public void Convert_ArquivoInexistente_RetornaNulo()
    {
        Assert.Null(Converter("nao-existe.png"));
    }

    [Fact]
    public void Convert_ArquivoCorrompido_RetornaNulo()
    {
        File.WriteAllBytes(Path.Combine(_pasta, "quebrada.png"), new byte[] { 1, 2, 3, 4 });

        Assert.Null(Converter("quebrada.png"));
    }

    [Fact]
    public void Convert_ImagemValida_RetornaBitmapCongelado()
    {
        File.WriteAllBytes(Path.Combine(_pasta, "ok.png"), System.Convert.FromBase64String(PngUmPixelBase64));

        var resultado = Converter("ok.png");

        var imagem = Assert.IsType<BitmapImage>(resultado);
        Assert.True(imagem.IsFrozen);
    }

    [Fact]
    public void Convert_ImagemValida_NaoTravaOArquivo()
    {
        var caminho = Path.Combine(_pasta, "livre.png");
        File.WriteAllBytes(caminho, System.Convert.FromBase64String(PngUmPixelBase64));

        Converter("livre.png");

        var excecao = Record.Exception(() => File.Delete(caminho));
        Assert.Null(excecao);
    }
}
```

Se `Convert_ImagemValida_*` falharem por restrição de thread do WPF no
xUnit (exceção de thread/STA ao criar `BitmapImage`), reporte
DONE_WITH_CONCERNS com a mensagem exata, mantenha os quatro testes de
retorno nulo e remova esses dois — não use reflection nem workarounds.

- [ ] **Step 2: Confirmar que falha (erro de compilação)**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet build backend/VarthexComanda.slnx
```

Esperado: falha — `FotoArquivoParaImagemConverter` não existe.

- [ ] **Step 3: Implementar o conversor**

`backend/src/VarthexComanda.Desktop/Catalogo/FotoArquivoParaImagemConverter.cs`:

```csharp
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace VarthexComanda.Desktop.Catalogo;

public class FotoArquivoParaImagemConverter : IValueConverter
{
    public static string? DiretorioFotos { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string nomeArquivo || string.IsNullOrWhiteSpace(nomeArquivo) || string.IsNullOrEmpty(DiretorioFotos))
        {
            return null;
        }

        var caminho = Path.Combine(DiretorioFotos, Path.GetFileName(nomeArquivo));
        if (!File.Exists(caminho))
        {
            return null;
        }

        try
        {
            var imagem = new BitmapImage();
            imagem.BeginInit();
            imagem.CacheOption = BitmapCacheOption.OnLoad;
            imagem.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            imagem.DecodePixelWidth = 320;
            imagem.UriSource = new Uri(caminho, UriKind.Absolute);
            imagem.EndInit();
            imagem.Freeze();
            return imagem;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or FileFormatException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
```

- [ ] **Step 4: Ligar o diretório de fotos no startup**

Em `App.xaml.cs`, logo depois de `paths.EnsureCreated();`, adicionar:

```csharp
        VarthexComanda.Desktop.Catalogo.FotoArquivoParaImagemConverter.DiretorioFotos = paths.FotosDirectory;
```

- [ ] **Step 5: Tela de Produtos — preview e botões**

Ler `ProdutosView.xaml` e `ProdutosView.xaml.cs`. Em
`ProdutosView.xaml`:

1. Garantir um `xmlns:local="clr-namespace:VarthexComanda.Desktop.Catalogo"`
   (se já existir um prefixo para esse namespace, reutilize) e declarar o
   conversor em `UserControl.Resources` (crie o elemento se não existir):

```xml
<local:FotoArquivoParaImagemConverter x:Key="FotoImagem" />
```

2. Na coluna direita (o formulário), logo **depois** do `CheckBox` "Ativo"
   e antes da linha dos botões Novo/Salvar/Desativar, inserir:

```xml
<TextBlock Text="Foto" Margin="0,8,0,2" />
<Grid Width="120" Height="120" HorizontalAlignment="Left" Background="#EDEDEA" ClipToBounds="True">
    <Image Source="{Binding FotoArquivoAtual, Converter={StaticResource FotoImagem}}" Stretch="UniformToFill" />
</Grid>
<StackPanel Orientation="Horizontal" Margin="0,4,0,0">
    <Button Content="Escolher foto..." Click="EscolherFoto_Click"
            IsEnabled="{Binding TemProdutoSelecionado}" Margin="0,0,8,0" Padding="8,2" />
    <Button Content="Remover foto" Command="{Binding RemoverFotoCommand}" Padding="8,2" />
</StackPanel>
<TextBlock Text="Salve o produto e selecione-o na lista para adicionar uma foto."
           FontSize="11" Foreground="#6B6B67" TextWrapping="Wrap" Margin="0,4,0,8">
    <TextBlock.Style>
        <Style TargetType="TextBlock">
            <Style.Triggers>
                <DataTrigger Binding="{Binding TemProdutoSelecionado}" Value="True">
                    <Setter Property="Visibility" Value="Collapsed" />
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </TextBlock.Style>
</TextBlock>
```

Em `ProdutosView.xaml.cs`, adicionar `using System.Windows;` e
`using Microsoft.Win32;` (se faltarem) e o handler:

```csharp
    private void EscolherFoto_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new OpenFileDialog
        {
            Title = "Escolher foto do produto",
            Filter = "Imagens (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp"
        };

        if (dialogo.ShowDialog() == true && DataContext is ProdutosViewModel viewModel)
        {
            viewModel.DefinirFoto(dialogo.FileName);
        }
    }
```

- [ ] **Step 6: Cards do menu do Atendimento**

Em `AtendimentoView.xaml` (ler o arquivo primeiro):

1. Adicionar `xmlns:catalogo="clr-namespace:VarthexComanda.Desktop.Catalogo"`
   no elemento raiz e, em `UserControl.Resources`:

```xml
<catalogo:FotoArquivoParaImagemConverter x:Key="FotoImagem" />
```

2. No `DataTemplate` do card de produto (o `Button` com `Width="128"
   Height="128"` bindado a `ProdutosCatalogo`), substituir a linha
   `<Border Grid.Row="0" Background="#EDEDEA" />` por:

```xml
<Grid Grid.Row="0" Background="#EDEDEA" ClipToBounds="True">
    <Image Source="{Binding FotoArquivo, Converter={StaticResource FotoImagem}}" Stretch="UniformToFill" />
</Grid>
```

(O `Binding FotoArquivo` resolve em `Produto.FotoArquivo`, o `DataContext` do
card. Sem foto, o `Image` fica vazio e o fundo cinza continua visível.)

- [ ] **Step 7: Build, suíte completa e commit**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet build backend/VarthexComanda.slnx
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release
```

Esperado: build limpo; todos PASS (214 + 6 novos do conversor = 220, ou 218
se os dois testes de imagem válida forem removidos pela ressalva do
Step 1).

```bash
git add backend/src/VarthexComanda.Desktop/Catalogo/FotoArquivoParaImagemConverter.cs backend/src/VarthexComanda.Desktop/Catalogo/ProdutosView.xaml backend/src/VarthexComanda.Desktop/Catalogo/ProdutosView.xaml.cs backend/src/VarthexComanda.Desktop/Atendimento/AtendimentoView.xaml backend/src/VarthexComanda.Desktop/App.xaml.cs backend/tests/VarthexComanda.Desktop.Tests/Catalogo/FotoArquivoParaImagemConverterTests.cs
git commit -m "feat: exibe foto do produto no menu e na tela de Produtos"
```

- [ ] **Step 8: Verificação manual (sem display: só checagem de inicialização)**

Ambiente sem display: rode o app (`dotnet run --project
backend/src/VarthexComanda.Desktop`), confirme que o processo sobe sem
exceção por alguns segundos e encerre-o. **Não afirme ter feito o roteiro
interativo.** Roteiro para um humano testar depois do merge:

1. Aba Produtos: sem produto selecionado, "Escolher foto..." fica
   desabilitado e aparece a dica.
2. Selecionar um produto → "Escolher foto..." habilita; escolher um JPG/PNG
   → preview aparece no formulário.
3. Aba Atendimento → abrir uma comanda → o card do produto mostra a foto.
4. Trocar a foto → preview e card mudam; o arquivo antigo some de
   `%LOCALAPPDATA%\VarthexComanda\fotos`.
5. "Remover foto" → volta ao cinza e o arquivo some da pasta.
6. Escolher um `.pdf` (renomeando a extensão do filtro se necessário) ou
   um arquivo > 10 MB → mensagem de erro, produto inalterado.

---

## Task 6: Verificação final e changelog

**Files:**
- Modify: `docs/CHANGELOG.md`

- [ ] **Step 1: Rodar a suíte completa**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release
```

Esperado: todos PASS, 0 falhas (total esperado ≈ 220; reporte o número
real observado).

- [ ] **Step 2: Atualizar o changelog**

Em `docs/CHANGELOG.md`, no topo (mesmo formato das entradas anteriores),
adicionar:

```markdown
## 1.12 - 2026-09-19 - Foto do produto

- Produtos passam a ter uma foto opcional: "Escolher foto..." e "Remover
  foto" no formulário da tela de Produtos (para um produto já salvo e
  selecionado), com preview.
- A foto aparece nos cards do menu da tela de Atendimento; produto sem
  foto (ou com arquivo ausente/corrompido) continua mostrando o
  placeholder cinza.
- A imagem é copiada para `%LOCALAPPDATA%\VarthexComanda\fotos` (formatos
  JPG/PNG/BMP, até 10 MB); o banco guarda só o nome do arquivo. Trocar ou
  remover apaga o arquivo antigo.
- Limitação conhecida: o backup ainda copia só o banco de dados — as fotos
  não vão junto (fatia futura).
```

- [ ] **Step 3: Commitar**

```bash
git add docs/CHANGELOG.md
git commit -m "docs: registra foto do produto no changelog"
```
