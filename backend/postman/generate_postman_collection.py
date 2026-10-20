import json
import uuid

def make_req(name, method, url_path, body=None, token_var=None, pre_script=None, test_script=None, headers=None):
    hdrs = []
    if headers:
        for k, v in headers.items():
            hdrs.append({"key": k, "value": v, "type": "text"})
    
    if token_var:
        hdrs.append({"key": "Authorization", "value": f"Bearer {{{{{token_var}}}}}", "type": "text"})
    
    req_body = None
    if body is not None:
        hdrs.append({"key": "Content-Type", "value": "application/json", "type": "text"})
        req_body = {
            "mode": "raw",
            "raw": json.dumps(body, indent=2) if isinstance(body, dict) else body,
            "options": {
                "raw": {
                    "language": "json"
                }
            }
        }
    
    events = []
    if pre_script:
        events.append({
            "listen": "prerequest",
            "script": {
                "type": "text/javascript",
                "exec": pre_script.strip().split("\n")
            }
        })
    if test_script:
        events.append({
            "listen": "test",
            "script": {
                "type": "text/javascript",
                "exec": test_script.strip().split("\n")
            }
        })
    
    path_segments = [seg for seg in url_path.split("/") if seg]
    
    return {
        "name": name,
        "event": events,
        "request": {
            "method": method,
            "header": hdrs,
            "body": req_body,
            "url": {
                "raw": "{{baseUrl}}/" + url_path.lstrip("/"),
                "host": ["{{baseUrl}}"],
                "path": path_segments
            }
        },
        "response": []
    }

def make_file_upload_req(name, url_path, file_key, file_src, token_var=None, test_script=None):
    hdrs = []
    if token_var:
        hdrs.append({"key": "Authorization", "value": f"Bearer {{{{{token_var}}}}}", "type": "text"})
    
    events = []
    if test_script:
        events.append({
            "listen": "test",
            "script": {
                "type": "text/javascript",
                "exec": test_script.strip().split("\n")
            }
        })
    
    path_segments = [seg for seg in url_path.split("/") if seg]
    
    return {
        "name": name,
        "event": events,
        "request": {
            "method": "POST",
            "header": hdrs,
            "body": {
                "mode": "formdata",
                "formdata": [
                    {
                        "key": file_key,
                        "type": "file",
                        "src": file_src
                    }
                ]
            },
            "url": {
                "raw": "{{baseUrl}}/" + url_path.lstrip("/"),
                "host": ["{{baseUrl}}"],
                "path": path_segments
            }
        },
        "response": []
    }

collection = {
    "info": {
        "_postman_id": str(uuid.uuid4()),
        "name": "SpendWise-V1-Full-E2E",
        "description": "Comprehensive E2E Postman Collection for SpendWise V1.0 API validation.",
        "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
    },
    "item": []
}

# -------------------------------------------------------------
# 01-Health
# -------------------------------------------------------------
health_items = [
    make_req(
        "Health Live",
        "GET",
        "health/live",
        test_script="""
pm.test("Status code is 200", function () {
    pm.response.to.have.status(200);
});
pm.test("Service is Healthy", function () {
    pm.expect(pm.response.text()).to.eql("Healthy");
});
"""
    ),
    make_req(
        "Health Ready",
        "GET",
        "health/ready",
        test_script="""
pm.test("Status code is 200", function () {
    pm.response.to.have.status(200);
});
pm.test("Database readiness is Healthy", function () {
    pm.expect(pm.response.text()).to.eql("Healthy");
});
"""
    ),
    make_req(
        "Health Ping",
        "GET",
        "health",
        test_script="""
pm.test("Status code is 200", function () {
    pm.response.to.have.status(200);
});
pm.test("Overall health is Healthy", function () {
    pm.expect(pm.response.text()).to.eql("Healthy");
});
"""
    )
]

collection["item"].append({
    "name": "01-Health",
    "item": health_items
})

