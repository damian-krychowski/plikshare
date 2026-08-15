-- Per-agent delivery inbox for system events. This is the high-churn firehose, deliberately kept in
-- its own database so its growth, retention and VACUUM never touch the main operational database.
-- No foreign keys here: agent/workspace/box ids are plain values referencing the main database
-- (cross-database FKs are impossible in SQLite), exactly like the audit log keeps decoupled ids.
CREATE TABLE IF NOT EXISTS aev_agent_events
(
    aev_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    aev_external_id TEXT NOT NULL,

    aev_agent_id INTEGER NOT NULL,

    aev_event_type TEXT NOT NULL,
    aev_event_category TEXT NOT NULL,

    aev_workspace_external_id TEXT NULL,
    aev_box_external_id TEXT NULL,

    aev_actor_identity_type TEXT NOT NULL,
    aev_actor_identity TEXT NOT NULL,

    aev_occurred_at TEXT NOT NULL,
    aev_created_at TEXT NOT NULL,

    aev_is_acked BOOLEAN NOT NULL DEFAULT FALSE
);

-- The poll path: WHERE aev_agent_id = ? AND aev_id > ? ORDER BY aev_id. aev_id is the per-agent cursor.
CREATE INDEX IF NOT EXISTS index__aev_agent_events__agent_id__id ON aev_agent_events (aev_agent_id, aev_id);

CREATE UNIQUE INDEX IF NOT EXISTS index__aev_agent_events__external_id ON aev_agent_events (aev_external_id);

-- The retention purge path: DELETE WHERE aev_created_at < cutoff.
CREATE INDEX IF NOT EXISTS index__aev_agent_events__created_at ON aev_agent_events (aev_created_at);
