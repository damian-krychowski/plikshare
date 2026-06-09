CREATE INDEX IF NOT EXISTS index__fi_files__folder_children_metadata
ON fi_files (fi_folder_id)
WHERE
    fi_parent_file_id IS NOT NULL
    AND fi_deleted_at IS NULL
    AND fi_is_upload_completed = TRUE
    AND fi_metadata IS NOT NULL;
