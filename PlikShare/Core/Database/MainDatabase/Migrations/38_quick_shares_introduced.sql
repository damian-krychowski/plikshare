CREATE TABLE IF NOT EXISTS qs_quick_shares
(
    qs_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    qs_external_id TEXT NOT NULL,
    qs_workspace_id INTEGER NOT NULL,
    qs_creator_id INTEGER NOT NULL,

    -- Public URL identifier. Always present, can be auto-generated or user-chosen.
    qs_slug TEXT NOT NULL,

    -- SHA-256 of the high-entropy `?token=` query-param. Only set for full-encryption
    -- workspaces (future). Plaintext token is shown to the creator ONCE and never stored.
    qs_secret_hash BLOB NULL,

    qs_name TEXT NOT NULL,
    qs_created_at TEXT NOT NULL,
    qs_expires_at TEXT NULL,
    qs_password_hash TEXT NULL,
    qs_password_salt BLOB NULL,
    qs_max_downloads INTEGER NULL,
    qs_downloads_count INTEGER NOT NULL,
    qs_mode TEXT NOT NULL,
    qs_allow_individual_file_download BOOLEAN NOT NULL,
    qs_last_accessed_at TEXT NULL,

    FOREIGN KEY (qs_workspace_id) REFERENCES w_workspaces (w_id),
    FOREIGN KEY (qs_creator_id)   REFERENCES u_users      (u_id)
);

CREATE UNIQUE INDEX IF NOT EXISTS unique__qs__external_id
    ON qs_quick_shares (qs_external_id);

CREATE UNIQUE INDEX IF NOT EXISTS unique__qs__slug
    ON qs_quick_shares (qs_slug);

CREATE UNIQUE INDEX IF NOT EXISTS unique__qs__secret_hash
    ON qs_quick_shares (qs_secret_hash) WHERE qs_secret_hash IS NOT NULL;

CREATE INDEX IF NOT EXISTS index__qs__workspace_id
    ON qs_quick_shares (qs_workspace_id);

CREATE INDEX IF NOT EXISTS index__qs__expires_at
    ON qs_quick_shares (qs_expires_at) WHERE qs_expires_at IS NOT NULL;


CREATE TABLE IF NOT EXISTS qsi_quick_share_items
(
    qsi_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    qsi_quick_share_id INTEGER NOT NULL,
    qsi_file_id INTEGER NULL,
    qsi_folder_id INTEGER NULL,
    qsi_is_excluded BOOLEAN NOT NULL,

    FOREIGN KEY (qsi_quick_share_id) REFERENCES qs_quick_shares (qs_id) ON DELETE CASCADE,
    FOREIGN KEY (qsi_file_id)        REFERENCES fi_files        (fi_id) ON DELETE CASCADE,
    FOREIGN KEY (qsi_folder_id)      REFERENCES fo_folders      (fo_id) ON DELETE CASCADE,

    CHECK (
        (qsi_file_id IS NOT NULL AND qsi_folder_id IS NULL)
        OR
        (qsi_file_id IS NULL     AND qsi_folder_id IS NOT NULL)
    )
);

CREATE INDEX IF NOT EXISTS index__qsi__qs_id
    ON qsi_quick_share_items (qsi_quick_share_id);

CREATE INDEX IF NOT EXISTS index__qsi__file_id
    ON qsi_quick_share_items (qsi_file_id) WHERE qsi_file_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS index__qsi__folder_id
    ON qsi_quick_share_items (qsi_folder_id) WHERE qsi_folder_id IS NOT NULL;
