import React, { useState } from 'react';
import {
  StyleSheet,
  Text,
  View,
  ScrollView,
  Pressable,
  TextInput,
  Alert,
  Modal,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useAuth } from '@/auth/AuthProvider';
import { AppScreen } from '@/components/AppScreen';
import { GlassCard } from '@/components/GlassCard';
import { useTheme } from '@/theme/useTheme';
import { radius, spacing } from '@/theme/tokens';

export interface GroupFundItem {
  id: string;
  name: string;
  fundType: 'fixed' | 'free';
  balance: number;
  spendableBalance: number;
  heldAmount: number;
  targetAmount?: number;
  membersCount: number;
  role: 'owner' | 'member';
  canSpend: boolean;
  status: string;
  cycleText?: string;
  myContribution: number;
  requestsCount: number;
}

export default function GroupsScreen() {
  const { user } = useAuth();
  const { colors, isDark } = useTheme();

  // Filters
  const [filterType, setFilterType] = useState<'all' | 'fixed' | 'free'>('all');
  const [selectedGroup, setSelectedGroup] = useState<GroupFundItem | null>(null);
  const [detailTab, setDetailTab] = useState<'overview' | 'requests' | 'members' | 'governance'>('overview');

  // Modals
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [newFundName, setNewFundName] = useState('');
  const [newFundType, setNewFundType] = useState<'fixed' | 'free'>('fixed');
  const [newFundTarget, setNewFundTarget] = useState('');

  // Sample realistic data honoring Backend Fixed Fund V2 and Free Fund V1 contracts
  const [groups, setGroups] = useState<GroupFundItem[]>([
    {
      id: 'grp-fixed-1',
      name: 'Quỹ Du lịch Đà Nẵng 2026',
      fundType: 'fixed',
      balance: 18500000,
      spendableBalance: 15500000,
      heldAmount: 3000000,
      targetAmount: 25000000,
      membersCount: 5,
      role: 'owner',
      canSpend: true,
      status: 'Đang hoạt động (Kỳ 2/3)',
      cycleText: 'Chu kỳ hàng tháng • 1.500.000 ₫/kỳ',
      myContribution: 3000000,
      requestsCount: 1,
    },
    {
      id: 'grp-free-1',
      name: 'Quỹ Ăn trưa Công ty',
      fundType: 'free',
      balance: 4200000,
      spendableBalance: 4200000,
      heldAmount: 0,
      membersCount: 8,
      role: 'member',
      canSpend: false,
      status: 'Đóng góp tự do',
      cycleText: 'Tích lũy linh hoạt',
      myContribution: 600000,
      requestsCount: 0,
    },
    {
      id: 'grp-fixed-2',
      name: 'Quỹ Tiết kiệm Nhóm Bạn Thân',
      fundType: 'fixed',
      balance: 45000000,
      spendableBalance: 45000000,
      heldAmount: 0,
      targetAmount: 50000000,
      membersCount: 4,
      role: 'member',
      canSpend: true,
      status: 'Đang hoạt động (Kỳ 5/5)',
      cycleText: 'Chu kỳ hàng tháng • 2.500.000 ₫/kỳ',
      myContribution: 12500000,
      requestsCount: 0,
    },
  ]);

  const filteredGroups = groups.filter((g) => {
    if (filterType === 'fixed') return g.fundType === 'fixed';
    if (filterType === 'free') return g.fundType === 'free';
    return true;
  });

  const totalGroupCapital = groups.reduce((acc, cur) => acc + cur.myContribution, 0);

  const handleCreateFund = () => {
    if (!newFundName.trim()) {
      Alert.alert('Lỗi', 'Vui lòng nhập tên quỹ');
      return;
    }
    const target = parseInt(newFundTarget.replace(/\D/g, ''), 10) || 0;
    const newGroup: GroupFundItem = {
      id: `grp-${Date.now()}`,
      name: newFundName.trim(),
      fundType: newFundType,
      balance: 0,
      spendableBalance: 0,
      heldAmount: 0,
      targetAmount: newFundType === 'fixed' ? target || 10000000 : undefined,
      membersCount: 1,
      role: 'owner',
      canSpend: true,
      status: newFundType === 'fixed' ? 'Mới khởi tạo (Kỳ 1)' : 'Đóng góp tự do',
      cycleText: newFundType === 'fixed' ? 'Chu kỳ hàng tháng' : 'Tích lũy linh hoạt',
      myContribution: 0,
      requestsCount: 0,
    };
    setGroups([newGroup, ...groups]);
    setIsCreateModalOpen(false);
    setNewFundName('');
    setNewFundTarget('');
    Alert.alert('Thành công', `Đã tạo quỹ ${newFundType === 'fixed' ? 'Cố định V2' : 'Tự do V1'}: "${newGroup.name}"`);
  };

  const handleContribute = (group: GroupFundItem) => {
    if (Alert.prompt) {
      Alert.prompt(
        'Đóng góp quỹ',
        `Nhập số tiền muốn đóng vào ${group.name}:`,
        (text) => {
          const amount = parseInt(text.replace(/\D/g, ''), 10);
          if (amount > 0) {
            setGroups((prev) =>
              prev.map((g) =>
                g.id === group.id
                  ? {
                      ...g,
                      balance: g.balance + amount,
                      spendableBalance: g.spendableBalance + amount,
                      myContribution: g.myContribution + amount,
                    }
                  : g
              )
            );
            Alert.alert('Thành công', `Đã đóng góp ${amount.toLocaleString('vi-VN')} ₫ vào quỹ.`);
          }
        },
        'plain-text',
        '500000'
      );
    } else {
      Alert.alert('Đóng góp quỹ', `Đã xác nhận đóng góp 500.000 ₫ vào quỹ ${group.name}.`);
    }
  };

  const handleRequestSpend = (group: GroupFundItem) => {
    Alert.alert(
      'Yêu cầu chi tiêu',
      `Tạo đề xuất chi từ quỹ ${group.name} (Áp dụng quy tắc hạn mức 20% và duyệt 24h đối với Quỹ cố định)`,
      [
        { text: 'Xác nhận tạo yêu cầu', onPress: () => Alert.alert('Đã gửi', 'Yêu cầu chi đã được gửi tới thành viên!') },
        { text: 'Hủy', style: 'cancel' },
      ]
    );
  };

  return (
    <AppScreen title="Quỹ nhóm" scrollable>
      <View style={styles.container}>
        {/* 1. PORTFOLIO SUMMARY CARD */}
        <GlassCard style={styles.summaryCard}>
          <View style={styles.summaryTop}>
            <View>
              <Text style={[styles.summaryLabel, { color: colors.textMuted }]}>
                Tổng vốn góp của bạn tại các quỹ
              </Text>
              <Text style={[styles.summaryAmount, { color: colors.text }]}>
                {totalGroupCapital.toLocaleString('vi-VN')} ₫
              </Text>
            </View>
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

          <View style={[styles.summaryFooter, { borderTopColor: colors.border }]}>
            <View style={styles.summaryStat}>
              <Text style={[styles.statValue, { color: colors.primary }]}>{groups.length}</Text>
              <Text style={[styles.statLabel, { color: colors.textMuted }]}>Quỹ tham gia</Text>
            </View>
            <View style={[styles.statDivider, { backgroundColor: colors.border }]} />
            <View style={styles.summaryStat}>
              <Text style={[styles.statValue, { color: colors.accent }]}>
                {groups.filter((g) => g.fundType === 'fixed').length}
              </Text>
              <Text style={[styles.statLabel, { color: colors.textMuted }]}>Cố định V2</Text>
            </View>
            <View style={[styles.statDivider, { backgroundColor: colors.border }]} />
            <View style={styles.summaryStat}>
              <Text style={[styles.statValue, { color: colors.violet }]}>
                {groups.filter((g) => g.fundType === 'free').length}
              </Text>
              <Text style={[styles.statLabel, { color: colors.textMuted }]}>Tự do V1</Text>
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
              Tất cả ({groups.length})
            </Text>
          </Pressable>
          <Pressable
            onPress={() => setFilterType('fixed')}
            style={[styles.segmentBtn, filterType === 'fixed' && [styles.segmentBtnActive, { backgroundColor: colors.surface }]]}
          >
            <Text style={[styles.segmentText, { color: filterType === 'fixed' ? colors.primary : colors.textMuted }]}>
              Cố định V2
            </Text>
          </Pressable>
          <Pressable
            onPress={() => setFilterType('free')}
            style={[styles.segmentBtn, filterType === 'free' && [styles.segmentBtnActive, { backgroundColor: colors.surface }]]}
          >
            <Text style={[styles.segmentText, { color: filterType === 'free' ? colors.primary : colors.textMuted }]}>
              Tự do V1
            </Text>
          </Pressable>
        </View>

        {/* 3. GROUP FUND LIST */}
        <View style={styles.fundList}>
          {filteredGroups.map((group) => {
            const isFixed = group.fundType === 'fixed';
            const progress = group.targetAmount ? Math.min(100, Math.round((group.balance / group.targetAmount) * 100)) : 0;

            return (
              <Pressable
                key={group.id}
                onPress={() => setSelectedGroup(group)}
                style={({ pressed }) => [{ opacity: pressed ? 0.85 : 1 }]}
              >
                <GlassCard style={styles.fundCard}>
                  {/* Card Header: Type badge & Role badge */}
                  <View style={styles.cardHeaderRow}>
                    <View
                      style={[
                        styles.fundTypeBadge,
                        {
                          backgroundColor: isFixed ? (isDark ? '#0F3D2E' : '#B8F0D8') : (isDark ? '#2E1A47' : '#EDE9FE'),
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
                      <View style={[styles.roleBadge, { backgroundColor: group.role === 'owner' ? colors.primarySoft : colors.muted }]}>
                        <Text style={[styles.roleBadgeText, { color: group.role === 'owner' ? colors.primary : colors.textMuted }]}>
                          {group.role === 'owner' ? 'Trưởng quỹ' : 'Thành viên'}
                        </Text>
                      </View>
                      <Ionicons name="chevron-forward" size={16} color={colors.textMuted} />
                    </View>
                  </View>

                  {/* Fund Name */}
                  <Text style={[styles.fundName, { color: colors.text }]}>{group.name}</Text>
                  <Text style={[styles.fundCycle, { color: colors.textMuted }]}>{group.cycleText}</Text>

                  {/* Balances */}
                  <View style={styles.fundBalanceRow}>
                    <View>
                      <Text style={[styles.fundBalanceLabel, { color: colors.textMuted }]}>Số dư khả dụng</Text>
                      <Text style={[styles.fundBalanceValue, { color: colors.text }]}>
                        {group.spendableBalance.toLocaleString('vi-VN')} ₫
                      </Text>
                    </View>
                    {group.heldAmount > 0 && (
                      <View style={styles.heldBadge}>
                        <Text style={[styles.heldLabel, { color: colors.amber }]}>
                          Tạm giữ: {group.heldAmount.toLocaleString('vi-VN')} ₫
                        </Text>
                      </View>
                    )}
                  </View>

                  {/* Progress Bar for Fixed Fund */}
                  {isFixed && group.targetAmount && (
                    <View style={styles.progressContainer}>
                      <View style={styles.progressLabels}>
                        <Text style={[styles.progressText, { color: colors.textMuted }]}>
                          Tiến độ: {progress}%
                        </Text>
                        <Text style={[styles.progressText, { color: colors.textMuted }]}>
                          Mục tiêu: {group.targetAmount.toLocaleString('vi-VN')} ₫
                        </Text>
                      </View>
                      <View style={[styles.progressBarTrack, { backgroundColor: isDark ? '#1C382F' : '#D0EADB' }]}>
                        <View style={[styles.progressBarFill, { width: `${progress}%`, backgroundColor: colors.primary }]} />
                      </View>
                    </View>
                  )}

                  {/* Card Actions Footer */}
                  <View style={[styles.cardFooter, { borderTopColor: colors.border }]}>
                    <Text style={[styles.memberInfo, { color: colors.textMuted }]}>
                      {group.membersCount} thành viên • Vốn của bạn: {group.myContribution.toLocaleString('vi-VN')} ₫
                    </Text>
                    <View style={styles.cardActionsRow}>
                      <Pressable
                        onPress={() => handleContribute(group)}
                        style={[styles.miniBtn, { backgroundColor: colors.primary }]}
                      >
                        <Text style={styles.miniBtnText}>Đóng góp</Text>
                      </Pressable>
                      {group.canSpend && (
                        <Pressable
                          onPress={() => handleRequestSpend(group)}
                          style={[styles.miniBtnOutline, { borderColor: colors.primary }]}
                        >
                          <Text style={[styles.miniBtnOutlineText, { color: colors.primary }]}>Yêu cầu chi</Text>
                        </Pressable>
                      )}
                    </View>
                  </View>
                </GlassCard>
              </Pressable>
            );
          })}
        </View>

        {/* 4. DETAIL MODAL */}
        {selectedGroup && (
          <Modal
            visible={Boolean(selectedGroup)}
            animationType="slide"
            transparent
            onRequestClose={() => setSelectedGroup(null)}
          >
            <View style={styles.modalOverlay}>
              <View style={[styles.modalSheet, { backgroundColor: colors.surface }]}>
                {/* Modal Header */}
                <View style={styles.modalHeader}>
                  <View style={{ flex: 1 }}>
                    <Text style={[styles.modalTitle, { color: colors.text }]}>{selectedGroup.name}</Text>
                    <Text style={[styles.modalSub, { color: colors.textMuted }]}>
                      {selectedGroup.fundType === 'fixed' ? 'Quỹ Cố định V2 • Quản trị cam kết' : 'Quỹ Tự do V1 • Biến động tự nguyện'}
                    </Text>
                  </View>
                  <Pressable onPress={() => setSelectedGroup(null)} style={styles.modalCloseBtn}>
                    <Ionicons name="close" size={24} color={colors.text} />
                  </Pressable>
                </View>

                {/* Modal Detail Tabs */}
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
                      Yêu cầu ({selectedGroup.requestsCount})
                    </Text>
                  </Pressable>
                  <Pressable
                    onPress={() => setDetailTab('members')}
                    style={[styles.detailTabItem, detailTab === 'members' && { borderBottomColor: colors.primary }]}
                  >
                    <Text style={[styles.detailTabText, { color: detailTab === 'members' ? colors.primary : colors.textMuted }]}>
                      Thành viên ({selectedGroup.membersCount})
                    </Text>
                  </Pressable>
                  <Pressable
                    onPress={() => setDetailTab('governance')}
                    style={[styles.detailTabItem, detailTab === 'governance' && { borderBottomColor: colors.primary }]}
                  >
                    <Text style={[styles.detailTabText, { color: detailTab === 'governance' ? colors.primary : colors.textMuted }]}>
                      Quản trị
                    </Text>
                  </Pressable>
                </View>

                <ScrollView style={styles.modalBody}>
                  {detailTab === 'overview' && (
                    <View style={styles.tabContent}>
                      <View style={[styles.heroDetailBox, { backgroundColor: isDark ? colors.surfaceElevated : '#E6F8F0' }]}>
                        <Text style={[styles.heroDetailLabel, { color: colors.textMuted }]}>Số dư khả dụng</Text>
                        <Text style={[styles.heroDetailAmount, { color: colors.primary }]}>
                          {selectedGroup.spendableBalance.toLocaleString('vi-VN')} ₫
                        </Text>
                        <Text style={[styles.heroDetailSub, { color: colors.textSecondary }]}>
                          Tổng quỹ: {selectedGroup.balance.toLocaleString('vi-VN')} ₫ • Đã giữ: {selectedGroup.heldAmount.toLocaleString('vi-VN')} ₫
                        </Text>
                      </View>

                      <View style={styles.detailGrid}>
                        <View style={[styles.detailCell, { borderColor: colors.border }]}>
                          <Text style={[styles.cellLabel, { color: colors.textMuted }]}>Vốn của bạn</Text>
                          <Text style={[styles.cellVal, { color: colors.text }]}>
                            {selectedGroup.myContribution.toLocaleString('vi-VN')} ₫
                          </Text>
                        </View>
                        <View style={[styles.detailCell, { borderColor: colors.border }]}>
                          <Text style={[styles.cellLabel, { color: colors.textMuted }]}>Quyền chi</Text>
                          <Text style={[styles.cellVal, { color: selectedGroup.canSpend ? colors.primary : colors.textMuted }]}>
                            {selectedGroup.canSpend ? 'Được phép chi' : 'Chỉ xem'}
                          </Text>
                        </View>
                      </View>

                      {/* Detail CTA Row */}
                      <View style={styles.detailCtaRow}>
                        <Pressable
                          onPress={() => handleContribute(selectedGroup)}
                          style={[styles.ctaPrimary, { backgroundColor: colors.primary }]}
                        >
                          <Ionicons name="arrow-down" size={18} color="#FFFFFF" />
                          <Text style={styles.ctaPrimaryText}>Đóng góp tiền</Text>
                        </Pressable>
                        {selectedGroup.canSpend && (
                          <Pressable
                            onPress={() => handleRequestSpend(selectedGroup)}
                            style={[styles.ctaSecondary, { borderColor: colors.primary }]}
                          >
                            <Ionicons name="card-outline" size={18} color={colors.primary} />
                            <Text style={[styles.ctaSecondaryText, { color: colors.primary }]}>Yêu cầu chi</Text>
                          </Pressable>
                        )}
                      </View>
                    </View>
                  )}

                  {detailTab === 'requests' && (
                    <View style={styles.tabContent}>
                      {selectedGroup.requestsCount === 0 ? (
                        <View style={styles.emptyRequests}>
                          <Ionicons name="checkmark-done-circle-outline" size={48} color={colors.primary} />
                          <Text style={[styles.emptyRequestsTitle, { color: colors.text }]}>Không có yêu cầu chờ duyệt</Text>
                          <Text style={[styles.emptyRequestsSub, { color: colors.textMuted }]}>
                            Các khoản chi vượt 20% hoặc có phản đối sẽ hiển thị tại đây.
                          </Text>
                        </View>
                      ) : (
                        <View style={[styles.requestCard, { borderColor: colors.border }]}>
                          <View style={styles.requestHeader}>
                            <Text style={[styles.requestTitle, { color: colors.text }]}>Chi phí đặt phòng Đà Nẵng</Text>
                            <Text style={[styles.requestAmount, { color: colors.danger }]}>- 3.000.000 ₫</Text>
                          </View>
                          <Text style={[styles.requestMeta, { color: colors.textMuted }]}>
                            Người đề xuất: Bạn • Trạng thái: Đang trong thời gian chờ 24h
                          </Text>
                          <View style={styles.requestActionRow}>
                            <Pressable style={[styles.miniApproveBtn, { backgroundColor: colors.primary }]}>
                              <Text style={styles.miniBtnText}>Duyệt ngay</Text>
                            </Pressable>
                            <Pressable style={[styles.miniRejectBtn, { borderColor: colors.danger }]}>
                              <Text style={[styles.miniRejectText, { color: colors.danger }]}>Phản đối</Text>
                            </Pressable>
                          </View>
                        </View>
                      )}
                    </View>
                  )}

                  {detailTab === 'members' && (
                    <View style={styles.tabContent}>
                      <View style={styles.memberRow}>
                        <Ionicons name="person-circle" size={36} color={colors.primary} />
                        <View style={{ flex: 1, marginLeft: 10 }}>
                          <Text style={[styles.memberName, { color: colors.text }]}>
                            {user?.email || 'Bạn'} (Bạn)
                          </Text>
                          <Text style={[styles.memberSub, { color: colors.textMuted }]}>
                            {selectedGroup.role === 'owner' ? 'Trưởng quỹ' : 'Thành viên'} • Đã đóng {selectedGroup.myContribution.toLocaleString('vi-VN')} ₫
                          </Text>
                        </View>
                        <View style={[styles.memberBadge, { backgroundColor: colors.primarySoft }]}>
                          <Text style={[styles.memberBadgeText, { color: colors.primary }]}>
                            {selectedGroup.canSpend ? 'CanSpend' : 'View'}
                          </Text>
                        </View>
                      </View>
                    </View>
                  )}

                  {detailTab === 'governance' && (
                    <View style={styles.tabContent}>
                      <Text style={[styles.govTitle, { color: colors.text }]}>Quy tắc quản trị quỹ</Text>
                      <Text style={[styles.govCopy, { color: colors.textSecondary }]}>
                        {selectedGroup.fundType === 'fixed'
                          ? '• Quỹ cố định V2 áp dụng ngưỡng cam kết tin cậy và hạn mức chi 20% số dư.\n• Khoản chi vượt hạn mức kích hoạt cơ chế đếm ngược 24h và quyền phản đối.\n• Miễn nhiệm trưởng quỹ tự động nếu không hoạt động.'
                          : '• Quỹ tự do V1 cho phép nạp và rút tự nguyện dựa trên vốn thực có.\n• Đơn rút tiền phê duyệt theo quy tắc hạn ngạch 7 ngày (ApprovedUnused).'}
                      </Text>
                      <Pressable
                        onPress={() => Alert.alert('Rời quỹ', 'Bạn có chắc chắn muốn yêu cầu rời quỹ và tất toán vốn góp?')}
                        style={[styles.leaveBtn, { borderColor: colors.danger }]}
                      >
                        <Ionicons name="exit-outline" size={18} color={colors.danger} />
                        <Text style={[styles.leaveBtnText, { color: colors.danger }]}>Yêu cầu rút vốn & Rời quỹ</Text>
                      </Pressable>
                    </View>
                  )}
                </ScrollView>
              </View>
            </View>
          </Modal>
        )}

        {/* 5. CREATE FUND MODAL */}
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
                {/* Fund Name Input */}
                <Text style={[styles.inputLabel, { color: colors.text }]}>Tên Quỹ</Text>
                <TextInput
                  value={newFundName}
                  onChangeText={setNewFundName}
                  placeholder="Ví dụ: Quỹ Ăn trưa Team / Quỹ Du lịch"
                  placeholderTextColor={colors.placeholder}
                  style={[styles.formInput, { backgroundColor: colors.surfaceElevated, borderColor: colors.border, color: colors.text }]}
                />

                {/* Fund Type Picker */}
                <Text style={[styles.inputLabel, { color: colors.text, marginTop: 14 }]}>Loại Quỹ</Text>
                <View style={styles.typeOptionRow}>
                  <Pressable
                    onPress={() => setNewFundType('fixed')}
                    style={[
                      styles.typeOptionCard,
                      { borderColor: newFundType === 'fixed' ? colors.primary : colors.border },
                      newFundType === 'fixed' && { backgroundColor: isDark ? '#0F3D2E' : '#E6F8F0' },
                    ]}
                  >
                    <Ionicons name="shield-checkmark" size={24} color={colors.primary} />
                    <Text style={[styles.typeOptionTitle, { color: colors.text }]}>Cố định V2</Text>
                    <Text style={[styles.typeOptionDesc, { color: colors.textMuted }]}>
                      Chu kỳ bắt buộc, cam kết tin cậy, chi 24h & hạn mức 20%
                    </Text>
                  </Pressable>

                  <Pressable
                    onPress={() => setNewFundType('free')}
                    style={[
                      styles.typeOptionCard,
                      { borderColor: newFundType === 'free' ? colors.violet : colors.border },
                      newFundType === 'free' && { backgroundColor: isDark ? '#2E1A47' : '#F5F3FF' },
                    ]}
                  >
                    <Ionicons name="sparkles" size={24} color={colors.violet} />
                    <Text style={[styles.typeOptionTitle, { color: colors.text }]}>Tự do V1</Text>
                    <Text style={[styles.typeOptionDesc, { color: colors.textMuted }]}>
                      Đóng góp tự nguyện, rút vốn linh hoạt, duyệt bởi Trưởng quỹ
                    </Text>
                  </Pressable>
                </View>

                {/* Target Amount for Fixed */}
                {newFundType === 'fixed' && (
                  <View style={{ marginTop: 14 }}>
                    <Text style={[styles.inputLabel, { color: colors.text }]}>Mục tiêu quỹ (₫)</Text>
                    <TextInput
                      value={newFundTarget}
                      onChangeText={setNewFundTarget}
                      keyboardType="numeric"
                      placeholder="Ví dụ: 20000000"
                      placeholderTextColor={colors.placeholder}
                      style={[styles.formInput, { backgroundColor: colors.surfaceElevated, borderColor: colors.border, color: colors.text }]}
                    />
                  </View>
                )}

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
      </View>
    </AppScreen>
  );
}

const styles = StyleSheet.create({
  container: {
    padding: spacing.md,
    gap: spacing.md,
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
    maxHeight: '85%',
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
  },
  requestCard: {
    padding: 14,
    borderRadius: radius.md,
    borderWidth: 1,
  },
  requestHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  requestTitle: {
    fontSize: 14,
    fontWeight: '700',
  },
  requestAmount: {
    fontSize: 15,
    fontWeight: '800',
  },
  requestMeta: {
    fontSize: 12,
    marginTop: 6,
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
  createSubmitBtn: {
    marginTop: 20,
    paddingVertical: 14,
    borderRadius: radius.lg,
    alignItems: 'center',
  },
  createSubmitText: {
    color: '#FFFFFF',
    fontSize: 15,
    fontWeight: '700',
  },
});
