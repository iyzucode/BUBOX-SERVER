-- 009_seed_weekly_menus.sql
-- Seed Master Menus for Nasi Tim, Sup, Bubur, Snack, Pelengkap
-- Assign menus across all 7 days of the week (Senin s/d Minggu)

DO $$
DECLARE
    v_bubur_id UUID := '34e8efb8-be36-456d-89b1-8cca88d1011b';
    v_nasitim_id UUID := '7c2c6589-da98-4310-bfd6-b9741e65abd5';
    v_sup_id UUID := '852ab500-aa84-40b2-be1b-c198702c573c';
    v_snack_id UUID := 'f705d124-8e23-4061-8ebb-9103eba1e0d2';
    v_pelengkap_id UUID := 'f9aaff46-88f2-4865-b82c-d51521c3fc39';

    -- Master menu IDs
    m_bubur_ayam UUID := '0f0a5624-7ae2-4410-bc86-e0c003ddf7df';
    m_bubur_salmon UUID := '5943cac9-fd48-41e7-879b-93c40bb09ada';
    m_bubur_sapi UUID := 'b1a11111-1111-4111-8111-111111111111';
    m_bubur_hati UUID := 'b2a22222-2222-4222-8222-222222222222';

    m_tim_ayam UUID := 'c1a11111-1111-4111-8111-111111111111';
    m_tim_dori UUID := 'c2a22222-2222-4222-8222-222222222222';
    m_tim_sapi UUID := 'c3a33333-3333-4333-8333-333333333333';

    m_sup_ayam UUID := 'd1a11111-1111-4111-8111-111111111111';
    m_sup_salmon UUID := 'd2a22222-2222-4222-8222-222222222222';
    m_sup_sapi UUID := 'd3a33333-3333-4333-8333-333333333333';

    m_snack_makaroni UUID := '33ef1904-ff4c-4fbe-be72-3ccd53a8ad27';
    m_snack_puding UUID := 'e1a11111-1111-4111-8111-111111111111';
    m_snack_puree UUID := 'e2a22222-2222-4222-8222-222222222222';

    m_pelengkap_pepes UUID := '61234a98-afdd-4c98-aa6d-107fdbaf48ef';
    m_pelengkap_perkedel UUID := 'f1a11111-1111-4111-8111-111111111111';
    m_pelengkap_nugget UUID := 'f2a22222-2222-4222-8222-222222222222';
