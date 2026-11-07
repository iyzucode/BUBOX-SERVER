-- Detail Profil: nomor kontak + batas waktu perubahan data profil.
--   Nama        : 7 hari sekali   (full_name_changed_at)
--   Username    : 30 hari sekali  (username_changed_at)
--   Foto profil : 7 hari sekali   (avatar_changed_at; TIDAK dikosongkan saat foto dihapus,
--                                  supaya hapus-lalu-unggah tidak melewati batas)
-- NULL = belum pernah diubah sejak daftar → boleh langsung diubah.

ALTER TABLE users ADD COLUMN IF NOT EXISTS full_name_changed_at TIMESTAMPTZ NULL;
ALTER TABLE users ADD COLUMN IF NOT EXISTS username_changed_at TIMESTAMPTZ NULL;
ALTER TABLE users ADD COLUMN IF NOT EXISTS avatar_changed_at TIMESTAMPTZ NULL;
ALTER TABLE users ADD COLUMN IF NOT EXISTS phone_number VARCHAR(20) NULL;
ALTER TABLE users ADD COLUMN IF NOT EXISTS whatsapp_number VARCHAR(20) NULL;

-- Foto yang sudah ada dihitung sebagai perubahan terakhir.
UPDATE users SET avatar_changed_at = avatar_updated_at
WHERE avatar_changed_at IS NULL AND avatar_updated_at IS NOT NULL;
