# ADR 0004 Windows .NET e WPF

## Situação

Aceita em 13 de setembro de 2026.

## Contexto

O usuário confirmou que o computador de desenvolvimento e o computador-alvo pertencem à família Windows. O produto é um aplicativo desktop local, de terminal único, sem servidor e com banco SQLite. O desenvolvimento precisa gerar um pacote simples de instalar e manter.

## Decisão

Usar C# com .NET 10 LTS, WPF e XAML na interface, MVVM com CommunityToolkit.Mvvm, Entity Framework Core com SQLite, xUnit para testes e publicação autocontida para Windows. `win-x64` é o padrão inicial até a arquitetura do equipamento ser confirmada.

## Consequências

- o aplicativo será específico para Windows;
- o usuário final não precisará instalar o runtime separadamente no pacote autocontido;
- a interface não deve vazar dependências de WPF para o domínio;
- migrações do Entity Framework Core controlarão a evolução do banco;
- a edição, versão, arquitetura, resolução e escala do Windows devem ser homologadas;
- uma mudança futura para outro sistema operacional exigirá nova decisão arquitetural.

## Alternativas consideradas

- Java e JavaFX: tecnicamente adequados, mas deixam de aproveitar a plataforma Windows confirmada;
- Avalonia: indicado para multiplataforma, requisito que não existe no MVP;
- C puro: aumenta esforço de interface, persistência e manutenção sem benefício para o domínio administrativo.
