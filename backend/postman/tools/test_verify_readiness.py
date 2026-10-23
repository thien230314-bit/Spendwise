#!/usr/bin/env python3
"""
Unit and Regression Test Suite for verify_readiness.py (v2.0 Strict Engine)
Verifies:
1. Rejection of HTTP 200 with generic status-only assertion (missing contract/business state).
2. Requirement of ledger/balance evidence for financial operations.
3. Detection of operations missing from Postman Collection.
4. Detection of Git HEAD mismatch.
5. Rejection of fake 404 tests against dummy GUIDs (00000000-0000-0000-0000-000000000001).
6. Contract baseline delta detection (added, removed, modified routes).
7. Acceptance of genuine contract & business state evidence.
"""

import unittest
import os
import sys

# Ensure repository root is on sys.path
repo_root = os.path.abspath(os.path.join(os.path.dirname(__file__), "../../.."))
if repo_root not in sys.path:
    sys.path.insert(0, repo_root)

from backend.postman.tools.verify_readiness import (
    normalize_path,
    classify_operations,
    match_operation,
    parse_postman_collection,
    match_collection_request_to_operation,
    parse_newman_execution,
    evaluate_evidence_quality,
    evaluate_user_operations,
    check_gate_verdict,
    detect_contract_deltas,
    APPROVED_EXTERNAL_DEFERRED_OPS,
    DUMMY_GUID
)

