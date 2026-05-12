ALTER TABLE w_workspaces
    ADD COLUMN w_audit_log_disabled_events_json TEXT NOT NULL DEFAULT '[]';
