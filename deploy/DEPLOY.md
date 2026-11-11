# Deploy LUANNA ke VPS

Satu VPS Ubuntu 24.04, Docker Compose, satu domain (`luanna.shop`) dengan HTTPS otomatis.

```
Internet ──443──► Caddy (HTTPS Let's Encrypt)
                   ├─ /api/*  → api  (.NET, port 5073, tidak terbuka ke internet)
                   └─ /*      → web  (nginx, hasil `expo export -p web`)
                 api ──► postgres (jaringan internal, volume permanen)
```

Yang terbuka ke internet hanya port 22 (SSH), 80, dan 443.

| File | Fungsi |
|---|---|
| `docker-compose.prod.yml` | Definisi 4 service di atas |
| `Caddyfile` | HTTPS, header keamanan, routing `/api` vs web |
| `.env.example` | Daftar rahasia produksi → salin jadi `.env` (tidak di-commit) |
| `scripts/migrate.sh` | Menjalankan SQL migrasi yang belum jalan (dicatat di `schema_migrations`) |
| `scripts/backup.sh` | `pg_dump` + rotasi 14 hari |
| `scripts/update.sh` | Rilis: pull → build → backup → migrasi → restart → cek health |

---

## 0. Sebelum mulai

- [ ] Ganti password root VPS di panel penyedia (password lama sempat terlihat di screenshot).
- [ ] Domain `luanna.shop`: buat record **A** `@` → IP VPS, dan **A** `www` → IP VPS.
      AAAA (IPv6) boleh ditambah setelah IPv6 VPS dipastikan berfungsi.
- [ ] Siapkan: server key Midtrans (sandbox dulu), API key Resend, email untuk Let's Encrypt.

## 1. Amankan server (sekali saja)

Dari komputer kamu (bukan dari VPS), buat SSH key bila belum punya:

```bash
ssh-keygen -t ed25519 -C "luanna-vps"
```

Login sebagai root, lalu buat user `deploy`:

```bash
ssh root@202.10.42.121
passwd                                   # ganti password root
adduser deploy
usermod -aG sudo deploy
mkdir -p /home/deploy/.ssh
nano /home/deploy/.ssh/authorized_keys   # tempel isi ~/.ssh/id_ed25519.pub dari komputer kamu
chown -R deploy:deploy /home/deploy/.ssh && chmod 700 /home/deploy/.ssh && chmod 600 /home/deploy/.ssh/authorized_keys
```

**Di terminal lain**, pastikan `ssh deploy@202.10.42.121` berhasil dengan key. Baru setelah itu matikan login root & password:

```bash
sudo sed -i -E 's/^#?PermitRootLogin .*/PermitRootLogin no/; s/^#?PasswordAuthentication .*/PasswordAuthentication no/' /etc/ssh/sshd_config
sudo sshd -t && sudo systemctl restart ssh
```

Firewall, proteksi brute force, update otomatis, zona waktu:

```bash
sudo apt update && sudo apt -y upgrade
sudo apt -y install ufw fail2ban unattended-upgrades git curl
sudo ufw default deny incoming && sudo ufw default allow outgoing
sudo ufw allow OpenSSH && sudo ufw allow 80/tcp && sudo ufw allow 443/tcp && sudo ufw allow 443/udp
sudo ufw enable
sudo dpkg-reconfigure -plow unattended-upgrades
sudo timedatectl set-timezone Asia/Jakarta
```

Swap 2 GB bila RAM < 4 GB (build Expo butuh memori):

```bash
sudo fallocate -l 2G /swapfile && sudo chmod 600 /swapfile && sudo mkswap /swapfile && sudo swapon /swapfile
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab
```

Docker Engine + Compose (repo resmi Docker):

```bash
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker deploy    # logout lalu login lagi agar berlaku
```

> Catatan: port yang dibuka lewat Docker (`ports:`) melewati aturan ufw. Di compose ini hanya Caddy
> yang membuka port (80/443), jadi API dan Postgres tetap tidak bisa dijangkau dari luar.

## 2. Ambil kode

Repo GitHub privat: buat **deploy key** (read-only) per repo, atau clone dengan akun GitHub kamu.

```bash
sudo mkdir -p /opt/luanna && sudo chown deploy:deploy /opt/luanna
cd /opt/luanna
git clone https://github.com/iyzucode/BUBOX-SERVER.git Server
git clone https://github.com/iyzucode/BUBOX-CLIENT.git Client
```

Kedua folder harus bersebelahan (`/opt/luanna/Server` dan `/opt/luanna/Client`).
Checkout branch yang mau dirilis di keduanya (mis. `main` setelah `feat/order-flow-qris` di-merge).

