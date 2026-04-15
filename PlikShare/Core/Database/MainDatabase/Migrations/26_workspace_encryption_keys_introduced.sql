CREATE TABLE wek_workspace_encryption_keys (
    wek_workspace_id INTEGER NOT NULL,
    wek_user_id INTEGER NOT NULL,
    wek_wrapped_workspace_dek BLOB NOT NULL,
    wek_wrapped_at TEXT NOT NULL,
    wek_wrapped_by_user_id INTEGER NULL,
    PRIMARY KEY (wek_workspace_id, wek_user_id),
    FOREIGN KEY (wek_workspace_id) REFERENCES w_workspaces(w_id) ON DELETE CASCADE,
    FOREIGN KEY (wek_user_id) REFERENCES u_users(u_id) ON DELETE CASCADE,
    FOREIGN KEY (wek_wrapped_by_user_id) REFERENCES u_users(u_id) ON DELETE SET NULL
);

CREATE INDEX idx_wek_user_id ON wek_workspace_encryption_keys (wek_user_id);
