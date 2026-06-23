ALTER TABLE messages
    ADD COLUMN IF NOT EXISTS citations_json jsonb;
