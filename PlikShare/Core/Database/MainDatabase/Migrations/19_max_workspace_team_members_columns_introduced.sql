ALTER TABLE w_workspaces ADD COLUMN w_max_team_members INTEGER NULL;
ALTER TABLE u_users ADD COLUMN u_default_max_workspace_team_members INTEGER NULL;