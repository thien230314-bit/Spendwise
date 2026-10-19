# Admin A3 group investigation — 8/8 DONE, FUNCTIONAL BACKEND LOCKED

Date: 2026-10-19. Final-closure baseline: `ecf2acc3a4dc9c4593497df9773a753732d09655`; earlier partial baseline: `5a9f778c4578eb5d050dc594f1ec8326adeb56e9`.
A1 remains LOCKED. A2 retains four DONE functions and A2.3 POLICY_BLOCKED. This does not approve production operation or complete Backend V1.0.

## HTTP contract

All routes are POST below `/api/v1/admin/support/{caseId}/funds`. Supply the normal access token privately, never in the body or URL. Every body requires a meaningful `reason` (maximum 1000 characters). Canonical Admin needs the assigned active support case; SuperAdmin needs an assigned case or an escalated case claimed by that actor. Legacy SystemAdmin is not implicitly promoted. A current, verified, active operator with an unrevoked session is required. The subject must remain an ordinary User.

| Suffix | Body beyond reason | Source / result |
| --- | --- | --- |
| `search` | `code?`, `kind?`, `status?`, `relatedUserId?`, `createdFromUtc?`, `createdToUtc?`, `page=1`, `pageSize=20` | Scoped V1 funds plus distinct Legacy groups; exact SPW, not a global directory |
| `{fundId}/detail` | none | Stored founder/owner/revision/dissolution, explicit unknown Legacy fields |
| `{fundId}/members` | `status?` Current/Historical, paging | V1 membership intervals, current Legacy membership, Fixed spending flag |
| `{fundId}/activity` | paging | Persisted nonfinancial event IDs, actor/subject, UTC, outcome, verified typed correlation, explicit missing cause/history |
| `{fundId}/invitations` | `status?`, paging | Actual V1/Legacy invitation states; never tokens; absent decision times stay unknown |
| `{fundId}/finance/{source}` | `status?`, paging | V1 Contributions/Requests; Legacy Contributions/Expenses/Transactions/Refunds |
| `{fundId}/postings/{postingId}` | none | Actual linked fund posting and at most 100 entries, explicit truncation |
| `{fundId}/treasury` | none | Stored ledger balance, completed net, qualified zero evidence; never proof all refunds finished |
| `{fundId}/governance/{source}` | `parentId?`, `status?`, paging | Elections/Ballots/Candidates/Electorate/OwnershipOffers/ActivityProposals/ActivityVoters |
| `{fundId}/cases` | `status?`, `parentId?` report ID, paging | Assigned/claimed active support tasks and derivative subject disputes with structured fund/posting linkage; deduplicated by source/ID, evidence counts |
| `{fundId}/reports/{source}/{reportId}/history` | paging | Authorized Support/Dispute typed history, actor, status transitions, UTC, evidence count; no narrative or attachment bytes |
| `{fundId}/dissolution` | none | Independent persisted closure, ledger-zero and recipient-refund evidence, source IDs/history routes |
| `{fundId}/expenses/{expenseId}/postings` | `status?`, paging | Actual Legacy expense posting candidates; ambiguity is not resolved by picking an arbitrary posting |

Search `kind`: Fixed, Free, Legacy. Response Legacy taxonomy is `Legacy:<stored type>`, not V1. Capacities: Fixed 30, Free 6, Legacy unknown. Status: Active, Dissolved, Suspended, DeletionPending. Dates are UTC, from inclusive/to exclusive. RelatedUserId, if supplied, must be the case subject. Page size is bounded by the existing InputBoundary policy. Stable time/ID ordering and authoritative CountAsync totals precede paging. Feeds return `{observedAtUtc,page,pageSize,total,items}` using the existing A2Page contract.

200 means an authorized read, not financial execution. 400 means invalid filter/source/reason, including Free activity voting and V1 governance requested for Legacy. 403 denies role/session/case/subject/fund/posting scope; 404 may report a missing case. Do not interpret an empty collection as proof that a financial operation finished.

## Authorization and audit