# -------------------------------------------------------------
# 02-Auth
# -------------------------------------------------------------
auth_items = [
    make_req(
        "01 - Register User A",
        "POST",
        "api/auth/register",
        body={
            "fullName": "User Alpha",
            "email": "{{userA_email}}",
            "phone": "{{userA_phone}}",
            "password": "{{user_password}}",
            "confirmPassword": "{{user_password}}"
        },
        pre_script="""
if (!pm.environment.get("runId")) {
    pm.environment.set("runId", Date.now().toString());
}
var runId = pm.environment.get("runId");
pm.environment.set("userA_email", "e2e_usera_" + runId + "@spendwise.test");
pm.environment.set("userA_phone", "09" + Math.floor(10000000 + Math.random() * 90000000));
pm.environment.set("userB_email", "e2e_userb_" + runId + "@spendwise.test");
pm.environment.set("userB_phone", "09" + Math.floor(10000000 + Math.random() * 90000000));
if (!pm.environment.get("user_new_password")) {
    pm.environment.set("user_new_password", "NewPassword456!@#");
}
if (!pm.environment.get("user_new_pin")) {
    pm.environment.set("user_new_pin", "654321");
}
""",
        test_script="""
pm.test("Registration successful (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Returns email verification pending", function () {
    var jsonData = pm.response.json();
    pm.expect(jsonData.status).to.be.a("string");
});
"""
    ),
    make_req(
        "02 - Verify Registration Email User A",
        "POST",
        "api/auth/email-verification/verify",
        body={
            "email": "{{userA_email}}",
            "otp": "{{static_otp}}"
        },
        test_script="""
pm.test("Email verified successfully (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Challenge requires SetupSecurityPin", function () {
    var jsonData = pm.response.json();
    pm.expect(jsonData.challengeToken).to.be.a("string");
    pm.expect(jsonData.status).to.eql("PIN_SETUP_REQUIRED");
    pm.environment.set("userA_challengeToken", jsonData.challengeToken);
});
"""
    ),
    make_req(
        "03 - Setup PIN User A",
        "POST",
        "api/auth/pin/setup",
        body={
            "challengeToken": "{{userA_challengeToken}}",
            "pin": "{{default_pin}}",
            "confirmPin": "{{default_pin}}"
        },
        test_script="""
pm.test("PIN setup successful (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Returns access & refresh tokens", function () {
    var jsonData = pm.response.json();
    pm.expect(jsonData.accessToken).to.be.a("string");
    pm.expect(jsonData.refreshToken).to.be.a("string");
    pm.expect(jsonData.user).to.be.an("object");
    pm.environment.set("userA_accessToken", jsonData.accessToken);
    pm.environment.set("userA_refreshToken", jsonData.refreshToken);
    pm.environment.set("userA_id", jsonData.user.id);
});
"""
    ),
    make_req(
        "04 - Login User A (Challenge)",
        "POST",
        "api/auth/login",
        body={
            "identifier": "{{userA_email}}",
            "password": "{{user_password}}"
        },
        test_script="""
pm.test("Login initiated successfully (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Challenge requires SecurityPin verification", function () {
    var jsonData = pm.response.json();
    pm.expect(jsonData.challengeToken).to.be.a("string");
    pm.expect(jsonData.status).to.eql("PIN_REQUIRED");
    pm.environment.set("userA_challengeToken", jsonData.challengeToken);
});
"""
    ),
    make_req(
        "05 - Verify PIN for Login User A",
        "POST",
        "api/auth/pin/verify",
        body={
            "challengeToken": "{{userA_challengeToken}}",
            "pin": "{{default_pin}}"
        },
        test_script="""
pm.test("PIN login verified successfully (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Updates access & refresh tokens", function () {
    var jsonData = pm.response.json();
    pm.expect(jsonData.accessToken).to.be.a("string");
    pm.expect(jsonData.refreshToken).to.be.a("string");
    pm.environment.set("userA_accessToken", jsonData.accessToken);
    pm.environment.set("userA_refreshToken", jsonData.refreshToken);
});
"""
    ),
    make_req(
        "06 - Refresh Token User A",
        "POST",
        "api/auth/refresh",
        body={
            "refreshToken": "{{userA_refreshToken}}"
        },
        test_script="""
pm.test("Token rotated successfully (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Returns new access & refresh tokens", function () {
    var jsonData = pm.response.json();
    pm.expect(jsonData.accessToken).to.be.a("string");
    pm.expect(jsonData.refreshToken).to.be.a("string");
    pm.environment.set("userA_accessToken", jsonData.accessToken);
    pm.environment.set("userA_refreshToken", jsonData.refreshToken);
});
"""
    ),
    make_req(
        "07 - Change Password User A",
        "POST",
        "api/auth/change-password",
        token_var="userA_accessToken",
        body={
            "currentPassword": "{{user_password}}",
            "newPassword": "{{user_new_password}}"
        },
        test_script="""
pm.test("Password changed successfully (204 No Content)", function () {
    pm.response.to.have.status(204);
});
"""
    ),
    make_req(
        "07a - Login User A With New Password",
        "POST",
        "api/auth/login",
        body={
            "identifier": "{{userA_email}}",
            "password": "{{user_new_password}}"
        },
        test_script="""
pm.test("Login with new password succeeds (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Captures challenge token", function () {
    var jsonData = pm.response.json();
    pm.expect(jsonData.challengeToken).to.be.a("string");
    pm.environment.set("userA_challengeToken", jsonData.challengeToken);
});
"""
    ),
    make_req(
        "07b - Verify PIN User A After Password Change",
        "POST",
        "api/auth/pin/verify",
        body={
            "challengeToken": "{{userA_challengeToken}}",
            "pin": "{{default_pin}}"
        },
        test_script="""
pm.test("PIN verified successfully (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Refreshes access & refresh tokens", function () {
    var jsonData = pm.response.json();
    pm.expect(jsonData.accessToken).to.be.a("string");
    pm.environment.set("userA_accessToken", jsonData.accessToken);
    pm.environment.set("userA_refreshToken", jsonData.refreshToken);
});
"""
    ),
    make_req(
        "08 - Change Password Back User A",
        "POST",
        "api/auth/change-password",
        token_var="userA_accessToken",
        body={
            "currentPassword": "{{user_new_password}}",
            "newPassword": "{{user_password}}"
        },
        test_script="""
pm.test("Password reverted successfully (204 No Content)", function () {
    pm.response.to.have.status(204);
});
"""
    ),
    make_req(
        "08a - Login User A With Reverted Password",
        "POST",
        "api/auth/login",
        body={
            "identifier": "{{userA_email}}",
            "password": "{{user_password}}"
        },
        test_script="""
pm.test("Login with reverted password succeeds (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Captures challenge token", function () {
    var jsonData = pm.response.json();
    pm.expect(jsonData.challengeToken).to.be.a("string");
    pm.environment.set("userA_challengeToken", jsonData.challengeToken);
});
"""
    ),
    make_req(
        "08b - Verify PIN User A After Password Revert",
        "POST",
        "api/auth/pin/verify",
        body={
            "challengeToken": "{{userA_challengeToken}}",
            "pin": "{{default_pin}}"
        },
        test_script="""
pm.test("PIN verified successfully (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Refreshes access & refresh tokens", function () {
    var jsonData = pm.response.json();
    pm.expect(jsonData.accessToken).to.be.a("string");
    pm.environment.set("userA_accessToken", jsonData.accessToken);
    pm.environment.set("userA_refreshToken", jsonData.refreshToken);
});
"""
    ),
    make_req(
        "09 - Change Security PIN User A",
        "PUT",
        "api/security-pin",
        token_var="userA_accessToken",
        body={
            "currentPin": "{{default_pin}}",
            "newPin": "{{user_new_pin}}",
            "confirmPin": "{{user_new_pin}}"
        },
        test_script="""
pm.test("PIN changed successfully (204 No Content)", function () {
    pm.response.to.have.status(204);
});
"""
    ),
    make_req(
        "10 - Change Security PIN Back User A",
        "PUT",
        "api/security-pin",
        token_var="userA_accessToken",
        body={
            "currentPin": "{{user_new_pin}}",
            "newPin": "{{default_pin}}",
            "confirmPin": "{{default_pin}}"
        },
        test_script="""
pm.test("PIN reverted successfully (204 No Content)", function () {
    pm.response.to.have.status(204);
});
"""
    ),
    make_req(
        "11 - Forgot Security PIN User A",
        "POST",
        "api/auth/pin/forgot",
        body={
            "identifier": "{{userA_email}}"
        },
        test_script="""
pm.test("Forgot PIN instructions accepted (202 Accepted)", function () {
    pm.response.to.have.status(202);
});
"""
    ),
    make_req(
        "12 - Validate Invalid PIN Reset Token",
        "POST",
        "api/auth/pin/reset/validate",
        body={
            "token": "invalid-token-string"
        },
        test_script="""
pm.test("Token validation responds (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Token is reported invalid", function () {
    var res = pm.response.json();
    pm.expect(res.valid).to.eql(false);
});
"""
    ),
    make_req(
        "13 - Negative Check: Wrong PIN Verify (Assert 401)",
        "POST",
        "api/security-pin/verify",
        token_var="userA_accessToken",
        body={
            "pin": "000000"
        },
        test_script="""
pm.test("Wrong PIN rejected (401 Unauthorized)", function () {
    pm.response.to.have.status(401);
});
"""
    ),
    make_req(
        "14 - Negative Check: Wrong OTP (Assert 400/401)",
        "POST",
        "api/auth/email-verification/verify",
        body={
            "email": "{{userA_email}}",
            "otp": "00000000"
        },
        test_script="""
pm.test("Wrong OTP rejected (400 or 401)", function () {
    pm.expect([400, 401]).to.include(pm.response.code);
});
"""
    ),
    make_req(
        "15 - Negative Check: Invalid Access Token (Assert 401)",
        "GET",
        "api/wallets",
        headers={
            "Authorization": "Bearer invalid.token.payload"
        },
        test_script="""
pm.test("Invalid token rejected (401 Unauthorized)", function () {
    pm.response.to.have.status(401);
});
"""
    ),
    make_req(
        "16 - Register User B",
        "POST",
        "api/auth/register",
        body={
            "fullName": "User Beta",
            "email": "{{userB_email}}",
            "phone": "{{userB_phone}}",
            "password": "{{user_password}}",
            "confirmPassword": "{{user_password}}"
        },
        test_script="""
pm.test("User B registered (200 OK)", function () {
    pm.response.to.have.status(200);
});
"""
    ),
    make_req(
        "17 - Verify Registration Email User B",
        "POST",
        "api/auth/email-verification/verify",
        body={
            "email": "{{userB_email}}",
            "otp": "{{static_otp}}"
        },
        test_script="""
pm.test("User B email verified (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Challenge requires SetupSecurityPin", function () {
    var jsonData = pm.response.json();
    pm.expect(jsonData.challengeToken).to.be.a("string");
    pm.environment.set("userB_challengeToken", jsonData.challengeToken);
});
"""
    ),
    make_req(
        "18 - Setup PIN User B",
        "POST",
        "api/auth/pin/setup",
        body={
            "challengeToken": "{{userB_challengeToken}}",
            "pin": "{{default_pin}}",
            "confirmPin": "{{default_pin}}"
        },
        test_script="""
pm.test("User B PIN setup successful (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Returns User B access token and user ID", function () {
    var jsonData = pm.response.json();
    pm.expect(jsonData.accessToken).to.be.a("string");
    pm.expect(jsonData.user).to.be.an("object");
    pm.environment.set("userB_accessToken", jsonData.accessToken);
    pm.environment.set("userB_id", jsonData.user.id);
});
"""
    ),
    make_req(
        "19 - Forgot Password User A",
        "POST",
        "api/auth/forgot-password",
        body={
            "identifier": "{{userA_email}}"
        },
        test_script="""
pm.test("Forgot password instructions accepted (202 Accepted)", function () {
    pm.response.to.have.status(202);
});
"""
    ),
    make_req(
        "20 - Reset Password User A",
        "POST",
        "api/auth/reset-password",
        body={
            "identifier": "{{userA_email}}",
            "otp": "{{static_otp}}",
            "newPassword": "{{user_password}}"
        },
        test_script="""
pm.test("Password reset completed (204 No Content)", function () {
    pm.response.to.have.status(204);
});
"""
    ),
    make_req(
        "21 - Re-login User A After Password Reset",
        "POST",
        "api/auth/login",
        body={
            "identifier": "{{userA_email}}",
            "password": "{{user_password}}"
        },
        test_script="""
pm.test("Login initiated successfully (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Challenge requires SecurityPin verification", function () {
    var jsonData = pm.response.json();
    pm.expect(jsonData.challengeToken).to.be.a("string");
    pm.environment.set("userA_challengeToken", jsonData.challengeToken);
});
"""
    ),
    make_req(
        "22 - Re-verify PIN User A After Password Reset",
        "POST",
        "api/auth/pin/verify",
        body={
            "challengeToken": "{{userA_challengeToken}}",
            "pin": "{{default_pin}}"
        },
        test_script="""
pm.test("PIN login verified successfully (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Updates access & refresh tokens", function () {
    var jsonData = pm.response.json();
    pm.expect(jsonData.accessToken).to.be.a("string");
    pm.expect(jsonData.refreshToken).to.be.a("string");
    pm.environment.set("userA_accessToken", jsonData.accessToken);
    pm.environment.set("userA_refreshToken", jsonData.refreshToken);
});
"""
    )
]

