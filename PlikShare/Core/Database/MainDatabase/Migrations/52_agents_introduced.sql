CREATE TABLE IF NOT EXISTS a_agents
(
    a_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    a_external_id TEXT NOT NULL,

    a_owner_user_id INTEGER NOT NULL,

    a_name TEXT NOT NULL,
    a_is_enabled BOOLEAN NOT NULL,

    a_is_admin BOOLEAN NOT NULL DEFAULT FALSE,
    a_can_add_workspace BOOLEAN NOT NULL DEFAULT FALSE,
    a_can_manage_general_settings BOOLEAN NOT NULL DEFAULT FALSE,
    a_can_manage_users BOOLEAN NOT NULL DEFAULT FALSE,
    a_can_manage_storages BOOLEAN NOT NULL DEFAULT FALSE,
    a_can_manage_email_providers BOOLEAN NOT NULL DEFAULT FALSE,
    a_can_manage_auth BOOLEAN NOT NULL DEFAULT FALSE,
    a_can_manage_integrations BOOLEAN NOT NULL DEFAULT FALSE,
    a_can_manage_audit_log BOOLEAN NOT NULL DEFAULT FALSE,
    a_can_manage_agents BOOLEAN NOT NULL DEFAULT FALSE,

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

    FOREIGN KEY (at_agent_id) REFERENCES a_agents (a_id)
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
    FOREIGN KEY (wa_workspace_id) REFERENCES w_workspaces (w_id),
    FOREIGN KEY (wa_agent_id) REFERENCES a_agents (a_id)
);

CREATE INDEX IF NOT EXISTS index__wa_workspace_agents__wa_workspace_id ON wa_workspace_agents (wa_workspace_id);
CREATE INDEX IF NOT EXISTS index__wa_workspace_agents__wa_agent_id ON wa_workspace_agents (wa_agent_id);

CREATE TABLE IF NOT EXISTS ba_box_agents
(
    ba_box_id INTEGER NOT NULL,
    ba_agent_id INTEGER NOT NULL,

    ba_allow_download BOOLEAN NOT NULL,
    ba_allow_upload BOOLEAN NOT NULL,
    ba_allow_list BOOLEAN NOT NULL,
    ba_allow_delete_file BOOLEAN NOT NULL,
    ba_allow_rename_file BOOLEAN NOT NULL,
    ba_allow_move_items BOOLEAN NOT NULL,
    ba_allow_create_folder BOOLEAN NOT NULL,
    ba_allow_delete_folder BOOLEAN NOT NULL,
    ba_allow_rename_folder BOOLEAN NOT NULL,

    ba_created_at TEXT NOT NULL,

    PRIMARY KEY (ba_box_id, ba_agent_id),
    FOREIGN KEY (ba_box_id) REFERENCES bo_boxes (bo_id),
    FOREIGN KEY (ba_agent_id) REFERENCES a_agents (a_id)
);

CREATE INDEX IF NOT EXISTS index__ba_box_agents__ba_box_id ON ba_box_agents (ba_box_id);
CREATE INDEX IF NOT EXISTS index__ba_box_agents__ba_agent_id ON ba_box_agents (ba_agent_id);

ALTER TABLE w_workspaces ADD COLUMN w_owner_agent_id INTEGER NULL REFERENCES a_agents (a_id);

CREATE INDEX IF NOT EXISTS index__w_workspaces__w_owner_agent_id ON w_workspaces (w_owner_agent_id);
