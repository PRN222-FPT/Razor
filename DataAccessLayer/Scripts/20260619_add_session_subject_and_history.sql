ALTER TABLE sessions
    ADD COLUMN IF NOT EXISTS subject_id uuid;

CREATE INDEX IF NOT EXISTS idx_sessions_subject
    ON sessions (subject_id);

ALTER TABLE sessions
    DROP CONSTRAINT IF EXISTS fk_session_subject;

ALTER TABLE sessions
    ADD CONSTRAINT fk_session_subject
    FOREIGN KEY (subject_id)
    REFERENCES subjects(subject_id)
    ON DELETE SET NULL;
