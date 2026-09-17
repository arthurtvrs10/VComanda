# Comandas (Etapa 3) — Grade, Abertura e Itens

Data: 2026-09-17
Escopo: RF06-13, UC02-04 — grade de números, abertura de comanda, catálogo
rápido, lançamento/alteração/remoção de itens, recálculo de total, cancelamento.

## Contexto

A base técnica (Etapa 0+1) e o catálogo (Etapa 2) já estão em `main`. As
entidades `Comanda`/`ItemComanda` e seu mapeamento EF Core **já existem** desde
a Etapa 0+1 — esta fatia não cria schema novo, só a camada de Application,
Infrastructure (repositório) e UI em cima do que já existe.

Fonte de verdade: [docs/docs/02-requisitos.md](../docs/docs/02-requisitos.md)
(RF06-13), [03-regras-negocio.md](../docs/docs/03-regras-negocio.md) (RN01-10),
[04-casos-de-uso.md](../docs/docs/04-casos-de-uso.md) (UC02-04),
[15-contratos-aplicacao.md](../docs/docs/15-contratos-aplicacao.md) (contrato
de `ComandaRepository` e casos de uso — nomes são sugestões, adaptados aqui),
[07-experiencia-usuario.md](../docs/docs/07-experiencia-usuario.md) (tela
inicial = grade de comandas, tela da comanda), e os testes de
[09-testes-aceitacao.md](../docs/docs/09-testes-aceitacao.md) (CT01-08).

Fora de escopo: encerramento e venda (RF14-17, Etapa 4).

## Decisão: transacionalidade sem Unit of Work explícito

Confirmado com o usuário: `IComandaRepository` usa métodos "verbo" por
operação de negócio inteira, não CRUD fino. Cada método (`AbrirComanda`,
`AdicionarItem`, `AlterarQuantidade`, `RemoverItem`, `CancelarComanda`) abre
**um único** `DbContext` (via `IDbContextFactory`), carrega o que precisa,
muta, recalcula o total e chama `SaveChanges()` uma vez — atômico por
construção, sem precisar de uma abstração de transação separada. Isso
satisfaz a exigência de docs/06-arquitetura.md ("abertura de comanda...
inclusão, alteração ou remoção de item com atualização do total" devem ser
transacionais).

**Concorrência do número (RN01, CT02):** `AbrirComanda` não faz
pré-verificação e confia — a verificação prévia teria uma janela de corrida
(TOCTOU). Em vez disso, tenta inserir diretamente; o índice único parcial
`uq_comanda_numero_aberta` (já existe desde a Etapa 0+1) é quem garante a
regra de verdade. O repositório deixa a `DbUpdateException` de violação de
unicidade propagar; o caso de uso `AbrirComanda` a captura e traduz para
`Resultado<Comanda>.Falha("Já existe uma comanda aberta com esse número.")`.
Isso é o que o teste de concorrência do guia de implementação (Etapa 3, item
9) precisa exercitar.

## Grade de números

QV02 (quantos números de comanda existem fisicamente) segue sem resposta em
[13-pendencias-validacao.md](../docs/docs/13-pendencias-validacao.md). Em vez
de travar nisso, a grade não assume uma faixa fixa: mostra as comandas
**atualmente abertas** (consulta dinâmica via `ListarAbertas()`) como
ocupadas, e o operador digita qualquer número positivo livre para abrir. Se
QV02 for respondida depois, é ajuste de UI (grade fixa com N células), não de
dados ou casos de uso.

## Contratos

