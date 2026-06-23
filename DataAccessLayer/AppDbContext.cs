using System;
using System.Collections.Generic;
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<BenchmarkResult> BenchmarkResults { get; set; }

    public virtual DbSet<BenchmarkRun> BenchmarkRuns { get; set; }

    public virtual DbSet<Chapter> Chapters { get; set; }

    public virtual DbSet<Chunk> Chunks { get; set; }

    public virtual DbSet<Document> Documents { get; set; }

    public virtual DbSet<Message> Messages { get; set; }

    public virtual DbSet<ProcessingJob> ProcessingJobs { get; set; }

    public virtual DbSet<Session> Sessions { get; set; }

    public virtual DbSet<Subject> Subjects { get; set; }

    public virtual DbSet<Teacher> Teachers { get; set; }

    public virtual DbSet<TeacherSubject> TeacherSubjects { get; set; }

    public virtual DbSet<TestQuestion> TestQuestions { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("uuid-ossp");

        modelBuilder.Entity<BenchmarkResult>(entity =>
        {
            entity.HasKey(e => e.ResultId).HasName("benchmark_results_pkey");

            entity.ToTable("benchmark_results");

            entity.HasIndex(e => e.BenchmarkRunId, "IX_benchmark_results_benchmark_run_id");

            entity.HasIndex(e => e.QuestionId, "IX_benchmark_results_question_id");

            entity.Property(e => e.ResultId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("result_id");
            entity.Property(e => e.BenchmarkRunId).HasColumnName("benchmark_run_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.QuestionId).HasColumnName("question_id");
            entity.Property(e => e.ResponseTimeMs).HasColumnName("response_time_ms");
            entity.Property(e => e.Score)
                .HasPrecision(5, 2)
                .HasColumnName("score");

            entity.HasOne(d => d.BenchmarkRun).WithMany(p => p.BenchmarkResults)
                .HasForeignKey(d => d.BenchmarkRunId)
                .HasConstraintName("fk_result_run");

            entity.HasOne(d => d.Question).WithMany(p => p.BenchmarkResults)
                .HasForeignKey(d => d.QuestionId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_result_question");
        });

        modelBuilder.Entity<BenchmarkRun>(entity =>
        {
            entity.HasKey(e => e.BenchmarkRunId).HasName("benchmark_runs_pkey");

            entity.ToTable("benchmark_runs");

            entity.HasIndex(e => e.ExecutedBy, "IX_benchmark_runs_executed_by");

            entity.Property(e => e.BenchmarkRunId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("benchmark_run_id");
            entity.Property(e => e.CompletedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("completed_at");
            entity.Property(e => e.ExecutedBy).HasColumnName("executed_by");
            entity.Property(e => e.RunName)
                .HasMaxLength(255)
                .HasColumnName("run_name");
            entity.Property(e => e.StartedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("started_at");

            entity.HasOne(d => d.ExecutedByNavigation).WithMany(p => p.BenchmarkRuns)
                .HasForeignKey(d => d.ExecutedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_benchmark_user");
        });

        modelBuilder.Entity<Chapter>(entity =>
        {
            entity.HasKey(e => e.ChapterId).HasName("chapters_pkey");

            entity.ToTable("chapters");

            entity.HasIndex(e => e.SubjectId, "idx_chapters_subject");

            entity.Property(e => e.ChapterId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("chapter_id");
            entity.Property(e => e.ChapterOrder)
                .HasDefaultValue(1)
                .HasColumnName("chapter_order");
            entity.Property(e => e.ChapterTitle)
                .HasMaxLength(255)
                .HasColumnName("chapter_title");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.SubjectId).HasColumnName("subject_id");

            entity.HasOne(d => d.Subject).WithMany(p => p.Chapters)
                .HasForeignKey(d => d.SubjectId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_chapter_subject");
        });

        modelBuilder.Entity<Chunk>(entity =>
        {
            entity.HasKey(e => e.ChunkId).HasName("chunks_pkey");

            entity.ToTable("chunks");

            entity.HasIndex(e => e.DocumentId, "idx_chunks_document");

            entity.Property(e => e.ChunkId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("chunk_id");
            entity.Property(e => e.ChunkIndex).HasColumnName("chunk_index");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.DocumentId).HasColumnName("document_id");

            entity.HasOne(d => d.Document).WithMany(p => p.Chunks)
                .HasForeignKey(d => d.DocumentId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_chunk_document");
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.DocumentId).HasName("documents_pkey");

            entity.ToTable("documents");

            entity.HasIndex(e => e.UploadedBy, "IX_documents_uploaded_by");

            entity.HasIndex(e => e.UploadedTeacher, "IX_documents_uploaded_teacher");

            entity.HasIndex(e => e.ChapterId, "idx_documents_chapter");

            entity.HasIndex(e => e.SubjectId, "idx_documents_subject");

            entity.Property(e => e.DocumentId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("document_id");
            entity.Property(e => e.ChapterId).HasColumnName("chapter_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.FileType)
                .HasMaxLength(50)
                .HasColumnName("file_type");
            entity.Property(e => e.FileUrl).HasColumnName("file_url");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValueSql("'pending'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.SubjectId).HasColumnName("subject_id");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");
            entity.Property(e => e.UploadedBy).HasColumnName("uploaded_by");
            entity.Property(e => e.UploadedTeacher).HasColumnName("uploaded_teacher");

            entity.HasOne(d => d.Chapter).WithMany(p => p.Documents)
                .HasForeignKey(d => d.ChapterId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_document_chapter");

            entity.HasOne(d => d.Subject).WithMany(p => p.Documents)
                .HasForeignKey(d => d.SubjectId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_document_subject");

            entity.HasOne(d => d.UploadedByNavigation).WithMany(p => p.Documents)
                .HasForeignKey(d => d.UploadedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_document_user");

            entity.HasOne(d => d.UploadedTeacherNavigation).WithMany(p => p.Documents)
                .HasForeignKey(d => d.UploadedTeacher)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_document_teacher");
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.MessageId).HasName("messages_pkey");

            entity.ToTable("messages");

            entity.HasIndex(e => e.SessionId, "idx_messages_session");

            entity.Property(e => e.MessageId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("message_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.CitationsJson)
                .HasColumnType("jsonb")
                .HasColumnName("citations_json");
            entity.Property(e => e.MessageContent).HasColumnName("message_content");
            entity.Property(e => e.SenderRole)
                .HasMaxLength(50)
                .HasColumnName("sender_role");
            entity.Property(e => e.SessionId).HasColumnName("session_id");

            entity.HasOne(d => d.Session).WithMany(p => p.Messages)
                .HasForeignKey(d => d.SessionId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_message_session");
        });

        modelBuilder.Entity<ProcessingJob>(entity =>
        {
            entity.HasKey(e => e.JobId).HasName("processing_jobs_pkey");

            entity.ToTable("processing_jobs");

            entity.HasIndex(e => e.DocumentId, "idx_processing_document");

            entity.Property(e => e.JobId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("job_id");
            entity.Property(e => e.DocumentId).HasColumnName("document_id");
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message");
            entity.Property(e => e.FinishedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("finished_at");
            entity.Property(e => e.JobStatus)
                .HasMaxLength(50)
                .HasDefaultValueSql("'queued'::character varying")
                .HasColumnName("job_status");
            entity.Property(e => e.StartedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("started_at");

            entity.HasOne(d => d.Document).WithMany(p => p.ProcessingJobs)
                .HasForeignKey(d => d.DocumentId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_processing_document");
        });

        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasKey(e => e.SessionId).HasName("sessions_pkey");

            entity.ToTable("sessions");

            entity.HasIndex(e => e.UserId, "IX_sessions_user_id");

            entity.HasIndex(e => e.SubjectId, "idx_sessions_subject");

            entity.Property(e => e.SessionId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("session_id");
            entity.Property(e => e.EndedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("ended_at");
            entity.Property(e => e.StartedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("started_at");
            entity.Property(e => e.SubjectId).HasColumnName("subject_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Subject).WithMany(p => p.Sessions)
                .HasForeignKey(d => d.SubjectId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_session_subject");

            entity.HasOne(d => d.User).WithMany(p => p.Sessions)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_session_user");
        });

        modelBuilder.Entity<Subject>(entity =>
        {
            entity.HasKey(e => e.SubjectId).HasName("subjects_pkey");

            entity.ToTable("subjects");

            entity.HasIndex(e => e.SubjectCode, "subjects_subject_code_key").IsUnique();

            entity.Property(e => e.SubjectId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("subject_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.SubjectCode)
                .HasMaxLength(50)
                .HasColumnName("subject_code");
            entity.Property(e => e.SubjectName)
                .HasMaxLength(255)
                .HasColumnName("subject_name");
        });

        modelBuilder.Entity<Teacher>(entity =>
        {
            entity.HasKey(e => e.TeacherId).HasName("teachers_pkey");

            entity.ToTable("teachers");

            entity.HasIndex(e => e.Email, "teachers_email_key").IsUnique();

            entity.Property(e => e.TeacherId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("teacher_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Department)
                .HasMaxLength(255)
                .HasColumnName("department");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.FullName)
                .HasMaxLength(255)
                .HasColumnName("full_name");
        });

        modelBuilder.Entity<TeacherSubject>(entity =>
        {
            entity.HasKey(e => e.TeacherSubjectId).HasName("teacher_subjects_pkey");

            entity.ToTable("teacher_subjects");

            entity.HasIndex(e => e.SubjectId, "idx_teacher_subjects_subject");

            entity.HasIndex(e => e.TeacherId, "idx_teacher_subjects_teacher");

            entity.HasIndex(e => new { e.TeacherId, e.SubjectId }, "teacher_subjects_teacher_subject_key").IsUnique();

            entity.HasIndex(e => new { e.SubjectId, e.IsHeadOfDepartment }, "teacher_subjects_one_leader_per_subject")
                .IsUnique()
                .HasFilter("is_head_of_department");

            entity.Property(e => e.TeacherSubjectId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("teacher_subject_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.IsHeadOfDepartment).HasColumnName("is_head_of_department");
            entity.Property(e => e.SubjectId).HasColumnName("subject_id");
            entity.Property(e => e.TeacherId).HasColumnName("teacher_id");

            entity.HasOne(d => d.Subject).WithMany(p => p.TeacherSubjects)
                .HasForeignKey(d => d.SubjectId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_teacher_subject_subject");

            entity.HasOne(d => d.Teacher).WithMany(p => p.TeacherSubjects)
                .HasForeignKey(d => d.TeacherId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_teacher_subject_teacher");
        });

        modelBuilder.Entity<TestQuestion>(entity =>
        {
            entity.HasKey(e => e.QuestionId).HasName("test_questions_pkey");

            entity.ToTable("test_questions");

            entity.HasIndex(e => e.ChapterId, "idx_questions_chapter");

            entity.Property(e => e.QuestionId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("question_id");
            entity.Property(e => e.ChapterId).HasColumnName("chapter_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Difficulty)
                .HasMaxLength(50)
                .HasColumnName("difficulty");
            entity.Property(e => e.QuestionText).HasColumnName("question_text");

            entity.HasOne(d => d.Chapter).WithMany(p => p.TestQuestions)
                .HasForeignKey(d => d.ChapterId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_question_chapter");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("users_pkey");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "users_email_key").IsUnique();

            entity.HasIndex(e => e.StudentCode, "users_student_code_key").IsUnique();

            entity.Property(e => e.UserId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("user_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.FullName)
                .HasMaxLength(255)
                .HasColumnName("full_name");
            entity.Property(e => e.IsBlocked).HasColumnName("is_blocked");
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
            entity.Property(e => e.PasswordResetTokenExpiresAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("password_reset_token_expires_at");
            entity.Property(e => e.PasswordResetTokenHash)
                .HasColumnName("password_reset_token_hash");
            entity.Property(e => e.Role)
                .HasMaxLength(50)
                .HasDefaultValueSql("'student'::character varying")
                .HasColumnName("role");
            entity.Property(e => e.StudentCode)
                .HasMaxLength(50)
                .HasColumnName("student_code");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
