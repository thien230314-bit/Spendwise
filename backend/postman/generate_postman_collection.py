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
        "07 - Register User B",
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
        "08 - Verify Registration Email User B",
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
        "09 - Setup PIN User B",
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
        "receipt.png",
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
        "receipt.png",
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
        "12 - P2P Transfer Wrong PIN (Assert Unauthorized / Bad Request)",
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
        "01 - Create Fixed Fund (User A)",
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
        "07 - User A Sets Spending Permission ON for User B",
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
        "08 - Get Fixed Fund Finance Summary",
        "GET",
        "api/v1/funds/{{fixedFundId}}/finance",
        token_var="userA_accessToken",
        test_script="""
pm.test("Status code is 200", function () {
    pm.response.to.have.status(200);
});
pm.test("Treasury balance is 2M VND", function () {
    var f = pm.response.json();
    pm.expect(f.balanceVnd).to.eql(2000000);
});
"""
    ),
    make_req(
        "09 - User B Previews Refund (Case A Unspent 100%)",
        "GET",
        "api/v1/funds/{{fixedFundId}}/refund-preview",
        token_var="userB_accessToken",
        test_script="""
pm.test("Status code is 200", function () {
    pm.response.to.have.status(200);
});
pm.test("Eligible refund amount is 100% of contribution (1M VND)", function () {
    var p = pm.response.json();
    pm.expect(p.eligibleRefundAmountVnd).to.eql(1000000);
    pm.expect(p.caseType).to.eql("CaseA_Unspent");
});
"""
    ),
    make_req(
        "10 - User B Leaves Fixed Fund (Refund Case A)",
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
pm.test("1M VND refunded to internal wallet", function () {
    var res = pm.response.json();
    pm.expect(res.refundAmountVnd).to.eql(1000000);
    pm.expect(res.status).to.eql("Completed");
    pm.expect(res.caseType).to.eql("CaseA_Unspent");
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
        "07 - User A Reviews Withdrawal (Verifies Guard)",
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
pm.test("Asserts ApprovedAwaitingFinanceContract state", function () {
    var res = pm.response.json();
    pm.expect(res.state).to.eql("ApprovedAwaitingFinanceContract");
});
"""
    ),
    make_req(
        "08 - User A Previews Dissolution Refund",
        "GET",
        "api/v1/funds/{{freeFundId}}/refund-preview?memberUserId={{userA_id}}",
        token_var="userA_accessToken",
        test_script="""
pm.test("Status code is 200", function () {
    pm.response.to.have.status(200);
});
pm.test("Preview calculates proportional share", function () {
    var p = pm.response.json();
    pm.expect(p.eligibleRefundAmountVnd).to.be.above(0);
});
"""
    ),
    make_req(
        "09 - User A Dissolves Free Fund (Case C/D Zero Balance Invariant)",
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
        "05 - ADM-03: Admin Support Cases",
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
        "06 - ADM-04: Admin Notifications Templates",
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
        "07 - ADM-05: Maintenance Status",
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
        "08 - Security Check: Non-Admin Access Prohibited (Assert 403 Forbidden)",
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
        "02 - AI Chat Financial Query",
        "POST",
        "api/ai/chat",
        token_var="userA_accessToken",
        body={
            "message": "Làm thế nào để quản lý chi tiêu hiệu quả?",
            "mode": "FinancialQuery"
        },
        test_script="""
pm.test("AI chat responds (200 OK)", function () {
    pm.response.to.have.status(200);
});
pm.test("Returns non-empty response", function () {
    var res = pm.response.json();
    pm.expect(res.message).to.be.a("string");
    pm.expect(res.message.length).to.be.above(0);
});
"""
    )
]

collection["item"].append({
    "name": "09-Integrations",
    "item": integration_items
})

with open("backend/postman/SpendWise-V1-Full-E2E.postman_collection.json", "w", encoding="utf-8") as f:
    json.dump(collection, f, indent=2, ensure_ascii=False)

print("Final E2E Collection generated successfully!")
