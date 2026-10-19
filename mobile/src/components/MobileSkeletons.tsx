import React from 'react';
import { StyleSheet, View } from 'react-native';
import { radius, spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

/**
 * Premium Stitch V2 Skeleton placeholders for Mobile screens.
 * Uses consistent soft emerald-toned or slate muted blocks.
 * Zero rotating spinners.
 */

export function SkeletonBlock({
  width = '100%',
  height = 16,
  borderRadius = 8,
  style,
}: {
  width?: number | string;
  height?: number;
  borderRadius?: number;
  style?: object;
}) {
  const { colors, isDark } = useTheme();
  return (
    <View
      aria-hidden
      accessible={false}
      accessibilityElementsHidden
      importantForAccessibility="no-hide-descendants"
      style={[
        {
          width: width as any,
          height,
          borderRadius,
          backgroundColor: isDark ? 'rgba(30, 41, 59, 0.7)' : 'rgba(226, 232, 240, 0.8)',
        },
        style,
      ]}
    />
  );
}

export function HomeSkeleton() {
  const { colors, isDark } = useTheme();
  return (
    <View style={styles.container}>
      {/* Header Profile + Balance Skeleton Card */}
      <View
        style={[
          styles.card,
          {
            backgroundColor: isDark ? '#064E3B' : '#047857',
            borderColor: 'transparent',
            padding: spacing.lg,
          },
        ]}
      >
        <View style={styles.rowBetween}>
          <SkeletonBlock width={120} height={14} borderRadius={6} style={{ backgroundColor: 'rgba(255,255,255,0.2)' }} />
          <SkeletonBlock width={36} height={36} borderRadius={18} style={{ backgroundColor: 'rgba(255,255,255,0.2)' }} />
        </View>
        <View style={{ marginTop: spacing.md, gap: 8 }}>
          <SkeletonBlock width={80} height={12} borderRadius={4} style={{ backgroundColor: 'rgba(255,255,255,0.2)' }} />
          <SkeletonBlock width={220} height={32} borderRadius={8} style={{ backgroundColor: 'rgba(255,255,255,0.3)' }} />
        </View>
        <View style={[styles.rowBetween, { marginTop: spacing.md }]}>
          <SkeletonBlock width={110} height={40} borderRadius={10} style={{ backgroundColor: 'rgba(255,255,255,0.15)' }} />
          <SkeletonBlock width={110} height={40} borderRadius={10} style={{ backgroundColor: 'rgba(255,255,255,0.15)' }} />
        </View>
      </View>

      {/* Quick Action Pills Skeleton */}
      <View style={styles.rowBetween}>
        {[1, 2, 3, 4].map((i) => (
          <View key={i} style={styles.actionCol}>
            <SkeletonBlock width={54} height={54} borderRadius={18} />
            <SkeletonBlock width={48} height={10} borderRadius={4} style={{ marginTop: 6 }} />
          </View>
        ))}
      </View>

      {/* Net Cashflow Card Skeleton */}
      <View style={[styles.card, { backgroundColor: colors.surfaceElevated, borderColor: colors.border }]}>
        <View style={styles.rowBetween}>
          <SkeletonBlock width={140} height={18} borderRadius={6} />
          <SkeletonBlock width={70} height={14} borderRadius={4} />
        </View>
        <View style={{ marginTop: spacing.sm, gap: 10 }}>
          <SkeletonBlock width="100%" height={24} borderRadius={6} />
          <SkeletonBlock width="100%" height={24} borderRadius={6} />
        </View>
      </View>

      {/* Recent Transactions Skeleton */}
      <View style={[styles.card, { backgroundColor: colors.surfaceElevated, borderColor: colors.border }]}>
        <SkeletonBlock width={160} height={18} borderRadius={6} />
        <View style={{ marginTop: spacing.sm, gap: 12 }}>
          {[1, 2, 3].map((i) => (
            <View key={i} style={styles.rowBetween}>
              <View style={{ flexDirection: 'row', alignItems: 'center', gap: 12 }}>
                <SkeletonBlock width={40} height={40} borderRadius={12} />
                <View style={{ gap: 6 }}>
                  <SkeletonBlock width={120} height={14} borderRadius={4} />
                  <SkeletonBlock width={80} height={10} borderRadius={4} />
                </View>
              </View>
              <SkeletonBlock width={90} height={16} borderRadius={4} />
            </View>
          ))}
        </View>
      </View>
    </View>
  );
}

export function WalletSkeleton() {
  const { colors } = useTheme();
  return (
    <View style={styles.container}>
      <View style={[styles.card, { backgroundColor: colors.surfaceElevated, borderColor: colors.border }]}>
        <SkeletonBlock width={130} height={16} borderRadius={6} />
        <SkeletonBlock width={200} height={30} borderRadius={8} style={{ marginTop: 8 }} />
        <View style={[styles.rowBetween, { marginTop: 14 }]}>
          <SkeletonBlock width="48%" height={46} borderRadius={12} />
          <SkeletonBlock width="48%" height={46} borderRadius={12} />
        </View>
      </View>
      <View style={[styles.card, { backgroundColor: colors.surfaceElevated, borderColor: colors.border }]}>
        <SkeletonBlock width={180} height={18} borderRadius={6} />
        <View style={{ marginTop: spacing.sm, gap: 12 }}>
          {[1, 2, 3, 4].map((i) => (
            <View key={i} style={styles.rowBetween}>
              <View style={{ flexDirection: 'row', alignItems: 'center', gap: 10 }}>
                <SkeletonBlock width={36} height={36} borderRadius={10} />
                <SkeletonBlock width={130} height={14} borderRadius={4} />
              </View>
              <SkeletonBlock width={80} height={14} borderRadius={4} />
            </View>
          ))}
        </View>
      </View>
    </View>
  );
}

export function TransferSkeleton() {
  const { colors } = useTheme();
  return (
    <View style={styles.container}>
      <View style={[styles.card, { backgroundColor: colors.surfaceElevated, borderColor: colors.border }]}>
        <SkeletonBlock width={140} height={16} borderRadius={6} />
        <SkeletonBlock width="100%" height={48} borderRadius={12} style={{ marginTop: 10 }} />
      </View>
      <View style={[styles.card, { backgroundColor: colors.surfaceElevated, borderColor: colors.border }]}>
        <SkeletonBlock width={150} height={16} borderRadius={6} />
        <SkeletonBlock width="100%" height={48} borderRadius={12} style={{ marginTop: 10 }} />
      </View>
      <View style={[styles.card, { backgroundColor: colors.surfaceElevated, borderColor: colors.border }]}>
        <SkeletonBlock width={160} height={16} borderRadius={6} />
        <SkeletonBlock width="100%" height={56} borderRadius={12} style={{ marginTop: 10 }} />
        <View style={[styles.rowBetween, { marginTop: 10 }]}>
          {[1, 2, 3, 4, 5].map((i) => (
            <SkeletonBlock key={i} width={56} height={32} borderRadius={8} />
          ))}
        </View>
      </View>
    </View>
  );
}

export function PlanningSkeleton() {
  const { colors } = useTheme();
  return (
    <View style={styles.container}>
      <SkeletonBlock width="100%" height={44} borderRadius={12} />
      <View style={[styles.card, { backgroundColor: colors.surfaceElevated, borderColor: colors.border }]}>
        <View style={styles.rowBetween}>
          <SkeletonBlock width={150} height={18} borderRadius={6} />
          <SkeletonBlock width={70} height={16} borderRadius={6} />
        </View>
        <SkeletonBlock width="100%" height={12} borderRadius={6} style={{ marginTop: 12 }} />
        <View style={[styles.rowBetween, { marginTop: 8 }]}>
          <SkeletonBlock width={100} height={12} borderRadius={4} />
          <SkeletonBlock width={100} height={12} borderRadius={4} />
        </View>
      </View>
      <View style={[styles.card, { backgroundColor: colors.surfaceElevated, borderColor: colors.border }]}>
        <SkeletonBlock width={180} height={18} borderRadius={6} />
        <View style={{ marginTop: 12, gap: 10 }}>
          {[1, 2].map((i) => (
            <View key={i} style={styles.rowBetween}>
              <SkeletonBlock width={140} height={16} borderRadius={4} />
              <SkeletonBlock width={80} height={16} borderRadius={4} />
            </View>
          ))}
        </View>
      </View>
    </View>
  );
}

export function FriendsSkeleton() {
  const { colors } = useTheme();
  return (
    <View style={styles.container}>
      <SkeletonBlock width="100%" height={48} borderRadius={12} />
      <View style={[styles.card, { backgroundColor: colors.surfaceElevated, borderColor: colors.border }]}>
        <SkeletonBlock width={140} height={16} borderRadius={6} />
        <View style={{ marginTop: 12, gap: 14 }}>
          {[1, 2, 3].map((i) => (
            <View key={i} style={styles.rowBetween}>
              <View style={{ flexDirection: 'row', alignItems: 'center', gap: 12 }}>
                <SkeletonBlock width={42} height={42} borderRadius={21} />
                <View style={{ gap: 6 }}>
                  <SkeletonBlock width={120} height={14} borderRadius={4} />
                  <SkeletonBlock width={90} height={10} borderRadius={4} />
                </View>
              </View>
              <SkeletonBlock width={70} height={32} borderRadius={8} />
            </View>
          ))}
        </View>
      </View>
    </View>
  );
}

export function MenuSkeleton() {
  const { colors } = useTheme();
  return (
    <View style={styles.container}>
      <View style={[styles.card, { backgroundColor: colors.surfaceElevated, borderColor: colors.border }]}>
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: 14 }}>
          <SkeletonBlock width={52} height={52} borderRadius={26} />
          <View style={{ gap: 8, flex: 1 }}>
            <SkeletonBlock width={140} height={18} borderRadius={6} />
            <SkeletonBlock width={180} height={12} borderRadius={4} />
          </View>
        </View>
      </View>
      {[1, 2, 3].map((section) => (
        <View key={section} style={[styles.card, { backgroundColor: colors.surfaceElevated, borderColor: colors.border }]}>
          <SkeletonBlock width={120} height={16} borderRadius={6} />
          <View style={{ marginTop: 10, gap: 12 }}>
            {[1, 2, 3].map((item) => (
              <View key={item} style={styles.rowBetween}>
                <View style={{ flexDirection: 'row', alignItems: 'center', gap: 12 }}>
                  <SkeletonBlock width={28} height={28} borderRadius={8} />
                  <SkeletonBlock width={130} height={14} borderRadius={4} />
                </View>
                <SkeletonBlock width={16} height={16} borderRadius={4} />
              </View>
            ))}
          </View>
        </View>
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    gap: spacing.md,
    paddingBottom: spacing.xxl,
  },
  card: {
    borderRadius: 20,
    borderWidth: 1,
    padding: spacing.md,
  },
  rowBetween: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  actionCol: {
    alignItems: 'center',
    width: 68,
  },
});
