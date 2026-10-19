import {
  Transaction,
  TransactionRule,
  CategoryItem,
  TransactionDirection,
  TransactionSemanticType,
  ClassificationSource,
  NotificationItem,
} from '../types';

export interface ClassifyCandidateParams {
  amount: number;
  direction: TransactionDirection;
  title?: string;
  description?: string;
  counterparty?: string;
  date?: string; // ISO date string or YYYY-MM-DD
  existingTransactions: Transaction[];
  rules: TransactionRule[];
  categories: CategoryItem[];
}

export interface ClassificationResult {
  direction: TransactionDirection;
  semanticType: TransactionSemanticType;
  category: string;
  classificationSource: ClassificationSource;
  classificationConfidence: number;
  needsCategoryReview: boolean;
  matchedRuleId?: string;
  matchedPatternDescription?: string;
  suggestedCategory?: string;
}

/**
 * Priority classification engine for SpendWise:
 * 1. Explicit User rule
 * 2. Strong historical/pattern match (including conservative Salary pattern)
 * 3. Otherwise Ask User / Other (Immediate Thu/Chi, needsCategoryReview = true)
 *
 * Strictly frontend logic - no external AI or API calls.
 */
export function classifyCandidateTransaction(
  params: ClassifyCandidateParams
): ClassificationResult {
  const {
    amount,
    direction,
    title = '',
    description = '',
    counterparty = '',
    date = new Date().toISOString(),
    existingTransactions = [],
    rules = [],
    categories = [],
  } = params;

  const candidateText = `${title} ${description} ${counterparty}`.toLowerCase().trim();
  const absAmount = Math.abs(amount);

  // -------------------------------------------------------------
  // PRIORITY 1: Explicit User Rule
  // User rules always take absolute precedence.
  // -------------------------------------------------------------
  const activeRules = [...rules]
    .filter((r) => r.isActive && r.descriptionPattern && r.descriptionPattern.trim() !== '')
    .sort((a, b) => (b.priority ?? 0) - (a.priority ?? 0));

  for (const rule of activeRules) {
    const pattern = rule.descriptionPattern.toLowerCase().trim();
    if (pattern && candidateText.includes(pattern)) {
      // Find matching category object
      const matchedCat = categories.find(
        (c) => c.id === rule.targetCategoryId || c.key === rule.targetCategoryId
      );

      // INVARIANT: If an explicit Rule targets a category whose kind conflicts with
      // transaction direction, ignore that rule for this transaction, continue classification,
      // and do NOT change semanticType to match the wrong category.
      if (matchedCat) {
        if (direction === 'credit' && matchedCat.kind === 'expense') {
          continue;
        }
        if (direction === 'debit' && matchedCat.kind === 'income') {
          continue;
        }
      }

      const targetCategoryKey = matchedCat?.key || rule.targetCategoryId;
      const semanticType: TransactionSemanticType = direction === 'credit' ? 'income' : 'expense';

      return {
        direction,
        semanticType,
        category: targetCategoryKey,
        classificationSource: 'rule',
        classificationConfidence: 1.0,
        needsCategoryReview: false,
        matchedRuleId: rule.id,
      };
    }
  }

  // -------------------------------------------------------------
  // PRIORITY 2: Strong Historical / Pattern Match
  // Includes conservative frontend-demo salary pattern & counterparty patterns
  // -------------------------------------------------------------
  if (direction === 'credit') {
    // 2A. Conservative Frontend-Demo Salary Pattern
    // Rules:
    // - Do NOT assume company account = salary
    // - Do NOT assume same sender = salary
    // - Do NOT assume similar amount alone = salary
    // - Must use genuinely User-confirmed salary history (manual/rule/manual entry)
    // - Do NOT consider status === 'completed' as User confirmation
    // - Must have confirmed Salary records across at least 2 distinct months
    // - Signals: incoming direction, similar monthly timing, reasonable amount variation range.
    //   Sender/counterparty strengthens confidence but remains optional.
    const confirmedSalaries = existingTransactions.filter((tx) => {
      const isSalaryCat =
        tx.category === 'categories.salary' ||
        tx.category === 'cat_inc_01' ||
        tx.category === 'salary' ||
        tx.category === 'Lương & Thưởng';
      // User-confirmed classification only:
      const isConfirmed =
        tx.classificationSource === 'manual' ||
        tx.classificationSource === 'rule' ||
        tx.isManualEntry === true;
      const isCredit =
        tx.direction === 'credit' ||
        tx.semanticType === 'income' ||
        tx.type === 'income' ||
        tx.amount > 0;
      return isSalaryCat && isConfirmed && isCredit && Math.abs(tx.amount) > 0;
    });

    // Extract distinct calendar months (YYYY-MM)
    const distinctMonths = new Set<string>();
    confirmedSalaries.forEach((tx) => {
      const d = new Date(tx.date || tx.rawDate || tx.occurredAt || '');
      if (!isNaN(d.getTime())) {
        const monthKey = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
        distinctMonths.add(monthKey);
      }
    });

    // Only apply salary pattern if there are confirmed salaries across >= 2 different months
    if (distinctMonths.size >= 2) {
      const salaryAmounts = confirmedSalaries.map((tx) => Math.abs(tx.amount));
      const minSalary = Math.min(...salaryAmounts);
      const maxSalary = Math.max(...salaryAmounts);

      // Meaningful salary variation: within 0.70x min to 1.35x max (accounting for bonuses/OT/deductions)
      const isAmountInRange =
        absAmount >= Math.max(1000000, minSalary * 0.7) &&
        absAmount <= maxSalary * 1.35;

      // Timing check: candidate day of month
      const candidateDate = new Date(date);
      const candidateDay = !isNaN(candidateDate.getTime()) ? candidateDate.getDate() : 1;

      const pastDays = confirmedSalaries.map((tx) => {
        const d = new Date(tx.date || tx.rawDate || tx.occurredAt || '');
        return !isNaN(d.getTime()) ? d.getDate() : 1;
      });

      // Check if within ±4 days of any past salary payout day (or near end-of-month / beginning of month)
      const isTimingSimilar = pastDays.some((pDay) => {
        const diff = Math.abs(candidateDay - pDay);
        return diff <= 4 || diff >= 27;
      });

      if (isAmountInRange && isTimingSimilar) {
        // Check counterparty match to strengthen confidence (optional)
        const pastSenders = confirmedSalaries
          .map((tx) => (tx.counterparty || tx.title || '').toLowerCase().trim())
          .filter(Boolean);
        const candidateSender = counterparty.toLowerCase().trim();
        const hasMatchingSender =
          candidateSender &&
          pastSenders.some((s) => s.includes(candidateSender) || candidateSender.includes(s));

        // Check explicit salary keywords in description/title
        const hasSalaryKeyword =
          candidateText.includes('luong') ||
          candidateText.includes('lương') ||
          candidateText.includes('salary') ||
          candidateText.includes('payroll') ||
          candidateText.includes('thuong') ||
          candidateText.includes('thưởng');

        let confidence = 0.75;
        if (hasMatchingSender) confidence += 0.12;
        if (hasSalaryKeyword) confidence += 0.10;

        // Cap confidence at 0.98
        confidence = Math.min(0.98, confidence);

        // "Prefer suggestion/review when confidence is not very high."
        const needsReview = confidence < 0.90;

        return {
          direction: 'credit',
          semanticType: 'income',
          category: 'categories.salary',
          classificationSource: 'pattern',
          classificationConfidence: confidence,
          needsCategoryReview: needsReview,
          suggestedCategory: 'categories.salary',
          matchedPatternDescription: hasMatchingSender
            ? 'Khớp mô hình lương định kỳ và đơn vị chi trả từ lịch sử'
            : 'Khớp mô hình lương định kỳ theo chu kỳ ngày và khoảng tiền',
        };
      }
    }
  }

  // 2B. Generic Counterparty Pattern Match (across >= 2 distinct completed transactions)
  if (counterparty && counterparty.trim().length >= 3) {
    const cleanCounterparty = counterparty.toLowerCase().trim();
    const pastMatching = existingTransactions.filter((tx) => {
      const pastCp = (tx.counterparty || '').toLowerCase().trim();
      return (
        pastCp &&
        (pastCp === cleanCounterparty ||
          pastCp.includes(cleanCounterparty) ||
          cleanCounterparty.includes(pastCp)) &&
        tx.category &&
        tx.category !== 'categories.others' &&
        tx.category !== 'categories.income_other' &&
        tx.category !== 'categories.expense_other' &&
        tx.status === 'completed'
      );
    });

    if (pastMatching.length >= 2) {
      // Find category with highest frequency
      const catCounts: Record<string, number> = {};
      pastMatching.forEach((tx) => {
        catCounts[tx.category] = (catCounts[tx.category] || 0) + 1;
      });

      let topCategory = '';
      let maxCount = 0;
      for (const [cat, count] of Object.entries(catCounts)) {
        if (count > maxCount) {
          maxCount = count;
          topCategory = cat;
        }
      }

      if (topCategory && maxCount >= 2) {
        const ratio = maxCount / pastMatching.length;
        if (ratio >= 0.75) {
          const matchedCat = categories.find((c) => c.key === topCategory || c.id === topCategory);
          // Direction must match category kind
          const kindMatches =
            !matchedCat ||
            (direction === 'credit' && matchedCat.kind === 'income') ||
            (direction === 'debit' && matchedCat.kind === 'expense');

          if (kindMatches) {
            const semanticType: TransactionSemanticType = direction === 'credit' ? 'income' : 'expense';

            return {
              direction,
              semanticType,
              category: topCategory,
              classificationSource: 'pattern',
              classificationConfidence: 0.82,
              needsCategoryReview: true, // Suggest to user with review option
              suggestedCategory: topCategory,
              matchedPatternDescription: `Khớp thói quen chi tiêu từ đối tác "${counterparty}"`,
            };
          }
        }
      }
    }
  }

  // -------------------------------------------------------------
  // PRIORITY 3: Otherwise Ask User / Other
  // Unclassified transactions immediately count as Thu/Chi!
  // -------------------------------------------------------------
  if (direction === 'credit') {
    return {
      direction: 'credit',
      semanticType: 'income',
      category: 'categories.income_other',
      classificationSource: 'system',
      classificationConfidence: 0.2,
      needsCategoryReview: true,
    };
  }

  return {
    direction: 'debit',
    semanticType: 'expense',
    category: 'categories.expense_other',
    classificationSource: 'system',
    classificationConfidence: 0.2,
    needsCategoryReview: true,
  };
}

/**
 * Creates the exact required User notification payload for an unclassified transaction:
 * "Giao dịch đã được ghi nhận vào lịch sử và thống kê nhưng cần bạn chọn hạng mục chính xác."
 */
export function buildReviewNotification(tx: Transaction): Partial<NotificationItem> {
  return {
    id: `notif_review_${tx.id}`,
    userId: tx.userId,
    title: 'Cần phân loại giao dịch',
    message:
      'Giao dịch đã được ghi nhận vào lịch sử và thống kê nhưng cần bạn chọn hạng mục chính xác.',
    titleKey: 'notifications.reviewRequiredTitle',
    messageKey: 'notifications.reviewRequiredMessage',
    time: 'Vừa xong',
    timestamp: Date.now(),
    isRead: false,
    type: 'payment',
    category: 'transaction',
    amountChange: tx.amount,
    accountId: tx.walletId,
    accountName: tx.walletName,
    actionRoute: `/transactions?reviewTx=${tx.id}`,
    actionLabelKey: 'notifications.actionClassifyNow',
    relatedTransactionId: tx.id,
  };
}