class TestVerifyReadiness(unittest.TestCase):

    def test_normalize_path(self):
        self.assertEqual(normalize_path("/api/ping/"), "/api/ping")
        self.assertEqual(normalize_path("/api/users"), "/api/users")
        self.assertEqual(normalize_path("/"), "/")

    def test_match_operation_simple_and_param(self):
        op = {"method": "GET", "path": "/api/users/me"}
        req_match = {"method": "GET", "path": "/api/users/me"}
        req_diff_method = {"method": "POST", "path": "/api/users/me"}
        req_diff_path = {"method": "GET", "path": "/api/users/other"}

        self.assertEqual(len(match_operation(op, [req_match])), 1)
        self.assertEqual(len(match_operation(op, [req_diff_method])), 0)
        self.assertEqual(len(match_operation(op, [req_diff_path])), 0)

        op_param = {"method": "GET", "path": "/api/v1/funds/{id}/elections"}
        req_param_match = {"method": "GET", "path": "/api/v1/funds/e2e00000-0000-0000-0000-00000000000c/elections"}
        self.assertEqual(len(match_operation(op_param, [req_param_match])), 1)

    def test_generic_status_assertion_rejected_from_success_verified(self):
        """HTTP 200 with only 'Status code is 200' must be rejected as weak evidence."""
        op = {"method": "GET", "path": "/api/users/profile", "tag": "Profile"}
        matches = [{
            "name": "Get Profile",
            "method": "GET",
            "path": "/api/users/profile",
            "statusCode": 200,
            "assertionsPassed": True,
            "assertionCount": 1,
            "assertionNames": ["Status code is 200 OK"]
        }]
        
        ok, reason = evaluate_evidence_quality(op, matches)
        self.assertFalse(ok)
        self.assertIn("Weak evidence", reason)

        tier_counts, rows = evaluate_user_operations([op], matches)
        self.assertEqual(tier_counts["FAIL"], 1)
        self.assertEqual(tier_counts["SUCCESS_VERIFIED"], 0)

    def test_contract_and_business_state_assertion_accepted(self):
        """HTTP 200 with schema/property/status assertions must pass evidence evaluation."""
        op = {"method": "GET", "path": "/api/users/profile", "tag": "Profile"}
        matches = [{
            "name": "Get Profile",
            "method": "GET",
            "path": "/api/users/profile",
            "statusCode": 200,
            "assertionsPassed": True,
            "assertionCount": 2,
            "assertionNames": ["Status code is 200 OK", "User profile contains valid id, email, and active status"]
        }]
        
        ok, reason = evaluate_evidence_quality(op, matches)
        self.assertTrue(ok)

        tier_counts, rows = evaluate_user_operations([op], matches)
        self.assertEqual(tier_counts["SUCCESS_VERIFIED"], 1)
        self.assertEqual(tier_counts["FAIL"], 0)

    def test_financial_mutation_requires_ledger_or_balance_evidence(self):
        """Financial endpoint without balance/ledger/transaction assertions fails."""
        op = {"method": "POST", "path": "/api/transfers/internal", "tag": "Transfers"}
        matches_weak = [{
            "name": "Internal Transfer",
            "method": "POST",
            "path": "/api/transfers/internal",
            "statusCode": 201,
            "assertionsPassed": True,
            "assertionCount": 1,
            "assertionNames": ["Status code is 201 Created"]
        }]
        ok, reason = evaluate_evidence_quality(op, matches_weak)
        self.assertFalse(ok)

        matches_strong = [{
            "name": "Internal Transfer",
            "method": "POST",
            "path": "/api/transfers/internal",
            "statusCode": 201,
            "assertionsPassed": True,
            "assertionCount": 2,
            "assertionNames": ["Status code is 201 Created", "Transfer updates wallet ledger balance and returns transactionId"]
        }]
        ok2, reason2 = evaluate_evidence_quality(op, matches_strong)
        self.assertTrue(ok2)

    def test_unmapped_collection_operation_fails(self):
        """OpenAPI operation missing from Postman Collection is classified NOT_VERIFIED."""
        op = {"method": "GET", "path": "/api/wallets/unmapped-endpoint", "tag": "Wallets"}
        collection_requests = [
            {"method": "GET", "raw_url": "{{baseUrl}}/api/wallets", "defined_assertions": ["Valid"]}
        ]
        tier_counts, rows = evaluate_user_operations([op], [], collection_requests=collection_requests)
        self.assertEqual(tier_counts["NOT_VERIFIED"], 1)
        self.assertIn("Missing request definition", rows[0]["evidence"])

    def test_dummy_guid_fake_404_rejected_from_negative_verified(self):
        """A 404 test on dummy GUID (00000000-0000-0000-0000-000000000001) is not accepted as negative proof."""
        op = {"method": "POST", "path": "/api/bank-connections/{id}/unlink", "tag": "Banking"}
        matches = [{
            "name": "Unlink Fake 404",
            "method": "POST",
            "path": f"/api/bank-connections/{DUMMY_GUID}/unlink",
            "statusCode": 404,
            "assertionsPassed": True,
            "assertionCount": 1,
            "assertionNames": ["Status code is 404"]
        }]
        tier_counts, rows = evaluate_user_operations([op], matches)
        # Because the dummy 404 is ignored, it has neither valid happy-path nor valid negative proof
        self.assertEqual(tier_counts["NOT_VERIFIED"], 1)

    def test_unlink_bank_connection_success_verified(self):
        """Unlink bank connection in TestMode returning 200 with sessionId is SUCCESS_VERIFIED."""
        op = {"method": "POST", "path": "/api/bank-connections/{id}/unlink", "tag": "Banking"}
        matches = [{
            "name": "B5-03 - Unlink Bank Connection Success",
            "method": "POST",
            "path": "/api/bank-connections/e2e00000-0000-0000-0000-000000000025/unlink",
            "statusCode": 200,
            "assertionsPassed": True,
            "assertionCount": 2,
            "assertionNames": ["Unlink returns 200 OK", "Unlink response contains valid sessionId and purpose"]
        }]
        tier_counts, rows = evaluate_user_operations([op], matches)
        self.assertEqual(tier_counts["SUCCESS_VERIFIED"], 1)
        self.assertEqual(tier_counts["FAIL"], 0)

    def test_contract_delta_detection(self):
        baseline_data = {
            "counts": {"USER": 1, "ADMIN": 0, "SYSTEM": 0},
            "operations": [
                {"method": "GET", "path": "/api/users/me"}
            ]
        }
        # Modified current ops: added a rogue route, removed baseline route
        current_ops = [
            {"method": "POST", "path": "/api/rogue/route", "scope": "USER"}
        ]
        ok, issues, _ = detect_contract_deltas(current_ops, baseline_data)
        self.assertFalse(ok)
        self.assertTrue(any("added operations" in i for i in issues))
        self.assertTrue(any("missing operations" in i for i in issues))

    def test_git_head_mismatch_fails_gate(self):
        all_ops = [{"scope": "USER"} for _ in range(216)] + [{"scope": "ADMIN"} for _ in range(122)] + [{"scope": "SYSTEM"}]
        user_ops = [op for op in all_ops if op["scope"] == "USER"]
        admin_ops = [op for op in all_ops if op["scope"] == "ADMIN"]
        sys_ops = [op for op in all_ops if op["scope"] == "SYSTEM"]

        stats = {"requests": {"total": 300, "failed": 0}, "assertions": {"total": 450, "failed": 0}}
        tier_counts = {"SUCCESS_VERIFIED": 208, "EXTERNAL_LIVE_DEFERRED": 8, "NEGATIVE_ONLY": 0, "NOT_VERIFIED": 0, "FAIL": 0}
        rows = []

        passed, reasons = check_gate_verdict(all_ops, user_ops, admin_ops, sys_ops, stats, tier_counts, rows, git_head_match=False)
        self.assertFalse(passed)
        self.assertTrue(any("Git HEAD mismatch" in r for r in reasons))

    def test_check_gate_verdict_rejects_negative_only(self):
        all_ops = [{"scope": "USER"} for _ in range(216)] + [{"scope": "ADMIN"} for _ in range(122)] + [{"scope": "SYSTEM"}]
        user_ops = [op for op in all_ops if op["scope"] == "USER"]
        admin_ops = [op for op in all_ops if op["scope"] == "ADMIN"]
        sys_ops = [op for op in all_ops if op["scope"] == "SYSTEM"]

        stats = {"requests": {"total": 200, "failed": 0}, "assertions": {"total": 300, "failed": 0}}
        tier_counts = {"SUCCESS_VERIFIED": 215, "NEGATIVE_ONLY": 1, "EXTERNAL_LIVE_DEFERRED": 0, "NOT_VERIFIED": 0, "FAIL": 0}
        rows = [
            {"method": "POST", "path": "/api/recurring-transactions/{id}/occurrences/{id}/record", "status": "NEGATIVE_ONLY"}
        ]

        passed, reasons = check_gate_verdict(all_ops, user_ops, admin_ops, sys_ops, stats, tier_counts, rows)
        self.assertFalse(passed)
        self.assertTrue(any("NEGATIVE_ONLY" in r for r in reasons))

    def test_check_gate_verdict_passes_when_all_clean(self):
        all_ops = [{"scope": "USER"} for _ in range(216)] + [{"scope": "ADMIN"} for _ in range(122)] + [{"scope": "SYSTEM"}]
        user_ops = [op for op in all_ops if op["scope"] == "USER"]
        admin_ops = [op for op in all_ops if op["scope"] == "ADMIN"]
        sys_ops = [op for op in all_ops if op["scope"] == "SYSTEM"]

        stats = {"requests": {"total": 300, "failed": 0}, "assertions": {"total": 450, "failed": 0}}
        tier_counts = {"SUCCESS_VERIFIED": 208, "EXTERNAL_LIVE_DEFERRED": 8, "NEGATIVE_ONLY": 0, "NOT_VERIFIED": 0, "FAIL": 0}
        rows = [
            {"method": op[0], "path": op[1], "status": "EXTERNAL_LIVE_DEFERRED"}
            for op in APPROVED_EXTERNAL_DEFERRED_OPS
        ]

        passed, reasons = check_gate_verdict(all_ops, user_ops, admin_ops, sys_ops, stats, tier_counts, rows)
        self.assertTrue(passed, f"Gate failed unexpectedly with reasons: {reasons}")
        self.assertEqual(len(reasons), 0)

if __name__ == "__main__":
    unittest.main()
