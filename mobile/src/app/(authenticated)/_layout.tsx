import { Tabs, useRouter, useSegments } from 'expo-router';
import { useEffect } from 'react';
import { StyleSheet, Text, View, useWindowDimensions } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useAuth } from '@/auth/AuthProvider';
import { HomeSkeleton } from '@/components/MobileSkeletons';
import { GlobalFloatingAiAssistant } from '@/components/GlobalFloatingAiAssistant';
import { navigation, radius, spacing, typography } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

export default function AuthenticatedLayout() {
  const { status, user } = useAuth();
  const { colors, isDark } = useTheme();
  const insets = useSafeAreaInsets();
  const { fontScale } = useWindowDimensions();
  const router = useRouter();
  const segments = useSegments();
  const isAuthenticatedRoute = segments[0] === '(authenticated)';
  const tabHeight = Math.max(
    navigation.tabHeight,
    navigation.iconContainer + typography.tabLabel.lineHeight * fontScale * 2 + spacing.md
  );

  useEffect(() => {
    if (!isAuthenticatedRoute || status === 'initializing' || status === 'authenticated') return;
    const target = status === 'challenge-pending' ? '/(guest)/pin' : '/(guest)/login';
    router.replace(target);
  }, [isAuthenticatedRoute, router, status]);

  if (status === 'initializing') {
    return (
      <View
        style={{
          flex: 1,
          backgroundColor: colors.background,
          paddingHorizontal: spacing.md,
          paddingTop: insets.top + spacing.md,
        }}
      >
        <HomeSkeleton />
      </View>
    );
  }
  if (status !== 'authenticated' || !user) return null;

  return (
    <View style={{ flex: 1 }}>
      <Tabs
        initialRouteName="home"
        screenOptions={{
          headerShown: false,
          sceneStyle: { backgroundColor: colors.background },
          tabBarActiveTintColor: colors.primary,
          tabBarInactiveTintColor: colors.textMuted,
          tabBarLabelPosition: 'below-icon',
          tabBarHideOnKeyboard: true,
          tabBarStyle: {
            backgroundColor: isDark ? 'rgba(18, 42, 34, 0.94)' : 'rgba(255, 255, 255, 0.94)',
            borderTopColor: colors.border,
            borderTopWidth: 1,
            height: tabHeight + insets.bottom,
            paddingTop: spacing.xs,
            paddingBottom: Math.max(insets.bottom, spacing.xs),
            elevation: 8,
            shadowColor: '#000',
            shadowOffset: { width: 0, height: -2 },
            shadowOpacity: 0.06,
            shadowRadius: 8,
          },
          tabBarItemStyle: { minHeight: 48, paddingHorizontal: 0 },
          animation: 'none',
        }}
      >
        {/* TAB 1: TRANG CHỦ */}
        <Tabs.Screen
          name="home"
          options={{
            title: 'Trang chủ',
            tabBarAccessibilityLabel: 'Trang chủ',
            tabBarLabel: ({ color }) => (
              <Text style={[styles.label, { color }]}>Trang chủ</Text>
            ),
            tabBarIcon: ({ color, focused }) => (
              <View style={[styles.icon, focused && { backgroundColor: isDark ? '#0F3D2E' : '#E6F8F0' }]}>
                <Ionicons
                  name={focused ? 'home' : 'home-outline'}
                  size={20}
                  color={color}
                />
              </View>
            ),
          }}
        />

        {/* TAB 2: QUỸ NHÓM */}
        <Tabs.Screen
          name="groups"
          options={{
            title: 'Quỹ nhóm',
            tabBarAccessibilityLabel: 'Quỹ nhóm',
            tabBarLabel: ({ color }) => (
              <Text style={[styles.label, { color }]}>Quỹ nhóm</Text>
            ),
            tabBarIcon: ({ color, focused }) => (
              <View style={[styles.icon, focused && { backgroundColor: isDark ? '#0F3D2E' : '#E6F8F0' }]}>
                <Ionicons
                  name={focused ? 'people' : 'people-outline'}
                  size={21}
                  color={color}
                />
              </View>
            ),
          }}
        />

        {/* TAB 3: QR (CENTER RAISED BUTTON) */}
        <Tabs.Screen
          name="qr"
          options={{
            title: 'QR',
            tabBarAccessibilityLabel: 'Mã QR & Quét',
            tabBarLabel: ({ color }) => (
              <Text style={[styles.label, { color: colors.primary, fontWeight: '700' }]}>QR</Text>
            ),
            tabBarIcon: ({ focused }) => (
              <View
                style={[
                  styles.qrRaisedButton,
                  {
                    backgroundColor: colors.primary,
                    shadowColor: colors.primary,
                  },
                ]}
              >
                <Ionicons name="qr-code" size={24} color="#FFFFFF" />
              </View>
            ),
          }}
        />

        {/* TAB 4: LỊCH SỬ */}
        <Tabs.Screen
          name="history"
          options={{
            title: 'Lịch sử',
            tabBarAccessibilityLabel: 'Lịch sử giao dịch',
            tabBarLabel: ({ color }) => (
              <Text style={[styles.label, { color }]}>Lịch sử</Text>
            ),
            tabBarIcon: ({ color, focused }) => (
              <View style={[styles.icon, focused && { backgroundColor: isDark ? '#0F3D2E' : '#E6F8F0' }]}>
                <Ionicons
                  name={focused ? 'time' : 'time-outline'}
                  size={21}
                  color={color}
                />
              </View>
            ),
          }}
        />

        {/* TAB 5: CÁ NHÂN */}
        <Tabs.Screen
          name="profile"
          options={{
            title: 'Cá nhân',
            tabBarAccessibilityLabel: 'Hồ sơ cá nhân',
            tabBarLabel: ({ color }) => (
              <Text style={[styles.label, { color }]}>Cá nhân</Text>
            ),
            tabBarIcon: ({ color, focused }) => (
              <View style={[styles.icon, focused && { backgroundColor: isDark ? '#0F3D2E' : '#E6F8F0' }]}>
                <Ionicons
                  name={focused ? 'person' : 'person-outline'}
                  size={20}
                  color={color}
                />
              </View>
            ),
          }}
        />

        {/* STACK ROUTES (NOT SHOWN AS TABS) */}
        <Tabs.Screen
          name="wallet"
          options={{
            href: null,
          }}
        />
        <Tabs.Screen
          name="transfer"
          options={{
            href: null,
          }}
        />
        <Tabs.Screen
          name="planning"
          options={{
            href: null,
          }}
        />
        <Tabs.Screen
          name="friends"
          options={{
            href: null,
          }}
        />
        <Tabs.Screen
          name="ai-chat"
          options={{
            href: null,
          }}
        />
        <Tabs.Screen
          name="menu"
          options={{
            href: null,
          }}
        />
      </Tabs>
      <GlobalFloatingAiAssistant />
    </View>
  );
}

const styles = StyleSheet.create({
  loading: { flex: 1, alignItems: 'center', justifyContent: 'center' },
  icon: {
    width: navigation.iconContainer,
    height: navigation.iconContainer,
    borderRadius: radius.md,
    alignItems: 'center',
    justifyContent: 'center',
  },
  qrRaisedButton: {
    width: 46,
    height: 46,
    borderRadius: 23,
    alignItems: 'center',
    justifyContent: 'center',
    transform: [{ translateY: -10 }],
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.35,
    shadowRadius: 8,
    elevation: 6,
  },
  label: {
    ...typography.tabLabel,
    textAlign: 'center',
    marginHorizontal: 2,
    fontSize: 11,
  },
});
