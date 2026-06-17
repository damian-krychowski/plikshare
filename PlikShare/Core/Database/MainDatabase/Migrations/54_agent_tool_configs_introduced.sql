CREATE TABLE IF NOT EXISTS atc_agent_tool_configs
(
    atc_agent_id INTEGER NOT NULL,
    atc_tool_name TEXT NOT NULL,

    atc_is_enabled BOOLEAN NOT NULL,
    atc_requires_approval BOOLEAN NOT NULL,

    PRIMARY KEY (atc_agent_id, atc_tool_name),
    FOREIGN KEY (atc_agent_id) REFERENCES a_agents (a_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS index__atc_agent_tool_configs__atc_agent_id ON atc_agent_tool_configs (atc_agent_id);
