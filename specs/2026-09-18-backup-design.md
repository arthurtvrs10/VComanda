# Backup e Recuperação (Etapa 6)

Data: 2026-09-18
Escopo: RF21-24, RN19, UC08/UC09 — backup automático (na primeira abertura do
dia e ao encerrar o app), backup manual (pasta local ou externa), validação
de backup (formato/versão/integridade/checksum) e restauração (com cópia
preventiva e reinício).

## Contexto

Base técnica (Etapa 0+1), catálogo (Etapa 2), comandas (Etapa 3),
encerramento/venda (Etapa 4) e histórico (Etapa 5) já estão em `main`. A
entidade `BackupRegistro`/`StatusBackup` e seu mapeamento EF Core (tabela
`backup_registro`) **já existem** desde a Etapa 0+1 — esta fatia não cria
schema novo. Também já existe `DatabaseBackupService.BackupIfExists`, um
`File.Copy` ingênuo chamado apenas quando há migração pendente no startup —
esta fatia **substitui** esse serviço por um mecanismo real, e adiciona tudo
que falta: gatilhos automáticos corretos, backup manual, validação e
restauração.

Fonte de verdade: [docs/docs/02-requisitos.md](../docs/docs/02-requisitos.md)
(RF21-24), [03-regras-negocio.md](../docs/docs/03-regras-negocio.md) (RN19),
[04-casos-de-uso.md](../docs/docs/04-casos-de-uso.md) (UC08/UC09),
[08-seguranca-backup.md](../docs/docs/08-seguranca-backup.md) (política de
backup, processo de backup e de restauração — fonte normativa desta fatia),
[14-guia-implementacao.md](../docs/docs/14-guia-implementacao.md) (Etapa 6).

Fora de escopo: tela completa de "Configurações" (estabelecimento, retenção
configurável, RF25 — Etapa 7); rotação/retenção de logs (RF26 — Etapa 7);
recuperação de comandas abertas após término inesperado (RF27 — já
estruturalmente coberta, já que `ListarAbertas()` sempre reflete o estado
persistido; Etapa 7 apenas formaliza/testa isso); cópia externa automática
agendada (a política menciona pendrive/pasta sincronizada "ao fim do dia" —
esta fatia entrega o backup manual para pasta externa via UI, não um
agendador).

## Decisões confirmadas com o usuário

**UI mínima nesta fatia:** uma aba "Backup" simples no shell de navegação
(criar backup agora, listar backups recentes, restaurar) — não a tela
"Configurações" completa da Etapa 7. A Etapa 7 reaproveita os mesmos casos de
uso, só adicionando campos de estabelecimento/retenção configurável.

**Backup ao encerrar o app:** incondicional (sem rastrear "houve mudança"),
para não tocar `EfComandaRepository` (código já revisado por três fatias
anteriores) só para adicionar um marcador de escrita. Pior caso: um backup a
mais em dias sem uso — a retenção de 30 cópias absorve isso sem problema.

**Reinício após restauração:** reinício real do processo — `Process.Start`
apontando para o próprio executável, seguido de `Environment.Exit` no
processo atual. O novo processo sobe do zero e passa pela checagem de
integridade que `App.xaml.cs` já faz no startup (migração + `PRAGMA
integrity_check`) — reaproveitando essa lógica em vez de duplicá-la.

## Mecanismo de backup (Infrastructure)

Substituir `DatabaseBackupService.BackupIfExists` por um `BackupService` que:

1. Cria o snapshot via `SqliteConnection.BackupDatabase` (API de backup
   nativa do SQLite, via `Microsoft.Data.Sqlite`) — não `File.Copy`. Isso
   produz uma cópia consistente mesmo com o banco ativo aberto por outra
   conexão, ao contrário de uma cópia de arquivo bruta.
2. Grava em arquivo temporário no destino, depois renomeia para o nome
   definitivo `varthex-comanda-AAAA-MM-DD-HHMMSS.db` (mesmo padrão de nome já
   usado, ajustado ao formato da política).
3. Calcula um checksum SHA-256 do arquivo final e grava um arquivo
   companheiro `<nome>.db.sha256` (conteúdo: só o hash em hex) ao lado do
   backup. Isso faz o checksum viajar com o arquivo — funciona tanto para
   backups criados por este app quanto para um arquivo escolhido de fora
   (pendrive, backup de outra instalação) que não tem registro na tabela
   `backup_registro` local.
4. Roda `PRAGMA integrity_check` no arquivo recém-criado.
5. Registra o resultado (sucesso ou falha, com mensagem) via
   `IBackupRegistroRepository`.
