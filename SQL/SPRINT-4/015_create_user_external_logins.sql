-- ==========================================================
-- File: 015_create_user_external_logins.sql
-- Description: Login dengan penyedia luar (Google).
--   * user_external_logins: satu baris per akun penyedia yang tertaut ke user.
--     provider_user_id = "sub" dari ID token Google (stabil, tidak berubah walau email diganti).
--   * users.password_hash boleh kosong untuk akun yang hanya masuk lewat Google.
-- ==========================================================

CREATE TABLE IF NOT EXISTS user_external_logins (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    provider VARCHAR(30) NOT NULL,          -- 'GOOGLE'
    provider_user_id VARCHAR(255) NOT NULL, -- Google "sub"
    email VARCHAR(255) NOT NULL,            -- email dari penyedia saat ditautkan
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_login_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_user_external_logins_provider_user UNIQUE (provider, provider_user_id),
    CONSTRAINT uq_user_external_logins_user_provider UNIQUE (user_id, provider)
);

CREATE INDEX IF NOT EXISTS idx_user_external_logins_user ON user_external_logins (user_id);

ALTER TABLE users ALTER COLUMN password_hash DROP NOT NULL;
