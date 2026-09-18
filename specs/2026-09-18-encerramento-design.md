# Encerramento e Venda (Etapa 4)

Data: 2026-09-18
Escopo: RF14-17, RN09/RN13-15, UC05 — revalidar total, exibir tela de
encerramento, confirmar cobrança externa e gravar venda + fechamento da
comanda em uma única transação.

## Contexto

Base técnica (Etapa 0+1), catálogo (Etapa 2) e comandas (Etapa 3) já estão em
`main`. As entidades `Venda`/`StatusVenda` e seu mapeamento EF Core (schema,
CHECK constraints, `UNIQUE(comanda_id)`, `UNIQUE(numero)`) **já existem** desde
a Etapa 0+1 — esta fatia não cria schema novo, só a camada de Application,
Infrastructure (repositório) e UI em cima do que já existe.

Fonte de verdade: [docs/docs/02-requisitos.md](../docs/docs/02-requisitos.md)
(RF14-17), [03-regras-negocio.md](../docs/docs/03-regras-negocio.md)
(RN09, RN13-15, RN20), [04-casos-de-uso.md](../docs/docs/04-casos-de-uso.md)
(UC05), [05-modelagem-dados.md](../docs/docs/05-modelagem-dados.md) (entidade
VENDA), [07-experiencia-usuario.md](../docs/docs/07-experiencia-usuario.md)
("Tela de encerramento"), [14-guia-implementacao.md](../docs/docs/14-guia-implementacao.md)
(Etapa 4), e os testes de [09-testes-aceitacao.md](../docs/docs/09-testes-aceitacao.md)
(CT07-11).

Fora de escopo: histórico de vendas e filtro por data (RF18-20, Etapa 5),
qualquer forma de pagamento, cálculo de troco, chamada a adquirente,
armazenamento de token/NSU/cartão (proibições explícitas do guia de
implementação), fuso horário local para exibição (nada nesta fatia mostra
data/hora — fica para a Etapa 5).

## Decisões confirmadas com o usuário

**Repositório:** `IComandaRepository` ganha um oitavo método verbo,
`EncerrarComanda(int comandaId, DateTime agora) → Venda`, seguindo o mesmo
padrão das Etapas 2-3 (um `DbContext`, um `SaveChanges()`, atômico por
construção) em vez de introduzir uma abstração de Unit of Work nova ou um
`IVendaRepository` separado. O repositório de Comanda passa a conhecer a
entidade `Venda` — custo aceito.

**Numeração da venda:** `venda.numero` é "sequencial interno único" (não
precisa bater com o número da comanda, RN visão-e-decisões/modelagem). Dentro
do mesmo `DbContext` de `EncerrarComanda`, calcula `MAX(numero) + 1` (ou `1`
se não houver nenhuma venda ainda). Sem tabela de contador dedicada — o app
roda em instância única (mutex já existente) e a UI é single-threaded, então
o risco de corrida é baixo; a `UNIQUE(numero)` já existente no schema é a rede
de segurança.

**Fuso horário:** `venda.finalizada_em` grava `IClock.UtcNow`, igual a todas
as datas já gravadas por `comanda`/`item_comanda`. Nenhuma mudança em
`IClock` ou no código já mesclado de fatias anteriores. RN20 (relógio local
na exibição) fica para quando a Etapa 5 exibir datas pela primeira vez —
nada nesta fatia mostra data/hora na tela.

**Navegação:** a "Tela de encerramento" (UC05) é uma `Window` modal com sua
própria `EncerramentoViewModel`, aberta via `ShowDialog` a partir de um novo
comando `VerTotal` em `AtendimentoViewModel` — não um terceiro estado dentro
da já numerosa `AtendimentoView`/`AtendimentoViewModel`. Fechar sem confirmar
não altera nada; confirmar executa `EncerrarComanda` e fecha retornando à
grade atualizada.

## Contratos

```csharp
namespace VarthexComanda.Application.Atendimento;

public interface IComandaRepository
{
    // ... 7 métodos já existentes (Etapa 3) ...
    Venda EncerrarComanda(int comandaId, DateTime agora);
}
```

`EncerrarComanda` no repositório: abre um `DbContext`, relê a `Comanda` e seus
`ItemComanda` (mesma checagem de existência e de `Status == Aberta` que os
outros 5 métodos mutantes já fazem, lançando `ComandaNaoAbertaException`
senão), calcula `numero = (contexto.Vendas.Max(v => (int?)v.Numero) ?? 0) + 1`,
cria a `Venda` com `TotalCentavos = comanda.TotalCentavos` (já correto, soma
persistida dos itens — RN08), `FinalizadaEm = agora`, `Status = Concluida`,
muda `comanda.Status = Fechada` e `comanda.FechadaEm = agora`, um único
`SaveChanges()`. O número da comanda libera sozinho — o índice único parcial
`uq_comanda_numero_aberta` só cobre `status = 'ABERTA'`, nada extra a fazer.

## Caso de uso

