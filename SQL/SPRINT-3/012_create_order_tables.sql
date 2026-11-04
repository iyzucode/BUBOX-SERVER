-- ==========================================================
-- 012_create_order_tables.sql
-- Modul Order: status (state machine), order, hari kirim, item,
-- pembayaran QRIS Midtrans, notifikasi webhook, dan riwayat status.
-- Lihat docs/ORDER_FLOW_PLANNING.md
-- ==========================================================

-- 1. Referensi status Order & OrderDay
CREATE TABLE IF NOT EXISTS rf_order_statuses (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    scope VARCHAR(10) NOT NULL CHECK (scope IN ('ORDER', 'DAY')),
    code VARCHAR(40) NOT NULL,
    name VARCHAR(100) NOT NULL,
    status_group VARCHAR(10) NOT NULL CHECK (status_group IN ('NORMAL', 'CANCEL')),
    sort_order INT NOT NULL,
    is_terminal BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_rf_order_statuses UNIQUE (scope, code)
);

INSERT INTO rf_order_statuses (scope, code, name, status_group, sort_order, is_terminal)
VALUES
    ('ORDER', 'AWAITING_PAYMENT',      'Menunggu Pembayaran',             'NORMAL', 10,  FALSE),
    ('ORDER', 'PAID',                  'Dibayar',                         'NORMAL', 20,  FALSE),
    ('ORDER', 'IN_PROGRESS',           'Diproses',                        'NORMAL', 30,  FALSE),
    ('ORDER', 'COMPLETED',             'Selesai',                         'NORMAL', 40,  TRUE),
    ('ORDER', 'CANCELLED_BY_CUSTOMER', 'Dibatalkan Customer',             'CANCEL', 910, TRUE),
    ('ORDER', 'CANCELLED_BY_ADMIN',    'Dibatalkan Admin',                'CANCEL', 920, TRUE),
    ('ORDER', 'EXPIRED',               'Kedaluwarsa',                     'CANCEL', 930, TRUE),
    ('ORDER', 'PAYMENT_FAILED',        'Pembayaran Gagal',                'CANCEL', 940, TRUE),
    ('DAY',   'SCHEDULED',             'Terjadwal',                       'NORMAL', 10,  FALSE),
    ('DAY',   'CONFIRMED',             'Terkonfirmasi',                   'NORMAL', 20,  FALSE),
    ('DAY',   'PREPARING',             'Sedang Dimasak',                  'NORMAL', 30,  FALSE),
    ('DAY',   'READY',                 'Siap',                            'NORMAL', 40,  FALSE),
    ('DAY',   'OUT_FOR_DELIVERY',      'Dalam Pengiriman',                'NORMAL', 50,  FALSE),
    ('DAY',   'DELIVERED',             'Terkirim',                        'NORMAL', 60,  TRUE),
    ('DAY',   'PICKED_UP',             'Sudah Diambil',                   'NORMAL', 61,  TRUE),
    ('DAY',   'CANCELLED',             'Dibatalkan',                      'CANCEL', 910, TRUE),
    ('DAY',   'CANCELLED_BY_ADMIN',    'Dibatalkan Admin (Force Majeure)', 'CANCEL', 920, TRUE)
ON CONFLICT (scope, code) DO UPDATE
SET name = EXCLUDED.name,
    status_group = EXCLUDED.status_group,
    sort_order = EXCLUDED.sort_order,
    is_terminal = EXCLUDED.is_terminal;

-- 2. Transisi status yang diizinkan beserta role pelakunya
--    allowed_roles memakai kode role (rf_roles.code) + 'SYSTEM' untuk proses otomatis
CREATE TABLE IF NOT EXISTS rf_order_status_transitions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    scope VARCHAR(10) NOT NULL CHECK (scope IN ('ORDER', 'DAY')),
    from_code VARCHAR(40) NOT NULL,
    to_code VARCHAR(40) NOT NULL,
    allowed_roles TEXT[] NOT NULL,
    description VARCHAR(255),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_rf_order_status_transitions UNIQUE (scope, from_code, to_code),
    CONSTRAINT fk_transition_from FOREIGN KEY (scope, from_code) REFERENCES rf_order_statuses(scope, code),
    CONSTRAINT fk_transition_to FOREIGN KEY (scope, to_code) REFERENCES rf_order_statuses(scope, code)
);

