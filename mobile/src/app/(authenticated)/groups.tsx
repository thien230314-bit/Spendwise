import React, { useState, useEffect, useCallback } from 'react';
import {
  StyleSheet,
  Text,
  View,
  ScrollView,
  Pressable,
  TextInput,
  Alert,
  Modal,
  ActivityIndicator,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useAuth } from '@/auth/AuthProvider';
import { AppScreen } from '@/components/AppScreen';
import { GlassCard } from '@/components/GlassCard';
import { useTheme } from '@/theme/useTheme';
import { radius, spacing } from '@/theme/tokens';
import {
  fundsApi,
  V1FundDto,
  V1MemberDto,
  V1FundRequestDto,
  V1FinanceDto,
} from '@/api/fundsApi';

export default function GroupsScreen() {
  const { user } = useAuth();
  const { colors, isDark } = useTheme();

  // State
  const [funds, setFunds] = useState<V1FundDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [isDemoMode, setIsDemoMode] = useState(false);
  const [filterType, setFilterType] = useState<'all' | 'Fixed' | 'Free'>('all');
  const [selectedFund, setSelectedFund] = useState<V1FundDto | null>(null);
  const [selectedFinance, setSelectedFinance] = useState<V1FinanceDto | null>(null);
  const [selectedMembers, setSelectedMembers] = useState<V1MemberDto[]>([]);
  const [selectedRequests, setSelectedRequests] = useState<V1FundRequestDto[]>([]);
  const [detailTab, setDetailTab] = useState<'overview' | 'requests' | 'members' | 'governance'>('overview');

  // Create Fund Modal
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [newFundName, setNewFundName] = useState('');
  const [newFundKind, setNewFundKind] = useState<'Fixed' | 'Free'>('Fixed');
  const [newFundTarget, setNewFundTarget] = useState('9000000');
  const [newFundDurationHours, setNewFundDurationHours] = useState('24'); // 1 to 720h per fixed.md
  const [trustCommitmentAccepted, setTrustCommitmentAccepted] = useState(false);
  const [securityPin, setSecurityPin] = useState('');

  // Action Modals
  const [isContributeModalOpen, setIsContributeModalOpen] = useState(false);
  const [contributeAmount, setContributeAmount] = useState('');
  const [isRequestModalOpen, setIsRequestModalOpen] = useState(false);
  const [requestKind, setRequestKind] = useState<'Expense' | 'Withdrawal'>('Expense');
  const [requestAmount, setRequestAmount] = useState('');
  const [requestPurpose, setRequestPurpose] = useState('');
  const [requestRecipient, setRequestRecipient] = useState('');
  const [actionPin, setActionPin] = useState('');

  // Fallback fixtures honoring Fixed Fund V2 (max 3 cycles!) and Free Fund V1
  const getInitialFallbackFunds = (): V1FundDto[] => [
    {
      id: 'grp-fixed-01',
      name: 'Quỹ Du lịch Đà Nẵng 2026',
      kind: 'Fixed',
      targetVnd: 9000000,
      contributionDurationHours: 24,
      trustCommitmentAccepted: true,
      status: 'Đang hoạt động (Kỳ 2/3)',
      currentCycle: 2,
      maxCycles: 3,
      balanceVnd: 9000000,
      availableSpendableVnd: 9000000,
      heldAmountVnd: 0,
      myContributionVnd: 3000000,
      myRole: 'owner',
      canSpend: true,
      effectiveCanSpend: true,
      membersCount: 3,
    },
    {
      id: 'grp-free-01',
      name: 'Quỹ Ăn trưa Văn phòng',
      kind: 'Free',
      targetVnd: null,
      contributionDurationHours: null,
      status: 'Đóng góp tự nguyện',
      currentCycle: 1,
      maxCycles: 1,
      balanceVnd: 4200000,
      availableSpendableVnd: 3400000, // 800.000 held by ApprovedUnused expense
      heldAmountVnd: 800000,
      myContributionVnd: 1200000,
      myRole: 'member',
      canSpend: false,
      effectiveCanSpend: false,
      membersCount: 4,
    },
    {
      id: 'grp-fixed-02',
      name: 'Quỹ Dự án Team Công nghệ',
      kind: 'Fixed',
      targetVnd: 15000000,
      contributionDurationHours: 72,
      trustCommitmentAccepted: true,
      status: 'Đang hoạt động (Kỳ 3/3 - Kỳ cuối)',
      currentCycle: 3,
      maxCycles: 3,
      balanceVnd: 15000000,
      availableSpendableVnd: 15000000,
      heldAmountVnd: 0,
      myContributionVnd: 5000000,
      myRole: 'member',
      canSpend: true,
      effectiveCanSpend: true,
      membersCount: 3,
    },
  ];

  const loadFunds = useCallback(async () => {
    try {
      const liveFunds = await fundsApi.getFunds();
      if (liveFunds && liveFunds.length > 0) {
        setFunds(liveFunds);
        setIsDemoMode(false);
      } else {
        setFunds(getInitialFallbackFunds());
        setIsDemoMode(true);
      }
    } catch {
      // Backend not running / offline sandbox fallback
      setFunds(getInitialFallbackFunds());
      setIsDemoMode(true);
    }
  }, []);

  useEffect(() => {
    (async () => {
      await loadFunds();
    })();
  }, [loadFunds]);

  const handleRefresh = async () => {
    setLoading(true);
    await loadFunds();
    setLoading(false);
  };

  const loadFundDetails = async (fund: V1FundDto) => {
    setSelectedFund(fund);
    try {
      const [members, reqs, fin] = await Promise.all([
        fundsApi.getMembers(fund.id).catch(() => []),
        fundsApi.getRequests(fund.id).catch(() => []),
        fundsApi.getFinance(fund.id).catch(() => null),
      ]);
      setSelectedMembers(members);
      setSelectedRequests(reqs);
      setSelectedFinance(fin);
    } catch {
      // Fallback detail mock
      setSelectedMembers([
        {
          userId: user?.id || 'usr-me',
          fullName: user?.fullName || 'Bạn',
          seat: 1,
          isOwner: fund.myRole === 'owner',
          canSpend: Boolean(fund.canSpend),
          effectiveCanSpend: Boolean(fund.effectiveCanSpend),
          individualContributionCompleted: true,
          groupFundingCompleted: true,
          canManageSpendingPermission: fund.myRole === 'owner',
          joinedAt: '01/11/2026',
          equityVnd: fund.myContributionVnd,
          contributedVnd: fund.myContributionVnd,
        },
      ]);
      setSelectedRequests(
        fund.kind === 'Free'
          ? [
              {
                id: 'req-free-01',
                fundId: fund.id,
                requestedByUserId: 'usr-member-2',
                recipientUserId: 'usr-member-2',
                recipientName: 'Trần Mai',
                kind: 'Expense',
                amountVnd: 800000,
                purpose: 'Mua nước uống & đồ ăn vặt tuần',
                state: 'ApprovedUnused',
                createdAt: '03/11/2026 10:00',
              },
            ]
          : []
      );
      setSelectedFinance(null);
    }
  };

  const filteredFunds = funds.filter((f) => {
    if (filterType === 'Fixed') return f.kind === 'Fixed';
    if (filterType === 'Free') return f.kind === 'Free';
    return true;
  });

  const totalCapital = funds.reduce((acc, cur) => acc + (cur.myContributionVnd || 0), 0);

  // Handle Create Fund following fixed.md & User.md
  const handleCreateFund = async () => {
    if (!newFundName.trim()) {
      Alert.alert('Lỗi nhập liệu', 'Vui lòng nhập tên quỹ.');
      return;
    }
    if (newFundKind === 'Fixed') {
      const target = parseInt(newFundTarget.replace(/\D/g, ''), 10);
      const hours = parseInt(newFundDurationHours.replace(/\D/g, ''), 10);
      if (!target || target <= 0) {
        Alert.alert('Lỗi Fixed Fund V2', 'Mục tiêu đóng góp phải lớn hơn 0 ₫.');
        return;
      }
      if (!hours || hours < 1 || hours > 720) {
        Alert.alert('Lỗi Fixed Fund V2', 'Thời lượng góp cá nhân phải từ 1 đến 720 giờ (tối đa 30 ngày).');
        return;
      }
      if (!trustCommitmentAccepted) {
        Alert.alert(
          'Cam kết tin tưởng bắt buộc',
          'Bạn phải đồng ý với cam kết tin tưởng Fixed Fund V2 trước khi khởi tạo nhóm.'
        );
        return;
      }
      if (!securityPin || securityPin.length !== 6) {
        Alert.alert('Lỗi bảo mật', 'Vui lòng nhập mã PIN bảo mật 6 số.');
        return;
      }

      try {
        const result = await fundsApi.createFund({
          name: newFundName.trim(),
          kind: 'Fixed',
          targetVnd: target,
          contributionDurationHours: hours,
          trustCommitmentAccepted: true,
          commitmentVersion: 'v2.0',
          securityPin,
        });
        Alert.alert('Thành công (Backend)', `Đã tạo Fixed Fund V2: "${result.name}"`);
        setIsCreateModalOpen(false);
        loadFunds();
      } catch (err: any) {
        if (isDemoMode) {
          const newF: V1FundDto = {
            id: `grp-fixed-${Date.now()}`,
            name: newFundName.trim(),
            kind: 'Fixed',
            targetVnd: target,
            contributionDurationHours: hours,
            trustCommitmentAccepted: true,
            status: 'Khởi tạo (Kỳ 1/3)',
            currentCycle: 1,
            maxCycles: 3,
            balanceVnd: 0,
            availableSpendableVnd: 0,
            heldAmountVnd: 0,
            myContributionVnd: 0,
            myRole: 'owner',
            canSpend: true,
            effectiveCanSpend: false,
            membersCount: 1,
          };
          setFunds([newF, ...funds]);
          setIsCreateModalOpen(false);
          Alert.alert(
            '[DEMO SANDBOX]',
            `Mô phỏng tạo Fixed Fund V2: "${newF.name}" (Kỳ 1/3, hạn góp: ${hours}h, mục tiêu: ${target.toLocaleString('vi-VN')} ₫). Khi kết nối Backend, yêu cầu xác thực API.`
          );
        } else {
          Alert.alert('Lỗi tạo quỹ', err?.message || 'Không thể tạo quỹ từ Backend.');
        }
      }
    } else {
      // Free Fund V1: targetVnd is null, contributionDurationHours is null
      if (!securityPin || securityPin.length !== 6) {
        Alert.alert('Lỗi bảo mật', 'Vui lòng nhập mã PIN bảo mật 6 số.');
        return;
      }
      try {
        const result = await fundsApi.createFund({
          name: newFundName.trim(),
          kind: 'Free',
          targetVnd: null,
          securityPin,
        });
        Alert.alert('Thành công (Backend)', `Đã tạo Free Fund V1: "${result.name}"`);
        setIsCreateModalOpen(false);
        loadFunds();
      } catch (err: any) {
        if (isDemoMode) {
          const newF: V1FundDto = {
            id: `grp-free-${Date.now()}`,
            name: newFundName.trim(),
            kind: 'Free',
            targetVnd: null,
            contributionDurationHours: null,
            status: 'Đóng góp tự nguyện',
            currentCycle: 1,
            maxCycles: 1,
            balanceVnd: 0,
            availableSpendableVnd: 0,
            heldAmountVnd: 0,
            myContributionVnd: 0,
            myRole: 'owner',
            canSpend: false,
            effectiveCanSpend: false,
            membersCount: 1,
          };
          setFunds([newF, ...funds]);
          setIsCreateModalOpen(false);
          Alert.alert(
            '[DEMO SANDBOX]',
            `Mô phỏng tạo Free Fund V1: "${newF.name}". Thành viên góp tự nguyện, vốn động và rút vốn theo luật ApprovedUnused.`
          );
        } else {
          Alert.alert('Lỗi tạo quỹ', err?.message || 'Không thể tạo quỹ từ Backend.');
        }
      }
    }
  };

  // Handle Contribute
  const handleContributeSubmit = async () => {
    if (!selectedFund) return;
    const amount = parseInt(contributeAmount.replace(/\D/g, ''), 10);
    if (!amount || amount <= 0) {
      Alert.alert('Lỗi', 'Vui lòng nhập số tiền hợp lệ');
      return;
    }
    if (!actionPin || actionPin.length !== 6) {
      Alert.alert('Lỗi bảo mật', 'Vui lòng nhập mã PIN 6 số');
      return;
    }

    try {
      await fundsApi.contribute(selectedFund.id, {
        walletId: 'default-wallet',
        amountVnd: amount,
        idempotencyKey: `contrib-${Date.now()}`,
        securityPin: actionPin,
        purpose: selectedFund.kind === 'Fixed' ? 'Đóng nghĩa vụ kỳ' : 'Góp vốn tự nguyện',
      });
      Alert.alert('Thành công (Backend)', `Đã xác nhận đóng góp ${amount.toLocaleString('vi-VN')} ₫ vào quỹ.`);
      setIsContributeModalOpen(false);
      setContributeAmount('');
      setActionPin('');
      loadFunds();
    } catch (err: any) {
      if (isDemoMode) {
        Alert.alert(
          '[DEMO SANDBOX - CẢNH BÁO]',
          `Thao tác đóng góp ${amount.toLocaleString('vi-VN')} ₫ là mô phỏng trên giao diện kiểm thử. Số dư thực tế chỉ thay đổi khi bút toán sổ cái Backend được xác nhận thành công.`
        );
        setIsContributeModalOpen(false);
        setContributeAmount('');
        setActionPin('');
      } else {
        Alert.alert('Lỗi đóng góp', err?.message || 'Không thể đóng góp vào quỹ.');
      }
    }
  };

  // Handle Request (Fixed Direct Expense vs Free Expense / Withdrawal)
  const handleRequestSubmit = async () => {
    if (!selectedFund) return;
    const amount = parseInt(requestAmount.replace(/\D/g, ''), 10);
    if (!amount || amount <= 0) {
      Alert.alert('Lỗi', 'Vui lòng nhập số tiền hợp lệ');
      return;
    }
    if (!requestPurpose.trim()) {
      Alert.alert('Lỗi', 'Vui lòng nhập mục đích chi/rút');
      return;
    }
    if (!actionPin || actionPin.length !== 6) {
      Alert.alert('Lỗi bảo mật', 'Vui lòng nhập mã PIN 6 số');
      return;
    }

    // Check Free Fund safety rule for Withdrawal
    if (selectedFund.kind === 'Free' && requestKind === 'Withdrawal') {
      const hasActiveApprovedExpense = selectedRequests.some((r) => r.state === 'ApprovedUnused');
      if (hasActiveApprovedExpense) {
        Alert.alert(
          'Quy tắc an toàn Free Fund V1',
          'Quỹ hiện đang có khoản chi phí đã duyệt nhưng chưa thanh toán (ApprovedUnused). Cổng an toàn đang chặn yêu cầu rút vốn để bảo toàn thanh khoản.'
        );
        return;
      }
    }

    try {
      await fundsApi.createRequest(selectedFund.id, {
        kind: requestKind,
        amountVnd: amount,
        recipientUserId: requestRecipient || user?.id || 'usr-recipient',
        purpose: requestPurpose.trim(),
        idempotencyKey: `req-${Date.now()}`,
        securityPin: actionPin,
      });
      Alert.alert('Thành công (Backend)', `Đã gửi yêu cầu ${requestKind} thành công.`);
      setIsRequestModalOpen(false);
      setRequestAmount('');
      setRequestPurpose('');
      setActionPin('');
      loadFundDetails(selectedFund);
    } catch (err: any) {
      if (isDemoMode) {
        Alert.alert(
          '[DEMO SANDBOX - MÔ PHỎNG]',
          `Đã ghi nhận yêu cầu ${requestKind}: ${amount.toLocaleString('vi-VN')} ₫. Trong môi trường thực tế, yêu cầu chỉ có hiệu lực khi có xác nhận từ Backend.`
        );
        setIsRequestModalOpen(false);
        setRequestAmount('');
        setRequestPurpose('');
        setActionPin('');
      } else {
        Alert.alert('Lỗi tạo yêu cầu', err?.message || 'Không thể tạo yêu cầu chi.');
      }
    }
  };

  // Close Activity / Cycle (Fixed Fund V2)
  const handleCloseActivity = async () => {
    if (!selectedFund) return;
    Alert.alert(
      'Đóng hoạt động kỳ (Fixed Fund V2)',
      'Bạn là Chủ quỹ. Đóng kỳ hoạt động hiện tại để bắt đầu quyết toán hoặc đề xuất kỳ kế tiếp (tối đa 3 kỳ)?',
      [
        { text: 'Hủy', style: 'cancel' },
        {
          text: 'Xác nhận Đóng kỳ',
          onPress: async () => {
            try {
              await fundsApi.closeActivity(selectedFund.id, '123456');
              Alert.alert('Thành công', 'Đã đóng kỳ hoạt động thành công.');
              loadFunds();
            } catch (err: any) {
              if (isDemoMode) {
                Alert.alert(
                  '[DEMO SANDBOX]',
                  'Mô phỏng đóng kỳ hoạt động Fixed Fund V2. Trạng thái chuyển sang chờ quyết toán hoặc đề xuất kỳ tiếp theo.'
                );
              } else {
                Alert.alert('Lỗi', err?.message || 'Không thể đóng kỳ.');
              }
            }
          },
        },
      ]
    );
  };

  return (
    <AppScreen title="Quỹ nhóm">
      <View style={styles.container}>
        {/* DEMO / LIVE STATUS BADGE */}
        {isDemoMode && (
          <View style={[styles.demoBanner, { backgroundColor: isDark ? '#2D1F0E' : '#FEF3C7', borderColor: colors.amber }]}>
            <Ionicons name="information-circle-outline" size={16} color={colors.amber} />
            <Text style={[styles.demoBannerText, { color: colors.amber }]}>
              CHẾ ĐỘ MÔ PHỎNG (SANDBOX DEMO) • Backend chưa kết nối hoặc chưa có dữ liệu quỹ thực tế.
            </Text>
          </View>
        )}

        {/* 1. PORTFOLIO SUMMARY CARD */}
        <GlassCard style={styles.summaryCard}>
          <View style={styles.summaryTop}>
            <View>
              <Text style={[styles.summaryLabel, { color: colors.textMuted }]}>
                Tổng vốn góp của bạn tại các quỹ
              </Text>
              <Text style={[styles.summaryAmount, { color: colors.text }]}>
                {totalCapital.toLocaleString('vi-VN')} ₫
              </Text>
            </View>
            <View style={{ flexDirection: 'row', gap: 8, alignItems: 'center' }}>
              <Pressable
                accessibilityRole="button"
                accessibilityLabel="Làm mới danh sách quỹ"
                onPress={handleRefresh}
                style={[styles.refreshIconBtn, { backgroundColor: isDark ? colors.surfaceElevated : '#E6F8F0' }]}
              >
                {loading ? (
                  <ActivityIndicator size="small" color={colors.primary} />
                ) : (
                  <Ionicons name="refresh" size={16} color={colors.primary} />
                )}
              </Pressable>
              <Pressable
                accessibilityRole="button"
                accessibilityLabel="Tạo quỹ mới"
                onPress={() => setIsCreateModalOpen(true)}
                style={[styles.createBtn, { backgroundColor: colors.primary }]}
              >
                <Ionicons name="add" size={18} color="#FFFFFF" />
                <Text style={styles.createBtnText}>Tạo quỹ</Text>
              </Pressable>
            </View>
          </View>

          <View style={[styles.summaryFooter, { borderTopColor: colors.border }]}>
            <View style={styles.summaryStat}>
              <Text style={[styles.statValue, { color: colors.primary }]}>{funds.length}</Text>
              <Text style={[styles.statLabel, { color: colors.textMuted }]}>Tổng số quỹ</Text>
            </View>
            <View style={[styles.statDivider, { backgroundColor: colors.border }]} />
            <View style={styles.summaryStat}>
              <Text style={[styles.statValue, { color: colors.accent }]}>
                {funds.filter((g) => g.kind === 'Fixed').length}
              </Text>
              <Text style={[styles.statLabel, { color: colors.textMuted }]}>Cố định V2 (max 3 kỳ)</Text>
            </View>
            <View style={[styles.statDivider, { backgroundColor: colors.border }]} />
            <View style={styles.summaryStat}>
              <Text style={[styles.statValue, { color: colors.violet }]}>
                {funds.filter((g) => g.kind === 'Free').length}
              </Text>
              <Text style={[styles.statLabel, { color: colors.textMuted }]}>Tự do V1 (Vốn động)</Text>
            </View>
          </View>
        </GlassCard>

        {/* 2. FILTER SEGMENTS */}
        <View style={[styles.segmentContainer, { backgroundColor: isDark ? colors.surfaceElevated : '#DCEEE4' }]}>
          <Pressable
            onPress={() => setFilterType('all')}
            style={[styles.segmentBtn, filterType === 'all' && [styles.segmentBtnActive, { backgroundColor: colors.surface }]]}
          >
            <Text style={[styles.segmentText, { color: filterType === 'all' ? colors.primary : colors.textMuted }]}>
              Tất cả ({funds.length})
            </Text>
          </Pressable>
          <Pressable
            onPress={() => setFilterType('Fixed')}
            style={[styles.segmentBtn, filterType === 'Fixed' && [styles.segmentBtnActive, { backgroundColor: colors.surface }]]}
          >
            <Text style={[styles.segmentText, { color: filterType === 'Fixed' ? colors.primary : colors.textMuted }]}>
              Cố định V2
            </Text>
          </Pressable>
          <Pressable
            onPress={() => setFilterType('Free')}
            style={[styles.segmentBtn, filterType === 'Free' && [styles.segmentBtnActive, { backgroundColor: colors.surface }]]}
          >
            <Text style={[styles.segmentText, { color: filterType === 'Free' ? colors.primary : colors.textMuted }]}>
              Tự do V1
            </Text>
          </Pressable>
        </View>

        {/* 3. GROUP FUND LIST */}
        {loading ? (
          <ActivityIndicator size="large" color={colors.primary} style={{ marginVertical: 32 }} />
        ) : (
          <View style={styles.fundList}>
            {filteredFunds.map((fund) => {
              const isFixed = fund.kind === 'Fixed';
              const progress = fund.targetVnd
                ? Math.min(100, Math.round(((fund.balanceVnd || 0) / fund.targetVnd) * 100))
                : 0;

              return (
                <Pressable
                  key={fund.id}
                  onPress={() => loadFundDetails(fund)}
                  style={({ pressed }) => [{ opacity: pressed ? 0.85 : 1 }]}
                >
                  <GlassCard style={styles.fundCard}>
                    {/* Header: Kind & Role Badge */}
                    <View style={styles.cardHeaderRow}>
                      <View
                        style={[
                          styles.fundTypeBadge,
                          {
                            backgroundColor: isFixed
                              ? (isDark ? '#0F3D2E' : '#B8F0D8')
                              : (isDark ? '#2E1A47' : '#EDE9FE'),
                            borderColor: isFixed ? colors.primary : colors.violet,
                          },
                        ]}
                      >
                        <Ionicons
                          name={isFixed ? 'shield-checkmark' : 'sparkles'}
                          size={12}
                          color={isFixed ? colors.primary : colors.violet}
                        />
                        <Text
                          style={[
                            styles.fundTypeBadgeText,
                            { color: isFixed ? colors.primary : colors.violet },
                          ]}
                        >
                          {isFixed ? 'Quỹ Cố định V2' : 'Quỹ Tự do V1'}
                        </Text>
                      </View>

                      <View style={styles.roleContainer}>
                        <View style={[styles.roleBadge, { backgroundColor: fund.myRole === 'owner' ? colors.primarySoft : colors.muted }]}>
                          <Text style={[styles.roleBadgeText, { color: fund.myRole === 'owner' ? colors.primary : colors.textMuted }]}>
                            {fund.myRole === 'owner' ? 'Trưởng quỹ' : 'Thành viên'}
                          </Text>
                        </View>
                        <Ionicons name="chevron-forward" size={16} color={colors.textMuted} />
                      </View>
                    </View>

                    {/* Title & Cycle Status */}
                    <Text style={[styles.fundName, { color: colors.text }]}>{fund.name}</Text>
                    <Text style={[styles.fundCycle, { color: colors.textMuted }]}>
                      {isFixed
                        ? `Chu kỳ ${fund.currentCycle || 1}/3 • Hạn góp ${fund.contributionDurationHours || 24}h • Chia đều VND`
                        : 'Vốn động linh hoạt • Tối đa 6 thành viên • ApprovedUnused 7 ngày'}
                    </Text>

                    {/* Balance */}
                    <View style={styles.fundBalanceRow}>
                      <View>
                        <Text style={[styles.fundBalanceLabel, { color: colors.textMuted }]}>
                          {isFixed ? 'Số dư khả dụng chi trực tiếp' : 'Số dư khả dụng của quỹ'}
                        </Text>
                        <Text style={[styles.fundBalanceValue, { color: colors.text }]}>
                          {(fund.availableSpendableVnd ?? fund.balanceVnd ?? 0).toLocaleString('vi-VN')} ₫
                        </Text>
                      </View>
                      {(fund.heldAmountVnd || 0) > 0 && (
                        <View style={styles.heldBadge}>
                          <Text style={[styles.heldLabel, { color: colors.amber }]}>
                            Giữ chỗ chi: {(fund.heldAmountVnd || 0).toLocaleString('vi-VN')} ₫
                          </Text>
                        </View>
                      )}
                    </View>

                    {/* Progress for Fixed Fund V2 */}
                    {isFixed && fund.targetVnd && (
                      <View style={styles.progressContainer}>
                        <View style={styles.progressLabels}>
                          <Text style={[styles.progressText, { color: colors.textMuted }]}>
                            Đạt mục tiêu: {progress}%
                          </Text>
                          <Text style={[styles.progressText, { color: colors.textMuted }]}>
                            Mục tiêu kỳ: {fund.targetVnd.toLocaleString('vi-VN')} ₫
                          </Text>
                        </View>
                        <View style={[styles.progressBarTrack, { backgroundColor: isDark ? '#1C382F' : '#D0EADB' }]}>
                          <View style={[styles.progressBarFill, { width: `${progress}%`, backgroundColor: colors.primary }]} />
                        </View>
                      </View>
                    )}

                    {/* Footer */}
                    <View style={[styles.cardFooter, { borderTopColor: colors.border }]}>
                      <Text style={[styles.memberInfo, { color: colors.textMuted }]}>
                        {fund.membersCount || 1} thành viên • Vốn của bạn: {(fund.myContributionVnd || 0).toLocaleString('vi-VN')} ₫
                      </Text>
                      <View style={styles.cardActionsRow}>
                        <Pressable
                          onPress={() => {
                            setSelectedFund(fund);
                            setIsContributeModalOpen(true);
                          }}
                          style={[styles.miniBtn, { backgroundColor: colors.primary }]}
                        >
                          <Text style={styles.miniBtnText}>Góp tiền</Text>
                        </Pressable>
                        <Pressable
                          onPress={() => {
                            setSelectedFund(fund);
                            setIsRequestModalOpen(true);
                          }}
                          style={[styles.miniBtnOutline, { borderColor: colors.primary }]}
                        >
                          <Text style={[styles.miniBtnOutlineText, { color: colors.primary }]}>
                            {isFixed ? 'Chi chung' : 'Yêu cầu'}
                          </Text>
                        </Pressable>
                      </View>
                    </View>
                  </GlassCard>
                </Pressable>
              );
            })}
          </View>
        )}

        {/* 4. DETAIL MODAL (Fixed Fund V2 & Free Fund V1 Compliant) */}
        {selectedFund && (
          <Modal
            visible={Boolean(selectedFund)}
            animationType="slide"
            transparent
            onRequestClose={() => setSelectedFund(null)}
          >
            <View style={styles.modalOverlay}>
              <View style={[styles.modalSheet, { backgroundColor: colors.surface }]}>
                {/* Modal Header */}
                <View style={styles.modalHeader}>
                  <View style={{ flex: 1 }}>
                    <Text style={[styles.modalTitle, { color: colors.text }]}>{selectedFund.name}</Text>
                    <Text style={[styles.modalSub, { color: colors.textMuted }]}>
                      {selectedFund.kind === 'Fixed'
                        ? `Fixed Fund V2 • Kỳ ${selectedFund.currentCycle || 1}/3 • Auto-Kick & Thay ghế 72h`
                        : 'Free Fund V1 • Vốn động • 2 nút Expense/Withdrawal • Cổng chặn rút tiền an toàn'}
                    </Text>
                  </View>
                  <Pressable onPress={() => setSelectedFund(null)} style={styles.modalCloseBtn}>
                    <Ionicons name="close" size={24} color={colors.text} />
                  </Pressable>
                </View>

                {/* Detail Tabs */}
                <View style={[styles.detailTabRow, { borderBottomColor: colors.border }]}>
                  <Pressable
                    onPress={() => setDetailTab('overview')}
                    style={[styles.detailTabItem, detailTab === 'overview' && { borderBottomColor: colors.primary }]}
                  >
                    <Text style={[styles.detailTabText, { color: detailTab === 'overview' ? colors.primary : colors.textMuted }]}>
                      Tổng quan
                    </Text>
                  </Pressable>
                  <Pressable
                    onPress={() => setDetailTab('requests')}
                    style={[styles.detailTabItem, detailTab === 'requests' && { borderBottomColor: colors.primary }]}
                  >
                    <Text style={[styles.detailTabText, { color: detailTab === 'requests' ? colors.primary : colors.textMuted }]}>
                      Yêu cầu ({selectedRequests.length})
                    </Text>
                  </Pressable>
                  <Pressable
                    onPress={() => setDetailTab('members')}
                    style={[styles.detailTabItem, detailTab === 'members' && { borderBottomColor: colors.primary }]}
                  >
                    <Text style={[styles.detailTabText, { color: detailTab === 'members' ? colors.primary : colors.textMuted }]}>
                      Thành viên ({selectedMembers.length})
                    </Text>
                  </Pressable>
                  <Pressable
                    onPress={() => setDetailTab('governance')}
                    style={[styles.detailTabItem, detailTab === 'governance' && { borderBottomColor: colors.primary }]}
                  >
                    <Text style={[styles.detailTabText, { color: detailTab === 'governance' ? colors.primary : colors.textMuted }]}>
                      Quản trị & Bầu cử
                    </Text>
                  </Pressable>
                </View>

                <ScrollView style={styles.modalBody}>
                  {detailTab === 'overview' && (
                    <View style={styles.tabContent}>
                      <View style={[styles.heroDetailBox, { backgroundColor: isDark ? colors.surfaceElevated : '#E6F8F0' }]}>
                        <Text style={[styles.heroDetailLabel, { color: colors.textMuted }]}>
                          Số dư khả dụng hiện tại
                        </Text>
                        <Text style={[styles.heroDetailAmount, { color: colors.primary }]}>
                          {(selectedFund.availableSpendableVnd ?? selectedFund.balanceVnd ?? 0).toLocaleString('vi-VN')} ₫
                        </Text>
                        <Text style={[styles.heroDetailSub, { color: colors.textSecondary }]}>
                          Tổng quỹ: {(selectedFund.balanceVnd || 0).toLocaleString('vi-VN')} ₫ • Đã giữ chỗ: {(selectedFund.heldAmountVnd || 0).toLocaleString('vi-VN')} ₫
                        </Text>
                      </View>

                      {/* Detail Metrics */}
                      <View style={styles.detailGrid}>
                        <View style={[styles.detailCell, { borderColor: colors.border }]}>
                          <Text style={[styles.cellLabel, { color: colors.textMuted }]}>Vốn thực có của bạn</Text>
                          <Text style={[styles.cellVal, { color: colors.text }]}>
                            {(selectedFund.myContributionVnd || 0).toLocaleString('vi-VN')} ₫
                          </Text>
                        </View>
                        <View style={[styles.detailCell, { borderColor: colors.border }]}>
                          <Text style={[styles.cellLabel, { color: colors.textMuted }]}>
                            {selectedFund.kind === 'Fixed' ? 'Quyền chi (CanSpend)' : 'Vai trò'}
                          </Text>
                          <Text style={[styles.cellVal, { color: selectedFund.canSpend ? colors.primary : colors.text }]}>
                            {selectedFund.kind === 'Fixed'
                              ? selectedFund.canSpend ? 'Được cấp quyền chi' : 'Chỉ xem'
                              : selectedFund.myRole === 'owner' ? 'Chủ quỹ (Duyệt)' : 'Thành viên'}
                          </Text>
                        </View>
                      </View>

                      {/* CTAs */}
                      <View style={styles.detailCtaRow}>
                        <Pressable
                          onPress={() => setIsContributeModalOpen(true)}
                          style={[styles.ctaPrimary, { backgroundColor: colors.primary }]}
                        >
                          <Ionicons name="arrow-down" size={18} color="#FFFFFF" />
                          <Text style={styles.ctaPrimaryText}>Đóng góp vốn</Text>
                        </Pressable>

                        <Pressable
                          onPress={() => setIsRequestModalOpen(true)}
                          style={[styles.ctaSecondary, { borderColor: colors.primary }]}
                        >
                          <Ionicons name="card-outline" size={18} color={colors.primary} />
                          <Text style={[styles.ctaSecondaryText, { color: colors.primary }]}>
                            {selectedFund.kind === 'Fixed' ? 'Chi chung trực tiếp' : 'Tạo yêu cầu Chi/Rút'}
                          </Text>
                        </Pressable>
                      </View>

                      {selectedFund.kind === 'Fixed' && selectedFund.myRole === 'owner' && (
                        <Pressable
                          onPress={handleCloseActivity}
                          style={[styles.closeActivityBtn, { borderColor: colors.amber }]}
                        >
                          <Ionicons name="checkmark-done" size={18} color={colors.amber} />
                          <Text style={[styles.closeActivityBtnText, { color: colors.amber }]}>
                            Đóng hoạt động kỳ này (Hoàn thành kỳ {selectedFund.currentCycle || 1}/3)
                          </Text>
                        </Pressable>
                      )}
                    </View>
                  )}

                  {detailTab === 'requests' && (
                    <View style={styles.tabContent}>
                      {selectedRequests.length === 0 ? (
                        <View style={styles.emptyRequests}>
                          <Ionicons name="checkmark-done-circle-outline" size={48} color={colors.primary} />
                          <Text style={[styles.emptyRequestsTitle, { color: colors.text }]}>Không có yêu cầu nào</Text>
                          <Text style={[styles.emptyRequestsSub, { color: colors.textMuted }]}>
                            {selectedFund.kind === 'Fixed'
                              ? 'Fixed Fund V2 cho phép chi trực tiếp khi đạt mục tiêu và có CanSpend.'
                              : 'Free Fund V1 hỗ trợ yêu cầu Expense và Withdrawal có kiểm soát giữ chỗ 7 ngày.'}
                          </Text>
                        </View>
                      ) : (
                        selectedRequests.map((req) => (
                          <View key={req.id} style={[styles.requestCard, { borderColor: colors.border }]}>
                            <View style={styles.requestHeader}>
                              <View style={styles.reqBadgeRow}>
                                <View
                                  style={[
                                    styles.reqBadge,
                                    {
                                      backgroundColor: req.kind === 'Expense' ? colors.primarySoft : '#FEF3C7',
                                    },
                                  ]}
                                >
                                  <Text
                                    style={[
                                      styles.reqBadgeText,
                                      { color: req.kind === 'Expense' ? colors.primary : colors.amber },
                                    ]}
                                  >
                                    {req.kind === 'Expense' ? 'Chi tiêu chung' : 'Rút vốn cá nhân'}
                                  </Text>
                                </View>
                                <Text style={[styles.reqStateText, { color: colors.textMuted }]}>
                                  {req.state === 'ApprovedUnused'
                                    ? 'Đã duyệt (Giữ chỗ 7 ngày)'
                                    : req.state === 'AwaitingOwnerReview'
                                    ? 'Chờ Chủ quỹ duyệt'
                                    : req.state}
                                </Text>
                              </View>
                              <Text style={[styles.requestAmount, { color: colors.danger }]}>
                                -{req.amountVnd.toLocaleString('vi-VN')} ₫
                              </Text>
                            </View>

                            <Text style={[styles.requestTitle, { color: colors.text }]}>{req.purpose}</Text>
                            <Text style={[styles.requestMeta, { color: colors.textMuted }]}>
                              Người yêu cầu: {req.recipientName || 'Thành viên'} • {req.createdAt}
                            </Text>

                            {/* Owner review actions for Free Fund */}
                            {selectedFund.kind === 'Free' &&
                              selectedFund.myRole === 'owner' &&
                              req.state === 'AwaitingOwnerReview' && (
                                <View style={styles.requestActionRow}>
                                  <Pressable
                                    onPress={async () => {
                                      try {
                                        await fundsApi.reviewRequest(req.id, {
                                          approved: true,
                                          securityPin: '123456',
                                        });
                                        Alert.alert('Thành công', 'Đã duyệt yêu cầu.');
                                        loadFundDetails(selectedFund);
                                      } catch (err: any) {
                                        Alert.alert('Lỗi duyệt', err?.message || 'Không thể duyệt.');
                                      }
                                    }}
                                    style={[styles.miniApproveBtn, { backgroundColor: colors.primary }]}
                                  >
                                    <Text style={styles.miniBtnText}>Duyệt</Text>
                                  </Pressable>
                                  <Pressable
                                    onPress={async () => {
                                      try {
                                        await fundsApi.reviewRequest(req.id, {
                                          approved: false,
                                          securityPin: '123456',
                                        });
                                        Alert.alert('Từ chối', 'Đã từ chối yêu cầu.');
                                        loadFundDetails(selectedFund);
                                      } catch (err: any) {
                                        Alert.alert('Lỗi', err?.message || 'Không thể từ chối.');
                                      }
                                    }}
                                    style={[styles.miniRejectBtn, { borderColor: colors.danger }]}
                                  >
                                    <Text style={[styles.miniRejectText, { color: colors.danger }]}>Từ chối</Text>
                                  </Pressable>
                                </View>
                              )}
                          </View>
                        ))
                      )}
                    </View>
                  )}

                  {detailTab === 'members' && (
                    <View style={styles.tabContent}>
                      {selectedMembers.map((m) => (
                        <View key={m.userId} style={styles.memberRow}>
                          <Ionicons name="person-circle" size={38} color={colors.primary} />
                          <View style={{ flex: 1, marginLeft: 10 }}>
                            <Text style={[styles.memberName, { color: colors.text }]}>{m.fullName}</Text>
                            <Text style={[styles.memberSub, { color: colors.textMuted }]}>
                              {m.isOwner ? 'Chủ quỹ' : 'Thành viên'} • Ghế #{m.seat} • Vốn:{' '}
                              {(m.equityVnd || m.contributedVnd || 0).toLocaleString('vi-VN')} ₫
                            </Text>
                          </View>
                          <View style={[styles.memberBadge, { backgroundColor: colors.primarySoft }]}>
                            <Text style={[styles.memberBadgeText, { color: colors.primary }]}>
                              {selectedFund.kind === 'Fixed'
                                ? m.canSpend ? 'CanSpend: BẬT' : 'Chỉ xem'
                                : 'Vốn động'}
                            </Text>
                          </View>
                        </View>
                      ))}
                    </View>
                  )}

                  {detailTab === 'governance' && (
                    <View style={styles.tabContent}>
                      <Text style={[styles.govTitle, { color: colors.text }]}>
                        {selectedFund.kind === 'Fixed'
                          ? 'Quy chuẩn Fixed Fund V2 (fixed.md)'
                          : 'Quy chuẩn Free Fund V1 (User.md)'}
                      </Text>
                      <Text style={[styles.govCopy, { color: colors.textSecondary }]}>
                        {selectedFund.kind === 'Fixed'
                          ? '• Cam kết tin tưởng bắt buộc trước khi tham gia.\n• Chốt phân bổ chia đều tuyệt đối đến 1 VND theo số ghế.\n• Hết hạn cá nhân không nạp đủ: Auto-Kick và mở cửa sổ thay ghế 72h.\n• Khi đủ target, người có CanSpend được chi chung trực tiếp.\n• Tối đa 3 kỳ hoạt động; chuyển kỳ theo cơ chế đồng thuận cá nhân (không bỏ phiếu đa số).\n• Bầu Owner khi chủ quỹ vắng 15 ngày có bằng chứng (Vòng 1: 12h, Vòng 2: 12h bằng PIN).'
                          : '• Vốn động linh hoạt theo công thức Ei = Ci - Si - Wi.\n• Tối đa 6 thành viên; 2 nút Expense và Withdrawal riêng biệt.\n• Cổng chặn rút tiền: Không duyệt rút tiền cá nhân khi còn khoản Expense ApprovedUnused.\n• Quyền chi ApprovedUnused giữ chỗ hạn mức 7 ngày.\n• Thành viên tự do rời quỹ nếu không còn yêu cầu chờ duyệt.'}
                      </Text>

                      {selectedFinance && (
                        <View style={[styles.financeAuditBox, { backgroundColor: isDark ? colors.surfaceElevated : '#F3F4F6', borderColor: colors.border }]}>
                          <Text style={{ fontSize: 11, fontWeight: '700', color: colors.text }}>Trạng thái Sổ cái (Ledger Conformance):</Text>
                          <Text style={{ fontSize: 10, color: colors.textMuted, marginTop: 2 }}>
                            Trạng thái chi: {selectedFinance.spendingState} • Cổng đóng góp: {selectedFinance.contributionsEnabled ? 'Mở' : 'Đóng'}
                          </Text>
                        </View>
                      )}

                      <Pressable
                        onPress={() => {
                          Alert.alert(
                            'Rời quỹ',
                            selectedFund.kind === 'Fixed'
                              ? 'Theo quy tắc Fixed Fund V2, thành viên không được tự ý rời trong kỳ đang hoạt động bình thường. Nếu có tranh chấp, vui lòng tạo khiếu nại đối soát.'
                              : 'Bạn có chắc chắn muốn rời quỹ? Số vốn thực có sẽ được hoàn lại ví cá nhân nếu không còn yêu cầu chờ duyệt.'
                          );
                        }}
                        style={[styles.leaveBtn, { borderColor: colors.danger }]}
                      >
                        <Ionicons name="exit-outline" size={18} color={colors.danger} />
                        <Text style={[styles.leaveBtnText, { color: colors.danger }]}>
                          {selectedFund.kind === 'Fixed' ? 'Rời quỹ (Bị khóa trong kỳ)' : 'Rời quỹ & Tất toán vốn'}
                        </Text>
                      </Pressable>
                    </View>
                  )}
                </ScrollView>
              </View>
            </View>
          </Modal>
        )}

        {/* 5. CREATE FUND MODAL (Fixed V2 & Free V1) */}
        <Modal
          visible={isCreateModalOpen}
          animationType="slide"
          transparent
          onRequestClose={() => setIsCreateModalOpen(false)}
        >
          <View style={styles.modalOverlay}>
            <View style={[styles.modalSheet, { backgroundColor: colors.surface }]}>
              <View style={styles.modalHeader}>
                <Text style={[styles.modalTitle, { color: colors.text }]}>Tạo Quỹ Nhóm Mới</Text>
                <Pressable onPress={() => setIsCreateModalOpen(false)} style={styles.modalCloseBtn}>
                  <Ionicons name="close" size={24} color={colors.text} />
                </Pressable>
              </View>

              <ScrollView style={styles.modalBody}>
                <Text style={[styles.inputLabel, { color: colors.text }]}>Tên Quỹ</Text>
                <TextInput
                  value={newFundName}
                  onChangeText={setNewFundName}
                  placeholder="Ví dụ: Quỹ Du lịch Đà Nẵng / Quỹ Ăn trưa"
                  placeholderTextColor={colors.placeholder}
                  style={[styles.formInput, { backgroundColor: colors.surfaceElevated, borderColor: colors.border, color: colors.text }]}
                />

                <Text style={[styles.inputLabel, { color: colors.text, marginTop: 14 }]}>Loại Quỹ</Text>
                <View style={styles.typeOptionRow}>
                  <Pressable
                    onPress={() => setNewFundKind('Fixed')}
                    style={[
                      styles.typeOptionCard,
                      { borderColor: newFundKind === 'Fixed' ? colors.primary : colors.border },
                      newFundKind === 'Fixed' && { backgroundColor: isDark ? '#0F3D2E' : '#E6F8F0' },
                    ]}
                  >
                    <Ionicons name="shield-checkmark" size={22} color={colors.primary} />
                    <Text style={[styles.typeOptionTitle, { color: colors.text }]}>Cố định V2</Text>
                    <Text style={[styles.typeOptionDesc, { color: colors.textMuted }]}>
                      Tối đa 3 kỳ, hạn góp 1-720h, Auto-Kick, thay ghế 72h, chi trực tiếp khi đủ target.
                    </Text>
                  </Pressable>

                  <Pressable
                    onPress={() => setNewFundKind('Free')}
                    style={[
                      styles.typeOptionCard,
                      { borderColor: newFundKind === 'Free' ? colors.violet : colors.border },
                      newFundKind === 'Free' && { backgroundColor: isDark ? '#2E1A47' : '#F5F3FF' },
                    ]}
                  >
                    <Ionicons name="sparkles" size={22} color={colors.violet} />
                    <Text style={[styles.typeOptionTitle, { color: colors.text }]}>Tự do V1</Text>
                    <Text style={[styles.typeOptionDesc, { color: colors.textMuted }]}>
                      Tối đa 6 người, vốn động, 2 nút Expense/Withdrawal, giữ chỗ 7 ngày.
                    </Text>
                  </Pressable>
                </View>

                {newFundKind === 'Fixed' && (
                  <View style={{ marginTop: 14, gap: 10 }}>
                    <Text style={[styles.inputLabel, { color: colors.text }]}>Mục tiêu góp kỳ 1 (₫)</Text>
                    <TextInput
                      value={newFundTarget}
                      onChangeText={setNewFundTarget}
                      keyboardType="numeric"
                      placeholder="9000000"
                      placeholderTextColor={colors.placeholder}
                      style={[styles.formInput, { backgroundColor: colors.surfaceElevated, borderColor: colors.border, color: colors.text }]}
                    />

                    <Text style={[styles.inputLabel, { color: colors.text }]}>Thời lượng góp cá nhân (1–720 giờ)</Text>
                    <TextInput
                      value={newFundDurationHours}
                      onChangeText={setNewFundDurationHours}
                      keyboardType="numeric"
                      placeholder="24"
                      placeholderTextColor={colors.placeholder}
                      style={[styles.formInput, { backgroundColor: colors.surfaceElevated, borderColor: colors.border, color: colors.text }]}
                    />

                    {/* Trust Commitment Checkbox Box */}
                    <Pressable
                      onPress={() => setTrustCommitmentAccepted(!trustCommitmentAccepted)}
                      style={[styles.trustBox, { borderColor: trustCommitmentAccepted ? colors.primary : colors.border }]}
                    >
                      <Ionicons
                        name={trustCommitmentAccepted ? 'checkbox' : 'square-outline'}
                        size={22}
                        color={trustCommitmentAccepted ? colors.primary : colors.textMuted}
                      />
                      <Text style={[styles.trustText, { color: colors.text }]}>
                        Tôi xác nhận cam kết tin tưởng Fixed Fund V2: hiểu rõ nghĩa vụ đóng góp chốt trước, không tự ý rời trong kỳ, và tuân thủ quy chế kiểm toán.
                      </Text>
                    </Pressable>
                  </View>
                )}

                <Text style={[styles.inputLabel, { color: colors.text, marginTop: 14 }]}>Mã PIN bảo mật 6 số</Text>
                <TextInput
                  value={securityPin}
                  onChangeText={setSecurityPin}
                  keyboardType="numeric"
                  secureTextEntry
                  maxLength={6}
                  placeholder="Nhập 6 số PIN của bạn"
                  placeholderTextColor={colors.placeholder}
                  style={[styles.formInput, { backgroundColor: colors.surfaceElevated, borderColor: colors.border, color: colors.text }]}
                />

                <Pressable
                  onPress={handleCreateFund}
                  style={[styles.createSubmitBtn, { backgroundColor: colors.primary }]}
                >
                  <Text style={styles.createSubmitText}>Khởi tạo Quỹ ngay</Text>
                </Pressable>
              </ScrollView>
            </View>
          </View>
        </Modal>

        {/* 6. CONTRIBUTE MODAL */}
        <Modal
          visible={isContributeModalOpen}
          animationType="slide"
          transparent
          onRequestClose={() => setIsContributeModalOpen(false)}
        >
          <View style={styles.modalOverlay}>
            <View style={[styles.modalSheet, { backgroundColor: colors.surface }]}>
              <View style={styles.modalHeader}>
                <Text style={[styles.modalTitle, { color: colors.text }]}>
                  Đóng góp quỹ: {selectedFund?.name}
                </Text>
                <Pressable onPress={() => setIsContributeModalOpen(false)}>
                  <Ionicons name="close" size={24} color={colors.text} />
                </Pressable>
              </View>

              <View style={styles.modalBody}>
                <Text style={[styles.inputLabel, { color: colors.text }]}>Số tiền đóng góp (₫)</Text>
                <TextInput
                  value={contributeAmount}
                  onChangeText={setContributeAmount}
                  keyboardType="numeric"
                  placeholder="Ví dụ: 1000000"
                  placeholderTextColor={colors.placeholder}
                  style={[styles.formInput, { backgroundColor: colors.surfaceElevated, borderColor: colors.border, color: colors.text }]}
                />

                <Text style={[styles.inputLabel, { color: colors.text, marginTop: 10 }]}>Mã PIN bảo mật 6 số</Text>
                <TextInput
                  value={actionPin}
                  onChangeText={setActionPin}
                  keyboardType="numeric"
                  secureTextEntry
                  maxLength={6}
                  placeholder="••••••"
                  placeholderTextColor={colors.placeholder}
                  style={[styles.formInput, { backgroundColor: colors.surfaceElevated, borderColor: colors.border, color: colors.text }]}
                />

                <Pressable
                  onPress={handleContributeSubmit}
                  style={[styles.createSubmitBtn, { backgroundColor: colors.primary }]}
                >
                  <Text style={styles.createSubmitText}>Xác nhận đóng góp</Text>
                </Pressable>
              </View>
            </View>
          </View>
        </Modal>

        {/* 7. REQUEST MODAL (Expense / Withdrawal) */}
        <Modal
          visible={isRequestModalOpen}
          animationType="slide"
          transparent
          onRequestClose={() => setIsRequestModalOpen(false)}
        >
          <View style={styles.modalOverlay}>
            <View style={[styles.modalSheet, { backgroundColor: colors.surface }]}>
              <View style={styles.modalHeader}>
                <Text style={[styles.modalTitle, { color: colors.text }]}>
                  {selectedFund?.kind === 'Fixed' ? 'Chi chung trực tiếp' : 'Yêu cầu Chi/Rút Free Fund'}
                </Text>
                <Pressable onPress={() => setIsRequestModalOpen(false)}>
                  <Ionicons name="close" size={24} color={colors.text} />
                </Pressable>
              </View>

              <ScrollView style={styles.modalBody}>
                {selectedFund?.kind === 'Free' && (
                  <View style={styles.kindSelectorRow}>
                    <Pressable
                      onPress={() => setRequestKind('Expense')}
                      style={[
                        styles.kindSelectBtn,
                        requestKind === 'Expense' && [styles.kindSelectBtnActive, { backgroundColor: colors.primarySoft }],
                      ]}
                    >
                      <Text style={[styles.kindSelectText, { color: requestKind === 'Expense' ? colors.primary : colors.textMuted }]}>
                        Yêu cầu Chi chung
                      </Text>
                    </Pressable>
                    <Pressable
                      onPress={() => setRequestKind('Withdrawal')}
                      style={[
                        styles.kindSelectBtn,
                        requestKind === 'Withdrawal' && [styles.kindSelectBtnActive, { backgroundColor: '#FEF3C7' }],
                      ]}
                    >
                      <Text style={[styles.kindSelectText, { color: requestKind === 'Withdrawal' ? colors.amber : colors.textMuted }]}>
                        Yêu cầu Rút vốn cá nhân
                      </Text>
                    </Pressable>
                  </View>
                )}

                <Text style={[styles.inputLabel, { color: colors.text }]}>Số tiền (₫)</Text>
                <TextInput
                  value={requestAmount}
                  onChangeText={setRequestAmount}
                  keyboardType="numeric"
                  placeholder="Ví dụ: 500000"
                  placeholderTextColor={colors.placeholder}
                  style={[styles.formInput, { backgroundColor: colors.surfaceElevated, borderColor: colors.border, color: colors.text }]}
                />

                <Text style={[styles.inputLabel, { color: colors.text, marginTop: 10 }]}>Mục đích</Text>
                <TextInput
                  value={requestPurpose}
                  onChangeText={setRequestPurpose}
                  placeholder="Ví dụ: Thanh toán tiền ăn trưa nhóm"
                  placeholderTextColor={colors.placeholder}
                  style={[styles.formInput, { backgroundColor: colors.surfaceElevated, borderColor: colors.border, color: colors.text }]}
                />

                <Text style={[styles.inputLabel, { color: colors.text, marginTop: 10 }]}>Người nhận</Text>
                <TextInput
                  value={requestRecipient}
                  onChangeText={setRequestRecipient}
                  placeholder="Tên hoặc tài khoản người nhận"
                  placeholderTextColor={colors.placeholder}
                  style={[styles.formInput, { backgroundColor: colors.surfaceElevated, borderColor: colors.border, color: colors.text }]}
                />

                <Text style={[styles.inputLabel, { color: colors.text, marginTop: 10 }]}>Mã PIN 6 số</Text>
                <TextInput
                  value={actionPin}
                  onChangeText={setActionPin}
                  keyboardType="numeric"
                  secureTextEntry
                  maxLength={6}
                  placeholder="••••••"
                  placeholderTextColor={colors.placeholder}
                  style={[styles.formInput, { backgroundColor: colors.surfaceElevated, borderColor: colors.border, color: colors.text }]}
                />

                <Pressable
                  onPress={handleRequestSubmit}
                  style={[styles.createSubmitBtn, { backgroundColor: colors.primary }]}
                >
                  <Text style={styles.createSubmitText}>
                    {selectedFund?.kind === 'Fixed' ? 'Xác nhận Chi trực tiếp' : 'Gửi yêu cầu'}
                  </Text>
                </Pressable>
              </ScrollView>
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
  },
  demoBanner: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    padding: 10,
    borderRadius: radius.md,
    borderWidth: 1,
  },
  demoBannerText: {
    fontSize: 11,
    fontWeight: '700',
    flex: 1,
    lineHeight: 16,
  },
  summaryCard: {
    padding: spacing.lg,
  },
  summaryTop: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: spacing.md,
  },
  summaryLabel: {
    fontSize: 13,
    fontWeight: '600',
  },
  summaryAmount: {
    fontSize: 24,
    fontWeight: '800',
    marginTop: 4,
  },
  createBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    paddingHorizontal: 14,
    paddingVertical: 10,
    borderRadius: radius.pill,
  },
  refreshIconBtn: {
    width: 38,
    height: 38,
    borderRadius: 19,
    alignItems: 'center',
    justifyContent: 'center',
  },
  createBtnText: {
    color: '#FFFFFF',
    fontWeight: '700',
    fontSize: 13,
  },
  summaryFooter: {
    flexDirection: 'row',
    borderTopWidth: 1,
    paddingTop: spacing.md,
    justifyContent: 'space-around',
  },
  summaryStat: {
    alignItems: 'center',
  },
  statValue: {
    fontSize: 18,
    fontWeight: '800',
  },
  statLabel: {
    fontSize: 11,
    marginTop: 2,
  },
  statDivider: {
    width: 1,
    height: '80%',
    alignSelf: 'center',
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
    fontSize: 13,
    fontWeight: '700',
  },
  fundList: {
    gap: spacing.md,
  },
  fundCard: {
    padding: spacing.md,
  },
  cardHeaderRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  fundTypeBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderRadius: radius.pill,
    borderWidth: 1,
  },
  fundTypeBadgeText: {
    fontSize: 11,
    fontWeight: '700',
  },
  roleContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  roleBadge: {
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: radius.sm,
  },
  roleBadgeText: {
    fontSize: 11,
    fontWeight: '700',
  },
  fundName: {
    fontSize: 17,
    fontWeight: '700',
    marginTop: 8,
  },
  fundCycle: {
    fontSize: 12,
    marginTop: 2,
  },
  fundBalanceRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-end',
    marginTop: 12,
  },
  fundBalanceLabel: {
    fontSize: 12,
  },
  fundBalanceValue: {
    fontSize: 18,
    fontWeight: '800',
    marginTop: 2,
  },
  heldBadge: {
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: radius.sm,
    backgroundColor: 'rgba(217, 130, 24, 0.12)',
  },
  heldLabel: {
    fontSize: 11,
    fontWeight: '700',
  },
  progressContainer: {
    marginTop: 10,
  },
  progressLabels: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 4,
  },
  progressText: {
    fontSize: 11,
    fontWeight: '600',
  },
  progressBarTrack: {
    height: 6,
    borderRadius: 3,
    overflow: 'hidden',
  },
  progressBarFill: {
    height: '100%',
    borderRadius: 3,
  },
  cardFooter: {
    borderTopWidth: 1,
    marginTop: 12,
    paddingTop: 10,
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  memberInfo: {
    fontSize: 11,
    flex: 1,
  },
  cardActionsRow: {
    flexDirection: 'row',
    gap: 8,
  },
  miniBtn: {
    paddingHorizontal: 10,
    paddingVertical: 6,
    borderRadius: radius.md,
  },
  miniBtnText: {
    color: '#FFFFFF',
    fontSize: 12,
    fontWeight: '700',
  },
  miniBtnOutline: {
    paddingHorizontal: 10,
    paddingVertical: 6,
    borderRadius: radius.md,
    borderWidth: 1,
  },
  miniBtnOutlineText: {
    fontSize: 12,
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
    maxHeight: '88%',
    paddingBottom: 24,
  },
  modalHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: spacing.lg,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(0,0,0,0.06)',
  },
  modalTitle: {
    fontSize: 18,
    fontWeight: '800',
  },
  modalSub: {
    fontSize: 12,
    marginTop: 2,
  },
  modalCloseBtn: {
    padding: 6,
  },
  detailTabRow: {
    flexDirection: 'row',
    borderBottomWidth: 1,
    paddingHorizontal: spacing.md,
  },
  detailTabItem: {
    paddingVertical: 12,
    marginRight: 16,
    borderBottomWidth: 2,
    borderBottomColor: 'transparent',
  },
  detailTabText: {
    fontSize: 13,
    fontWeight: '700',
  },
  modalBody: {
    padding: spacing.md,
  },
  tabContent: {
    paddingVertical: spacing.sm,
    gap: spacing.md,
  },
  heroDetailBox: {
    padding: spacing.md,
    borderRadius: radius.lg,
    alignItems: 'center',
  },
  heroDetailLabel: {
    fontSize: 13,
  },
  heroDetailAmount: {
    fontSize: 26,
    fontWeight: '800',
    marginVertical: 4,
  },
  heroDetailSub: {
    fontSize: 12,
  },
  detailGrid: {
    flexDirection: 'row',
    gap: 10,
  },
  detailCell: {
    flex: 1,
    padding: 12,
    borderRadius: radius.md,
    borderWidth: 1,
  },
  cellLabel: {
    fontSize: 11,
  },
  cellVal: {
    fontSize: 14,
    fontWeight: '700',
    marginTop: 4,
  },
  detailCtaRow: {
    flexDirection: 'row',
    gap: 12,
    marginTop: spacing.sm,
  },
  ctaPrimary: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 6,
    paddingVertical: 14,
    borderRadius: radius.lg,
  },
  ctaPrimaryText: {
    color: '#FFFFFF',
    fontWeight: '700',
    fontSize: 14,
  },
  ctaSecondary: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 6,
    paddingVertical: 14,
    borderRadius: radius.lg,
    borderWidth: 1.5,
  },
  ctaSecondaryText: {
    fontWeight: '700',
    fontSize: 14,
  },
  closeActivityBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 6,
    paddingVertical: 12,
    borderRadius: radius.md,
    borderWidth: 1.5,
    marginTop: 8,
  },
  closeActivityBtnText: {
    fontSize: 13,
    fontWeight: '700',
  },
  emptyRequests: {
    alignItems: 'center',
    paddingVertical: 24,
    gap: 8,
  },
  emptyRequestsTitle: {
    fontSize: 15,
    fontWeight: '700',
  },
  emptyRequestsSub: {
    fontSize: 13,
    textAlign: 'center',
    paddingHorizontal: 16,
  },
  requestCard: {
    padding: 14,
    borderRadius: radius.md,
    borderWidth: 1,
    gap: 4,
  },
  requestHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  reqBadgeRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  reqBadge: {
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: radius.sm,
  },
  reqBadgeText: {
    fontSize: 11,
    fontWeight: '700',
  },
  reqStateText: {
    fontSize: 11,
  },
  requestTitle: {
    fontSize: 14,
    fontWeight: '700',
    marginTop: 4,
  },
  requestAmount: {
    fontSize: 15,
    fontWeight: '800',
  },
  requestMeta: {
    fontSize: 12,
  },
  requestActionRow: {
    flexDirection: 'row',
    gap: 10,
    marginTop: 10,
  },
  miniApproveBtn: {
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: radius.sm,
  },
  miniRejectBtn: {
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: radius.sm,
    borderWidth: 1,
  },
  miniRejectText: {
    fontSize: 12,
    fontWeight: '700',
  },
  memberRow: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 8,
  },
  memberName: {
    fontSize: 14,
    fontWeight: '700',
  },
  memberSub: {
    fontSize: 12,
    marginTop: 2,
  },
  memberBadge: {
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: radius.sm,
  },
  memberBadgeText: {
    fontSize: 11,
    fontWeight: '700',
  },
  govTitle: {
    fontSize: 15,
    fontWeight: '700',
  },
  govCopy: {
    fontSize: 13,
    lineHeight: 20,
  },
  financeAuditBox: {
    padding: 10,
    borderRadius: radius.md,
    marginTop: 10,
    borderWidth: 1,
  },
  leaveBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 6,
    paddingVertical: 12,
    borderRadius: radius.md,
    borderWidth: 1,
    marginTop: 12,
  },
  leaveBtnText: {
    fontSize: 13,
    fontWeight: '700',
  },
  inputLabel: {
    fontSize: 13,
    fontWeight: '600',
    marginBottom: 6,
  },
  formInput: {
    height: 48,
    borderRadius: radius.md,
    borderWidth: 1,
    paddingHorizontal: 14,
    fontSize: 15,
  },
  typeOptionRow: {
    flexDirection: 'row',
    gap: 10,
  },
  typeOptionCard: {
    flex: 1,
    padding: 12,
    borderRadius: radius.md,
    borderWidth: 1.5,
    gap: 4,
  },
  typeOptionTitle: {
    fontSize: 14,
    fontWeight: '700',
  },
  typeOptionDesc: {
    fontSize: 11,
    lineHeight: 15,
  },
  trustBox: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    gap: 8,
    padding: 10,
    borderRadius: radius.md,
    borderWidth: 1,
    marginTop: 4,
  },
  trustText: {
    flex: 1,
    fontSize: 12,
    lineHeight: 18,
  },
  createSubmitBtn: {
    marginTop: 18,
    paddingVertical: 14,
    borderRadius: radius.lg,
    alignItems: 'center',
  },
  createSubmitText: {
    color: '#FFFFFF',
    fontSize: 15,
    fontWeight: '700',
  },
  kindSelectorRow: {
    flexDirection: 'row',
    gap: 8,
    marginBottom: 12,
  },
  kindSelectBtn: {
    flex: 1,
    paddingVertical: 10,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: 'transparent',
    alignItems: 'center',
  },
  kindSelectBtnActive: {
    borderColor: 'rgba(0,0,0,0.1)',
  },
  kindSelectText: {
    fontSize: 12,
    fontWeight: '700',
  },
});
