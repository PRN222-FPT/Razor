@AGENTS.md

## Claude Code

- Use the repository `AGENTS.md` as the canonical source of project rules.
- Load project skills from `.agents/skills` or the thin shims under `.claude/skills` when a task matches a skill.
- Prefer plan-first behavior for changes that touch authentication, database schema, security, public UI, or tests.
