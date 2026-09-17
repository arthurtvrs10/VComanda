# Catálogo (Etapa 2) — Categorias e Produtos

Data: 2026-09-17
Escopo: RF01-05, UC01, US01-03 — cadastro/alteração/desativação de categorias e
produtos, com tela de listagem, formulário, filtro e busca.

## Contexto

A base técnica (Etapa 0+1, já em `main`) entrega solução, banco, migração inicial,
instância única, logging e uma janela WPF vazia. Este spec cobre a primeira fatia de
negócio real: o catálogo, primeiro passo do roadmap
([11-roadmap-riscos.md](../docs/docs/11-roadmap-riscos.md): "E2 Catálogo").

Fonte de verdade: [docs/docs/02-requisitos.md](../docs/docs/02-requisitos.md) (RF01-05),
[03-regras-negocio.md](../docs/docs/03-regras-negocio.md) (RN16-17),
[04-casos-de-uso.md](../docs/docs/04-casos-de-uso.md) (UC01),
[15-contratos-aplicacao.md](../docs/docs/15-contratos-aplicacao.md) (contratos de
repositório), [07-experiencia-usuario.md](../docs/docs/07-experiencia-usuario.md)
(tela "Produtos"), [18-historias-usuario.md](../docs/docs/18-historias-usuario.md)
(US01-03), e os testes adicionais de catálogo em
[09-testes-aceitacao.md](../docs/docs/09-testes-aceitacao.md).

Fora de escopo: comandas, itens, vendas (RF06+, Etapa 3).

## Organização do código

Pastas de domínio dentro de cada projeto de camada existente (decisão confirmada
pelo usuário — reconcilia a estrutura por domínio sugerida em
[06-arquitetura.md](../docs/docs/06-arquitetura.md) com a estrutura por camada já
aprovada em [19-plataforma-windows-dotnet.md](../docs/docs/19-plataforma-windows-dotnet.md)):

```
backend/src/VarthexComanda.Domain/
  Categoria.cs, Produto.cs                    (já existem, não movidos)

backend/src/VarthexComanda.Application/
  Catalogo/
    ICategoriaRepository.cs
    IProdutoRepository.cs
    Resultado.cs
    CadastrarCategoria.cs
    AlterarCategoria.cs
    ListarCategoriasAtivas.cs
    CadastrarProduto.cs
    AlterarProduto.cs
    DesativarProduto.cs
    PesquisarProdutos.cs

backend/src/VarthexComanda.Infrastructure/
  Persistence/Catalogo/
    EfCategoriaRepository.cs
    EfProdutoRepository.cs

backend/src/VarthexComanda.Desktop/
  Catalogo/
    ProdutosViewModel.cs
    CategoriaItem.cs / ProdutoItem.cs (modelos de exibição, se necessário)
  MainWindow.xaml / MainWindow.xaml.cs (reformulada como tela "Produtos")
  App.xaml.cs (composição via DI — ver seção própria)
```

## Tipo `Resultado<T>`

