export interface AdminUserRecord {
  id: string;
  name: string;
  email: string;
  phone: string;
  role: 'User' | 'SystemAdmin';
  status: 'active' | 'restricted' | 'locked' | 'suspended';
  kycTier: 'tier1' | 'tier2' | 'tier3';
  twoFactorEnabled: boolean;
  activeSessionsCount: number;
  lastLoginAt: string;
  createdAt: string;
  riskScore: number; // 0 - 100
}

export interface AdminTransactionRecord {
  id: string;
  referenceCode: string;
  type: 'p2p_transfer' | 'merchant_payment' | 'group_split' | 'refund';
  amount: number;
  currency: 'VND' | 'USD';
  status: 'completed' | 'pending' | 'flagged' | 'failed' | 'reversed';
  senderMasked: string;
  receiverMasked: string;
  channel: 'SW_WALLET' | 'VIETQR' | 'VISA_MC';
  riskScore: number;
  createdAt: string;
  notes?: string;
}

export interface AdminLedgerEntry {
  id: string;
  entryNumber: string;
  relatedTxRef: string;
  timestamp: string;
  debitAccount: string;
  debitAmount: number;
  creditAccount: string;
  creditAmount: number;
  currency: 'VND' | 'USD';
  status: 'balanced' | 'investigation_required';
  recordedBy: string;
}

export interface AdminRiskCase {
  id: string;
  caseNumber: string;
  severity: 'critical' | 'high' | 'medium' | 'low';
  ruleTriggered: string;
  ruleTriggeredEn: string;
  targetUserMasked: string;
  relatedTxRef: string;
  riskScore: number;
  status: 'open_review' | 'under_investigation' | 'reviewed_allowed' | 'blocked';
  createdAt: string;
  detectedReason: string;
  detectedReasonEn: string;
}

export interface AdminDisputeRecord {
  id: string;
  disputeCode: string;
  userMasked: string;
  transactionRef: string;
  amount: number;
  currency: 'VND' | 'USD';
  reasonCategory: 'unauthorized_access' | 'duplicate_deduction' | 'incorrect_split' | 'merchant_dispute';
  status: 'open' | 'under_review' | 'waiting_user_evidence' | 'resolved' | 'rejected';
  createdAt: string;
  lastUpdated: string;
  description: string;
  adminNotes?: string;
}

export interface AdminSupportTicket {
  id: string;
  ticketNumber: string;
  userMasked: string;
  userEmailMasked: string;
  subject: string;
  category: 'account_access' | 'payment_issue' | 'kyc_verification' | 'group_billing' | 'technical';
  priority: 'urgent' | 'high' | 'normal' | 'low';
  status: 'new' | 'assigned' | 'in_progress' | 'resolved' | 'closed';
  assignedAgent?: string;
  createdAt: string;
  updatedAt: string;
}

export interface AdminAuditRecord {
  id: string;
  actor: string;
  actorRole: 'SystemAdmin' | 'SecOfficer' | 'SystemCron' | 'AutomatedRuleEngine';
  action: string;
  actionEn: string;
  target: string;
  targetType: 'USER' | 'TRANSACTION' | 'CONFIGURATION' | 'FEATURE_FLAG' | 'RISK_RULE';
  result: 'SUCCESS' | 'FAILED' | 'WARNING';
  ipAddress: string;
  timestamp: string;
  details: string;
}

import { BankProvider, BankTransaction } from './index';
export type { BankTransaction };

export interface AdminBankIntegrationEvent {
  id: string;
  provider: BankProvider;
  eventType: string;
  externalEventId?: string;
  externalTransactionId?: string;
  externalBankAccountId?: string;
  processingStatus: 'Received' | 'Processed' | 'Unmatched' | 'Failed' | 'Ignored';
  duplicateDeliveryCount: number;
  lastDuplicateReceivedAt?: string;
  receivedAt: string;
  processedAt?: string;
  details?: string;
  payload?: Record<string, any>;
}

export interface BankReconciliationSummary {
  activeConnections: number;
  inactiveConnections: number;
  unlinkedConnections: number;
  processedEvents: number;
  duplicateDeliveries: number;
  unmatchedEvents: number;
  failedEvents: number;
  importedTransactions: number;
}

export interface SePayIntegrationStatus {
  enabled: boolean;
  environment: 'Sandbox' | 'Production';
  configured: boolean;
  companyConfigured: boolean;
  lifecycleWebhookConfigured: boolean;
  ipnConfigured: boolean;
  lastSuccessfulProviderCallAt?: string;
  lastWebhookReceivedAt?: string;
}

export interface AdminServiceHealth {
  id: string;
  serviceName: string;
  category: 'core' | 'database' | 'realtime' | 'ai' | 'banking' | 'cache';
  status: 'healthy' | 'degraded' | 'maintenance' | 'offline';
  uptimePercentage: number;
  latencyMs: number;
  lastChecked: string;
  endpoint: string;
}

export interface AdminConfigurationSetting {
  key: string;
  label: string;
  labelEn: string;
  category: 'security' | 'limits' | 'notifications' | 'maintenance';
  value: string | number | boolean;
  type: 'number' | 'boolean' | 'string' | 'select';
  options?: { label: string; labelEn: string; value: string | number }[];
  description: string;
  descriptionEn: string;
}

export interface AdminFeatureFlag {
  id: string;
  key: string;
  name: string;
  nameEn: string;
  description: string;
  descriptionEn: string;
  category: 'core' | 'experimental' | 'financial' | 'ai';
  enabled: boolean;
  targetAudience: 'all_users' | 'beta_testers' | 'internal_only' | 'disabled';
  lastModifiedBy: string;
  updatedAt: string;
}