collection["item"].append({
    "name": "02-Auth",
    "item": auth_items
})

# -------------------------------------------------------------
# 03-Wallet & Finance
# -------------------------------------------------------------
wallet_items = [
    make_req(
        "01 - List Wallets User A",
        "GET",
        "api/wallets",
        token_var="userA_accessToken",
        test_script="""
pm.test("Status code is 200", function () {
    pm.response.to.have.status(200);
});
pm.test("Identifies User A Main Wallet", function () {
    var wallets = pm.response.json();
    pm.expect(wallets).to.be.an("array");
    var mainWallet = wallets.find(function (w) { return w.type === "spending" || w.isDefault; });
    pm.expect(mainWallet).to.be.an("object");
    pm.environment.set("userA_mainWalletId", mainWallet.id);
    pm.environment.set("userA_initialBalance", mainWallet.availableBalance);
});
"""
    ),
    make_req(
        "02 - List Categories",
        "GET",
        "api/categories",
        token_var="userA_accessToken",
        test_script="""
pm.test("Status code is 200", function () {
    pm.response.to.have.status(200);
});
pm.test("Captures Income and Expense category IDs", function () {
    var cats = pm.response.json();
    pm.expect(cats).to.be.an("array");
    var incomeCat = cats.find(function (c) { return c.kind === "Income"; });
    var expenseCat = cats.find(function (c) { return c.kind === "Expense"; });
    pm.expect(incomeCat).to.be.an("object");
    pm.expect(expenseCat).to.be.an("object");
    pm.environment.set("incomeCategoryId", incomeCat.id);
    pm.environment.set("expenseCategoryId", expenseCat.id);
});
"""
    ),
    make_req(
        "03 - Create Sub-wallet 1 (Savings)",
        "POST",
        "api/wallets",
        token_var="userA_accessToken",
        body={
            "name": "Savings Alpha",
            "type": "savings",
            "isDefault": False
        },
        test_script="""
pm.test("Sub-wallet 1 created (201 Created)", function () {
    pm.response.to.have.status(201);
});
pm.test("Saves Sub-wallet 1 ID", function () {
    var w = pm.response.json();
    pm.expect(w.id).to.be.a("string");
    pm.environment.set("userA_subWallet1Id", w.id);
});
"""
    ),
    make_req(
        "04 - Create Sub-wallet 2 (Emergency)",
        "POST",
        "api/wallets",
        token_var="userA_accessToken",
        body={
            "name": "Emergency Alpha",
            "type": "emergency",
            "isDefault": False
        },
        test_script="""
pm.test("Sub-wallet 2 created (201 Created)", function () {
    pm.response.to.have.status(201);
});
pm.test("Saves Sub-wallet 2 ID", function () {
    var w = pm.response.json();
    pm.expect(w.id).to.be.a("string");
    pm.environment.set("userA_subWallet2Id", w.id);
});
"""
    ),
    make_req(
        "05 - Attempt 3rd Sub-wallet (Assert 409 limit exceeded)",
        "POST",
        "api/wallets",
        token_var="userA_accessToken",
        body={
            "name": "Overflow Alpha",
            "type": "savings",
            "isDefault": False
        },
        test_script="""
pm.test("Fails with 409 Conflict due to MaximumPersonalWallets limit", function () {
    pm.response.to.have.status(409);
});
"""
    ),
    make_file_upload_req(
        "05.1 - Upload Receipt Evidence User A (Income)",
        "api/evidence",
        "file",
        "backend/postman/receipt.png",
        token_var="userA_accessToken",
        test_script="""
pm.test("Receipt evidence uploaded (201 Created)", function () {
    pm.response.to.have.status(201);
});
pm.test("Saves userA_evidenceId1", function () {
    var ev = pm.response.json();
    pm.expect(ev.id).to.be.a("string");
    pm.environment.set("userA_evidenceId1", ev.id);
});
"""
    ),
    make_req(
        "06 - Record Income to User A Main Wallet",
        "POST",
        "api/transactions/income",
        token_var="userA_accessToken",
        body={
            "walletId": "{{userA_mainWalletId}}",
            "categoryId": "{{incomeCategoryId}}",
            "amount": 10000000,
            "description": "Salary deposit 10M VND",
            "evidenceIds": ["{{userA_evidenceId1}}"],
            "securityPin": "{{default_pin}}"
        },
        test_script="""
pm.test("Income recorded (201 Created)", function () {
    pm.response.to.have.status(201);
});
pm.test("Transaction amount verified", function () {
    var tx = pm.response.json();
    pm.expect(tx.amount).to.eql(10000000);
});
"""
    ),
    make_file_upload_req(
        "06.1 - Upload Receipt Evidence User A (Expense)",
        "api/evidence",
        "file",
        "backend/postman/receipt.png",
        token_var="userA_accessToken",
        test_script="""
pm.test("Receipt evidence uploaded (201 Created)", function () {
    pm.response.to.have.status(201);
});
pm.test("Saves userA_evidenceId2", function () {
    var ev = pm.response.json();
    pm.expect(ev.id).to.be.a("string");
    pm.environment.set("userA_evidenceId2", ev.id);
});
"""
    ),
    make_req(
        "07 - Record Expense from User A Main Wallet",
        "POST",
        "api/transactions/expense",
        token_var="userA_accessToken",
        body={
            "walletId": "{{userA_mainWalletId}}",
            "categoryId": "{{expenseCategoryId}}",
            "amount": 500000,
            "description": "Supermarket groceries",
            "evidenceIds": ["{{userA_evidenceId2}}"],
            "securityPin": "{{default_pin}}"
        },
        test_script="""
pm.test("Expense recorded (201 Created)", function () {
    pm.response.to.have.status(201);
});
pm.test("Transaction amount verified", function () {
    var tx = pm.response.json();
    pm.expect(tx.amount).to.eql(500000);
});
"""
    ),
    make_req(
        "08 - Internal Transfer (Main to Sub-wallet)",
        "POST",
        "api/transfers/internal",
        token_var="userA_accessToken",
        body={
            "sourceWalletId": "{{userA_mainWalletId}}",
            "destinationWalletId": "{{userA_subWallet1Id}}",
            "amount": 1000000,
            "description": "Transfer to Savings",
            "idempotencyKey": "internal-{{runId}}",
            "securityPin": "{{default_pin}}"
        },
        test_script="""
pm.test("Internal transfer completed (201 Created)", function () {
    pm.response.to.have.status(201);
});
pm.test("Transfer status is Completed", function () {
    var t = pm.response.json();
    pm.expect(t.status).to.eql("Completed");
    pm.expect(t.amount).to.eql(1000000);
});
"""
    ),
    make_req(
        "09 - List Wallets User B",
        "GET",
        "api/wallets",
        token_var="userB_accessToken",
        test_script="""
pm.test("Status code is 200", function () {
    pm.response.to.have.status(200);
});
pm.test("Identifies User B Main Wallet", function () {
    var wallets = pm.response.json();
    var mainWallet = wallets.find(function (w) { return w.type === "spending" || w.isDefault; });
    pm.expect(mainWallet).to.be.an("object");
    pm.environment.set("userB_mainWalletId", mainWallet.id);
    pm.environment.set("userB_initialBalance", mainWallet.availableBalance);
});
"""
    ),
    make_req(
        "10 - P2P Transfer (User A to User B)",
        "POST",
        "api/transfers/p2p",
        token_var="userA_accessToken",
        body={
            "sourceWalletId": "{{userA_mainWalletId}}",
            "recipientUserId": "{{userB_id}}",
            "amount": 2000000,
            "description": "Fund contribution and split advance",
            "idempotencyKey": "p2p-{{runId}}",
            "securityPin": "{{default_pin}}"
        },
        test_script="""
pm.test("P2P transfer completed (201 Created)", function () {
    pm.response.to.have.status(201);
});
pm.test("Recipient and amount verified", function () {
    var t = pm.response.json();
    pm.expect(t.status).to.eql("Completed");
    pm.expect(t.amount).to.eql(2000000);
    pm.expect(t.recipientUserId).to.eql(pm.environment.get("userB_id"));
});
"""
    ),
    make_req(
        "11 - P2P Transfer Replay Protection",
        "POST",
        "api/transfers/p2p",
        token_var="userA_accessToken",
        body={
            "sourceWalletId": "{{userA_mainWalletId}}",
            "recipientUserId": "{{userB_id}}",
            "amount": 2000000,
            "description": "Fund contribution and split advance",
            "idempotencyKey": "p2p-{{runId}}",
            "securityPin": "{{default_pin}}"
        },
        test_script="""
pm.test("Replayed transfer returns 201 Created with replayed flag", function () {
    pm.response.to.have.status(201);
    var t = pm.response.json();
    pm.expect(t.replayed).to.eql(true);
});
"""
    ),
    make_req(
        "12 - Negative Check: P2P Insufficient Balance (Assert 409 Conflict)",
        "POST",
        "api/transfers/p2p",
        token_var="userA_accessToken",
        body={
            "sourceWalletId": "{{userA_mainWalletId}}",
            "recipientUserId": "{{userB_id}}",
            "amount": 999999999,
            "description": "Excess transfer test",
            "idempotencyKey": "p2p-excess-{{runId}}",
            "securityPin": "{{default_pin}}"
        },
        test_script="""
pm.test("Fails with 409 Conflict due to insufficient funds", function () {
    pm.response.to.have.status(409);
});
"""
    ),
    make_req(
        "13 - Negative Check: P2P Wrong PIN (Assert 400/401)",
        "POST",
        "api/transfers/p2p",
        token_var="userA_accessToken",
        body={
            "sourceWalletId": "{{userA_mainWalletId}}",
            "recipientUserId": "{{userB_id}}",
            "amount": 100000,
            "description": "Wrong PIN transfer",
            "idempotencyKey": "p2p-badpin-{{runId}}",
            "securityPin": "999999"
        },
        test_script="""
pm.test("Fails with 401 Unauthorized or 400 Bad Request due to invalid security PIN", function () {
    pm.expect([400, 401]).to.include(pm.response.code);
});
"""
    )
]

