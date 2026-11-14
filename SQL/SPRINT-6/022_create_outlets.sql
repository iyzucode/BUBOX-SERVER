-- Outlet tempat customer mengambil pesanan (metode PICKUP / "Ambil Sendiri").
--
--   outlets                 : dikelola admin (nama, alamat, wilayah kelurahan, jam ambil mulai-selesai, WA, titik peta)
--   orders.outlet_id        : outlet yang dipilih customer (hanya PICKUP)
--   orders.outlet_snapshot  : salinan data outlet saat pesanan dibuat, agar riwayat pesanan tidak
--                             berubah bila outlet diubah kemudian
--
-- Outlet yang sudah dipakai pesanan tidak bisa dihapus (FK RESTRICT); cukup dinonaktifkan.

CREATE TABLE IF NOT EXISTS outlets (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(100) NOT NULL,
    full_address VARCHAR(500) NOT NULL,
    -- Kelurahan/desa (13 karakter, mis. 34.01.01.2001)
    wilayah_code VARCHAR(13) NOT NULL REFERENCES rf_wilayah(kode) ON DELETE RESTRICT,
    -- Jam ambil (WIB): keduanya kosong atau keduanya terisi
    pickup_start_time TIME NULL,
    pickup_end_time TIME NULL,
    whatsapp_number VARCHAR(20) NULL,
    latitude DOUBLE PRECISION NULL CHECK (latitude BETWEEN -90 AND 90),
    longitude DOUBLE PRECISION NULL CHECK (longitude BETWEEN -180 AND 180),
    sort_order INT NOT NULL DEFAULT 0,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_by UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    updated_by UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_outlets_coordinates CHECK ((latitude IS NULL) = (longitude IS NULL)),
    CONSTRAINT chk_outlets_pickup_time CHECK (
        (pickup_start_time IS NULL) = (pickup_end_time IS NULL)
        AND (pickup_end_time IS NULL OR pickup_end_time > pickup_start_time))
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_outlets_name ON outlets (LOWER(name));
CREATE INDEX IF NOT EXISTS idx_outlets_active_sort ON outlets (sort_order, name) WHERE is_active;

ALTER TABLE orders ADD COLUMN IF NOT EXISTS outlet_id UUID NULL REFERENCES outlets(id) ON DELETE RESTRICT;
ALTER TABLE orders ADD COLUMN IF NOT EXISTS outlet_snapshot JSONB NULL;
CREATE INDEX IF NOT EXISTS idx_orders_outlet ON orders (outlet_id) WHERE outlet_id IS NOT NULL;
-- Dipakai untuk "outlet terakhir" customer
CREATE INDEX IF NOT EXISTS idx_orders_user_outlet_recent ON orders (user_id, created_at DESC) WHERE outlet_id IS NOT NULL;

-- Pesanan PICKUP baru wajib punya outlet. NOT VALID: pesanan PICKUP lama (sebelum ada outlet) dibiarkan.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_orders_pickup_outlet') THEN
        ALTER TABLE orders
            ADD CONSTRAINT chk_orders_pickup_outlet
            CHECK (fulfillment_type <> 'PICKUP' OR outlet_snapshot IS NOT NULL) NOT VALID;
    END IF;
END $$;
