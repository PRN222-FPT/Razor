-- PostgreSQL bootstrap script for FPT UniRAG Razor Pages
-- Usage example:
--   psql -U postgres -f create_database.sql

DO
$$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'prn222') THEN
        EXECUTE 'CREATE DATABASE prn222';
    END IF;
END
$$;

\connect prn222;

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

CREATE TABLE IF NOT EXISTS users (
    user_id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    full_name character varying(255) NOT NULL,
    email character varying(255) NOT NULL,
    password_hash text NOT NULL,
    role character varying(50) DEFAULT 'student',
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    is_blocked boolean NOT NULL DEFAULT false,
    student_code character varying(50),
    password_reset_token_hash text,
    password_reset_token_expires_at timestamp without time zone
);

CREATE UNIQUE INDEX IF NOT EXISTS users_email_key
    ON users (email);

CREATE UNIQUE INDEX IF NOT EXISTS users_student_code_key
    ON users (student_code)
    WHERE student_code IS NOT NULL;

CREATE TABLE IF NOT EXISTS teachers (
    teacher_id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    full_name character varying(255) NOT NULL,
    email character varying(255),
    department character varying(255),
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP
);

CREATE UNIQUE INDEX IF NOT EXISTS teachers_email_key
    ON teachers (email)
    WHERE email IS NOT NULL;

CREATE TABLE IF NOT EXISTS subjects (
    subject_id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    subject_code character varying(50) NOT NULL,
    subject_name character varying(255) NOT NULL,
    description text,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP
);

CREATE UNIQUE INDEX IF NOT EXISTS subjects_subject_code_key
    ON subjects (subject_code);

