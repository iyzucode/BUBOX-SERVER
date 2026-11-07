-- ==========================================================
-- File: 016_add_place_and_wilayah_to_user_addresses.sql
-- Description: Alamat dari Google Maps.
--   * place_id     : ID tempat Google (opsional, untuk referensi/perbaikan data).
--   * wilayah_code : kode kelurahan/desa resmi (rf_wilayah, format 34.01.01.2001) hasil pencocokan
--                    titik peta ke master wilayah. Dipakai untuk cek jangkauan delivery per kecamatan
--                    (4 segmen pertama kode = kode kecamatan) tanpa bergantung pada teks nama wilayah.
-- ==========================================================

ALTER TABLE user_addresses
    ADD COLUMN IF NOT EXISTS place_id VARCHAR(255) NULL,
    ADD COLUMN IF NOT EXISTS wilayah_code VARCHAR(13) NULL;

CREATE INDEX IF NOT EXISTS idx_user_addresses_wilayah_code ON user_addresses (wilayah_code);