V1CaseWorkflow is reused unchanged. Its transaction locks actor/subject admission, operator session and case before revalidating current authorization. Fund detail feeds take the existing fund root row and recheck retained subject membership. These are no-op admission updates, not financial or membership mutations. Audit writes are intentional. Successful case/dataset and fund reads are audited with actor, case, subject, fund, purpose and dataset; rejected initial permission/reason and workflow scope failures use the existing denial paths. No user-supplied reason grants scope.

SPW lookup cannot substitute for case assignment. A retained V1 membership, or actual Legacy membership/accepted invitation/join event, supplies subject-to-fund evidence. Dissolution does not remove historical scope. Other support tasks are listed only for the same subject, same fund, current actor's assignment/escalation claim and active status. A dispute has no independent assignee column: it requires the subject's exact fund-linked FinancialTransaction plus an authorized support task for that transaction, or the current task's explicit persisted fund reference. Mere membership, ticket text or a free-form reason cannot grant dispute scope. History scope is rechecked before release. Evidence is counted, not downloaded. Existing actions remain on V1CaseWorkflow; the dispute investigation route points back to the authorized support task for escalation, not a new dispute mutation API. A3 has no owner, vote, hold, money or membership mutation endpoint.

Legacy/V1 User endpoints remain separate. HTTP tests exercise `/api/groups` and `/api/v1/funds`: canonical operators and legacy SystemAdmin are denied while ordinary User remains allowed. User AI is User-policy protected; legacy Admin AI provides aggregate summaries, not this case-scoped reader. No alternate route was modified.

## Financial interpretation

- V1 contribution verification requires Completed posting, positive same-currency balanced entries, the fund-account debit, matching contribution BusinessId/type and the correct subject wallet credit. Invalid or incomplete postings require review.
- Legacy rows are available from original GroupContribution and fund-paid GroupExpense records as well as FundTransaction/GroupRefund history, even when the display-history row is missing. GroupContribution, GroupFundExpense and GroupRefund use the actual writer BusinessType/BusinessId. Verification checks group, business amount, currency, fund/wallet or system clearing direction, completion, wallet owner where stored, and duplicate business postings. Linked FinancialTransaction IDs are returned only for the row's recorded user and matching posting, with amount/currency/status/business/wallet consistency checked. GroupExpense does not persist its creator: original-source user provenance stays NOT_RECORDED; a FundTransaction actor is identified as history-only, not independently proven.
- `LEDGER_COMPLETED` means a bound Completed canonical two-entry posting, not a claim that an arbitrary balanced historical journal belongs to this business. `UNVERIFIED` means missing/unknown business provenance or ambiguous expense posting candidates; `LEDGER_REQUIRES_REVIEW` means mismatched, unbalanced, pending/reversed, duplicate or inconsistent evidence. `MISSING_LEDGER` means no linked posting. Candidate postings remain separately inspectable; no arbitrary first candidate is verified.
- Requests retain requester/beneficiary/reviewer/cancellation metadata. Approval alone remains `FINANCE_BLOCKED`, with no fabricated posting or hold.
- Missing treasury is unknown, not zero. Qualified ledger net-zero evidence, persisted group closure and verified refunds are independent fields. The dissolution reader uses the stored group currency, not an assumed VND conversion. Refund verification compares the frozen deletion-voter recipient set, stored refund rows and snapshot total; positive refunds each need verified posting/wallet evidence. No entitlement formula is recomputed. `VERIFIED_NO_REFUND_DUE` records an evidenced zero obligation, never a fabricated transfer. Missing/unfinished refunds, ledger or recipient coverage remain NOT_VERIFIED even for Closed groups. V1 refund execution remains NOT_IMPLEMENTED.
- Raw JSON, election policy, narratives, credentials, invitation tokens, attachment bytes and hashes are not returned. ON/OFF and known election/activity result enums are allowlisted. Legacy JSON is parsed only for bounded, typed member GUID fields; malformed/missing payload is unavailable and never an authority source. V1 activity references are matched to same-fund invitation/ownership/election/ballot/candidate/proposal records; current related-record state is separate from historical event outcome. Known actor mismatches require review. Cause/history not persisted is NOT_RECORDED, not inferred from current state. Distinct event IDs are retained even when names repeat.
- There is no mapped persisted fund expense hold/report-to-V1-request relation in the current model. HoldState is NOT_IMPLEMENTED, ExpenseRequestLinkage is NOT_AVAILABLE; FINANCE_BLOCKED requests are never presented as captured/released/Completed. A report does not suspend the group. The obsolete SharedExpense domain type has no current DbContext/model mapping or writer; no mapping/schema or guessed legacy source was added just because that class exists.

