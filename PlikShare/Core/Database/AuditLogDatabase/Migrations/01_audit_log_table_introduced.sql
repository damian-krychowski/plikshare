CREATE TABLE IF NOT EXISTS al_audit_logs
(
    al_id                    INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    al_external_id           TEXT    NOT NULL,

    al_created_at            TEXT    NOT NULL,

    al_correlation_id        TEXT    NULL,

    al_actor_identity_type   TEXT    NOT NULL,
    al_actor_identity        TEXT    NOT NULL,
    al_actor_email           TEXT    NULL,
    al_actor_ip              TEXT    NULL,

    al_event_category        TEXT    NOT NULL,
    al_event_type            TEXT    NOT NULL,
    al_event_severity        TEXT    NOT NULL,

    al_resource_type         TEXT    NULL,
    al_resource_external_id  TEXT    NULL,
    al_resource_name         TEXT    NULL,

    al_workspace_external_id TEXT    NULL,

    al_details               TEXT    NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS index__al__al_external_id
    ON al_audit_logs (al_external_id);

CREATE INDEX IF NOT EXISTS index__al__al_created_at
    ON al_audit_logs (al_created_at);

CREATE INDEX IF NOT EXISTS index__al__al_event_category
    ON al_audit_logs (al_event_category);

CREATE INDEX IF NOT EXISTS index__al__al_event_type
    ON al_audit_logs (al_event_type);

CREATE INDEX IF NOT EXISTS index__al__al_actor_identity_type_and_identity
    ON al_audit_logs (al_actor_identity_type, al_actor_identity);

CREATE INDEX IF NOT EXISTS index__al__al_resource_type_and_external_id
    ON al_audit_logs (al_resource_type, al_resource_external_id);

CREATE INDEX IF NOT EXISTS index__al__al_workspace_external_id
    ON al_audit_logs (al_workspace_external_id);

CREATE INDEX IF NOT EXISTS index__al__al_event_severity
    ON al_audit_logs (al_event_severity);

CREATE INDEX IF NOT EXISTS index__al__al_workspace_and_created_at
    ON al_audit_logs (al_workspace_external_id, al_created_at);

CREATE INDEX IF NOT EXISTS index__al__al_actor_and_created_at
    ON al_audit_logs (al_actor_identity_type, al_actor_identity, al_created_at);
