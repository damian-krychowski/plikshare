CREATE TABLE IF NOT EXISTS atwo_agent_tool_workspace_overrides
(
    atwo_agent_id INTEGER NOT NULL,
    atwo_workspace_id INTEGER NOT NULL,
    atwo_tool_name TEXT NOT NULL,

    atwo_is_enabled BOOLEAN NULL,
    atwo_requires_approval BOOLEAN NULL,

    PRIMARY KEY (atwo_agent_id, atwo_workspace_id, atwo_tool_name),
    FOREIGN KEY (atwo_agent_id) REFERENCES a_agents (a_id) ON DELETE CASCADE,
    FOREIGN KEY (atwo_workspace_id) REFERENCES w_workspaces (w_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS index__atwo_agent_tool_workspace_overrides__atwo_agent_id ON atwo_agent_tool_workspace_overrides (atwo_agent_id);
CREATE INDEX IF NOT EXISTS index__atwo_agent_tool_workspace_overrides__atwo_workspace_id ON atwo_agent_tool_workspace_overrides (atwo_workspace_id);
