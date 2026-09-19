# Redesign da tela de Atendimento

Data: 2026-09-18
Escopo: redesenhar a tela "Atendimento" (grade de comandas + edição de
itens) do zero visualmente, mantendo toda a lógica de negócio já existente
e testada.

## Contexto

A tela de Atendimento atual (Etapa 3) usa uma lista dinâmica só com
comandas já abertas, um campo de texto pra digitar o número de uma nova
comanda, e um painel de edição com dropdown de categoria/produto + campo
de quantidade numérico. Funciona, mas não tem "cara de sistema de PDV" —
o design foi refeito do zero num processo de wireframe/mockup iterativo
(ASCII tipado → canvas de design em camadas → ajustes de fluxo), aprovado
passo a passo pelo usuário. Este documento formaliza as decisões técnicas
pra implementar o resultado aprovado sem regressão na lógica já existente.

Fonte de verdade de UX: as mensagens da conversa que aprovaram o wireframe
e as iterações de design (grade fixa numerada, tela de menu+carrinho,
paleta neutra "de sistema", fluxo Voltar vs. Finalizar). Este spec traduz
essas decisões visuais em contratos técnicos.

## Descoberta importante: quase tudo já existe

Toda a lógica de negócio necessária já está implementada e testada em
`AtendimentoViewModel` (Etapa 3) e no diálogo de Encerramento (Etapa 4).
Esta fatia é, na prática, uma reconstrução de View (XAML) + uma peça nova
pequena no ViewModel (a grade de slots configurável) — não lógica de
domínio nova.

Mapeamento tela-nova → código já existente (reaproveitado sem mudança de
assinatura):

| Elemento da tela nova | Código já existente reaproveitado |
| --- | --- |
| Clicar num slot livre → abre a comanda | `AbrirComanda` (via `AbrirCommand`/lógica interna já existente) |
| Clicar num slot aberto → entra na comanda | `SelecionarComandaCommand` |
| Abas de categoria no rodapé do menu | `Categorias` (de `ListarCategoriasAtivas`), `CategoriaCatalogo` |
| Grade de produtos (cards quadrados) | `ProdutosCatalogo` (de `PesquisarProdutos`) |
| Clicar no card do produto → adiciona 1 unidade | `AdicionarProdutoAoItemCommand` |
| Tabela do carrinho | `Itens` (`ObservableCollection<ItemComanda>`) |
| Stepper "−" / "+" na linha do item | `DiminuirQuantidadeCommand` / `AumentarQuantidadeCommand` |
| Ícone de lixeira na linha do item | `RemoverCommand` |
| Total da comanda | `ComandaAtual.TotalCentavos` |
| Botão "Voltar" | `FecharEdicaoCommand` |
| Botão "Cancelar comanda" | `CancelarComandaAtualCommand` |
| Botão "Finalizar comanda (F4)" | `VerTotalCommand` (já abre `IEncerramentoDialog`, que já mostra total + confirmação de cobrança) — só troca o rótulo/ícone e ganha o atalho de teclado F4, que é novo |

O que É novo: a grade fixa de N slots numerados (hoje a grade só lista
comandas abertas) e o layout visual inteiro (XAML).

## Decisões técnicas

**Tamanho da grade:** vem de `ObterConfiguracao().QuantidadeMaximaComandas`
(já existe, RF25). Se nunca configurado (`null`), usa **20** como padrão —
a grade nunca fica vazia/sem sentido numa instalação nova. Recalculado
toda vez que a tela de Atendimento é reaberta (mesmo padrão de
`AtualizarCategorias`/`AtualizarVendas`/`AtualizarLista` já usado nas
outras telas ao trocar de aba).

**Slots da grade — novo tipo, só na camada Desktop:**

```csharp
namespace VarthexComanda.Desktop.Atendimento;

public class ComandaSlotItem
{
    public required int Numero { get; init; }
    public required bool Aberta { get; init; }
    public int? ComandaId { get; init; }
    public long? TotalCentavos { get; init; }
    public DateTime? AbertaEmUtc { get; init; }
}
```

Não é uma entidade nova de domínio nem precisa de tabela — é só a grade de
1..N cruzada com `ComandasAbertas` (já carregada), recalculada em memória
sempre que `ComandasAbertas` muda. `AtendimentoViewModel` ganha uma
`ObservableCollection<ComandaSlotItem> Slots` e um método privado
`AtualizarSlots()` chamado no fim de `AtualizarComandasAbertas()`.