CREATE TABLE IF NOT EXISTS chapters (
    chapter_id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    subject_id uuid NOT NULL,
    chapter_title character varying(255) NOT NULL,
    chapter_order integer NOT NULL DEFAULT 1,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_chapters_subject
    ON chapters (subject_id);

CREATE UNIQUE INDEX IF NOT EXISTS chapters_subject_normalized_title_key
    ON chapters (subject_id, lower(btrim(chapter_title)));

CREATE TABLE IF NOT EXISTS teacher_subjects (
    teacher_subject_id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    teacher_id uuid NOT NULL,
    subject_id uuid NOT NULL,
    is_head_of_department boolean NOT NULL DEFAULT false,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_teacher_subjects_teacher
    ON teacher_subjects (teacher_id);

CREATE INDEX IF NOT EXISTS idx_teacher_subjects_subject
    ON teacher_subjects (subject_id);

CREATE UNIQUE INDEX IF NOT EXISTS teacher_subjects_teacher_subject_key
    ON teacher_subjects (teacher_id, subject_id);

CREATE UNIQUE INDEX IF NOT EXISTS teacher_subjects_one_leader_per_subject
    ON teacher_subjects (subject_id, is_head_of_department)
    WHERE is_head_of_department;

CREATE TABLE IF NOT EXISTS sessions (
    session_id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id uuid NOT NULL,
    subject_id uuid,
    started_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    ended_at timestamp without time zone
);

CREATE INDEX IF NOT EXISTS IX_sessions_user_id
    ON sessions (user_id);

CREATE INDEX IF NOT EXISTS idx_sessions_subject
    ON sessions (subject_id);

CREATE TABLE IF NOT EXISTS messages (
    message_id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    session_id uuid NOT NULL,
    sender_role character varying(50) NOT NULL,
    message_content text NOT NULL,
    citations_json jsonb,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_messages_session
    ON messages (session_id);

CREATE TABLE IF NOT EXISTS documents (
    document_id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    chapter_id uuid NOT NULL,
    title character varying(255) NOT NULL,
    file_url text NOT NULL,
    file_type character varying(50),
    chunking_strategy character varying(50) NOT NULL,
    chunk_size integer NOT NULL,
    chunk_overlap integer NOT NULL,
    uploaded_by uuid,
    uploaded_teacher uuid,
    status character varying(50) DEFAULT 'pending',
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    subject_id uuid NOT NULL
);

CREATE INDEX IF NOT EXISTS IX_documents_uploaded_by
    ON documents (uploaded_by);

CREATE INDEX IF NOT EXISTS IX_documents_uploaded_teacher
    ON documents (uploaded_teacher);

CREATE INDEX IF NOT EXISTS idx_documents_chapter
    ON documents (chapter_id);

CREATE INDEX IF NOT EXISTS idx_documents_subject
    ON documents (subject_id);

CREATE UNIQUE INDEX IF NOT EXISTS documents_chapter_id_key
    ON documents (chapter_id);

CREATE TABLE IF NOT EXISTS chunks (
    chunk_id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    document_id uuid NOT NULL,
    chunk_index integer NOT NULL,
    content text NOT NULL,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_chunks_document
    ON chunks (document_id);

CREATE TABLE IF NOT EXISTS processing_jobs (
    job_id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    document_id uuid NOT NULL,
    job_status character varying(50) DEFAULT 'queued',
    started_at timestamp without time zone,
    finished_at timestamp without time zone,
    error_message text
);

CREATE INDEX IF NOT EXISTS idx_processing_document
    ON processing_jobs (document_id);

CREATE TABLE IF NOT EXISTS test_questions (
    question_id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    chapter_id uuid NOT NULL,
    question_text text NOT NULL,
    difficulty character varying(50),
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_questions_chapter
    ON test_questions (chapter_id);

CREATE TABLE IF NOT EXISTS benchmark_runs (
    benchmark_run_id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    run_name character varying(255),
    executed_by uuid,
    started_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    completed_at timestamp without time zone
);

CREATE INDEX IF NOT EXISTS IX_benchmark_runs_executed_by
    ON benchmark_runs (executed_by);

CREATE TABLE IF NOT EXISTS benchmark_results (
    result_id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    benchmark_run_id uuid NOT NULL,
    question_id uuid,
    score numeric(5,2),
    response_time_ms integer,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS IX_benchmark_results_benchmark_run_id
    ON benchmark_results (benchmark_run_id);

CREATE INDEX IF NOT EXISTS IX_benchmark_results_question_id
    ON benchmark_results (question_id);

ALTER TABLE chapters
    DROP CONSTRAINT IF EXISTS fk_chapter_subject;
ALTER TABLE chapters
    ADD CONSTRAINT fk_chapter_subject
    FOREIGN KEY (subject_id)
    REFERENCES subjects(subject_id)
    ON DELETE CASCADE;

ALTER TABLE teacher_subjects
    DROP CONSTRAINT IF EXISTS fk_teacher_subject_teacher;
ALTER TABLE teacher_subjects
    ADD CONSTRAINT fk_teacher_subject_teacher
    FOREIGN KEY (teacher_id)
    REFERENCES teachers(teacher_id)
    ON DELETE CASCADE;

ALTER TABLE teacher_subjects
    DROP CONSTRAINT IF EXISTS fk_teacher_subject_subject;
ALTER TABLE teacher_subjects
    ADD CONSTRAINT fk_teacher_subject_subject
    FOREIGN KEY (subject_id)
    REFERENCES subjects(subject_id)
    ON DELETE CASCADE;

ALTER TABLE sessions
    DROP CONSTRAINT IF EXISTS fk_session_user;
ALTER TABLE sessions
    ADD CONSTRAINT fk_session_user
    FOREIGN KEY (user_id)
    REFERENCES users(user_id);

ALTER TABLE sessions
    DROP CONSTRAINT IF EXISTS fk_session_subject;
ALTER TABLE sessions
    ADD CONSTRAINT fk_session_subject
    FOREIGN KEY (subject_id)
    REFERENCES subjects(subject_id)
    ON DELETE CASCADE;

ALTER TABLE messages
    DROP CONSTRAINT IF EXISTS fk_message_session;
ALTER TABLE messages
    ADD CONSTRAINT fk_message_session
    FOREIGN KEY (session_id)
    REFERENCES sessions(session_id)
    ON DELETE CASCADE;

ALTER TABLE documents
    DROP CONSTRAINT IF EXISTS fk_document_chapter;
ALTER TABLE documents
    ADD CONSTRAINT fk_document_chapter
    FOREIGN KEY (chapter_id)
    REFERENCES chapters(chapter_id)
    ON DELETE CASCADE;

ALTER TABLE documents
    DROP CONSTRAINT IF EXISTS fk_document_subject;
ALTER TABLE documents
    ADD CONSTRAINT fk_document_subject
    FOREIGN KEY (subject_id)
    REFERENCES subjects(subject_id)
    ON DELETE CASCADE;

ALTER TABLE documents
    DROP CONSTRAINT IF EXISTS fk_document_user;
ALTER TABLE documents
    ADD CONSTRAINT fk_document_user
    FOREIGN KEY (uploaded_by)
    REFERENCES users(user_id)
    ON DELETE SET NULL;

ALTER TABLE documents
    DROP CONSTRAINT IF EXISTS fk_document_teacher;
ALTER TABLE documents
    ADD CONSTRAINT fk_document_teacher
    FOREIGN KEY (uploaded_teacher)
    REFERENCES teachers(teacher_id)
    ON DELETE SET NULL;

ALTER TABLE chunks
    DROP CONSTRAINT IF EXISTS fk_chunk_document;
ALTER TABLE chunks
    ADD CONSTRAINT fk_chunk_document
    FOREIGN KEY (document_id)
    REFERENCES documents(document_id)
    ON DELETE CASCADE;

ALTER TABLE processing_jobs
    DROP CONSTRAINT IF EXISTS fk_processing_document;
ALTER TABLE processing_jobs
    ADD CONSTRAINT fk_processing_document
    FOREIGN KEY (document_id)
    REFERENCES documents(document_id)
    ON DELETE CASCADE;

ALTER TABLE test_questions
    DROP CONSTRAINT IF EXISTS fk_question_chapter;
ALTER TABLE test_questions
    ADD CONSTRAINT fk_question_chapter
    FOREIGN KEY (chapter_id)
    REFERENCES chapters(chapter_id)
    ON DELETE CASCADE;

ALTER TABLE benchmark_runs
    DROP CONSTRAINT IF EXISTS fk_benchmark_user;
ALTER TABLE benchmark_runs
    ADD CONSTRAINT fk_benchmark_user
    FOREIGN KEY (executed_by)
    REFERENCES users(user_id)
    ON DELETE SET NULL;

ALTER TABLE benchmark_results
    DROP CONSTRAINT IF EXISTS fk_result_run;
ALTER TABLE benchmark_results
    ADD CONSTRAINT fk_result_run
    FOREIGN KEY (benchmark_run_id)
    REFERENCES benchmark_runs(benchmark_run_id);

ALTER TABLE benchmark_results
    DROP CONSTRAINT IF EXISTS fk_result_question;
ALTER TABLE benchmark_results
    ADD CONSTRAINT fk_result_question
    FOREIGN KEY (question_id)
    REFERENCES test_questions(question_id)
    ON DELETE SET NULL;
