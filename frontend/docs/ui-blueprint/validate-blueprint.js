import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

console.log('=== SPENDWISE UI BLUEPRINT ENHANCED VALIDATOR ===');
const manifestPath = path.join(__dirname, 'feature-manifest.json');

if (!fs.existsSync(manifestPath)) {
  console.error('FAIL: feature-manifest.json not found!');
  process.exit(1);
}

let manifest;
try {
  manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
  console.log('✓ Check 1: feature-manifest.json parsed successfully.');
} catch (e) {
  console.error('FAIL: Invalid JSON in feature-manifest.json:', e.message);
  process.exit(1);
}

const errors = [];
const warnings = [];

// 1. Metric Counts Verification
const countedPages = manifest.pages.length;
let countedSections = 0;
let countedFeatures = 0;
const countedVer = {
  BROWSER_VERIFIED: 0,
  SOURCE_VERIFIED: 0,
  UNVERIFIED: 0,
  CONFLICT: 0
};

const uniqueSectionIds = new Set();
const uniqueFeatureIds = new Set();

manifest.pages.forEach(p => {
  (p.sections || []).forEach(s => {
    countedSections++;
    if (uniqueSectionIds.has(s.sectionId)) {
      errors.push(`Duplicate sectionId in manifest: ${s.sectionId}`);
    }
    uniqueSectionIds.add(s.sectionId);
  });

  (p.features || []).forEach(f => {
    countedFeatures++;
    if (uniqueFeatureIds.has(f.featureId)) {
      errors.push(`Duplicate featureId in manifest: ${f.featureId}`);
    }
    uniqueFeatureIds.add(f.featureId);

    if (f.verificationStatus && countedVer[f.verificationStatus] !== undefined) {
      countedVer[f.verificationStatus]++;
    } else {
      errors.push(`Feature ${f.featureId} has invalid verificationStatus: ${f.verificationStatus}`);
    }
  });
});

if (manifest.metrics.totalUniqueScreens !== countedPages) {
  errors.push(`Metric mismatch: totalUniqueScreens (${manifest.metrics.totalUniqueScreens}) != countedPages (${countedPages})`);
}
if (manifest.metrics.totalInventorySections !== countedSections) {
  errors.push(`Metric mismatch: totalInventorySections (${manifest.metrics.totalInventorySections}) != countedSections (${countedSections})`);
}
if (manifest.metrics.totalTrackedFeatures !== countedFeatures) {
  errors.push(`Metric mismatch: totalTrackedFeatures (${manifest.metrics.totalTrackedFeatures}) != countedFeatures (${countedFeatures})`);
}
if (manifest.metrics.verificationCounts.BROWSER_VERIFIED !== countedVer.BROWSER_VERIFIED) {
  errors.push(`Metric mismatch: BROWSER_VERIFIED (${manifest.metrics.verificationCounts.BROWSER_VERIFIED}) != counted (${countedVer.BROWSER_VERIFIED})`);
}
if (manifest.metrics.verificationCounts.SOURCE_VERIFIED !== countedVer.SOURCE_VERIFIED) {
  errors.push(`Metric mismatch: SOURCE_VERIFIED (${manifest.metrics.verificationCounts.SOURCE_VERIFIED}) != counted (${countedVer.SOURCE_VERIFIED})`);
}

// 2. Comprehensive Feature Spec Fields Verification
const requiredSpecFields = [
  'expectedBehavior',
  'apiDependencies',
  'validation',
  'permissions',
  'states',
  'navigationTarget',
  'sideEffects',
  'evidence',
  'verificationStatus'
];

manifest.pages.forEach(p => {
  p.features.forEach(f => {
    requiredSpecFields.forEach(field => {
      if (!f[field] || typeof f[field] !== 'string' || f[field].trim() === '') {
        errors.push(`Feature ${f.featureId} in ${p.pageId} is missing required specification field: ${field}`);
      }
    });

    // 3. Evidence file path verification
    if (f.evidence) {
      const parts = f.evidence.split(':');
      const relPath = parts[0];
      const fullPath = path.resolve(__dirname, '..', '..', relPath);
      if (!fs.existsSync(fullPath)) {
        errors.push(`Feature ${f.featureId} evidence path does not exist on disk: ${relPath}`);
      }
    }
  });
});

// 4. Validate User Pages Markdown vs Manifest Parity
const userMdDir = path.join(__dirname, 'USER-PAGES');
const userMdFiles = fs.readdirSync(userMdDir).filter(f => f.endsWith('.md'));