Casos de uso retornam sucesso/falha de validação sem lançar exceção para erros
esperados (nome vazio, preço inválido, categoria inativa) — exceções ficam
reservadas para falhas técnicas (banco indisponível), como já é o padrão desta
camada (`docs/15`: "falhas técnicas são convertidas em resultado ou exceção de
aplicação compreensível para a interface").

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

## Casos de uso e regras

Todos os métodos são síncronos por enquanto (banco local, sem I/O de rede) e
recebem/retornam tipos de `Domain`, nunca `DbContext` (docs/15: "ViewModels
dependem de casos de uso e nunca de VarthexDbContext").

- **CadastrarCategoria(nome)** → `Resultado<Categoria>`. Valida: nome não pode ser
  vazio/só espaços (`Trim()` antes de gravar); duplicidade de nome é responsabilidade
  do índice único `categoria.nome` (`UNIQUE` + `COLLATE NOCASE` no schema) — capturar
  `DbUpdateException` de violação de unicidade e traduzir para
  `Resultado<Categoria>.Falha("Já existe uma categoria com esse nome.")`.
- **AlterarCategoria(id, nome, ativo)** → `Resultado<Categoria>`. Mesmas validações de
  nome. Desativar categoria não afeta produtos existentes (RN16) — não valida nem
  bloqueia produtos vinculados; um produto de categoria desativada simplesmente
  deixa de poder receber *novos* produtos daquela categoria (ver `CadastrarProduto`).
- **ListarCategoriasAtivas()** → `IReadOnlyList<Categoria>`, ordenadas por nome.
- **CadastrarProduto(nome, categoriaId, precoCentavos)** → `Resultado<Produto>`.
  Valida: nome não vazio (trim); `precoCentavos > 0` (RNF10 — sempre `long`,
  nunca `double`); categoria deve existir E estar ativa (US02: "categoria ativa,
  nome obrigatório e preço positivo em centavos") — senão
  `Resultado<Produto>.Falha("Selecione uma categoria ativa.")`.
- **AlterarProduto(id, nome, categoriaId, precoCentavos)** → `Resultado<Produto>`.
  Mesmas validações de `CadastrarProduto`. Alterar preço não modifica itens já
  lançados em comandas (RN05) — isso é garantido pelo snapshot em `item_comanda`
  (já no schema; nada a fazer aqui além de não tocar em `item_comanda`).
- **DesativarProduto(id)** → `Resultado<Produto>`. Define `Ativo = false`; não
  exclui fisicamente (RN17). Reativar é o mesmo fluxo de `AlterarProduto` com
  `ativo = true` — não precisa de caso de uso próprio.
- **PesquisarProdutos(categoriaId?, texto?)** → `IReadOnlyList<Produto>`. Filtra por
  categoria quando informada; filtra por nome contendo o texto, sem diferenciar
  maiúsculas/minúsculas (comparação via `EF.Functions.Like` com o texto em minúsculas
  dos dois lados, já que `produto.nome` não tem `COLLATE NOCASE` no schema — a
  busca case-insensitive é responsabilidade da consulta, não da coluna). Lista
  produtos ativos e inativos (a tela mostra os dois, com indicação visual do
  estado) — filtrar só por ativos é responsabilidade da UI/RF09, não desta consulta.

## Repositórios

Interfaces em `Application/Catalogo/`, implementações em
`Infrastructure/Persistence/Catalogo/`, usando `IDbContextFactory<VarthexComandaDbContext>`
(um `CreateDbContext()` por operação — DbContext de curta duração, como já exigido
em docs/06 e docs/15).

```csharp
public interface ICategoriaRepository
{
    Categoria? BuscarPorId(int id);
    IReadOnlyList<Categoria> ListarAtivas();
    bool ExisteNome(string nome, int? ignorarId = null);
    Categoria Salvar(Categoria categoria);
}

public interface IProdutoRepository
{
    Produto? BuscarPorId(int id);
    IReadOnlyList<Produto> Pesquisar(int? categoriaId, string? texto);
    Produto Salvar(Produto produto);
}
```

## Composição (`App.xaml.cs`) — resolve a pendência de DI deixada em aberto

A revisão final da Etapa 0+1 apontou que `Microsoft.Extensions.DependencyInjection`
e `CommunityToolkit.Mvvm` estavam referenciados mas nunca usados — esta fatia
resolve isso, já que é a primeira que precisa de fato compor ViewModels e casos
de uso.

Mudança no startup:

1. Mutex, `AppPaths`, logging continuam exatamente como estão.
2. Em vez de abrir uma `SqliteConnection` manual e construir `DbContextOptions` uma
   vez, registrar `services.AddDbContextFactory<VarthexComandaDbContext>(o =>
   o.UseSqlite($"Data Source={paths.DatabasePath};Foreign Keys=True"))` — o EF Core
   cuida de abrir/fechar conexões por `DbContext` criado.
3. A migração e o `PRAGMA integrity_check` do startup passam a criar um
   `DbContext` via `serviceProvider.GetRequiredService<IDbContextFactory<...>>().CreateDbContext()`,
   fazer o trabalho, e descartá-lo — mesmo comportamento observável de antes.
4. Registrar `ICategoriaRepository`/`IProdutoRepository` e os casos de uso de
   catálogo (transient — são objetos sem estado).
5. Registrar `ProdutosViewModel` e `MainWindow` no container; resolver
   `MainWindow` do `ServiceProvider` em vez de `new MainWindow()`.
6. Backup preventivo e `IClock` continuam iguais (só passam a vir do container
   em vez de variáveis locais soltas).

## Tela "Produtos" (`MainWindow`)

Layout (docs/07 "Tela Produtos": "Lista, filtros, formulário, preço, categoria e
situação"):

- topo: caixa de busca por nome + combo de filtro por categoria (inclui "Todas");
- lista central: produtos (nome, categoria, preço formatado, situação ativo/inativo);
- painel lateral: formulário (nome, combo de categoria ativa, preço em reais
  convertido para/de centavos na borda da UI, checkbox ativo) com botões "Novo",
  "Salvar", "Desativar";
- campo simples para cadastrar nova categoria (texto + botão "Adicionar categoria"),
  sem diálogo separado — mantém a tela em um único lugar por enquanto.

`ProdutosViewModel` (CommunityToolkit.Mvvm, `[ObservableProperty]`/`[RelayCommand]`)
expõe: lista observável de produtos, lista de categorias para o combo, propriedades
do formulário, comandos `Salvar`, `Novo`, `Desativar`, `AdicionarCategoria`,
`Pesquisar`. Mensagens de erro do `Resultado<T>` viram uma propriedade de texto
exibida na tela (sem `MessageBox` para erro de validação — isso é para erro técnico,
como já é convenção no `App.xaml.cs`).

## Testes

- `Application.Tests`: um teste por regra de validação de cada caso de uso
  (nome vazio, preço zero/negativo, categoria inativa/inexistente, duplicidade de
  nome de categoria) — usando repositórios fake em memória (implementações simples
  de `ICategoriaRepository`/`IProdutoRepository` só para os testes, não EF).
- `Infrastructure.Tests`: um teste por repositório contra SQLite real (mesmo padrão
  já usado na Etapa 0+1) confirmando que `Salvar`/`BuscarPorId`/`ListarAtivas`/
  `Pesquisar` funcionam e que a busca por nome não diferencia maiúsculas de
  minúsculas.
- Sem testes de UI automatizados (WPF) — verificação manual do formulário, como na
  Etapa 0+1.

## Critérios de pronto

- `dotnet build`/`dotnet test` limpos (0 erros, 0 avisos) na solução inteira;
- cadastrar categoria, cadastrar produto vinculado, editar preço, desativar produto
  e ver que ele some da lista de "ativos" mas continua existindo no banco, tudo
  funcionando manualmente na tela;
- categoria inativa não aparece no combo de seleção ao cadastrar/editar produto;
- busca por nome funciona independente de maiúsculas/minúsculas.
