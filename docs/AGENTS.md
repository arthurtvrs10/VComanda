# Instruções para agentes de desenvolvimento

## Fonte de verdade

Antes de alterar código, leia nesta ordem:

1. `README.md`;
2. `docs/01-escopo-mvp.md`;
3. `docs/02-requisitos.md`;
4. `docs/03-regras-negocio.md`;
5. `docs/14-guia-implementacao.md`;
6. `docs/19-plataforma-windows-dotnet.md`;
7. o caso de uso e teste relacionado à tarefa.

## Restrições obrigatórias

- o MVP funciona sem internet;
- o MVP usa um único computador;
- o sistema calcula e exibe o total;
- o operador digita o total manualmente na maquininha;
- não processar nem registrar pagamento;
- não adicionar forma de pagamento, troco, valor recebido, cartão, token, NSU ou autorização;
- não criar integração com adquirente sem mudança de escopo aprovada;
- preservar nome e preço históricos no item;
- armazenar dinheiro em centavos inteiros;
- usar transação para operações que alteram mais de um registro;
- preservar o banco antes de migrações e restaurações.
- impedir duas instâncias do aplicativo usando a mesma base;
- recuperar comandas abertas após encerramento inesperado;
- manter dados em `%LOCALAPPDATA%\VarthexComanda`, fora da pasta do executável.

## Plataforma definida

- sistema operacional: Windows;
- linguagem e runtime: C# e .NET 10 LTS;
- interface: WPF com XAML e MVVM;
- persistência: SQLite com Entity Framework Core;
- testes: xUnit;
- distribuição inicial: publicação autocontida para `win-x64`.

Não substituir a stack ou incluir servidor, API web ou contêiner sem registrar e aprovar uma decisão arquitetural.

## Regra para implementação

Cada mudança deve indicar os códigos RF, RN e CT correspondentes. Se o comportamento não estiver documentado ou houver conflito, não invente silenciosamente: registre a dúvida em `docs/13-pendencias-validacao.md` ou abra uma issue.

## Banco

`database/schema.sql` representa a versão inicial. Em código real, alterações posteriores devem ser novas migrações. Não edite uma migração já utilizada em produção.

## Conclusão

Execute `scripts/validate.sh`, testes automatizados e os critérios de aceitação afetados. Atualize documentação, rastreabilidade e `CHANGELOG.md` quando o comportamento mudar.
