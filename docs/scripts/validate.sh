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
  docs/19-plataforma-windows-dotnet.md
  docs/adr/0004-windows-dotnet-wpf.md
  database/schema.sql
  database/seed.sql
)

for item in "${required[@]}"; do
  test -s "$repo_dir/$item" || { echo "Ausente: $item"; exit 1; }
done

echo "Validando banco SQLite"
if command -v sqlite3 >/dev/null 2>&1; then
  sqlite3 "$validation_tmp/varthex-comanda.db" < "$repo_dir/database/schema.sql"
  sqlite3 "$validation_tmp/varthex-comanda.db" < "$repo_dir/database/seed.sql"
  integrity="$(sqlite3 "$validation_tmp/varthex-comanda.db" 'PRAGMA integrity_check;')"
  test "$integrity" = "ok" || { echo "Integridade inválida: $integrity"; exit 1; }
  total="$(sqlite3 "$validation_tmp/varthex-comanda.db" 'SELECT total_centavos FROM venda WHERE id = 1;')"
  test "$total" = "5400" || { echo "Seed inesperado: $total"; exit 1; }
else
  echo "Aviso: sqlite3 não instalado; validação do banco ignorada"
fi

echo "Validando stack documental"
rg -q 'C# e \.NET 10 LTS' "$repo_dir/README.md" || { echo "Stack .NET ausente do README"; exit 1; }
rg -q 'WPF' "$repo_dir/docs/06-arquitetura.md" || { echo "WPF ausente da arquitetura"; exit 1; }
if rg -n -i 'Interface JavaFX|Java 21 LTS|Persistência JDBC|confirmar Java 21' \
  "$repo_dir/README.md" "$repo_dir/AGENTS.md" "$repo_dir/docs"; then
  echo "Referência ativa à stack Java encontrada"
  exit 1
fi

echo "Validando cobertura documental"
rg -q 'RF27' "$repo_dir/docs/02-requisitos.md" || { echo "RF27 ausente"; exit 1; }
rg -q 'RNF21' "$repo_dir/docs/02-requisitos.md" || { echo "RNF21 ausente"; exit 1; }
rg -q 'RN23' "$repo_dir/docs/03-regras-negocio.md" || { echo "RN23 ausente"; exit 1; }
rg -q 'CT22' "$repo_dir/docs/09-testes-aceitacao.md" || { echo "CT22 ausente"; exit 1; }

echo "Verificando proibição de tabela pagamento"
if rg -n -i 'CREATE TABLE (IF NOT EXISTS )?pagamento' "$repo_dir/database"; then
  echo "O MVP não pode possuir tabela pagamento"
  exit 1
fi

echo "Validação concluída"
