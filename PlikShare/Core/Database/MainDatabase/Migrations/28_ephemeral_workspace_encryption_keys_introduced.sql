CREATE TABLE ewek_ephemeral_workspace_encryption_keys (
    ewek_workspace_id INTEGER NOT NULL,
    ewek_user_id INTEGER NOT NULL,
    ewek_storage_dek_version INTEGER NOT NULL,
    ewek_encrypted_workspace_dek BLOB NOT NULL,
    ewek_created_at TEXT NOT NULL,
    ewek_expires_at TEXT NOT NULL,
    ewek_created_by_user_id INTEGER NULL,
    PRIMARY KEY (ewek_workspace_id, ewek_user_id, ewek_storage_dek_version),
    FOREIGN KEY (ewek_workspace_id) REFERENCES w_workspaces(w_id) ON DELETE CASCADE,
    FOREIGN KEY (ewek_user_id) REFERENCES u_users(u_id) ON DELETE CASCADE,
    FOREIGN KEY (ewek_created_by_user_id) REFERENCES u_users(u_id) ON DELETE SET NULL
);

CREATE INDEX idx_ewek_user_id ON ewek_ephemeral_workspace_encryption_keys (ewek_user_id);
