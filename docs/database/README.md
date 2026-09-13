# Banco de dados

## Arquivos

- `schema.sql`: esquema inicial do SQLite;
- `seed.sql`: massa pequena de demonstração;
- `verificacoes.sql`: consultas de integridade e resumo.

## Teste rápido

```bash
sqlite3 lanchonete.db < schema.sql
sqlite3 lanchonete.db < seed.sql
sqlite3 lanchonete.db < verificacoes.sql
```

O resultado de `PRAGMA integrity_check` deve ser `ok`. As consultas de divergência devem retornar zero linhas. O resumo diário do `seed.sql` deve apresentar uma venda de 5.400 centavos.

## Regras

- não alterar `schema.sql` já implantado para atualizar produção;
- criar nova migração numerada;
- criar backup antes da migração;
- testar migração com banco vazio e com banco da versão anterior;
- manter `PRAGMA foreign_keys = ON` em toda conexão;
- não adicionar tabela de pagamento ao MVP.

