# ADR 0002 SQLite

## Status

Aceita.

## Contexto

Há um único processo principal e não existe necessidade de acesso concorrente por vários computadores.

## Decisão

Usar SQLite com chaves estrangeiras, transações, migrações versionadas e backup consistente.

## Consequências

- não há serviço de banco separado;
- implantação é simples;
- o arquivo não deve ser compartilhado diretamente em pasta de rede;
- índices e transações precisam ser definidos explicitamente;
- migração para servidor será necessária se surgirem múltiplos terminais concorrentes.

