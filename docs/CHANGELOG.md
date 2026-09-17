# Histórico de alterações

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
