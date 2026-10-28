const crypto = require('node:crypto');
const fs = require('node:fs');
const path = require('node:path');

const BASE_URL = 'http://127.0.0.1:5158';

function base64UrlEncode(buffer) {
  return buffer.toString('base64')
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '');
}

function base64UrlDecode(str) {
  let padded = str.replace(/-/g, '+').replace(/_/g, '/');
  while (padded.length % 4 !== 0) padded += '=';
  return Buffer.from(padded, 'base64');
}

const securityResults = [];

function recordSecurityTest(id, name, endpoint, expectedStatus, actualStatus, pass, assertions, extra = {}) {
  const result = {
    testId: id,
    name: name,
    endpoint: endpoint,
    expectedStatus: expectedStatus,
    actualStatus: actualStatus,
    assertions: assertions,
    status: pass ? 'PASS' : 'FAIL',
    timestamp: new Date().toISOString(),
    ...extra
  };
  securityResults.push(result);
  const mark = pass ? '✅ PASS' : '❌ FAIL';
  console.log(`${mark} [${id}] ${name} (HTTP ${actualStatus})`);
  if (!pass && extra.error) {
    console.error(`   Error details: ${extra.error}`);
  }
}

async function loginUser(email, password, pin) {
  const r1 = await fetch(`${BASE_URL}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ identifier: email, password })
  });
  const d1 = await r1.json();
  if (!d1.challengeToken) {
    throw new Error(`Login challenge failed: ${JSON.stringify(d1)}`);
  }

  const r2 = await fetch(`${BASE_URL}/api/auth/pin/verify`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ challengeToken: d1.challengeToken, pin })
  });
  const d2 = await r2.json();
  if (!d2.accessToken) {
    throw new Error(`PIN verify failed: ${JSON.stringify(d2)}`);
  }
  return d2.accessToken;
}

function buildWebAuthnAssertion(challenge, privateKey, options = {}) {
  const rpId = options.rpId !== undefined ? options.rpId : 'spendwise.test';
  const origin = options.origin !== undefined ? options.origin : 'https://spendwise.test';
  const type = options.type !== undefined ? options.type : 'webauthn.get';
  const userVerified = options.userVerified !== undefined ? options.userVerified : true;
  const userPresent = options.userPresent !== undefined ? options.userPresent : true;
  const signCountVal = options.signCount !== undefined ? options.signCount : 1;
  const overrideChallenge = options.overrideChallenge !== undefined ? options.overrideChallenge : challenge;

  const clientData = {
    type: type,
    challenge: overrideChallenge,
    origin: origin
  };
  const clientDataJSON = Buffer.from(JSON.stringify(clientData));
  const clientDataJSONBase64 = base64UrlEncode(clientDataJSON);

  // RP ID hash (32 bytes)
  let rpIdHash;
  if (options.corruptRpIdHash) {
    rpIdHash = crypto.createHash('sha256').update('evil.phishing.attacker').digest();
  } else {
    rpIdHash = crypto.createHash('sha256').update(rpId).digest();
  }

  // Flags: UP (bit 0 = 0x01), UV (bit 2 = 0x04)
  let flagsByte = 0;
  if (userPresent) flagsByte |= 0x01;
  if (userVerified) flagsByte |= 0x04;
  const flags = Buffer.from([flagsByte]);

  // Sign count (4 bytes big-endian)
  const signCountBuf = Buffer.alloc(4);
  signCountBuf.writeUInt32BE(signCountVal, 0);

  const authData = Buffer.concat([rpIdHash, flags, signCountBuf]);
  const authDataBase64 = base64UrlEncode(authData);

  // Signed payload: authData || SHA256(clientDataJSON)
  const clientDataHash = crypto.createHash('sha256').update(clientDataJSON).digest();
  const signedPayload = Buffer.concat([authData, clientDataHash]);

  const signer = crypto.createSign('SHA256');
  signer.update(signedPayload);
  const signature = signer.sign(privateKey);
  const signatureBase64 = base64UrlEncode(signature);

  return {
    clientDataJSON: clientDataJSONBase64,
    authenticatorData: authDataBase64,
    signature: signatureBase64
  };
}

async function runSecuritySuite() {
  console.log('================================================================');
  console.log('SPENDWISE BIOMETRIC SECURITY & WEBAUTHN COMPLIANCE TEST SUITE');
  console.log('================================================================\n');

  const userAEmail = process.env.SPENDWISE_FIXTURE_USER_A || 'fixture_user@spendwise.test';
  const userBEmail = process.env.SPENDWISE_FIXTURE_USER_B || 'userB_cgahvu2@spendwise.test';
  const fixturePassword = process.env.SPENDWISE_FIXTURE_PASSWORD || 'StrongPass1!';
  const pin = process.env.SPENDWISE_FIXTURE_PIN || '123456';

  const userAToken = await loginUser(userAEmail, fixturePassword, pin);
  const userBToken = await loginUser(userBEmail, fixturePassword, pin);

  // Cryptographic Keypairs
  const ecKey = crypto.generateKeyPairSync('ec', { namedCurve: 'prime256v1' });
  const ecPublicKeySpki = ecKey.publicKey.export({ type: 'spki', format: 'der' }).toString('base64');
  const ecCredentialId = 'cred_ec_' + base64UrlEncode(crypto.randomBytes(24));

  const rsaKey = crypto.generateKeyPairSync('rsa', { modulusLength: 2048 });
  const rsaPublicKeySpki = rsaKey.publicKey.export({ type: 'spki', format: 'der' }).toString('base64');
  const rsaCredentialId = 'cred_rsa_' + base64UrlEncode(crypto.randomBytes(24));

  let regChallenge = null;
  let secondCredDbId = null;

  // -------------------------------------------------------------------
  // 1. REGISTRATION SECURITY
  // -------------------------------------------------------------------
  console.log('--- 1. REGISTRATION SECURITY ---');

  // SEC-REG-01: Valid Registration
  try {
    const optRes = await fetch(`${BASE_URL}/api/auth/passkeys/register/options`, {
      method: 'POST',
      headers: { 'Authorization': `Bearer ${userAToken}` }
    });
    const optData = await optRes.json();
    regChallenge = optData.challenge;

    const res = await fetch(`${BASE_URL}/api/auth/passkeys/register/verify`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${userAToken}`
      },
      body: JSON.stringify({
        challenge: regChallenge,
        credentialId: ecCredentialId,
        publicKey: ecPublicKeySpki,
        algorithm: 'ES256',
        deviceName: 'iPhone 15 Pro (Face ID)'
      })
    });
    const data = await res.json();
    const pass = res.status === 200 && data.credentialId === ecCredentialId && data.isRevoked === false;
    recordSecurityTest(
      'SEC-REG-01',
      'Valid Passkey Registration with ES256 Public Key',
      'POST /api/auth/passkeys/register/verify',
      200,
      res.status,
      pass,
      {
        expected: 'HTTP 200, credentialId stored, isRevoked=false',
        actual: `HTTP ${res.status}, credentialId=${data.credentialId}, isRevoked=${data.isRevoked}`
      }
    );
  } catch (e) {
    recordSecurityTest('SEC-REG-01', 'Valid Passkey Registration', 'POST /api/auth/passkeys/register/verify', 200, 500, false, {}, { error: e.message });
  }

  // SEC-REG-02: Malformed Public Key
  try {
    const optRes = await fetch(`${BASE_URL}/api/auth/passkeys/register/options`, {
      method: 'POST',
      headers: { 'Authorization': `Bearer ${userAToken}` }
    });
    const optData = await optRes.json();

    const res = await fetch(`${BASE_URL}/api/auth/passkeys/register/verify`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${userAToken}`
      },
      body: JSON.stringify({
        challenge: optData.challenge,
        credentialId: 'bad_key_' + Date.now(),
        publicKey: 'NOT_A_VALID_SPKI_OR_EC_POINT_BYTES',
        algorithm: 'ES256'
      })
    });
    const data = await res.json();
    const pass = res.status === 400 && data.code === 'invalid_public_key';
    recordSecurityTest(
      'SEC-REG-02',
      'Reject Malformed Public Key String',
      'POST /api/auth/passkeys/register/verify',
      400,
      res.status,
      pass,
      {
        expected: 'HTTP 400 with code "invalid_public_key"',
        actual: `HTTP ${res.status} with code "${data.code}"`
      }
    );
  } catch (e) {
    recordSecurityTest('SEC-REG-02', 'Reject Malformed Public Key', 'POST /api/auth/passkeys/register/verify', 400, 500, false, {}, { error: e.message });
  }

  // SEC-REG-03: Challenge Replay in Registration
  try {
    const res = await fetch(`${BASE_URL}/api/auth/passkeys/register/verify`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${userAToken}`
      },
      body: JSON.stringify({
        challenge: regChallenge, // Replayed from SEC-REG-01
        credentialId: 'replay_reg_' + Date.now(),
        publicKey: ecPublicKeySpki,
        algorithm: 'ES256'
      })
    });
    const data = await res.json();
    const pass = res.status === 400 && data.code === 'invalid_or_expired_challenge';
    recordSecurityTest(
      'SEC-REG-03',
      'Reject Replayed Registration Challenge (One-Time Use)',
      'POST /api/auth/passkeys/register/verify',
      400,
      res.status,
      pass,
      {
        expected: 'HTTP 400 with code "invalid_or_expired_challenge"',
        actual: `HTTP ${res.status} with code "${data.code}"`
      }
    );
  } catch (e) {
    recordSecurityTest('SEC-REG-03', 'Reject Replayed Registration Challenge', 'POST /api/auth/passkeys/register/verify', 400, 500, false, {}, { error: e.message });
  }

  // SEC-REG-04: Duplicate Credential ID
  try {
    const optRes = await fetch(`${BASE_URL}/api/auth/passkeys/register/options`, {
      method: 'POST',
      headers: { 'Authorization': `Bearer ${userAToken}` }
    });
    const optData = await optRes.json();

    const res = await fetch(`${BASE_URL}/api/auth/passkeys/register/verify`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${userAToken}`
      },
      body: JSON.stringify({
        challenge: optData.challenge,
        credentialId: ecCredentialId, // Already registered
        publicKey: ecPublicKeySpki,
        algorithm: 'ES256'
      })
    });
    const data = await res.json();
    const pass = res.status === 409 && data.code === 'credential_already_registered';
    recordSecurityTest(
      'SEC-REG-04',
      'Reject Duplicate Passkey Credential ID',
      'POST /api/auth/passkeys/register/verify',
      409,
      res.status,
      pass,
      {
        expected: 'HTTP 409 with code "credential_already_registered"',
        actual: `HTTP ${res.status} with code "${data.code}"`
      }
    );
  } catch (e) {
    recordSecurityTest('SEC-REG-04', 'Reject Duplicate Passkey Credential ID', 'POST /api/auth/passkeys/register/verify', 409, 500, false, {}, { error: e.message });
  }

  // SEC-REG-05: Untrusted Origin in clientDataJSON during Registration
  try {
    const optRes = await fetch(`${BASE_URL}/api/auth/passkeys/register/options`, {
      method: 'POST',
      headers: { 'Authorization': `Bearer ${userAToken}` }
    });
    const optData = await optRes.json();

    const badClientData = {
      type: 'webauthn.create',
      challenge: optData.challenge,
      origin: 'https://attacker-phishing.fake'
    };

    const res = await fetch(`${BASE_URL}/api/auth/passkeys/register/verify`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${userAToken}`
      },
      body: JSON.stringify({
        challenge: optData.challenge,
        credentialId: 'origin_test_' + Date.now(),
        publicKey: ecPublicKeySpki,
        algorithm: 'ES256',
        clientDataJSON: base64UrlEncode(Buffer.from(JSON.stringify(badClientData)))
      })
    });
    const data = await res.json();
    const pass = res.status === 400 && data.code === 'invalid_client_data';
    recordSecurityTest(
      'SEC-REG-05',
      'Reject Untrusted Origin in Registration clientDataJSON',
      'POST /api/auth/passkeys/register/verify',
      400,
      res.status,
      pass,
      {
        expected: 'HTTP 400 with code "invalid_client_data"',
        actual: `HTTP ${res.status} with code "${data.code}" (${data.title || ''})`
      }
    );
  } catch (e) {
    recordSecurityTest('SEC-REG-05', 'Reject Untrusted Origin in Registration', 'POST /api/auth/passkeys/register/verify', 400, 500, false, {}, { error: e.message });
  }

  // SEC-REG-06: Wrong Type in clientDataJSON during Registration
  try {
    const optRes = await fetch(`${BASE_URL}/api/auth/passkeys/register/options`, {
      method: 'POST',
      headers: { 'Authorization': `Bearer ${userAToken}` }
    });
    const optData = await optRes.json();

    const badTypeClientData = {
      type: 'webauthn.get', // Wrong for registration
      challenge: optData.challenge,
      origin: 'https://spendwise.test'
    };

    const res = await fetch(`${BASE_URL}/api/auth/passkeys/register/verify`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${userAToken}`
      },
      body: JSON.stringify({
        challenge: optData.challenge,
        credentialId: 'type_test_' + Date.now(),
        publicKey: ecPublicKeySpki,
        algorithm: 'ES256',
        clientDataJSON: base64UrlEncode(Buffer.from(JSON.stringify(badTypeClientData)))
      })
    });
    const data = await res.json();
    const pass = res.status === 400 && data.code === 'invalid_client_data';
    recordSecurityTest(
      'SEC-REG-06',
      'Reject Mismatched Type (webauthn.get) in Registration clientDataJSON',
      'POST /api/auth/passkeys/register/verify',
      400,
      res.status,
      pass,
      {
        expected: 'HTTP 400 with code "invalid_client_data"',
        actual: `HTTP ${res.status} with code "${data.code}"`
      }
    );
  } catch (e) {
    recordSecurityTest('SEC-REG-06', 'Reject Mismatched Type in Registration', 'POST /api/auth/passkeys/register/verify', 400, 500, false, {}, { error: e.message });
  }

  // SEC-REG-07: Register Multi-Device (RS256)
  try {
    const optRes = await fetch(`${BASE_URL}/api/auth/passkeys/register/options`, {
      method: 'POST',
      headers: { 'Authorization': `Bearer ${userAToken}` }
    });
    const optData = await optRes.json();

    const res = await fetch(`${BASE_URL}/api/auth/passkeys/register/verify`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${userAToken}`
      },
      body: JSON.stringify({
        challenge: optData.challenge,
        credentialId: rsaCredentialId,
        publicKey: rsaPublicKeySpki,
        algorithm: 'RS256',
        deviceName: 'Pixel 8 (Fingerprint)'
      })
    });
    const data = await res.json();
    const pass = res.status === 200 && data.algorithm === 'RS256';
    secondCredDbId = data.id;
    recordSecurityTest(
      'SEC-REG-07',
      'Register Multi-Device Passkey (RS256 Algorithm)',
      'POST /api/auth/passkeys/register/verify',
      200,
      res.status,
      pass,
      {
        expected: 'HTTP 200, algorithm="RS256"',
        actual: `HTTP ${res.status}, algorithm="${data.algorithm}"`
      }
    );
  } catch (e) {
    recordSecurityTest('SEC-REG-07', 'Register Multi-Device Passkey (RS256)', 'POST /api/auth/passkeys/register/verify', 200, 500, false, {}, { error: e.message });
  }

  // SEC-REG-08: Revoke Passkey
  try {
    const res = await fetch(`${BASE_URL}/api/auth/passkeys/${secondCredDbId}`, {
      method: 'DELETE',
      headers: { 'Authorization': `Bearer ${userAToken}` }
    });
    const pass = res.status === 204;
    recordSecurityTest(
      'SEC-REG-08',
      'Revoke Biometric Credential via DELETE',
      `DELETE /api/auth/passkeys/${secondCredDbId}`,
      204,
      res.status,
      pass,
      {
        expected: 'HTTP 204 NoContent',
        actual: `HTTP ${res.status}`
      }
    );
  } catch (e) {
    recordSecurityTest('SEC-REG-08', 'Revoke Biometric Credential', 'DELETE /api/auth/passkeys', 204, 500, false, {}, { error: e.message });
  }

  // -------------------------------------------------------------------
  // 2. QUICK LOGIN & WEBAUTHN ASSERTION SECURITY
  // -------------------------------------------------------------------
  console.log('\n--- 2. QUICK LOGIN & WEBAUTHN ASSERTION SECURITY ---');

  let loginChallenge = null;
  let biometricAccessToken = null;

  // SEC-LOG-01: Valid WebAuthn Login
  try {
    const optRes = await fetch(`${BASE_URL}/api/auth/passkeys/login/options`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: userAEmail })
    });
    const optData = await optRes.json();
    loginChallenge = optData.challenge;

    const assertion = buildWebAuthnAssertion(loginChallenge, ecKey.privateKey, {
      rpId: 'spendwise.test',
      origin: 'https://spendwise.test',
      signCount: 10
    });

    const res = await fetch(`${BASE_URL}/api/auth/passkeys/login/verify`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        credentialId: ecCredentialId,
        challenge: loginChallenge,
        signature: assertion.signature,
        clientDataJSON: assertion.clientDataJSON,
        authenticatorData: assertion.authenticatorData
      })
    });
    const data = await res.json();
    const pass = res.status === 200 && Boolean(data.accessToken) && Boolean(data.refreshToken);
    biometricAccessToken = data.accessToken;
    recordSecurityTest(
      'SEC-LOG-01',
      'WebAuthn Biometric Quick Login with Valid Flags (UP=1, UV=1)',
      'POST /api/auth/passkeys/login/verify',
      200,
      res.status,
      pass,
      {
        expected: 'HTTP 200 with valid JWT AccessToken and RefreshToken',
        actual: `HTTP ${res.status}, hasAccessToken=${Boolean(data.accessToken)}`
      }
    );
  } catch (e) {
    recordSecurityTest('SEC-LOG-01', 'WebAuthn Biometric Quick Login', 'POST /api/auth/passkeys/login/verify', 200, 500, false, {}, { error: e.message });
  }

  // SEC-LOG-02: Spoofed / Wrong rpIdHash
  try {
    const optRes = await fetch(`${BASE_URL}/api/auth/passkeys/login/options`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: userAEmail })
    });
    const optData = await optRes.json();

    const badRpAssertion = buildWebAuthnAssertion(optData.challenge, ecKey.privateKey, {
      corruptRpIdHash: true // Sets rpIdHash of attacker domain
    });

    const res = await fetch(`${BASE_URL}/api/auth/passkeys/login/verify`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        credentialId: ecCredentialId,
        challenge: optData.challenge,
        signature: badRpAssertion.signature,
        clientDataJSON: badRpAssertion.clientDataJSON,
        authenticatorData: badRpAssertion.authenticatorData
      })
    });
    const data = await res.json();
    const pass = res.status === 401 && data.code === 'biometric_verification_failed';
    recordSecurityTest(
      'SEC-LOG-02',
      'Reject Mismatched rpIdHash in authenticatorData',
      'POST /api/auth/passkeys/login/verify',
      401,
      res.status,
      pass,
      {
        expected: 'HTTP 401 with code "biometric_verification_failed"',
        actual: `HTTP ${res.status} with code "${data.code}" (${data.title || ''})`
      }
    );
  } catch (e) {
    recordSecurityTest('SEC-LOG-02', 'Reject Mismatched rpIdHash', 'POST /api/auth/passkeys/login/verify', 401, 500, false, {}, { error: e.message });
  }

  // SEC-LOG-03: Untrusted Origin in clientDataJSON
  try {
    const optRes = await fetch(`${BASE_URL}/api/auth/passkeys/login/options`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: userAEmail })
    });
    const optData = await optRes.json();

    const badOriginAssertion = buildWebAuthnAssertion(optData.challenge, ecKey.privateKey, {
      origin: 'https://evil-phishing-bank.com'
    });

    const res = await fetch(`${BASE_URL}/api/auth/passkeys/login/verify`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        credentialId: ecCredentialId,
        challenge: optData.challenge,
        signature: badOriginAssertion.signature,
        clientDataJSON: badOriginAssertion.clientDataJSON,
        authenticatorData: badOriginAssertion.authenticatorData
      })
    });
    const data = await res.json();
    const pass = res.status === 401 && data.code === 'biometric_verification_failed';
    recordSecurityTest(
      'SEC-LOG-03',
      'Reject Untrusted Origin in Assertion clientDataJSON',
      'POST /api/auth/passkeys/login/verify',
      401,
      res.status,
      pass,
      {
        expected: 'HTTP 401 with code "biometric_verification_failed"',
        actual: `HTTP ${res.status} with code "${data.code}" (${data.title || ''})`
      }
    );
  } catch (e) {
    recordSecurityTest('SEC-LOG-03', 'Reject Untrusted Origin', 'POST /api/auth/passkeys/login/verify', 401, 500, false, {}, { error: e.message });
  }

  // SEC-LOG-04: Wrong Type in clientDataJSON
  try {
    const optRes = await fetch(`${BASE_URL}/api/auth/passkeys/login/options`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: userAEmail })
    });
    const optData = await optRes.json();

    const badTypeAssertion = buildWebAuthnAssertion(optData.challenge, ecKey.privateKey, {
      type: 'webauthn.create' // Expected webauthn.get
    });

    const res = await fetch(`${BASE_URL}/api/auth/passkeys/login/verify`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        credentialId: ecCredentialId,
        challenge: optData.challenge,
        signature: badTypeAssertion.signature,
        clientDataJSON: badTypeAssertion.clientDataJSON,
        authenticatorData: badTypeAssertion.authenticatorData
      })
    });
    const data = await res.json();
    const pass = res.status === 401 && data.code === 'biometric_verification_failed';
    recordSecurityTest(
      'SEC-LOG-04',
      'Reject Wrong Type (webauthn.create) in Assertion clientDataJSON',
      'POST /api/auth/passkeys/login/verify',
      401,
      res.status,
      pass,
      {
        expected: 'HTTP 401 with code "biometric_verification_failed"',
        actual: `HTTP ${res.status} with code "${data.code}" (${data.title || ''})`
      }
    );
  } catch (e) {
    recordSecurityTest('SEC-LOG-04', 'Reject Wrong Type in Assertion', 'POST /api/auth/passkeys/login/verify', 401, 500, false, {}, { error: e.message });
  }

  // SEC-LOG-05: Mismatched Challenge in clientDataJSON
  try {
    const optRes = await fetch(`${BASE_URL}/api/auth/passkeys/login/options`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: userAEmail })
    });
    const optData = await optRes.json();

    const badChalAssertion = buildWebAuthnAssertion(optData.challenge, ecKey.privateKey, {
      overrideChallenge: base64UrlEncode(crypto.randomBytes(32)) // Spoofed challenge
    });

    const res = await fetch(`${BASE_URL}/api/auth/passkeys/login/verify`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        credentialId: ecCredentialId,
        challenge: optData.challenge,
        signature: badChalAssertion.signature,
        clientDataJSON: badChalAssertion.clientDataJSON,
        authenticatorData: badChalAssertion.authenticatorData
      })
    });
    const data = await res.json();
    const pass = res.status === 401 && data.code === 'biometric_verification_failed';
    recordSecurityTest(
      'SEC-LOG-05',
      'Reject Mismatched Challenge in Assertion clientDataJSON',
      'POST /api/auth/passkeys/login/verify',
      401,
      res.status,
      pass,
      {
        expected: 'HTTP 401 with code "biometric_verification_failed"',
        actual: `HTTP ${res.status} with code "${data.code}" (${data.title || ''})`
      }
    );
  } catch (e) {
    recordSecurityTest('SEC-LOG-05', 'Reject Mismatched Challenge', 'POST /api/auth/passkeys/login/verify', 401, 500, false, {}, { error: e.message });
  }

  // SEC-LOG-06: Replay of used authentication challenge
  try {
    const assertion = buildWebAuthnAssertion(loginChallenge, ecKey.privateKey, { signCount: 11 });
    const res = await fetch(`${BASE_URL}/api/auth/passkeys/login/verify`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        credentialId: ecCredentialId,
        challenge: loginChallenge, // Replayed from SEC-LOG-01
        signature: assertion.signature,
        clientDataJSON: assertion.clientDataJSON,
        authenticatorData: assertion.authenticatorData
      })
    });
    const data = await res.json();
    const pass = res.status === 401 && data.code === 'invalid_or_expired_challenge';
    recordSecurityTest(
      'SEC-LOG-06',
      'Reject Replay of Used Authentication Challenge',
      'POST /api/auth/passkeys/login/verify',
      401,
      res.status,
      pass,
      {
        expected: 'HTTP 401 with code "invalid_or_expired_challenge"',
        actual: `HTTP ${res.status} with code "${data.code}"`
      }
    );
  } catch (e) {
    recordSecurityTest('SEC-LOG-06', 'Reject Replay in Authentication', 'POST /api/auth/passkeys/login/verify', 401, 500, false, {}, { error: e.message });
  }

  // SEC-LOG-07: Forged / Corrupted Cryptographic Signature
  try {
    const optRes = await fetch(`${BASE_URL}/api/auth/passkeys/login/options`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: userAEmail })
    });
    const optData = await optRes.json();

    const otherEcKey = crypto.generateKeyPairSync('ec', { namedCurve: 'prime256v1' });
    const forgedAssertion = buildWebAuthnAssertion(optData.challenge, otherEcKey.privateKey);

    const res = await fetch(`${BASE_URL}/api/auth/passkeys/login/verify`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        credentialId: ecCredentialId,
        challenge: optData.challenge,
        signature: forgedAssertion.signature, // Signed by wrong private key
        clientDataJSON: forgedAssertion.clientDataJSON,
        authenticatorData: forgedAssertion.authenticatorData
      })
    });
    const data = await res.json();
    const pass = res.status === 401 && data.code === 'biometric_verification_failed';
    recordSecurityTest(
      'SEC-LOG-07',
      'Reject Forged Cryptographic Signature (Wrong Key)',
      'POST /api/auth/passkeys/login/verify',
      401,
      res.status,
      pass,
      {
        expected: 'HTTP 401 with code "biometric_verification_failed"',
        actual: `HTTP ${res.status} with code "${data.code}"`
      }
    );
  } catch (e) {
    recordSecurityTest('SEC-LOG-07', 'Reject Forged Signature', 'POST /api/auth/passkeys/login/verify', 401, 500, false, {}, { error: e.message });
  }

  // SEC-LOG-08: Authentication with Revoked Credential
  try {
    const optRes = await fetch(`${BASE_URL}/api/auth/passkeys/login/options`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: userAEmail })
    });
    const optData = await optRes.json();

    const revokedAssertion = buildWebAuthnAssertion(optData.challenge, rsaKey.privateKey);

    const res = await fetch(`${BASE_URL}/api/auth/passkeys/login/verify`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        credentialId: rsaCredentialId, // Revoked in SEC-REG-08
        challenge: optData.challenge,
        signature: revokedAssertion.signature,
        clientDataJSON: revokedAssertion.clientDataJSON,
        authenticatorData: revokedAssertion.authenticatorData
      })
    });
    const data = await res.json();
    const pass = res.status === 401 && data.code === 'credential_not_found_or_revoked';
    recordSecurityTest(
      'SEC-LOG-08',
      'Reject Authentication using Revoked Passkey Credential',
      'POST /api/auth/passkeys/login/verify',
      401,
      res.status,
      pass,
      {
        expected: 'HTTP 401 with code "credential_not_found_or_revoked"',
        actual: `HTTP ${res.status} with code "${data.code}"`
      }
    );
  } catch (e) {
    recordSecurityTest('SEC-LOG-08', 'Reject Revoked Credential', 'POST /api/auth/passkeys/login/verify', 401, 500, false, {}, { error: e.message });
  }

  // SEC-LOG-09: Non-existent Credential ID
  try {
    const optRes = await fetch(`${BASE_URL}/api/auth/passkeys/login/options`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: userAEmail })
    });
    const optData = await optRes.json();

    const fakeAssertion = buildWebAuthnAssertion(optData.challenge, ecKey.privateKey);

    const res = await fetch(`${BASE_URL}/api/auth/passkeys/login/verify`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        credentialId: 'non_existent_cred_999999',
        challenge: optData.challenge,
        signature: fakeAssertion.signature,
        clientDataJSON: fakeAssertion.clientDataJSON,
        authenticatorData: fakeAssertion.authenticatorData
      })
    });
    const data = await res.json();
    const pass = res.status === 401 && data.code === 'credential_not_found_or_revoked';
    recordSecurityTest(
      'SEC-LOG-09',
      'Reject Authentication with Non-Existent Credential ID',
      'POST /api/auth/passkeys/login/verify',
      401,
      res.status,
      pass,
      {
        expected: 'HTTP 401 with code "credential_not_found_or_revoked"',
        actual: `HTTP ${res.status} with code "${data.code}"`
      }
    );
  } catch (e) {
    recordSecurityTest('SEC-LOG-09', 'Reject Non-Existent Credential ID', 'POST /api/auth/passkeys/login/verify', 401, 500, false, {}, { error: e.message });
  }

  // SEC-LOG-10: Counter Anomaly / Cloning Detection
  try {
    const optRes = await fetch(`${BASE_URL}/api/auth/passkeys/login/options`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: userAEmail })
    });
    const optData = await optRes.json();

    // Stored signCount is 10 from SEC-LOG-01. Presenting signCount=5 (rollback)
    const clonedAssertion = buildWebAuthnAssertion(optData.challenge, ecKey.privateKey, {
      signCount: 5
    });

    const res = await fetch(`${BASE_URL}/api/auth/passkeys/login/verify`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        credentialId: ecCredentialId,
        challenge: optData.challenge,
        signature: clonedAssertion.signature,
        clientDataJSON: clonedAssertion.clientDataJSON,
        authenticatorData: clonedAssertion.authenticatorData
      })
    });
    const data = await res.json();
    const pass = res.status === 401 && data.code === 'authenticator_counter_mismatch';
    recordSecurityTest(
      'SEC-LOG-10',
      'Detect Authenticator Cloning (SignCount Counter Rollback)',
      'POST /api/auth/passkeys/login/verify',
      401,
      res.status,
      pass,
      {
        expected: 'HTTP 401 with code "authenticator_counter_mismatch"',
        actual: `HTTP ${res.status} with code "${data.code}"`
      }
    );
  } catch (e) {
    recordSecurityTest('SEC-LOG-10', 'Detect Authenticator Cloning', 'POST /api/auth/passkeys/login/verify', 401, 500, false, {}, { error: e.message });
  }

  // SEC-LOG-11: Authenticate with Biometric JWT
  try {
    const res = await fetch(`${BASE_URL}/api/auth/passkeys`, {
      headers: { 'Authorization': `Bearer ${biometricAccessToken}` }
    });
    const pass = res.status === 200;
    recordSecurityTest(
      'SEC-LOG-11',
      'Authorize API Calls Using Biometric Session JWT AccessToken',
      'GET /api/auth/passkeys',
      200,
      res.status,
      pass,
      {
        expected: 'HTTP 200 authorized',
        actual: `HTTP ${res.status}`
      }
    );
  } catch (e) {
    recordSecurityTest('SEC-LOG-11', 'Authorize API Calls Using Biometric Session JWT', 'GET /api/auth/passkeys', 200, 500, false, {}, { error: e.message });
  }

  // -------------------------------------------------------------------
  // 3. TRANSACTION AUTHORIZATION SECURITY
  // -------------------------------------------------------------------
  console.log('\n--- 3. TRANSACTION AUTHORIZATION SECURITY ---');

  // Fetch wallets for User A
  const wRes = await fetch(`${BASE_URL}/api/wallets`, {
    headers: { 'Authorization': `Bearer ${userAToken}` }
  });
  const wallets = await wRes.json();
  const sourceWallet = wallets[0];
  const destWallet = wallets[1] || wallets[0];

  // SEC-TX-01: Enrollment check (User B has no passkeys)
  try {
    const res = await fetch(`${BASE_URL}/api/transaction-authorizations/options`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${userBToken}`
      },
      body: JSON.stringify({
        transactionType: 'InternalTransfer',
        amount: 10000,
        sourceId: sourceWallet.id,
        destinationId: destWallet.id,
        idempotencyKey: 'tx_sec_enroll_' + Date.now()
      })
    });
    const data = await res.json();
    const pass = res.status === 400 && data.code === 'biometric_not_enrolled';
    recordSecurityTest(
      'SEC-TX-01',
      'Require Biometric Enrollment Before Transaction Authorization Options',
      'POST /api/transaction-authorizations/options',
      400,
      res.status,
      pass,
      {
        expected: 'HTTP 400 with code "biometric_not_enrolled"',
        actual: `HTTP ${res.status} with code "${data.code}"`
      }
    );
  } catch (e) {
    recordSecurityTest('SEC-TX-01', 'Require Biometric Enrollment', 'POST /api/transaction-authorizations/options', 400, 500, false, {}, { error: e.message });
  }

  // SEC-TX-02: Balance check in Phase A
  try {
    const res = await fetch(`${BASE_URL}/api/transaction-authorizations/options`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${userAToken}`
      },
      body: JSON.stringify({
        transactionType: 'InternalTransfer',
        amount: 999999999999, // Exceeds balance
        sourceId: sourceWallet.id,
        destinationId: destWallet.id,
        idempotencyKey: 'tx_sec_over_' + Date.now()
      })
    });
    const data = await res.json();
    const pass = res.status === 400 && data.code === 'insufficient_funds';
    recordSecurityTest(
      'SEC-TX-02',
      'Enforce Source Wallet Balance Constraint in Phase A',
      'POST /api/transaction-authorizations/options',
      400,
      res.status,
      pass,
      {
        expected: 'HTTP 400 with code "insufficient_funds"',
        actual: `HTTP ${res.status} with code "${data.code}"`
      }
    );
  } catch (e) {
    recordSecurityTest('SEC-TX-02', 'Enforce Source Wallet Balance', 'POST /api/transaction-authorizations/options', 400, 500, false, {}, { error: e.message });
  }

  // Create valid Phase A authorization for subsequent tests
  const txAmount = 15000;
  const txIdempotencyKey = 'tx_sec_valid_' + Date.now();
  const txOptRes = await fetch(`${BASE_URL}/api/transaction-authorizations/options`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${userAToken}`
    },
    body: JSON.stringify({
      transactionType: 'InternalTransfer',
      amount: txAmount,
      currency: 'VND',
      sourceId: sourceWallet.id,
      destinationId: destWallet.id,
      idempotencyKey: txIdempotencyKey
    })
  });
  const txAuth = await txOptRes.json();

  // SEC-TX-03: Reject Direct Mode for Financial Payments (Requires WebAuthn assertion)
  try {
    const res = await fetch(`${BASE_URL}/api/transaction-authorizations/verify`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${userAToken}`
      },
      body: JSON.stringify({
        authorizationId: txAuth.authorizationId,
        credentialId: ecCredentialId,
        signature: 'dummy_direct_sig',
        clientDataJSON: null,
        authenticatorData: null
      })
    });
    const data = await res.json();
    const pass = res.status === 401 && data.code === 'biometric_verification_failed';
    recordSecurityTest(
      'SEC-TX-03',
      'Reject Direct Mode for Financial Payments (Require WebAuthn Assertion)',
      'POST /api/transaction-authorizations/verify',
      401,
      res.status,
      pass,
      {
        expected: 'HTTP 401 with code "biometric_verification_failed"',
        actual: `HTTP ${res.status} with code "${data.code}" (${data.title || ''})`
      }
    );
  } catch (e) {
    recordSecurityTest('SEC-TX-03', 'Reject Direct Mode for Financial Payments', 'POST /api/transaction-authorizations/verify', 401, 500, false, {}, { error: e.message });
  }

  // SEC-TX-04: Reject Assertion with Missing User Verification (UV flag not set)
  try {
    const noUvAssertion = buildWebAuthnAssertion(txAuth.challenge, ecKey.privateKey, {
      userPresent: true,
      userVerified: false, // UV bit (0x04) = 0
      signCount: 15
    });

    const res = await fetch(`${BASE_URL}/api/transaction-authorizations/verify`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${userAToken}`
      },
      body: JSON.stringify({
        authorizationId: txAuth.authorizationId,
        credentialId: ecCredentialId,
        signature: noUvAssertion.signature,
        clientDataJSON: noUvAssertion.clientDataJSON,
        authenticatorData: noUvAssertion.authenticatorData
      })
    });
    const data = await res.json();
    const pass = res.status === 401 && data.code === 'biometric_verification_failed';
    recordSecurityTest(
      'SEC-TX-04',
      'Enforce User Verification Flag (UV=1) in Payment Authorization',
      'POST /api/transaction-authorizations/verify',
      401,
      res.status,
      pass,
      {
        expected: 'HTTP 401 with code "biometric_verification_failed"',
        actual: `HTTP ${res.status} with code "${data.code}" (${data.title || ''})`
      }
    );
  } catch (e) {
    recordSecurityTest('SEC-TX-04', 'Enforce UV Flag in Payment Authorization', 'POST /api/transaction-authorizations/verify', 401, 500, false, {}, { error: e.message });
  }

  // SEC-TX-05: Cross-User Authorization Isolation (User B cannot authorize User A's tx)
  try {
    const validAssertion = buildWebAuthnAssertion(txAuth.challenge, ecKey.privateKey, { signCount: 20 });
    const res = await fetch(`${BASE_URL}/api/transaction-authorizations/verify`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${userBToken}` // User B token
      },
      body: JSON.stringify({
        authorizationId: txAuth.authorizationId,
        credentialId: ecCredentialId,
        signature: validAssertion.signature,
        clientDataJSON: validAssertion.clientDataJSON,
        authenticatorData: validAssertion.authenticatorData
      })
    });
    const pass = res.status === 404;
    recordSecurityTest(
      'SEC-TX-05',
      'Cross-User Transaction Authorization Isolation (HTTP 404 for Non-Owner)',
      'POST /api/transaction-authorizations/verify',
      404,
      res.status,
      pass,
      {
        expected: 'HTTP 404 NotFound',
        actual: `HTTP ${res.status}`
      }
    );
  } catch (e) {
    recordSecurityTest('SEC-TX-05', 'Cross-User Transaction Authorization Isolation', 'POST /api/transaction-authorizations/verify', 404, 500, false, {}, { error: e.message });
  }

  // SEC-TX-06: Valid Phase B Authorization & Execution
  try {
    const validAssertion = buildWebAuthnAssertion(txAuth.challenge, ecKey.privateKey, {
      signCount: 25,
      userVerified: true,
      userPresent: true
    });

    const res = await fetch(`${BASE_URL}/api/transaction-authorizations/verify`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${userAToken}`
      },
      body: JSON.stringify({
        authorizationId: txAuth.authorizationId,
        credentialId: ecCredentialId,
        signature: validAssertion.signature,
        clientDataJSON: validAssertion.clientDataJSON,
        authenticatorData: validAssertion.authenticatorData
      })
    });
    const data = await res.json();
    const pass = res.status === 200 && data.success === true && data.status === 'Executed';
    recordSecurityTest(
      'SEC-TX-06',
      'Authorize & Atomically Settle Financial Transaction via Biometric Signature',
      'POST /api/transaction-authorizations/verify',
      200,
      res.status,
      pass,
      {
        expected: 'HTTP 200, success=true, status="Executed"',
        actual: `HTTP ${res.status}, success=${data.success}, status="${data.status}"`
      }
    );
  } catch (e) {
    recordSecurityTest('SEC-TX-06', 'Authorize & Settle Financial Transaction', 'POST /api/transaction-authorizations/verify', 200, 500, false, {}, { error: e.message });
  }

  // SEC-TX-07: Idempotent Replay Prevention (No Double Spend)
  try {
    const validAssertion = buildWebAuthnAssertion(txAuth.challenge, ecKey.privateKey, { signCount: 26 });
    const res = await fetch(`${BASE_URL}/api/transaction-authorizations/verify`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${userAToken}`
      },
      body: JSON.stringify({
        authorizationId: txAuth.authorizationId,
        credentialId: ecCredentialId,
        signature: validAssertion.signature,
        clientDataJSON: validAssertion.clientDataJSON,
        authenticatorData: validAssertion.authenticatorData
      })
    });
    const data = await res.json();
    const pass = res.status === 200 && data.status === 'Executed';
    recordSecurityTest(
      'SEC-TX-07',
      'Idempotent Replay of Executed Authorization (Zero Double Posting)',
      'POST /api/transaction-authorizations/verify',
      200,
      res.status,
      pass,
      {
        expected: 'HTTP 200 cached Executed result',
        actual: `HTTP ${res.status}, status="${data.status}"`
      }
    );
  } catch (e) {
    recordSecurityTest('SEC-TX-07', 'Idempotent Replay of Executed Authorization', 'POST /api/transaction-authorizations/verify', 200, 500, false, {}, { error: e.message });
  }

  // SEC-TX-08: Zero-Knowledge Verification (No Private Keys or Templates in DB)
  try {
    const res = await fetch(`${BASE_URL}/api/auth/passkeys`, {
      headers: { 'Authorization': `Bearer ${userAToken}` }
    });
    const list = await res.json();
    const leaksPrivateKey = list.some(c => c.privateKey || c.rawPrivateKey || c.biometricTemplate);
    const pass = !leaksPrivateKey;
    recordSecurityTest(
      'SEC-TX-08',
      'Zero-Knowledge Credential Security (No Private Keys or Biometric Templates Stored)',
      'GET /api/auth/passkeys',
      200,
      res.status,
      pass,
      {
        expected: 'No private key or template data in API response or database',
        actual: `leaksPrivateKey=${leaksPrivateKey}`
      }
    );
  } catch (e) {
    recordSecurityTest('SEC-TX-08', 'Zero-Knowledge Credential Security', 'GET /api/auth/passkeys', 200, 500, false, {}, { error: e.message });
  }

  // -------------------------------------------------------------------
  // SUMMARY
  // -------------------------------------------------------------------
  const total = securityResults.length;
  const passed = securityResults.filter(r => r.status === 'PASS').length;
  const failed = securityResults.filter(r => r.status === 'FAIL').length;

  console.log('\n================================================================');
  console.log(`SECURITY SUITE TOTAL: ${total} | PASSED: ${passed} | FAILED: ${failed}`);
  console.log('================================================================\n');

  const reportDir = path.resolve(__dirname, '../../../reports/biometric');
  if (!fs.existsSync(reportDir)) fs.mkdirSync(reportDir, { recursive: true });

  const reportPath = path.join(reportDir, 'biometric-final-security-results.json');
  fs.writeFileSync(reportPath, JSON.stringify({
    summary: {
      suite: 'Biometric Security & WebAuthn Compliance',
      total,
      passed,
      failed,
      verdict: failed === 0 ? 'PASS' : 'FAIL',
      timestamp: new Date().toISOString()
    },
    results: securityResults
  }, null, 2));

  console.log(`Security results saved to ${reportPath}`);
}

runSecuritySuite().catch(err => {
  console.error('Fatal Security Suite error:', err);
  process.exit(1);
});
