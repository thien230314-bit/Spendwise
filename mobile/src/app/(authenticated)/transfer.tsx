import React, { useState } from 'react';
import {
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
  Alert,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { router } from 'expo-router';
import { AppScreen } from '@/components/AppScreen';
import { GlassCard } from '@/components/GlassCard';
import { radius, spacing, typography } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

const QUICK_AMOUNTS = [100000, 200000, 500000, 1000000, 2000000];

const RECENT_RECIPIENTS = [
  { id: 'rec-1', name: 'An Nguyễn', spendId: 'SW-AN-8899', initials: 'AN' },
  { id: 'rec-2', name: 'Trần Mai', spendId: 'SW-MAI-1234', initials: 'TM' },
  { id: 'rec-3', name: 'Bảo Hoàng', spendId: 'SW-BAO-5678', initials: 'BH' },
];

export default function TransferScreen() {
  const { colors, isDark } = useTheme();
  const [recipient, setRecipient] = useState('');
  const [amount, setAmount] = useState('');
  const [note, setNote] = useState('');
  const [isSuccess, setIsSuccess] = useState(false);
  const [txCode, setTxCode] = useState('SW-TX-982415');

  const parsedAmount = parseInt(amount.replace(/\D/g, ''), 10) || 0;

  const handleSelectRecent = (rec: typeof RECENT_RECIPIENTS[0]) => {
    setRecipient(rec.spendId);
  };

  const handleTransfer = () => {
    if (!recipient.trim()) {
      Alert.alert('Lỗi', 'Vui lòng nhập người nhận hoặc SpendID');
      return;
    }
    if (parsedAmount <= 0) {
      Alert.alert('Lỗi', 'Vui lòng nhập số tiền hợp lệ');
      return;
    }
    Alert.alert(
      'Xác nhận chuyển tiền',
      `Chuyển ${parsedAmount.toLocaleString('vi-VN')} ₫ đến ${recipient}?`,
      [
        { text: 'Hủy', style: 'cancel' },
        {
          text: 'Xác nhận (Mã PIN)',
          onPress: () => {
            setTxCode(`SW-TX-${Math.floor(100000 + Math.random() * 900000)}`);
            setIsSuccess(true);
          },
        },
      ]
    );
  };

  if (isSuccess) {
    return (
      <AppScreen title="Chuyển tiền thành công" scrollable>
        <View style={styles.container}>
          <GlassCard style={styles.successCard}>
            <View style={[styles.successIconWrap, { backgroundColor: colors.primarySoft }]}>
              <Ionicons name="checkmark-circle" size={48} color={colors.primary} />
            </View>

            <Text style={[styles.successAmount, { color: colors.text }]}>
              -{parsedAmount.toLocaleString('vi-VN')} ₫
            </Text>

            <View style={[styles.successTable, { borderColor: colors.border }]}>
              <View style={styles.rowBetween}>
                <Text style={[styles.detailLabel, { color: colors.textMuted }]}>Người nhận</Text>
                <Text style={[styles.detailValue, { color: colors.text }]}>{recipient}</Text>
              </View>
              <View style={styles.rowBetween}>
                <Text style={[styles.detailLabel, { color: colors.textMuted }]}>Thời gian</Text>
                <Text style={[styles.detailValue, { color: colors.text }]}>Vừa xong</Text>
              </View>
              <View style={styles.rowBetween}>
                <Text style={[styles.detailLabel, { color: colors.textMuted }]}>Mã giao dịch</Text>
                <Text style={[styles.detailValue, { color: colors.text, fontFamily: typography.brand.fontFamily }]}>
                  {txCode}
                </Text>
              </View>
              {note ? (
                <View style={styles.rowBetween}>
                  <Text style={[styles.detailLabel, { color: colors.textMuted }]}>Lời nhắn</Text>
                  <Text style={[styles.detailValue, { color: colors.text }]}>{note}</Text>
                </View>
              ) : null}
            </View>

            <Pressable
              onPress={() => {
                setIsSuccess(false);
                setAmount('');
                setRecipient('');
                setNote('');
                router.replace('/(authenticated)/home');
              }}
              style={[styles.primaryBtn, { backgroundColor: colors.primary }]}
            >
              <Text style={styles.primaryBtnText}>Về trang chủ</Text>
            </Pressable>
          </GlassCard>
        </View>
      </AppScreen>
    );
  }

  return (
    <AppScreen title="Chuyển tiền P2P" scrollable>
      <View style={styles.container}>
        {/* BACK NAV ROW */}
        <Pressable onPress={() => router.replace('/(authenticated)/home')} style={styles.backRow}>
          <Ionicons name="arrow-back" size={20} color={colors.primary} />
          <Text style={[styles.backText, { color: colors.primary }]}>Quay lại Trang chủ</Text>
        </Pressable>

        {/* 1. RECENT RECIPIENTS BAR */}
        <View style={styles.sectionWrap}>
          <Text style={[styles.sectionTitle, { color: colors.text }]}>Gần đây & Bạn bè</Text>
          <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.recentRow}>
            {RECENT_RECIPIENTS.map((rec) => (
              <Pressable
                key={rec.id}
                onPress={() => handleSelectRecent(rec)}
                style={({ pressed }) => [styles.recentItem, { opacity: pressed ? 0.75 : 1 }]}
              >
                <View style={[styles.recentAvatar, { backgroundColor: colors.primarySoft }]}>
                  <Text style={[styles.recentInitials, { color: colors.primary }]}>{rec.initials}</Text>
                </View>
                <Text style={[styles.recentName, { color: colors.text }]} numberOfLines={1}>
                  {rec.name}
                </Text>
              </Pressable>
            ))}
          </ScrollView>
        </View>

        {/* 2. TRANSFER FORM */}
        <GlassCard style={styles.formCard}>
          {/* Recipient */}
          <Text style={[styles.inputLabel, { color: colors.text }]}>Người nhận / SpendID</Text>
          <View style={[styles.inputWrap, { backgroundColor: colors.surfaceElevated, borderColor: colors.border }]}>
            <Ionicons name="person-outline" size={18} color={colors.textMuted} />
            <TextInput
              value={recipient}
              onChangeText={setRecipient}
              placeholder="Nhập SpendID, email hoặc tên bạn bè"
              placeholderTextColor={colors.placeholder}
              style={[styles.textInput, { color: colors.text }]}
            />
          </View>

          {/* Amount */}
          <Text style={[styles.inputLabel, { color: colors.text, marginTop: 14 }]}>Số tiền chuyển (₫)</Text>
          <View style={[styles.inputWrap, { backgroundColor: colors.surfaceElevated, borderColor: colors.border }]}>
            <Ionicons name="cash-outline" size={18} color={colors.primary} />
            <TextInput
              value={amount}
              onChangeText={setAmount}
              keyboardType="numeric"
              placeholder="0 ₫"
              placeholderTextColor={colors.placeholder}
              style={[styles.textInput, { color: colors.text, fontSize: 18, fontWeight: '700' }]}
            />
          </View>

          {/* Quick Amounts */}
          <View style={styles.quickAmountRow}>
            {QUICK_AMOUNTS.map((amt) => (
              <Pressable
                key={amt}
                onPress={() => setAmount(amt.toString())}
                style={[
                  styles.quickPill,
                  {
                    borderColor: colors.border,
                    backgroundColor: isDark ? colors.surfaceElevated : '#E6F8F0',
                  },
                ]}
              >
                <Text style={[styles.quickPillText, { color: colors.primary }]}>
                  {amt >= 1000000 ? `${amt / 1000000}tr` : `${amt / 1000}k`}
                </Text>
              </Pressable>
            ))}
          </View>

          {/* Note */}
          <Text style={[styles.inputLabel, { color: colors.text, marginTop: 14 }]}>Lời nhắn</Text>
          <View style={[styles.inputWrap, { backgroundColor: colors.surfaceElevated, borderColor: colors.border }]}>
            <Ionicons name="chatbox-ellipses-outline" size={18} color={colors.textMuted} />
            <TextInput
              value={note}
              onChangeText={setNote}
              placeholder="Ví dụ: Tiền ăn trưa / Chuyển khoản"
              placeholderTextColor={colors.placeholder}
              style={[styles.textInput, { color: colors.text }]}
            />
          </View>

          {/* CTA */}
          <Pressable
            onPress={handleTransfer}
            style={[styles.primaryBtn, { backgroundColor: colors.primary, marginTop: 20 }]}
          >
            <Text style={styles.primaryBtnText}>Tiếp tục xác nhận</Text>
          </Pressable>
        </GlassCard>
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
  sectionWrap: {
    gap: 8,
  },
  sectionTitle: {
    fontSize: 15,
    fontWeight: '700',
  },
  recentRow: {
    flexDirection: 'row',
    gap: 14,
    paddingVertical: 4,
  },
  recentItem: {
    alignItems: 'center',
    width: 68,
    gap: 4,
  },
  recentAvatar: {
    width: 50,
    height: 50,
    borderRadius: 25,
    alignItems: 'center',
    justifyContent: 'center',
  },
  recentInitials: {
    fontSize: 16,
    fontWeight: '800',
  },
  recentName: {
    fontSize: 12,
    fontWeight: '600',
    textAlign: 'center',
  },
  formCard: {
    padding: spacing.lg,
  },
  inputLabel: {
    fontSize: 13,
    fontWeight: '600',
    marginBottom: 6,
  },
  inputWrap: {
    flexDirection: 'row',
    alignItems: 'center',
    height: 48,
    borderRadius: radius.md,
    borderWidth: 1,
    paddingHorizontal: 12,
    gap: 8,
  },
  textInput: {
    flex: 1,
    fontSize: 15,
  },
  quickAmountRow: {
    flexDirection: 'row',
    gap: 8,
    marginTop: 8,
    flexWrap: 'wrap',
  },
  quickPill: {
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: radius.pill,
    borderWidth: 1,
  },
  quickPillText: {
    fontSize: 12,
    fontWeight: '700',
  },
  primaryBtn: {
    height: 48,
    borderRadius: radius.lg,
    alignItems: 'center',
    justifyContent: 'center',
  },
  primaryBtnText: {
    color: '#FFFFFF',
    fontSize: 15,
    fontWeight: '700',
  },
  successCard: {
    padding: spacing.xl,
    alignItems: 'center',
    gap: spacing.md,
  },
  successIconWrap: {
    width: 72,
    height: 72,
    borderRadius: 36,
    alignItems: 'center',
    justifyContent: 'center',
  },
  successAmount: {
    fontSize: 28,
    fontWeight: '800',
  },
  successTable: {
    width: '100%',
    borderRadius: radius.md,
    borderWidth: 1,
    padding: spacing.md,
    gap: 10,
  },
  rowBetween: {
    flexDirection: 'row',
    justifyContent: 'space-between',
  },
  detailLabel: {
    fontSize: 13,
  },
  detailValue: {
    fontSize: 13,
    fontWeight: '700',
  },
});