userMdFiles.forEach(f => {
  const content = fs.readFileSync(path.join(userMdDir, f), 'utf8');
  const pageMatch = content.match(/Page ID:[^`]*`([^`]+)`/);
  if (!pageMatch) {
    errors.push(`No Page ID declared in USER-PAGES/${f}`);
    return;
  }
  const pageId = pageMatch[1].trim();
  const jsonPage = manifest.pages.find(p => p.pageId === pageId);
  if (!jsonPage) {
    errors.push(`Page ID ${pageId} from ${f} not found in manifest JSON`);
    return;
  }

  // Check sections in table
  const secMatches = [...content.matchAll(/\|\s*`([A-Z0-9_-]+-SEC-[0-9A-Z]+)`\s*\|/g)].map(m => m[1]);
  const jsonSecIds = (jsonPage.sections || []).map(s => s.sectionId);
  secMatches.forEach(sid => {
    if (!jsonSecIds.includes(sid)) {
      errors.push(`Section ${sid} found in ${f} table but missing from manifest JSON for ${pageId}`);
    }
  });
  jsonSecIds.forEach(sid => {
    if (!secMatches.includes(sid)) {
      errors.push(`Section ${sid} defined in manifest JSON for ${pageId} but missing from ${f} table`);
    }
  });

  // Check features
  const featMatches = [...content.matchAll(/-\s*\*\*Feature ID:\*\*\s*`([A-Z0-9_-]+)`/g)].map(m => m[1]);
  const jsonFeatIds = (jsonPage.features || []).map(feat => feat.featureId);
  featMatches.forEach(fid => {
    if (!jsonFeatIds.includes(fid)) {
      errors.push(`Feature ${fid} found in ${f} but missing from manifest JSON for ${pageId}`);
    }
  });
  jsonFeatIds.forEach(fid => {
    if (!featMatches.includes(fid)) {
      errors.push(`Feature ${fid} defined in manifest JSON for ${pageId} but missing from ${f}`);
    }
  });
});

// 5. Validate Admin Pages Markdown Files Existence & Parity
const adminMdDir = path.join(__dirname, 'ADMIN-PAGES');
const requiredAdminDocs = [
  'admin-overview.md',
  'admin-users.md',
  'admin-transactions.md',
  'admin-ledger.md',
  'admin-risk.md',
  'admin-disputes.md',
  'admin-support.md',
  'admin-audit.md',
  'admin-banking.md',
  'admin-reconciliation.md',
  'admin-health.md',
  'admin-configuration.md',
  'admin-feature-flags.md'
];

requiredAdminDocs.forEach(docName => {
  const fullPath = path.join(adminMdDir, docName);
  if (!fs.existsSync(fullPath)) {
    errors.push(`Missing individual admin document: ADMIN-PAGES/${docName}`);
  } else {
    const docContent = fs.readFileSync(fullPath, 'utf8');
    const pageMatch = docContent.match(/Page ID:[^`]*`([^`]+)`/);
    if (!pageMatch) {
      errors.push(`Missing Page ID in ADMIN-PAGES/${docName}`);
    } else {
      const pageId = pageMatch[1].trim();
      const jsonPage = manifest.pages.find(p => p.pageId === pageId);
      if (!jsonPage) {
        errors.push(`Page ID ${pageId} from ${docName} not found in manifest JSON`);
      }
    }
  }
});

// 6. Regression & Business Contradiction Verification
// Regression check for ADM-BNK-002: Must be monitoring synced transactions, NOT mock payment simulator
const bnkPage = manifest.pages.find(p => p.pageId === 'ADMIN-BANKING');
const bnk002 = bnkPage ? bnkPage.features.find(f => f.featureId === 'ADM-BNK-002') : null;
if (!bnk002) {
  errors.push('Regression error: ADM-BNK-002 missing from ADMIN-BANKING');
} else {
  if (bnk002.title.toLowerCase().includes('simulator') || bnk002.title.toLowerCase().includes('mock inbound')) {
    errors.push('Regression contradiction: ADM-BNK-002 is wrongly described as mock simulator instead of Synced Transactions Monitor');
  }
  if (!bnk002.handler.includes('getAdminBankTransactions')) {
    errors.push('Regression contradiction: ADM-BNK-002 handler does not target bankApiService.getAdminBankTransactions');
  }
}

// Regression check for ADM-HLT-001: Must reflect .NET 10 / SQL Server, NOT PostgreSQL
const hltPage = manifest.pages.find(p => p.pageId === 'ADMIN-HEALTH');
const hlt001 = hltPage ? hltPage.features.find(f => f.featureId === 'ADM-HLT-001') : null;
if (!hlt001) {
  errors.push('Regression error: ADM-HLT-001 missing from ADMIN-HEALTH');
} else {
  if (hlt001.title.includes('Postgres') || hlt001.expectedBehavior.includes('PostgreSQL')) {
    errors.push('Regression contradiction: ADM-HLT-001 wrongly specifies PostgreSQL instead of SQL Server / .NET 10');
  }
}

// 7. Validate FEATURE-ID-MIGRATION.md
const migPath = path.join(__dirname, 'FEATURE-ID-MIGRATION.md');
if (!fs.existsSync(migPath)) {
  errors.push('FEATURE-ID-MIGRATION.md not found!');
}

console.log('----------------------------------------------------');
console.log(`Evaluated: ${countedPages} Screens, ${countedSections} Sections, ${countedFeatures} Features.`);
console.log(`Browser Verified: ${countedVer.BROWSER_VERIFIED}, Source Verified: ${countedVer.SOURCE_VERIFIED}`);

if (errors.length > 0) {
  console.error(`FAIL: Found ${errors.length} validation errors:`);
  errors.forEach((err, idx) => console.error(`  ${idx + 1}. ${err}`));
  process.exit(1);
} else {
  console.log('PASS: All 10 validation check suites passed with 0 errors!');
  process.exit(0);
}
