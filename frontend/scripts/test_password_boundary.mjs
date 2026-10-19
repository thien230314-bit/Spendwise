import { isValidPassword } from '../src/utils/inputValidation';
import { calculatePasswordStrength } from '../src/utils/inputValidation';

const boundaryTests = [
  { name: '7 characters, valid character types', input: 'Pass123', expected: false },
  { name: '8 characters, valid character types', input: 'Pass1234', expected: true },
  { name: '9 characters, valid character types', input: 'Pass12345', expected: true },
  { name: '12 characters, valid character types', input: 'Password1234', expected: true },
  { name: '128 characters, valid character types', input: 'A' + 'a'.repeat(126) + '1', expected: true },
  { name: '129 characters', input: 'A' + 'a'.repeat(127) + '1', expected: false },
  { name: 'Missing uppercase', input: 'password123', expected: false },
  { name: 'Missing lowercase', input: 'PASSWORD123', expected: false },
  { name: 'Missing number', input: 'PasswordABC', expected: false },
  { name: 'Valid password with special characters', input: 'Pass@123!', expected: true },
];

console.log('--- PASSWORD BOUNDARY TESTS (8-128 POLICY) ---');
let allPassed = true;
for (const test of boundaryTests) {
  const result = isValidPassword(test.input);
  const pass = result === test.expected;
  if (!pass) allPassed = false;
  console.log(`${pass ? '✓ PASS' : '✗ FAIL'}: ${test.name} -> result: ${result}, expected: ${test.expected}`);
}

console.log('\n--- PASSWORD STRENGTH TESTS ---');
const weakTest = calculatePasswordStrength('Pass1234');
console.log(`Valid 8-char password strength level: ${weakTest.level}, mandatory satisfied: ${weakTest.isMandatorySatisfied}`);

if (!allPassed) {
  console.error('Some boundary tests failed!');
  process.exit(1);
} else {
  console.log('ALL BOUNDARY TESTS PASSED SUCCESSFULLY!');
}
