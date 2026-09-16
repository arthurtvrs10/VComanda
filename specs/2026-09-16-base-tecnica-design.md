# Base técnica do Varthex Comanda — Etapas 0 e 1

Data: 2026-09-16
Escopo: fundação técnica do sistema (sem telas de negócio).

## Contexto

O repositório é hoje só documentação (`docs/`). Este spec cobre a primeira fatia de código,
correspondente às Etapas 0 e 1 de [docs/docs/14-guia-implementacao.md](../../docs/14-guia-implementacao.md):
validar ambiente e criar a base técnica + banco. As regras de negócio (RF/RN/UC/CT) já estão
definidas nos demais documentos de `docs/docs/` e não são reabertas aqui — este spec trata
apenas de estrutura de projeto, persistência e infraestrutura básica.

Fonte de verdade para decisões técnicas: [docs/docs/19-plataforma-windows-dotnet.md](../../docs/19-plataforma-windows-dotnet.md).

## Localização do código

Novo diretório `backend/` na raiz do repositório (irmão de `docs/`):

```
backend/
  VarthexComanda.sln
  src/
    VarthexComanda.Domain/
    VarthexComanda.Application/
    VarthexComanda.Infrastructure/
    VarthexComanda.Desktop/
  tests/
    VarthexComanda.Domain.Tests/
    VarthexComanda.Application.Tests/
    VarthexComanda.Infrastructure.Tests/
```

Regras de dependência (de docs/19): `Domain` não referencia nada; `Application` referencia
`Domain`; `Infrastructure` referencia `Application` e `Domain`; `Desktop` referencia
`Application` e `Infrastructure` e é a raiz de composição (DI). Testes referenciam só o
projeto que verificam.

## Escopo desta entrega

Inclui:

- Solução .NET 10 com os 4 projetos de produção e os 3 de teste, alvo `net10.0`.
- Projeto `Desktop` como app WPF mínimo: inicia, mostra uma janela vazia, usa
  `Microsoft.Extensions.DependencyInjection` para composição e `CommunityToolkit.Mvvm`
  para o padrão MVVM (ainda sem ViewModels de negócio).
- `Infrastructure` com EF Core + SQLite, `DbContext` e a primeira migração equivalente ao
  esquema de [docs/database/schema.sql](../../database/schema.sql).
  `PRAGMA foreign_keys = ON` em toda conexão.
- Resolução do diretório de dados via
  `Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)`, criando:
  ```
  %LOCALAPPDATA%\VarthexComanda\data\varthex-comanda.db
  %LOCALAPPDATA%\VarthexComanda\logs\varthex-comanda-AAAA-MM-DD.log
  %LOCALAPPDATA%\VarthexComanda\backups\
  ```
- Logging com Serilog, arquivo com rotação diária.
- Mutex nomeado de instância única: segunda execução detecta o mutex, informa que o
  aplicativo já está aberto e encerra com segurança (sem tocar no banco).
- Sequência de inicialização segura (de docs/19): adquirir mutex → criar pastas locais →
  configurar log → aplicar migrações pendentes → validar `PRAGMA integrity_check` →
  abrir janela principal.
- Testes xUnit:
  - migração aplica em banco vazio sem erro;
  - migração repetida (app iniciado duas vezes) não duplica estrutura nem quebra;
  - segunda instância é recusada (mutex já adquirido) e não corrompe o banco da primeira;
  - `PRAGMA integrity_check` retorna `ok` após criação.

Não inclui (fica para próxima fatia): qualquer entidade ou tela de catálogo, comanda,
item, venda; qualquer RF de negócio; backup/restauração funcional (só a pasta é criada);
empacotamento/publicação `win-x64` final.

## Critérios de pronto (retirados de docs/14 e docs/19)

- app abre pelo `dotnet run` e mantém critérios abaixo também depois de `dotnet publish`;
- banco, logs e backups ficam fora da pasta do executável;
- iniciar duas vezes não duplica estrutura nem corrompe dados;
- aplicação funciona sem rede;
- `dotnet build` e `dotnet test` passam em configuração `Release`.

## Fora de escopo / riscos conhecidos

- QV07 (versão/arquitetura exata do Windows de produção) segue pendente; usar `win-x64`
  como já decidido provisoriamente em docs/19, sem bloquear esta entrega.
- Backup/restauração real (Etapa 6) e empacotamento final (Etapa 7) são fatias futuras.
