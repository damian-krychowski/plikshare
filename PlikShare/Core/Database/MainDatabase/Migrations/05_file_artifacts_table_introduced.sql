-- 1.fa_file_artifacts table introductions
CREATE TABLE IF NOT EXISTS fa_file_artifacts
(
    fa_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    fa_external_id TEXT NOT NULL,
    fa_workspace_id INTEGER NOT NULL,
    fa_file_id INTEGER NOT NULL,

    fa_type TEXT NOT NULL,
    fa_content BLOB NOT NULL,

    fa_owner_identity_type TEXT NOT NULL,
    fa_owner_identity TEXT NOT NULL,
    fa_created_at TEXT NOT NULL,

    fa_uniqueness_id TEXT,

    FOREIGN KEY (fa_workspace_id) REFERENCES w_workspaces (w_id),
    FOREIGN KEY (fa_file_id) REFERENCES fi_files (fi_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS index__fa_file_artifacts__fa_type ON fa_file_artifacts (fa_type);
CREATE UNIQUE INDEX IF NOT EXISTS index__fa_file_artifacts__fa_external_id ON fa_file_artifacts (fa_external_id);
CREATE UNIQUE INDEX IF NOT EXISTS index__fa_file_artifacts__fa_uniqueness_id ON fa_file_artifacts (fa_uniqueness_id);