CREATE TABLE IF NOT EXISTS qsh_quick_shares
(
    qsh_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    qsh_external_id TEXT NOT NULL,
    qsh_workspace_id INTEGER NOT NULL,
    qsh_creator_id INTEGER NOT NULL,

    -- Public URL identifier. Always present, can be auto-generated or user-chosen.
    qsh_slug TEXT NOT NULL,

    -- SHA-256 of the high-entropy `?token=` query-param. Only set for full-encryption
    -- workspaces (future). Plaintext token is shown to the creator ONCE and never stored.
    qsh_secret_hash BLOB NULL,

    qsh_name TEXT NOT NULL,
    qsh_created_at TEXT NOT NULL,
    qsh_expires_at TEXT NULL,
    qsh_password_hash TEXT NULL,
    qsh_password_salt BLOB NULL,
    qsh_max_downloads INTEGER NULL,
    qsh_downloads_count INTEGER NOT NULL,
    qsh_mode TEXT NOT NULL,
    qsh_allow_individual_file_download BOOLEAN NOT NULL,
    qsh_last_accessed_at TEXT NULL,

    FOREIGN KEY (qsh_workspace_id) REFERENCES w_workspaces (w_id),
    FOREIGN KEY (qsh_creator_id)   REFERENCES u_users      (u_id)
);

CREATE UNIQUE INDEX IF NOT EXISTS unique__qsh__external_id
    ON qsh_quick_shares (qsh_external_id);

CREATE UNIQUE INDEX IF NOT EXISTS unique__qsh__slug
    ON qsh_quick_shares (qsh_slug);

CREATE UNIQUE INDEX IF NOT EXISTS unique__qsh__secret_hash
    ON qsh_quick_shares (qsh_secret_hash) WHERE qsh_secret_hash IS NOT NULL;

CREATE INDEX IF NOT EXISTS index__qsh__workspace_id
    ON qsh_quick_shares (qsh_workspace_id);

CREATE INDEX IF NOT EXISTS index__qsh__expires_at
    ON qsh_quick_shares (qsh_expires_at) WHERE qsh_expires_at IS NOT NULL;


CREATE TABLE IF NOT EXISTS qshi_quick_share_items
(
    qshi_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    qshi_quick_share_id INTEGER NOT NULL,
    qshi_file_id INTEGER NULL,
    qshi_folder_id INTEGER NULL,
    qshi_is_excluded BOOLEAN NOT NULL,

    FOREIGN KEY (qshi_quick_share_id) REFERENCES qsh_quick_shares (qsh_id) ON DELETE CASCADE,
    FOREIGN KEY (qshi_file_id)        REFERENCES fi_files         (fi_id) ON DELETE CASCADE,
    FOREIGN KEY (qshi_folder_id)      REFERENCES fo_folders       (fo_id) ON DELETE CASCADE,

    CHECK (
        (qshi_file_id IS NOT NULL AND qshi_folder_id IS NULL)
        OR
        (qshi_file_id IS NULL     AND qshi_folder_id IS NOT NULL)
    )
);

CREATE INDEX IF NOT EXISTS index__qshi__qsh_id
    ON qshi_quick_share_items (qshi_quick_share_id);

CREATE INDEX IF NOT EXISTS index__qshi__file_id
    ON qshi_quick_share_items (qshi_file_id) WHERE qshi_file_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS index__qshi__folder_id
    ON qshi_quick_share_items (qshi_folder_id) WHERE qshi_folder_id IS NOT NULL;
