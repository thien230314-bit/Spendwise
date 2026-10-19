import type { BankConnection, BankTransaction, BankConnectionStatus, BankTransactionDirection, BankTransactionSource, AccountEnvironment } from '../types';
import type {
  AdminBankIntegrationEvent,
  BankReconciliationSummary,
  SePayIntegrationStatus,
} from '../types/admin';
import type {
  BankConnectionDto,
  AdminBankConnectionDto,
  BankTransactionDto,
  AdminBankTransactionDto,
  AdminBankIntegrationEventDto,
  BankReconciliationDto,
  SePayBankHubStatusDto,
} from './bankApiService';

export function mapBankConnection(dto: BankConnectionDto, userId: string): BankConnection {
  return {
    id: dto.id,
    userId,
    provider: 'SePayBankHub',
    bankBrandName: dto.bankBrandName,
    accountType: dto.accountType,
    accountHolderName: dto.accountHolderName,
    maskedAccountNumber: dto.maskedAccountNumber,
    status: (dto.status as BankConnectionStatus) || 'Active',
    linkedAt: dto.linkedAt,
    lastSyncedAt: dto.lastSyncedAt,
    createdAt: dto.linkedAt || new Date().toISOString(),
    accountEnvironment: dto.accountEnvironment === 'TestMode' ? 'TestMode' : 'BankHub',
  };
}

export function mapAdminBankConnection(dto: AdminBankConnectionDto): BankConnection {
  return {
    id: dto.id,
    userId: dto.userId,
    provider: 'SePayBankHub',
    bankBrandName: dto.bankBrandName,
    accountType: 'Thanh toán',
    maskedAccountNumber: dto.maskedAccountNumber,
    status: (dto.status as BankConnectionStatus) || 'Active',
    linkedAt: dto.linkedAt,
    lastSyncedAt: dto.lastSyncedAt,
    createdAt: dto.createdAt,
    accountEnvironment: dto.accountEnvironment === 'TestMode' ? 'TestMode' : 'BankHub',
  };
}

export function mapBankTransaction(dto: BankTransactionDto, userId: string): BankTransaction {
  const direction: BankTransactionDirection =
    dto.direction?.toLowerCase() === 'debit' ? 'Debit' : 'Credit';
  const source: BankTransactionSource =
    dto.source === 'ManualSync' ? 'ManualSync' : 'Webhook';

  return {
    id: dto.id,
    userId,
    bankConnectionId: dto.bankConnectionId,
    provider: 'SePayBankHub',
    bankBrandName: dto.bankBrandName,
    maskedAccountNumber: dto.maskedAccountNumber,
    externalTransactionId: dto.externalTransactionId,
    direction,
    amount: dto.amount,
    currency: dto.currency || 'VND',
    transactionDate: dto.transactionDate,
    content: dto.content,
    referenceCode: dto.referenceCode,
    paymentCode: dto.paymentCode,
    gateway: dto.gateway,
    source,
    importedAt: dto.importedAt,
    categoryId: dto.categoryId,
    categoryName: dto.categoryName,
  };
}

export function mapAdminBankTransaction(dto: AdminBankTransactionDto): BankTransaction {
  const direction: BankTransactionDirection =
    dto.direction?.toLowerCase() === 'debit' ? 'Debit' : 'Credit';
  const source: BankTransactionSource =
    dto.source === 'ManualSync' ? 'ManualSync' : 'Webhook';

  return {
    id: dto.id,
    userId: dto.userId,
    bankConnectionId: dto.bankConnectionId,
    provider: 'SePayBankHub',
    bankBrandName: dto.bankBrandName,
    maskedAccountNumber: dto.maskedAccountNumber,
    externalTransactionId: dto.externalTransactionId,
    direction,
    amount: dto.amount,
    currency: dto.currency || 'VND',
    transactionDate: dto.transactionDate,
    content: dto.content,
    referenceCode: dto.referenceCode,
    paymentCode: dto.paymentCode,
    gateway: dto.gateway,
    source,
    importedAt: dto.importedAt,
  };
}

export function mapAdminBankIntegrationEvent(dto: AdminBankIntegrationEventDto): AdminBankIntegrationEvent {
  let processingStatus: 'Received' | 'Processed' | 'Unmatched' | 'Failed' | 'Ignored' = 'Processed';
  const rawStatus = dto.status?.toLowerCase();
  if (rawStatus === 'unmatched') {
    processingStatus = 'Unmatched';
  } else if (rawStatus === 'failed') {
    processingStatus = 'Failed';
  } else if (rawStatus === 'received') {
    processingStatus = 'Received';
  } else if (rawStatus === 'ignored') {
    processingStatus = 'Ignored';
  }

  return {
    id: dto.id,
    provider: 'SePayBankHub',
    eventType: dto.eventType,
    externalEventId: dto.externalEventId,
    externalTransactionId: dto.externalTransactionId,
    externalBankAccountId: dto.externalBankAccountId,
    processingStatus,
    duplicateDeliveryCount: dto.duplicateDeliveryCount || 0,
    lastDuplicateReceivedAt: dto.lastDuplicateReceivedAt,
    receivedAt: dto.receivedAt,
    processedAt: dto.processedAt,
    details: dto.failureCode ? `Mã lỗi: ${dto.failureCode}` : undefined,
  };
}

export function mapBankReconciliationSummary(dto: BankReconciliationDto): BankReconciliationSummary {
  return {
    activeConnections: dto.activeConnections,
    inactiveConnections: dto.inactiveConnections,
    unlinkedConnections: dto.unlinkedConnections,
    processedEvents: dto.processedEvents,
    duplicateDeliveries: dto.duplicateDeliveries,
    unmatchedEvents: dto.unmatchedEvents,
    failedEvents: dto.failedEvents,
    importedTransactions: dto.importedTransactions,
  };
}

export function mapSePayIntegrationStatus(dto: SePayBankHubStatusDto): SePayIntegrationStatus {
  return {
    enabled: dto.enabled,
    environment: dto.environment === 'Production' ? 'Production' : 'Sandbox',
    configured: dto.configured,
    companyConfigured: dto.companyConfigured,
    lifecycleWebhookConfigured: dto.lifecycleWebhookConfigured,
    ipnConfigured: dto.ipnConfigured,
    lastSuccessfulProviderCallAt: dto.lastSuccessfulProviderCallAt,
    lastWebhookReceivedAt: dto.lastWebhookReceivedAt,
  };
}