collection["item"].append({
    "name": "03-Wallet & Finance",
    "item": wallet_items
})

# -------------------------------------------------------------
# 04-Friends
# -------------------------------------------------------------
friend_items = [
    make_req(
        "01 - Search User B by Contact",
        "GET",
        "api/friends/search?contact={{userB_email}}",
        token_var="userA_accessToken",
        test_script="""
pm.test("Status code is 200", function () {
    pm.response.to.have.status(200);
});
pm.test("Finds User B profile", function () {
    var contact = pm.response.json();
    pm.expect(contact.userId).to.eql(pm.environment.get("userB_id"));
});
"""
    ),
    make_req(
        "02 - Send Friend Request (A to B)",
        "POST",
        "api/friends/requests",
        token_var="userA_accessToken",
        body={
            "contact": "{{userB_email}}",
            "message": "Let's connect on SpendWise!"
        },
        test_script="""
pm.test("Friend request sent (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Captures friendRequestId", function () {
    var req = pm.response.json();
    pm.expect(req.id).to.be.a("string");
    pm.environment.set("friendRequestId", req.id);
});
"""
    ),
    make_req(
        "03 - User B Incoming Friend Requests",
        "GET",
        "api/friends/requests/incoming",
        token_var="userB_accessToken",
        test_script="""
pm.test("Status code is 200", function () {
    pm.response.to.have.status(200);
});
pm.test("Incoming request from User A is present", function () {
    var reqs = pm.response.json();
    var found = reqs.find(function (r) { return r.id === pm.environment.get("friendRequestId"); });
    pm.expect(found).to.be.an("object");
});
"""
    ),
    make_req(
        "04 - User B Accepts Friend Request",
        "POST",
        "api/friends/requests/{{friendRequestId}}/accept",
        token_var="userB_accessToken",
        test_script="""
pm.test("Friend request accepted (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Friendship established", function () {
    var friend = pm.response.json();
    pm.expect(friend.userId).to.eql(pm.environment.get("userA_id"));
});
"""
    ),
    make_req(
        "05 - User A Lists Friends",
        "GET",
        "api/friends",
        token_var="userA_accessToken",
        test_script="""
pm.test("Status code is 200", function () {
    pm.response.to.have.status(200);
});
pm.test("User B is listed in friends list", function () {
    var friends = pm.response.json();
    var found = friends.find(function (f) { return f.userId === pm.environment.get("userB_id"); });
    pm.expect(found).to.be.an("object");
});
"""
    )
]

collection["item"].append({
    "name": "04-Friends",
    "item": friend_items
})

