import { Tabs, useRouter, useSegments } from 'expo-router';
import { useEffect } from 'react';
import { SymbolView } from 'expo-symbols';
import type { SymbolViewProps } from 'expo-symbols';
import { StyleSheet, Text, View, useWindowDimensions } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useAuth } from '@/auth/AuthProvider';
import { HomeSkeleton } from '@/components/MobileSkeletons';
import { navigation, radius, spacing, typography } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

const tabs: { name: string; title: string; icon: SymbolViewProps['name'] }[] = [
  { name: 'home', title: 'Trang chủ', icon: { ios: 'house', android: 'home', web: 'home' } },
  { name: 'wallet', title: 'Ví & GD', icon: { ios: 'wallet.bifold', android: 'account_balance_wallet', web: 'account_balance_wallet' } },
  { name: 'transfer', title: 'Chuyển tiền', icon: { ios: 'arrow.left.arrow.right', android: 'swap_horiz', web: 'swap_horiz' } },
  { name: 'planning', title: 'Kế hoạch', icon: { ios: 'calendar.badge.checkmark', android: 'event_available', web: 'event_available' } },
  { name: 'menu', title: 'Tiện ích', icon: { ios: 'square.grid.2x2', android: 'grid_view', web: 'grid_view' } },
];

import { GlobalFloatingAiAssistant } from '@/components/GlobalFloatingAiAssistant';

export default function AuthenticatedLayout() {
  const { status, user } = useAuth();
  const { colors, isDark } = useTheme();
  const insets = useSafeAreaInsets();
  const { fontScale } = useWindowDimensions();
  const router = useRouter();
  const segments = useSegments();
  const isAuthenticatedRoute = segments[0] === '(authenticated)';
  const tabHeight = Math.max(navigation.tabHeight, navigation.iconContainer + typography.tabLabel.lineHeight * fontScale * 2 + spacing.md);

  useEffect(() => {
    // A retained tab layout must not redirect while a guest route is active.
    // Session cleanup clears user before changing status. Keep the shell hidden
    // until status settles; guest screens still treat authenticated as signed in.
    if (!isAuthenticatedRoute || status === 'initializing' || status === 'authenticated') return;
    const target = status === 'challenge-pending' ? '/(guest)/pin' : '/(guest)/login';
    router.replace(target);
  }, [isAuthenticatedRoute, router, status]);

  if (status === 'initializing') {
    return (
      <View style={{ flex: 1, backgroundColor: colors.background, paddingHorizontal: spacing.md, paddingTop: insets.top + spacing.md }}>
        <HomeSkeleton />
      </View>
    );
  }
  if (status !== 'authenticated' || !user) return null;

  return (
    <View style={{ flex: 1 }}>
      <Tabs initialRouteName="home" screenOptions={{
        headerShown: false,
        sceneStyle: { backgroundColor: colors.background },
        tabBarActiveTintColor: colors.primary,
        tabBarInactiveTintColor: colors.textMuted,
        tabBarLabelPosition: 'below-icon',
        tabBarHideOnKeyboard: true,
        tabBarStyle: { backgroundColor: colors.surfaceElevated, borderTopColor: colors.border, height: tabHeight + insets.bottom, paddingTop: spacing.xs, paddingBottom: Math.max(insets.bottom, spacing.xs), elevation: 0 },
        tabBarItemStyle: { minHeight: 48, paddingHorizontal: 0 },
        tabBarIconStyle: { height: navigation.iconContainer, width: navigation.iconContainer },
        animation: 'none',
      }}>
        {tabs.map((tab) => (
          <Tabs.Screen key={tab.name} name={tab.name} options={{
            title: tab.title,
            tabBarAccessibilityLabel: tab.title,
            tabBarLabel: ({ color }) => <Text style={[styles.label, { color }]}>{tab.title}</Text>,
            tabBarIcon: ({ color, focused }) => (
              <View aria-hidden accessible={false} accessibilityElementsHidden importantForAccessibility="no-hide-descendants" style={[styles.icon, focused && { backgroundColor: colors.primarySoft }, tab.name === 'transfer' && { backgroundColor: colors.primary, transform: [{ translateY: -2 }] }]}>
                <SymbolView name={tab.icon} size={navigation.iconSize} tintColor={tab.name === 'transfer' ? (isDark ? colors.background : colors.surfaceElevated) : color} />
              </View>
            ),
          }} />
        ))}
      </Tabs>
      <GlobalFloatingAiAssistant />
    </View>
  );
}

const styles = StyleSheet.create({
  loading: { flex: 1, alignItems: 'center', justifyContent: 'center' },
  icon: { width: navigation.iconContainer, height: navigation.iconContainer, borderRadius: radius.md, alignItems: 'center', justifyContent: 'center' },
  label: { ...typography.tabLabel, textAlign: 'center', marginHorizontal: 2 },
});