BEGIN
    -- Resolve dynamic category IDs from rf_menu_categories
    SELECT id INTO v_bubur_id FROM rf_menu_categories WHERE code = 'BUBUR';
    SELECT id INTO v_nasitim_id FROM rf_menu_categories WHERE code = 'NASI_TIM';
    SELECT id INTO v_sup_id FROM rf_menu_categories WHERE code = 'SUP';
    SELECT id INTO v_snack_id FROM rf_menu_categories WHERE code = 'SNACK';
    SELECT id INTO v_pelengkap_id FROM rf_menu_categories WHERE code = 'PELENGKAP';

    -- 1. Insert Master Menus
    -- BUBUR
    INSERT INTO menus (id, category_id, name, description, ingredients, nutrition_info, price, is_active, is_deleted, created_at, updated_at)
    VALUES
    (m_bubur_sapi, v_bubur_id, 'Bubur Daging Sapi Bayam Labu', 'Bubur beras merah kaya zat besi dengan daging sapi cincang halus dan sari labu.', 'Beras merah, daging sapi giling, bayam hijau, labu kuning, kaldu sapi', 'Energi 165 kkal, Protein 7.2g, Zat Besi 2.8mg', 27000.00, true, false, NOW(), NOW()),
    (m_bubur_hati, v_bubur_id, 'Bubur Hati Sapi Brokoli Gurih', 'Bubur lembut dengan hati sapi pilihan tinggi zat besi pencegah stunting.', 'Beras organik, hati sapi segar, brokoli cincang, wortel, mentega tawar', 'Energi 170 kkal, Protein 8.0g, Zat Besi 3.5mg', 25000.00, true, false, NOW(), NOW())
    ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, price = EXCLUDED.price, is_deleted = false, is_active = true;

    -- NASI TIM
    INSERT INTO menus (id, category_id, name, description, ingredients, nutrition_info, price, is_active, is_deleted, created_at, updated_at)
    VALUES
    (m_tim_ayam, v_nasitim_id, 'Nasi Tim Ayam Jamur Gurih', 'Nasi tim pulen lembut dengan ayam cincang gurih dan jamur kancing bergizi.', 'Beras pulen, daging ayam kampung cincang, jamur champignon, daun bawang, minyak wijen', 'Energi 190 kkal, Protein 8.5g, Serat 2.1g', 28000.00, true, false, NOW(), NOW()),
    (m_tim_dori, v_nasitim_id, 'Nasi Tim Ikan Dori Wortel', 'Nasi tim lembut lumer di mulut kaya omega 3 dari ikan dori segar.', 'Beras pulen, fillet ikan dori, serutan wortel, seledri, kaldu ayam', 'Energi 180 kkal, Protein 8.0g, Omega-3 450mg', 29000.00, true, false, NOW(), NOW()),
    (m_tim_sapi, v_nasitim_id, 'Nasi Tim Daging Cincang Manis', 'Nasi tim dengan daging sapi cincang gurih manis alami kaya zat besi.', 'Beras organik, daging sapi cincang, kacang polong manis, kecap organik rendah natrium', 'Energi 205 kkal, Protein 9.0g, Zat Besi 3.1mg', 30000.00, true, false, NOW(), NOW())
    ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, price = EXCLUDED.price, is_deleted = false, is_active = true;

    -- SUP
    INSERT INTO menus (id, category_id, name, description, ingredients, nutrition_info, price, is_active, is_deleted, created_at, updated_at)
    VALUES
    (m_sup_ayam, v_sup_id, 'Sup Krim Ayam Jagung Manis', 'Sup kental hangat dengan sari jagung manis manis alami dan cincangan dada ayam.', 'Jagung manis pipil, dada ayam cincang, kaldu ayam kampung, susu pasteurisasi', 'Energi 155 kkal, Protein 6.8g, Kalsium 95mg', 25000.00, true, false, NOW(), NOW()),
    (m_sup_salmon, v_sup_id, 'Sup Brokoli Daging Ikan Salmon', 'Sup bening aromatik kaya DHA dan antioksidan untuk kecerdasan si kecil.', 'Fillet salmon segar, brokoli organik, tomat cherry, kaldu rumput laut', 'Energi 175 kkal, Protein 8.5g, DHA 600mg', 28000.00, true, false, NOW(), NOW()),
    (m_sup_sapi, v_sup_id, 'Sup Labu Daging Sapi Cincang', 'Sup labu kuning lembut gurih dengan serpihan daging sapi empuk.', 'Labu kuning kabocha, daging sapi giling empuk, wortel, seledri, kaldu iga', 'Energi 165 kkal, Protein 7.5g, Vitamin A 450mcg', 27000.00, true, false, NOW(), NOW())
    ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, price = EXCLUDED.price, is_deleted = false, is_active = true;

    -- SNACK
    INSERT INTO menus (id, category_id, name, description, ingredients, nutrition_info, price, is_active, is_deleted, created_at, updated_at)
    VALUES
    (m_snack_puding, v_snack_id, 'Puding Buah Naga Pisang', 'Puding serat alami buah naga merah dan pisang ambon manis menyegarkan.', 'Buah naga merah, pisang ambon matang, agar-agar rumput laut, air kelapa murni', 'Energi 85 kkal, Serat 2.5g, Vitamin C 15mg', 12000.00, true, false, NOW(), NOW()),
    (m_snack_puree, v_snack_id, 'Puree Alpukat Pisang Keju', 'Snack creamy penambah berat badan kaya lemak baik nabati dan kalsium keju.', 'Alpukat mentega matang, pisang cavendish, parutan keju cheddar baby-safe', 'Energi 140 kkal, Lemak Baik 8.2g, Kalsium 80mg', 14000.00, true, false, NOW(), NOW())
    ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, price = EXCLUDED.price, is_deleted = false, is_active = true;

    -- PELENGKAP
    INSERT INTO menus (id, category_id, name, description, ingredients, nutrition_info, price, is_active, is_deleted, created_at, updated_at)
    VALUES
    (m_pelengkap_perkedel, v_pelengkap_id, 'Perkedel Tahu Telur Puyuh', 'Perkedel lembut kaya protein nabati dan telur puyuh utuh di dalamnya.', 'Tahu susu lembut, telur puyuh rebus, daun seledri, bawang putih panggang', 'Energi 110 kkal, Protein 5.8g, Kolin 65mg', 12000.00, true, false, NOW(), NOW()),
    (m_pelengkap_nugget, v_pelengkap_id, 'Nugget Ayam Wortel Homemade', 'Nugget kukus ayam non-MSG dengan parutan wortel renyah sehat.', 'Daging dada ayam giling, wortel parut, tepung oat, telur ayam omega-3', 'Energi 135 kkal, Protein 7.0g, Vitamin A 220mcg', 16000.00, true, false, NOW(), NOW())
    ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, price = EXCLUDED.price, is_deleted = false, is_active = true;

    -- 2. Assignments for Day 1 (Senin)
    INSERT INTO menu_assignments (id, menu_id, day_of_week, day_name, created_at) VALUES
    (gen_random_uuid(), m_bubur_ayam, 1, 'Senin', NOW()),
    (gen_random_uuid(), m_tim_ayam, 1, 'Senin', NOW()),
    (gen_random_uuid(), m_sup_ayam, 1, 'Senin', NOW()),
    (gen_random_uuid(), m_snack_makaroni, 1, 'Senin', NOW()),
    (gen_random_uuid(), m_pelengkap_nugget, 1, 'Senin', NOW())
    ON CONFLICT (menu_id, day_of_week) DO NOTHING;

    -- 3. Assignments for Day 2 (Selasa)
    INSERT INTO menu_assignments (id, menu_id, day_of_week, day_name, created_at) VALUES
    (gen_random_uuid(), m_bubur_salmon, 2, 'Selasa', NOW()),
    (gen_random_uuid(), m_tim_dori, 2, 'Selasa', NOW()),
    (gen_random_uuid(), m_sup_salmon, 2, 'Selasa', NOW()),
    (gen_random_uuid(), m_snack_puding, 2, 'Selasa', NOW()),
    (gen_random_uuid(), m_pelengkap_pepes, 2, 'Selasa', NOW())
    ON CONFLICT (menu_id, day_of_week) DO NOTHING;

    -- 4. Assignments for Day 3 (Rabu)
    INSERT INTO menu_assignments (id, menu_id, day_of_week, day_name, created_at) VALUES
    (gen_random_uuid(), m_bubur_sapi, 3, 'Rabu', NOW()),
    (gen_random_uuid(), m_tim_sapi, 3, 'Rabu', NOW()),
    (gen_random_uuid(), m_sup_sapi, 3, 'Rabu', NOW()),
    (gen_random_uuid(), m_snack_puree, 3, 'Rabu', NOW()),
    (gen_random_uuid(), m_pelengkap_perkedel, 3, 'Rabu', NOW())
    ON CONFLICT (menu_id, day_of_week) DO NOTHING;

    -- 5. Assignments for Day 4 (Kamis)
    INSERT INTO menu_assignments (id, menu_id, day_of_week, day_name, created_at) VALUES
    (gen_random_uuid(), m_bubur_hati, 4, 'Kamis', NOW()),
    (gen_random_uuid(), m_tim_ayam, 4, 'Kamis', NOW()),
    (gen_random_uuid(), m_sup_ayam, 4, 'Kamis', NOW()),
    (gen_random_uuid(), m_snack_makaroni, 4, 'Kamis', NOW()),
    (gen_random_uuid(), m_pelengkap_pepes, 4, 'Kamis', NOW())
    ON CONFLICT (menu_id, day_of_week) DO NOTHING;

    -- 6. Assignments for Day 5 (Jumat)
    INSERT INTO menu_assignments (id, menu_id, day_of_week, day_name, created_at) VALUES
    (gen_random_uuid(), m_bubur_salmon, 5, 'Jumat', NOW()),
    (gen_random_uuid(), m_tim_dori, 5, 'Jumat', NOW()),
    (gen_random_uuid(), m_sup_salmon, 5, 'Jumat', NOW()),
    (gen_random_uuid(), m_snack_puding, 5, 'Jumat', NOW()),
    (gen_random_uuid(), m_pelengkap_nugget, 5, 'Jumat', NOW())
    ON CONFLICT (menu_id, day_of_week) DO NOTHING;

    -- 7. Assignments for Day 6 (Sabtu - TODAY!)
    INSERT INTO menu_assignments (id, menu_id, day_of_week, day_name, created_at) VALUES
    (gen_random_uuid(), m_bubur_ayam, 6, 'Sabtu', NOW()),
    (gen_random_uuid(), m_tim_ayam, 6, 'Sabtu', NOW()),
    (gen_random_uuid(), m_sup_ayam, 6, 'Sabtu', NOW()),
    (gen_random_uuid(), m_snack_puree, 6, 'Sabtu', NOW()),
    (gen_random_uuid(), m_pelengkap_perkedel, 6, 'Sabtu', NOW())
    ON CONFLICT (menu_id, day_of_week) DO NOTHING;

    -- 8. Assignments for Day 7 (Minggu)
    INSERT INTO menu_assignments (id, menu_id, day_of_week, day_name, created_at) VALUES
    (gen_random_uuid(), m_bubur_sapi, 7, 'Minggu', NOW()),
    (gen_random_uuid(), m_tim_sapi, 7, 'Minggu', NOW()),
    (gen_random_uuid(), m_sup_sapi, 7, 'Minggu', NOW()),
    (gen_random_uuid(), m_snack_makaroni, 7, 'Minggu', NOW()),
    (gen_random_uuid(), m_pelengkap_nugget, 7, 'Minggu', NOW())
    ON CONFLICT (menu_id, day_of_week) DO NOTHING;

END $$;
