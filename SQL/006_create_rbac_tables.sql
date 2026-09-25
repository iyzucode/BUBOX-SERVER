-- ==========================================================
-- Bubox Auth - Role-Based Access Control (RBAC) Migration
-- File: 006_create_rbac_tables.sql
-- Description: Creates rf_roles and user_roles tables
-- ==========================================================

-- 1. Reference Table: rf_roles
CREATE TABLE IF NOT EXISTS rf_roles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(50) NOT NULL UNIQUE,
    name VARCHAR(50) NOT NULL UNIQUE,
    description VARCHAR(255),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Index for lookup by code
CREATE INDEX IF NOT EXISTS idx_rf_roles_code ON rf_roles(code);

-- 2. Seed 6 primary roles
INSERT INTO rf_roles (code, name, description)
VALUES 
    ('CUSTOMER', 'Customer', 'Pengguna/pelanggan katering yang memesan produk'),
    ('ADMIN', 'Admin', 'Administrator operasional aplikasi'),
    ('DRIVER', 'Driver', 'Kurir / armada pengiriman pesanan katering'),
    ('KITCHEN', 'Kitchen', 'Tim dapur / persiapan produksi katering'),
    ('OWNER', 'Owner', 'Pemilik usaha / manajemen'),
    ('SUPER_ADMIN', 'Super Admin', 'Administrator sistem dengan hak akses penuh')
ON CONFLICT (code) DO NOTHING;

-- 3. User Roles Mapping Table: user_roles (Many-to-Many)
CREATE TABLE IF NOT EXISTS user_roles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    role_id UUID NOT NULL REFERENCES rf_roles(id) ON DELETE CASCADE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_user_roles_user_id_role_id UNIQUE (user_id, role_id)
);

-- Indexes for efficient queries
CREATE INDEX IF NOT EXISTS idx_user_roles_user_id ON user_roles(user_id);
CREATE INDEX IF NOT EXISTS idx_user_roles_role_id ON user_roles(role_id);

-- 4. Assign default 'Customer' role to any existing users currently in the database
INSERT INTO user_roles (user_id, role_id)
SELECT u.id, r.id
FROM users u
CROSS JOIN rf_roles r
WHERE r.code = 'CUSTOMER'
  AND NOT EXISTS (
      SELECT 1 FROM user_roles ur WHERE ur.user_id = u.id AND ur.role_id = r.id
  );
