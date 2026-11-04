import React, { useState } from 'react';
import {
  StyleSheet,
  Text,
  View,
  Pressable,
  TextInput,
  Modal,
  Alert,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { AppScreen } from '@/components/AppScreen';
import { GlassCard } from '@/components/GlassCard';
import { useTheme } from '@/theme/useTheme';
import { radius, spacing, typography } from '@/theme/tokens';

export interface TransactionItem {
  id: string;
  title: string;
  category: string;
  amount: number;
  type: 'income' | 'expense' | 'fund_contribution' | 'fund_spend';
  timestamp: string;
  walletName: string;
  notes?: string;
  status: 'completed' | 'pending' | 'failed';
  code: string;
}

export default function HistoryScreen() {
  const { colors, isDark } = useTheme();

  // Filters & Search
  const [filterType, setFilterType] = useState<'all' | 'income' | 'expense' | 'fund'>('all');
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedTx, setSelectedTx] = useState<TransactionItem | null>(null);

  // Realistic sample history matching SpendWise User Ledger
  const transactions: TransactionItem[] = [
    {
      id: 'tx-101',
      title: 'Lương tháng 10/2026',
      category: 'Thu nhập chính',
      amount: 35000000,
      type: 'income',
      timestamp: '01/11/2026 09:30',
      walletName: 'Ví chi tiêu chính',
      notes: 'Chuyển khoản lương TechCorp',
      status: 'completed',
      code: 'SW-TX-782410',
    },
    {
      id: 'tx-102',
      title: 'Đóng góp Quỹ Du lịch Đà Nẵng',
      category: 'Quỹ nhóm',
      amount: -1500000,
      type: 'fund_contribution',
      timestamp: '02/11/2026 14:15',
      walletName: 'Ví chi tiêu chính',
      notes: 'Đóng kỳ 2/3 Quỹ Đà Nẵng',
      status: 'completed',
      code: 'SW-TX-782411',
    },
    {
      id: 'tx-103',
      title: 'Siêu thị WinMart Times City',
      category: 'Ăn uống & Nhu yếu phẩm',
      amount: -845000,
      type: 'expense',
      timestamp: '03/11/2026 18:45',
      walletName: 'Ví chi tiêu chính',
      notes: 'Thực phẩm tuần đầu tháng',
      status: 'completed',
      code: 'SW-TX-782412',
    },
    {
      id: 'tx-104',
      title: 'Hoàn tiền vé xem phim CGV',
      category: 'Khác',
      amount: 220000,
      type: 'income',
      timestamp: '03/11/2026 21:00',
      walletName: 'Ví phụ',
      notes: 'Huỷ đặt vé 2 người',
      status: 'completed',
      code: 'SW-TX-782413',
    },
    {
      id: 'tx-105',
      title: 'Cà phê Highland Coffee',
      category: 'Ăn uống',
      amount: -65000,
      type: 'expense',
      timestamp: '04/11/2026 08:15',
      walletName: 'Ví chi tiêu chính',
      notes: 'Cà phê sáng cùng đồng nghiệp',
      status: 'completed',
      code: 'SW-TX-782414',
    },
    {
      id: 'tx-106',
      title: 'Ăn trưa Quỹ Team',
      category: 'Quỹ nhóm',
      amount: -350000,
      type: 'fund_spend',
      timestamp: '04/11/2026 12:30',
      walletName: 'Quỹ Ăn trưa Công ty',
      notes: 'Thanh toán bữa trưa văn phòng',
      status: 'completed',
      code: 'SW-TX-782415',
    },
  ];

  const filtered = transactions.filter((tx) => {
    if (filterType === 'income' && tx.type !== 'income') return false;
    if (filterType === 'expense' && tx.type !== 'expense') return false;
    if (filterType === 'fund' && tx.type !== 'fund_contribution' && tx.type !== 'fund_spend') return false;
    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase();
      return (
        tx.title.toLowerCase().includes(q) ||
        tx.category.toLowerCase().includes(q) ||
        tx.code.toLowerCase().includes(q) ||
        (tx.notes && tx.notes.toLowerCase().includes(q))
      );
    }
    return true;
  });

  const totalInflow = filtered
    .filter((t) => t.amount > 0)
    .reduce((acc, cur) => acc + cur.amount, 0);

  const totalOutflow = filtered
    .filter((t) => t.amount < 0)
    .reduce((acc, cur) => acc + Math.abs(cur.amount), 0);

  return (
    <AppScreen title="Lịch sử giao dịch" scrollable>
      <View style={styles.container}>
        {/* 1. SEARCH BAR */}
        <View style={[styles.searchBox, { backgroundColor: colors.surface, borderColor: colors.border }]}>
          <Ionicons name="search" size={20} color={colors.textMuted} />
          <TextInput
            value={searchQuery}
            onChangeText={setSearchQuery}
            placeholder="Tìm theo nội dung, mã GD, phân loại..."
            placeholderTextColor={colors.placeholder}
            style={[styles.searchInput, { color: colors.text }]}
          />
          {searchQuery.length > 0 && (
            <Pressable onPress={() => setSearchQuery('')}>
              <Ionicons name="close-circle" size={18} color={colors.textMuted} />
            </Pressable>
          )}
        </View>

        {/* 2. CASHFLOW SUMMARY PILL */}
        <GlassCard style={styles.flowCard}>
          <View style={styles.flowItem}>
            <View style={[styles.flowIconBg, { backgroundColor: isDark ? '#0F3D2E' : '#E6F8F0' }]}>
              <Ionicons name="arrow-down" size={16} color={colors.primary} />
            </View>
            <View>
              <Text style={[styles.flowLabel, { color: colors.textMuted }]}>Tổng tiền vào</Text>
              <Text style={[styles.flowValue, { color: colors.primary }]}>
                +{totalInflow.toLocaleString('vi-VN')} ₫
              </Text>
            </View>
          </View>

          <View style={[styles.flowDivider, { backgroundColor: colors.border }]} />

          <View style={styles.flowItem}>
            <View style={[styles.flowIconBg, { backgroundColor: isDark ? '#3D1518' : '#FDE8E9' }]}>
              <Ionicons name="arrow-up" size={16} color={colors.danger} />
            </View>
            <View>
              <Text style={[styles.flowLabel, { color: colors.textMuted }]}>Tổng tiền ra</Text>
              <Text style={[styles.flowValue, { color: colors.danger }]}>
                -{totalOutflow.toLocaleString('vi-VN')} ₫
              </Text>
            </View>
          </View>
        </GlassCard>

        {/* 3. FILTER SEGMENTS */}
        <View style={[styles.segmentContainer, { backgroundColor: isDark ? colors.surfaceElevated : '#DCEEE4' }]}>
          <Pressable
            onPress={() => setFilterType('all')}
            style={[styles.segmentBtn, filterType === 'all' && [styles.segmentBtnActive, { backgroundColor: colors.surface }]]}
          >
            <Text style={[styles.segmentText, { color: filterType === 'all' ? colors.primary : colors.textMuted }]}>
              Tất cả
            </Text>
          </Pressable>
          <Pressable
            onPress={() => setFilterType('income')}
            style={[styles.segmentBtn, filterType === 'income' && [styles.segmentBtnActive, { backgroundColor: colors.surface }]]}
          >
            <Text style={[styles.segmentText, { color: filterType === 'income' ? colors.primary : colors.textMuted }]}>
              Tiền vào
            </Text>
          </Pressable>
          <Pressable
            onPress={() => setFilterType('expense')}
            style={[styles.segmentBtn, filterType === 'expense' && [styles.segmentBtnActive, { backgroundColor: colors.surface }]]}
          >
            <Text style={[styles.segmentText, { color: filterType === 'expense' ? colors.primary : colors.textMuted }]}>
              Tiền ra
            </Text>
          </Pressable>
          <Pressable
            onPress={() => setFilterType('fund')}
            style={[styles.segmentBtn, filterType === 'fund' && [styles.segmentBtnActive, { backgroundColor: colors.surface }]]}
          >
            <Text style={[styles.segmentText, { color: filterType === 'fund' ? colors.primary : colors.textMuted }]}>
              Quỹ nhóm
            </Text>
          </Pressable>
        </View>

        {/* 4. TRANSACTION ROWS */}
        <View style={styles.txList}>
          {filtered.length === 0 ? (
            <View style={styles.emptyView}>
              <Ionicons name="receipt-outline" size={48} color={colors.textMuted} />
              <Text style={[styles.emptyTitle, { color: colors.text }]}>Không có giao dịch nào</Text>
              <Text style={[styles.emptySub, { color: colors.textMuted }]}>
                Thử đổi bộ lọc hoặc xóa từ khóa tìm kiếm.
              </Text>
            </View>
          ) : (
            filtered.map((tx) => {
              const isIncome = tx.amount > 0;
              const isFund = tx.type.startsWith('fund');

              return (
                <Pressable
                  key={tx.id}
                  onPress={() => setSelectedTx(tx)}
                  style={({ pressed }) => [{ opacity: pressed ? 0.8 : 1 }]}
                >
                  <GlassCard style={styles.txRow}>
                    {/* Icon */}
                    <View
                      style={[
                        styles.txIcon,
                        {
                          backgroundColor: isIncome
                            ? (isDark ? '#0F3D2E' : '#E6F8F0')
                            : isFund
                            ? (isDark ? '#2E1A47' : '#EDE9FE')
                            : (isDark ? '#3D1518' : '#FDE8E9'),
                        },
                      ]}
                    >
                      <Ionicons
                        name={
                          isIncome
                            ? 'arrow-down-outline'
                            : isFund
                            ? 'people-outline'
                            : 'arrow-up-outline'
                        }
                        size={20}
                        color={
                          isIncome
                            ? colors.primary
                            : isFund
                            ? colors.violet
                            : colors.danger
                        }
                      />
                    </View>

                    {/* Info */}
                    <View style={styles.txInfo}>
                      <Text style={[styles.txTitle, { color: colors.text }]} numberOfLines={1}>
                        {tx.title}
                      </Text>
                      <Text style={[styles.txMeta, { color: colors.textMuted }]}>
                        {tx.timestamp} • {tx.category}
                      </Text>
                    </View>

                    {/* Amount */}
                    <View style={styles.txAmountCol}>
                      <Text
                        style={[
                          styles.txAmount,
                          {
                            color: isIncome
                              ? colors.primary
                              : colors.text,
                          },
                        ]}
                      >
                        {isIncome ? '+' : ''}
                        {tx.amount.toLocaleString('vi-VN')} ₫
                      </Text>
                      <Text style={[styles.txWallet, { color: colors.textMuted }]}>
                        {tx.walletName}
                      </Text>
                    </View>
                  </GlassCard>
                </Pressable>
              );
            })
          )}
        </View>

        {/* 5. TRANSACTION DETAIL RECEIPT MODAL */}
        {selectedTx && (
          <Modal
            visible={Boolean(selectedTx)}
            animationType="slide"
            transparent
            onRequestClose={() => setSelectedTx(null)}
          >
            <View style={styles.modalOverlay}>
              <View style={[styles.receiptSheet, { backgroundColor: colors.surface }]}>
                {/* Header */}
                <View style={styles.receiptHeader}>
                  <Text style={[styles.receiptSheetTitle, { color: colors.text }]}>Chi tiết giao dịch</Text>
                  <Pressable onPress={() => setSelectedTx(null)} style={styles.closeBtn}>
                    <Ionicons name="close" size={24} color={colors.text} />
                  </Pressable>
                </View>

                {/* Amount Hero */}
                <View style={styles.receiptAmountHero}>
                  <Text style={[styles.receiptHeroAmount, { color: selectedTx.amount > 0 ? colors.primary : colors.text }]}>
                    {selectedTx.amount > 0 ? '+' : ''}
                    {selectedTx.amount.toLocaleString('vi-VN')} ₫
                  </Text>
                  <View style={[styles.statusTag, { backgroundColor: colors.primarySoft }]}>
                    <Ionicons name="checkmark-circle" size={14} color={colors.primary} />
                    <Text style={[styles.statusTagText, { color: colors.primary }]}>Thành công</Text>
                  </View>
                </View>

                {/* Receipt Details Table */}
                <View style={[styles.detailsTable, { borderColor: colors.border }]}>
                  <View style={styles.detailRow}>
                    <Text style={[styles.detailRowLabel, { color: colors.textMuted }]}>Mục tiêu / Tiêu đề</Text>
                    <Text style={[styles.detailRowValue, { color: colors.text }]}>{selectedTx.title}</Text>
                  </View>
                  <View style={styles.detailRow}>
                    <Text style={[styles.detailRowLabel, { color: colors.textMuted }]}>Mã giao dịch</Text>
                    <Text style={[styles.detailRowValue, { color: colors.text, fontFamily: typography.brand.fontFamily }]}>
                      {selectedTx.code}
                    </Text>
                  </View>
                  <View style={styles.detailRow}>
                    <Text style={[styles.detailRowLabel, { color: colors.textMuted }]}>Thời gian</Text>
                    <Text style={[styles.detailRowValue, { color: colors.text }]}>{selectedTx.timestamp}</Text>
                  </View>
                  <View style={styles.detailRow}>
                    <Text style={[styles.detailRowLabel, { color: colors.textMuted }]}>Nguồn tiền</Text>
                    <Text style={[styles.detailRowValue, { color: colors.text }]}>{selectedTx.walletName}</Text>
                  </View>
                  <View style={styles.detailRow}>
                    <Text style={[styles.detailRowLabel, { color: colors.textMuted }]}>Danh mục</Text>
                    <Text style={[styles.detailRowValue, { color: colors.text }]}>{selectedTx.category}</Text>
                  </View>
                  {selectedTx.notes && (
                    <View style={styles.detailRow}>
                      <Text style={[styles.detailRowLabel, { color: colors.textMuted }]}>Ghi chú</Text>
                      <Text style={[styles.detailRowValue, { color: colors.text }]}>{selectedTx.notes}</Text>
                    </View>
                  )}
                </View>

                {/* Dispute / Correction CTA */}
                <Pressable
                  onPress={() => {
                    Alert.alert(
                      'Yêu cầu hỗ trợ',
                      `Gửi khiếu nại hoặc đối soát cho giao dịch ${selectedTx.code}?`,
                      [
                        { text: 'Gửi yêu cầu', onPress: () => Alert.alert('Đã gửi', 'Đội ngũ hỗ trợ đã tiếp nhận yêu cầu đối soát.') },
                        { text: 'Hủy', style: 'cancel' },
                      ]
                    );
                  }}
                  style={[styles.disputeBtn, { borderColor: colors.amber }]}
                >
                  <Ionicons name="alert-circle-outline" size={18} color={colors.amber} />
                  <Text style={[styles.disputeBtnText, { color: colors.amber }]}>Báo cáo / Yêu cầu đối soát</Text>
                </Pressable>
              </View>
            </View>
          </Modal>
        )}
      </View>
    </AppScreen>
  );
}

