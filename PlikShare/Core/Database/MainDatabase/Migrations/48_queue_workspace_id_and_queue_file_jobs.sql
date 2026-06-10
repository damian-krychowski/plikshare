ALTER TABLE q_queue ADD COLUMN q_workspace_id INTEGER;
ALTER TABLE qc_queue_completed ADD COLUMN qc_workspace_id INTEGER;

CREATE INDEX IF NOT EXISTS index__q_queue__q_workspace_id
    ON q_queue (q_workspace_id)
    WHERE q_workspace_id IS NOT NULL;

CREATE TABLE IF NOT EXISTS qfj_queue_file_jobs (
    qfj_queue_job_id INTEGER NOT NULL,
    qfj_file_id INTEGER NOT NULL,
    PRIMARY KEY (qfj_queue_job_id, qfj_file_id),
    FOREIGN KEY (qfj_queue_job_id) REFERENCES q_queue (q_id) ON DELETE CASCADE
) WITHOUT ROWID;

CREATE INDEX IF NOT EXISTS index__qfj_queue_file_jobs__qfj_file_id
    ON qfj_queue_file_jobs (qfj_file_id);
