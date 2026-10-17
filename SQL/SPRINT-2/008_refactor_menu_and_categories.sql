-- 008_refactor_menu_and_categories.sql
-- Migrasi refactoring kategori produk, master menu, dan penugasan jadwal harian

-- 1. Buat Tabel Referensi Kategori Menu (rf_menu_categories)
CREATE TABLE IF NOT EXISTS rf_menu_categories (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(30) NOT NULL UNIQUE,
    name VARCHAR(50) NOT NULL,
    category_type VARCHAR(20) NOT NULL CHECK (category_type IN ('UTAMA', 'SEKUNDER')),
    display_order INT NOT NULL DEFAULT 1,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at TIMESTAMPTZ NULL
);

CREATE INDEX IF NOT EXISTS idx_rf_menu_categories_type ON rf_menu_categories(category_type) WHERE is_deleted = false;
CREATE INDEX IF NOT EXISTS idx_rf_menu_categories_active ON rf_menu_categories(is_active) WHERE is_deleted = false;

-- 2. Seed Data Kategori Default
INSERT INTO rf_menu_categories (code, name, category_type, display_order)
VALUES 
    ('BUBUR', 'Bubur', 'UTAMA', 1),
    ('NASI_TIM', 'Nasi Tim', 'UTAMA', 2),
    ('SUP', 'Sup', 'UTAMA', 3),
    ('SNACK', 'Snack', 'SEKUNDER', 4),
    ('PELENGKAP', 'Pelengkap', 'SEKUNDER', 5)
ON CONFLICT (code) DO NOTHING;

-- 3. Tambahkan Kolom Baru dan Soft Delete ke Tabel menus
ALTER TABLE menus 
    ADD COLUMN IF NOT EXISTS category_id UUID REFERENCES rf_menu_categories(id) ON DELETE RESTRICT,
    ADD COLUMN IF NOT EXISTS is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS deleted_at TIMESTAMPTZ NULL;

-- Migrasikan data kategori eksisting ke category_id berdasarkan nama/kode
UPDATE menus m
SET category_id = c.id
FROM rf_menu_categories c
WHERE m.category_id IS NULL 
  AND (UPPER(m.category) = c.code OR UPPER(REPLACE(m.category, ' ', '_')) = c.code);

-- Fallback jika ada yang belum terpetakan, default ke kategori BUBUR
UPDATE menus 
SET category_id = (SELECT id FROM rf_menu_categories WHERE code = 'BUBUR' LIMIT 1)
WHERE category_id IS NULL;

-- Kunci category_id menjadi NOT NULL
ALTER TABLE menus ALTER COLUMN category_id SET NOT NULL;

-- 4. Buat Tabel Penugasan Jadwal Harian (menu_assignments) - Hard Delete
CREATE TABLE IF NOT EXISTS menu_assignments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    menu_id UUID NOT NULL REFERENCES menus(id) ON DELETE RESTRICT,
    day_of_week INT NOT NULL CHECK (day_of_week BETWEEN 1 AND 7), -- 1: Senin s/d 7: Minggu
    day_name VARCHAR(20) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_menu_assignments UNIQUE (menu_id, day_of_week)
);

CREATE INDEX IF NOT EXISTS idx_menu_assignments_day ON menu_assignments(day_of_week);
CREATE INDEX IF NOT EXISTS idx_menu_assignments_menu ON menu_assignments(menu_id);

-- 5. Migrasikan Data Jadwal Harian Eksisting dari Tabel menus ke menu_assignments
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_name = 'menus' AND column_name = 'day_of_week'
    ) THEN
        INSERT INTO menu_assignments (menu_id, day_of_week, day_name, created_at)
        SELECT id, day_of_week, day_name, created_at
        FROM menus
        ON CONFLICT (menu_id, day_of_week) DO NOTHING;
    END IF;
END $$;

-- 6. Hapus Indeks Lama dan Buat Indeks Baru untuk menus
DROP INDEX IF EXISTS idx_menus_day_type;
DROP INDEX IF EXISTS idx_menus_category;

CREATE INDEX IF NOT EXISTS idx_menus_category_id ON menus(category_id);
CREATE UNIQUE INDEX IF NOT EXISTS uq_menus_category_name 
ON menus (category_id, LOWER(TRIM(name))) 
WHERE is_deleted = false;

-- 7. Lepaskan Kolom Redundan dari Tabel menus
ALTER TABLE menus 
    DROP COLUMN IF EXISTS day_of_week,
    DROP COLUMN IF EXISTS day_name,
    DROP COLUMN IF EXISTS menu_type,
    DROP COLUMN IF EXISTS category;
