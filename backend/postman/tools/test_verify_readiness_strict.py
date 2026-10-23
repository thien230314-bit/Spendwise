import unittest
import os
import sys
import tempfile
import subprocess
import json

repo_root = os.path.abspath(os.path.join(os.path.dirname(__file__), "../../.."))
if repo_root not in sys.path:
    sys.path.insert(0, repo_root)

VERIFIER_SCRIPT = os.path.join(repo_root, "backend/postman/tools/verify_readiness.py")
COLLECTION_PATH = os.path.join(repo_root, "backend/postman/SpendWise-User-API.postman_collection.json")
BASELINE_PATH = os.path.join(repo_root, "backend/postman/contracts/openapi_contract_baseline.json")
EVIDENCE_PATH = os.path.join(repo_root, "backend/postman/evidence/newman_execution_evidence_sanitized.json")
OPENAPI_PATH = os.path.join(repo_root, "scratch/openapi_live.json")

class TestVerifyReadinessStrictGates(unittest.TestCase):

    def test_missing_collection_fails_gate(self):
        """Verifier must exit with code 1 if --collection points to a non-existent file."""
        cmd = [
            sys.executable, VERIFIER_SCRIPT,
            "--run-report", EVIDENCE_PATH,
            "--openapi", OPENAPI_PATH,
            "--collection", "non_existent_collection.json",
            "--baseline", BASELINE_PATH
        ]
        res = subprocess.run(cmd, capture_output=True, text=True, cwd=repo_root)
        self.assertEqual(res.returncode, 1)
        self.assertIn("Missing required Postman collection", res.stderr + res.stdout)

    def test_missing_baseline_fails_gate(self):
        """Verifier must exit with code 1 if --baseline points to a non-existent file."""
        cmd = [
            sys.executable, VERIFIER_SCRIPT,
            "--run-report", EVIDENCE_PATH,
            "--openapi", OPENAPI_PATH,
            "--collection", COLLECTION_PATH,
            "--baseline", "non_existent_baseline.json"
        ]
        res = subprocess.run(cmd, capture_output=True, text=True, cwd=repo_root)
        self.assertEqual(res.returncode, 1)
        self.assertIn("Missing required OpenAPI contract baseline", res.stderr + res.stdout)

    def test_git_head_mismatch_fails_gate(self):
        """Verifier must exit with code 1 if --git-head does not match current repository HEAD."""
        cmd = [
            sys.executable, VERIFIER_SCRIPT,
            "--run-report", EVIDENCE_PATH,
            "--openapi", OPENAPI_PATH,
            "--collection", COLLECTION_PATH,
            "--baseline", BASELINE_PATH,
            "--git-head", "0000000000000000000000000000000000000000"
        ]
        res = subprocess.run(cmd, capture_output=True, text=True, cwd=repo_root)
        self.assertEqual(res.returncode, 1)
        self.assertIn("Git HEAD mismatch", res.stderr + res.stdout)

    def test_collection_checksum_mismatch_fails_gate(self):
        """Verifier must detect mismatch between root and backend/postman collection copies."""
        with tempfile.TemporaryDirectory() as tmpdir:
            temp_root_col = os.path.join(tmpdir, "SpendWise-User-API.postman_collection.json")
            with open(COLLECTION_PATH, "r", encoding="utf-8") as f:
                c_data = json.load(f)
            c_data["info"]["description"] = "Altered copy"
            with open(temp_root_col, "w", encoding="utf-8") as f:
                json.dump(c_data, f)
            
            # Link or copy backend/postman directory structure
            temp_backend_dir = os.path.join(tmpdir, "backend", "postman")
            os.makedirs(temp_backend_dir, exist_ok=True)
            temp_backend_col = os.path.join(temp_backend_dir, "SpendWise-User-API.postman_collection.json")
            with open(COLLECTION_PATH, "r", encoding="utf-8") as f:
                with open(temp_backend_col, "w", encoding="utf-8") as out_f:
                    out_f.write(f.read())

            cmd = [
                sys.executable, VERIFIER_SCRIPT,
                "--run-report", EVIDENCE_PATH,
                "--openapi", OPENAPI_PATH,
                "--collection", temp_backend_col,
                "--baseline", BASELINE_PATH
            ]
            res = subprocess.run(cmd, capture_output=True, text=True, cwd=tmpdir)
            self.assertEqual(res.returncode, 1)
            self.assertIn("Collection checksum parity error", res.stderr + res.stdout)

    def test_runtime_openapi_failure_aborts_when_required(self):
        """When --require-runtime is set and server is down, verifier must fail-closed."""
        cmd = [
            sys.executable, VERIFIER_SCRIPT,
            "--run-report", EVIDENCE_PATH,
            "--collection", COLLECTION_PATH,
            "--baseline", BASELINE_PATH,
            "--openapi", "http://127.0.0.1:59999/swagger/v1/swagger.json",
            "--require-runtime"
        ]
        res = subprocess.run(cmd, capture_output=True, text=True, cwd=repo_root)
        self.assertEqual(res.returncode, 1)
        self.assertIn("Required runtime OpenAPI endpoint unreachable", res.stderr + res.stdout)

    def test_happy_path_passes_gate(self):
        """Full suite against verified evidence passes gate with exit code 0."""
        cmd = [
            sys.executable, VERIFIER_SCRIPT,
            "--run-report", EVIDENCE_PATH,
            "--openapi", OPENAPI_PATH,
            "--collection", COLLECTION_PATH,
            "--baseline", BASELINE_PATH
        ]
        res = subprocess.run(cmd, capture_output=True, text=True, cwd=repo_root)
        self.assertEqual(res.returncode, 0)
        self.assertIn("100% BACKEND USER READINESS CRITERIA MET WITH DEEP EVIDENCE", res.stdout)

if __name__ == "__main__":
    unittest.main()