**Duas simplificações (aprovadas) em relação ao mockup visual:**

1. O card compacto de cada slot mostra só o **total** (`TotalCentavos`,
   já existe em `Comanda` — nenhuma consulta extra necessária). A
   "quantidade de itens" que aparecia no mockup foi removida do card
   compacto — exigiria uma consulta adicional por comanda (N+1) só pra
   um dado que já fica visível assim que a comanda é aberta (na tabela de
   itens). Fora de escopo desta fatia.
2. O "tempo" mostrado no card é **"aberta há X"**, calculado a partir de
   `Comanda.AbertaEm` (via `FusoBrasilia`, já existe) — não "última
   modificação", porque não existe timestamp de última modificação na
   comanda hoje (só em `ItemComanda.AtualizadoEm`, e buscar isso pra
   todas as comandas abertas de uma vez seria uma consulta cara). Fora de
   escopo desta fatia.

**Fotos dos produtos:** `Produto` não tem campo de imagem hoje. Os cards
do menu usam um **ícone placeholder genérico** (o mesmo pra todo
produto) — nenhuma mudança de schema ou upload de imagem nesta fatia.
Fica para uma fatia futura (fora de escopo).

**Atalho de teclado F4:** o botão "Finalizar comanda" ganha um
`KeyBinding` real (`Key.F4` → `VerTotalCommand`) na `AtendimentoView`,
só quando a comanda estiver em edição (o binding fica dentro do
`UserControl` da tela, não é um handler global de janela). Isso é o
único item de acessibilidade/teclado desta fatia — não amplia pra RNF18
completo (que continua uma fatia própria e futura).

**Categoria padrão selecionada:** ao entrar numa comanda, a primeira
categoria ativa (`Categorias.FirstOrDefault()`) é selecionada
automaticamente em `CategoriaCatalogo` — sem aba "Todas as categorias"
(o mockup aprovado não tinha essa aba).

## Layout visual aprovado (referência, já validada com o usuário)

**Tela principal — grade de N slots** (N = `TamanhoGrade`, ex. 20):

```
┌────────────────────────────────────────────────────────────────┐
│ Atendimento | Produtos | Histórico | Backup | Configurações     │
├────────────────────────────────────────────────────────────────┤
│ COMANDAS                                                        │
│ ┌────────┐┌────────┐┌────────┐┌────────┐┌────────┐             │
│ │COMANDA ││COMANDA ││COMANDA ││COMANDA ││COMANDA │             │
│ │12 min  ││        ││3 min   ││        ││agora   │  ← aberta:  │
│ │  01    ││  02    ││  03    ││  04    ││  05    │    fundo    │
│ │3 it.·  ││ LIVRE  ││1 it.·  ││ LIVRE  ││2 it.·  │    âmbar    │
│ │R$28,00 ││        ││R$10,00 ││        ││R$15,00 │  ← livre:   │
│ └────────┘└────────┘└────────┘└────────┘└────────┘    branco   │
│                  ... (repete até N) ...                         │
└────────────────────────────────────────────────────────────────┘
```

**Tela de comanda — menu (esquerda) + carrinho em tabela (direita)**:

```
┌────────────────────────────────────────────────────────────────┐
│ Atendimento | Produtos | Histórico | Backup | Configurações     │
├───────────────────────────────┬──────────────────────────────────┤
│ MENU                          │ COMANDA #07                       │
│ ┌────┐┌────┐┌────┐            │ ┌──────────────────────────────┐ │
│ │[img]││[img]││[img]│          │ │DESCRIÇÃO │QTD.│PREÇO│TOTAL│  │ │
│ │Nome+││Nome+││Nome+│          │ ├──────────────────────────────┤ │
│ │Preço││Preço││Preço│          │ │X-Burguer │ 1  │18,00│18,00│🗑│ │
│ └────┘└────┘└────┘            │ │Coca-Cola │ 2  │5,00 │10,00│🗑│ │
│ ┌──────┬──────┬──────┬──────┐  │ └──────────────────────────────┘ │
│ │Lanches│Refri.│Sobre.│Outros│  │ TOTAL DA COMANDA      R$ 28,00   │
│ └──────┴──────┴──────┴──────┘  │ [← Voltar][✕ Cancelar]  [✓Finalizar (F4)]│
└───────────────────────────────┴──────────────────────────────────┘
```

Paleta: fundo neutro claro, fonte de sistema (Segoe UI), bordas cinzas
sólidas em tudo, cantos retos (sem `border-radius`). Cores só por função:
verde (`Finalizar`), vermelho (`Cancelar`/excluir), âmbar sólido
(comanda ocupada na grade).

