-- ==========================================================
-- 011_create_app_settings.sql
-- Pengaturan aplikasi yang bisa diubah admin dari dashboard
-- (cutoff pemesanan, batas waktu bayar, ongkir)
-- ==========================================================

CREATE TABLE IF NOT EXISTS app_settings (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    key VARCHAR(100) NOT NULL UNIQUE,
    value VARCHAR(500) NOT NULL,
    value_type VARCHAR(20) NOT NULL CHECK (value_type IN ('INT', 'DECIMAL', 'TIME', 'STRING', 'BOOL')),
    description VARCHAR(255),
    updated_by UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Seed nilai default (tidak menimpa nilai yang sudah diubah admin)
INSERT INTO app_settings (key, value, value_type, description)
VALUES
    ('ORDER_CUTOFF_DAYS_BEFORE', '1', 'INT', 'Batas pemesanan: berapa hari sebelum tanggal kirim (H-N)'),
    ('ORDER_CUTOFF_TIME', '17:00', 'TIME', 'Batas pemesanan: jam cutoff (format HH:mm) pada hari H-N'),
    ('ORDER_TIMEZONE', 'Asia/Jakarta', 'STRING', 'Zona waktu acuan cutoff dan tanggal kirim'),
    ('PAYMENT_TIMEOUT_MINUTES', '60', 'INT', 'Batas waktu pembayaran QRIS dalam menit'),
    ('DELIVERY_FEE', '0', 'DECIMAL', 'Ongkos kirim per order (Rupiah)')
ON CONFLICT (key) DO NOTHING;
