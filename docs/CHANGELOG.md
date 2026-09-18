# Histórico de alterações

## 1.10 — Configurações do estabelecimento (RF25)

- Nova tela "Configurações": nome do estabelecimento, quantidade máxima de
  comandas e pasta de backup externa padrão, persistidos na tabela
  `configuracao` já existente desde a base técnica.
- `AbrirComanda` passa a respeitar a quantidade máxima configurada (sem
  teto enquanto nada for configurado, preservando o comportamento
  anterior) — resolve QV02.
- O backup automático do encerramento do app (`CriarBackupAutomatico`,
  ramo incondicional) passa a também copiar para a pasta externa
  configurada, quando houver uma — cumprindo a política de cópia externa
  ao fim do dia (docs/08) sem exigir clique manual. A abertura do dia
  continua só na pasta gerenciada.

## 1.9 - 2026-09-18

- backup e recuperação (Etapa 6, RF21-24): backup automático na primeira
  abertura do dia e ao encerrar o app, backup manual com opção de pasta
  externa, validação de formato/versão/integridade/checksum, e restauração
  com cópia preventiva da base atual e reinício automático do aplicativo;
  motor de backup passa a usar a API de snapshot nativa do SQLite em vez de
  cópia de arquivo direta; retenção mantém as 30 cópias mais recentes na
  pasta gerenciada.

## 1.8 - 2026-09-18

- histórico e resumo (Etapa 5, RF18-20): tela combinando consulta de vendas
  por data, localização pelo número da comanda, detalhe dos itens de uma
  venda e resumo diário (quantidade, total, ticket médio); filtro de data
  respeita o fuso de Brasília (RN20) mesmo com os horários gravados em UTC;
  estado vazio quando não há vendas concluídas na data selecionada.

## 1.7 - 2026-09-18

- encerramento e venda (Etapa 4, RF14-17): tela de encerramento exibindo itens,
  preços e total a pagar; confirmação manual de cobrança aprovada fora do sistema;
  encerramento grava a venda e fecha a comanda em uma única transação (RN13-15);
  número da comanda é liberado após o fechamento; comanda vazia não pode ser
  encerrada (RN09).

## 1.6 - 2026-09-17

- implementadas comandas (Etapa 3): grade de números abertos, abertura,
  catálogo rápido, lançamento/alteração/remoção de itens e cancelamento
  (RF06-13);
- lançar o mesmo produto duas vezes na mesma comanda incrementa a
  quantidade de uma única linha em vez de duplicar, desde que o preço
  não tenha mudado entre os dois lançamentos;
- abrir comanda não faz pré-checagem de número livre — insere direto e
  deixa o índice único do banco ser a fonte da verdade, cobrindo o caso
  de concorrência (RN01);
- `MainWindow` deixa de mostrar só Produtos e vira um shell com dois
  botões (Atendimento, Produtos) — Atendimento abre por padrão, conforme
  a tela inicial esperada;
- criado o projeto `VarthexComanda.Desktop.Tests`, com testes de unidade
  reais para `AtendimentoViewModel` e (retroativamente) `ProdutosViewModel`
  — fecha a lacuna que a Etapa 2 deixou aberta e que tinha deixado passar
  dois bugs reais;
- ainda sem encerramento nem venda (RF14-17) — entra na próxima fatia.

## 1.5 - 2026-09-17

- implementado o catálogo (Etapa 2): cadastro e busca de produtos, cadastro de
  categorias (RF01, RF03, RF05); os casos de uso de alteração e desativação de
  categoria (RF02) existem e têm testes, mas ainda não têm tela própria —
  produtos podem ser alterados e desativados pela tela "Produtos", categorias
  ainda não;
- casos de uso de catálogo validam nome obrigatório, preço positivo em centavos e
  categoria ativa, sem exceções para erros esperados;
- repositórios de categoria e produto sobre `IDbContextFactory`, com DbContext
  de curta duração por operação;
- composição do Desktop passa a usar `Microsoft.Extensions.DependencyInjection`
  de verdade (container, `IDbContextFactory` registrado, ViewModels resolvidas
  pelo container) — fecha a pendência de DI/MVVM deixada em aberto na Etapa 0+1;
- `MainWindow` deixa de ser uma janela vazia e passa a exibir a tela "Produtos";
- limitação conhecida: busca por nome e detecção de duplicidade de categoria
  ignoram maiúsculas/minúsculas apenas em caracteres ASCII (SQLite `LOWER()`/
  `NOCASE` não tratam acentos) — nomes acentuados como "Açaí" podem não bater
  em buscas ou podem ser duplicados sob acentuação diferente; correção própria
  fica para uma fatia futura;
- ainda sem comandas, itens ou vendas (RF06+) — entra na próxima fatia.

## 1.4 - 2026-09-16

- criada a base técnica do código em `backend/` (Etapas 0 e 1 do guia de implementação);
- solução .NET 10 com Domain, Application, Infrastructure e Desktop (WPF);
- SQLite + EF Core mapeados a partir de `database/schema.sql`, com migração inicial;
- instância única via mutex, backup preventivo antes de migrar e logging rotativo com Serilog;
- ainda sem telas ou regras de negócio (RF01+) — entra na próxima fatia.

## 1.3 - 2026-09-13

- adotado o nome oficial Varthex Comanda;
- confirmada a família do sistema operacional Windows;
- substituída a proposta Java por C# .NET 10 LTS WPF e SQLite;
- definido Entity Framework Core para persistência e migrações;
- incluída publicação autocontida para Windows;
- adicionadas regras de instância única e recuperação de comandas abertas;
- adicionados controle de rotação de logs e testes específicos de Windows;
- incluídos guia técnico da plataforma e ADR da stack;
- mantido pagamento completamente fora do MVP.

## 1.2 - 2026-09-13

- retirado o processamento de pagamentos do MVP;
- retirada a entidade `PAGAMENTO` do modelo e do SQL;
- definido que o sistema apenas exibe o total a pagar;
- definida digitação manual do total na maquininha;
- incluída confirmação explícita antes do encerramento;
- registrada integração com maquininha somente como evolução futura;
- criado pacote documental para implementação pelo GitHub.

## 1.1 - 2026-09-13

- documento consolidado em padrão ABNT;
- incluídos requisitos, regras, casos de uso, modelo de dados, arquitetura, testes, riscos e roadmap.

## 1.0 - 2026-09-13

- definição inicial do sistema local de comandas.