- **EncerrarComanda(comandaId)** → `Resultado<Venda>`. Antes de chamar o
  repositório: busca `ComandaComItens` via `BuscarComItens` (já existe) para
  revalidar o estado persistido (RF14 "revalidar itens, subtotais e total" —
  não recalcula preços, RN05 já os travou no lançamento; só relê o que está
  gravado, não confia em cache da UI); falha com
  `Resultado.Falha("Adicione um item antes de encerrar.")` se `Itens.Count == 0`
  (RN09); senão delega ao repositório. `ComandaNaoAbertaException` do
  repositório (comanda já fechada/cancelada, ex. clique duplo) vira
  `Resultado.Falha("A comanda não está aberta.")`, igual ao padrão dos outros
  5 casos de uso.

## Tela de encerramento (Desktop)

Ordem visual obrigatória (docs/07): número da comanda → lista de produtos com
quantidade/preço unitário/subtotal (reaproveita `CentavosParaMoedaConverter`
da Etapa 3) → total em destaque → texto "Digite este valor na maquininha" →
confirmação "A cobrança foi aprovada fora do sistema?" (um `CheckBox`/toggle,
não uma pergunta pop-up — a confirmação É a tela, não uma interrupção sobre
ela) → botão principal "Confirmar e encerrar" (habilitado só quando o toggle
está marcado) → botão secundário "Voltar para a comanda". Sem botões de
forma de pagamento (proibido).

`EncerramentoViewModel`: carrega itens via `BuscarComItens` ao abrir; expõe
`Itens`, `Total`, `CobrancaAprovada` (bool), `Mensagem`; comando
`ConfirmarEncerrar` (só executa se `CobrancaAprovada`) chama o caso de uso,
em `try/catch` (mesmo padrão de `AtendimentoViewModel`/`ProdutosViewModel`)
— sucesso fecha o modal com `DialogResult = true`; falha mostra `Mensagem` e
mantém o modal aberto (comanda continua aberta, nada foi commitado); comando
`Voltar` fecha com `DialogResult = false` sem chamar nada.

`AtendimentoViewModel` não pode instanciar `EncerramentoView`/`Window`
diretamente sem reintroduzir um tipo WPF nela (ela hoje não tem nenhum
`using System.Windows`, propriedade que a Etapa 3 estabeleceu e a revisão
final validou). Mesma solução já usada para `IConfirmador`: uma interface
pequena em Desktop,

```csharp
public interface IEncerramentoDialog
{
    bool Abrir(int comandaId);
}
```

implementada em Desktop resolvendo `EncerramentoViewModel`+`EncerramentoView`
via o container de DI, chamando `ShowDialog()` e devolvendo
`DialogResult == true`. `AtendimentoViewModel` ganha um novo comando
`VerTotal`, habilitado só quando `Itens.Count > 0`, que chama
`_encerramentoDialog.Abrir(ComandaAtual.Id)`; se retornar `true`, chama
`FecharEdicao()` (a comanda some da tela, já que não está mais aberta) e
`AtualizarComandasAbertas()` (some da grade).

## Erros

| Situação | Mensagem | Onde |
| --- | --- | --- |
| Comanda vazia | "Adicione um item antes de encerrar." | Botão "Ver total" desabilitado na origem; caso de uso recusa mesmo assim se chamado |
| Cobrança não marcada | botão "Confirmar e encerrar" desabilitado | `EncerramentoViewModel` |
| Falha ao encerrar | "A venda não foi registrada e a comanda continua aberta." | `EncerramentoViewModel`, try/catch |
| Comanda já fechada/cancelada (corrida rara) | "A comanda não está aberta." | Caso de uso, via `ComandaNaoAbertaException` |

## Testes

- `Application.Tests`: `EncerrarComandaTests` — sucesso grava venda com
  número/total/status corretos e fecha a comanda; comanda vazia recusa sem
  chamar o repositório; comanda não-aberta propaga a falha traduzida.
- `Infrastructure.Tests`: `EfComandaRepositoryTests` — `EncerrarComanda` grava
  venda e fecha comanda no mesmo `SaveChanges` (total da venda bate com o
  total da comanda); duas vendas seguidas recebem números sequenciais
  (1, depois 2); RN10 guard (comanda já fechada lança
  `ComandaNaoAbertaException`); número da comanda reaparece livre na listagem
  de comandas abertas após o encerramento.
- `Desktop.Tests`: `EncerramentoViewModelTests` — carrega itens/total ao
  construir; `ConfirmarEncerrar` sem `CobrancaAprovada` não chama o caso de
  uso; sucesso seta `DialogResult`; falha mantém o modal aberto com
  `Mensagem` preenchida. `AtendimentoViewModelTests` — novos testes com um
  `FakeEncerramentoDialog` (mesmo estilo de `FakeConfirmador`, Etapa 3): `VerTotal`
  desabilitado com `Itens.Count == 0`; `VerTotal` chama o diálogo e, quando ele
  retorna `true`, a comanda some de `ComandasAbertas` e a edição fecha; quando
  retorna `false`, nada muda.

## Critérios de pronto

- `dotnet build`/`dotnet test` limpos na solução inteira;
- abrir comanda, lançar itens, clicar "Ver total", marcar a confirmação e
  "Confirmar e encerrar" — comanda some da grade, venda gravada com total
  correto, número da comanda livre para reabrir;
- tentar encerrar comanda vazia — botão desabilitado;
- "Voltar para a comanda" não altera nada (comanda continua aberta, total
  intacto);
- duas comandas encerradas em sequência recebem `venda.numero` 1 e 2.