INSERT INTO rf_order_status_transitions (scope, from_code, to_code, allowed_roles, description)
VALUES
    -- ORDER
    ('ORDER', 'AWAITING_PAYMENT', 'PAID',                  ARRAY['SYSTEM'],                  'QRIS settlement dari Midtrans'),
    ('ORDER', 'AWAITING_PAYMENT', 'CANCELLED_BY_CUSTOMER', ARRAY['CUSTOMER'],                'Customer membatalkan sebelum bayar'),
    ('ORDER', 'AWAITING_PAYMENT', 'CANCELLED_BY_ADMIN',    ARRAY['ADMIN', 'SUPER_ADMIN'],    'Admin membatalkan sebelum bayar'),
    ('ORDER', 'AWAITING_PAYMENT', 'EXPIRED',               ARRAY['SYSTEM'],                  'Melewati batas waktu bayar'),
    ('ORDER', 'AWAITING_PAYMENT', 'PAYMENT_FAILED',        ARRAY['SYSTEM'],                  'Midtrans deny/cancel/failure'),
    ('ORDER', 'PAID',             'IN_PROGRESS',           ARRAY['SYSTEM'],                  'Hari pertama mulai dimasak'),
    ('ORDER', 'PAID',             'COMPLETED',             ARRAY['SYSTEM'],                  'Semua hari selesai'),
    ('ORDER', 'PAID',             'CANCELLED_BY_ADMIN',    ARRAY['SYSTEM'],                  'Semua hari dibatalkan karena force majeure'),
    ('ORDER', 'IN_PROGRESS',      'COMPLETED',             ARRAY['SYSTEM'],                  'Semua hari selesai'),
    ('ORDER', 'IN_PROGRESS',      'CANCELLED_BY_ADMIN',    ARRAY['SYSTEM'],                  'Semua hari dibatalkan karena force majeure'),
    -- DAY
    ('DAY', 'SCHEDULED',        'CONFIRMED',          ARRAY['SYSTEM'],                                  'Order dibayar'),
    ('DAY', 'SCHEDULED',        'CANCELLED',          ARRAY['SYSTEM'],                                  'Order batal/expired/gagal bayar'),
    ('DAY', 'CONFIRMED',        'PREPARING',          ARRAY['KITCHEN', 'ADMIN', 'SUPER_ADMIN'],         'Mulai dimasak'),
    ('DAY', 'PREPARING',        'READY',              ARRAY['KITCHEN', 'ADMIN', 'SUPER_ADMIN'],         'Siap diantar / diambil'),
    ('DAY', 'READY',            'OUT_FOR_DELIVERY',   ARRAY['DRIVER', 'ADMIN', 'SUPER_ADMIN'],          'Dibawa kurir (DELIVERY)'),
    ('DAY', 'OUT_FOR_DELIVERY', 'DELIVERED',          ARRAY['DRIVER', 'ADMIN', 'SUPER_ADMIN'],          'Sampai ke customer (DELIVERY)'),
    ('DAY', 'READY',            'PICKED_UP',          ARRAY['ADMIN', 'SUPER_ADMIN'],                    'Diambil customer (PICKUP)'),
    ('DAY', 'CONFIRMED',        'CANCELLED_BY_ADMIN', ARRAY['ADMIN', 'SUPER_ADMIN'],                    'Force majeure'),
    ('DAY', 'PREPARING',        'CANCELLED_BY_ADMIN', ARRAY['ADMIN', 'SUPER_ADMIN'],                    'Force majeure'),
    ('DAY', 'READY',            'CANCELLED_BY_ADMIN', ARRAY['ADMIN', 'SUPER_ADMIN'],                    'Force majeure'),
    ('DAY', 'OUT_FOR_DELIVERY', 'CANCELLED_BY_ADMIN', ARRAY['ADMIN', 'SUPER_ADMIN'],                    'Force majeure')
ON CONFLICT (scope, from_code, to_code) DO UPDATE
SET allowed_roles = EXCLUDED.allowed_roles,
    description = EXCLUDED.description;

-- 3. Orders (level kontrak & pembayaran)
CREATE TABLE IF NOT EXISTS orders (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    order_number VARCHAR(30) NOT NULL UNIQUE,
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    status_code VARCHAR(40) NOT NULL CHECK (status_code IN (
        'AWAITING_PAYMENT', 'PAID', 'IN_PROGRESS', 'COMPLETED',
        'CANCELLED_BY_CUSTOMER', 'CANCELLED_BY_ADMIN', 'EXPIRED', 'PAYMENT_FAILED')),
    fulfillment_type VARCHAR(20) NOT NULL CHECK (fulfillment_type IN ('DELIVERY', 'PICKUP')),
    address_id UUID NULL REFERENCES user_addresses(id) ON DELETE SET NULL,
    address_snapshot JSONB NULL,
    subtotal NUMERIC(12, 2) NOT NULL CHECK (subtotal >= 0),
    discount_amount NUMERIC(12, 2) NOT NULL DEFAULT 0 CHECK (discount_amount >= 0),
    delivery_fee NUMERIC(12, 2) NOT NULL DEFAULT 0 CHECK (delivery_fee >= 0),
    grand_total NUMERIC(12, 2) NOT NULL CHECK (grand_total >= 0),
    notes VARCHAR(500) NULL,
    payment_due_at TIMESTAMPTZ NOT NULL,
    paid_at TIMESTAMPTZ NULL,
    completed_at TIMESTAMPTZ NULL,
    cancelled_at TIMESTAMPTZ NULL,
    cancel_reason VARCHAR(500) NULL,
    cancelled_by UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_orders_delivery_address CHECK (fulfillment_type = 'PICKUP' OR address_snapshot IS NOT NULL)
);

