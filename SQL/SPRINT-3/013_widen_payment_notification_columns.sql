-- ==========================================================
-- 013_widen_payment_notification_columns.sql
-- payment_notifications adalah log audit webhook: harus bisa menyimpan payload apa pun.
-- order_id dari "Test notification URL" Midtrans lebih panjang dari 50 karakter,
-- sehingga VARCHAR(50) membuat webhook gagal (500).
-- ==========================================================

ALTER TABLE payment_notifications
    ALTER COLUMN provider_order_id TYPE VARCHAR(255),
    ALTER COLUMN transaction_status TYPE VARCHAR(100);