## 3. Isi rahasia

```bash
cd /opt/luanna/Server/deploy
cp .env.example .env && chmod 600 .env
openssl rand -hex 32       # → POSTGRES_PASSWORD
openssl rand -base64 48    # → JWT_KEY
nano .env
```

API **menolak jalan** bila `JWT_KEY` kosong, kurang dari 32 karakter, atau sama dengan kunci contoh lama di repo.

## 4. Jalankan pertama kali

```bash
cd /opt/luanna/Server/deploy
docker compose -f docker-compose.prod.yml up -d --wait postgres
./scripts/migrate.sh                                   # 001 … 018, ±15 detik
docker compose -f docker-compose.prod.yml up -d --build
docker compose -f docker-compose.prod.yml ps
curl https://luanna.shop/api/health
```

Build pertama bisa 5–15 menit. Caddy mengambil sertifikat HTTPS otomatis begitu DNS mengarah ke VPS
(cek `docker compose -f docker-compose.prod.yml logs caddy` bila HTTPS belum aktif).

Admin pertama: daftar akun lewat aplikasi seperti biasa, lalu beri peran admin:

```bash
docker compose -f docker-compose.prod.yml exec -T postgres psql -U bubox -d bubox_db -c "
INSERT INTO user_roles (user_id, role_id)
SELECT u.id, r.id FROM users u, rf_roles r
WHERE u.email = 'email-admin@contoh.com' AND r.code = 'SUPER_ADMIN'
ON CONFLICT DO NOTHING;"
```

Foto menu tidak ikut di seed: unggah ulang lewat halaman admin.

## 5. Konfigurasi layanan luar

| Layanan | Isi |
|---|---|
| Google Cloud → OAuth client (Web) | *Authorized JavaScript origins*: `https://luanna.shop` · *Authorized redirect URIs*: `https://luanna.shop/oauth-redirect` |
| Midtrans dashboard → Payment Notification URL | `https://luanna.shop/api/Payment/MidtransNotification` |
| Resend | Verifikasi domain `luanna.shop` (record SPF/DKIM di DNS), lalu `EMAIL_FROM_ADDRESS=noreply@luanna.shop` |
| Google Maps (saat diaktifkan) | Key browser dibatasi HTTP referrer `https://luanna.shop/*`; key server dibatasi Geocoding API + IP VPS |

Uji alur bayar dengan **sandbox** di domain asli dulu. Setelah lancar, ganti `MIDTRANS_SERVER_KEY`
dengan key production, set `MIDTRANS_IS_PRODUCTION=true`, lalu `docker compose ... up -d api`.

## 6. Rilis berikutnya

```bash
/opt/luanna/Server/deploy/scripts/update.sh main
```

Urutannya: ambil kode kedua repo → build image → backup database → migrasi → jalankan versi baru → cek `/api/health`.
Migrasi baru cukup ditambahkan sebagai file `SQL/SPRINT-n/0NN_nama.sql` (nomor lanjut); script menjalankannya sekali.

Rollback cepat: checkout commit sebelumnya di kedua repo lalu `docker compose ... up -d --build`.
Migrasi database tidak otomatis dibatalkan: pulihkan dari backup bila perlu.

## 7. Backup & pemantauan

```bash
sudo mkdir -p /var/backups/luanna && sudo chown deploy:deploy /var/backups/luanna
crontab -e
# tambahkan:
0 2 * * * /opt/luanna/Server/deploy/scripts/backup.sh >> /home/deploy/luanna-backup.log 2>&1
```

- Salin backup ke luar VPS secara rutin (object storage S3-compatible dengan `rclone`, atau unduh ke komputer lain).
- Uji restore sesekali ke database terpisah: backup yang tidak pernah diuji belum tentu bisa dipakai.
- Pantau `https://luanna.shop/api/health` dengan UptimeRobot / Better Stack (gratis, notifikasi email/Telegram).
- Log: `docker compose -f docker-compose.prod.yml logs -f api` (ukuran log dibatasi 5 × 10 MB per service).
- Cek ruang disk: `df -h` dan `docker system df`.

## 8. Tahap lanjutan (setelah stabil)

- **CI/CD**: GitHub Actions membangun image tiap tag rilis → simpan di GHCR → VPS cukup `pull` (tanpa build di server produksi, rollback per tag).
- **Staging**: subdomain `staging.luanna.shop` dengan `.env` & database terpisah untuk uji sebelum produksi.
- **Backup offsite otomatis** dan alert disk/CPU (mis. Netdata atau Better Stack).
