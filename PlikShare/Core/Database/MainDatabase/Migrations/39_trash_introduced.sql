ALTER TABLE fi_files ADD COLUMN fi_deleted_at TEXT NULL;

-- JSON array `[{"folderId":<id>,"rawName":"<name>"}, ...]` snapshotting the file's folder
-- ancestry at trash-time. TEXT (not BLOB) so workspace-encrypted values can be stored as
-- the "pse:..."-prefixed envelope used by other encryptable string columns (e.g. fi_name).
ALTER TABLE fi_files ADD COLUMN fi_original_folder_path TEXT NULL;

CREATE INDEX IF NOT EXISTS index__fi_files__fi_workspace_id__fi_deleted_at
    ON fi_files (fi_workspace_id, fi_deleted_at)
    WHERE fi_deleted_at IS NOT NULL;

-- Trash policy JSON: `{"enabled":false}` or `{"enabled":true,"retentionDays":30}`.
-- Default is disabled so existing rows keep their current behaviour (hard-delete);
-- new feature is strictly opt-in for both storages and workspaces.
ALTER TABLE s_storages
    ADD COLUMN s_default_trash_policy_json TEXT NOT NULL
    DEFAULT '{"enabled":false}';

ALTER TABLE w_workspaces
    ADD COLUMN w_trash_policy_json TEXT NOT NULL
    DEFAULT '{"enabled":false}';
