CREATE TABLE IF NOT EXISTS qs_queue_sagas
(
    qs_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    qs_on_completed_queue_job_type TEXT NOT NULL,
    qs_on_completed_queue_job_definition TEXT NOT NULL ,
    qs_correlation_id TEXT NOT NULL
);

ALTER TABLE q_queue ADD COLUMN q_saga_id INTEGER NULL REFERENCES qs_queue_sagas(qs_id);
CREATE INDEX IF NOT EXISTS index__q_queue__q_saga_id ON q_queue (q_saga_id);