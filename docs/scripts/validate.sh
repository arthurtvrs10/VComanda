#!/usr/bin/env bash
set -euo pipefail

repo_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
validation_tmp="$(mktemp -d)"
trap 'rm -rf "$validation_tmp"' EXIT

echo "Validando estrutura obrigatória"
required=(
  README.md
  docs/02-requisitos.md
  docs/03-regras-negocio.md
  docs/04-casos-de-uso.md
  docs/05-modelagem-dados.md
  docs/09-testes-aceitacao.md
  docs/14-guia-implementacao.md
  database/schema.sql
  database/seed.sql
)

for item in "${required[@]}"; do
  test -s "$repo_dir/$item" || { echo "Ausente: $item"; exit 1; }
done

echo "Validando banco SQLite"
if command -v sqlite3 >/dev/null 2>&1; then
  sqlite3 "$validation_tmp/lanchonete.db" < "$repo_dir/database/schema.sql"
  sqlite3 "$validation_tmp/lanchonete.db" < "$repo_dir/database/seed.sql"
  integrity="$(sqlite3 "$validation_tmp/lanchonete.db" 'PRAGMA integrity_check;')"
  test "$integrity" = "ok" || { echo "Integridade inválida: $integrity"; exit 1; }
  total="$(sqlite3 "$validation_tmp/lanchonete.db" 'SELECT total_centavos FROM venda WHERE id = 1;')"
  test "$total" = "5400" || { echo "Seed inesperado: $total"; exit 1; }
else
  echo "Aviso: sqlite3 não instalado; validação do banco ignorada"
fi

echo "Verificando proibição de tabela pagamento"
if rg -n -i 'CREATE TABLE (IF NOT EXISTS )?pagamento' "$repo_dir/database"; then
  echo "O MVP não pode possuir tabela pagamento"
  exit 1
fi

echo "Validação concluída"

