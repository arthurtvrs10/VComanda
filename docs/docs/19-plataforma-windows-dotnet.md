# Plataforma Windows e .NET

Este documento define como iniciar, organizar, persistir, testar e publicar o Varthex Comanda. Ele complementa os requisitos sem substituir as regras de negócio.

## Linha de base

| Elemento | Decisão |
| --- | --- |
| Sistema operacional | Windows; versão e arquitetura exatas pendentes em QV07 |
| Linguagem | C# |
| Runtime | .NET 10 LTS |
| Interface | WPF e XAML |
| Padrão de apresentação | MVVM com CommunityToolkit.Mvvm |
| Persistência | Entity Framework Core com SQLite |
| Testes | xUnit |
| Logs | Serilog com rotação |
| Publicação | Autocontida; `win-x64` como padrão inicial |

## Estrutura da solução

```text
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

Dependências permitidas:

- `Domain` não referencia outro projeto;
- `Application` referencia `Domain`;
- `Infrastructure` referencia `Application` e `Domain`;
- `Desktop` referencia `Application` e `Infrastructure` e funciona como raiz de composição;
- testes referenciam apenas os projetos que verificam.

## Criação inicial

Execute no PowerShell a partir da raiz do futuro repositório de código:

```powershell
dotnet new sln -n VarthexComanda
dotnet new classlib -n VarthexComanda.Domain -o src/VarthexComanda.Domain -f net10.0
dotnet new classlib -n VarthexComanda.Application -o src/VarthexComanda.Application -f net10.0
dotnet new classlib -n VarthexComanda.Infrastructure -o src/VarthexComanda.Infrastructure -f net10.0
dotnet new wpf -n VarthexComanda.Desktop -o src/VarthexComanda.Desktop -f net10.0
dotnet new xunit -n VarthexComanda.Domain.Tests -o tests/VarthexComanda.Domain.Tests -f net10.0
dotnet new xunit -n VarthexComanda.Application.Tests -o tests/VarthexComanda.Application.Tests -f net10.0
dotnet new xunit -n VarthexComanda.Infrastructure.Tests -o tests/VarthexComanda.Infrastructure.Tests -f net10.0
dotnet sln add (Get-ChildItem -Recurse -Filter *.csproj)
```

Depois, adicione as referências conforme a direção definida e os pacotes abaixo:

```powershell
dotnet add src/VarthexComanda.Infrastructure package Microsoft.EntityFrameworkCore.Sqlite
dotnet add src/VarthexComanda.Infrastructure package Microsoft.EntityFrameworkCore.Design
dotnet add src/VarthexComanda.Desktop package CommunityToolkit.Mvvm
dotnet add src/VarthexComanda.Desktop package Microsoft.Extensions.DependencyInjection
dotnet add src/VarthexComanda.Desktop package Serilog
dotnet add src/VarthexComanda.Desktop package Serilog.Sinks.File
```

As versões devem ser compatíveis com .NET 10, registradas centralmente e travadas no repositório. Não usar versão prévia em produção.

## Persistência

- usar `long` para centavos e conversão explícita na interface;
- usar um `DbContext` de curta duração por caso de uso ou unidade de trabalho;
- ativar `foreign_keys`, definir tempo limite de banco e usar transações explícitas nos fluxos documentados;
- criar a primeira migração a partir do modelo equivalente a `database/schema.sql`;
- nunca editar migração já aplicada em produção;
- criar backup válido antes de migrar uma base existente;
- validar `PRAGMA integrity_check` na rotina de diagnóstico e restauração.

Comandos básicos:

```powershell
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate --project src/VarthexComanda.Infrastructure --startup-project src/VarthexComanda.Desktop
dotnet ef database update --project src/VarthexComanda.Infrastructure --startup-project src/VarthexComanda.Desktop
```

## Arquivos locais

```text
%LOCALAPPDATA%\VarthexComanda\
  data\varthex-comanda.db
  logs\varthex-comanda-AAAA-MM-DD.log
  backups\varthex-comanda-AAAA-MM-DD-HHMMSS.db
```

O caminho deve ser obtido por `Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)`. O executável e os arquivos operacionais ficam separados.

## Inicialização segura

1. adquirir mutex nomeado da aplicação;
2. resolver e criar as pastas locais;
3. configurar log com rotação;
4. criar cópia preventiva quando houver migração pendente;
5. abrir o SQLite e aplicar migrações;
6. validar consultas essenciais;
7. carregar comandas abertas;
8. exibir a tela de atendimento.

Se o mutex já estiver ocupado, informar que o Varthex Comanda está aberto e encerrar a segunda execução. Se a base estiver inválida, não permitir gravações nem restaurar cópia automaticamente.

## Testes e qualidade

```powershell
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
```

Os testes de infraestrutura devem criar bancos temporários. Nunca executar testes automatizados destrutivos contra a base da lanchonete. Cada teste deve indicar os códigos RF, RN, RNF e CT que cobre quando aplicável.

## Publicação para Windows

Enquanto a arquitetura final estiver pendente, usar `win-x64`:

```powershell
dotnet publish src/VarthexComanda.Desktop/VarthexComanda.Desktop.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --output artifacts/publish/win-x64
```

Manter múltiplos arquivos na primeira versão simplifica o diagnóstico das bibliotecas nativas do SQLite. A pasta publicada deve ser testada em um Windows limpo sem SDK do .NET. Depois da homologação, um instalador pode criar atalhos e registrar a versão sem apagar `%LOCALAPPDATA%\VarthexComanda` durante atualização ou desinstalação.

## Critério técnico de pronto

- build e testes passam em configuração Release;
- migrações funcionam em banco vazio e em cópia da versão anterior;
- aplicativo funciona sem internet;
- segunda instância é recusada;
- fechamento forçado preserva comandas já confirmadas;
- pacote autocontido inicia no Windows homologado;
- dados sobrevivem à atualização e desinstalação do executável;
- nenhum modelo, tabela, tela ou log registra pagamento no MVP.
