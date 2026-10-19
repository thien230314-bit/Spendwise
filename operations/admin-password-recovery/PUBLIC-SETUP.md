# Local recovery tool — review source, do not execute automatically

This tool is not part of normal API startup. Its target instance/database safety guards remain unchanged. Never run it against a real account without a separately approved recovery operation.

The recovery email is supplied through the process environment variable `SPENDWISE_RECOVERY_EMAIL` in the operator's private terminal. Without it, the target is the non-deliverable synthetic `admin@fixture.invalid`; no real account address is shipped. Do not put the actual email, SQL/SMTP credentials, OTP, password or tokens in tracked files or command arguments. Existing configuration is read from authorized local User Secrets by the tool; those are not included in this repository.

Private historical runbooks and the one-off email migration scripts are deliberately excluded from public source. No past execution approval is transferred by publishing this tool. Hidden input and targeted outbox dispatch protections remain mandatory.
