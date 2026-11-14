-- Biaya layanan & pajak pesanan.
--
--   dasar          = subtotal menu − diskon voucher
--   biaya layanan  = PERCENT: ceil(dasar × %)   | FIXED: nominal tetap per pesanan
--   pajak          = ceil((dasar + biaya layanan) × %)
--   total          = dasar + biaya layanan + pajak + ongkir
--
-- Berlaku untuk Pesan Antar & Ambil Sendiri. Nilai default 0 = tidak dikenakan.
-- Nominal & tarif disalin ke tiap pesanan, sehingga perubahan pengaturan tidak mengubah pesanan lama.

INSERT INTO app_settings (key, value, value_type, description)
VALUES
    ('SERVICE_FEE_TYPE', 'FIXED', 'STRING', 'Jenis biaya layanan: PERCENT (persen dari subtotal - diskon) atau FIXED (Rupiah per pesanan)'),
    ('SERVICE_FEE_VALUE', '0', 'DECIMAL', 'Besar biaya layanan: persen (maks 2 desimal) atau Rupiah, sesuai SERVICE_FEE_TYPE'),
    ('TAX_PERCENT', '0', 'DECIMAL', 'Pajak (persen, maks 2 desimal) dari subtotal - diskon + biaya layanan')
ON CONFLICT (key) DO NOTHING;

ALTER TABLE orders ADD COLUMN IF NOT EXISTS service_fee NUMERIC(12, 2) NOT NULL DEFAULT 0 CHECK (service_fee >= 0);
-- Terisi bila biaya layanan berupa persen (untuk label "Biaya layanan (5%)"); null = nominal tetap
ALTER TABLE orders ADD COLUMN IF NOT EXISTS service_fee_percent NUMERIC(5, 2) NULL CHECK (service_fee_percent BETWEEN 0 AND 100);
ALTER TABLE orders ADD COLUMN IF NOT EXISTS tax_amount NUMERIC(12, 2) NOT NULL DEFAULT 0 CHECK (tax_amount >= 0);
ALTER TABLE orders ADD COLUMN IF NOT EXISTS tax_percent NUMERIC(5, 2) NOT NULL DEFAULT 0 CHECK (tax_percent BETWEEN 0 AND 100);