# -------------------------------------------------------------
# 05-Fixed Fund
# -------------------------------------------------------------
fixed_items = [
    make_req(
        "01 - Create Fixed Fund 1 (User A)",
        "POST",
        "api/v1/funds",
        token_var="userA_accessToken",
        body={
            "name": "Trip Da Lat {{runId}}",
            "kind": "Fixed",
            "targetVnd": 2000000,
            "securityPin": "{{default_pin}}"
        },
        test_script="""
pm.test("Fixed fund created (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Saves Fixed Fund ID", function () {
    var fund = pm.response.json();
    pm.expect(fund.id).to.be.a("string");
    pm.expect(fund.kind).to.eql("Fixed");
    pm.environment.set("fixedFundId", fund.id);
});
"""
    ),
    make_req(
        "02 - Invite User B to Fixed Fund",
        "POST",
        "api/v1/funds/{{fixedFundId}}/invitations",
        token_var="userA_accessToken",
        body={
            "userId": "{{userB_id}}"
        },
        test_script="""
pm.test("Invitation sent (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Saves Fixed Invitation ID", function () {
    var invite = pm.response.json();
    pm.expect(invite.id).to.be.a("string");
    pm.environment.set("fixedInviteId", invite.id);
});
"""
    ),
    make_req(
        "03 - User B Accepts Fixed Fund Invitation",
        "POST",
        "api/v1/funds/invitations/{{fixedInviteId}}/accept",
        token_var="userB_accessToken",
        test_script="""
pm.test("Invitation accepted (200 OK)", function () {
    pm.response.to.have.status(200);
});
"""
    ),
    make_req(
        "04 - Confirm Obligations (User A)",
        "POST",
        "api/v1/funds/{{fixedFundId}}/obligations/confirm",
        token_var="userA_accessToken",
        body={
            "targetVnd": 2000000,
            "securityPin": "{{default_pin}}"
        },
        test_script="""
pm.test("Obligations confirmed (200 OK)", function () {
    pm.response.to.have.status(200);
});
"""
    ),
    make_req(
        "05 - User A Contributes 1M VND",
        "POST",
        "api/v1/funds/{{fixedFundId}}/contributions",
        token_var="userA_accessToken",
        body={
            "walletId": "{{userA_mainWalletId}}",
            "amountVnd": 1000000,
            "obligationRevision": 1,
            "idempotencyKey": "contrib-a-fixed-{{runId}}",
            "securityPin": "{{default_pin}}"
        },
        test_script="""
pm.test("Contribution successful (200 OK)", function () {
    pm.response.to.have.status(200);
});
"""
    ),
    make_req(
        "06 - User B Contributes 1M VND",
        "POST",
        "api/v1/funds/{{fixedFundId}}/contributions",
        token_var="userB_accessToken",
        body={
            "walletId": "{{userB_mainWalletId}}",
            "amountVnd": 1000000,
            "obligationRevision": 1,
            "idempotencyKey": "contrib-b-fixed-{{runId}}",
            "securityPin": "{{default_pin}}"
        },
        test_script="""
pm.test("Contribution successful (200 OK)", function () {
    pm.response.to.have.status(200);
});
"""
    ),
    make_req(
        "07 - Negative Check: User B Direct Spend when CanSpend is OFF (Assert 403 Forbidden)",
        "POST",
        "api/v1/funds/{{fixedFundId}}/requests",
        token_var="userB_accessToken",
        body={
            "kind": "Expense",
            "amountVnd": 50000,
            "recipientUserId": "{{userB_id}}",
            "purpose": "Unauthorized snack spend",
            "securityPin": "{{default_pin}}",
            "idempotencyKey": "spend-canspend-off-{{runId}}"
        },
        test_script="""
pm.test("Direct spend rejected when CanSpend is OFF (403 Forbidden)", function () {
    pm.response.to.have.status(403);
});
"""
    ),
    make_req(
        "08 - User A Sets Spending Permission ON for User B",
        "PUT",
        "api/v1/funds/{{fixedFundId}}/members/{{userB_id}}/spending-permission",
        token_var="userA_accessToken",
        body={
            "enabled": True,
            "securityPin": "{{default_pin}}"
        },
        test_script="""
pm.test("Spending permission enabled (204 No Content)", function () {
    pm.response.to.have.status(204);
});
"""
    ),
    make_req(
        "09 - User B Direct Spend Under 20% Threshold (Immediate Completion)",
        "POST",
        "api/v1/funds/{{fixedFundId}}/requests",
        token_var="userB_accessToken",
        body={
            "kind": "Expense",
            "amountVnd": 100000,
            "recipientUserId": "{{userB_id}}",
            "purpose": "Team snacks under 20%",
            "securityPin": "{{default_pin}}",
            "idempotencyKey": "spend-under20-{{runId}}"
        },
        test_script="""
pm.test("Direct spend under threshold completes immediately (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("State is Completed", function () {
    var res = pm.response.json();
    pm.expect(res.state).to.eql("Completed");
    pm.expect(res.amountVnd).to.eql(100000);
});
"""
    ),
    make_req(
        "10 - User B Large Expense Over 20% Threshold (Enters 24h Hold)",
        "POST",
        "api/v1/funds/{{fixedFundId}}/requests",
        token_var="userB_accessToken",
        body={
            "kind": "Expense",
            "amountVnd": 500000,
            "recipientUserId": "{{userB_id}}",
            "purpose": "Hotel deposit over 20%",
            "securityPin": "{{default_pin}}",
            "idempotencyKey": "spend-over20-{{runId}}"
        },
        test_script="""
pm.test("Large expense accepted and held (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("State enters 24h hold", function () {
    var res = pm.response.json();
    pm.expect(["Pending24h", "Hold24h"]).to.include(res.state);
    pm.expect(res.amountVnd).to.eql(500000);
    pm.environment.set("heldExpenseId", res.id);
});
"""
    ),
    make_req(
        "10a - Owner Cannot Bypass 24h Hold", "POST",
        "api/v1/funds/requests/{{heldExpenseId}}/review", token_var="userA_accessToken",
        body={"approved": True, "reason": "Early review must fail", "securityPin": "{{default_pin}}"},
        test_script='pm.test("Early review blocked", () => pm.response.to.have.status(409)); pm.test("Server hold enforced", () => pm.expect(pm.response.json().code).to.eql("fund_request_hold_active"));'
    ),
    make_req(
        "10b - Member Reports Large Expense Hold (Transitions to Suspended)", "POST",
        "api/v1/funds/requests/{{heldExpenseId}}/report", token_var="userA_accessToken",
        body={"reason": "Contractor invoice amount discrepancy"},
        test_script='pm.test("Report accepted (200 OK)", () => pm.response.to.have.status(200)); pm.test("State transitions to Suspended", () => { var res = pm.response.json(); pm.expect(res.state).to.eql("Suspended"); pm.expect(res.executionAvailability).to.eql("Suspended"); });'
    ),
    make_req(
        "10c - Refund Cannot Consume Held Funds While Suspended", "GET",
        "api/v1/funds/{{fixedFundId}}/refund-preview", token_var="userB_accessToken",
        test_script='pm.test("Refund blocked while suspended hold active", () => pm.response.to.have.status(409)); pm.test("Hold protected", () => pm.expect(pm.response.json().code).to.eql("refund_pending_hold"));'
    ),
    make_req(
        "10d - Requester Cancels Suspended Expense Releasing Hold", "POST",
        "api/v1/funds/requests/{{heldExpenseId}}/cancel", token_var="userB_accessToken",
        body={"securityPin": "{{default_pin}}"},
        test_script='pm.test("Cancel accepted", () => pm.response.to.have.status(200)); pm.test("Hold cancelled", () => pm.expect(pm.response.json().state).to.eql("Cancelled"));'
    ),
    make_req(
        "11 - Get Fixed Fund Finance Summary",
        "GET",
        "api/v1/funds/{{fixedFundId}}/finance",
        token_var="userA_accessToken",
        test_script="""
pm.test("Status code is 200", function () {
    pm.response.to.have.status(200);
});
pm.test("Active treasury balance is positive", function () {
    var f = pm.response.json();
    pm.expect(f.balanceVnd).to.be.above(0);
});
"""
    ),
    make_req(
        "12 - User B Previews Refund (Case B Partially Spent)",
        "GET",
        "api/v1/funds/{{fixedFundId}}/refund-preview",
        token_var="userB_accessToken",
        test_script="""
pm.test("Status code is 200", function () {
    pm.response.to.have.status(200);
});
pm.test("Refund preview calculated according to Case B", function () {
    var p = pm.response.json();
    pm.expect(p.eligibleRefundAmountVnd).to.be.above(0);
    pm.expect(["CaseB_FixedSpentClearShare", "CaseB_PartiallySpent"]).to.include(p.caseType);
});
"""
    ),
    make_req(
        "13 - User B Leaves Fixed Fund (Refund Case B)",
        "POST",
        "api/v1/funds/{{fixedFundId}}/leave",
        token_var="userB_accessToken",
        body={
            "securityPin": "{{default_pin}}",
            "idempotencyKey": "leave-b-fixed-{{runId}}"
        },
        test_script="""
pm.test("Member left with refund processed (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Refund processed successfully", function () {
    var res = pm.response.json();
    pm.expect(res.refundAmountVnd).to.be.above(0);
    pm.expect(res.status).to.eql("Completed");
    pm.expect(["CaseB_FixedSpentClearShare", "CaseB_PartiallySpent"]).to.include(res.caseType);
});
"""
    ),
    make_req(
        "14 - Create Fixed Fund 2 for Kick and Dissolve (User A)",
        "POST",
        "api/v1/funds",
        token_var="userA_accessToken",
        body={
            "name": "Weekend BBQ {{runId}}",
            "kind": "Fixed",
            "targetVnd": 1000000,
            "securityPin": "{{default_pin}}"
        },
        test_script="""
pm.test("Fixed fund 2 created (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Saves Fixed Fund 2 ID", function () {
    var fund = pm.response.json();
    pm.expect(fund.id).to.be.a("string");
    pm.environment.set("fixedFund2Id", fund.id);
});
"""
    ),
    make_req(
        "15 - Invite User B to Fixed Fund 2",
        "POST",
        "api/v1/funds/{{fixedFund2Id}}/invitations",
        token_var="userA_accessToken",
        body={
            "userId": "{{userB_id}}"
        },
        test_script="""
pm.test("Invitation to Fund 2 sent (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Saves Invitation ID", function () {
    var invite = pm.response.json();
    pm.expect(invite.id).to.be.a("string");
    pm.environment.set("fixedInvite2Id", invite.id);
});
"""
    ),
    make_req(
        "16 - User B Accepts Fixed Fund 2 Invitation",
        "POST",
        "api/v1/funds/invitations/{{fixedInvite2Id}}/accept",
        token_var="userB_accessToken",
        test_script="""
pm.test("Invitation to Fund 2 accepted (200 OK)", function () {
    pm.response.to.have.status(200);
});
"""
    ),
    make_req(
        "17 - Confirm Obligations Fixed Fund 2",
        "POST",
        "api/v1/funds/{{fixedFund2Id}}/obligations/confirm",
        token_var="userA_accessToken",
        body={
            "targetVnd": 1000000,
            "securityPin": "{{default_pin}}"
        },
        test_script="""
pm.test("Obligations confirmed for Fund 2 (200 OK)", function () {
    pm.response.to.have.status(200);
});
"""
    ),
    make_req(
        "18 - User A Contributes 500k to Fixed Fund 2",
        "POST",
        "api/v1/funds/{{fixedFund2Id}}/contributions",
        token_var="userA_accessToken",
        body={
            "walletId": "{{userA_mainWalletId}}",
            "amountVnd": 500000,
            "obligationRevision": 1,
            "idempotencyKey": "contrib-a-fund2-{{runId}}",
            "securityPin": "{{default_pin}}"
        },
        test_script="""
pm.test("User A Fund 2 contribution successful (200 OK)", function () {
    pm.response.to.have.status(200);
});
"""
    ),
    make_req(
        "19 - User B Contributes 500k to Fixed Fund 2",
        "POST",
        "api/v1/funds/{{fixedFund2Id}}/contributions",
        token_var="userB_accessToken",
        body={
            "walletId": "{{userB_mainWalletId}}",
            "amountVnd": 500000,
            "obligationRevision": 1,
            "idempotencyKey": "contrib-b-fund2-{{runId}}",
            "securityPin": "{{default_pin}}"
        },
        test_script="""
pm.test("User B Fund 2 contribution successful (200 OK)", function () {
    pm.response.to.have.status(200);
});
"""
    ),
    make_req(
        "20 - User A Kicks User B from Fixed Fund 2 (Refund Processed)",
        "POST",
        "api/v1/funds/{{fixedFund2Id}}/members/{{userB_id}}/kick",
        token_var="userA_accessToken",
        body={
            "reason": "Member relocated",
            "securityPin": "{{default_pin}}",
            "idempotencyKey": "kick-b-fund2-{{runId}}"
        },
        test_script="""
pm.test("Kick processed with refund (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("500k VND refunded to kicked member", function () {
    var res = pm.response.json();
    pm.expect(res.refundAmountVnd).to.eql(500000);
    pm.expect(res.status).to.eql("Completed");
});
"""
    ),
    make_req(
        "21 - User A Dissolves Fixed Fund 2 (Zero Balance Invariant)",
        "POST",
        "api/v1/funds/{{fixedFund2Id}}/dissolve",
        token_var="userA_accessToken",
        body={
            "securityPin": "{{default_pin}}",
            "idempotencyKey": "dissolve-fixed2-{{runId}}"
        },
        test_script="""
pm.test("Fixed Fund 2 dissolved (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Treasury remaining balance is exactly 0 VND", function () {
    var res = pm.response.json();
    pm.expect(res.finalBalanceVnd).to.eql(0);
});
"""
    )
]