## Acceptance matrix against Admin.md A3

| Function | Status | Implemented / remaining |
| --- | --- | --- |
| A3.1 SPW lookup | DONE | Exact code, case scope, invalid/missing code, foreign fund, closed case, canonical roles tested |
| A3.2 directory | DONE | Scoped Fixed/Free/Legacy, filters, stable paging/count, no balances/member details in directory |
| A3.3 membership/ownership | DONE | Stored founder/current owner, V1 current/past intervals, permissions, actual ownership offers/election records; Legacy missing fields explicitly unknown |
| A3.4 operational timeline | DONE | Actual event outcomes/correlation and invitation/ownership crosschecks; V1 and Legacy, missing/malformed historical fields, actor mismatch, paging and same-name distinct IDs tested |
| A3.5 financial investigation | DONE | Original business records, strict Ledger binding, FinancialTransaction provenance, candidate ambiguity/drill-down; Legacy contribution/expense/refund and V1 contribution/blocked requests tested; no execution added |
| A3.6 governance | DONE | Stored elections, rounds/votes, electorate/candidates, transfers, Fixed-only activity proposals; no policy readiness or forced outcome inferred |
| A3.7 dissolved history | DONE | Active/dissolved V1 and Legacy, retained case scope, historical owner/source IDs, independent closure/net-zero/recipient refund evidence, pending/missing/zero-due and currency cases tested |
| A3.8 complained funds | DONE | Assigned/claimed support plus derivative disputes, exact subject/fund/transaction and direct fund-task linkage, report history/evidence/navigation, duplicate/foreign/closed/revoked tests; absent future report/hold relations explicit, no whole-fund freeze |

The previous PARTIAL reader gaps are implemented and final gates below passed. Missing historical fields and absent future execution relations are explicit, tested investigation outcomes, not fabricated records or blanket blockers. Existing User execution that lacks an approved finance contract remains independently FINANCE_BLOCKED. A3 is functionally LOCKED for the scoped read-only Backend contract; this is not production SQL/concurrency or financial-execution approval.

## Verification and limits

Evidence is local and excluded from public Git under `checkpoints/A3-EVIDENCE/`. Initial RED compilation established missing reader methods; a later real SQLite test exposed a projection-translation error and the projection was corrected without client-side unbounded paging. The original fixture foreign-key error was corrected by using the actual membership ID.

Mutation control deliberately removed fund-ID scope: both Admin/SuperAdmin lookup cases failed at the expected Forbidden assertion. The guard was restored and its SHA-256 matched the pre-mutation file. The file later received only the code-length boundary change before final regression. A separately named legacy-route RED run actually passed all four cases, proving existing protection; it is not evidence of a new Legacy defect or a code fix.

Final verification results are recorded in the closure section after runner completion. Full unfiltered suite, live API/provider E2E and SQL02 concurrency are not implied by SQLite HTTP tests. Read serialization can contend with fund writers; production SQL concurrency and query-plan behavior remain unverified in this batch. No schema/entity, User finance handler, locked A1/A2 implementation, Frontend or Mobile changes were made.

### Historical partial runner evidence (before final closure)

