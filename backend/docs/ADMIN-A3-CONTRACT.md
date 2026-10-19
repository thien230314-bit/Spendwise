# Admin A3 group investigation — PARTIAL, NOT LOCKED

Date: 2026-10-19. Baseline: `5a9f778c4578eb5d050dc594f1ec8326adeb56e9`.
A1 remains LOCKED. A2 retains four DONE functions and A2.3 POLICY_BLOCKED. This does not approve production operation or complete Backend V1.0.

## HTTP contract

All routes are POST below `/api/v1/admin/support/{caseId}/funds`. Supply the normal access token privately, never in the body or URL. Every body requires a meaningful `reason` (maximum 1000 characters). Canonical Admin needs the assigned active support case; SuperAdmin needs an assigned case or an escalated case claimed by that actor. Legacy SystemAdmin is not implicitly promoted. A current, verified, active operator with an unrevoked session is required. The subject must remain an ordinary User.

| Suffix | Body beyond reason | Source / result |
| --- | --- | --- |
| `search` | `code?`, `kind?`, `status?`, `relatedUserId?`, `createdFromUtc?`, `createdToUtc?`, `page=1`, `pageSize=20` | Scoped V1 funds plus distinct Legacy groups; exact SPW, not a global directory |
| `{fundId}/detail` | none | Stored founder/owner/revision/dissolution, explicit unknown Legacy fields |
| `{fundId}/members` | `status?` Current/Historical, paging | V1 membership intervals, current Legacy membership, Fixed spending flag |
| `{fundId}/activity` | paging | Persisted nonfinancial event IDs, actor/subject, UTC time, reference, Recorded outcome |
| `{fundId}/finance/{source}` | `status?`, paging | V1 Contributions/Requests; Legacy Transactions/Refunds |
| `{fundId}/postings/{postingId}` | none | Actual linked fund posting and at most 100 entries, explicit truncation |
| `{fundId}/treasury` | none | Stored ledger balance, completed net, qualified zero evidence; never proof all refunds finished |
| `{fundId}/governance/{source}` | `parentId?`, `status?`, paging | Elections/Ballots/Candidates/Electorate/OwnershipOffers/ActivityProposals/ActivityVoters |
| `{fundId}/cases` | paging | Only the current authorized case with a structured fund reference or the subject's linked financial posting; evidence count, existing case action/detail endpoints |

Search `kind`: Fixed, Free, Legacy. Response Legacy taxonomy is `Legacy:<stored type>`, not V1. Capacities: Fixed 30, Free 6, Legacy unknown. Status: Active, Dissolved, Suspended, DeletionPending. Dates are UTC, from inclusive/to exclusive. RelatedUserId, if supplied, must be the case subject. Page size is bounded by the existing InputBoundary policy. Stable time/ID ordering and authoritative CountAsync totals precede paging. Feeds return `{observedAtUtc,page,pageSize,total,items}` using the existing A2Page contract.

200 means an authorized read, not financial execution. 400 means invalid filter/source/reason, including Free activity voting and V1 governance requested for Legacy. 403 denies role/session/case/subject/fund/posting scope; 404 may report a missing case. Do not interpret an empty collection as proof that a financial operation finished.

## Authorization and audit

V1CaseWorkflow is reused unchanged. Its transaction locks actor/subject admission, operator session and case before revalidating current authorization. Fund detail feeds take the existing fund root row and recheck retained subject membership. These are no-op admission updates, not financial or membership mutations. Audit writes are intentional. Successful case/dataset and fund reads are audited with actor, case, subject, fund, purpose and dataset; rejected initial permission/reason and workflow scope failures use the existing denial paths. No user-supplied reason grants scope.

SPW lookup cannot substitute for case assignment. A retained V1 membership, or actual Legacy membership/accepted invitation/join event, supplies subject-to-fund evidence. Dissolution does not remove historical scope. No other case's body, files or private payload is returned. Evidence is counted, not downloaded. Existing case actions remain on V1CaseWorkflow, including its authorization/concurrency rules; A3 has no owner, vote, hold, money or membership mutation endpoint.

Legacy/V1 User endpoints remain separate. HTTP tests exercise `/api/groups` and `/api/v1/funds`: canonical operators and legacy SystemAdmin are denied while ordinary User remains allowed. User AI is User-policy protected; legacy Admin AI provides aggregate summaries, not this case-scoped reader. No alternate route was modified.

## Financial interpretation

