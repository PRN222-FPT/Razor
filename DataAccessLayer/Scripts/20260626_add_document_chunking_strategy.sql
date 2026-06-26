BEGIN;

ALTER TABLE documents
    ADD COLUMN IF NOT EXISTS chunking_strategy character varying(50);

ALTER TABLE documents
    ADD COLUMN IF NOT EXISTS chunk_size integer;

ALTER TABLE documents
    ADD COLUMN IF NOT EXISTS chunk_overlap integer;

UPDATE documents
SET chunking_strategy = 'recursive'
WHERE chunking_strategy IS NULL
   OR btrim(chunking_strategy) = '';

UPDATE documents
SET chunk_size = 1400
WHERE chunk_size IS NULL
   OR chunk_size <= 0;

UPDATE documents
SET chunk_overlap = 180
WHERE chunk_overlap IS NULL
   OR chunk_overlap < 0;

ALTER TABLE documents
    ALTER COLUMN chunking_strategy SET NOT NULL;

ALTER TABLE documents
    ALTER COLUMN chunk_size SET NOT NULL;

ALTER TABLE documents
    ALTER COLUMN chunk_overlap SET NOT NULL;

COMMIT;
