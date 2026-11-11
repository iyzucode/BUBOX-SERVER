#!/usr/bin/env bash
# Rilis versi baru: ambil kode terbaru kedua repo → backup → build → migrasi → jalankan ulang.
# Pemakaian:  /opt/luanna/Server/deploy/scripts/update.sh [branch]   (default: main)
set -euo pipefail

BRANCH="${1:-main}"
DEPLOY_DIR="$(cd "$(dirname "$0")/.." && pwd)"
SERVER_DIR="$(cd "$DEPLOY_DIR/.." && pwd)"
CLIENT_DIR="$(cd "$SERVER_DIR/../Client" && pwd)"
COMPOSE=(docker compose -f "$DEPLOY_DIR/docker-compose.prod.yml" --env-file "$DEPLOY_DIR/.env")

echo "== Ambil kode ($BRANCH)"
for repo in "$SERVER_DIR" "$CLIENT_DIR"; do
  git -C "$repo" fetch --prune origin
  git -C "$repo" checkout "$BRANCH"
  git -C "$repo" pull --ff-only origin "$BRANCH"
  echo "   $(basename "$repo"): $(git -C "$repo" log --oneline -1)"
done

echo "== Build image"
"${COMPOSE[@]}" build --pull

echo "== Backup sebelum migrasi"
if "${COMPOSE[@]}" ps --status running postgres | grep -q postgres; then
  "$DEPLOY_DIR/scripts/backup.sh"
fi

echo "== Database & migrasi"
"${COMPOSE[@]}" up -d --wait postgres
"$DEPLOY_DIR/scripts/migrate.sh"

echo "== Jalankan versi baru"
"${COMPOSE[@]}" up -d --remove-orphans

echo "== Cek kesehatan"
domain="$(grep -E '^DOMAIN=' "$DEPLOY_DIR/.env" | cut -d= -f2-)"
for _ in $(seq 1 30); do
  if curl -fsS "https://$domain/api/health" >/dev/null; then
    echo "OK: https://$domain/api/health"
    docker image prune -f >/dev/null
    exit 0
  fi
  sleep 2
done
echo "PERINGATAN: /api/health belum merespons. Cek: docker compose logs api --tail 100" >&2
exit 1
