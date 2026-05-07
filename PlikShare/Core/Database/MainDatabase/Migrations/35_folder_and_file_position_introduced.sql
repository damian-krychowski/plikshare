ALTER TABLE fo_folders ADD COLUMN fo_position INTEGER NULL;
ALTER TABLE fi_files ADD COLUMN fi_position INTEGER NULL;

CREATE INDEX IF NOT EXISTS index__fo_folders__fo_parent_folder_id__fo_position
    ON fo_folders (fo_parent_folder_id, fo_position) WHERE fo_position IS NOT NULL;

CREATE INDEX IF NOT EXISTS index__fi_files__fi_folder_id__fi_position
    ON fi_files (fi_folder_id, fi_position) WHERE fi_position IS NOT NULL;
