import { useEffect, useRef, useState } from 'react';
import { AccessibilityInfo, Animated, Easing, Image, KeyboardAvoidingView, Platform, Pressable, StyleSheet, Text, useWindowDimensions, View } from 'react-native';
import type { PropsWithChildren } from 'react';
import { SafeAreaView } from 'react-native-safe-area-context';
import { router } from 'expo-router';
import { SymbolView } from 'expo-symbols';
import { authNotebookDark, authNotebookLight, authType } from '@/theme/authTokens';
import { fontStyles } from '@/theme/fonts';
import { useTheme } from '@/theme/useTheme';
import { useAuthColors } from './AuthUI';
import LoginForm from './LoginForm';
import RegisterForm from './RegisterForm';

// One clipped composition, one animation clock, two permanently mounted forms.
export function AuthComposition({ register }: { register: boolean }) {
  const { height: windowHeight } = useWindowDimensions();
  const [height, setHeight] = useState(windowHeight - 40);
  const [progress] = useState(() => new Animated.Value(register ? 1 : 0));
  const [destination, setDestination] = useState(register);
  const [settled, setSettled] = useState(register);
  const lastTarget = useRef(register);
  const pendingRoute = useRef<(() => void) | null>(null);
  const [reduced, setReduced] = useState(false);
  const [loginBusy, setLoginBusy] = useState(false);
  const [registerBusy, setRegisterBusy] = useState(false);
  const c = useAuthColors();
  const material = useTheme().isDark ? authNotebookDark : authNotebookLight;
  const welcomeHeight = height * 0.30;
  const formHeight = height * 0.70;
  const animating = destination !== settled;
  const blocked = animating || (settled ? registerBusy : loginBusy);

  useEffect(() => {
    if (Platform.OS === 'web') {
      const media = window.matchMedia('(prefers-reduced-motion: reduce)');
      const update = () => setReduced(media.matches);
      update();
      media.addEventListener('change', update);
      return () => media.removeEventListener('change', update);
    }
    let active = true;
    AccessibilityInfo.isReduceMotionEnabled().then(value => { if (active) setReduced(value); });
    const subscription = AccessibilityInfo.addEventListener('reduceMotionChanged', setReduced);
    return () => { active = false; subscription.remove(); };
  }, []);

  // Deep links/back navigation select the same composition rather than a new screen.
  useEffect(() => {
    if (lastTarget.current === register) return;
    progress.stopAnimation(() => {
      pendingRoute.current = null;
      setDestination(register);
    });
  }, [register, progress]);

  useEffect(() => {
    const complete = () => {
      setSettled(destination);
      const navigate = pendingRoute.current;
      pendingRoute.current = null;
      navigate?.();
    };
    if (reduced) {
      lastTarget.current = destination;
      progress.stopAnimation(() => { progress.setValue(destination ? 1 : 0); complete(); });
      return;
    }
    if (lastTarget.current === destination) return;
    lastTarget.current = destination;
    const animation = Animated.timing(progress, {
      toValue: destination ? 1 : 0, duration: 1000, easing: Easing.linear,
      // Per-layer easing comes from this one clock's staged interpolations.
      useNativeDriver: false,
    });
    animation.start(({ finished }) => { if (finished) complete(); });
    return () => animation.stop();
  }, [destination, reduced, progress]);

  const switchTo = (next: boolean) => {
    if (blocked) return;
    pendingRoute.current = next
      ? () => router.push('/(guest)/register')
      : () => router.replace('/(guest)/login');
    setDestination(next);
  };
  const phase = (inputRange: number[], outputRange: number[]) => progress.interpolate({
    inputRange, outputRange, easing: Easing.inOut(Easing.cubic), extrapolate: 'clamp',
  });
  // 400ms hold + 600ms movement, mirrored for the return transition.
  const loginFormY = destination
    ? phase([0, 0.25, 0.55, 1], [welcomeHeight, welcomeHeight, height, height])
    : phase([0, 0.6, 1], [welcomeHeight, 0, 0]);
  const registerFormY = destination
    ? phase([0, 0.4, 1], [welcomeHeight, welcomeHeight, 0])
    : phase([0, 0.45, 0.75, 1], [-formHeight, -formHeight, 0, 0]);
  const loginOpacity = phase([0, 0.49, 0.51, 1], [1, 1, 0, 0]);
  const registerOpacity = phase([0, 0.49, 0.51, 1], [0, 0, 1, 1]);
  const loginY = destination
    ? phase([0, 0.3, 0.6, 1], [0, 0, -welcomeHeight, -welcomeHeight])
    : phase([0, 0.3, 1], [0, -welcomeHeight, -welcomeHeight]);
  const registerY = destination
    ? phase([0, 0.7, 1], [height, height, formHeight])
    : phase([0, 0.4, 0.7, 1], [height, height, formHeight, formHeight]);

  return <SafeAreaView style={[styles.safe, { backgroundColor: c.paper }]}>
    <KeyboardAvoidingView style={styles.safe} behavior={Platform.OS === 'ios' ? 'padding' : 'height'} enabled={Platform.OS !== 'web'}>
      <View testID="auth-composition" onLayout={event => setHeight(event.nativeEvent.layout.height)} style={[styles.frame, { backgroundColor: c.paper, borderColor: c.border }]}>
        <FormLayer active={!settled} blocked={animating} opacity={loginOpacity} translateY={loginFormY} height={formHeight} backgroundColor={c.paper} testID="auth-login-form">
          <LoginForm active={!settled} onBusyChange={setLoginBusy} />
        </FormLayer>
        <FormLayer active={settled} blocked={animating} opacity={registerOpacity} translateY={registerFormY} height={formHeight} backgroundColor={c.paper} testID="auth-register-form">
          <RegisterForm active={settled} onBusyChange={setRegisterBusy} />
        </FormLayer>
        {/* Exactly one 300%-height surface ABOVE the forms, clipped by the viewport.
            At halfway it sweeps across the viewport and masks the form hand-off. */}
        <Animated.View testID="auth-panel-sweep" pointerEvents="none" aria-hidden style={[styles.sweep, {
          height: height * 3, backgroundColor: material.facingPage, borderRadius: c.paperRadius,
          transform: [{ translateY: phase([0, 1], [-height * 2.70, formHeight]) }],
        }]} />
        <Animated.View testID="auth-login-welcome" pointerEvents={!settled && !blocked ? 'auto' : 'none'} accessibilityElementsHidden={settled || animating} importantForAccessibility={settled || animating ? 'no-hide-descendants' : 'auto'} {...(Platform.OS === 'web' && (settled || animating) ? { inert: true } : {})} style={[styles.welcome, { height: welcomeHeight, transform: [{ translateY: loginY }] }]}>
          <Welcome register={false} compact={welcomeHeight < 176} disabled={blocked} onPress={() => switchTo(true)} />
        </Animated.View>
        <Animated.View testID="auth-register-welcome" pointerEvents={settled && !blocked ? 'auto' : 'none'} accessibilityElementsHidden={!settled || animating} importantForAccessibility={!settled || animating ? 'no-hide-descendants' : 'auto'} {...(Platform.OS === 'web' && (!settled || animating) ? { inert: true } : {})} style={[styles.welcome, { height: welcomeHeight, transform: [{ translateY: registerY }] }]}>
          <Welcome register compact={welcomeHeight < 176} disabled={blocked} onPress={() => switchTo(false)} />
        </Animated.View>
      </View>
    </KeyboardAvoidingView>
  </SafeAreaView>;
}

