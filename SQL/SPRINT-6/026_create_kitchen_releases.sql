-- Rilis daftar masak ke dapur per tanggal kirim.
--
-- Admin memvalidasi pesanan H-1 (setelah cutoff + batas waktu bayar) lalu merilisnya ke dapur.
-- Dapur hanya melihat tanggal yang sudah dirilis. Batalkan rilis = hapus barisnya; rilis ulang = upsert.
-- day_count & portions = snapshot jumlah hari kirim & porsi saat dirilis.

CREATE TABLE IF NOT EXISTS kitchen_releases (
    delivery_date DATE PRIMARY KEY,
    released_at TIMESTAMPTZ NOT NULL,
    released_by UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    day_count INT NOT NULL DEFAULT 0 CHECK (day_count >= 0),
    portions INT NOT NULL DEFAULT 0 CHECK (portions >= 0)
);