collection["item"].append({
    "name": "05-Fixed Fund",
    "item": fixed_items
})

# -------------------------------------------------------------
# 06-Free Fund
# -------------------------------------------------------------
free_items = [
    make_req(
        "01 - Create Free Fund (User A)",
        "POST",
        "api/v1/funds",
        token_var="userA_accessToken",
        body={
            "name": "Team Lunch {{runId}}",
            "kind": "Free",
            "securityPin": "{{default_pin}}"
        },
        test_script="""
pm.test("Free fund created (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Saves Free Fund ID", function () {
    var fund = pm.response.json();
    pm.expect(fund.id).to.be.a("string");
    pm.expect(fund.kind).to.eql("Free");
    pm.environment.set("freeFundId", fund.id);
});
"""
    ),
    make_req(
        "02 - Invite User B to Free Fund",
        "POST",
        "api/v1/funds/{{freeFundId}}/invitations",
        token_var="userA_accessToken",
        body={
            "userId": "{{userB_id}}"
        },
        test_script="""
pm.test("Invitation sent (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Saves Free Invitation ID", function () {
    var invite = pm.response.json();
    pm.expect(invite.id).to.be.a("string");
    pm.environment.set("freeInviteId", invite.id);
});
"""
    ),
    make_req(
        "03 - User B Accepts Free Fund Invitation",
        "POST",
        "api/v1/funds/invitations/{{freeInviteId}}/accept",
        token_var="userB_accessToken",
        test_script="""
pm.test("Invitation accepted (200 OK)", function () {
    pm.response.to.have.status(200);
});
"""
    ),
    make_req(
        "04 - User A Contributes 700k VND",
        "POST",
        "api/v1/funds/{{freeFundId}}/contributions",
        token_var="userA_accessToken",
        body={
            "walletId": "{{userA_mainWalletId}}",
            "amountVnd": 700000,
            "idempotencyKey": "contrib-a-free-{{runId}}",
            "securityPin": "{{default_pin}}"
        },
        test_script="""
pm.test("Contribution successful (200 OK)", function () {
    pm.response.to.have.status(200);
});
"""
    ),
    make_req(
        "05 - User B Contributes 300k VND",
        "POST",
        "api/v1/funds/{{freeFundId}}/contributions",
        token_var="userB_accessToken",
        body={
            "walletId": "{{userB_mainWalletId}}",
            "amountVnd": 300000,
            "idempotencyKey": "contrib-b-free-{{runId}}",
            "securityPin": "{{default_pin}}"
        },
        test_script="""
pm.test("Contribution successful (200 OK)", function () {
    pm.response.to.have.status(200);
});
"""
    ),
    make_req(
        "06 - User B Creates Withdrawal Request",
        "POST",
        "api/v1/funds/{{freeFundId}}/requests",
        token_var="userB_accessToken",
        body={
            "kind": "Withdrawal",
            "amountVnd": 150000,
            "recipientUserId": "{{userB_id}}",
            "purpose": "Snacks reimbursement",
            "securityPin": "{{default_pin}}",
            "idempotencyKey": "withdraw-b-free-{{runId}}"
        },
        test_script="""
pm.test("Withdrawal request created (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Saves Withdrawal Request ID", function () {
    var req = pm.response.json();
    pm.expect(req.id).to.be.a("string");
    pm.environment.set("withdrawalRequestId", req.id);
});
"""
    ),
    make_req(
        "07 - User A Approves Withdrawal (Real Money Payout)",
        "POST",
        "api/v1/funds/requests/{{withdrawalRequestId}}/review",
        token_var="userA_accessToken",
        body={
            "approved": True,
            "reason": "Approved snacks for team",
            "securityPin": "{{default_pin}}"
        },
        test_script="""
pm.test("Review processed (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Asserts Completed state and real payout", function () {
    var res = pm.response.json();
    pm.expect(res.state).to.eql("Completed");
    pm.expect(res.amountVnd).to.eql(150000);
});
"""
    ),
    make_req(
        "08 - User A Previews Dissolution Refund (Net Equity Formula)",
        "GET",
        "api/v1/funds/{{freeFundId}}/refund-preview?memberUserId={{userA_id}}",
        token_var="userA_accessToken",
        test_script="""
pm.test("Status code is 200", function () {
    pm.response.to.have.status(200);
});
pm.test("Preview calculates proportional share based on net equity", function () {
    var p = pm.response.json();
    pm.expect(p.eligibleRefundAmountVnd).to.be.above(0);
});
"""
    ),
    make_req(
        "09 - User A Dissolves Free Fund with Withdrawal History (Zero Balance Invariant)",
        "POST",
        "api/v1/funds/{{freeFundId}}/dissolve",
        token_var="userA_accessToken",
        body={
            "securityPin": "{{default_pin}}",
            "idempotencyKey": "dissolve-free-{{runId}}"
        },
        test_script="""
pm.test("Fund dissolved successfully (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Treasury remaining balance is exactly 0 VND", function () {
    var res = pm.response.json();
    pm.expect(res.finalBalanceVnd).to.eql(0);
    pm.expect(res.refunds).to.be.an("array");
    pm.expect(res.refunds.length).to.be.at.least(2);
});
"""
    )
]

collection["item"].append({
    "name": "06-Free Fund",
    "item": free_items
})

# -------------------------------------------------------------
# 07-Support
# -------------------------------------------------------------
support_items = [
    make_req(
        "01 - User A Submits Support Ticket",
        "POST",
        "api/cases/support",
        token_var="userA_accessToken",
        body={
            "category": "general",
            "priority": "Normal",
            "subject": "E2E Support Case {{runId}}",
            "description": "Ledger reconciliation and fund inquiry"
        },
        test_script="""
pm.test("Support ticket created (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Saves supportCaseId", function () {
    var c = pm.response.json();
    pm.expect(c.id).to.be.a("string");
    pm.environment.set("supportCaseId", c.id);
});
"""
    ),
    make_req(
        "02 - User A Lists Support Cases",
        "GET",
        "api/cases/support",
        token_var="userA_accessToken",
        test_script="""
pm.test("Status code is 200", function () {
    pm.response.to.have.status(200);
});
pm.test("Ticket is listed in User A cases", function () {
    var cases = pm.response.json();
    var found = cases.find(function (c) { return c.id === pm.environment.get("supportCaseId"); });
    pm.expect(found).to.be.an("object");
});
"""
    ),
    make_req(
        "03 - User A Replies to Support Case",
        "POST",
        "api/cases/support/{{supportCaseId}}/messages",
        token_var="userA_accessToken",
        body={
            "message": "Providing additional context for support case investigation."
        },
        test_script="""
pm.test("Message added to support case (200 OK)", function () {
    pm.response.to.have.status(200);
});
"""
    )
]

collection["item"].append({
    "name": "07-Support",
    "item": support_items
})

