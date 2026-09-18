# Configurações (Etapa 7, parte 1) — RF25

Data: 2026-09-18
Escopo: RF25 — manter nome do estabelecimento, faixa válida de números de
comanda e pasta de backup externa padrão.

## Contexto

Base técnica (Etapa 0+1), catálogo, comandas, encerramento/venda, histórico e
backup/recuperação já estão em `main`. A entidade `Configuracao`
(`chave`/`valor`/`atualizado_em`) e seu mapeamento EF Core (tabela
`configuracao`) **já existem** desde a Etapa 0+1 — esta fatia não cria schema
novo, só a camada de Application/Infrastructure/Desktop sobre ela.

A Etapa 7 do guia de implementação ("Configuração e acabamento") junta várias
frentes independentes — RF25 (esta fatia), rotação de logs (RNF21),
navegação por teclado (RNF18), e publicação/instalador (RNF09/RNF17). Cada
uma vira sua própria fatia; esta cobre só RF25.

Fonte de verdade: [docs/docs/02-requisitos.md](../docs/docs/02-requisitos.md)
(RF25), [05-modelagem-dados.md](../docs/docs/05-modelagem-dados.md) (entidade
CONFIGURACAO), [07-experiencia-usuario.md](../docs/docs/07-experiencia-usuario.md)
(área "Configurações" — "Ajustar operação: Estabelecimento, comandas, backup
e retenção"), [13-pendencias-validacao.md](../docs/docs/13-pendencias-validacao.md)
(QV02 — resolvida por esta fatia, ver decisão abaixo).

Fora de escopo: retenção de logs configurável (RNF21, fatia própria),
navegação por teclado (RNF18, fatia própria), instalador/publicação
(RNF09/RNF17, fatia própria).

## Decisões confirmadas com o usuário

**Faixa de números de comanda (resolve QV02):** Configurações ganha um campo
"Quantidade de comandas" (um inteiro positivo, ex.: 30). `AbrirComanda`
passa a validar que o número digitado está entre 1 e essa quantidade —
**mas só quando ela estiver configurada**. Enquanto ninguém configurar (app
recém-instalado), `AbrirComanda` mantém o comportamento atual (`numero > 0`,
sem teto) — não trava o uso antes de alguém visitar a tela de Configurações.
A grade de comandas (Etapa 3) continua dinâmica, mostrando só as abertas —
essa fatia muda apenas a validação de abertura, não a tela em si.

**Pasta de backup externa como destino padrão:** Configurações ganha um
campo "Pasta de backup externa" (opcional, escolhido via
`Microsoft.Win32.OpenFolderDialog`, o mesmo diálogo já usado na Etapa 6).
Quando configurada, o backup automático do **encerramento do app**
(`CriarBackupAutomatico.Executar(incondicional: true)`) passa a tentar
também uma cópia lá, além da pasta gerenciada — isso cumpre literalmente a
política do docs/08 ("Cópia externa | Pendrive ou pasta sincronizada ao fim
do dia"), que hoje só acontecia por clique manual na tela de Backup (Etapa
6). O backup da **primeira abertura do dia** continua só na pasta
gerenciada — a cópia externa é especificamente "ao fim do dia". O botão
"Escolher pasta externa e criar backup..." da tela de Backup (Etapa 6)
continua existindo sem mudanças, para uma cópia pontual em pasta diferente
da configurada.

## Contratos

```csharp
namespace VarthexComanda.Application.Configuracao;

public class ConfiguracaoEstabelecimento
{
    public required string NomeEstabelecimento { get; init; }
    public int? QuantidadeMaximaComandas { get; init; }
    public string? PastaBackupExterna { get; init; }
}

public interface IConfiguracaoRepository
{
    string? ObterValor(string chave);
    void Definir(string chave, string valor, DateTime atualizadoEm);
}
```

Três chaves usadas nesta fatia: `estabelecimento.nome`,
`comandas.quantidade_maxima` (armazenada como string numérica),
`backup.pasta_externa`. `IConfiguracaoRepository` é genérico
(chave/valor cru) — a tradução para `ConfiguracaoEstabelecimento` (parsing,
valores ausentes) acontece nos casos de uso, não no repositório.

## Casos de uso

- **ObterConfiguracao()** → `ConfiguracaoEstabelecimento`. Lê as três chaves;
  `NomeEstabelecimento` vira `string.Empty` se ausente (nunca configurado);
  `QuantidadeMaximaComandas`/`PastaBackupExterna` viram `null` se ausentes
  ou, no caso da quantidade, se o valor guardado não for um inteiro válido
  (defensivo, não deveria acontecer já que `SalvarConfiguracao` valida antes
  de gravar). Sem `Resultado<T>` — é uma consulta simples, mesmo padrão de
  `ListarCategoriasAtivas`.
- **SalvarConfiguracao(ConfiguracaoEstabelecimento)** → `Resultado<ConfiguracaoEstabelecimento>`.
  Valida `NomeEstabelecimento` não-vazio (`Resultado.Falha("Informe o nome
  do estabelecimento.")`) e, se `QuantidadeMaximaComandas` não for nulo,
  que seja `> 0` (`Resultado.Falha("A quantidade de comandas deve ser maior
  que zero.")`). Grava as três chaves via `IConfiguracaoRepository.Definir`
  (usando `IClock.UtcNow` para `atualizado_em`); se `PastaBackupExterna`
  for nulo/vazio, grava uma string vazia (não deixa a chave ausente, para
  distinguir "nunca configurado" de "configurado e depois limpo" —
  irrelevante para a leitura atual, mas evita ambiguidade futura).

## Pontos de consumo em código já mesclado

**`AbrirComanda` (Etapa 3, `VarthexComanda.Application.Atendimento`):**
ganha uma dependência de `ObterConfiguracao` (o caso de uso, não o
repositório direto — mantém a mesma camada). Adiciona, depois da checagem
`numero <= 0` já existente:

```csharp
var configuracao = _obterConfiguracao.Executar();
if (configuracao.QuantidadeMaximaComandas is int maximo && numero > maximo)
{
    return Resultado<Comanda>.Falha($"O número da comanda deve ser no máximo {maximo}.");
}
```

**`CriarBackupAutomatico` (Etapa 6, `VarthexComanda.Application.Backup`):**
ganha a mesma dependência de `ObterConfiguracao`. No ramo `incondicional`
(depois de `_backupService.CriarBackupGerenciado()`), se
`configuracao.PastaBackupExterna` não for nulo/vazio, tenta também
`_backupService.CriarBackupExterno(pastaExterna)` — envolto em try/catch
próprio, sem deixar uma falha na cópia externa mascarar ou interromper o
fluxo (mesmo padrão de tolerância a falha já usado em `CriarBackupManual`).

## Tela de Configurações (Desktop)

Nova aba "Configurações" no shell de navegação, ao lado de
Atendimento/Produtos/Histórico/Backup:

- Campo "Nome do estabelecimento" (`TextBox`).
- Campo "Quantidade de comandas" (`TextBox` numérico, mesmo padrão de
  parsing já usado em `AtendimentoViewModel.NovoNumero`).
- Campo "Pasta de backup externa" (`TextBox` somente leitura mostrando o
  caminho atual) + botão "Escolher..." (`OpenFolderDialog`, mesmo diálogo da
  Etapa 6) + botão "Limpar" (zera o campo).
- Botão "Salvar".
- Mensagem de erro/sucesso.

Sem confirmação via `IConfirmador` — alterar configuração não é uma ação
destrutiva (RNF08 não se aplica aqui).

## Erros

| Situação | Mensagem | Onde |
| --- | --- | --- |
| Nome vazio ao salvar | "Informe o nome do estabelecimento." | `SalvarConfiguracao` |
| Quantidade de comandas ≤ 0 | "A quantidade de comandas deve ser maior que zero." | `SalvarConfiguracao` |
| Abrir comanda acima da quantidade configurada | "O número da comanda deve ser no máximo {N}." | `AbrirComanda` |
| Falha ao salvar configuração | mensagem genérica de erro | `ConfiguracaoViewModel`, try/catch |

## Testes

- `Infrastructure.Tests`: `EfConfiguracaoRepositoryTests` — `Definir` grava e
  `ObterValor` lê de volta (upsert: chamar `Definir` duas vezes na mesma
  chave atualiza o valor, não duplica linha); `ObterValor` de uma chave
  inexistente retorna `null`.
- `Application.Tests`: `ObterConfiguracaoTests` (nada configurado → nome
  vazio, quantidade/pasta nulos; tudo configurado → todos os campos
  corretos; quantidade com valor não-numérico gravado diretamente no
  repositório → tratado como nulo, não lança exceção),
  `SalvarConfiguracaoTests` (nome vazio falha; quantidade zero/negativa
  falha; sucesso grava as três chaves e devolve o objeto salvo),
  `AbrirComandaTests` (novo teste: sem configuração, número alto passa;
  com configuração, número acima do limite falha; número dentro do limite
  passa), `CriarBackupAutomaticoTests` (novo teste: incondicional com pasta
  externa configurada tenta `CriarBackupExterno`; incondicional sem pasta
  configurada não tenta; abertura do dia nunca tenta, mesmo com pasta
  configurada).
- `Desktop.Tests`: `ConfiguracaoViewModelTests` — carrega configuração atual
  ao abrir; salvar com sucesso atualiza mensagem; salvar com nome vazio
  mostra erro sem chamar o caso de uso de salvar com dados inválidos (a
  validação do caso de uso já cobre isso — o teste confirma que a
  ViewModel não engole o erro).

## Critérios de pronto

- `dotnet build`/`dotnet test` limpos na solução inteira;
- configurar nome, quantidade de comandas e pasta de backup, salvar, fechar
  e reabrir o app — os três valores continuam lá;
- tentar abrir uma comanda com número acima da quantidade configurada —
  recusado com mensagem clara;
- sem nunca ter configurado nada, abrir uma comanda com qualquer número
  positivo continua funcionando (comportamento anterior preservado);
- configurar uma pasta de backup externa, fechar o app — aparecem dois
  backups (gerenciado + externo) ao reabrir a tela de Backup.
