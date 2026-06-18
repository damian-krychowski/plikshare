CREATE TABLE IF NOT EXISTS a_agents
(
    a_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    a_external_id TEXT NOT NULL,

    a_owner_user_id INTEGER NOT NULL,

    a_name TEXT NOT NULL,
    a_is_enabled BOOLEAN NOT NULL,

    a_max_workspace_number INTEGER NULL,
    a_default_max_workspace_size_in_bytes INTEGER NULL,
    a_default_max_workspace_team_members INTEGER NULL,

    a_storage_access_mode TEXT NOT NULL DEFAULT 'all',

    a_created_at TEXT NOT NULL,

    FOREIGN KEY (a_owner_user_id) REFERENCES u_users (u_id)
);

CREATE UNIQUE INDEX IF NOT EXISTS index__a_agents__a_external_id ON a_agents (a_external_id);
CREATE INDEX IF NOT EXISTS index__a_agents__a_owner_user_id ON a_agents (a_owner_user_id);

CREATE TABLE IF NOT EXISTS at_agent_tokens
(
    at_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    at_agent_id INTEGER NOT NULL,

    at_token_hash TEXT NOT NULL,
    at_token_masked TEXT NOT NULL,

    at_created_at TEXT NOT NULL,
    at_expires_at TEXT NULL,
    at_last_used_at TEXT NULL,
    at_revoked_at TEXT NULL,

    FOREIGN KEY (at_agent_id) REFERENCES a_agents (a_id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS index__at_agent_tokens__at_token_hash ON at_agent_tokens (at_token_hash);
CREATE INDEX IF NOT EXISTS index__at_agent_tokens__at_agent_id ON at_agent_tokens (at_agent_id);

CREATE TABLE IF NOT EXISTS asa_agent_storage_access
(
    asa_agent_id INTEGER NOT NULL,
    asa_storage_id INTEGER NOT NULL,

    PRIMARY KEY (asa_agent_id, asa_storage_id),
    FOREIGN KEY (asa_agent_id) REFERENCES a_agents (a_id) ON DELETE CASCADE,
    FOREIGN KEY (asa_storage_id) REFERENCES s_storages (s_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS index__asa_agent_storage_access__asa_agent_id ON asa_agent_storage_access (asa_agent_id);
CREATE INDEX IF NOT EXISTS index__asa_agent_storage_access__asa_storage_id ON asa_agent_storage_access (asa_storage_id);

CREATE TABLE IF NOT EXISTS wa_workspace_agents
(
    wa_workspace_id INTEGER NOT NULL,
    wa_agent_id INTEGER NOT NULL,

    wa_created_at TEXT NOT NULL,

    PRIMARY KEY (wa_workspace_id, wa_agent_id),
    FOREIGN KEY (wa_workspace_id) REFERENCES w_workspaces (w_id) ON DELETE CASCADE,
    FOREIGN KEY (wa_agent_id) REFERENCES a_agents (a_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS index__wa_workspace_agents__wa_workspace_id ON wa_workspace_agents (wa_workspace_id);
CREATE INDEX IF NOT EXISTS index__wa_workspace_agents__wa_agent_id ON wa_workspace_agents (wa_agent_id);

-- An agent invited to a box. Membership only — what the agent may do is governed by the tool layer
-- (global config + per-box overrides), not by per-box permission flags.
CREATE TABLE IF NOT EXISTS ba_box_agents
(
    ba_box_id INTEGER NOT NULL,
    ba_agent_id INTEGER NOT NULL,

    ba_created_at TEXT NOT NULL,

    PRIMARY KEY (ba_box_id, ba_agent_id),
    FOREIGN KEY (ba_box_id) REFERENCES bo_boxes (bo_id) ON DELETE CASCADE,
    FOREIGN KEY (ba_agent_id) REFERENCES a_agents (a_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS index__ba_box_agents__ba_box_id ON ba_box_agents (ba_box_id);
CREATE INDEX IF NOT EXISTS index__ba_box_agents__ba_agent_id ON ba_box_agents (ba_agent_id);

ALTER TABLE w_workspaces ADD COLUMN w_owner_agent_id INTEGER NULL REFERENCES a_agents (a_id);

CREATE INDEX IF NOT EXISTS index__w_workspaces__w_owner_agent_id ON w_workspaces (w_owner_agent_id);

-- Agent-created quick shares: the creating agent is recorded here, while qsh_creator_id keeps
-- the responsible human owner (for an agent it is the agent's owner).
ALTER TABLE qsh_quick_shares ADD COLUMN qsh_creator_agent_id INTEGER NULL REFERENCES a_agents (a_id) ON DELETE SET NULL;

CREATE INDEX IF NOT EXISTS index__qsh__creator_agent_id
    ON qsh_quick_shares (qsh_creator_agent_id) WHERE qsh_creator_agent_id IS NOT NULL;

CREATE TABLE IF NOT EXISTS atc_agent_tool_configs
(
    atc_agent_id INTEGER NOT NULL,
    atc_tool_name TEXT NOT NULL,

    atc_is_enabled BOOLEAN NOT NULL,
    atc_requires_approval BOOLEAN NOT NULL,

    PRIMARY KEY (atc_agent_id, atc_tool_name),
    FOREIGN KEY (atc_agent_id) REFERENCES a_agents (a_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS index__atc_agent_tool_configs__atc_agent_id ON atc_agent_tool_configs (atc_agent_id);

CREATE TABLE IF NOT EXISTS atwo_agent_tool_workspace_overrides
(
    atwo_agent_id INTEGER NOT NULL,
    atwo_workspace_id INTEGER NOT NULL,
    atwo_tool_name TEXT NOT NULL,

    atwo_is_enabled BOOLEAN NULL,
    atwo_requires_approval BOOLEAN NULL,

    PRIMARY KEY (atwo_agent_id, atwo_workspace_id, atwo_tool_name),
    FOREIGN KEY (atwo_agent_id) REFERENCES a_agents (a_id) ON DELETE CASCADE,
    FOREIGN KEY (atwo_workspace_id) REFERENCES w_workspaces (w_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS index__atwo_agent_tool_workspace_overrides__atwo_agent_id ON atwo_agent_tool_workspace_overrides (atwo_agent_id);
CREATE INDEX IF NOT EXISTS index__atwo_agent_tool_workspace_overrides__atwo_workspace_id ON atwo_agent_tool_workspace_overrides (atwo_workspace_id);

CREATE TABLE IF NOT EXISTS atbo_agent_tool_box_overrides
(
    atbo_agent_id INTEGER NOT NULL,
    atbo_box_id INTEGER NOT NULL,
    atbo_tool_name TEXT NOT NULL,

    atbo_is_enabled BOOLEAN NULL,
    atbo_requires_approval BOOLEAN NULL,

    PRIMARY KEY (atbo_agent_id, atbo_box_id, atbo_tool_name),
    FOREIGN KEY (atbo_agent_id) REFERENCES a_agents (a_id) ON DELETE CASCADE,
    FOREIGN KEY (atbo_box_id) REFERENCES bo_boxes (bo_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS index__atbo_agent_tool_box_overrides__atbo_agent_id ON atbo_agent_tool_box_overrides (atbo_agent_id);
CREATE INDEX IF NOT EXISTS index__atbo_agent_tool_box_overrides__atbo_box_id ON atbo_agent_tool_box_overrides (atbo_box_id);

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