# -------------------------------------------------------------
# 08-Admin 5 Areas
# -------------------------------------------------------------
admin_items = [
    make_req(
        "01 - Admin Login Challenge",
        "POST",
        "api/auth/login",
        body={
            "identifier": "{{admin_email}}",
            "password": "{{admin_password}}"
        },
        test_script="""
pm.test("Admin login challenge returned (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Captures admin challenge and status", function () {
    var jsonData = pm.response.json();
    pm.expect(jsonData.challengeToken).to.be.a("string");
    pm.environment.set("admin_challengeToken", jsonData.challengeToken);
    pm.environment.set("admin_challengeStatus", jsonData.status);
});
"""
    ),
    make_req(
        "02 - Admin Complete Login (PIN Setup or Verify)",
        "POST",
        "api/auth/pin/setup",
        body={
            "challengeToken": "{{admin_challengeToken}}",
            "pin": "{{default_pin}}",
            "confirmPin": "{{default_pin}}"
        },
        pre_script="""
var status = pm.environment.get("admin_challengeStatus");
if (status === "PIN_REQUIRED") {
    pm.request.url.path = ["api", "auth", "pin", "verify"];
    pm.request.body.raw = JSON.stringify({
        challengeToken: pm.environment.get("admin_challengeToken"),
        pin: pm.environment.get("default_pin")
    });
} else {
    pm.request.url.path = ["api", "auth", "pin", "setup"];
    pm.request.body.raw = JSON.stringify({
        challengeToken: pm.environment.get("admin_challengeToken"),
        pin: pm.environment.get("default_pin"),
        confirmPin: pm.environment.get("default_pin")
    });
}
""",
        test_script="""
pm.test("Admin authenticated successfully (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Admin access token issued", function () {
    var jsonData = pm.response.json();
    pm.expect(jsonData.accessToken).to.be.a("string");
    pm.environment.set("admin_accessToken", jsonData.accessToken);
    if (jsonData.user && jsonData.user.id) {
        pm.environment.set("admin_id", jsonData.user.id);
    }
});
"""
    ),
    make_req(
        "03 - ADM-01: Admin Overview",
        "GET",
        "api/v1/admin/overview",
        token_var="admin_accessToken",
        test_script="""
pm.test("Admin overview retrieved (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Metrics returned in summary", function () {
    var res = pm.response.json();
    pm.expect(res).to.be.an("object");
});
"""
    ),
    make_req(
        "04 - ADM-02: Admin Users Search",
        "POST",
        "api/v1/admin/users/search",
        token_var="admin_accessToken",
        body={
            "reason": "Audit search users for V1 acceptance"
        },
        test_script="""
pm.test("Admin users search returned (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Finds registered test users", function () {
    var res = pm.response.json();
    pm.expect(res.items).to.be.an("array");
    pm.expect(res.items.length).to.be.at.least(1);
});
"""
    ),
    make_req(
        "05 - ADM-03: Admin Support Cases Search",
        "POST",
        "api/v1/admin/support/tickets/search",
        token_var="admin_accessToken",
        body={
            "reason": "Audit support tickets for V1 acceptance"
        },
        test_script="""
pm.test("Admin support cases returned (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Includes user support cases", function () {
    var res = pm.response.json();
    pm.expect(res.items).to.be.an("array");
});
"""
    ),
    make_req(
        "06 - ADM-03: Admin Assigns Ticket",
        "POST",
        "api/v1/admin/support/{{supportCaseId}}/assign",
        token_var="admin_accessToken",
        body={
            "reason": "Assigning ticket to admin",
            "operatorId": "{{admin_id}}"
        },
        test_script="""
pm.test("Support ticket assigned to operator (200 OK)", function () {
    pm.response.to.have.status(200);
});
"""
    ),
    make_req(
        "07 - ADM-03: Admin Requests Info from User",
        "POST",
        "api/v1/admin/support/{{supportCaseId}}/request-info",
        token_var="admin_accessToken",
        body={
            "reason": "Need more info from customer",
            "message": "Please provide proof of receipt and payment screenshot."
        },
        test_script="""
pm.test("Info requested from user (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Case status moves to WaitingForUser", function () {
    var res = pm.response.json();
    pm.expect(res.status).to.eql("WaitingForUser");
});
"""
    ),
    make_req(
        "08 - User A Replies with Requested Info",
        "POST",
        "api/cases/support/{{supportCaseId}}/messages",
        token_var="userA_accessToken",
        body={
            "message": "Here is the transaction receipt and details as requested."
        },
        test_script="""
pm.test("User replied to support ticket (200 OK)", function () {
    pm.response.to.have.status(200);
});
"""
    ),
    make_req(
        "09 - ADM-03: Admin Resolves Ticket",
        "POST",
        "api/v1/admin/support/{{supportCaseId}}/resolve",
        token_var="admin_accessToken",
        body={
            "reason": "Resolution complete",
            "resolutionMessage": "All details verified and case resolved successfully.",
            "isRejected": False
        },
        test_script="""
pm.test("Ticket resolved successfully (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("NewStatus is Resolved", function () {
    var res = pm.response.json();
    pm.expect(res.newStatus).to.eql("Resolved");
});
"""
    ),
    make_req(
        "10 - ADM-04: Admin Notifications Templates",
        "POST",
        "api/v1/admin/notifications/templates",
        token_var="admin_accessToken",
        body={
            "reason": "Audit notification templates for V1 acceptance"
        },
        test_script="""
pm.test("Admin notification templates returned (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Templates list returned", function () {
    var templates = pm.response.json();
    pm.expect(templates).to.be.an("array");
});
"""
    ),
    make_req(
        "11 - ADM-05: Maintenance Status",
        "GET",
        "api/v1/system/maintenance",
        token_var="admin_accessToken",
        test_script="""
pm.test("System maintenance status returned (200 OK)", function () {
    pm.response.to.have.status(200);
});
"""
    ),
    make_req(
        "12 - Security Check: Non-Admin Access Prohibited (Assert 403 Forbidden)",
        "GET",
        "api/v1/admin/overview",
        token_var="userA_accessToken",
        test_script="""
pm.test("Non-admin user receives 403 Forbidden", function () {
    pm.response.to.have.status(403);
});
"""
    )
]

collection["item"].append({
    "name": "08-Admin 5 Areas",
    "item": admin_items
})

# -------------------------------------------------------------
# 09-Integrations
# -------------------------------------------------------------
integration_items = [
    make_req(
        "01 - Bank Connections Status",
        "GET",
        "api/bank-connections",
        token_var="userA_accessToken",
        test_script="""
pm.test("Bank connections status returned (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Reports bank connections list", function () {
    var connections = pm.response.json();
    pm.expect(connections).to.be.an("array");
});
"""
    ),
    make_req(
        "02 - SePay TestMode: Start Link Challenge",
        "POST",
        "api/bank-connections/test-mode/link-challenges",
        token_var="userA_accessToken",
        test_script="""
pm.test("Bank link challenge created (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Captures challengeId", function () {
    var res = pm.response.json();
    pm.expect(res.challengeId).to.be.a("string");
    pm.environment.set("bankChallengeId", res.challengeId);
});
"""
    ),
    make_req(
        "03 - SePay TestMode: Verify Challenge Rejection When Unconfirmed",
        "POST",
        "api/bank-connections/test-mode/link-challenges/{{bankChallengeId}}/confirm",
        token_var="userA_accessToken",
        test_script="""
pm.test("Unconfirmed challenge rejected (409 Conflict)", function () {
    pm.response.to.have.status(409);
});
pm.test("Returns bank_link_not_confirmed", function () {
    var res = pm.response.json();
    pm.expect(res.code).to.eql("bank_link_not_confirmed");
});
"""
    ),
    make_req(
        "04 - SePay TestMode: List Linked Accounts",
        "GET",
        "api/bank-connections/test-mode/linked-accounts",
        token_var="userA_accessToken",
        test_script="""
pm.test("Linked accounts retrieved (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Returns array of linked accounts", function () {
    var list = pm.response.json();
    pm.expect(list).to.be.an("array");
});
"""
    ),
    make_req(
        "05 - SePay TestMode: Top-Up Without Active Connection Rejected",
        "POST",
        "api/bank-connections/test-mode/wallet-top-ups",
        token_var="userA_accessToken",
        body={
            "bankConnectionId": "00000000-0000-0000-0000-000000000001",
            "walletId": "{{userA_mainWalletId}}",
            "amount": 500000
        },
        test_script="""
pm.test("Unlinked connection top-up rejected (404 Not Found)", function () {
    pm.response.to.have.status(404);
});
pm.test("Returns bank_connection_not_found", function () {
    var res = pm.response.json();
    pm.expect(res.code).to.eql("bank_connection_not_found");
});
"""
    ),
    make_req(
        "06 - SePay TestMode: List Wallet Top-Ups",
        "GET",
        "api/bank-connections/test-mode/wallet-top-ups",
        token_var="userA_accessToken",
        test_script="""
pm.test("Wallet top-ups retrieved (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Returns array of top-ups", function () {
    var list = pm.response.json();
    pm.expect(list).to.be.an("array");
});
"""
    ),
    make_req(
        "07 - AI Chat Financial Query (Gemini)",
        "POST",
        "api/ai/chat",
        token_var="userA_accessToken",
        body={
            "message": "Làm thế nào để quản lý chi tiêu hiệu quả?",
            "mode": "FinancialQuery"
        },
        test_script="""
pm.test("Unconfigured AI unavailable (503)", function () {
    pm.response.to.have.status(503);
});
pm.test("Returns explicit unavailable code", function () {
    pm.expect(pm.response.json().code).to.eql("AI_PROVIDER_UNAVAILABLE");
});
"""
    )
]