`A3-final-regression-verified.trx`: 91 PASS / 0 FAIL / 0 SKIP after the final production/test edit. Breakdown: A3 22 (13 reader/scope/history, 5 finance, 4 HTTP); A1 14; A2 15; OpenAPI 2; SocialBankGroupPhase 24; V1CaseWorkflow 2; V1Permission 12. The requested `V1FundTests` filter fragment matches no existing class; no broader V1 fund-finance suite is claimed. Full suite was NOT RUN.

SQL02 preflight only: exact candidate identity confirmed; both SQL memory-pressure flags false; zero owned test catalogs; free SQL-volume bytes 2,934,034,432; available RAM 1,415,584 KB, below the existing 1,500,000 KB gate by 84,416 KB. SQL02 tests were NOT RUN; no threshold was lowered, service restarted, catalog created or manual cleanup performed.

`dotnet build SpendWise.slnx -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false`: exit 0, 0 errors, 4 existing NU1902/NU1903 warnings for System.Security.Cryptography.Pkcs 9.0.4 in Infrastructure and the operational recovery project. No package was added/upgraded; known dependency advisories remain a separate risk. No EF pending-change command was run; no model/migration file changed.

### Final-closure evidence

Final source: `A3-FINAL-CLOSURE.trx` — **162 PASS / 0 FAIL / 0 SKIP** after the final implementation/test edit. A3: 54 (14 base reader/scope, 19 closure/provenance, 7 dissolution, 5 finance, 4 HTTP roles, 4 reports, 1 posting-boundary). A1/A2: **29 PASS** (14/15). Related/OpenAPI: **79 PASS** (2 OpenAPI, 24 SocialBankGroupPhase, 4 V1CaseFund, 2 V1CaseWorkflow, 13 V1FundFinance, 2 V1FundRequest, 20 V1LegacyRoleBoundary, 12 V1Permission). The approved scope requires focused/related gates, not an unverified DB-writing full suite; unfiltered full Backend suite and live providers were NOT RUN.

Release: `dotnet build SpendWise.slnx -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` — exit 0, **0 errors / 4 existing Pkcs NU1902/NU1903 warnings**. No dependencies/configuration were changed. No model/entity/migration change, so no new migration or production application; an EF pending-change command was NOT RUN.

SQL02: **NOT RUN**. Read-only preflight matched the dedicated candidate, both pressure flags were false, SQL-volume free space was 2,931,564,544 bytes, but available RAM was 1,375,384 KB against the existing 1,500,000 KB minimum (deficit 124,616 KB). No gate reduction, SQL service action or catalog creation/deletion. Final read-only catalog check: **0 owned test catalogs**. Exact values are retained in ignored `SQL02-FINAL-CLOSURE-PREFLIGHT.json`. No SQL01/application database connection, provider call, application startup, migration or seeding was performed.

Evidence remains local under `checkpoints/A3-EVIDENCE/`. The source/published contract contains no credentials or real personal data. Full staged blobs are secret-scanned before commit/push. No routine ZIP is created.

- RED: missing closure contracts; SQL-translated report-union projection then exposed mismatched assigned properties and was corrected rather than materializing unbounded results. Fixture uniqueness errors were fixed with distinct synthetic ticket numbers/tokens, not weakened constraints.
- RED/GREEN: USD Legacy refunds exposed a hardcoded currency in dissolution evidence; explicit fund tasks exposed missing derivative dispute visibility without duplicate support cases. Both have targeted behavioral regressions.
- Negative control: replacing business binding with unconditional success made WrongGroup, WrongDirection and WrongUser fail (3 FAIL / 12 PASS); guard restored with identical SHA-256 before final gates.
- Alternative to SQL02: real relational SQLite transactions, constraints and HTTP pipeline, including four roles, session/case negatives, all A3 routes/OpenAPI, original business-to-ledger chains, read-only invariants and A1/A2 regression. This is NOT SQL Server production/concurrency proof. SQL02 is conditional on the unchanged resource gate.

Rollback only this commit's additive A3 readers/controller/registration/contracts/tests/docs after review, preserving subsequent work. No database rollback, checkpoint restore or data rewrite is required. No migration was created/applied and no SQL01 connection was used. No routine source ZIP was created.
