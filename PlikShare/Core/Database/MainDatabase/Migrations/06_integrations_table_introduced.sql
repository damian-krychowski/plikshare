CREATE TABLE IF NOT EXISTS i_integrations
(
    i_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    i_external_id TEXT NOT NULL,
    
    i_type TEXT NOT NULL,
    i_name TEXT NOT NULL,
    i_is_active BOOLEAN NOT NULL,
    i_details_encrypted BLOB NOT NULL
);

CREATE INDEX IF NOT EXISTS index__i_integrations__i_type ON i_integrations (i_type);
CREATE UNIQUE INDEX IF NOT EXISTS index__i_integrations__i_external_id ON i_integrations (i_external_id);
CREATE UNIQUE INDEX IF NOT EXISTS index__i_integrations__i_name ON i_integrations (i_name);


CREATE TABLE IF NOT EXISTS itw_integrations_textract_workspace
(
    itw_integration_id INTEGER NOT NULL PRIMARY KEY,
    itw_workspace_id INTEGER NOT NULL,
                
    FOREIGN KEY (itw_integration_id) REFERENCES i_integrations (i_id),
    FOREIGN KEY (itw_workspace_id) REFERENCES w_workspaces (w_id)
);

CREATE INDEX IF NOT EXISTS index__itw_integrations_textract_workspace__itw_workspace_id ON itw_integrations_textract_workspace (itw_workspace_id);