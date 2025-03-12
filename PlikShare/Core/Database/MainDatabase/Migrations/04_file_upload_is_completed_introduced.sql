-- 1. fu_file_uploads table changes
ALTER TABLE fu_file_uploads ADD COLUMN fu_is_completed BOOLEAN DEFAULT FALSE NOT NULL;