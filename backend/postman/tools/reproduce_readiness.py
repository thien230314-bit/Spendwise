#!/usr/bin/env python3
"""
SpendWise Backend Acceptance & Readiness Gate Reproducer Tool
Ingests sanitized execution evidence, Postman collection, and versioned contract baseline.
Re-executes the readiness audit logic and outputs readiness_gate_results.json and POSTMAN-ENDPOINT-COVERAGE.md.
"""

import sys
import os
import subprocess

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "../../.."))
VERIFIER_SCRIPT = os.path.join(REPO_ROOT, "backend/postman/tools/verify_readiness.py")
COLLECTION_PATH = os.path.join(REPO_ROOT, "backend/postman/SpendWise-User-API.postman_collection.json")
BASELINE_PATH = os.path.join(REPO_ROOT, "backend/postman/contracts/openapi_contract_baseline.json")
EVIDENCE_PATH = os.path.join(REPO_ROOT, "backend/postman/evidence/newman_execution_evidence_sanitized.json")
OPENAPI_PATH = os.path.join(REPO_ROOT, "scratch/openapi_live.json")

def reproduce():
    print("================================================================================")
    print(" REPRODUCING SPENDWISE BACKEND ACCEPTANCE READINESS GATE EVALUATION")
    print("================================================================================")
    cmd = [
        sys.executable, VERIFIER_SCRIPT,
        "--run-report", EVIDENCE_PATH,
        "--collection", COLLECTION_PATH,
        "--baseline", BASELINE_PATH,
        "--openapi", OPENAPI_PATH,
        "--results-json", os.path.join(REPO_ROOT, "readiness_gate_results.json"),
        "--markdown-out", os.path.join(REPO_ROOT, "POSTMAN-ENDPOINT-COVERAGE.md")
    ]
    res = subprocess.run(cmd, cwd=REPO_ROOT)
    sys.exit(res.returncode)

if __name__ == "__main__":
    reproduce()
