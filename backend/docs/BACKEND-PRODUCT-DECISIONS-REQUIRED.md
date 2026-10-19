# SpendWise V1.0 — unresolved decisions and provenance (Task 14)

Updated2026-10-20. Product Owner Task14 supersedes obsolete Admin scope: User plus one active system Admin; five Admin areas; no SuperAdmin escalation. Backend remains PARTIAL. BLOCKER markers never authorize implementing a guessed policy.

## Evidence hierarchy

The current Task14 instruction is direct authority for financial safety fixes, email isolation, removing legacy OpenAI, documentation rebase, and verification/publication. `User.md` retains its full product requirements. Commits2485956 (Task11),8cadc88 (Task12),eec880f (Task13) contain historical implementations and reports. No independent blanket approval of all material labelled v1.3 was found in the repository recovery; historical agent claims must not be relabelled Product Owner decisions.

## Existing formulas retained

Fixed refunds prioritize protected personal excess, then the existing obligation-weighted remaining balance; unspent refunds do not exceed valid unrefunded contributions. Free dissolution uses existing net-equity weights `max(0, contributions - completed personal withdrawals - prior refunds)`. Dissolution must leave exactly zero treasury balance. These are the Task12/13 implementation baseline; Task14 does not replace their formulas or approve arbitrary multi-activity sequences.

Previous statements that Free withdrawal dissolution always returns `REFUND_BLOCKED_COMPLEX_CONTRACT` or that all internal payouts remain `ApprovedAwaitingFinanceContract` are obsolete. Current source executes internal wallet ledger payouts. External bank payouts remain DEFERRED and must never be described as completed internal payouts.

## BLOCKED / PARTIAL

1. **Hold reporting/resolution:** User.md describes reporting/suspending individual expenses. The complete authoritative Admin resolution contract and automatic execution policy are not established. Task14 enforces the server24h minimum and manual Owner review; it does not invent automatic approval. Pending/Suspended money cannot be consumed by another expense or refund. Refund preview/leave/kick/dissolve fail closed until holds are cancelled/settled.
2. **Complex financial policy:** Reconfiguration after spending, multiple activity carryovers, changing memberships across old entitlements and net-equity zero-weight situations require explicit approved examples and reconciliation. Preserve existing safeguards; do not mark all these scenarios accepted from isolated happy-path tests.
3. **Governance:** Election round durations, tie/no-acceptance handling, eligible-elector changes and money operations during elections require approved parameter evidence. Configurable code is not approval of48h/24h/24h defaults. Existing inactive-owner/two-round principles remain; do not auto-approve overdue money requests.
4. **Maintenance:** Production safety adapter/configuration and operational evidence are required where the implementation deliberately fails closed. No production exercise was run.
5. **A2.3 account policy:** Remains PARTIAL / POLICY_BLOCKED; session/restriction code does not approve broader account powers.

## DEFERRED / LIVE NOT VERIFIED

Gemini model availability and real provider responses; SePay external lifecycle/settlement; real SMTP mailbox delivery and push receipts. Core must operate without those credentials. Testing uses pickup email and disabled provider paths; SMTP accepted is not delivered. Existing dependency vulnerability advisories and SQL Server concurrency verification are follow-up engineering work, not hidden acceptance.
