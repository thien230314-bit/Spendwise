import type { ComponentProps } from 'react';
import { useState } from 'react';
import { StyleSheet, Text, TextInput, View } from 'react-native';
import { effects, fontStyles, radius, spacing, typography } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';
import { UIIcon, type IconName } from './MobileUI';

type Props = ComponentProps<typeof TextInput> & { label: string; icon?: IconName };

export function FormField({ label, icon, style, onFocus, onBlur, ...props }: Props) {
  const { colors } = useTheme();
  const [focused, setFocused] = useState(false);
  return (
    <View style={styles.group}>
      <Text style={[styles.label, { color: colors.text }]}>{label}</Text>
      <View style={styles.inputWrap}>
      <TextInput {...props} accessibilityLabel={props.accessibilityLabel ?? label} placeholderTextColor={colors.placeholder} selectionColor={colors.primary}
        onFocus={event => { setFocused(true); onFocus?.(event); }} onBlur={event => { setFocused(false); onBlur?.(event); }}
        style={[styles.input, { backgroundColor: colors.input, borderColor: focused ? colors.focus : colors.border, color: colors.text, boxShadow: focused ? effects.focusRing : effects.controlShadow }, icon && { paddingLeft: 40 }, style]} />
        {icon && <View style={styles.icon}><UIIcon name={icon} size={18} color={focused ? colors.primary : colors.placeholder} /></View>}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  inputWrap: { position: 'relative' }, icon: { position: 'absolute', left: 14, top: 15, pointerEvents: 'none' },
  group: { gap: spacing.xs }, label: { ...typography.label },
  input: { ...fontStyles.medium, borderRadius: radius.field, borderWidth: 1, fontSize: 16, minHeight: 48, paddingHorizontal: 14, paddingVertical: 11 },
});
