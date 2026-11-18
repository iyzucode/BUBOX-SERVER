-- "Tanpa zona" sebagai pilihan admin di Wilayah Antar.
--
-- Pilihan admin di suatu wilayah langsung berlaku ke semua wilayah di bawahnya. Wilayah di bawahnya tetap boleh
-- dikecualikan, termasuk dilepas dari zona induknya: baris dengan zone_id NULL berarti "wilayah ini tanpa zona".
-- v_order_day_delivery_assignments tidak perlu diubah: aturan paling spesifik yang NULL menghasilkan tanpa zona.

ALTER TABLE delivery_zone_areas ALTER COLUMN zone_id DROP NOT NULL;
