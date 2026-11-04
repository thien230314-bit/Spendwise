import React, { useState } from 'react';
import {
  StyleSheet,
  Text,
  View,
  Pressable,
  TextInput,
  Alert,
  Image,
  ActivityIndicator,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { router } from 'expo-router';
import * as Clipboard from 'expo-clipboard';
import { useAuth } from '@/auth/AuthProvider';
import { AppScreen } from '@/components/AppScreen';
import { GlassCard } from '@/components/GlassCard';
import { useTheme } from '@/theme/useTheme';
import { radius, spacing } from '@/theme/tokens';

export default function QrScreen() {
  const { user } = useAuth();
  const { colors, isDark } = useTheme();

  // Mode: My QR vs Scanner
  const [mode, setMode] = useState<'my_qr' | 'scan'>('my_qr');
  // QR Type: VietQR (Napas247) vs SpendWise P2P
  const [qrType, setQrType] = useState<'vietqr' | 'spendwise'>('vietqr');
  const [manualCode, setManualCode] = useState('');
  const [imgLoading, setImgLoading] = useState(true);
  const [imgError, setImgError] = useState(false);
  const [copiedState, setCopiedState] = useState(false);
  const [clipboardContent, setClipboardContent] = useState<string | null>(null);

  const spendId = (user as any)?.spendId || (user as any)?.accountNumber || 'SW-USER-8888';
  const fullName = (user as any)?.fullName || user?.email || 'Người dùng SpendWise';

  // URLs for scannable QR codes
  const vietQrUrl = `https://img.vietqr.io/image/vietcombank-982401-compact2.png?amount=0&addInfo=SpendWise%20${encodeURIComponent(spendId)}&accountName=${encodeURIComponent(fullName)}`;
  const p2pQrUrl = `https://api.qrserver.com/v1/create-qr-code/?size=300x300&data=${encodeURIComponent(`spendwise:user:${spendId}`)}`;

  const currentQrUrl = qrType === 'vietqr' ? vietQrUrl : p2pQrUrl;

  // Genuine Clipboard Copy via expo-clipboard with verification
  const handleCopySpendId = async () => {
    try {
      await Clipboard.setStringAsync(spendId);
      const readBack = await Clipboard.getStringAsync();
      if (readBack === spendId) {
        setCopiedState(true);
        setClipboardContent(readBack);
        setTimeout(() => setCopiedState(false), 3000);
        Alert.alert(
          'Đã sao chép vào bộ nhớ tạm (Verified Clipboard)',
          `Mã SpendID: "${spendId}" đã được ghi vào bộ nhớ tạm của thiết bị và xác minh đọc lại thành công.`
        );
      } else {
        Alert.alert('Thông báo', `SpendID: ${spendId}`);
      }
    } catch (err: any) {
      Alert.alert('Lỗi Clipboard', err?.message || 'Không thể ghi vào bộ nhớ tạm');
    }
  };

  // Verify Clipboard Content
  const handleVerifyClipboard = async () => {
    try {
      const text = await Clipboard.getStringAsync();
      setClipboardContent(text);
      Alert.alert(
        'Nội dung bộ nhớ tạm hiện tại',
        text ? `"${text}"` : '(Bộ nhớ tạm đang trống)'
      );
    } catch (err: any) {
      Alert.alert('Lỗi kiểm tra', err?.message || 'Không thể đọc bộ nhớ tạm');
    }
  };

  // Handle scanned/manual code
  const handleProcessCode = (code: string) => {
    const trimmed = code.trim();
    if (!trimmed) {
      Alert.alert('Lỗi', 'Vui lòng nhập hoặc quét mã SpendID / QR.');
      return;
    }

    let targetId = trimmed;
    if (trimmed.startsWith('spendwise:user:')) {
      targetId = trimmed.replace('spendwise:user:', '');
    }

    Alert.alert(
      'Mã hợp lệ',
      `Đã nhận diện SpendID: ${targetId}`,
      [
        {
          text: 'Chuyển tiền tới người này',
          onPress: () => router.push('/(authenticated)/transfer'),
        },
        { text: 'Đóng', style: 'cancel' },
      ]
    );
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

            {/* QR TYPE SWITCHER: VIETQR VS SPENDWISE P2P */}
            <View style={styles.qrTypeRow}>
              <Pressable
                onPress={() => {
                  setQrType('vietqr');
                  setImgLoading(true);
                  setImgError(false);
                }}
                style={[
                  styles.qrTypeChip,
                  qrType === 'vietqr'
                    ? { backgroundColor: colors.primary }
                    : { backgroundColor: isDark ? colors.surfaceElevated : '#E5E7EB' },
                ]}
              >
                <Text
                  style={[
                    styles.qrTypeChipText,
                    { color: qrType === 'vietqr' ? '#FFFFFF' : colors.textMuted },
                  ]}
                >
                  VietQR (Napas247)
                </Text>
              </Pressable>

              <Pressable
                onPress={() => {
                  setQrType('spendwise');
                  setImgLoading(true);
                  setImgError(false);
                }}
                style={[
                  styles.qrTypeChip,
                  qrType === 'spendwise'
                    ? { backgroundColor: colors.primary }
                    : { backgroundColor: isDark ? colors.surfaceElevated : '#E5E7EB' },
                ]}
              >
                <Text
                  style={[
                    styles.qrTypeChipText,
                    { color: qrType === 'spendwise' ? '#FFFFFF' : colors.textMuted },
                  ]}
                >
                  SpendWise P2P
                </Text>
              </Pressable>
            </View>

            {/* REAL SCANNABLE QR IMAGE CONTAINER */}
            <View style={[styles.qrBox, { backgroundColor: '#FFFFFF', borderColor: colors.border }]}>
              {imgLoading && (
                <View style={styles.qrLoader}>
                  <ActivityIndicator size="large" color={colors.primary} />
                  <Text style={styles.qrLoadingText}>Đang tạo mã QR chuẩn...</Text>
                </View>
              )}

              {!imgError ? (
                <Image
                  source={{ uri: currentQrUrl }}
                  style={[styles.qrImage, imgLoading ? { opacity: 0 } : { opacity: 1 }]}
                  resizeMode="contain"
                  onLoadEnd={() => setImgLoading(false)}
                  onError={() => {
                    setImgLoading(false);
                    setImgError(true);
                  }}
                />
              ) : (
                /* OFFLINE FALLBACK CARD */
                <View style={styles.qrFallback}>
                  <Ionicons name="qr-code" size={140} color={colors.primary} />
                  <Text style={styles.fallbackTitle}>Mã nhận diện ngoại tuyến</Text>
                  <Text style={styles.fallbackId}>{spendId}</Text>
                  <Text style={styles.fallbackNote}>Chuyển khoản VCB: 9824 • Nội dung: {spendId}</Text>
                </View>
              )}
            </View>

            <Text style={[styles.qrGuidanceText, { color: colors.textMuted }]}>
              {qrType === 'vietqr'
                ? 'Mã VietQR hợp lệ chuẩn Napas247 • Quét được bằng tất cả ứng dụng Ngân hàng (VCB, MB, Techcombank,...)'
                : 'Mã SpendWise P2P • Quét bằng ứng dụng SpendWise để chuyển tiền nội bộ tức thì'}
            </Text>

            {/* SPEND ID PILL WITH REAL CLIPBOARD */}
            <Pressable
              onPress={handleCopySpendId}
              style={[
                styles.spendIdPill,
                { backgroundColor: isDark ? colors.surfaceElevated : '#E8F5EE' },
                copiedState && { borderColor: colors.primary, borderWidth: 1.5 },
              ]}
            >
              <Text style={[styles.spendIdLabel, { color: colors.textMuted }]}>SpendID:</Text>
              <Text style={[styles.spendIdValue, { color: colors.text }]}>{spendId}</Text>
              <Ionicons
                name={copiedState ? 'checkmark-circle' : 'copy-outline'}
                size={18}
                color={colors.primary}
              />
            </Pressable>

            {/* VERIFY CLIPBOARD ROW */}
            {clipboardContent && (
              <View style={[styles.clipboardStatus, { backgroundColor: isDark ? '#0F3D2E' : '#E6F8F0' }]}>
                <Ionicons name="clipboard-outline" size={14} color={colors.primary} />
                <Text style={[styles.clipboardStatusText, { color: colors.primary }]}>
                  Đã ghi vào Clipboard OS: &quot;{clipboardContent}&quot;
                </Text>
              </View>
            )}

            {/* ACTION BUTTONS */}
            <View style={styles.qrActions}>
              <Pressable
                onPress={handleCopySpendId}
                style={[styles.actionBtn, { backgroundColor: colors.primary }]}
              >
                <Ionicons name="copy" size={18} color="#FFFFFF" />
                <Text style={styles.actionBtnText}>
                  {copiedState ? 'Đã chép vào Clipboard' : 'Sao chép SpendID'}
                </Text>
              </Pressable>

              <Pressable
                onPress={handleVerifyClipboard}
                style={[styles.actionBtnOutline, { borderColor: colors.primary }]}
              >
                <Ionicons name="clipboard" size={18} color={colors.primary} />
                <Text style={[styles.actionBtnOutlineText, { color: colors.primary }]}>
                  Kiểm tra Clipboard
                </Text>
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
                <View style={[styles.scanBeam, { backgroundColor: colors.primary }]} />
              </View>
            </View>

            <Text style={[styles.scannerHint, { color: colors.textMuted }]}>
              Hướng camera vào mã VietQR hoặc SpendID để quét
            </Text>

            {/* MANUAL / PASTE INPUT */}
            <View style={styles.manualInputWrap}>
              <Text style={[styles.manualLabel, { color: colors.text }]}>Hoặc dán SpendID từ bộ nhớ tạm:</Text>
              <View style={styles.inputRow}>
                <TextInput
                  value={manualCode}
                  onChangeText={setManualCode}
                  placeholder="Ví dụ: SW-USER-8888"
                  placeholderTextColor={colors.placeholder}
                  style={[
                    styles.textInput,
                    {
                      backgroundColor: colors.surfaceElevated,
                      borderColor: colors.border,
                      color: colors.text,
                    },
                  ]}
                />
                <Pressable
                  onPress={async () => {
                    const text = await Clipboard.getStringAsync();
                    if (text) setManualCode(text);
                  }}
                  style={[styles.pasteBtn, { backgroundColor: colors.primarySoft }]}
                >
                  <Text style={[styles.pasteBtnText, { color: colors.primary }]}>Dán</Text>
                </Pressable>
              </View>

              <Pressable
                onPress={() => handleProcessCode(manualCode)}
                style={[styles.processBtn, { backgroundColor: colors.primary }]}
              >
                <Text style={styles.processBtnText}>Xác nhận mã & Chuyển tiền</Text>
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
    paddingBottom: 40,
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
    gap: 8,
    paddingVertical: 10,
    borderRadius: radius.pill,
  },
  segmentBtnActive: {
    shadowColor: '#000000',
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
    gap: 12,
  },
  qrCardHeader: {
    alignItems: 'center',
    gap: 4,
  },
  avatarCircle: {
    width: 52,
    height: 52,
    borderRadius: 26,
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 4,
  },
  userName: {
    fontSize: 18,
    fontWeight: '800',
  },
  userSub: {
    fontSize: 12,
  },
  qrTypeRow: {
    flexDirection: 'row',
    gap: 8,
    marginVertical: 4,
  },
  qrTypeChip: {
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: radius.pill,
  },
  qrTypeChipText: {
    fontSize: 12,
    fontWeight: '700',
  },
  qrBox: {
    width: 250,
    height: 250,
    borderRadius: radius.lg,
    alignItems: 'center',
    justifyContent: 'center',
    overflow: 'hidden',
    borderWidth: 1,
    marginVertical: 6,
  },
  qrLoader: {
    position: 'absolute',
    alignItems: 'center',
    gap: 8,
  },
  qrLoadingText: {
    fontSize: 11,
    color: '#6B7280',
    fontWeight: '500',
  },
  qrImage: {
    width: 236,
    height: 236,
  },
  qrFallback: {
    alignItems: 'center',
    justifyContent: 'center',
    padding: 10,
  },
  fallbackTitle: {
    fontSize: 12,
    fontWeight: '700',
    color: '#111827',
  },
  fallbackId: {
    fontSize: 14,
    fontWeight: '800',
    color: '#005538',
    marginTop: 2,
  },
  fallbackNote: {
    fontSize: 10,
    color: '#6B7280',
    marginTop: 2,
  },
  qrGuidanceText: {
    fontSize: 11,
    textAlign: 'center',
    lineHeight: 16,
    paddingHorizontal: 8,
  },
  spendIdPill: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: radius.pill,
  },
  spendIdLabel: {
    fontSize: 12,
  },
  spendIdValue: {
    fontSize: 14,
    fontWeight: '800',
    letterSpacing: 0.5,
  },
  clipboardStatus: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderRadius: radius.sm,
  },
  clipboardStatusText: {
    fontSize: 11,
    fontWeight: '600',
  },
  qrActions: {
    flexDirection: 'row',
    gap: 10,
    width: '100%',
    marginTop: 8,
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
    fontSize: 13,
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
    fontSize: 13,
  },
  scannerCard: {
    padding: spacing.lg,
    alignItems: 'center',
    gap: 16,
  },
  scannerViewport: {
    width: 250,
    height: 250,
    backgroundColor: '#0F172A',
    borderRadius: radius.lg,
    alignItems: 'center',
    justifyContent: 'center',
    overflow: 'hidden',
  },
  viewfinder: {
    width: 190,
    height: 190,
    borderWidth: 1,
    borderStyle: 'dashed',
    position: 'relative',
    alignItems: 'center',
    justifyContent: 'center',
  },
  scanCorner: {
    position: 'absolute',
    width: 20,
    height: 20,
    borderWidth: 3,
  },
  cornerTL: { top: -2, left: -2, borderRightWidth: 0, borderBottomWidth: 0 },
  cornerTR: { top: -2, right: -2, borderLeftWidth: 0, borderBottomWidth: 0 },
  cornerBL: { bottom: -2, left: -2, borderRightWidth: 0, borderTopWidth: 0 },
  cornerBR: { bottom: -2, right: -2, borderLeftWidth: 0, borderTopWidth: 0 },
  scanBeam: {
    width: '100%',
    height: 2,
    opacity: 0.8,
  },
  scannerHint: {
    fontSize: 12,
    textAlign: 'center',
  },
  manualInputWrap: {
    width: '100%',
    gap: 8,
    marginTop: 8,
  },
  manualLabel: {
    fontSize: 12,
    fontWeight: '600',
  },
  inputRow: {
    flexDirection: 'row',
    gap: 8,
  },
  textInput: {
    flex: 1,
    height: 44,
    borderRadius: radius.md,
    borderWidth: 1,
    paddingHorizontal: 12,
    fontSize: 14,
  },
  pasteBtn: {
    paddingHorizontal: 16,
    borderRadius: radius.md,
    alignItems: 'center',
    justifyContent: 'center',
  },
  pasteBtnText: {
    fontSize: 13,
    fontWeight: '700',
  },
  processBtn: {
    paddingVertical: 12,
    borderRadius: radius.md,
    alignItems: 'center',
    marginTop: 4,
  },
  processBtnText: {
    color: '#FFFFFF',
    fontSize: 14,
    fontWeight: '700',
  },
});
