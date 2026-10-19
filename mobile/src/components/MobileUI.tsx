import type { PropsWithChildren } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { SymbolView } from 'expo-symbols';
import { effects, fontStyles, radius, spacing, typography } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

const symbols = {
  mail: ['envelope', 'mail'], key: ['lock', 'lock'],
  wallet: ['wallet.bifold', 'account_balance_wallet'], transfer: ['arrow.left.arrow.right', 'swap_horiz'],
  add: ['plus', 'add'], budget: ['chart.pie', 'pie_chart'], goal: ['scope', 'track_changes'],
  calendar: ['calendar', 'calendar_month'], friends: ['person.2', 'group'], bank: ['building.columns', 'account_balance'],
  bell: ['bell', 'notifications'], ai: ['sparkles', 'auto_awesome'], help: ['lifepreserver', 'support_agent'],
  profile: ['person.crop.circle', 'account_circle'], settings: ['slider.horizontal.3', 'tune'],
  devices: ['iphone', 'devices'], lock: ['lock.shield', 'shield'], chevron: ['chevron.right', 'chevron_right'],
  down: ['chevron.down', 'expand_more'], search: ['magnifyingglass', 'search'], list: ['list.bullet.rectangle', 'receipt_long'],
  income: ['arrow.down.left', 'south_west'], expense: ['arrow.up.right', 'north_east'], folder: ['folder', 'folder'],
} as const;
export type IconName = keyof typeof symbols;

export function UIIcon({ name, size = 22, color }: { name: IconName; size?: number; color?: string }) {
  const { colors } = useTheme();
  const [ios, material] = symbols[name];
  return <View aria-hidden accessible={false} accessibilityElementsHidden importantForAccessibility="no-hide-descendants"><SymbolView name={{ ios, android: material, web: material }} size={size} tintColor={color ?? colors.primary} /></View>;
}

export function Copy({ children, muted = false, small = false }: PropsWithChildren<{ muted?: boolean; small?: boolean }>) {
  const { colors } = useTheme();
  return <Text style={[small ? typography.caption : typography.body, { color: muted ? colors.textMuted : colors.text }]}>{children}</Text>;
}

export function Card({ children, variant = 'panel' }: PropsWithChildren<{ variant?: 'panel' | 'dashboard' | 'list' }>) {
  const { colors } = useTheme();
  return <View style={[styles.card, { backgroundColor: variant === 'dashboard' ? colors.surfaceElevated : colors.surfacePanel, borderColor: colors.border, borderRadius: variant === 'panel' ? radius.panel : radius.lg }]}>{children}</View>;
}

export function SectionTitle({ title, action, onPress }: { title: string; action?: string; onPress?: () => void }) {
  const { colors } = useTheme();
  return <View style={styles.sectionRow}><Text accessibilityRole="header" style={[styles.sectionTitle, { color: colors.text }]}>{title}</Text>{action && <Pressable accessibilityRole="button" onPress={onPress} disabled={!onPress} accessibilityState={{ disabled: !onPress }} style={({ pressed }) => [styles.textAction, { opacity: pressed ? 0.65 : 1 }]}><Text style={[styles.actionText, { color: colors.primary }]}>{action}</Text></Pressable>}</View>;
}

export function Badge({ children }: PropsWithChildren) {
  const { colors } = useTheme();
  return <View style={[styles.badge, { backgroundColor: colors.primarySoft }]}><Text style={[styles.badgeText, { color: colors.primary }]}>{children}</Text></View>;
}

export function Skeleton({ wide = false, balance = false }: { wide?: boolean; balance?: boolean }) {
  const { colors } = useTheme();
  return <View aria-hidden accessible={false} accessibilityElementsHidden importantForAccessibility="no-hide-descendants" style={[styles.skeleton, { backgroundColor: colors.muted, width: wide ? '100%' : '64%', height: balance ? 36 : 16 }]} />;
}

export function UnwiredNote({ children = 'Dữ liệu tài chính chưa được kết nối.' }: PropsWithChildren) {
  return <Copy muted small>{children}</Copy>;
}

export function EmptyState({ icon, title, description }: { icon: IconName; title: string; description: string }) {
  const { colors } = useTheme();
  return <View style={styles.empty}><View style={[styles.emptyIcon, { backgroundColor: colors.muted }]}><UIIcon name={icon} size={26} color={colors.textMuted} /></View><Text style={[styles.emptyTitle, { color: colors.text }]}>{title}</Text><Text style={[styles.emptyCopy, { color: colors.textMuted }]}>{description}</Text></View>;
}

