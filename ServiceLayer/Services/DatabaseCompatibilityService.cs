using DataAccessLayer;
using Microsoft.EntityFrameworkCore;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

public sealed class DatabaseCompatibilityService(AppDbContext dbContext) : IDatabaseCompatibilityService
{
    public async Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            ALTER TABLE teacher_subjects
                DROP CONSTRAINT IF EXISTS teacher_subjects_subject_id_key;

            DROP INDEX IF EXISTS teacher_subjects_subject_id_key;

            DROP INDEX IF EXISTS teacher_subjects_teacher_id_key;

            CREATE UNIQUE INDEX IF NOT EXISTS teacher_subjects_one_leader_per_subject
                ON teacher_subjects (subject_id)
                WHERE is_head_of_department;

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
                ON DELETE CASCADE;

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

            ALTER TABLE users
                ADD COLUMN IF NOT EXISTS password_reset_token_hash text;

            ALTER TABLE users
                ADD COLUMN IF NOT EXISTS password_reset_token_expires_at timestamp without time zone;

            ALTER TABLE test_questions
                DROP CONSTRAINT IF EXISTS fk_question_chapter;

            ALTER TABLE test_questions
                ADD CONSTRAINT fk_question_chapter
                FOREIGN KEY (chapter_id)
                REFERENCES chapters(chapter_id)
                ON DELETE CASCADE;
            """;

        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }
}