6. Aplica retenção — mantém as 30 cópias mais recentes, **apenas** na pasta
   gerenciada (`AppPaths.BackupsDirectory`). Nunca poda uma pasta externa que
   o usuário escolheu para um backup manual — não é a pasta do app para
   gerenciar, e apagar arquivos ali seria uma surpresa destrutiva.

Falha em qualquer etapa é registrada (`StatusBackup.Falha` +
`Mensagem`) e não impede o atendimento (RF21/UC08 "Falha preserva backups
anteriores e não bloqueia o atendimento") — nunca lança exceção para fora do
método de backup automático; o botão de backup manual mostra a falha na UI
mas não trava a tela.

## Contratos

```csharp
namespace VarthexComanda.Application.Backup; // domínio próprio — backup não é atendimento nem catálogo

public interface IBackupRegistroRepository
{
    void Registrar(BackupRegistro registro);
    IReadOnlyList<BackupRegistro> ListarRecentes(int quantidade);
    bool ExisteBackupHoje(DateTime inicioUtc, DateTime fimUtc);
}
```

Mesma lógica para o restante da camada Application desta fatia
(`CriarBackupAutomatico`, `CriarBackupManual`, `ValidarBackup`,
`RestaurarBackup`, `RelatorioValidacao`): todos em
`VarthexComanda.Application.Backup`, pasta
`backend/src/VarthexComanda.Application/Backup/`. Na Infrastructure, o novo
`BackupService`/`EfBackupRegistroRepository` ficam em
`VarthexComanda.Infrastructure.Backup`, pasta
`backend/src/VarthexComanda.Infrastructure/Backup/`. Na Desktop,
`BackupViewModel`/`BackupView` ficam em `VarthexComanda.Desktop.Backup`,
pasta `backend/src/VarthexComanda.Desktop/Backup/` — mesmo padrão de
pasta-por-domínio já usado por `Catalogo`/`Atendimento`.

`ExisteBackupHoje` recebe o intervalo UTC do dia local (mesmo padrão
`FusoBrasilia` já estabelecido na Etapa 5) para decidir se o backup
"primeira abertura do dia" já rodou hoje.

## Casos de uso (Application)

- **CriarBackupAutomatico()** — chamado em dois pontos do `App.xaml.cs`:
  - No startup, **depois** da checagem de integridade existente: se
    `ExisteBackupHoje` (intervalo de hoje via `FusoBrasilia`) for falso, cria
    um backup na pasta gerenciada.
  - No `OnExit`, incondicionalmente (decisão confirmada acima).
  Nunca lança exceção — qualquer falha é registrada e engolida, o app
  continua/encerra normalmente.
- **CriarBackupManual(string? pastaExterna)** — RF22. Sempre grava na pasta
  gerenciada; se `pastaExterna` não for nulo, grava uma segunda cópia lá
  (sem aplicar retenção nessa pasta). Retorna um `Resultado<BackupRegistro>`
  para a UI mostrar sucesso/falha.
- **ValidarBackup(string caminhoArquivo)** — RF23. Verifica, nesta ordem: (a)
  extensão `.db` e que o arquivo abre como um banco SQLite válido; (b)
  versão — a última migração aplicada no arquivo está entre as migrações que
  este app conhece (`Database.GetMigrations()`), senão é de uma versão futura
  incompatível; (c) `PRAGMA integrity_check`; (d) checksum, comparando o
  arquivo `.db.sha256` companheiro (se existir) com o hash recalculado agora.
  Se o companheiro não existir (backup de origem externa sem esse arquivo),
  essa checagem é pulada e reportada como "não verificável", não como
  falha — **não bloqueia** a validação geral, já que formato + versão +
  `PRAGMA integrity_check` já são o sinal autoritativo de que o arquivo
  SQLite em si não está corrompido; o checksum é uma camada extra de
  confiança quando disponível, não um requisito único de aprovação. Retorna
  um `Resultado<RelatorioValidacao>` com o que passou e o que falhou.
- **RestaurarBackup(string caminhoArquivo)** — RF24/UC09. Chama
  `ValidarBackup` primeiro; se falhar, recusa sem tocar a base ativa. Se
  passar: cria uma cópia preventiva da base ativa atual (mesmo mecanismo de
  `BackupService`, destino identificado como cópia pré-restauração), restaura
  o arquivo escolhido para um caminho temporário, roda `PRAGMA
  integrity_check` nesse temporário, e só então substitui o arquivo
  definitivo do banco. Falha em qualquer etapa preserva a base ativa
  intocada (nada é sobrescrito antes da última etapa). Ao final, sinaliza
  para a UI que o processo precisa reiniciar o app.

## Restauração e reinício (Desktop)

Depois que `RestaurarBackup` confirma sucesso, a UI:
1. Mostra uma mensagem de sucesso.
2. Chama `Process.Start` com o caminho do executável atual
   (`Environment.ProcessPath` ou `Process.GetCurrentProcess().MainModule.FileName`).
3. Chama `Environment.Exit(0)` no processo atual (não `Application.Shutdown`,
   para não passar pelo fluxo normal de `OnExit` que dispararia outro backup
   automático desnecessário logo após a restauração).

O novo processo sobe do zero e passa pela checagem de integridade que
`App.xaml.cs` já faz — "verificar consultas essenciais" (UC09 passo 8) é
coberta pelo próprio startup normal do app, sem lógica nova duplicada.

## Tela de Backup (Desktop)

Nova aba "Backup" no shell de navegação (`MainWindow`, ao lado de
Atendimento/Produtos/Histórico):

- Botão "Criar backup agora" — grava na pasta gerenciada; um botão secundário
  "Escolher pasta externa..." abre `Microsoft.Win32.OpenFolderDialog` para
  também gravar lá.
- Lista dos backups recentes (`ListarRecentes`): data, status, checksum.
- Botão "Restaurar" no item selecionado da lista, ou "Selecionar arquivo..."
  para escolher um `.db` de fora da lista (pendrive, backup de outra
  instalação) via `Microsoft.Win32.OpenFileDialog`.
- Confirmação obrigatória (RN19) antes de restaurar: mostra a data do backup
  selecionado e o aviso "Isso vai substituir todos os dados atuais. Uma cópia
  de segurança da base atual será criada antes." — mesmo padrão de
  `IConfirmador` já estabelecido nas Etapas 3-4, reaproveitado aqui (não cria
  um segundo mecanismo de confirmação).

## Erros

| Situação | Mensagem | Onde |
| --- | --- | --- |
| Falha ao criar backup (automático) | registrada em log/`backup_registro`, sem interromper o app | `CriarBackupAutomatico`, engolida |
| Falha ao criar backup (manual) | mensagem de erro na tela, backups anteriores preservados | Tela de Backup |
| Backup inválido (formato/versão/integridade) | motivo específico de qual checagem falhou | `ValidarBackup`, exibido antes de permitir restaurar |
| Falha durante restauração | base ativa preservada, nada foi trocado | `RestaurarBackup` |

## Testes

- `Infrastructure.Tests`: `BackupServiceTests` — cria backup de um banco real
  (SQLite de teste), confirma que o arquivo `.db` e o `.db.sha256`
  companheiro existem, que o checksum bate, que `PRAGMA integrity_check`
  passa na cópia; retenção mantém só as 30 mais recentes na pasta gerenciada
  (teste com um número menor configurável para não criar 31 arquivos reais);
  pasta externa não sofre poda de retenção.
- `Application.Tests`: `CriarBackupAutomaticoTests` (não cria se já existe
  backup hoje; cria se não existe; nunca lança exceção mesmo se o
  repositório falhar), `ValidarBackupTests` (arquivo válido passa em todas
  as checagens; arquivo não-SQLite falha em formato; versão futura
  incompatível falha em versão; arquivo corrompido falha em integridade;
  ausência do `.sha256` companheiro é "não verificável", não falha dura),
  `RestaurarBackupTests` (sucesso: cópia preventiva criada, base trocada;
  falha de validação: base ativa intocada, nenhuma cópia preventiva
  necessária).
- `Desktop.Tests`: `BackupViewModelTests` — listar recentes ao abrir; criar
  backup manual chama o caso de uso e atualiza a lista; restaurar sem
  confirmação não faz nada; restaurar com confirmação chama
  `RestaurarBackup` e sinaliza reinício.
- Sem teste automatizado para o `Process.Start`/`Environment.Exit` real (não
  é praticamente testável em xUnit) — a checagem manual descrita abaixo cobre
  isso.

## Critérios de pronto

- `dotnet build`/`dotnet test` limpos na solução inteira;
- abrir o app pela primeira vez no dia — um backup automático aparece na
  lista;
- criar um backup manual pela tela — aparece na lista com checksum;
- restaurar um backup — cópia preventiva é criada, app reinicia sozinho e
  sobe normalmente com os dados do backup restaurado;
- tentar restaurar um arquivo `.db` corrompido ou de formato inválido — é
  recusado, base ativa continua intacta;
- reiniciar o app repetidamente no mesmo dia não cria backups duplicados de
  "primeira abertura".
