-- 007_create_menus_table.sql
-- Tabel manajemen menu harian MPASI Bubox (7 hari rotasi: Senin s/d Minggu)

CREATE TABLE IF NOT EXISTS menus (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    day_of_week INT NOT NULL CHECK (day_of_week BETWEEN 1 AND 7), -- 1: Senin, 2: Selasa, ..., 7: Minggu
    day_name VARCHAR(20) NOT NULL,                                -- 'Senin', 'Selasa', 'Rabu', 'Kamis', 'Jumat', 'Sabtu', 'Minggu'
    menu_type VARCHAR(20) NOT NULL CHECK (menu_type IN ('UTAMA', 'SEKUNDER')), -- 'UTAMA' (berbeda tiap hari), 'SEKUNDER' (fleksibel)
    category VARCHAR(50) NOT NULL,                                -- 'Bubur', 'Nasi Tim', 'Sup', 'Snack', 'Pelengkap'
    name VARCHAR(150) NOT NULL,                                   -- Nama menu hidangan
    description TEXT,                                             -- Deskripsi menu
    ingredients TEXT,                                             -- Komposisi / bahan pangan
    nutrition_info TEXT,                                          -- Informasi gizi / makronutrien
    price NUMERIC(12, 2) NOT NULL DEFAULT 0,                      -- Harga satuan
    image_url TEXT,                                               -- URL foto hidangan
    is_active BOOLEAN NOT NULL DEFAULT TRUE,                      -- Status aktif / tampil
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Indeks untuk pencarian cepat berdasarkan hari dan tipe menu
CREATE INDEX IF NOT EXISTS idx_menus_day_type ON menus(day_of_week, menu_type);
CREATE INDEX IF NOT EXISTS idx_menus_category ON menus(category);
CREATE INDEX IF NOT EXISTS idx_menus_is_active ON menus(is_active);
