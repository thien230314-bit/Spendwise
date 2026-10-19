import fs from 'fs';
import path from 'path';
import type { Plugin } from 'vite';

const APPROVALS_FILE = path.resolve(__dirname, '.ui-approvals.json');

const ALLOWED_PAGE_KEYS = ['dashboard', 'wallet', 'transactions', 'transfer', 'budget', 'goals'];
const ALLOWED_STATUSES = ['PENDING', 'APPROVED', 'NEEDS_REVISION', 'KEEP_CURRENT', 'APPLYING', 'APPLIED', 'APPLY_FAILED'];

function readApprovals() {
  try {
    if (fs.existsSync(APPROVALS_FILE)) {
      return JSON.parse(fs.readFileSync(APPROVALS_FILE, 'utf-8'));
    }
  } catch (err) {
    console.error('[devUiApprovalPlugin] Error reading .ui-approvals.json:', err);
  }
  return { version: '1.0.0', updatedAt: new Date().toISOString(), pages: {} };
}

function writeApprovals(data: any) {
  try {
    fs.writeFileSync(APPROVALS_FILE, JSON.stringify(data, null, 2), 'utf-8');
    return true;
  } catch (err) {
    console.error('[devUiApprovalPlugin] Error writing .ui-approvals.json:', err);
    return false;
  }
}

export function devUiApprovalPlugin(): Plugin {
  return {
    name: 'dev-ui-approval-middleware',
    apply: 'serve', // Strictly active in development server only! Never included in production bundle.
    configureServer(server) {
      server.middlewares.use('/api/dev/ui-approvals', (req, res, next) => {
        res.setHeader('Content-Type', 'application/json');

        if (req.method === 'GET') {
          const data = readApprovals();
          res.statusCode = 200;
          res.end(JSON.stringify({ success: true, data }));
          return;
        }

        if (req.method === 'POST') {
          let body = '';
          req.on('data', (chunk) => {
            body += chunk;
            if (body.length > 50000) {
              req.destroy();
            }
          });

          req.on('end', () => {
            try {
              const payload = JSON.parse(body);
              const { pageKey, status, previewVersion, fingerprint, feedback, parityStatus, parityMissingCount, parityAuditNotes } = payload;

              if (!ALLOWED_PAGE_KEYS.includes(pageKey)) {
                res.statusCode = 400;
                res.end(JSON.stringify({ success: false, error: 'Invalid page key' }));
                return;
              }

              if (!ALLOWED_STATUSES.includes(status)) {
                res.statusCode = 400;
                res.end(JSON.stringify({ success: false, error: 'Invalid status' }));
                return;
              }

              const data = readApprovals();
              if (!data.pages) data.pages = {};

              const existing = data.pages[pageKey] || {};
              data.pages[pageKey] = {
                ...existing,
                status,
                previewVersion: previewVersion || existing.previewVersion || 'v2.0',
                fingerprint: fingerprint || existing.fingerprint || '',
                parityStatus: parityStatus !== undefined ? parityStatus : existing.parityStatus || 'NOT_AUDITED',
                parityMissingCount: parityMissingCount !== undefined ? parityMissingCount : existing.parityMissingCount || 0,
                parityAuditNotes: parityAuditNotes !== undefined ? parityAuditNotes : existing.parityAuditNotes || null,
                decidedAt: new Date().toISOString(),
                feedback: feedback !== undefined ? feedback : existing.feedback || null,
              };
              data.updatedAt = new Date().toISOString();

              const ok = writeApprovals(data);
              if (ok) {
                res.statusCode = 200;
                res.end(JSON.stringify({ success: true, data: data.pages[pageKey] }));
              } else {
                res.statusCode = 500;
                res.end(JSON.stringify({ success: false, error: 'Failed to write storage file' }));
              }
            } catch (err: any) {
              res.statusCode = 400;
              res.end(JSON.stringify({ success: false, error: err?.message || 'Malformed JSON' }));
            }
          });
          return;
        }

        next();
      });
    },
  };
}
