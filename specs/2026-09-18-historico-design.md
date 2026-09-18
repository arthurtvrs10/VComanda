# Histórico e Resumo (Etapa 5)

Data: 2026-09-18
Escopo: RF18-20, RN20, UC07 — consultar vendas concluídas por data, localizar
pelo número da comanda, ver detalhe de uma venda, e resumo diário (quantidade,
total, ticket médio).

## Contexto

Base técnica (Etapa 0+1), catálogo (Etapa 2), comandas (Etapa 3) e
encerramento/venda (Etapa 4) já estão em `main`. `Venda`/`StatusVenda` e seu
mapeamento EF Core já existem desde a Etapa 0+1; `EncerrarComanda` (Etapa 4)
já escreve vendas. Esta fatia é a primeira a **ler** vendas — hoje não existe
nenhum método de consulta a `venda` em nenhum repositório.

Fonte de verdade: [docs/docs/02-requisitos.md](../docs/docs/02-requisitos.md)
(RF18-20), [03-regras-negocio.md](../docs/docs/03-regras-negocio.md) (RN20),
[04-casos-de-uso.md](../docs/docs/04-casos-de-uso.md) (UC07),
[05-modelagem-dados.md](../docs/docs/05-modelagem-dados.md) (entidade VENDA,
índice `idx_venda_finalizada_em`), [07-experiencia-usuario.md](../docs/docs/07-experiencia-usuario.md)
(áreas "Histórico"/"Resumo", mensagem "Sem vendas"),
[14-guia-implementacao.md](../docs/docs/14-guia-implementacao.md) (Etapa 5).

Fora de escopo: qualquer forma de pagamento (estrutural — `Venda` não carrega
esse dado), edição/exclusão de vendas (RN10/RN17 — histórico é só leitura),
exportação/relatórios além do resumo diário simples, período multi-dia (RF20 é
"resumo diário", um dia por vez).

## Decisões confirmadas com o usuário

**Repositório:** novo `IVendaRepository`, separado de `IComandaRepository`.
`IComandaRepository` já tem 8 métodos transacionais de atendimento; misturar
consulta analítica de histórico no mesmo contrato confundiria as
responsabilidades. `IVendaRepository` cresce sozinho conforme o histórico
evolui.

**Fuso horário:** `venda.finalizada_em` continua em UTC (nada muda em
`IClock` nem no que já foi gravado). O filtro por data local acontece **na
consulta**: o repositório recebe a data local já como um intervalo UTC
`[inícioUtc, fimUtc)`, calculado pelo caso de uso a partir do fuso fixo
`America/Sao_Paulo` (UTC-3, sem horário de verão atualmente no Brasil) — uma
venda às 23h de Brasília (02h UTC do dia seguinte) aparece no dia local
correto, não no dia UTC seguinte.

**Tela:** uma única tela "Histórico" combina lista + resumo — não duas telas
separadas como a tabela de navegação do docs/07 sugere à primeira vista.
Resumo (quantidade, total, ticket médio) e lista sempre refletem a mesma data
selecionada; um seletor de data duplicado em duas telas seria retrabalho sem
benefício.

