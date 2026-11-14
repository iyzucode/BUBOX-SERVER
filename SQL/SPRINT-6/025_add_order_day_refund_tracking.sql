-- Pencatatan refund manual per hari kirim.
--
-- requires_manual_refund (sudah ada) = hari ini dibatalkan setelah dibayar dan harus direfund di luar sistem.
-- Kolom baru mencatat bahwa refund itu sudah dilakukan admin: kapan, oleh siapa, dan catatannya
-- (mis. "Transfer BCA 14/11, ref 123"). Refund tertunda = requires_manual_refund AND refunded_at IS NULL.

ALTER TABLE order_days ADD COLUMN IF NOT EXISTS refunded_at TIMESTAMPTZ NULL;
ALTER TABLE order_days ADD COLUMN IF NOT EXISTS refunded_by UUID NULL REFERENCES users(id) ON DELETE SET NULL;
ALTER TABLE order_days ADD COLUMN IF NOT EXISTS refund_note VARCHAR(300) NULL;

-- Daftar refund yang masih tertunda (tab "Perlu Tindakan")
CREATE INDEX IF NOT EXISTS idx_order_days_refund_pending
    ON order_days (delivery_date)
    WHERE requires_manual_refund AND refunded_at IS NULL;