for item in integration_items[1:6]:
    for event in item.get("event", []):
        if event["listen"] == "test":
            original = "\n".join(event["script"]["exec"])
            event["script"]["exec"] = [
                'if (pm.environment.get("test_mode_enabled") === "true") {', original,
                '} else {',
                'pm.test("TestMode disabled outside Development", () => pm.response.to.have.status(404));',
                'pm.test("Explicit disabled code", () => pm.expect(pm.response.json().code).to.eql("sepay_test_mode_unavailable"));',
                'pm.environment.set("bankChallengeId", "00000000-0000-0000-0000-000000000001");',
                '}'
            ]

collection["item"].append({
    "name": "09-Integrations",
    "item": integration_items
})

# Task 17: real HTTP workflows; matured activity/election fixtures are supplied by the owned SQL02 runner.
core = []
def core_req(name, method, path, token="core_owner_token", body=None, status=200, checks="", capture=None):
    tests = f'pm.test("{name}: status", () => pm.response.to.have.status({status}));'
    if capture:
        tests += f'\npm.environment.set("{capture}", pm.response.json().id);'
    if checks:
        tests += '\npm.test("' + name + ': state", () => { ' + checks + ' });'
    core.append(make_req(name, method, path, body=body, token_var=token, test_script=tests))

for theme, lang in [("light", "vi-VN"), ("dark", "en-US"), ("system", "vi-VN")]:
    core_req("Preferences " + theme, "PUT", "api/users/me/preferences", "userA_accessToken", {"preferredTheme": theme, "preferredLanguage": lang, "defaultCurrency": "VND"}, checks=f'pm.expect(pm.response.json().preferredTheme).eql("{theme}"); pm.expect(pm.response.json().preferredLanguage).eql("{lang}");')
core_req("Other account preferences", "GET", "api/users/me", "userB_accessToken", checks='pm.expect(pm.response.json().preferredTheme).eql("system");')
core_req("Original account preferences retained", "GET", "api/users/me", "userA_accessToken", checks='pm.expect(pm.response.json().preferredTheme).eql("system"); pm.expect(pm.response.json().preferredLanguage).eql("vi-VN");')
core_req("Election freezes finance", "POST", "api/v1/funds/{{core_election_fund}}/contributions", "core_member_token", {"walletId":"{{core_member_wallet}}", "amountVnd":1, "idempotencyKey":"newman-election-frozen", "securityPin":"{{default_pin}}"}, 409)
core_req("Election no-votes failure", "POST", "api/v1/funds/elections/{{core_election}}/advance", "core_member_token", checks='pm.expect(pm.response.json().phase).eql("Failed"); pm.expect(pm.response.json().failureReason).eql("NoVotes");')
core_req("Owner settles approved activity", "POST", "api/v1/funds/activity-proposals/{{core_proposal}}/settle", body={"securityPin":"{{default_pin}}"}, checks='pm.expect(pm.response.json().state).eql("Settled");')
core_req("Settlement replay", "POST", "api/v1/funds/activity-proposals/{{core_proposal}}/settle", body={"securityPin":"{{default_pin}}"}, checks='pm.expect(pm.response.json().state).eql("Settled");')
core_req("Carry-over finance", "GET", "api/v1/funds/{{core_fund}}/finance", checks='var f=pm.response.json(); pm.expect(f.balanceVnd).eql(1000000); pm.expect(f.obligations.map(o=>o.paidVnd)).eql([500000,500000]);')
core_req("Support intake", "POST", "api/cases/support", "core_member_token", {"category":"General", "priority":"Normal", "subject":"Synthetic Newman support", "description":"Synthetic account investigation"}, capture="core_case")
core_req("Support claim", "POST", "api/v1/admin/support/{{core_case}}/assign", "core_admin_token", {"operatorId":"{{core_admin}}", "reason":"Synthetic claim"})
core_req("Support request information", "POST", "api/v1/admin/support/{{core_case}}/request-info", "core_admin_token", {"reason":"Synthetic request", "message":"Provide synthetic receipt"})
core_req("Support user reply", "POST", "api/cases/support/{{core_case}}/messages", "core_member_token", {"message":"Synthetic receipt supplied"})
status_path="api/v1/admin/support/{{core_case}}/users/{{core_member}}/status"
status_body={"status":"Restricted", "reason":"Synthetic verified report", "securityPin":"{{default_pin}}"}
core_req("User cannot restrict", "PUT", status_path, "core_owner_token", status_body, 403)
core_req("Admin restrict", "PUT", status_path, "core_admin_token", status_body, 204)
p2p={"sourceWalletId":"{{core_owner_wallet}}", "recipientUserId":"{{core_member}}", "amount":1000, "description":"Synthetic restricted inbound", "idempotencyKey":"newman-inbound", "securityPin":"{{default_pin}}"}
core_req("Restricted inbound P2P", "POST", "api/transfers/p2p", body=p2p, status=201)
core_req("Restricted outbound denied", "POST", "api/transfers/p2p", "core_member_token", {**p2p,"sourceWalletId":"{{core_member_wallet}}", "recipientUserId":"{{core_owner}}", "idempotencyKey":"newman-outbound-denied"}, 403)
core_req("Restricted refund preview", "GET", "api/v1/funds/{{core_fund}}/refund-preview", "core_member_token", checks='pm.expect(pm.response.json().eligibleRefundAmountVnd).eql(500000);')
for label in ["Refund", "Refund replay"]:
    core_req(label, "POST", "api/v1/funds/{{core_fund}}/leave", "core_member_token", {"securityPin":"{{default_pin}}", "idempotencyKey":"newman-refund", "destinationWalletId":"{{core_member_wallet}}"}, checks='pm.expect(pm.response.json().refundAmountVnd).eql(500000);')
core_req("Admin lock", "PUT", status_path, "core_admin_token", {**status_body,"status":"Locked"}, 204)
core_req("Locked token denied", "GET", "api/users/me", "core_member_token", status=401)
core_req("Admin unlock", "PUT", status_path, "core_admin_token", {**status_body,"status":"Active"}, 204)
core_req("Unlocked old token still denied", "GET", "api/users/me", "core_member_token", status=401)
core_req("Support resolve", "POST", "api/v1/admin/support/{{core_case}}/resolve", "core_admin_token", {"reason":"Synthetic reviewed evidence", "resolutionMessage":"Synthetic resolved", "isRejected":False})
broadcast={"type":"Operational", "targetRole":"User", "title":"Synthetic Newman notice", "message":"Synthetic operation notice", "idempotencyKey":"newman-broadcast", "securityPin":"{{default_pin}}", "reason":"Synthetic operations"}
core_req("User broadcast denied", "POST", "api/v1/system/broadcasts", "core_owner_token", broadcast, 403)
core_req("Broadcast draft", "POST", "api/v1/system/broadcasts", "core_admin_token", broadcast, capture="core_broadcast")
core_req("Broadcast publish", "POST", "api/v1/system/broadcasts/{{core_broadcast}}/publish", "core_admin_token", {"scheduledFor":None,"expectedRevision":1,"securityPin":"{{default_pin}}","reason":"Synthetic publish"}, 204)
core_req("Broadcast status", "POST", "api/v1/system/broadcasts/{{core_broadcast}}/status", "core_admin_token", {"reason":"Synthetic check"}, checks='pm.expect(pm.response.json().state).eql("Scheduled");')
core_req("Broadcast cancel draft", "POST", "api/v1/system/broadcasts", "core_admin_token", {**broadcast,"idempotencyKey":"newman-cancel"}, capture="core_cancel_broadcast")
core_req("Broadcast cancel", "POST", "api/v1/system/broadcasts/{{core_cancel_broadcast}}/cancel", "core_admin_token", {"securityPin":"{{default_pin}}","reason":"Synthetic cancel"}, 204)
core_req("Emergency maintenance", "POST", "api/v1/system/maintenance/emergency", "core_admin_token", {"expectedEndsAt":"{{core_maintenance_end}}", "scope":"Money", "securityPin":"{{default_pin}}", "reason":"Synthetic incident", "impactConfirmation":"CONFIRM_MAINTENANCE_IMPACT", "idempotencyKey":"newman-emergency"}, capture="core_maintenance")
core_req("Maintenance gates money", "GET", "api/wallets", "core_owner_token", status=503)
core_req("Maintenance safe reopen", "POST", "api/v1/system/maintenance/{{core_maintenance}}/close", "core_admin_token", {"securityPin":"{{default_pin}}","reason":"Synthetic reconciliation complete"}, 204)
core_req("Money restored", "GET", "api/wallets", checks='pm.expect(pm.response.json().find(w=>w.id===pm.environment.get("core_member_wallet"))).eql(undefined);')
collection["item"].append({"name":"10-Core Closure and Preferences", "item":core})

with open("backend/postman/SpendWise-V1-Full-E2E.postman_collection.json", "w", encoding="utf-8") as f:
    json.dump(collection, f, indent=2, ensure_ascii=False)

print("Final E2E Collection generated successfully!")
