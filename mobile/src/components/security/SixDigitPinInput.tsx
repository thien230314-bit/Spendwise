import { useRef, useEffect } from 'react';
import { Platform, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { SymbolView } from 'expo-symbols';
import { useAuthColors } from '@/components/auth/AuthUI';
import { fontStyles } from '@/theme/fonts';

export interface SixDigitPinInputProps {
  value: string;
  onChangeText: (value: string) => void;
  onComplete?: (pin: string) => void;
  disabled?: boolean;
  error?: boolean;
  autoFocus?: boolean;
  mask?: boolean;
  showToggle?: boolean;
  onToggleMask?: () => void;
  label?: string;
}

export function SixDigitPinInput({
  value,
  onChangeText,
  onComplete,
  disabled = false,
  error = false,
  autoFocus = true,
  mask = true,
  showToggle = false,
  onToggleMask,
  label = 'Mã bảo mật 6 số',
}: SixDigitPinInputProps) {
  const c = useAuthColors();
  const inputRef = useRef<TextInput>(null);

  useEffect(() => {
    if (autoFocus && !disabled) {
      const timer = setTimeout(() => {
        inputRef.current?.focus();
      }, 100);
      return () => clearTimeout(timer);
    }
  }, [autoFocus, disabled]);

  const handleTextChange = (text: string) => {
    const clean = text.replace(/\D/g, '').slice(0, 6);
    onChangeText(clean);
    if (clean.length === 6 && onComplete) {
      onComplete(clean);
    }
  };

  const focusInput = () => {
    if (!disabled) {
      inputRef.current?.focus();
    }
  };

  return (
    <View style={styles.container}>
      <Pressable onPress={focusInput} style={styles.slotsRow} accessibilityLabel={label}>
        <TextInput
          ref={inputRef}
          value={value}
          onChangeText={handleTextChange}
          maxLength={6}
          keyboardType="number-pad"
          inputMode="numeric"
          secureTextEntry={mask}
          autoFocus={autoFocus}
          editable={!disabled}
          accessibilityLabel={label}
          style={styles.hiddenInput}
        />

        {Array.from({ length: 6 }).map((_, index) => {
          const char = value[index] || '';
          const isFilled = Boolean(char);
          const isCurrentActive = index === value.length && !disabled && value.length < 6;

          let borderColor = c.border;
          let backgroundColor = c.input;

          if (error) {
            borderColor = c.error;
            backgroundColor = c.errorBackground;
          } else if (isCurrentActive) {
            borderColor = c.focus;
          } else if (isFilled) {
            borderColor = c.label;
          }

          return (
            <View
              key={index}
              style={[
                styles.slot,
                {
                  backgroundColor,
                  borderColor,
                  boxShadow: isCurrentActive
                    ? '0 0 0 2px rgba(16,185,129,0.2)'
                    : '0 1px 2px rgba(15,23,42,0.03)',
                },
              ]}
            >
              {isFilled ? (
                mask ? (
                  <View style={[styles.maskDot, { backgroundColor: c.text }]} />
                ) : (
                  <Text style={[styles.digitText, { color: c.text }]}>{char}</Text>
                )
              ) : isCurrentActive ? (
                <View style={[styles.cursor, { backgroundColor: c.focus }]} />
              ) : (
                <View style={[styles.placeholderDot, { backgroundColor: c.border }]} />
              )}
            </View>
          );
        })}
      </Pressable>

      {showToggle && onToggleMask && (
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={mask ? 'Hiện mã bảo mật' : 'Ẩn mã bảo mật'}
          onPress={onToggleMask}
          style={({ pressed }) => [styles.toggleButton, { opacity: pressed ? 0.6 : 1 }]}
        >
          <View aria-hidden accessible={false}>
            <SymbolView
              name={{
                ios: mask ? 'eye' : 'eye.slash',
                android: mask ? 'visibility' : 'visibility_off',
                web: mask ? 'visibility' : 'visibility_off',
              }}
              size={15}
              tintColor={c.muted}
            />
          </View>
          <Text style={[styles.toggleText, { color: c.muted }]}>
            {mask ? 'Hiện mã bảo mật' : 'Ẩn mã bảo mật'}
          </Text>
        </Pressable>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    alignItems: 'center',
    gap: 12,
  },
  slotsRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
    position: 'relative',
    width: '100%',
  },
  hiddenInput: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
    opacity: 0.001,
    zIndex: 10,
    pointerEvents: Platform.OS === 'web' ? 'auto' : 'none',
  },
  slot: {
    width: 44,
    height: 52,
    borderRadius: 12,
    borderWidth: 2,
    alignItems: 'center',
    justifyContent: 'center',
  },
  maskDot: {
    width: 10,
    height: 10,
    borderRadius: 5,
  },
  placeholderDot: {
    width: 6,
    height: 6,
    borderRadius: 3,
  },
  digitText: {
    ...fontStyles.bold,
    fontSize: 20,
    lineHeight: 26,
    textAlign: 'center',
  },
  cursor: {
    width: 2,
    height: 20,
    borderRadius: 1,
  },
  toggleButton: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    paddingVertical: 4,
    paddingHorizontal: 8,
  },
  toggleText: {
    ...fontStyles.medium,
    fontSize: 12,
    lineHeight: 18,
  },
});