const styles = StyleSheet.create({
  container: {
    padding: spacing.md,
    gap: spacing.md,
  },
  searchBox: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 12,
    height: 46,
    borderRadius: radius.md,
    borderWidth: 1,
    gap: 8,
  },
  searchInput: {
    flex: 1,
    fontSize: 14,
  },
  flowCard: {
    flexDirection: 'row',
    padding: spacing.md,
    justifyContent: 'space-between',
  },
  flowItem: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
  },
  flowIconBg: {
    width: 36,
    height: 36,
    borderRadius: 18,
    alignItems: 'center',
    justifyContent: 'center',
  },
  flowLabel: {
    fontSize: 11,
    fontWeight: '600',
  },
  flowValue: {
    fontSize: 15,
    fontWeight: '800',
    marginTop: 2,
  },
  flowDivider: {
    width: 1,
    height: '80%',
    alignSelf: 'center',
    marginHorizontal: 8,
  },
  segmentContainer: {
    flexDirection: 'row',
    borderRadius: radius.pill,
    padding: 4,
  },
  segmentBtn: {
    flex: 1,
    paddingVertical: 8,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: radius.pill,
  },
  segmentBtnActive: {
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.08,
    shadowRadius: 4,
    elevation: 2,
  },
  segmentText: {
    fontSize: 12,
    fontWeight: '700',
  },
  txList: {
    gap: 8,
  },
  txRow: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: 12,
    gap: 12,
  },
  txIcon: {
    width: 40,
    height: 40,
    borderRadius: 20,
    alignItems: 'center',
    justifyContent: 'center',
  },
  txInfo: {
    flex: 1,
  },
  txTitle: {
    fontSize: 14,
    fontWeight: '700',
  },
  txMeta: {
    fontSize: 12,
    marginTop: 2,
  },
  txAmountCol: {
    alignItems: 'flex-end',
  },
  txAmount: {
    fontSize: 15,
    fontWeight: '800',
  },
  txWallet: {
    fontSize: 11,
    marginTop: 2,
  },
  emptyView: {
    alignItems: 'center',
    paddingVertical: 40,
    gap: 8,
  },
  emptyTitle: {
    fontSize: 16,
    fontWeight: '700',
  },
  emptySub: {
    fontSize: 13,
  },
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.5)',
    justifyContent: 'flex-end',
  },
  receiptSheet: {
    borderTopLeftRadius: radius.panel,
    borderTopRightRadius: radius.panel,
    padding: spacing.lg,
    paddingBottom: 36,
  },
  receiptHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  receiptSheetTitle: {
    fontSize: 18,
    fontWeight: '800',
  },
  closeBtn: {
    padding: 4,
  },
  receiptAmountHero: {
    alignItems: 'center',
    marginVertical: spacing.lg,
    gap: 8,
  },
  receiptHeroAmount: {
    fontSize: 30,
    fontWeight: '800',
  },
  statusTag: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderRadius: radius.pill,
  },
  statusTagText: {
    fontSize: 12,
    fontWeight: '700',
  },
  detailsTable: {
    borderRadius: radius.md,
    borderWidth: 1,
    padding: 12,
    gap: 10,
    marginBottom: spacing.lg,
  },
  detailRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
  },
  detailRowLabel: {
    fontSize: 13,
  },
  detailRowValue: {
    fontSize: 13,
    fontWeight: '700',
  },
  disputeBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 6,
    paddingVertical: 14,
    borderRadius: radius.lg,
    borderWidth: 1.5,
  },
  disputeBtnText: {
    fontSize: 14,
    fontWeight: '700',
  },
});
