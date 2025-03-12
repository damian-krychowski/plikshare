ALTER TABLE fu_file_uploads ADD COLUMN fu_parent_file_id INTEGER NULL REFERENCES fi_files(fi_id);
ALTER TABLE fu_file_uploads ADD COLUMN fu_file_metadata BLOB NULL;
CREATE INDEX IF NOT EXISTS index__fu_file_uploads__fu_parent_file_id ON fu_file_uploads (fu_parent_file_id);