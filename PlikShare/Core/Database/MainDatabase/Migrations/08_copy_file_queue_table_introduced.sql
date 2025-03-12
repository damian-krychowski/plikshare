CREATE TABLE IF NOT EXISTS cfq_copy_file_queue
(
    cfq_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,

    cfq_file_id INTEGER NOT NULL,
    cfq_source_workspace_id INTEGER NOT NULL,
    cfq_file_upload_id INTEGER NOT NULL,
    cfq_destination_workspace_id INTEGER NOT NULL,
    cfq_upload_algorithm TEXT NOT NULL,
    cfq_status TEXT NOT NULL,
    cfq_on_completed_action TEXT NOT NULL,
    cfq_correlation_id TEXT NOT NULL,
                
    FOREIGN KEY (cfq_file_id) REFERENCES fi_files (fi_id),
    FOREIGN KEY (cfq_file_upload_id) REFERENCES fu_file_uploads (fu_id),
    FOREIGN KEY (cfq_source_workspace_id) REFERENCES w_workspaces (w_id),
    FOREIGN KEY (cfq_destination_workspace_id) REFERENCES  w_workspaces (w_id)
);

CREATE INDEX IF NOT EXISTS index__cfq_copy_file_queue__cfq_file_id ON cfq_copy_file_queue (cfq_file_id);
CREATE INDEX IF NOT EXISTS index__cfq_copy_file_queue__cfq_file_upload_id ON cfq_copy_file_queue (cfq_file_upload_id);
CREATE INDEX IF NOT EXISTS index__cfq_copy_file_queue__cfq_source_workspace_id ON cfq_copy_file_queue (cfq_source_workspace_id);
CREATE INDEX IF NOT EXISTS index__cfq_copy_file_queue__cfq_destination_workspace_id ON cfq_copy_file_queue (cfq_destination_workspace_id);