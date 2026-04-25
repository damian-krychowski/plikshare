ALTER TABLE fa_file_artifacts ADD COLUMN fa_content_hash BLOB NOT NULL DEFAULT (zeroblob(32));
