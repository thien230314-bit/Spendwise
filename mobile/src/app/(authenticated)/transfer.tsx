import React, { useState } from 'react';
import {
  KeyboardAvoidingView,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { SymbolView } from 'expo-symbols';
import { AppScreen } from '@/components/AppScreen';
import { Badge, Card, Copy, UIIcon } from '@/components/MobileUI';
import { PrimaryButton } from '@/components/PrimaryButton';
import { fontStyles, radius, spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

const QUICK_AMOUNTS = [100000, 200000, 500000, 1000000, 2000000];

export default function TransferScreen() {
  const { colors, isDark } = useTheme();
  const [recipient, setRecipient] = useState('');
  const [amount, setAmount] = useState('');
  const [note, setNote] = useState('');
  const [isSuccess, setIsSuccess] = useState(false);

  const parsedAmount = parseInt(amount.replace(/\D/g, ''), 10) || 0;

  if (isSuccess) {
    return (
      <AppScreen
        title="Chuyển tiền thành công"
        subtitle="Giao dịch của bạn đã được thực hiện an toàn trên mạng SpendWise."
      >
        <View style={styles.container}>
          <View
            style={[
              styles.cardBox,
              {
                backgroundColor: isDark ? '#101622' : '#FFFFFF',
                borderColor: colors.border,
                alignItems: 'center',
                paddingVertical: spacing.xl,
                gap: spacing.md,
              },
            ]}
          >
            <View
              style={{
                width: 64,
                height: 64,
                borderRadius: 32,
                backgroundColor: isDark ? '#064E3B' : '#ECFDF5',
                alignItems: 'center',
                justifyContent: 'center',
              }}
            >
              <SymbolView
                name={{ ios: 'checkmark.circle.fill', android: 'check_circle', web: 'check_circle' }}
                size={36}
                tintColor={colors.primary}
              />
            </View>

            <Text style={{ fontSize: 18, fontWeight: '800', color: colors.text }}>
              -{parsedAmount.toLocaleString('vi-VN')} ₫
            </Text>

            <View style={{ width: '100%', gap: 8, paddingHorizontal: spacing.sm, marginTop: spacing.sm }}>
              <View style={styles.rowBetween}>
                <Text style={{ fontSize: 13, color: colors.textMuted }}>Người nhận</Text>
                <Text style={{ fontSize: 13, fontWeight: '700', color: colors.text }}>{recipient}</Text>
              </View>
              <View style={styles.rowBetween}>
                <Text style={{ fontSize: 13, color: colors.textMuted }}>Thời gian</Text>
                <Text style={{ fontSize: 13, color: colors.text }}>Vừa xong</Text>
              </View>
              <View style={styles.rowBetween}>
                <Text style={{ fontSize: 13, color: colors.textMuted }}>Mã giao dịch</Text>
                <Text style={{ fontSize: 13, fontFamily: 'monospace', color: colors.text }}>
                  SW-TX-{Date.now().toString().slice(-6)}
                </Text>
              </View>
              {note ? (
                <View style={styles.rowBetween}>
                  <Text style={{ fontSize: 13, color: colors.textMuted }}>Lời nhắn</Text>
                  <Text style={{ fontSize: 13, color: colors.text }}>{note}</Text>
                </View>
              ) : null}
            </View>
          </View>

          <Pressable
            accessibilityRole="button"
            onPress={() => {
              setIsSuccess(false);
              setAmount('');
              setRecipient('');
              setNote('');
            }}
            style={[styles.submitBtn, { backgroundColor: colors.primary }]}
          >
            <Text style={[styles.submitBtnText, { color: '#FFFFFF' }]}>
              Thực hiện giao dịch khác
            </Text>
          </Pressable>
        </View>
      </AppScreen>
    );
  }

  return (
    <AppScreen
      title="Chuyển tiền"
      subtitle="Chuyển tiền nhanh trong hệ thống SpendWise hoặc liên ngân hàng."
    >
      <View style={styles.container}>
        {/* 1. Source Wallet Card (Card bo tròn 22px tinh tế) */}
        <View
          style={[
            styles.cardBox,
            {
              backgroundColor: isDark ? '#101622' : '#FFFFFF',
              borderColor: colors.border,
            },
          ]}
        >
          <View style={styles.cardHeaderRow}>
            <View style={styles.badgeWrap}>
              <View style={[styles.dotIndicator, { backgroundColor: colors.primary }]} />
              <Text style={[styles.cardTitle, { color: colors.text }]}>
                1. Nguồn tiền trích nợ
              </Text>
            </View>
            <View style={[styles.walletBadge, { backgroundColor: colors.primarySoft }]}>
              <Text style={[styles.walletBadgeText, { color: colors.primary }]}>
                Khả dụng: 14.250.000 ₫
              </Text>
            </View>
          </View>

          <View
            style={[
              styles.sourceWalletRow,
              {
                backgroundColor: isDark ? '#151D2A' : '#F8FAFC',
                borderColor: colors.border,
              },
            ]}
          >
            <View
              style={[
                styles.walletIconCircle,
                { backgroundColor: isDark ? '#052E26' : '#ECFDF5' },
              ]}
            >
              <UIIcon name="wallet" size={20} color={colors.primary} />
            </View>
            <View style={styles.walletDetails}>
              <Text style={[styles.walletMainName, { color: colors.text }]}>
                Ví SpendWise Mặc định
              </Text>
              <Text style={[styles.walletSubMeta, { color: colors.textMuted }]}>
                SW-98234-01 • Cá nhân
              </Text>
            </View>
            <SymbolView
              name={{ ios: 'checkmark.circle.fill', android: 'check_circle', web: 'check_circle' }}
              size={18}
              tintColor={colors.primary}
            />
          </View>
        </View>

        {/* 2. Recipient Input Card */}
        <View
          style={[
            styles.cardBox,
            {
              backgroundColor: isDark ? '#101622' : '#FFFFFF',
              borderColor: colors.border,
            },
          ]}
        >
          <Text style={[styles.cardTitle, { color: colors.text }]}>
            2. Người nhận tiền
          </Text>

          <View
            style={[
              styles.inputBox,
              {
                backgroundColor: isDark ? '#151D2A' : '#F8FAFC',
                borderColor: colors.border,
              },
            ]}
          >
            <SymbolView
              name={{ ios: 'person.crop.circle', android: 'account_circle', web: 'account_circle' }}
              size={20}
              tintColor={colors.textMuted}
            />
            <TextInput
              value={recipient}
              onChangeText={setRecipient}
              placeholder="Nhập số điện thoại hoặc SpendId người nhận"
              placeholderTextColor={colors.textMuted}
              style={[styles.inputField, { color: colors.text }]}
            />
            {recipient ? (
              <Pressable onPress={() => setRecipient('')} hitSlop={8}>
                <SymbolView
                  name={{ ios: 'xmark.circle.fill', android: 'cancel', web: 'cancel' }}
                  size={16}
                  tintColor={colors.textMuted}
                />
              </Pressable>
            ) : null}
          </View>

          {/* Quick Contact Chips (Darwin Piodos reference horizontal pill style) */}
          <View style={styles.quickContactsRow}>
            <Text style={[styles.quickSubLabel, { color: colors.textMuted }]}>
              Gần đây:
            </Text>
            <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.contactsScroll}>
              {[
                { name: 'Nguyễn Văn Minh', id: '0912345678' },
                { name: 'Trần Thị Mai', id: '0987654321' },
                { name: 'Lê Hoàng Nam', id: '0903456789' },
              ].map((contact, idx) => (
                <Pressable
                  key={idx}
                  onPress={() => setRecipient(contact.id)}
                  style={[
                    styles.contactChip,
                    {
                      backgroundColor: isDark ? '#151D2A' : '#F1F5F9',
                      borderColor: colors.border,
                    },
                  ]}
                >
                  <Text style={[styles.contactChipText, { color: colors.text }]}>
                    {contact.name}
                  </Text>
                </Pressable>
              ))}
            </ScrollView>
          </View>
        </View>

        {/* 3. Amount & Fast Presets Card */}
        <View
          style={[
            styles.cardBox,
            {
              backgroundColor: isDark ? '#101622' : '#FFFFFF',
              borderColor: colors.border,
            },
          ]}
        >
          <Text style={[styles.cardTitle, { color: colors.text }]}>
            3. Số tiền cần chuyển (VND)
          </Text>

          <View
            style={[
              styles.amountInputBox,
              {
                backgroundColor: isDark ? '#151D2A' : '#F8FAFC',
                borderColor: parsedAmount > 14250000 ? colors.danger : colors.border,
              },
            ]}
          >
            <Text style={[styles.currencyPrefix, { color: colors.primary }]}>₫</Text>
            <TextInput
              value={amount}
              onChangeText={(val) => {
                const numeric = val.replace(/\D/g, '');
                setAmount(numeric ? parseInt(numeric, 10).toLocaleString('vi-VN') : '');
              }}
              keyboardType="numeric"
              placeholder="0"
              placeholderTextColor={colors.textMuted}
              style={[styles.amountInputText, { color: colors.text }]}
            />
          </View>

          {/* Fast Preset Amount Pills (Reference design inspired) */}
          <View style={styles.presetsGrid}>
            {QUICK_AMOUNTS.map((amt) => {
              const isSelected = parsedAmount === amt;
              return (
                <Pressable
                  key={amt}
                  onPress={() => setAmount(amt.toLocaleString('vi-VN'))}
                  style={[
                    styles.presetPill,
                    {
                      backgroundColor: isSelected
                        ? isDark
                          ? '#34D399'
                          : '#047857'
                        : isDark
                        ? '#151D2A'
                        : '#F1F5F9',
                      borderColor: isSelected ? 'transparent' : colors.border,
                    },
                  ]}
                >
                  <Text
                    style={[
                      styles.presetPillText,
                      {
                        color: isSelected ? '#FFFFFF' : colors.text,
                        fontWeight: isSelected ? '700' : '500',
                      },
                    ]}
                  >
                    {(amt / 1000).toLocaleString('vi-VN')}k
                  </Text>
                </Pressable>
              );
            })}
          </View>

          {/* Message Input */}
          <View
            style={[
              styles.inputBox,
              {
                backgroundColor: isDark ? '#151D2A' : '#F8FAFC',
                borderColor: colors.border,
              },
            ]}
          >
            <SymbolView
              name={{ ios: 'text.bubble', android: 'chat_bubble_outline', web: 'chat_bubble_outline' }}
              size={18}
              tintColor={colors.textMuted}
            />
            <TextInput
              value={note}
              onChangeText={setNote}
              placeholder="Lời nhắn chuyển tiền (không bắt buộc)"
              placeholderTextColor={colors.textMuted}
              style={[styles.inputField, { color: colors.text }]}
            />
          </View>
        </View>

        {/* Submit Action Button */}
        <Pressable
          accessibilityRole="button"
          disabled={!recipient || parsedAmount <= 0}
          onPress={() => setIsSuccess(true)}
          style={({ pressed }) => [
            styles.submitBtn,
            {
              backgroundColor:
                !recipient || parsedAmount <= 0
                  ? isDark
                    ? '#1E293B'
                    : '#CBD5E1'
                  : colors.primary,
              opacity: pressed ? 0.85 : 1,
            },
          ]}
        >
          <Text
            style={[
              styles.submitBtnText,
              {
                color:
                  !recipient || parsedAmount <= 0 ? colors.textMuted : '#FFFFFF',
              },
            ]}
          >
            Xác nhận chuyển tiền →
          </Text>
        </Pressable>
      </View>
    </AppScreen>
  );
}

const styles = StyleSheet.create({
  container: {
    gap: spacing.md,
    paddingBottom: spacing.xxl,
  },
  cardBox: {
    borderRadius: 22,
    borderWidth: 1,
    padding: spacing.md,
    gap: spacing.sm + 2,
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.04,
    shadowRadius: 4,
    elevation: 1,
  },
  cardHeaderRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  rowBetween: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  badgeWrap: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  dotIndicator: {
    width: 8,
    height: 8,
    borderRadius: 4,
  },
  cardTitle: {
    fontSize: 14,
    fontWeight: '700',
    letterSpacing: -0.2,
  },
  walletBadge: {
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: 12,
  },
  walletBadgeText: {
    fontSize: 10,
    fontWeight: '700',
    fontFamily: 'monospace',
  },
  sourceWalletRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
    padding: spacing.sm,
    borderRadius: 16,
    borderWidth: 1,
  },
  walletIconCircle: {
    width: 38,
    height: 38,
    borderRadius: 19,
    alignItems: 'center',
    justifyContent: 'center',
  },
  walletDetails: {
    flex: 1,
    gap: 2,
  },
  walletMainName: {
    fontSize: 13,
    fontWeight: '700',
  },
  walletSubMeta: {
    fontSize: 11,
  },
  inputBox: {
    flexDirection: 'row',
    alignItems: 'center',
    borderWidth: 1,
    borderRadius: 16,
    paddingHorizontal: spacing.md,
    height: 46,
    gap: spacing.sm,
  },
  inputField: {
    flex: 1,
    fontSize: 13,
    paddingVertical: 0,
  },
  quickContactsRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.xs,
    paddingTop: 2,
  },
  quickSubLabel: {
    fontSize: 11,
    fontWeight: '600',
  },
  contactsScroll: {
    flexDirection: 'row',
    gap: 6,
  },
  contactChip: {
    paddingHorizontal: 10,
    paddingVertical: 5,
    borderRadius: 14,
    borderWidth: 1,
  },
  contactChipText: {
    fontSize: 11,
    fontWeight: '500',
  },
  amountInputBox: {
    flexDirection: 'row',
    alignItems: 'center',
    borderWidth: 1,
    borderRadius: 18,
    paddingHorizontal: spacing.md,
    height: 56,
    gap: spacing.xs,
  },
  currencyPrefix: {
    fontSize: 24,
    fontWeight: '800',
  },
  amountInputText: {
    flex: 1,
    fontSize: 22,
    fontWeight: '800',
    fontFamily: 'monospace',
    paddingVertical: 0,
  },
  presetsGrid: {
    flexDirection: 'row',
    gap: 6,
    justifyContent: 'space-between',
  },
  presetPill: {
    flex: 1,
    paddingVertical: 7,
    borderRadius: 12,
    borderWidth: 1,
    alignItems: 'center',
    justifyContent: 'center',
  },
  presetPillText: {
    fontSize: 11,
  },
  submitBtn: {
    height: 50,
    borderRadius: 18,
    alignItems: 'center',
    justifyContent: 'center',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 2,
    marginTop: spacing.xs,
  },
  submitBtnText: {
    fontSize: 14,
    fontWeight: '700',
  },
});
