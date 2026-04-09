ALTER TABLE al_audit_logs ADD COLUMN al_box_external_id TEXT NULL;
ALTER TABLE al_audit_logs ADD COLUMN al_box_link_external_id TEXT NULL;

CREATE INDEX IF NOT EXISTS index__al__al_box_external_id
    ON al_audit_logs (al_box_external_id);

CREATE INDEX IF NOT EXISTS index__al__al_box_link_external_id
    ON al_audit_logs (al_box_link_external_id);
