ALTER TABLE users
    ADD COLUMN IF NOT EXISTS password_reset_token_hash text;

ALTER TABLE users
    ADD COLUMN IF NOT EXISTS password_reset_token_expires_at timestamp without time zone;
