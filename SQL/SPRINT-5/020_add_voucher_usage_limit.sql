-- Kuota total pemakaian voucher (semua pengguna).
--   NULL = tanpa batas.
--   Yang dihitung hanya pemakaian aktif (released_at IS NULL): pesanan yang ditutup sebelum
--   dibayar mengembalikan kuotanya. Batas 1x per pengguna tetap berlaku terpisah.
-- Kuota ditegakkan saat pesanan dibuat dengan mengunci baris voucher (SELECT ... FOR UPDATE),
-- sehingga dua pesanan bersamaan tidak bisa sama-sama memakai sisa kuota terakhir.

ALTER TABLE vouchers ADD COLUMN IF NOT EXISTS usage_limit INT NULL;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_vouchers_usage_limit_positive') THEN
        ALTER TABLE vouchers
            ADD CONSTRAINT ck_vouchers_usage_limit_positive CHECK (usage_limit IS NULL OR usage_limit > 0);
    END IF;
END $$;

-- Menghitung pemakaian aktif per voucher dengan cepat.
CREATE INDEX IF NOT EXISTS idx_voucher_redemptions_active_voucher
    ON voucher_redemptions (voucher_id) WHERE released_at IS NULL;
