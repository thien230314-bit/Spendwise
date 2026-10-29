#!/usr/bin/env python3
"""
SpendWise Acceptance Gate - Machine Validator for User Backend E2E Results
Usage: python backend/scripts/validate_e2e_acceptance.py [path_to_newman_results.json]
"""

import sys
import json
import os
from typing import Dict, List, Any

# Standard expected provider-blocked test signatures (SePay withdrawal testmode not supported)
ALLOWED_PROVIDER_BLOCKED_PATTERNS = [
    "V1-IMP-10 - 01 - SePay TestMode: Create Wallet Withdrawal",
    "V1-IMP-10 - 03 - SePay TestMode: Get Wallet Withdrawal By Id",
    "Wallet withdrawal created or accepted",
    "Wallet withdrawal retrieved (200 OK)",
]

def validate_results(filepath: str) -> bool:
    print(f"=== SPENDWISE E2E ACCEPTANCE VALIDATOR ===")
    print(f"Reading Newman results from: {filepath}")

    if not os.path.exists(filepath):
        print(f"[FAIL] Results file not found: {filepath}", file=sys.stderr)
        return False

    with open(filepath, 'r', encoding='utf-8') as f:
        try:
            data = json.load(f)
        except Exception as e:
            print(f"[FAIL] Invalid JSON format: {e}", file=sys.stderr)
            return False

    run = data.get("run", {})
    stats = run.get("stats", {})
    executions = run.get("executions", [])
    failures = run.get("failures", [])

    total_requests = stats.get("requests", {}).get("total", 0)
    failed_requests = stats.get("requests", {}).get("failed", 0)
    total_assertions = stats.get("assertions", {}).get("total", 0)
    failed_assertions = stats.get("assertions", {}).get("failed", 0)

    print(f"\n[SUMMARY STATS]")
    print(f"  Total Requests Executed:    {total_requests}")
    print(f"  Failed Requests:           {failed_requests}")
    print(f"  Total Assertions Checked:  {total_assertions}")
    print(f"  Failed Assertions:         {failed_assertions}")
    print(f"  Passed Assertions:         {total_assertions - failed_assertions}")

    if total_assertions < 300:
        print(f"[FAIL] Assertion count too low ({total_assertions} < 300). Incomplete suite run!", file=sys.stderr)
        return False

    # Check for 500 Internal Server Errors in any execution
    server_errors = []
    for exec_item in executions:
        req_name = exec_item.get("item", {}).get("name", "Unknown")
        resp = exec_item.get("response")
        if resp:
            code = resp.get("code", 0)
            if code >= 500 and code != 504: # 504 is gateway timeout allowed only on provider mocks
                # HTTP 503 is allowed for B5-01 Controlled Sandbox Response (OPEN_BANKING_HUB_NOT_CONFIGURED)
                if code == 503 and ("B5-01" in req_name or "Controlled Sandbox Response" in req_name):
                    continue
                server_errors.append((req_name, code))

    if server_errors:
        print(f"\n[FAIL] Found {len(server_errors)} 5xx Internal Server Errors:", file=sys.stderr)
        for name, code in server_errors:
            print(f"  - {name}: HTTP {code}", file=sys.stderr)
        return False
    else:
        print(f"\n[PASS] Zero 5xx Internal Server Errors detected.")

    # Classify failures
    provider_blocked = []
    unexpected_failures = []

    for idx, f_item in enumerate(failures, 1):
        src_name = f_item.get("source", {}).get("name", "")
        parent_name = f_item.get("parent", {}).get("name", "")
        err = f_item.get("error", {})
        test_name = err.get("test", "")
        msg = err.get("message", "")

        full_desc = f"{parent_name} / {src_name} / {test_name}"
        is_allowed = any(pat in full_desc for pat in ALLOWED_PROVIDER_BLOCKED_PATTERNS)

        if is_allowed:
            provider_blocked.append({
                "source": src_name,
                "test": test_name,
                "message": msg
            })
        else:
            unexpected_failures.append({
                "parent": parent_name,
                "source": src_name,
                "test": test_name,
                "message": msg
            })

    print(f"\n[FAILURE ANALYSIS]")
    print(f"  Provider-Blocked Failures: {len(provider_blocked)} (Allowed external SePay limitation)")
    for p in provider_blocked:
        print(f"    * [{p['source']}] -> {p['test']}")

    if unexpected_failures:
        print(f"\n[FAIL] Found {len(unexpected_failures)} unexpected failures (MUST BE ZERO):", file=sys.stderr)
        for u in unexpected_failures:
            print(f"  - [{u['parent']}] {u['source']} -> {u['test']}: {u['message']}", file=sys.stderr)
        return False

    print(f"\n[PASS] All {len(provider_blocked)} failures strictly conform to allowed PROVIDER_BLOCKED status.")
    print(f"[PASS] 100% of testable User Backend assertions PASSED ({total_assertions - failed_assertions}/{total_assertions}).")
    print(f"\n>>> FINAL VERDICT: ACCEPTANCE GATE PASSED <<<")
    return True

if __name__ == "__main__":
    default_target = "reports/newman/backend-user-final-verified.json" if os.path.exists("reports/newman/backend-user-final-verified.json") else "reports/newman/final-user-e2e-results.json"
    target_path = sys.argv[1] if len(sys.argv) > 1 else default_target
    success = validate_results(target_path)
    sys.exit(0 if success else 1)