## Contratos — mudanças em `AtendimentoViewModel`

```csharp
// Construtor ganha 1 parâmetro novo (mesmo padrão da Etapa 7):
public AtendimentoViewModel(
    IComandaRepository comandas,
    IClock relogio,
    AbrirComanda abrirComanda,
    AdicionarItem adicionarItem,
    AlterarQuantidade alterarQuantidade,
    RemoverItem removerItem,
    CancelarComanda cancelarComanda,
    ListarCategoriasAtivas listarCategorias,
    PesquisarProdutos pesquisarProdutos,
    IConfirmador confirmador,
    IEncerramentoDialog encerramentoDialog,
    ObterConfiguracao obterConfiguracao)  // NOVO
```

Novas propriedades/membros:
- `ObservableCollection<ComandaSlotItem> Slots { get; }`
- `int TamanhoGrade` (privado, calculado no construtor e sempre que
  `AtualizarComandasAbertas()` roda — reler a configuração a cada
  atualização, não só uma vez no construtor, pra refletir uma mudança
  feita em Configurações sem exigir reiniciar o app, mesmo padrão que
  `AbrirComanda`/`CriarBackupAutomatico` já usam com `ObterConfiguracao`)
- `AtualizarSlots()` (privado): reconstrói `Slots` a partir de 1..N e
  `ComandasAbertas`
- `AbrirOuSelecionarSlot(ComandaSlotItem slot)` (novo command, chamado
  pelo clique no card da grade): se `slot.Aberta`, delega pro
  `SelecionarComanda` já existente; senão, chama `Abrir()` já existente
  passando `slot.Numero` (adaptação: `Abrir()` hoje lê de `NovoNumero`
  como string — este novo command monta esse valor internamente e
  invoca a mesma lógica, sem duplicar a validação/tratamento de erro já
  existente)

Nenhuma mudança de assinatura nos use cases (`AdicionarItem`,
`AlterarQuantidade`, `RemoverItem`, `CancelarComanda`, `EncerrarComanda`,
`AbrirComanda`) — todos reaproveitados como estão.

## Testes que quebram e precisam de atualização

`AtendimentoViewModelTests.cs`'s helper `CriarViewModel` constrói
`AtendimentoViewModel` com todos os 11 parâmetros atuais — vai precisar
do 12º (`ObterConfiguracao`), igual ao padrão já usado 8 vezes na Etapa 7
(passar `new ObterConfiguracao(new FakeConfiguracaoRepository())`).

## Testes novos

- `AtualizarSlots`: sem configuração → 20 slots, todos livres exceto os
  que têm comanda aberta correspondente; com `QuantidadeMaximaComandas`
  configurado → grade do tamanho configurado; comanda aberta com número
  fora do tamanho atual da grade (ex. configurado depois de já ter
  comandas abertas com número maior) não quebra a tela — só não aparece
  na grade (edge case a documentar, não a impedir).
- `AbrirOuSelecionarSlot`: slot livre → chama fluxo de abrir; slot aberto
  → chama fluxo de selecionar; ambos populam `ComandaAtual`/`Itens`
  corretamente.
- Testes de ViewModel de Desktop para os cálculos de exibição do slot
  (total formatado, "aberta há X" via `FusoBrasilia`).

## Fora de escopo (documentado, não implementado nesta fatia)

- Campo de foto no cadastro de produtos / upload de imagem.
- Contagem de itens no card compacto da grade.
- Timestamp de última modificação da comanda (só data de abertura).
- Aba "Todas as categorias" no filtro do menu.
- RNF18 completo (navegação por teclado em todas as telas) — só o
  atalho F4 desta tela.

## Critérios de pronto

- `dotnet build`/`dotnet test` limpos na solução inteira;
- grade mostra o número configurado de slots (ou 20, sem configuração);
- clicar num slot livre abre a comanda nesse número; clicar num aberto
  entra na edição;
- adicionar/remover/alterar quantidade de item funciona exatamente como
  hoje (mesma lógica, nova tela);
- "Voltar" mantém a comanda aberta e volta pra grade; "Cancelar comanda"
  cancela (com confirmação, como hoje); "Finalizar comanda"/F4 abre a
  tela de Encerramento existente;
- nenhuma regressão nos testes já existentes de `AtendimentoViewModel`
  (fora da atualização mecânica de construtor).
