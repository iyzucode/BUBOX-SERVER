#!/usr/bin/env bash
# Jalankan migrasi SQL (Server/SQL/SPRINT-*/NNN_*.sql) yang belum pernah dijalankan, berurutan
# menurut nomor file. File yang sudah jalan dicatat di tabel schema_migrations.
#
# Pemakaian (dari mana saja):   deploy/scripts/migrate.sh
# Database yang sudah berisi skema dari luar script ini (mis. hasil restore backup lama):
#   deploy/scripts/migrate.sh --baseline     → tandai semua file sebagai sudah jalan, tanpa menjalankannya.
#
# Catatan: tiap file dijalankan apa adanya (beberapa file punya BEGIN/COMMIT sendiri). Bila satu
# file gagal, script berhenti dan file itu tidak dicatat; perbaiki lalu jalankan ulang.
set -euo pipefail

DEPLOY_DIR="$(cd "$(dirname "$0")/.." && pwd)"
SQL_DIR="${SQL_DIR:-$DEPLOY_DIR/../SQL}"
# Bisa diganti untuk uji lokal, mis. PSQL="psql -h localhost -U postgres -d bubox_test".
PSQL="${PSQL:-docker compose -f $DEPLOY_DIR/docker-compose.prod.yml --env-file $DEPLOY_DIR/.env exec -T postgres psql -U bubox -d bubox_db}"

# stdin default /dev/null: `docker compose exec` selalu membaca stdin, jadi tanpa ini ia ikut
# "memakan" input lain di script. Isi file SQL dikirim lewat run_sql_file.
run_sql() { $PSQL -v ON_ERROR_STOP=1 -q "$@" </dev/null; }
run_sql_file() { $PSQL -v ON_ERROR_STOP=1 -q < "$1"; }

BASELINE=false
[[ "${1:-}" == "--baseline" ]] && BASELINE=true

run_sql -c "SET client_min_messages TO warning;
CREATE TABLE IF NOT EXISTS schema_migrations (
  filename   TEXT PRIMARY KEY,
  applied_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);"

applied="$(run_sql -tA -c "SELECT filename FROM schema_migrations")"

# Urutkan menurut nama file (nomor 001, 002, …) lintas folder SPRINT. Daftar dibaca ke array
# lebih dulu, jadi loop tidak bergantung pada stdin.
mapfile -t files < <(find "$SQL_DIR" -type f -name '[0-9][0-9][0-9]_*.sql' -printf '%f\t%p\n' | sort | cut -f2)

count=0
for file in "${files[@]}"; do
  name="$(basename "$file")"
  if grep -qxF "$name" <<<"$applied"; then
    continue
  fi

  if $BASELINE; then
    echo "baseline  $name"
  else
    echo "migrasi   $name"
    run_sql_file "$file"
  fi
  run_sql -c "INSERT INTO schema_migrations (filename) VALUES ('$name');"
  count=$((count + 1))
done

echo "Selesai: $count file baru."
