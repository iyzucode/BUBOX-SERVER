-- 010_add_coordinates_to_user_addresses.sql
-- Migrasi penambahan kolom titik koordinat (latitude & longitude) pada tabel user_addresses

ALTER TABLE user_addresses
ADD COLUMN IF NOT EXISTS latitude DOUBLE PRECISION NULL,
ADD COLUMN IF NOT EXISTS longitude DOUBLE PRECISION NULL;

-- Index opsional untuk pencarian lokasi
CREATE INDEX IF NOT EXISTS idx_user_addresses_coordinates ON user_addresses(latitude, longitude);