function FormLayer({ active, blocked, opacity, translateY, height, backgroundColor, testID, children }: PropsWithChildren<{ active: boolean; blocked: boolean; opacity: Animated.AnimatedInterpolation<number>; translateY: Animated.AnimatedInterpolation<number>; height: number; backgroundColor: string; testID: string }>) {
  const hidden = !active || blocked;
  return <Animated.View testID={testID} pointerEvents={hidden ? 'none' : 'auto'} accessibilityElementsHidden={hidden} importantForAccessibility={hidden ? 'no-hide-descendants' : 'auto'} {...(Platform.OS === 'web' && hidden ? { inert: true } : {})} style={[styles.formRegion, { opacity, height, backgroundColor, transform: [{ translateY }] }]}>{children}</Animated.View>;
}

function Welcome({ register, compact, disabled, onPress }: { register: boolean; compact: boolean; disabled: boolean; onPress: () => void }) {
  const [focused, setFocused] = useState(false);
  const c = useAuthColors();
  return <View style={styles.welcomeContent}>
    <View style={styles.brand}>
      <Image source={require('../../../assets/spenwise-logo.png')} resizeMode="contain" style={styles.logo} accessibilityLabel="SpendWise logo" />
      <Text style={[fontStyles.wordmarkStrong, styles.wordmark, { color: c.onButton }]}>SpendWise</Text>
    </View>
    {!compact && <Text style={[authType.label, { color: c.registrationBorder }]}>{register ? 'Đã có tài khoản' : 'Chưa có tài khoản'}</Text>}
    <Pressable accessibilityRole="button" accessibilityLabel={register ? 'Đã có tài khoản? Đăng nhập ngay' : 'Chưa có tài khoản? Đăng ký ngay'} disabled={disabled} accessibilityState={{ disabled }} onPress={onPress}
      onFocus={() => setFocused(true)} onBlur={() => setFocused(false)}
      style={({ pressed }) => [styles.switch, { backgroundColor: c.registration, borderColor: c.registrationBorder, opacity: disabled ? 0.6 : pressed ? 0.85 : 1, boxShadow: focused ? `0 0 0 2px ${c.onButton}` : 'none' }]}>
      <SymbolView name={{ ios: register ? 'arrow.right' : 'arrow.left', android: register ? 'arrow_forward' : 'arrow_back', web: register ? 'arrow_forward' : 'arrow_back' }} size={16} tintColor={c.onRegistration} />
      <Text style={[fontStyles.bold, styles.switchLabel, { color: c.onRegistration }]}>{register ? 'Đăng nhập ngay' : 'Đăng ký ngay'}</Text>
    </Pressable>
  </View>;
}

const styles = StyleSheet.create({
  safe: { flex: 1 },
  frame: { flex: 1, margin: 20, borderRadius: 15, borderWidth: 1, overflow: 'hidden' },
  sweep: { position: 'absolute', left: 0, right: 0, top: 0, zIndex: 2 },
  formRegion: { position: 'absolute', top: 0, left: 0, right: 0, zIndex: 1 },
  welcome: { position: 'absolute', top: 0, left: 0, right: 0, zIndex: 3 },
  welcomeContent: { flex: 1, paddingHorizontal: 20, paddingVertical: 12, gap: 12, justifyContent: 'center', alignItems: 'flex-start' },
  brand: { flexDirection: 'row', gap: 10, alignItems: 'center' },
  logo: { width: 32, height: 32 },
  wordmark: { fontSize: 24, lineHeight: 32, letterSpacing: -0.6 },
  switchLabel: { fontSize: 13, lineHeight: 20, letterSpacing: 0.7, textTransform: 'uppercase' },
  switch: { minHeight: 44, borderWidth: 1, borderRadius: 10, paddingHorizontal: 16, flexDirection: 'row', gap: 8, alignItems: 'center', justifyContent: 'center' },
});