- V1 contribution verification requires Completed posting, positive same-currency balanced entries, the fund-account debit, matching contribution BusinessId/type and the correct subject wallet credit. Invalid or incomplete postings require review.
- Legacy rows show actual source state and posting reference. A balanced completed fund-account posting is explicitly `LEDGER_POSTING_BALANCED_BUSINESS_BINDING_UNVERIFIED`; the reader does not pretend to reconstruct every historical business association.
- Requests retain requester/beneficiary/reviewer/cancellation metadata. Approval alone remains `FINANCE_BLOCKED`, with no fabricated posting or hold.
- Missing treasury is unknown, not zero. Qualified zero ledger evidence is separate from dissolution/refund completion. Missing hold execution contract is explicit, not a zero held amount.
- Raw activity JSON, election policy JSON, descriptions, passwords, credentials, tokens, attachment bytes and hashing fields are not returned. Only an ON/OFF spending-permission detail is allowlisted. Distinct persisted event IDs are retained; identical labels do not prove duplicate events.

## Acceptance matrix against Admin.md A3

| Function | Status | Implemented / remaining |
| --- | --- | --- |
| A3.1 SPW lookup | DONE | Exact code, case scope, invalid/missing code, foreign fund, closed case, canonical roles tested |
| A3.2 directory | DONE | Scoped Fixed/Free/Legacy, filters, stable paging/count, no balances/member details in directory |
| A3.3 membership/ownership | DONE | Stored founder/current owner, V1 current/past intervals, permissions, actual ownership offers/election records; Legacy missing fields explicitly unknown |
| A3.4 operational timeline | PARTIAL | Actual events, safe permission detail, actor/UTC/reference and paging; full typed cause/correlation/outcome enrichment and invitation-event crosschecks remain, not a policy blocker |
| A3.5 financial investigation | PARTIAL | Contributions, blocked requests, Legacy history/refunds, posting drill-down/treasury; strict Legacy business binding and dedicated Legacy refund/expense regression coverage remain |
| A3.6 governance | DONE | Stored elections, rounds/votes, electorate/candidates, transfers, Fixed-only activity proposals; no policy readiness or forced outcome inferred |
| A3.7 dissolved history | PARTIAL | Dissolved filter, retained membership/code/founder, explicit non-active flag, unknown refund/zero evidence; end-to-end historical refund/dissolution evidence coverage remains |
| A3.8 complained funds | PARTIAL | Deduplicated current assigned/claimed case, persisted fund/posting linkage, evidence count and existing actions; structured expense-report/hold/dispute investigation coverage is incomplete. No group-wide freeze or release/capture was added |

These are implementation/verification gaps, not blanket FINANCE_BLOCKED excuses. Existing User execution that lacks an approved finance contract remains independently FINANCE_BLOCKED. Do not mark A3 8/8 or LOCKED.

## Verification and limits

Evidence is local and excluded from public Git under `checkpoints/A3-EVIDENCE/`. Initial RED compilation established missing reader methods; a later real SQLite test exposed a projection-translation error and the projection was corrected without client-side unbounded paging. The original fixture foreign-key error was corrected by using the actual membership ID.

Mutation control deliberately removed fund-ID scope: both Admin/SuperAdmin lookup cases failed at the expected Forbidden assertion. The guard was restored and its SHA-256 matched the pre-mutation file. The file later received only the code-length boundary change before final regression. A separately named legacy-route RED run actually passed all four cases, proving existing protection; it is not evidence of a new Legacy defect or a code fix.

Final verification results are recorded in the closure section after runner completion. Full unfiltered suite, live API/provider E2E and SQL02 concurrency are not implied by SQLite HTTP tests. Read serialization can contend with fund writers; production SQL concurrency and query-plan behavior remain unverified in this batch. No schema/entity, User finance handler, locked A1/A2 implementation, Frontend or Mobile changes were made.

### Final runner evidence

`A3-final-regression-verified.trx`: 91 PASS / 0 FAIL / 0 SKIP after the final production/test edit. Breakdown: A3 22 (13 reader/scope/history, 5 finance, 4 HTTP); A1 14; A2 15; OpenAPI 2; SocialBankGroupPhase 24; V1CaseWorkflow 2; V1Permission 12. The requested `V1FundTests` filter fragment matches no existing class; no broader V1 fund-finance suite is claimed. Full suite was NOT RUN.

SQL02 preflight only: exact candidate identity confirmed; both SQL memory-pressure flags false; zero owned test catalogs; free SQL-volume bytes 2,934,034,432; available RAM 1,415,584 KB, below the existing 1,500,000 KB gate by 84,416 KB. SQL02 tests were NOT RUN; no threshold was lowered, service restarted, catalog created or manual cleanup performed.

`dotnet build SpendWise.slnx -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false`: exit 0, 0 errors, 4 existing NU1902/NU1903 warnings for System.Security.Cryptography.Pkcs 9.0.4 in Infrastructure and the operational recovery project. No package was added/upgraded; known dependency advisories remain a separate risk. No EF pending-change command was run; no model/migration file changed.

Rollback only this commit's additive A3 readers/controller/registration/contracts/tests/docs after review, preserving subsequent work. No database rollback, checkpoint restore or data rewrite is required. No migration was created/applied and no SQL01 connection was used. No routine source ZIP was created.
