-- Area pengantaran & penugasan driver per wilayah.
--
--   rf_wilayah (master, TIDAK diubah)
--     ├── delivery_coverage_areas : wilayah terjangkau / tidak (pengaturan admin)
--     └── delivery_zone_areas     : wilayah → zona ("driver wilayah")
--   delivery_zones                : zona, mis. "Zona Utara"
--   delivery_zone_drivers         : zona → user driver (+ riwayat pergantian)
--
-- Aturan di tingkat mana pun (provinsi 2, kab/kota 5, kecamatan 8, kelurahan 13 karakter);
-- untuk satu alamat yang berlaku adalah aturan PALING SPESIFIK yang cocok.
--
-- Driver satu hari kirim TIDAK disimpan permanen, tapi dihitung dari pemetaan terkini
-- (v_order_day_delivery_assignments), sehingga perubahan pemetaan langsung berlaku juga untuk
-- pesanan yang sudah ada. Urutan prioritas:
--   1. dibekukan saat "Dibawa kurir" (dispatched_*)  → riwayat tidak berubah lagi
--   2. penugasan manual admin (driver_override_user_id)
--   3. alamat → zona paling spesifik → driver aktif zona itu
--   4. tidak ada → antrean "Belum ada driver"
--
-- PERHATIAN: 004_create_rf_wilayah_table.sql memakai DROP TABLE ... CASCADE. Menjalankannya ulang
-- akan ikut menghapus foreign key tabel-tabel di bawah. Pembaruan data wilayah berikutnya harus dibuat
-- sebagai migrasi baru dengan INSERT ... ON CONFLICT, bukan DROP.

-- 1. Kode wilayah alamat di pesanan (disalin saat pesanan dibuat; dasar penentuan zona/driver)
ALTER TABLE orders ADD COLUMN IF NOT EXISTS delivery_wilayah_code VARCHAR(13) NULL;
CREATE INDEX IF NOT EXISTS idx_orders_delivery_wilayah_code ON orders (delivery_wilayah_code);

UPDATE orders o
SET delivery_wilayah_code = a.wilayah_code
FROM user_addresses a
WHERE o.address_id = a.id
  AND o.delivery_wilayah_code IS NULL
  AND a.wilayah_code IS NOT NULL;

-- 2. Cakupan pengantaran
CREATE TABLE IF NOT EXISTS delivery_coverage_areas (
    wilayah_code VARCHAR(13) PRIMARY KEY REFERENCES rf_wilayah(kode) ON DELETE RESTRICT,
    is_covered BOOLEAN NOT NULL,
    updated_by UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 3. Zona ("driver wilayah")
CREATE TABLE IF NOT EXISTS delivery_zones (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(30) NOT NULL UNIQUE CHECK (code ~ '^[A-Z0-9_-]{2,30}$'),
    name VARCHAR(100) NOT NULL,
    description VARCHAR(300) NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_by UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    updated_by UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 4. Wilayah → zona (satu wilayah satu zona)
CREATE TABLE IF NOT EXISTS delivery_zone_areas (
    wilayah_code VARCHAR(13) PRIMARY KEY REFERENCES rf_wilayah(kode) ON DELETE RESTRICT,
    zone_id UUID NOT NULL REFERENCES delivery_zones(id) ON DELETE CASCADE,
    updated_by UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS idx_delivery_zone_areas_zone ON delivery_zone_areas (zone_id);

-- 5. Zona → user driver. Satu driver aktif per zona; baris lama disimpan sebagai riwayat.
CREATE TABLE IF NOT EXISTS delivery_zone_drivers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    zone_id UUID NOT NULL REFERENCES delivery_zones(id) ON DELETE CASCADE,
    driver_user_id UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    assigned_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    assigned_by UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    unassigned_at TIMESTAMPTZ NULL,
    unassigned_by UUID NULL REFERENCES users(id) ON DELETE SET NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_delivery_zone_drivers_active
    ON delivery_zone_drivers (zone_id) WHERE unassigned_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_delivery_zone_drivers_driver
    ON delivery_zone_drivers (driver_user_id) WHERE unassigned_at IS NULL;

-- 6. Penugasan per hari kirim: manual admin & data yang dibekukan saat "Dibawa kurir"
ALTER TABLE order_days
    ADD COLUMN IF NOT EXISTS driver_override_user_id UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    ADD COLUMN IF NOT EXISTS override_reason VARCHAR(300) NULL,
    ADD COLUMN IF NOT EXISTS override_by UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    ADD COLUMN IF NOT EXISTS override_at TIMESTAMPTZ NULL,
    ADD COLUMN IF NOT EXISTS dispatched_zone_id UUID NULL REFERENCES delivery_zones(id) ON DELETE SET NULL,
    ADD COLUMN IF NOT EXISTS dispatched_zone_name VARCHAR(100) NULL,
    ADD COLUMN IF NOT EXISTS dispatched_driver_user_id UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    ADD COLUMN IF NOT EXISTS dispatched_at TIMESTAMPTZ NULL;
CREATE INDEX IF NOT EXISTS idx_order_days_driver_override
    ON order_days (driver_override_user_id) WHERE driver_override_user_id IS NOT NULL;

-- 7. Penugasan efektif per hari kirim (hanya pesanan DELIVERY).
--    Zona: aturan delivery_zone_areas paling spesifik; zona nonaktif = tidak ada zona.
CREATE OR REPLACE VIEW v_order_day_delivery_assignments AS
SELECT
    d.id AS order_day_id,
    d.order_id,
    o.delivery_wilayah_code,
    CASE WHEN d.dispatched_at IS NOT NULL THEN d.dispatched_zone_id ELSE z.id END AS zone_id,
    CASE WHEN d.dispatched_at IS NOT NULL THEN d.dispatched_zone_name ELSE z.name END AS zone_name,
    CASE
        WHEN d.dispatched_at IS NOT NULL THEN d.dispatched_driver_user_id
        WHEN d.driver_override_user_id IS NOT NULL THEN d.driver_override_user_id
        ELSE zd.driver_user_id
    END AS driver_user_id,
    CASE
        WHEN d.dispatched_at IS NOT NULL THEN 'DISPATCHED'
        WHEN d.driver_override_user_id IS NOT NULL THEN 'MANUAL'
        WHEN zd.driver_user_id IS NOT NULL THEN 'ZONE'
        ELSE 'UNASSIGNED'
    END AS assignment_source
FROM order_days d
JOIN orders o ON o.id = d.order_id
LEFT JOIN LATERAL (
    SELECT za.zone_id
    FROM delivery_zone_areas za
    WHERE o.delivery_wilayah_code IS NOT NULL
      AND za.wilayah_code = ANY (ARRAY[
          LEFT(o.delivery_wilayah_code, 2),
          LEFT(o.delivery_wilayah_code, 5),
          LEFT(o.delivery_wilayah_code, 8),
          o.delivery_wilayah_code])
    ORDER BY LENGTH(za.wilayah_code) DESC
    LIMIT 1
) best ON TRUE
LEFT JOIN delivery_zones z ON z.id = best.zone_id AND z.is_active
LEFT JOIN delivery_zone_drivers zd ON zd.zone_id = z.id AND zd.unassigned_at IS NULL
WHERE o.fulfillment_type = 'DELIVERY';
