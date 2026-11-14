-- Aturan ongkir per metode pemesanan (menggantikan DELIVERY_FEE yang hanya satu nominal).
--
-- Disimpan sebagai JSON di app_settings:
--   DELIVERY_FEE_RULE  : aturan Pesan Antar
--   PICKUP_FEE_RULE    : aturan Ambil Sendiri
--
-- Bentuk: {"mode": "FREE" | "FIXED" | "PER_DAY", "amount": <Rp, untuk FIXED>,
--          "tiers": [{"minDays": 1, "perDay": 2000}, {"minDays": 7, "perDay": 1500}, ...]}
--   FREE    : Rp 0
--   FIXED   : amount per pesanan
--   PER_DAY : jumlah hari kirim × tarif tingkat yang berlaku (tingkat dengan minDays terbesar ≤ jumlah hari).
--             Seluruh hari memakai tarif tingkat tersebut (bukan progresif).
--
-- Nominal & keterangan ongkir disalin ke pesanan (orders.delivery_fee, delivery_fee_note).

-- 1. Pesan Antar: pindahkan nilai ongkir tetap yang lama (bila ada) agar total pesanan tidak berubah
INSERT INTO app_settings (key, value, value_type, description)
SELECT
    'DELIVERY_FEE_RULE',
    CASE
        WHEN COALESCE(old.fee, 0) > 0 THEN '{"mode":"FIXED","amount":' || old.fee::text || ',"tiers":[]}'
        ELSE '{"mode":"FREE","amount":0,"tiers":[]}'
    END,
    'STRING',
    'Aturan ongkir Pesan Antar (JSON: mode FREE/FIXED/PER_DAY, amount, tiers minDays/perDay)'
FROM (
    SELECT (SELECT ROUND(value::numeric) FROM app_settings WHERE key = 'DELIVERY_FEE' AND value ~ '^[0-9]+(\.[0-9]+)?$') AS fee
) old
ON CONFLICT (key) DO NOTHING;

-- 2. Ambil Sendiri: gratis seperti sebelumnya
INSERT INTO app_settings (key, value, value_type, description)
VALUES ('PICKUP_FEE_RULE', '{"mode":"FREE","amount":0,"tiers":[]}', 'STRING',
        'Aturan biaya Ambil Sendiri (JSON: mode FREE/FIXED/PER_DAY, amount, tiers minDays/perDay)')
ON CONFLICT (key) DO NOTHING;

-- 3. Pengaturan lama sudah dipindahkan ke DELIVERY_FEE_RULE
DELETE FROM app_settings WHERE key = 'DELIVERY_FEE';

-- 4. Keterangan ongkir di pesanan, mis. "10 hari × Rp 1.500" (null bila gratis / nominal tetap)
ALTER TABLE orders ADD COLUMN IF NOT EXISTS delivery_fee_note VARCHAR(100) NULL;
