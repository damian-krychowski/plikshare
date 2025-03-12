ALTER TABLE fi_files ADD COLUMN fi_parent_file_id INTEGER NULL REFERENCES fi_files(fi_id);
CREATE INDEX IF NOT EXISTS index__fi_files__fi_parent_file_id ON fi_files (fi_parent_file_id);