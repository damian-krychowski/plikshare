CREATE TABLE IF NOT EXISTS itj_integrations_textract_jobs
(
    itj_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    itj_external_id TEXT NOT NULL,    

    itj_original_file_id INTEGER NOT NULL,
    itj_original_workspace_id INTEGER NOT NULL,

    itj_textract_workspace_id INTEGER NOT NULL,    
    itj_textract_integration_id INTEGER NOT NULL,

    itj_textract_file_id INTEGER,
    itj_textract_analysis_job_id TEXT,

    itj_status TEXT NOT NULL,
    itj_definition TEXT NOT NULL,

    itj_owner_identity_type TEXT NOT NULL,
    itj_owner_identity TEXT NOT NULL,
    itj_created_at TEXT NOT NULL,
                
    FOREIGN KEY (itj_original_file_id) REFERENCES fi_files (fi_id),
    FOREIGN KEY (itj_textract_file_id) REFERENCES fi_files (fi_id),
    FOREIGN KEY (itj_original_workspace_id) REFERENCES w_workspaces (w_id),
    FOREIGN KEY (itj_textract_workspace_id) REFERENCES w_workspaces (w_id),
    FOREIGN KEY (itj_textract_integration_id) REFERENCES i_integrations (i_id)
);

CREATE INDEX IF NOT EXISTS index__itj_integrations_textract_jobs__itj_original_workspace_id ON itj_integrations_textract_jobs (itj_original_workspace_id);
CREATE INDEX IF NOT EXISTS index__itj_integrations_textract_jobs__itj_original_file_id ON itj_integrations_textract_jobs (itj_original_file_id);
CREATE INDEX IF NOT EXISTS index__itj_integrations_textract_jobs__itj_textract_workspace_id ON itj_integrations_textract_jobs (itj_textract_workspace_id);
CREATE INDEX IF NOT EXISTS index__itj_integrations_textract_jobs__itj_textract_file_id ON itj_integrations_textract_jobs (itj_textract_file_id);
CREATE UNIQUE INDEX IF NOT EXISTS index__itj_integrations_textract_jobs__itj_external_id ON itj_integrations_textract_jobs (itj_external_id);