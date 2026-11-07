-- ==========================================================
-- File: 014_harden_user_otps.sql
-- Description: Pengamanan OTP email
--   * Kode OTP tidak lagi disimpan apa adanya: kolom otp_code diganti otp_hash
--     (SHA-256 dari kode + id user, heksadesimal 64 karakter).
--   * attempt_count: jumlah salah tebak; OTP hangus setelah batas tercapai.
--   * Semua OTP lama yang masih aktif dibatalkan karena tersimpan dalam teks biasa.
-- ==========================================================

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'user_otps' AND column_name = 'otp_code'
    ) THEN
        ALTER TABLE user_otps RENAME COLUMN otp_code TO otp_hash;
    END IF;
END $$;

ALTER TABLE user_otps ALTER COLUMN otp_hash TYPE VARCHAR(64);

ALTER TABLE user_otps
    ADD COLUMN IF NOT EXISTS attempt_count INT NOT NULL DEFAULT 0;

-- OTP lama berisi kode asli (bukan hash): batalkan semuanya.
UPDATE user_otps SET is_used = TRUE WHERE is_used = FALSE;

-- Untuk cek jeda kirim ulang & kuota per jam
CREATE INDEX IF NOT EXISTS idx_user_otps_recent
    ON user_otps (user_id, type, created_at DESC);