**Busca por número:** a data selecionada é sempre o filtro primário (padrão
"hoje"); o campo de busca por número de comanda filtra a lista **já
carregada** daquele dia, sem nova consulta ao banco. Encontrar uma venda de
outro dia exige trocar a data primeiro — não há busca global cross-data nesta
fatia (YAGNI; RF18 não pede isso, só "filtrar por data e localizar pelo
número").

## Contratos

```csharp
namespace VarthexComanda.Application.Atendimento;

public class VendaResumo
{
    public required Venda Venda { get; init; }
    public required int NumeroComanda { get; init; }
}

public interface IVendaRepository
{
    IReadOnlyList<VendaResumo> ListarPorData(DateTime inicioUtc, DateTime fimUtc);
    IReadOnlyList<ItemComanda>? BuscarItensDaVenda(int vendaId);
}
```

`VendaResumo` existe porque `Venda` sozinha não carrega o número da comanda
(só `ComandaId`) — a tela precisa exibir "Comanda 10", não o id interno.
`ListarPorData` faz um único `DbContext`, uma consulta com `join` entre
`venda` e `comanda` filtrando `finalizada_em >= inicioUtc && finalizada_em <
fimUtc` (intervalo meio-aberto — evita contar duas vezes uma venda exatamente
na meia-noite), ordenado por `finalizada_em`. Não carrega itens — é uma
listagem leve, uma linha por venda, para o dia inteiro.

`BuscarItensDaVenda(vendaId)`: busca a `Venda` pelo id, depois os
`ItemComanda` da `comanda_id` referenciada, ordenados por `Id` (mesmo padrão
de `BuscarComItens` em `EfComandaRepository`); devolve `null` se a venda não
existir. Carregado sob demanda quando o operador seleciona uma venda na
lista — mesmo padrão grade→detalhe que `AtendimentoView` já usa para
comandas.

Ambos os métodos são **somente leitura** — nenhum `SaveChanges()`, nenhuma
mutação. Isso não viola o padrão "um verbo, um DbContext, um SaveChanges" das
Etapas 2-4 porque esse padrão é sobre atomicidade de **escrita**; leitura pura
não precisa da mesma garantia.

## Casos de uso

- **ListarVendasPorData(DateTime dataLocal)** → `IReadOnlyList<VendaResumo>`.
  `dataLocal` é a meia-noite local do dia desejado (mesmo tipo/formato que
  `HistoricoViewModel.DataSelecionada`, evitando conversão de tipo entre as
  camadas). Calcula `inicioUtc`/`fimUtc` (meia-noite local até meia-noite
  local do dia seguinte, convertidas para UTC com o offset fixo `-03:00`),
  delega a `IVendaRepository.ListarPorData`. Sem validação de negócio — é uma
  consulta, não uma operação transacional; não retorna `Resultado<T>` (segue
  o padrão de `ListarCategoriasAtivas`/`PesquisarProdutos` da Etapa 2, que
  também são consultas simples sem `Resultado`).
- **BuscarItensDaVenda(int vendaId)** → `IReadOnlyList<ItemComanda>?`. Repassa
  direto ao repositório.

## Tela de Histórico (Desktop)

Layout: seletor de data no topo (padrão hoje) → linha de resumo logo abaixo
(quantidade de vendas, total do dia, ticket médio — todos recalculados
localmente a partir da lista já carregada, sem nova consulta) → campo de
busca por número de comanda → lista de vendas do dia (número da comanda,
total, horário) → painel de detalhe (itens/preço unitário/subtotal/total/
horário) que aparece ao selecionar uma venda da lista, reaproveitando
`CentavosParaMoedaConverter` para todo valor monetário.

Estado vazio (RN/UC07): sem vendas na data selecionada, mostra a mensagem
"Ainda não há vendas concluídas nesta data" (docs/07) e o resumo com
quantidade 0, total R$ 0,00, ticket médio R$ 0,00 (sem divisão por zero — a
ViewModel trata `quantidade == 0` como ticket médio zero, não uma exceção).

`HistoricoViewModel`: `[ObservableProperty] DataSelecionada` (`DateTime`, não
`DateOnly` — o `DatePicker` nativo do WPF bind a `DateTime?`, e usar o mesmo
tipo evita conversão na view; sempre truncado para meia-noite local ao ser
definido). Valor inicial calculado a partir de `IClock.UtcNow` convertido
para `America/Sao_Paulo` (mesmo offset fixo `-03:00` usado no caso de uso),
não `DateTime.Now` da máquina — mantém a mesma fonte de tempo já usada em
toda a aplicação. `Vendas` (`ObservableCollection<VendaResumo>`),
`TextoBuscaNumero`, `VendaSelecionada`, `ItensDaVendaSelecionada`
(`ObservableCollection<ItemComanda>`), propriedades computadas
`QuantidadeVendas`/`TotalDia`/`TicketMedio` (recalculadas quando `Vendas`
muda). Mudar `DataSelecionada` dispara `ListarVendasPorData` de novo;
digitar em `TextoBuscaNumero` filtra a `ObservableCollection` já carregada
(sem nova consulta); selecionar uma venda dispara `BuscarItensDaVenda`.

Novo botão "Histórico" no shell de navegação (`MainWindow.xaml`, ao lado de
"Atendimento"/"Produtos"), mesmo padrão de `MostrarAtendimento_Click`/
`MostrarProdutos_Click` — troca o conteúdo do `ContentControl` para
`HistoricoView`.

## Erros

| Situação | Mensagem | Onde |
| --- | --- | --- |
| Sem vendas na data | "Ainda não há vendas concluídas nesta data." | `HistoricoView`, resumo zerado |
| Falha ao carregar | mensagem genérica de erro | `HistoricoViewModel`, try/catch (mesmo padrão de `AtendimentoViewModel`/`ProdutosViewModel`) |

## Testes

- `Application.Tests`: `ListarVendasPorDataTests` — venda às 23h de Brasília
  aparece no dia local correto (não no dia UTC seguinte); venda à meia-noite
  local exata cai no dia certo (limite do intervalo meio-aberto); dia sem
  vendas retorna lista vazia. `BuscarItensDaVendaTests` — venda existente
  retorna os itens da comanda original; venda inexistente retorna `null`.
- `Infrastructure.Tests`: `EfVendaRepositoryTests` contra SQLite real —
  `ListarPorData` filtra corretamente pelo intervalo UTC e traz o número da
  comanda certo via join; `BuscarItensDaVenda` traz os itens certos.
- `Desktop.Tests`: `HistoricoViewModelTests` — resumo calculado corretamente
  (quantidade, total, ticket médio, incluindo ticket médio com centavos
  fracionários arredondados de forma consistente); dia sem vendas mostra
  estado vazio com resumo zerado, sem divisão por zero; busca por número
  filtra a lista carregada sem disparar nova consulta; selecionar uma venda
  popula `ItensDaVendaSelecionada`.

## Critérios de pronto

- `dotnet build`/`dotnet test` limpos na solução inteira;
- encerrar uma comanda (fluxo da Etapa 4), abrir Histórico no dia de hoje —
  a venda aparece na lista com o total e horário corretos;
- clicar na venda — o painel de detalhe mostra os itens/preços/subtotal
  batendo com o que foi lançado na comanda original;
- trocar a data para um dia sem vendas — estado vazio, resumo zerado, sem
  erro;
- buscar por um número de comanda que não está na lista do dia — lista fica
  vazia, sem nova consulta ao banco (verificável no log/teste, não
  necessariamente visualmente);
- botão "Histórico" acessível pelo shell de navegação junto de
  "Atendimento"/"Produtos".
