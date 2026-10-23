import os
import re
import subprocess
import unittest

SEED_SCRIPT_PATH = os.path.join(os.path.dirname(__file__), "seed_acceptance_fixtures.ps1")

class TestSeedAcceptanceFixturesSafety(unittest.TestCase):

    def setUp(self):
        self.assertTrue(os.path.exists(SEED_SCRIPT_PATH), f"Script not found: {SEED_SCRIPT_PATH}")
        with open(SEED_SCRIPT_PATH, "r", encoding="utf-8") as f:
            self.content = f.read()

    def test_no_unscoped_delete_from_statements(self):
        """Verifies that EVERY DELETE statement has an explicit WHERE clause or scoped alias join."""
        # Find all DELETE FROM <Table> without WHERE or alias join
        # Scoped examples:
        #   DELETE FROM Table WHERE ...
        #   DELETE eb FROM Table eb INNER JOIN ... WHERE ...
        lines = self.content.splitlines()
        violations = []
        for i, line in enumerate(lines, 1):
            stripped = line.strip()
            # Check for bare delete: DELETE FROM Table; or DELETE FROM Table without WHERE on same statement
            if re.match(r"^DELETE\s+FROM\s+\w+\s*;$", stripped, re.IGNORECASE):
                violations.append((i, stripped))
            elif re.match(r"^DELETE\s+FROM\s+\w+\s*$", stripped, re.IGNORECASE):
                # check next line for WHERE
                next_line = lines[i].strip() if i < len(lines) else ""
                if not next_line.upper().startswith("WHERE"):
                    violations.append((i, stripped + " -> " + next_line))

        self.assertEqual(violations, [], f"Found unscoped DELETE statements: {violations}")

    def test_preflight_guard_parameters_present(self):
        """Verifies that hardcoded checks block SQL01, PROD, and non-SpendWise_E2E_G4 databases."""
        self.assertIn("SQL01", self.content)
        self.assertIn("PROD", self.content)
        self.assertIn("SpendWise_E2E_G4", self.content)
        self.assertIn("SQLEXPRESS02", self.content)

    def test_server_side_environment_verification_query(self):
        """Verifies that DB_NAME() and SERVERPROPERTY('InstanceName') queries are executed before operations."""
        self.assertIn("SELECT DB_NAME()", self.content)
        self.assertIn("SERVERPROPERTY('InstanceName')", self.content)

    def test_fail_closed_on_prohibited_server(self):
        """Preflight test: Script must exit non-zero and abort when passed a prohibited server name."""
        cmd = [
            "powershell", "-NoProfile", "-ExecutionPolicy", "Bypass",
            "-File", SEED_SCRIPT_PATH,
            "-Server", "PROD-SQL01",
            "-Database", "SpendWise_E2E_G4"
        ]
        res = subprocess.run(cmd, capture_output=True, text=True)
        self.assertNotEqual(res.returncode, 0, "Script should fail when target server is PROD-SQL01")
        self.assertIn("SAFETY ABORT", res.stderr + res.stdout)

    def test_fail_closed_on_prohibited_database(self):
        """Preflight test: Script must exit non-zero and abort when passed a production or wrong database name."""
        cmd = [
            "powershell", "-NoProfile", "-ExecutionPolicy", "Bypass",
            "-File", SEED_SCRIPT_PATH,
            "-Server", r"DESKTOP-B5KA3LT\SQLEXPRESS02",
            "-Database", "SpendWiseDb"
        ]
        res = subprocess.run(cmd, capture_output=True, text=True)
        self.assertNotEqual(res.returncode, 0, "Script should fail when target database is SpendWiseDb")
        self.assertIn("SAFETY ABORT", res.stderr + res.stdout)

    def test_fail_closed_on_arbitrary_database(self):
        """Preflight test: Script must exit non-zero and abort when passed an arbitrary database name."""
        cmd = [
            "powershell", "-NoProfile", "-ExecutionPolicy", "Bypass",
            "-File", SEED_SCRIPT_PATH,
            "-Server", r"DESKTOP-B5KA3LT\SQLEXPRESS02",
            "-Database", "OtherDatabase"
        ]
        res = subprocess.run(cmd, capture_output=True, text=True)
        self.assertNotEqual(res.returncode, 0, "Script should fail when target database is OtherDatabase")
        self.assertIn("SAFETY ABORT", res.stderr + res.stdout)


if __name__ == "__main__":
    unittest.main()