CREATE INDEX IF NOT EXISTS idx_orders_user_created ON orders(user_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_orders_status ON orders(status_code);
CREATE INDEX IF NOT EXISTS idx_orders_awaiting_due ON orders(payment_due_at) WHERE status_code = 'AWAITING_PAYMENT';

-- 4. Order days (level operasional per tanggal kirim)
CREATE TABLE IF NOT EXISTS order_days (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id UUID NOT NULL REFERENCES orders(id) ON DELETE RESTRICT,
    delivery_date DATE NOT NULL,
    day_of_week SMALLINT NOT NULL CHECK (day_of_week BETWEEN 1 AND 7),
    status_code VARCHAR(40) NOT NULL CHECK (status_code IN (
        'SCHEDULED', 'CONFIRMED', 'PREPARING', 'READY', 'OUT_FOR_DELIVERY',
        'DELIVERED', 'PICKED_UP', 'CANCELLED', 'CANCELLED_BY_ADMIN')),
    day_subtotal NUMERIC(12, 2) NOT NULL CHECK (day_subtotal >= 0),
    requires_manual_refund BOOLEAN NOT NULL DEFAULT FALSE,
    cancel_reason VARCHAR(500) NULL,
    cancelled_by UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    cancelled_at TIMESTAMPTZ NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_order_days_order_date UNIQUE (order_id, delivery_date)
);

CREATE INDEX IF NOT EXISTS idx_order_days_date_status ON order_days(delivery_date, status_code);

-- 5. Order items (snapshot nama & harga menu)
CREATE TABLE IF NOT EXISTS order_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    order_day_id UUID NOT NULL REFERENCES order_days(id) ON DELETE RESTRICT,
    menu_id UUID NOT NULL REFERENCES menus(id) ON DELETE RESTRICT,
    menu_name VARCHAR(255) NOT NULL,
    category_id UUID NOT NULL,
    category_code VARCHAR(30) NOT NULL,
    category_name VARCHAR(50) NOT NULL,
    unit_price NUMERIC(12, 2) NOT NULL CHECK (unit_price >= 0),
    qty INT NOT NULL CHECK (qty BETWEEN 1 AND 20),
    line_total NUMERIC(12, 2) NOT NULL CHECK (line_total >= 0),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_order_items_day_menu UNIQUE (order_day_id, menu_id)
);

CREATE INDEX IF NOT EXISTS idx_order_items_day ON order_items(order_day_id);

-- 6. Payments (1 order = 1 transaksi QRIS Midtrans)
CREATE TABLE IF NOT EXISTS payments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id UUID NOT NULL UNIQUE REFERENCES orders(id) ON DELETE RESTRICT,
    provider VARCHAR(20) NOT NULL DEFAULT 'MIDTRANS',
    provider_order_id VARCHAR(50) NOT NULL UNIQUE,
    payment_type VARCHAR(20) NOT NULL DEFAULT 'qris',
    transaction_id VARCHAR(100) NULL,
    transaction_status VARCHAR(30) NOT NULL,
    acquirer VARCHAR(30) NULL,
    qr_string TEXT NULL,
    qr_url TEXT NULL,
    gross_amount NUMERIC(12, 2) NOT NULL,
    expires_at TIMESTAMPTZ NULL,
    paid_at TIMESTAMPTZ NULL,
    needs_review BOOLEAN NOT NULL DEFAULT FALSE,
    review_note VARCHAR(500) NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_payments_needs_review ON payments(needs_review) WHERE needs_review = TRUE;

-- 7. Payment notifications (payload mentah webhook untuk audit)
CREATE TABLE IF NOT EXISTS payment_notifications (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    payment_id UUID NULL REFERENCES payments(id) ON DELETE SET NULL,
    provider_order_id VARCHAR(50) NULL,
    transaction_status VARCHAR(30) NULL,
    signature_valid BOOLEAN NOT NULL,
    raw_payload JSONB NOT NULL,
    received_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_payment_notifications_order ON payment_notifications(provider_order_id);

-- 8. Riwayat perubahan status (audit & timeline)
CREATE TABLE IF NOT EXISTS order_status_histories (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id UUID NOT NULL REFERENCES orders(id) ON DELETE RESTRICT,
    scope VARCHAR(10) NOT NULL CHECK (scope IN ('ORDER', 'DAY')),
    entity_id UUID NOT NULL,
    from_code VARCHAR(40) NULL,
    to_code VARCHAR(40) NOT NULL,
    actor_user_id UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    actor_role VARCHAR(30) NOT NULL,
    reason VARCHAR(500) NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_order_status_histories_order ON order_status_histories(order_id, created_at);
