ALTER TABLE teacher_subjects
    DROP CONSTRAINT IF EXISTS teacher_subjects_subject_id_key;

DROP INDEX IF EXISTS teacher_subjects_subject_id_key;

DROP INDEX IF EXISTS teacher_subjects_teacher_id_key;

ALTER TABLE teacher_subjects
    DROP CONSTRAINT IF EXISTS teacher_subjects_teacher_id_key;

CREATE UNIQUE INDEX IF NOT EXISTS teacher_subjects_one_leader_per_subject
    ON teacher_subjects (subject_id)
    WHERE is_head_of_department;
