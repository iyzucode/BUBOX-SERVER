-- ==========================================================
-- Bubox Auth Migration: Add Username to Users Table
-- File: 002_add_username_to_users.sql
-- Description: Adds unique, human-readable username column
-- ==========================================================

-- 1. Add username column if not exists
ALTER TABLE users ADD COLUMN IF NOT EXISTS username VARCHAR(50);

-- 2. Populate username for existing users if any (based on email prefix)
UPDATE users 
SET username = LOWER(SPLIT_PART(email, '@', 1)) 
WHERE username IS NULL;

-- 3. Set NOT NULL constraint
ALTER TABLE users ALTER COLUMN username SET NOT NULL;

-- 4. Create case-insensitive unique index on LOWER(username)
CREATE UNIQUE INDEX IF NOT EXISTS idx_users_username_lower ON users(LOWER(username));
