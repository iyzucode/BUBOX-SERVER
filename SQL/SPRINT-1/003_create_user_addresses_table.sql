-- ==========================================================
-- Bubox Biodata - User Addresses Table Migration
-- File: 003_create_user_addresses_table.sql
-- Description: Creates user_addresses table for delivery address management
-- ==========================================================

CREATE TABLE IF NOT EXISTS user_addresses (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    label VARCHAR(50) NOT NULL,
    recipient_name VARCHAR(100) NOT NULL,
    phone_number VARCHAR(20) NOT NULL,
    full_address TEXT NOT NULL,
    subdistrict VARCHAR(100) NOT NULL,
    city VARCHAR(100) NOT NULL,
    province VARCHAR(100) NOT NULL,
    postal_code VARCHAR(10) NOT NULL,
    notes TEXT,
    is_primary BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Index for searching user's addresses
CREATE INDEX IF NOT EXISTS idx_user_addresses_user_id ON user_addresses(user_id);

-- Index for retrieving primary address efficiently
CREATE INDEX IF NOT EXISTS idx_user_addresses_primary ON user_addresses(user_id, is_primary);
