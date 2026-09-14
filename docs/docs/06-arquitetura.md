# Arquitetura

## Visão

Aplicativo desktop modular, executado em um computador, com banco SQLite no mesmo equipamento. O MVP não possui backend remoto nem API de pagamento.

```mermaid
flowchart TB
    UI[Interface WPF e MVVM]
    APP[Casos de uso da aplicação]
    DOMAIN[Domínio e regras]
    DB[(SQLite)]
    FS[Backup e logs]

    UI --> APP
    APP --> DOMAIN
    APP --> DB
    APP --> FS
```

## Componentes

| Componente | Responsabilidade | Não deve fazer |
| --- | --- | --- |
| Interface | Navegação, entrada, mensagens e estado visual | Executar SQL ou conter regra crítica |
| Aplicação | Orquestrar casos de uso e transações | Depender de controles WPF |
| Domínio | Entidades, estados, cálculos e invariantes | Conhecer SQLite, arquivo ou tela |
| Persistência | Consultas, mapeamento e migrações | Decidir regra de negócio |
| Backup | Snapshot, validação, retenção e restauração | Modificar venda ou comanda |
| Observabilidade | Logs técnicos e diagnóstico | Registrar cartão, senha ou dado pessoal desnecessário |

## Estrutura sugerida

```text
app/
  atendimento/
    domain/
    application/
    persistence/
    ui/
  catalogo/
    domain/
    application/
    persistence/
    ui/
  venda/
    domain/
    application/
    persistence/
    ui/
  backup/
  configuracao/
  shared/
```

Organização por domínio é preferida. Dentro de cada domínio, separe responsabilidades de negócio, aplicação, persistência e interface.

## Stack definida

- C# com .NET 10 LTS;
- WPF e XAML;
- MVVM com CommunityToolkit.Mvvm;
- SQLite;
- Entity Framework Core com provedor SQLite;
- migrações do Entity Framework Core;
- xUnit;
- Serilog para arquivos de log com rotação;
- publicação autocontida para Windows.

O executável de interface é também a raiz de composição. Domínio e aplicação não dependem de WPF, Entity Framework Core ou SQLite. A camada de infraestrutura implementa persistência, relógio, sistema de arquivos e backup.

## Transações

Devem ser transacionais:

- abertura de comanda e ocupação do número;
- inclusão, alteração ou remoção de item com atualização do total;
- encerramento com criação de venda e mudança de status;
- cancelamento e liberação do número;
- aplicação de migração.

Cada caso de uso que grava dados utiliza uma unidade de trabalho e uma instância de contexto de curta duração. O aplicativo deve ativar chaves estrangeiras, definir tempo limite de bloqueio e impedir uma segunda instância usando a mesma base.

## Integração futura com maquininha

Deve ser um adaptador opcional, nunca uma dependência do domínio. Uma interface futura pode seguir o formato:

```text
TerminalPagamento
  iniciarCobranca(valorCentavos, referencia)
  consultarSituacao(referencia)
  cancelarCobranca(referencia)
```

Essa interface não será implementada no MVP. O comportamento atual é um adaptador manual que apenas exibe o total e aguarda confirmação humana.

## Decisões arquiteturais

- [ADR 0001 Aplicação desktop local](adr/0001-aplicacao-desktop-local.md)
- [ADR 0002 SQLite](adr/0002-sqlite.md)
- [ADR 0003 Pagamento fora do MVP](adr/0003-pagamento-fora-mvp.md)
- [ADR 0004 Windows .NET e WPF](adr/0004-windows-dotnet-wpf.md)
