ALTER TABLE chapters
    DROP CONSTRAINT IF EXISTS fk_chapter_subject;

ALTER TABLE chapters
    ADD CONSTRAINT fk_chapter_subject
    FOREIGN KEY (subject_id)
    REFERENCES subjects(subject_id)
    ON DELETE CASCADE;

ALTER TABLE teacher_subjects
    DROP CONSTRAINT IF EXISTS fk_teacher_subject_subject;

ALTER TABLE teacher_subjects
    ADD CONSTRAINT fk_teacher_subject_subject
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

ALTER TABLE sessions
    DROP CONSTRAINT IF EXISTS fk_session_subject;

ALTER TABLE sessions
    ADD CONSTRAINT fk_session_subject
    FOREIGN KEY (subject_id)
    REFERENCES subjects(subject_id)
    ON DELETE CASCADE;

ALTER TABLE documents
    DROP CONSTRAINT IF EXISTS fk_document_subject;

ALTER TABLE documents
    ADD CONSTRAINT fk_document_subject
    FOREIGN KEY (subject_id)
    REFERENCES subjects(subject_id)
    ON DELETE CASCADE;

ALTER TABLE documents
    DROP CONSTRAINT IF EXISTS fk_document_chapter;

ALTER TABLE documents
    ADD CONSTRAINT fk_document_chapter
    FOREIGN KEY (chapter_id)
    REFERENCES chapters(chapter_id)
    ON DELETE CASCADE;

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

ALTER TABLE messages
    DROP CONSTRAINT IF EXISTS fk_message_session;

ALTER TABLE messages
    ADD CONSTRAINT fk_message_session
    FOREIGN KEY (session_id)
    REFERENCES sessions(session_id)
    ON DELETE CASCADE;

ALTER TABLE test_questions
    DROP CONSTRAINT IF EXISTS fk_question_chapter;

ALTER TABLE test_questions
    ADD CONSTRAINT fk_question_chapter
    FOREIGN KEY (chapter_id)
    REFERENCES chapters(chapter_id)
    ON DELETE CASCADE;
