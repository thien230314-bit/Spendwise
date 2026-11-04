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
import { router } from 'expo-router';
import { AppScreen } from '@/components/AppScreen';
import { GlassCard } from '@/components/GlassCard';
import { useTheme } from '@/theme/useTheme';
import { radius, spacing } from '@/theme/tokens';

export interface WalletItem {
  id: string;
  name: string;
  type: 'primary' | 'sub';
  balance: number;
  accountNumber: string;
  isDefault: boolean;
}

export default function WalletScreen() {
  const { colors, isDark } = useTheme();

  // Balance Scopes (Strictly separated per SpendWise rules)
  const [liquidBalance, setLiquidBalance] = useState(14250000); // Khả dụng chi tiêu
  const [heldBalance] = useState(3000000);       // Tạm giữ (Fixed Fund holds / transfers)
  const [savingsBalance] = useState(50000000); // Tiết kiệm cá nhân
  const [groupFundCapital] = useState(15500000); // Vốn góp quỹ nhóm

  // Personal Wallets
  const [wallets, setWallets] = useState<WalletItem[]>([
    {
      id: 'w-1',
      name: 'Ví chi tiêu chính',
      type: 'primary',
      balance: 11250000,
      accountNumber: 'SW-W1-982401',
      isDefault: true,
    },
    {
      id: 'w-2',
      name: 'Ví phụ: Ăn uống & Cà phê',
      type: 'sub',
      balance: 3000000,
      accountNumber: 'SW-W2-982402',
      isDefault: false,
    },
  ]);

  // Modal states
  const [isCreateWalletModalOpen, setIsCreateWalletModalOpen] = useState(false);
  const [newWalletName, setNewWalletName] = useState('');
  const [isDepositModalOpen, setIsDepositModalOpen] = useState(false);
  const [depositAmount, setDepositAmount] = useState('');

  const handleCreateSubWallet = () => {
    if (wallets.filter((w) => w.type === 'sub').length >= 2) {
      Alert.alert('Giới hạn', 'Mỗi tài khoản được phép tạo tối đa 2 ví phụ.');
      return;
    }
    if (!newWalletName.trim()) {
      Alert.alert('Lỗi', 'Vui lòng nhập tên ví');
      return;
    }
    const newWallet: WalletItem = {
      id: `w-${Date.now()}`,
      name: newWalletName.trim(),
      type: 'sub',
      balance: 0,
      accountNumber: `SW-W${wallets.length + 1}-${Math.floor(100000 + Math.random() * 900000)}`,
      isDefault: false,
    };
    setWallets([...wallets, newWallet]);
    setIsCreateWalletModalOpen(false);
    setNewWalletName('');
    Alert.alert('Thành công', `Đã tạo ví phụ "${newWallet.name}".`);
  };

  const handleDeposit = () => {
    const amt = parseInt(depositAmount.replace(/\D/g, ''), 10);
    if (!amt || amt <= 0) {
      Alert.alert('Lỗi', 'Vui lòng nhập số tiền nạp');
      return;
    }
    setLiquidBalance((prev) => prev + amt);
    setWallets((prev) =>
      prev.map((w) => (w.isDefault ? { ...w, balance: w.balance + amt } : w))
    );
    setIsDepositModalOpen(false);
    setDepositAmount('');
    Alert.alert('Thành công', `Đã nạp ${amt.toLocaleString('vi-VN')} ₫ vào Ví chi tiêu chính.`);
  };

  return (
    <AppScreen title="Tất cả ví & Tài sản" scrollable>
      <View style={styles.container}>
        {/* BACK TO HOME NAVIGATOR BAR */}
        <Pressable
          onPress={() => router.replace('/(authenticated)/home')}
          style={styles.backRow}
        >
          <Ionicons name="arrow-back" size={20} color={colors.primary} />
          <Text style={[styles.backText, { color: colors.primary }]}>Quay lại Trang chủ</Text>
        </Pressable>

        {/* 1. HERO LIQUID SPENDABLE CARD */}
        <GlassCard style={styles.heroCard}>
          <View style={styles.heroHeader}>
            <View style={styles.heroStatusDot} />
            <Text style={[styles.heroHeaderTitle, { color: colors.textMuted }]}>
              Tổng tiền khả dụng chi tiêu ngay
            </Text>
          </View>

          <Text style={[styles.heroAmount, { color: colors.primary }]}>
            {liquidBalance.toLocaleString('vi-VN')} ₫
          </Text>

          <Text style={[styles.heroSub, { color: colors.textSecondary }]}>
            Bao gồm số dư trên Ví chính và các ví phụ cá nhân
          </Text>

          {/* Quick Actions Row */}
          <View style={styles.heroActionRow}>
            <Pressable
              onPress={() => setIsDepositModalOpen(true)}
              style={[styles.heroActionBtn, { backgroundColor: colors.primary }]}
            >
              <Ionicons name="arrow-down" size={18} color="#FFFFFF" />
              <Text style={styles.heroActionBtnText}>Nạp tiền</Text>
            </Pressable>

            <Pressable
              onPress={() => router.push('/(authenticated)/transfer')}
              style={[styles.heroActionBtnOutline, { borderColor: colors.primary }]}
            >
              <Ionicons name="paper-plane" size={18} color={colors.primary} />
              <Text style={[styles.heroActionBtnOutlineText, { color: colors.primary }]}>Chuyển tiền</Text>
            </Pressable>
          </View>
        </GlassCard>

        {/* 2. NON-LIQUID SCOPE BREAKDOWN (NO DOUBLE-COUNTING) */}
        <View style={styles.sectionWrap}>
          <Text style={[styles.sectionTitle, { color: colors.text }]}>
            Phân bổ tài sản & Vốn (Không gộp lẫn)
          </Text>

          <View style={styles.scopeGrid}>
            {/* Held Balance */}
            <GlassCard style={styles.scopeCell}>
              <View style={[styles.scopeIconBg, { backgroundColor: isDark ? '#382A14' : '#FEF3C7' }]}>
                <Ionicons name="lock-closed-outline" size={18} color={colors.amber} />
              </View>
              <Text style={[styles.scopeLabel, { color: colors.textMuted }]}>Tạm giữ</Text>
              <Text style={[styles.scopeAmount, { color: colors.amber }]}>
                {heldBalance.toLocaleString('vi-VN')} ₫
              </Text>
              <Text style={[styles.scopeDesc, { color: colors.textMuted }]}>Đang giữ cho đề xuất chi quỹ</Text>
            </GlassCard>

            {/* Savings Balance */}
            <GlassCard style={styles.scopeCell}>
              <View style={[styles.scopeIconBg, { backgroundColor: isDark ? '#0F3D2E' : '#E6F8F0' }]}>
                <Ionicons name="shield-outline" size={18} color={colors.primary} />
              </View>
              <Text style={[styles.scopeLabel, { color: colors.textMuted }]}>Tiết kiệm</Text>
              <Text style={[styles.scopeAmount, { color: colors.primary }]}>
                {savingsBalance.toLocaleString('vi-VN')} ₫
              </Text>
              <Text style={[styles.scopeDesc, { color: colors.textMuted }]}>Mục tiêu dài hạn</Text>
            </GlassCard>

            {/* Group Fund Capital */}
            <GlassCard style={styles.scopeCellWide}>
              <View style={[styles.scopeIconBg, { backgroundColor: isDark ? '#2E1A47' : '#EDE9FE' }]}>
                <Ionicons name="people-outline" size={18} color={colors.violet} />
              </View>
              <View style={{ flex: 1 }}>
                <Text style={[styles.scopeLabel, { color: colors.textMuted }]}>Vốn tại các Quỹ nhóm</Text>
                <Text style={[styles.scopeAmount, { color: colors.violet }]}>
                  {groupFundCapital.toLocaleString('vi-VN')} ₫
                </Text>
                <Text style={[styles.scopeDesc, { color: colors.textMuted }]}>
                  Thuộc Quỹ Du lịch Đà Nẵng & Quỹ Bạn Thân
                </Text>
              </View>
            </GlassCard>
          </View>
        </View>

        {/* 3. PERSONAL WALLETS LIST */}
        <View style={styles.sectionWrap}>
          <View style={styles.sectionHeaderRow}>
            <Text style={[styles.sectionTitle, { color: colors.text }]}>Danh sách ví cá nhân</Text>
            {wallets.filter((w) => w.type === 'sub').length < 2 && (
              <Pressable
                onPress={() => setIsCreateWalletModalOpen(true)}
                style={({ pressed }) => [{ opacity: pressed ? 0.7 : 1 }]}
              >
                <Text style={[styles.addWalletLink, { color: colors.primary }]}>+ Tạo ví phụ</Text>
              </Pressable>
            )}
          </View>

          <View style={styles.walletListWrap}>
            {wallets.map((w) => (
              <GlassCard key={w.id} style={styles.walletCard}>
                <View style={styles.walletCardHeader}>
                  <View style={styles.walletNameRow}>
                    <Ionicons
                      name={w.type === 'primary' ? 'wallet' : 'folder-outline'}
                      size={20}
                      color={colors.primary}
                    />
                    <Text style={[styles.walletName, { color: colors.text }]}>{w.name}</Text>
                  </View>

                  {w.isDefault && (
                    <View style={[styles.defaultBadge, { backgroundColor: colors.primarySoft }]}>
                      <Text style={[styles.defaultBadgeText, { color: colors.primary }]}>Ví mặc định</Text>
                    </View>
                  )}
                </View>

                <View style={styles.walletCardBody}>
                  <Text style={[styles.walletBalance, { color: colors.text }]}>
                    {w.balance.toLocaleString('vi-VN')} ₫
                  </Text>
                  <Text style={[styles.walletAccountNo, { color: colors.textMuted }]}>
                    Số tài khoản: {w.accountNumber}
                  </Text>
                </View>
              </GlassCard>
            ))}
          </View>
        </View>

        {/* 4. LINKED BANK ACCOUNT */}
        <View style={styles.sectionWrap}>
          <Text style={[styles.sectionTitle, { color: colors.text }]}>Tài khoản ngân hàng liên kết</Text>
          <GlassCard style={styles.bankCard}>
            <View style={styles.bankRow}>
              <View style={[styles.bankIconBg, { backgroundColor: '#005538' }]}>
                <Ionicons name="business" size={20} color="#FFFFFF" />
              </View>
              <View style={{ flex: 1 }}>
                <Text style={[styles.bankName, { color: colors.text }]}>Vietcombank (VCB)</Text>
                <Text style={[styles.bankAccountNo, { color: colors.textMuted }]}>
                  **** **** **** 9824 • Nguyễn Văn A
                </Text>
              </View>
              <View style={[styles.bankConnectedBadge, { backgroundColor: colors.primarySoft }]}>
                <Ionicons name="checkmark-circle" size={14} color={colors.primary} />
                <Text style={[styles.bankConnectedText, { color: colors.primary }]}>Đã liên kết</Text>
              </View>
            </View>
          </GlassCard>
        </View>

        {/* CREATE SUB-WALLET MODAL */}
        <Modal
          visible={isCreateWalletModalOpen}
          transparent
          animationType="slide"
          onRequestClose={() => setIsCreateWalletModalOpen(false)}
        >
          <View style={styles.modalOverlay}>
            <View style={[styles.modalSheet, { backgroundColor: colors.surface }]}>
              <View style={styles.modalHeader}>
                <Text style={[styles.modalTitle, { color: colors.text }]}>Tạo ví phụ cá nhân</Text>
                <Pressable onPress={() => setIsCreateWalletModalOpen(false)}>
                  <Ionicons name="close" size={24} color={colors.text} />
                </Pressable>
              </View>

              <View style={styles.modalBody}>
                <Text style={[styles.modalLabel, { color: colors.text }]}>Tên ví phụ</Text>
                <TextInput
                  value={newWalletName}
                  onChangeText={setNewWalletName}
                  placeholder="Ví dụ: Ví Mua sắm / Ví Tiết kiệm hè"
                  placeholderTextColor={colors.placeholder}
                  style={[styles.modalInput, { backgroundColor: colors.surfaceElevated, borderColor: colors.border, color: colors.text }]}
                />

                <Text style={[styles.modalHint, { color: colors.textMuted }]}>
                  Ví phụ giúp phân tách các khoản tiền chi tiêu định kỳ khỏi ví chính.
                </Text>

                <Pressable
                  onPress={handleCreateSubWallet}
                  style={[styles.modalSubmitBtn, { backgroundColor: colors.primary }]}
                >
                  <Text style={styles.modalSubmitText}>Tạo ví ngay</Text>
                </Pressable>
              </View>
            </View>
          </View>
        </Modal>

        {/* DEPOSIT MODAL */}
        <Modal
          visible={isDepositModalOpen}
          transparent
          animationType="slide"
          onRequestClose={() => setIsDepositModalOpen(false)}
        >
          <View style={styles.modalOverlay}>
            <View style={[styles.modalSheet, { backgroundColor: colors.surface }]}>
              <View style={styles.modalHeader}>
                <Text style={[styles.modalTitle, { color: colors.text }]}>Nạp tiền vào Ví chính</Text>
                <Pressable onPress={() => setIsDepositModalOpen(false)}>
                  <Ionicons name="close" size={24} color={colors.text} />
                </Pressable>
              </View>

              <View style={styles.modalBody}>
                <Text style={[styles.modalLabel, { color: colors.text }]}>Số tiền nạp (₫)</Text>
                <TextInput
                  value={depositAmount}
                  onChangeText={setDepositAmount}
                  keyboardType="numeric"
                  placeholder="Ví dụ: 2000000"
                  placeholderTextColor={colors.placeholder}
                  style={[styles.modalInput, { backgroundColor: colors.surfaceElevated, borderColor: colors.border, color: colors.text }]}
                />

                <Pressable
                  onPress={handleDeposit}
                  style={[styles.modalSubmitBtn, { backgroundColor: colors.primary }]}
                >
                  <Text style={styles.modalSubmitText}>Xác nhận nạp</Text>
                </Pressable>
              </View>
            </View>
          </View>
        </Modal>
      </View>
    </AppScreen>
  );
}

