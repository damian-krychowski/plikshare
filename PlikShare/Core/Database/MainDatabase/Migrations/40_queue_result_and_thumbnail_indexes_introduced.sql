-- Generic batch grouping key as a first-class queue column. A batch (one generation request ->
-- N jobs) shares one q_batch_id; it propagates into qc_queue_completed on success so a batch's
-- progress can be aggregated from the queue tables alone, without a dedicated table.
ALTER TABLE q_queue ADD COLUMN q_batch_id TEXT;
ALTER TABLE qc_queue_completed ADD COLUMN qc_batch_id TEXT;

-- Nullable per-job outcome payload. Written only when a completed job has something worth
-- reporting (eg. some thumbnail variants failed to generate). NULL = nothing to report.
ALTER TABLE qc_queue_completed ADD COLUMN qc_result TEXT;

-- Partial indexes (only rows that actually belong to a batch pay the cost) so batch lookups
-- don't scan the whole queue / completed-archive.
CREATE INDEX IF NOT EXISTS index__q_queue__q_batch_id
    ON q_queue (q_batch_id)
    WHERE q_batch_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS index__qc_queue_completed__qc_batch_id
    ON qc_queue_completed (qc_batch_id)
    WHERE qc_batch_id IS NOT NULL;
