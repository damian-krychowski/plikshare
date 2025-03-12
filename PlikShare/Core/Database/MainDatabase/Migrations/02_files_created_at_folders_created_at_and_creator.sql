-- 1. Add fi_created_at to fi_files
ALTER TABLE fi_files ADD COLUMN fi_created_at TEXT NULL;

-- 2. Add new columns to fo_folders
ALTER TABLE fo_folders ADD COLUMN fo_created_at TEXT NULL;
ALTER TABLE fo_folders ADD COLUMN fo_creator_identity_type TEXT NULL;
ALTER TABLE fo_folders ADD COLUMN fo_creator_identity TEXT NULL;
CREATE INDEX IF NOT EXISTS index__fo_folders__fo_creator_identity_and_type ON fo_folders (fo_creator_identity_type, fo_creator_identity);