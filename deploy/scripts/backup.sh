#!/usr/bin/env bash
# Backup database (pg_dump format custom). Foto profil & gambar menu tersimpan di database,
# jadi ikut ter-backup. Pasang di cron, mis. tiap malam 02.00:
#   0 2 * * * /opt/luanna/Server/deploy/scripts/backup.sh >> /var/log/luanna-backup.log 2>&1
#
# Restore (HATI-HATI: menimpa isi database):
#   docker compose -f docker-compose.prod.yml exec -T postgres \
#     pg_restore -U bubox -d bubox_db --clean --if-exists < /var/backups/luanna/bubox_db_<waktu>.dump
#
# Simpan salinan di LUAR VPS (object storage / komputer lain): backup yang hanya ada di VPS
# ikut hilang kalau VPS-nya bermasalah.
set -euo pipefail

DEPLOY_DIR="$(cd "$(dirname "$0")/.." && pwd)"
BACKUP_DIR="${BACKUP_DIR:-/var/backups/luanna}"
KEEP_DAYS="${KEEP_DAYS:-14}"

mkdir -p "$BACKUP_DIR"
chmod 700 "$BACKUP_DIR"

stamp="$(date +%Y-%m-%d_%H%M)"
target="$BACKUP_DIR/bubox_db_$stamp.dump"

docker compose -f "$DEPLOY_DIR/docker-compose.prod.yml" --env-file "$DEPLOY_DIR/.env" exec -T postgres \
  pg_dump -U bubox -d bubox_db -Fc > "$target.partial"
mv "$target.partial" "$target"
chmod 600 "$target"

# Hapus backup lebih tua dari KEEP_DAYS hari.
find "$BACKUP_DIR" -name 'bubox_db_*.dump' -mtime +"$KEEP_DAYS" -delete

echo "$(date -Is) backup OK: $target ($(du -h "$target" | cut -f1))"
