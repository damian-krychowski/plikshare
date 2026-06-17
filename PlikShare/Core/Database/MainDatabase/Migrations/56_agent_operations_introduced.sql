CREATE TABLE IF NOT EXISTS aop_agent_operations
(
    aop_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    aop_external_id TEXT NOT NULL,

    aop_agent_id INTEGER NOT NULL,
    aop_workspace_id INTEGER NULL,

    aop_tool_name TEXT NOT NULL,
    aop_params_json TEXT NOT NULL,

    aop_status TEXT NOT NULL,

    aop_created_at TEXT NOT NULL,
    aop_expires_at TEXT NOT NULL,

    aop_resolved_by_user_id INTEGER NULL,
    aop_resolved_at TEXT NULL,

    aop_executed_at TEXT NULL,
    aop_result_json TEXT NULL,

    FOREIGN KEY (aop_agent_id) REFERENCES a_agents (a_id) ON DELETE CASCADE,
    FOREIGN KEY (aop_workspace_id) REFERENCES w_workspaces (w_id) ON DELETE CASCADE,
    FOREIGN KEY (aop_resolved_by_user_id) REFERENCES u_users (u_id) ON DELETE SET NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS index__aop_agent_operations__aop_external_id ON aop_agent_operations (aop_external_id);
CREATE INDEX IF NOT EXISTS index__aop_agent_operations__aop_agent_id ON aop_agent_operations (aop_agent_id);
CREATE INDEX IF NOT EXISTS index__aop_agent_operations__aop_status ON aop_agent_operations (aop_status);
