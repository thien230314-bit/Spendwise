# SpendWise Backend operating rules

## Scope and safety

- Follow the Product Owner's current task and User.md and Admin.md. Their BLOCKER/PENDING markers do not authorize implementation.
- A1 is functional Backend LOCKED (4/4). A2 has five verified functions, including A2.3 under the approved Task 17 restriction policy. Core business logic status: CORE BUSINESS LOGIC READY FOR EXTERNAL API CHECK; external providers and full E2E remain unverified. Evidence: backend/docs/CODEX-TASK17-CORE-CLOSURE.md. Never promote broader release statuses without evidence and approval where required.
- Preserve locked modules and unrelated local work. Frontend/mobile are read-only unless the task explicitly authorizes them.
- Never force-push, rewrite remote history, reset --hard, restore a whole checkpoint over source, change live SQL data or apply production migrations without explicit task authorization.
- Do not call real providers, reset credentials or run DB-writing tests without verified isolated fixtures and authority.

## Before implementation

1. Record current HEAD, branch, remote and git status. Read applicable nested AGENTS.md instructions.
2. Fetch origin/main and compare with local HEAD. Do not overwrite local/uncommitted work. Resolve remote divergence/conflicts safely; stop for owner direction if ambiguous.
3. Limit edits to the approved task. Do not absorb another agent's files into your commit.

## Verification and automatic publish

- Run a proportionate build and focused/related regression tests after the final source change. Verify DB isolation before integration tests. Report actual PASS/FAIL/SKIP; do not reuse old PASS as a current run.
- When relevant gates PASS and the scoped code is safe, automatically commit and push to main. PARTIAL work may be committed when its implemented portion passes and its PARTIAL status/blockers are documented honestly.
- If build/tests FAIL, secrets are suspected, or remote conflict is unresolved, do not push. Preserve local progress/checkpoint and report the exact blocker.
- Stage explicit scoped paths; inspect staged diff and scan ALL staged blobs for credentials, tokens, private keys, sensitive connection strings and real personal data, including docs/scripts/assets. .gitignore is not a secret scan. Never print secret values.
- Never publish checkpoints, local config/secrets, DB/backups, ZIPs, logs, test results, generated artifacts or sensitive screenshots. Keep real environment values out of tracked files; document safe placeholders/config sources.
- Fetch again before push; no force option. If remote moved, reconcile safely and rerun affected gates. Do not auto-delete files or weaken checks to make push succeed.
- Do not run a whole-system audit or create a source ZIP after every task unless requested. Do not start the next module without approval.

## Task handoff

Report module LOCKED/PARTIAL/BLOCKED; actual build/tests; changed files; commit SHA; GitHub commit link; push PASS/FAIL; remaining issues and checks NOT RUN.