```csharp
namespace VarthexComanda.Application.Atendimento;

public class ComandaComItens
{
    public required Comanda Comanda { get; init; }
    public required IReadOnlyList<ItemComanda> Itens { get; init; }
}

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

`Produto` (não um DTO próprio) é passado para `AdicionarItem` porque o
repositório precisa do nome e preço atuais para o snapshot histórico (RN04) —
quem resolve o produto e confirma que está ativo é o caso de uso
`AdicionarItem`, reaproveitando `IProdutoRepository` da Etapa 2.

**Comportamento de `AdicionarItem` no repositório:** se já existir um
`ItemComanda` nesta comanda para o mesmo `produtoId` **e** o mesmo
`preco_unitario_centavos` (o preço atual do produto no momento do clique),
incrementa a quantidade desse item em vez de criar uma linha nova — é o que
CT03 exige ("Duas inclusões de R$ 18,00 resultam em quantidade 2"). Se o
preço mudou entre dois lançamentos do mesmo produto na mesma comanda (preço
do produto foi alterado no meio do atendimento), cria uma linha nova — cada
uma preserva seu próprio preço histórico (RN05), nunca mescla preços
diferentes em uma linha.

**`total_centavos` da comanda** é recalculado como a soma de
`subtotal_centavos` de todos os itens da comanda (RN08), sempre dentro do
mesmo `SaveChanges()` da operação que mudou os itens.

**RN10 (comanda fechada é imutável):** `AlterarQuantidade`, `RemoverItem` e
`AdicionarItem` verificam, dentro do próprio método do repositório (depois de
carregar a comanda), que o status é `ABERTA`; caso contrário lançam
`InvalidOperationException`, que o caso de uso traduz para
`Resultado.Falha("A comanda não está aberta.")`. Defesa em profundidade — a
UI nunca deveria oferecer essas ações numa comanda fechada, mas o caso de uso
não confia só nisso.

## Casos de uso

- **AbrirComanda(numero)** → `Resultado<Comanda>`. Valida `numero > 0`; captura
  duplicidade (ver acima).
- **AdicionarItem(comandaId, produtoId, quantidade)** → `Resultado<ComandaComItens>`.
  Busca o produto via `IProdutoRepository`; falha se não existir ou estiver
  inativo (RN03, código `PRODUTO_INATIVO` do vocabulário de docs/15); valida
  `quantidade > 0` (RN06); delega ao repositório.
- **AlterarQuantidade(itemId, quantidade)** → `Resultado<ComandaComItens>`.
  Valida `quantidade > 0` — zero ou negativo é sempre rejeitado aqui (RN06,
  CT06); a UI é quem decide redirecionar uma quantidade zero para o fluxo de
  remoção confirmada (UC04), não este caso de uso.
- **RemoverItem(itemId)** → `Resultado<ComandaComItens>`. Sem validação extra
  além de RN10 (imutabilidade); confirmação é responsabilidade da UI (RNF08).
- **CancelarComanda(comandaId)** → `Resultado<Comanda>`. Falha se a comanda já
  não estiver `ABERTA`; confirmação é responsabilidade da UI.

## Shell de navegação (novo nesta fatia)

Em Etapa 2, `MainWindow` mostrava a tela de Produtos diretamente, sem shell,
porque não havia outra tela ainda — e ficou registrado explicitamente que a
Etapa 3 introduziria navegação real. É agora: [07-experiencia-usuario.md](../docs/docs/07-experiencia-usuario.md)
define a tela inicial como a grade de comandas, não Produtos.

- `ProdutosViewModel`/o conteúdo de Produtos migra de `MainWindow.xaml` para
  um `UserControl` próprio (`ProdutosView.xaml`), sem mudar o ViewModel.
- Novo `AtendimentoViewModel` + `AtendimentoView.xaml`: mostra a grade quando
  nenhuma comanda está aberta na tela, e a comanda (itens + catálogo rápido +
  total) quando uma está selecionada — uma única view com um
  `DataTrigger`/visibilidade condicional em vez de duas views separadas,
  para não multiplicar código de navegação por enquanto.
- `MainWindow` vira um shell simples: dois botões ("Atendimento" | "Produtos")
  e um `ContentControl` cujo conteúdo troca entre as duas views (ambas
  resolvidas via o container de DI, cada uma com seu ViewModel). Abre em
  "Atendimento" por padrão.

## Tela da comanda

Conforme docs/07 "Tela da comanda": catálogo por categoria (reaproveita
`ListarCategoriasAtivas`/`PesquisarProdutos` da Etapa 2) e busca por nome;
botões grandes de produto; painel de itens com quantidade, preço e subtotal;
controles `+`, `-` e remover; total sempre visível; `Cancelar comanda`
visualmente separado das demais ações.

## Testes

- `Application.Tests`: um teste por regra de cada caso de uso, com um fake
  `IComandaRepository` (e reaproveitando os fakes de categoria/produto da
  Etapa 2 onde fizer sentido).
- `Infrastructure.Tests`: `EfComandaRepository` contra SQLite real — abrir,
  listar abertas, adicionar item (incluindo o caso de mesclar quantidade e o
  caso de preço diferente gerar linha nova), alterar quantidade, remover,
  cancelar, e o teste de concorrência do número (duas chamadas a
  `AbrirComanda` com o mesmo número — a segunda deve lançar/falhar).
- Sem testes de UI automatizados nesta fatia (mesma decisão da Etapa 2) —
  **mas**, diferente da Etapa 2, a revisão final daquela fatia apontou que
  ViewModels sem tipos WPF são testáveis e que a ausência de testes deixou
  passar dois bugs reais por 8 revisões de tarefa. Esta fatia adota a
  recomendação: cria `VarthexComanda.Desktop.Tests` e cobre a lógica de
  `AtendimentoViewModel`/`ProdutosViewModel` com testes de unidade contra os
  fakes já existentes (sem tocar em WPF/XAML).

## Critérios de pronto

- `dotnet build`/`dotnet test` limpos na solução inteira;
- abrir comanda, lançar item duas vezes do mesmo produto (quantidade some em
  vez de duplicar linha), alterar quantidade, remover item e cancelar
  comanda, tudo funcionando manualmente na tela;
- abrir duas comandas com o mesmo número (uma após a outra, simulando
  concorrência) — a segunda é recusada e a primeira permanece intacta;
- comanda cancelada libera o número (reaparece como livre na grade);
- Produtos continua acessível e funcional pelo shell de navegação.