export function ActionTile({ icon, label, onPress }: { icon: IconName; label: string; onPress?: () => void }) {
  const { colors } = useTheme();
  return <Pressable accessibilityRole="button" accessibilityLabel={label} accessibilityState={{ disabled: !onPress }} disabled={!onPress} onPress={onPress} style={({ pressed }) => [styles.tile, { opacity: pressed ? 0.65 : 1 }]}><View style={[styles.tileIcon, { backgroundColor: colors.primarySoft, borderColor: colors.border }]}><UIIcon name={icon} /></View><Text style={[styles.tileLabel, { color: onPress ? colors.text : colors.textMuted }]}>{label}</Text></Pressable>;
}

export function SettingRow({ icon, title, detail, onPress }: { icon: IconName; title: string; detail: string; onPress?: () => void }) {
  const { colors } = useTheme();
  return <Pressable accessibilityRole="button" disabled={!onPress} accessibilityState={{ disabled: !onPress }} onPress={onPress} style={({ pressed }) => [styles.setting, { borderBottomColor: colors.border, opacity: pressed ? 0.65 : 1 }]}><View style={[styles.settingIcon, { backgroundColor: colors.primarySoft }]}><UIIcon name={icon} /></View><View style={styles.settingBody}><Text style={[styles.settingTitle, { color: colors.text }]}>{title}</Text><Copy muted small>{detail}</Copy></View>{onPress && <UIIcon name="chevron" color={colors.textMuted} size={18} />}</Pressable>;
}

export function Segments({ items, selected, onSelect }: { items: string[]; selected: string; onSelect: (item: string) => void }) {
  const { colors } = useTheme();
  return <View style={[styles.segments, { backgroundColor: colors.muted, borderColor: colors.border }]}>{items.map(item => <Pressable key={item} accessibilityRole="button" accessibilityState={{ selected: item === selected }} onPress={() => onSelect(item)} style={({ pressed }) => [styles.segment, { backgroundColor: item === selected ? colors.surfaceElevated : 'transparent', opacity: pressed ? 0.65 : 1 }]}><Text style={[styles.segmentLabel, { color: item === selected ? colors.primary : colors.textMuted }]}>{item}</Text></Pressable>)}</View>;
}

const styles = StyleSheet.create({
  card: { borderWidth: 1, padding: spacing.md, gap: spacing.md, boxShadow: effects.cardShadow },
  sectionRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: spacing.sm },
  sectionTitle: { ...typography.sectionTitle, flex: 1 }, textAction: { minHeight: 48, justifyContent: 'center' }, actionText: { fontSize: 13, ...fontStyles.semibold },
  badge: { paddingVertical: 5, paddingHorizontal: 9, borderRadius: radius.sm, alignSelf: 'flex-start' }, badgeText: { fontSize: 12, ...fontStyles.semibold },
  skeleton: { height: 16, borderRadius: 6 }, empty: { alignItems: 'center', gap: spacing.sm, paddingVertical: spacing.md },
  emptyIcon: { width: 48, height: 48, borderRadius: radius.md, alignItems: 'center', justifyContent: 'center' },
  emptyTitle: { fontSize: 15, lineHeight: 22, ...fontStyles.semibold, textAlign: 'center' }, emptyCopy: { ...fontStyles.body, fontSize: 13, lineHeight: 20, textAlign: 'center' },
  tile: { flex: 1, minWidth: 0, alignItems: 'center', gap: spacing.sm }, tileIcon: { width: 48, height: 48, borderWidth: 1, borderRadius: radius.lg, alignItems: 'center', justifyContent: 'center' },
  tileLabel: { fontSize: 12, lineHeight: 18, textAlign: 'center', ...fontStyles.semibold },
  setting: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, minHeight: 76, paddingVertical: spacing.sm, borderBottomWidth: StyleSheet.hairlineWidth },
  settingIcon: { width: 40, height: 40, borderRadius: radius.md, alignItems: 'center', justifyContent: 'center' }, settingBody: { flex: 1, gap: 3 }, settingTitle: { fontSize: 14, lineHeight: 22, ...fontStyles.semibold },
  segments: { flexDirection: 'row', borderWidth: 1, padding: spacing.xs, borderRadius: radius.md }, segment: { flex: 1, minHeight: 48, padding: spacing.xs, borderRadius: radius.sm, alignItems: 'center', justifyContent: 'center' }, segmentLabel: { fontSize: 13, lineHeight: 19, textAlign: 'center', ...fontStyles.semibold },
});
