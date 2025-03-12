-- 1. Add nullable workspace_id column with foreign key
ALTER TABLE i_integrations ADD COLUMN i_workspace_id INTEGER NULL REFERENCES w_workspaces(w_id);

-- 2. Update existing textract integrations with workspace_id
UPDATE i_integrations
SET i_workspace_id = (
    SELECT itw_workspace_id 
    FROM itw_integrations_textract_workspace 
    WHERE itw_integration_id = i_integrations.i_id
);

-- 3. Drop the junction table
DROP TABLE itw_integrations_textract_workspace;

-- 4. Add index for the new column
CREATE INDEX IF NOT EXISTS index__i_integrations__i_workspace_id ON i_integrations (i_workspace_id);