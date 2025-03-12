CREATE INDEX IF NOT EXISTS index__fo_folders__fo_external_id__fo_workspace_id__fo_is_being_deleted ON fo_folders (
    fo_external_id,
    fo_workspace_id,
    fo_is_being_deleted
);

CREATE INDEX IF NOT EXISTS index__fo_folders__fo_workspace_id__fo_is_being_deleted__fo_id ON fo_folders (
    fo_workspace_id,
    fo_is_being_deleted,
    fo_id
);

CREATE INDEX IF NOT EXISTS index__fo_folders__fo_parent_folder_id__fo_is_being_deleted__fo_id ON fo_folders (
    fo_parent_folder_id,
    fo_is_being_deleted,
    fo_id
);

CREATE INDEX IF NOT EXISTS index__fi_files__fi_workspace_id__fi_folder_id__fi_id ON fi_files (
    fi_workspace_id,
    fi_folder_id,
    fi_id
);

CREATE INDEX IF NOT EXISTS index__fi_files__fi_workspace_id__fi_external_id ON fi_files (
    fi_workspace_id,
    fi_external_id
);