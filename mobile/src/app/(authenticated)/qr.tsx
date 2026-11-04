import React, { useState } from 'react';
import {
  StyleSheet,
  Text,
  View,
  Pressable,
  TextInput,
  Alert,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { router } from 'expo-router';
import { useAuth } from '@/auth/AuthProvider';
import { AppScreen } from '@/components/AppScreen';
import { GlassCard } from '@/components/GlassCard';
import { useTheme } from '@/theme/useTheme';
import { radius, spacing, typography } from '@/theme/tokens';

export default function QrScreen() {
  const { user } = useAuth();
  const { colors, isDark } = useTheme();
  const [mode, setMode] = useState<'my_qr' | 'scan'>('my_qr');
  const [manualCode, setManualCode] = useState('');

  const spendId = (user as any)?.spendId || (user as any)?.accountNumber || 'SW-USER-8888';
  const fullName = (user as any)?.fullName || user?.email || 'Người dùng SpendWise';

  const handleCopySpendId = () => {
    Alert.alert('Đã sao chép', `Đã sao chép mã SpendID: ${spendId}`);
  };

  const handleSimulateScan = (scannedTarget: string) => {
    Alert.alert('Quét thành công', `Mã nhận diện: ${scannedTarget}`, [
      {
        text: 'Chuyển tiền',
        onPress: () => router.push('/(authenticated)/transfer'),
      },
      { text: 'Đóng', style: 'cancel' },
    ]);
  };

  return (
    <AppScreen title="Mã QR & Quét" scrollable>
      <View style={styles.container}>
        {/* SEGMENTED TAB SWITCHER */}
        <View style={[styles.segmentContainer, { backgroundColor: isDark ? colors.surfaceElevated : '#DCEEE4' }]}>
          <Pressable
            accessibilityRole="tab"
            accessibilityState={{ selected: mode === 'my_qr' }}
            onPress={() => setMode('my_qr')}
            style={[
              styles.segmentBtn,
              mode === 'my_qr' && [styles.segmentBtnActive, { backgroundColor: colors.surface }],
            ]}
          >
            <Ionicons
              name="qr-code-outline"
              size={18}
              color={mode === 'my_qr' ? colors.primary : colors.textMuted}
            />
            <Text
              style={[
                styles.segmentText,
                { color: mode === 'my_qr' ? colors.primary : colors.textMuted },
              ]}
            >
              Mã QR của tôi
            </Text>
          </Pressable>

          <Pressable
            accessibilityRole="tab"
            accessibilityState={{ selected: mode === 'scan' }}
            onPress={() => setMode('scan')}
            style={[
              styles.segmentBtn,
              mode === 'scan' && [styles.segmentBtnActive, { backgroundColor: colors.surface }],
            ]}
          >
            <Ionicons
              name="scan-outline"
              size={18}
              color={mode === 'scan' ? colors.primary : colors.textMuted}
            />
            <Text
              style={[
                styles.segmentText,
                { color: mode === 'scan' ? colors.primary : colors.textMuted },
              ]}
            >
              Quét mã QR
            </Text>
          </Pressable>
        </View>

        {mode === 'my_qr' ? (
          /* MY QR CARD */
          <GlassCard style={styles.qrCard}>
            <View style={styles.qrCardHeader}>
              <View style={[styles.avatarCircle, { backgroundColor: colors.primarySoft }]}>
                <Ionicons name="person" size={24} color={colors.primary} />
              </View>
              <Text style={[styles.userName, { color: colors.text }]}>{fullName}</Text>
              <Text style={[styles.userSub, { color: colors.textMuted }]}>Tài khoản cá nhân SpendWise</Text>
            </View>

            {/* QR GRAPHIC CONTAINER */}
            <View style={styles.qrBox}>
              <View style={[styles.qrPatternMock, { borderColor: colors.primary }]}>
                <Ionicons name="qr-code" size={170} color={colors.primary} />
              </View>
              <View style={[styles.qrLogoCenter, { backgroundColor: colors.surface, borderColor: colors.primary }]}>
                <Ionicons name="wallet" size={20} color={colors.primary} />
              </View>
            </View>

            {/* SPEND ID PILL */}
            <Pressable
              onPress={handleCopySpendId}
              style={[styles.spendIdPill, { backgroundColor: isDark ? colors.surfaceElevated : '#E8F5EE' }]}
            >
              <Text style={[styles.spendIdLabel, { color: colors.textMuted }]}>SpendID:</Text>
              <Text style={[styles.spendIdValue, { color: colors.text }]}>{spendId}</Text>
              <Ionicons name="copy-outline" size={16} color={colors.primary} />
            </Pressable>

            {/* ACTION BUTTONS */}
            <View style={styles.qrActions}>
              <Pressable
                onPress={handleCopySpendId}
                style={[styles.actionBtn, { backgroundColor: colors.primary }]}
              >
                <Ionicons name="copy" size={18} color="#FFFFFF" />
                <Text style={styles.actionBtnText}>Sao chép mã</Text>
              </Pressable>

              <Pressable
                onPress={() => Alert.alert('Chia sẻ', 'Tính năng chia sẻ ảnh QR đang mở.')}
                style={[styles.actionBtnOutline, { borderColor: colors.primary }]}
              >
                <Ionicons name="share-social-outline" size={18} color={colors.primary} />
                <Text style={[styles.actionBtnOutlineText, { color: colors.primary }]}>Chia sẻ QR</Text>
              </Pressable>
            </View>
          </GlassCard>
        ) : (
          /* SCANNER VIEW */
          <GlassCard style={styles.scannerCard}>
            <View style={styles.scannerViewport}>
              <View style={[styles.viewfinder, { borderColor: colors.primary }]}>
                <View style={[styles.scanCorner, styles.cornerTL, { borderColor: colors.primary }]} />
                <View style={[styles.scanCorner, styles.cornerTR, { borderColor: colors.primary }]} />
                <View style={[styles.scanCorner, styles.cornerBL, { borderColor: colors.primary }]} />
                <View style={[styles.scanCorner, styles.cornerBR, { borderColor: colors.primary }]} />
                <Ionicons name="scan" size={48} color={colors.primary} style={{ opacity: 0.6 }} />
              </View>
              <Text style={[styles.scannerHint, { color: colors.textMuted }]}>
                Di chuyển camera đến mã QR SpendWise hoặc VietQR để quét
              </Text>
            </View>

            {/* MANUAL CODE INPUT */}
            <View style={styles.manualSection}>
              <Text style={[styles.manualTitle, { color: colors.text }]}>Hoặc nhập mã SpendID thủ công:</Text>
              <View style={styles.manualInputRow}>
                <TextInput
                  value={manualCode}
                  onChangeText={setManualCode}
                  placeholder="Ví dụ: SW-USER-9824"
                  placeholderTextColor={colors.placeholder}
                  style={[
                    styles.manualInput,
                    {
                      backgroundColor: colors.surface,
                      borderColor: colors.border,
                      color: colors.text,
                    },
                  ]}
                />
                <Pressable
                  onPress={() => {
                    if (manualCode.trim()) {
                      handleSimulateScan(manualCode.trim());
                    } else {
                      Alert.alert('Thông báo', 'Vui lòng nhập mã SpendID');
                    }
                  }}
                  style={[styles.manualSubmitBtn, { backgroundColor: colors.primary }]}
                >
                  <Text style={styles.manualSubmitText}>Tìm</Text>
                </Pressable>
              </View>
            </View>

            {/* DEMO SCAN OPTIONS */}
            <View style={styles.quickScanList}>
              <Text style={[styles.quickScanTitle, { color: colors.textMuted }]}>Mã kiểm thử nhanh:</Text>
              <Pressable
                onPress={() => handleSimulateScan('SW-FRIEND-AN-NGUYEN')}
                style={[styles.quickScanItem, { borderColor: colors.border }]}
              >
                <Ionicons name="person-circle-outline" size={20} color={colors.primary} />
                <Text style={[styles.quickScanItemText, { color: colors.text }]}>Bạn bè: An Nguyễn (SW-FRIEND-AN)</Text>
              </Pressable>
              <Pressable
                onPress={() => handleSimulateScan('SW-FUND-DALAT-TRIP')}
                style={[styles.quickScanItem, { borderColor: colors.border }]}
              >
                <Ionicons name="people-circle-outline" size={20} color={colors.accent} />
                <Text style={[styles.quickScanItemText, { color: colors.text }]}>Quỹ: Du lịch Đà Lạt 2026</Text>
              </Pressable>
            </View>
          </GlassCard>
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
  segmentContainer: {
    flexDirection: 'row',
    borderRadius: radius.pill,
    padding: 4,
  },
  segmentBtn: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 6,
    paddingVertical: 10,
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
    fontSize: 13,
    fontWeight: '700',
  },
  qrCard: {
    padding: spacing.lg,
    alignItems: 'center',
  },
  qrCardHeader: {
    alignItems: 'center',
    marginBottom: spacing.md,
  },
  avatarCircle: {
    width: 48,
    height: 48,
    borderRadius: 24,
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 8,
  },
  userName: {
    fontSize: 17,
    fontWeight: '700',
  },
  userSub: {
    fontSize: 13,
  },
  qrBox: {
    width: 220,
    height: 220,
    alignItems: 'center',
    justifyContent: 'center',
    position: 'relative',
    marginVertical: spacing.md,
  },
  qrPatternMock: {
    width: 200,
    height: 200,
    borderWidth: 2,
    borderRadius: radius.lg,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: '#FFFFFF',
  },
  qrLogoCenter: {
    position: 'absolute',
    width: 40,
    height: 40,
    borderRadius: 20,
    borderWidth: 2,
    alignItems: 'center',
    justifyContent: 'center',
  },
  spendIdPill: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: radius.pill,
    marginBottom: spacing.lg,
  },
  spendIdLabel: {
    fontSize: 13,
    fontWeight: '600',
  },
  spendIdValue: {
    fontSize: 14,
    fontWeight: '800',
    fontFamily: typography.brand.fontFamily,
  },
  qrActions: {
    flexDirection: 'row',
    gap: 12,
    width: '100%',
  },
  actionBtn: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 6,
    paddingVertical: 12,
    borderRadius: radius.lg,
  },
  actionBtnText: {
    color: '#FFFFFF',
    fontWeight: '700',
    fontSize: 14,
  },
  actionBtnOutline: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 6,
    paddingVertical: 12,
    borderRadius: radius.lg,
    borderWidth: 1.5,
  },
  actionBtnOutlineText: {
    fontWeight: '700',
    fontSize: 14,
  },
  scannerCard: {
    padding: spacing.lg,
  },
  scannerViewport: {
    alignItems: 'center',
    marginBottom: spacing.lg,
  },
  viewfinder: {
    width: 200,
    height: 200,
    borderRadius: radius.lg,
    alignItems: 'center',
    justifyContent: 'center',
    position: 'relative',
    backgroundColor: 'rgba(0, 0, 0, 0.04)',
    marginBottom: 12,
  },
  scanCorner: {
    position: 'absolute',
    width: 24,
    height: 24,
  },
  cornerTL: {
    top: 8,
    left: 8,
    borderTopWidth: 3,
    borderLeftWidth: 3,
  },
  cornerTR: {
    top: 8,
    right: 8,
    borderTopWidth: 3,
    borderRightWidth: 3,
  },
  cornerBL: {
    bottom: 8,
    left: 8,
    borderBottomWidth: 3,
    borderLeftWidth: 3,
  },
  cornerBR: {
    bottom: 8,
    right: 8,
    borderBottomWidth: 3,
    borderRightWidth: 3,
  },
  scannerHint: {
    fontSize: 13,
    textAlign: 'center',
    paddingHorizontal: spacing.md,
  },
  manualSection: {
    marginTop: spacing.sm,
  },
  manualTitle: {
    fontSize: 13,
    fontWeight: '600',
    marginBottom: 6,
  },
  manualInputRow: {
    flexDirection: 'row',
    gap: 8,
  },
  manualInput: {
    flex: 1,
    height: 44,
    borderRadius: radius.md,
    borderWidth: 1,
    paddingHorizontal: 12,
    fontSize: 14,
  },
  manualSubmitBtn: {
    paddingHorizontal: 18,
    borderRadius: radius.md,
    alignItems: 'center',
    justifyContent: 'center',
  },
  manualSubmitText: {
    color: '#FFFFFF',
    fontWeight: '700',
    fontSize: 14,
  },
  quickScanList: {
    marginTop: spacing.lg,
    gap: 8,
  },
  quickScanTitle: {
    fontSize: 12,
    fontWeight: '600',
  },
  quickScanItem: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    padding: 10,
    borderRadius: radius.md,
    borderWidth: 1,
  },
  quickScanItemText: {
    fontSize: 13,
    fontWeight: '500',
  },
});