const styles = StyleSheet.create({
  container: {
    padding: spacing.md,
    gap: spacing.md,
    paddingBottom: 40,
  },
  backRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    paddingVertical: 6,
  },
  backText: {
    fontSize: 14,
    fontWeight: '700',
  },
  heroCard: {
    padding: spacing.lg,
  },
  heroHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  heroStatusDot: {
    width: 8,
    height: 8,
    borderRadius: 4,
    backgroundColor: '#34D399',
  },
  heroHeaderTitle: {
    fontSize: 13,
    fontWeight: '600',
  },
  heroAmount: {
    fontSize: 32,
    fontWeight: '800',
    marginVertical: 10,
  },
  heroSub: {
    fontSize: 12,
  },
  heroActionRow: {
    flexDirection: 'row',
    gap: 12,
    marginTop: spacing.md,
  },
  heroActionBtn: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 6,
    paddingVertical: 12,
    borderRadius: radius.lg,
  },
  heroActionBtnText: {
    color: '#FFFFFF',
    fontWeight: '700',
    fontSize: 14,
  },
  heroActionBtnOutline: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 6,
    paddingVertical: 12,
    borderRadius: radius.lg,
    borderWidth: 1.5,
  },
  heroActionBtnOutlineText: {
    fontWeight: '700',
    fontSize: 14,
  },
  sectionWrap: {
    gap: 8,
    marginTop: spacing.xs,
  },
  sectionHeaderRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  sectionTitle: {
    fontSize: 16,
    fontWeight: '700',
  },
  addWalletLink: {
    fontSize: 13,
    fontWeight: '700',
  },
  scopeGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 10,
  },
  scopeCell: {
    flex: 1,
    minWidth: '45%',
    padding: 12,
    gap: 4,
  },
  scopeCellWide: {
    width: '100%',
    flexDirection: 'row',
    alignItems: 'center',
    padding: 12,
    gap: 12,
  },
  scopeIconBg: {
    width: 32,
    height: 32,
    borderRadius: 16,
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 4,
  },
  scopeLabel: {
    fontSize: 11,
    fontWeight: '600',
  },
  scopeAmount: {
    fontSize: 17,
    fontWeight: '800',
  },
  scopeDesc: {
    fontSize: 11,
    marginTop: 2,
  },
  walletListWrap: {
    gap: 10,
  },
  walletCard: {
    padding: spacing.md,
    gap: 8,
  },
  walletCardHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  walletNameRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  walletName: {
    fontSize: 15,
    fontWeight: '700',
  },
  defaultBadge: {
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: radius.sm,
  },
  defaultBadgeText: {
    fontSize: 11,
    fontWeight: '700',
  },
  walletCardBody: {
    marginTop: 4,
  },
  walletBalance: {
    fontSize: 20,
    fontWeight: '800',
  },
  walletAccountNo: {
    fontSize: 12,
    marginTop: 2,
  },
  bankCard: {
    padding: spacing.md,
  },
  bankRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
  },
  bankIconBg: {
    width: 40,
    height: 40,
    borderRadius: 20,
    alignItems: 'center',
    justifyContent: 'center',
  },
  bankName: {
    fontSize: 14,
    fontWeight: '700',
  },
  bankAccountNo: {
    fontSize: 12,
    marginTop: 2,
  },
  bankConnectedBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: radius.pill,
  },
  bankConnectedText: {
    fontSize: 11,
    fontWeight: '700',
  },
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.5)',
    justifyContent: 'flex-end',
  },
  modalSheet: {
    borderTopLeftRadius: radius.panel,
    borderTopRightRadius: radius.panel,
    padding: spacing.lg,
    paddingBottom: 36,
  },
  modalHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: spacing.md,
  },
  modalTitle: {
    fontSize: 18,
    fontWeight: '800',
  },
  modalBody: {
    gap: 8,
  },
  modalLabel: {
    fontSize: 13,
    fontWeight: '600',
  },
  modalInput: {
    height: 48,
    borderRadius: radius.md,
    borderWidth: 1,
    paddingHorizontal: 14,
    fontSize: 15,
  },
  modalHint: {
    fontSize: 12,
  },
  modalSubmitBtn: {
    marginTop: 16,
    paddingVertical: 14,
    borderRadius: radius.lg,
    alignItems: 'center',
  },
  modalSubmitText: {
    color: '#FFFFFF',
    fontSize: 15,
    fontWeight: '700',
  },
});
