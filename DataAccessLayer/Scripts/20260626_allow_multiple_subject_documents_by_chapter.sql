BEGIN;

-- Normalize blank chapter titles before deduplication.
UPDATE chapters
SET chapter_title = btrim(chapter_title)
WHERE chapter_title <> btrim(chapter_title);

UPDATE chapters
SET chapter_title = 'General'
WHERE btrim(COALESCE(chapter_title, '')) = '';

-- Merge duplicate chapter rows within the same subject by normalized title.
WITH ranked_chapters AS (
    SELECT
        chapter_id,
        subject_id,
        lower(btrim(chapter_title)) AS normalized_title,
        ROW_NUMBER() OVER (
            PARTITION BY subject_id, lower(btrim(chapter_title))
            ORDER BY chapter_order NULLS LAST, created_at NULLS LAST, chapter_id
        ) AS row_number,
        FIRST_VALUE(chapter_id) OVER (
            PARTITION BY subject_id, lower(btrim(chapter_title))
            ORDER BY chapter_order NULLS LAST, created_at NULLS LAST, chapter_id
        ) AS canonical_chapter_id
    FROM chapters
),
duplicate_chapters AS (
    SELECT chapter_id, canonical_chapter_id
    FROM ranked_chapters
    WHERE row_number > 1
)
UPDATE documents AS documents_to_update
SET chapter_id = duplicate_chapters.canonical_chapter_id
FROM duplicate_chapters
WHERE documents_to_update.chapter_id = duplicate_chapters.chapter_id;

WITH ranked_chapters AS (
    SELECT
        chapter_id,
        subject_id,
        lower(btrim(chapter_title)) AS normalized_title,
        ROW_NUMBER() OVER (
            PARTITION BY subject_id, lower(btrim(chapter_title))
            ORDER BY chapter_order NULLS LAST, created_at NULLS LAST, chapter_id
        ) AS row_number,
        FIRST_VALUE(chapter_id) OVER (
            PARTITION BY subject_id, lower(btrim(chapter_title))
            ORDER BY chapter_order NULLS LAST, created_at NULLS LAST, chapter_id
        ) AS canonical_chapter_id
    FROM chapters
),
duplicate_chapters AS (
    SELECT chapter_id, canonical_chapter_id
    FROM ranked_chapters
    WHERE row_number > 1
)
UPDATE test_questions AS questions_to_update
SET chapter_id = duplicate_chapters.canonical_chapter_id
FROM duplicate_chapters
WHERE questions_to_update.chapter_id = duplicate_chapters.chapter_id;

WITH ranked_chapters AS (
    SELECT
        chapter_id,
        ROW_NUMBER() OVER (
            PARTITION BY subject_id, lower(btrim(chapter_title))
            ORDER BY chapter_order NULLS LAST, created_at NULLS LAST, chapter_id
        ) AS row_number
    FROM chapters
)
DELETE FROM chapters
WHERE chapter_id IN (
    SELECT chapter_id
    FROM ranked_chapters
    WHERE row_number > 1
);

-- Keep only the newest document per chapter before adding the unique index.
WITH ranked_documents AS (
    SELECT
        document_id,
        ROW_NUMBER() OVER (
            PARTITION BY chapter_id
            ORDER BY created_at DESC NULLS LAST, document_id DESC
        ) AS row_number
    FROM documents
),
documents_to_remove AS (
    SELECT document_id
    FROM ranked_documents
    WHERE row_number > 1
)
DELETE FROM processing_jobs
WHERE document_id IN (SELECT document_id FROM documents_to_remove);

WITH ranked_documents AS (
    SELECT
        document_id,
        ROW_NUMBER() OVER (
            PARTITION BY chapter_id
            ORDER BY created_at DESC NULLS LAST, document_id DESC
        ) AS row_number
    FROM documents
),
documents_to_remove AS (
    SELECT document_id
    FROM ranked_documents
    WHERE row_number > 1
)
DELETE FROM chunks
WHERE document_id IN (SELECT document_id FROM documents_to_remove);

WITH ranked_documents AS (
    SELECT
        document_id,
        ROW_NUMBER() OVER (
            PARTITION BY chapter_id
            ORDER BY created_at DESC NULLS LAST, document_id DESC
        ) AS row_number
    FROM documents
)
DELETE FROM documents
WHERE document_id IN (
    SELECT document_id
    FROM ranked_documents
    WHERE row_number > 1
);

CREATE UNIQUE INDEX IF NOT EXISTS chapters_subject_normalized_title_key
    ON chapters (subject_id, lower(btrim(chapter_title)));

CREATE UNIQUE INDEX IF NOT EXISTS documents_chapter_id_key
    ON documents (chapter_id);

COMMIT;
