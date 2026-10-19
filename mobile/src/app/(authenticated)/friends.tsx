import { useEffect, useState } from 'react';
import { Alert, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { AppScreen } from '@/components/AppScreen';
import { Card, Copy, EmptyState, SectionTitle, Segments, Skeleton, UIIcon, UnwiredNote } from '@/components/MobileUI';
import { friendsApi, type FriendContactDto, type FriendDto, type FriendRequestDto } from '@/api/friendsApi';
import { fontStyles, radius, spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

export default function FriendsScreen() {
  const { colors } = useTheme();
  const [activeTab, setActiveTab] = useState<'Bạn bè' | 'Lời mời' | 'Đã gửi'>('Bạn bè');

  // Search input & state
  const [searchContact, setSearchContact] = useState('');
  const [searchLoading, setSearchLoading] = useState(false);
  const [searchResult, setSearchResult] = useState<FriendContactDto | null>(null);
  const [searchError, setSearchError] = useState<string | null>(null);

  // Lists state
  const [friends, setFriends] = useState<FriendDto[]>([]);
  const [incoming, setIncoming] = useState<FriendRequestDto[]>([]);
  const [outgoing, setOutgoing] = useState<FriendRequestDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [actionInProgress, setActionInProgress] = useState<string | null>(null);

  const loadData = async () => {
    setLoading(true);
    try {
      const [f, inc, out] = await Promise.all([
        friendsApi.getFriends().catch(() => []),
        friendsApi.getIncomingRequests().catch(() => []),
        friendsApi.getOutgoingRequests().catch(() => []),
      ]);
      setFriends(f);
      setIncoming(inc);
      setOutgoing(out);
    } catch {
      // ignore
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const handleSearch = async () => {
    if (!searchContact.trim()) return;
    setSearchLoading(true);
    setSearchError(null);
    setSearchResult(null);
    try {
      const res = await friendsApi.searchFriendContact(searchContact.trim());
      setSearchResult(res);
    } catch (err: any) {
      setSearchError(err?.message || 'Không tìm thấy người dùng với thông tin này.');
    } finally {
      setSearchLoading(false);
    }
  };

  const handleSendRequest = async () => {
    if (!searchResult) return;
    setActionInProgress(searchResult.userId);
    try {
      await friendsApi.sendFriendRequest({ contact: searchResult.phone || searchResult.spendId });
      Alert.alert('Thành công', `Đã gửi lời mời kết bạn đến ${searchResult.fullName}.`);
      setSearchResult(null);
      setSearchContact('');
      loadData();
    } catch (err: any) {
      Alert.alert('Lỗi', err?.message || 'Không thể gửi lời mời.');
    } finally {
      setActionInProgress(null);
    }
  };

  const handleAcceptRequest = async (reqId: string) => {
    setActionInProgress(reqId);
    try {
      await friendsApi.acceptFriendRequest(reqId);
      loadData();
    } catch (err: any) {
      Alert.alert('Lỗi', err?.message || 'Không thể chấp nhận lời mời.');
    } finally {
      setActionInProgress(null);
    }
  };

  const handleRejectRequest = async (reqId: string) => {
    setActionInProgress(reqId);
    try {
      await friendsApi.rejectFriendRequest(reqId);
      loadData();
    } catch (err: any) {
      Alert.alert('Lỗi', err?.message || 'Không thể từ chối lời mời.');
    } finally {
      setActionInProgress(null);
    }
  };

  const handleCancelRequest = async (reqId: string) => {
    setActionInProgress(reqId);
    try {
      await friendsApi.cancelFriendRequest(reqId);
      loadData();
    } catch (err: any) {
      Alert.alert('Lỗi', err?.message || 'Không thể thu hồi lời mời.');
    } finally {
      setActionInProgress(null);
    }
  };

  const handleUnfriend = async (friendId: string, name: string) => {
    Alert.alert('Hủy kết bạn', `Bạn có chắc muốn hủy kết bạn với ${name}?`, [
      { text: 'Hủy', style: 'cancel' },
      {
        text: 'Đồng ý',
        style: 'destructive',
        onPress: async () => {
          try {
            await friendsApi.unfriend(friendId);
            loadData();
          } catch (err: any) {
            Alert.alert('Lỗi', err?.message || 'Không thể hủy kết bạn.');
          }
        },
      },
    ]);
  };

  return (
    <AppScreen title="Bạn bè" subtitle="Kết nối bạn bè, chia sẻ chi tiêu và quỹ chung.">
      {/* 1. Full-Featured Search Bar for User Discovery */}
      <Card>
        <SectionTitle title="Tìm kiếm & Kết bạn mới" />
        <View style={styles.searchRow}>
          <View style={[styles.searchInputContainer, { borderColor: colors.border, backgroundColor: colors.input }]}>
            <UIIcon name="search" color={colors.textMuted} size={18} />
            <TextInput
              value={searchContact}
              onChangeText={setSearchContact}
              placeholder="Nhập SĐT hoặc SpendId (VD: SW-12345)"
              placeholderTextColor={colors.textMuted}
              returnKeyType="search"
              onSubmitEditing={handleSearch}
              style={[styles.searchInput, { color: colors.text }]}
            />
          </View>
          <Pressable
            accessibilityRole="button"
            onPress={handleSearch}
            disabled={searchLoading || !searchContact.trim()}
            style={[
              styles.searchBtn,
              { backgroundColor: colors.primary, opacity: searchLoading || !searchContact.trim() ? 0.6 : 1 },
            ]}
          >
            <Text style={[styles.searchBtnText, { color: colors.onButton }]}>
              {searchLoading ? 'Đang tìm...' : 'Tìm'}
            </Text>
          </Pressable>
        </View>

        {/* Search Results Card */}
        {searchResult && (
          <View style={[styles.searchResultCard, { borderColor: colors.border, backgroundColor: colors.surfaceElevated }]}>
            <View style={styles.userRow}>
              <View style={[styles.avatar, { backgroundColor: colors.primarySoft }]}>
                <UIIcon name="profile" size={24} />
              </View>
              <View style={styles.userInfo}>
                <Text style={[styles.userName, { color: colors.text }]}>{searchResult.fullName}</Text>
                <Text style={[styles.userSpendId, { color: colors.textMuted }]}>SpendId: {searchResult.spendId}</Text>
              </View>
            </View>

            {searchResult.state === 'SELF' ? (
              <Copy muted small>Đây là tài khoản của chính bạn.</Copy>
            ) : searchResult.state === 'FRIENDS' ? (
              <Text style={[styles.statusBadge, { color: colors.primary }]}>✓ Đã là bạn bè</Text>
            ) : searchResult.state === 'REQUEST_SENT' ? (
              <Text style={[styles.statusBadge, { color: colors.textMuted }]}>Đã gửi lời mời kết bạn</Text>
            ) : searchResult.state === 'REQUEST_RECEIVED' ? (
              <Text style={[styles.statusBadge, { color: colors.accent }]}>Người này đã gửi lời mời cho bạn</Text>
            ) : (
              <Pressable
                accessibilityRole="button"
                onPress={handleSendRequest}
                disabled={actionInProgress === searchResult.userId}
                style={[styles.actionBtn, { backgroundColor: colors.primary }]}
              >
                <Text style={[styles.actionBtnText, { color: colors.onButton }]}>
                  {actionInProgress === searchResult.userId ? 'Đang gửi...' : '+ Kết bạn'}
                </Text>
              </Pressable>
            )}
          </View>
        )}

        {searchError && <Text style={[styles.errorText, { color: colors.danger }]}>{searchError}</Text>}
      </Card>

      {/* 2. Tabs: Friends, Incoming Requests, Sent Requests */}
      <Segments
        items={['Bạn bè', `Lời mời (${incoming.length})`, 'Đã gửi']}
        selected={
          activeTab === 'Bạn bè'
            ? 'Bạn bè'
            : activeTab === 'Lời mời'
            ? `Lời mời (${incoming.length})`
            : 'Đã gửi'
        }
        onSelect={(tab) => {
          if (tab.startsWith('Lời mời')) setActiveTab('Lời mời');
          else if (tab === 'Đã gửi') setActiveTab('Đã gửi');
          else setActiveTab('Bạn bè');
        }}
      />

      {/* Tab 1: Friends List */}
      {activeTab === 'Bạn bè' && (
        <Card variant="list">
          <SectionTitle title={`Danh sách bạn bè (${friends.length})`} />
          {loading ? (
            <Skeleton wide />
          ) : friends.length === 0 ? (
            <EmptyState
              icon="friends"
              title="Chưa có bạn bè nào"
              description="Tìm kiếm theo số điện thoại hoặc SpendId để kết bạn và chia sẻ chi tiêu."
            />
          ) : (
            friends.map((friend) => (
              <View key={friend.userId} style={[styles.listItem, { borderBottomColor: colors.border }]}>
                <View style={styles.userRow}>
                  <View style={[styles.avatar, { backgroundColor: colors.primarySoft }]}>
                    <UIIcon name="profile" size={24} />
                  </View>
                  <View style={styles.userInfo}>
                    <Text style={[styles.userName, { color: colors.text }]}>
                      {friend.nickname || friend.fullName}
                    </Text>
                    <Text style={[styles.userSpendId, { color: colors.textMuted }]}>
                      {friend.nickname ? `${friend.fullName} • ` : ''}{friend.spendId}
                    </Text>
                  </View>
                </View>

                <Pressable
                  accessibilityRole="button"
                  onPress={() => handleUnfriend(friend.userId, friend.fullName)}
                  style={styles.unfriendBtn}
                >
                  <Text style={[styles.unfriendText, { color: colors.danger }]}>Hủy</Text>
                </Pressable>
              </View>
            ))
          )}
        </Card>
      )}

      {/* Tab 2: Incoming Requests */}
      {activeTab === 'Lời mời' && (
        <Card variant="list">
          <SectionTitle title="Lời mời kết bạn đã nhận" />
          {incoming.length === 0 ? (
            <EmptyState
              icon="mail"
              title="Không có lời mời nào"
              description="Khi có người gửi lời mời kết bạn, yêu cầu sẽ hiển thị tại đây."
            />
          ) : (
            incoming.map((req) => (
              <View key={req.id} style={[styles.listItem, { borderBottomColor: colors.border }]}>
                <View style={styles.userInfo}>
                  <Text style={[styles.userName, { color: colors.text }]}>{req.fullName}</Text>
                  <Text style={[styles.userSpendId, { color: colors.textMuted }]}>{req.spendId}</Text>
                  {req.message && <Copy small muted>{req.message}</Copy>}
                </View>

                <View style={styles.actionRow}>
                  <Pressable
                    accessibilityRole="button"
                    onPress={() => handleAcceptRequest(req.id)}
                    disabled={actionInProgress === req.id}
                    style={[styles.smallBtn, { backgroundColor: colors.primary }]}
                  >
                    <Text style={[styles.smallBtnText, { color: colors.onButton }]}>Đồng ý</Text>
                  </Pressable>
                  <Pressable
                    accessibilityRole="button"
                    onPress={() => handleRejectRequest(req.id)}
                    disabled={actionInProgress === req.id}
                    style={[styles.smallBtn, { backgroundColor: colors.muted }]}
                  >
                    <Text style={[styles.smallBtnText, { color: colors.textMuted }]}>Từ chối</Text>
                  </Pressable>
                </View>
              </View>
            ))
          )}
        </Card>
      )}

      {/* Tab 3: Outgoing Requests */}
      {activeTab === 'Đã gửi' && (
        <Card variant="list">
          <SectionTitle title="Lời mời kết bạn đã gửi" />
          {outgoing.length === 0 ? (
            <EmptyState
              icon="mail"
              title="Chưa gửi lời mời nào"
              description="Các lời mời kết bạn đang chờ phản hồi sẽ xuất hiện tại đây."
            />
          ) : (
            outgoing.map((req) => (
              <View key={req.id} style={[styles.listItem, { borderBottomColor: colors.border }]}>
                <View style={styles.userInfo}>
                  <Text style={[styles.userName, { color: colors.text }]}>{req.fullName}</Text>
                  <Text style={[styles.userSpendId, { color: colors.textMuted }]}>{req.spendId}</Text>
                </View>

                <Pressable
                  accessibilityRole="button"
                  onPress={() => handleCancelRequest(req.id)}
                  disabled={actionInProgress === req.id}
                  style={[styles.smallBtn, { backgroundColor: colors.muted }]}
                >
                  <Text style={[styles.smallBtnText, { color: colors.danger }]}>Thu hồi</Text>
                </Pressable>
              </View>
            ))
          )}
        </Card>
      )}
    </AppScreen>
  );
}

const styles = StyleSheet.create({
  searchRow: { flexDirection: 'row', gap: spacing.sm, alignItems: 'center' },
  searchInputContainer: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
    borderWidth: 1,
    borderRadius: radius.md,
    paddingHorizontal: spacing.md,
    height: 48,
  },
  searchInput: { flex: 1, fontSize: 13, ...fontStyles.body },
  searchBtn: { paddingHorizontal: spacing.md, height: 48, borderRadius: radius.md, justifyContent: 'center', alignItems: 'center' },
  searchBtnText: { fontSize: 13, ...fontStyles.semibold },
  searchResultCard: { borderWidth: 1, borderRadius: radius.md, padding: spacing.md, gap: spacing.sm, marginTop: spacing.sm },
  userRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm, flex: 1 },
  avatar: { width: 40, height: 40, borderRadius: 14, alignItems: 'center', justifyContent: 'center' },
  userInfo: { flex: 1, gap: 2 },
  userName: { fontSize: 14, ...fontStyles.semibold },
  userSpendId: { fontSize: 11, fontVariant: ['tabular-nums'], ...fontStyles.body },
  statusBadge: { fontSize: 12, ...fontStyles.semibold },
  actionBtn: { paddingVertical: spacing.sm, paddingHorizontal: spacing.md, borderRadius: radius.sm, alignItems: 'center' },
  actionBtnText: { fontSize: 12, ...fontStyles.semibold },
  errorText: { fontSize: 12, ...fontStyles.body, marginTop: spacing.xs },
  listItem: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingVertical: spacing.sm, borderBottomWidth: StyleSheet.hairlineWidth },
  actionRow: { flexDirection: 'row', gap: spacing.xs },
  smallBtn: { paddingVertical: 6, paddingHorizontal: 12, borderRadius: radius.sm },
  smallBtnText: { fontSize: 11, ...fontStyles.semibold },
  unfriendBtn: { padding: spacing.xs },
  unfriendText: { fontSize: 11, ...fontStyles.body },
});
