-- Foto profil pengguna.
-- Gambar disimpan di database (sudah diperkecil di aplikasi, ~20-80 KB) supaya tidak butuh
-- volume file terpisah saat deploy. users.avatar_updated_at dipakai sebagai versi URL
-- (cache-busting) dan penanda "punya foto" tanpa perlu membaca tabel gambar.

CREATE TABLE IF NOT EXISTS user_avatars (
    user_id      UUID PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
    content_type VARCHAR(20) NOT NULL,
    data         BYTEA NOT NULL,
    updated_at   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

ALTER TABLE users ADD COLUMN IF NOT EXISTS avatar_updated_at TIMESTAMPTZ NULL;
