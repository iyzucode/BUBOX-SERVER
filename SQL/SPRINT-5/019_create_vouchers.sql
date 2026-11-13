-- Voucher / kode promo.
--
-- Aturan bisnis:
--   * Diskon dihitung dari subtotal menu (ongkir tidak didiskon); satu pesanan maksimal satu voucher.
--   * PERCENT: discount_value = persen (1-100), dibatasi max_discount bila diisi.
--     FIXED  : discount_value = potongan Rupiah.
--   * Setiap pengguna hanya boleh memakai satu voucher sekali (dijamin index unik parsial di
--     voucher_redemptions). Bila pesanan ditutup sebelum dibayar (batal / kedaluwarsa / gagal bayar),
--     pemakaian dilepas (released_at diisi) sehingga voucher bisa dipakai lagi.
--   * is_listed = false → voucher "rahasia": tidak tampil di daftar, hanya bisa dipakai dengan
--     mengetik kodenya.

CREATE TABLE IF NOT EXISTS vouchers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(30) NOT NULL CHECK (code ~ '^[A-Z0-9_-]{3,30}$'),
    title VARCHAR(100) NOT NULL,
    description VARCHAR(300) NULL,
    discount_type VARCHAR(10) NOT NULL CHECK (discount_type IN ('PERCENT', 'FIXED')),
    discount_value NUMERIC(12, 2) NOT NULL CHECK (discount_value > 0),
    max_discount NUMERIC(12, 2) NULL CHECK (max_discount IS NULL OR max_discount > 0),
    min_subtotal NUMERIC(12, 2) NOT NULL DEFAULT 0 CHECK (min_subtotal >= 0),
    payment_methods VARCHAR(20)[] NOT NULL DEFAULT ARRAY['QRIS']::VARCHAR(20)[]
        CHECK (cardinality(payment_methods) > 0 AND payment_methods <@ ARRAY['QRIS', 'CASH']::VARCHAR(20)[]),
    starts_at TIMESTAMPTZ NOT NULL,
    ends_at TIMESTAMPTZ NOT NULL,
    is_listed BOOLEAN NOT NULL DEFAULT TRUE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_by UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    updated_by UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CHECK (ends_at > starts_at),
    CHECK (discount_type <> 'PERCENT' OR discount_value <= 100)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_vouchers_code ON vouchers (code);
CREATE INDEX IF NOT EXISTS idx_vouchers_listed_window ON vouchers (ends_at) WHERE is_active AND is_listed;

CREATE TABLE IF NOT EXISTS voucher_redemptions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    voucher_id UUID NOT NULL REFERENCES vouchers(id) ON DELETE RESTRICT,
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    order_id UUID NOT NULL UNIQUE REFERENCES orders(id) ON DELETE CASCADE,
    discount_amount NUMERIC(12, 2) NOT NULL CHECK (discount_amount > 0),
    released_at TIMESTAMPTZ NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 1x per pengguna: hanya boleh ada satu pemakaian aktif (belum dilepas) per voucher per pengguna.
CREATE UNIQUE INDEX IF NOT EXISTS ux_voucher_redemptions_active
    ON voucher_redemptions (voucher_id, user_id) WHERE released_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_voucher_redemptions_user ON voucher_redemptions (user_id) WHERE released_at IS NULL;

-- Snapshot kode voucher di pesanan (untuk ditampilkan di rincian pesanan).
ALTER TABLE orders ADD COLUMN IF NOT EXISTS voucher_code VARCHAR(30) NULL;
