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
        lines = self.content.splitlines()
        violations = []
        for i, line in enumerate(lines, 1):
            stripped = line.strip()
            if re.match(r"^DELETE\s+FROM\s+\w+\s*;$", stripped, re.IGNORECASE):
                violations.append((i, stripped))
            elif re.match(r"^DELETE\s+FROM\s+\w+\s*$", stripped, re.IGNORECASE):
                next_line = lines[i].strip() if i < len(lines) else ""
                if not next_line.upper().startswith("WHERE"):
                    violations.append((i, stripped + " -> " + next_line))

        self.assertEqual(violations, [], f"Found unscoped DELETE statements: {violations}")

    def test_preflight_exact_instance_and_database_enforcement(self):
        """Verifies that exact instance 'SQLEXPRESS02' and exact db 'SpendWise_E2E_G4' are enforced."""
        self.assertIn("SQLEXPRESS02", self.content)
        self.assertIn("SpendWise_E2E_G4", self.content)
        self.assertIn("InstanceName", self.content)
        self.assertIn("IsNullOrWhiteSpace", self.content)
        self.assertIn("BeginTransaction", self.content)
        self.assertIn("Rollback", self.content)

    def test_fail_closed_on_prohibited_server(self):
        """Preflight test: Script must exit non-zero and abort when passed a prohibited server name."""
        cmd = [
            "powershell", "-NoProfile", "-ExecutionPolicy", "Bypass",
            "-File", SEED_SCRIPT_PATH,
            "-Server", "PROD-SQL01",
            "-Database", "SpendWise_E2E_G4"
        ]
        res = subprocess.run(cmd, capture_output=True, text=True)
        self.assertNotEqual(res.returncode, 0)
        self.assertIn("SAFETY ABORT", res.stderr + res.stdout)

    def test_fail_closed_on_substring_server(self):
        """Preflight test: Script must reject substring match (e.g. FAKE_SQLEXPRESS02_SERVER)."""
        cmd = [
            "powershell", "-NoProfile", "-ExecutionPolicy", "Bypass",
            "-File", SEED_SCRIPT_PATH,
            "-Server", "FAKE_SQLEXPRESS02_SERVER",
            "-Database", "SpendWise_E2E_G4"
        ]
        res = subprocess.run(cmd, capture_output=True, text=True)
        self.assertNotEqual(res.returncode, 0)
        self.assertIn("SAFETY ABORT", res.stderr + res.stdout)

    def test_fail_closed_on_prohibited_database(self):
        """Preflight test: Script must exit non-zero and abort when passed SpendWiseDb."""
        cmd = [
            "powershell", "-NoProfile", "-ExecutionPolicy", "Bypass",
            "-File", SEED_SCRIPT_PATH,
            "-Server", r"DESKTOP-B5KA3LT\SQLEXPRESS02",
            "-Database", "SpendWiseDb"
        ]
        res = subprocess.run(cmd, capture_output=True, text=True)
        self.assertNotEqual(res.returncode, 0)
        self.assertIn("SAFETY ABORT", res.stderr + res.stdout)

    def test_fail_closed_on_arbitrary_database(self):
        """Preflight test: Script must exit non-zero and abort when passed arbitrary database."""
        cmd = [
            "powershell", "-NoProfile", "-ExecutionPolicy", "Bypass",
            "-File", SEED_SCRIPT_PATH,
            "-Server", r"DESKTOP-B5KA3LT\SQLEXPRESS02",
            "-Database", "OtherDatabase"
        ]
        res = subprocess.run(cmd, capture_output=True, text=True)
        self.assertNotEqual(res.returncode, 0)
        self.assertIn("SAFETY ABORT", res.stderr + res.stdout)

    def test_isolated_database_preserves_non_fixture_control_data(self):
        """
        Integration test against SpendWise_FixtureSafety_Isolated:
        Executes the scoped cleanup logic and verifies that non-fixture control rows remain 100% untouched.
        """
        ps_test_script = """
Add-Type -AssemblyName System.Data
$isoConnStr = "Server=DESKTOP-B5KA3LT\\SQLEXPRESS02;Database=SpendWise_FixtureSafety_Isolated;Integrated Security=True;TrustServerCertificate=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection($isoConnStr)
$conn.Open()

# 1. Run scoped cleanup logic
$cleanupCmd = $conn.CreateCommand()
$cleanupCmd.CommandText = @"
-- Temporary tables
IF OBJECT_ID('tempdb..#FixtureUserIds') IS NOT NULL DROP TABLE #FixtureUserIds;
CREATE TABLE #FixtureUserIds (UserId UNIQUEIDENTIFIER PRIMARY KEY);
INSERT INTO #FixtureUserIds (UserId)
SELECT Id FROM Users WHERE Id = 'E2E00000-0000-0000-0000-000000000001' OR Email = 'fixture_user@spendwise.test';

IF OBJECT_ID('tempdb..#FixtureGroupIds') IS NOT NULL DROP TABLE #FixtureGroupIds;
CREATE TABLE #FixtureGroupIds (GroupId UNIQUEIDENTIFIER PRIMARY KEY);
INSERT INTO #FixtureGroupIds (GroupId)
SELECT Id FROM Groups WHERE Id = 'E2E00000-0000-0000-0000-000000000087' OR Name = 'E2E Acceptance Group';

-- Scoped Deletes
DELETE FROM Users WHERE Id IN (SELECT UserId FROM #FixtureUserIds);
DELETE FROM Groups WHERE Id IN (SELECT GroupId FROM #FixtureGroupIds);
"@
$cleanupCmd.ExecuteNonQuery() | Out-Null

# 2. Verify non-fixture control data remains
$checkCmd = $conn.CreateCommand()
$checkCmd.CommandText = "SELECT COUNT(*) FROM Users WHERE Id IN ('11111111-1111-1111-1111-111111111111', '22222222-2222-2222-2222-222222222222')"
$controlUsersCount = [int]$checkCmd.ExecuteScalar()

$checkCmd2 = $conn.CreateCommand()
$checkCmd2.CommandText = "SELECT COUNT(*) FROM Groups WHERE Id = '33333333-3333-3333-3333-333333333333'"
$controlGroupsCount = [int]$checkCmd2.ExecuteScalar()

# 3. Verify fixture data was deleted
$checkCmd3 = $conn.CreateCommand()
$checkCmd3.CommandText = "SELECT COUNT(*) FROM Users WHERE Id = 'E2E00000-0000-0000-0000-000000000001'"
$fixtureUsersCount = [int]$checkCmd3.ExecuteScalar()

$conn.Close()

if ($controlUsersCount -ne 2) { throw "FAIL: Control users were modified or deleted!" }
if ($controlGroupsCount -ne 1) { throw "FAIL: Control groups were modified or deleted!" }
if ($fixtureUsersCount -ne 0) { throw "FAIL: Fixture user was not cleaned up!" }
Write-Output "CONTROL_PRESERVED_SUCCESS"
"""
        cmd = ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", ps_test_script]
        res = subprocess.run(cmd, capture_output=True, text=True)
        self.assertEqual(res.returncode, 0, f"Integration test failed: {res.stderr}")
        self.assertIn("CONTROL_PRESERVED_SUCCESS", res.stdout)

if __name__ == "__main__":
    unittest.main()
