import { useState } from 'react';
import {
  KeyboardAvoidingView,
  Platform,
  ScrollView,
  StyleSheet,
  View,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { router } from 'expo-router';
import LoginForm from './LoginForm';
import RegisterForm from './RegisterForm';

export function AuthComposition({ register = false }: { register: boolean }) {
  const [isRegister, setIsRegister] = useState(register);
  const [busy, setBusy] = useState(false);

  const switchTo = (next: boolean) => {
    if (busy) return;
    setIsRegister(next);
    if (next) {
      router.push('/(guest)/register');
    } else {
      router.replace('/(guest)/login');
    }
  };

  return (
    <SafeAreaView style={styles.safe}>
      <KeyboardAvoidingView
        style={styles.keyboardView}
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      >
        <ScrollView
          contentContainerStyle={styles.scrollContent}
          keyboardShouldPersistTaps="handled"
          showsVerticalScrollIndicator={false}
        >
          <View style={styles.cardContainer}>
            {isRegister ? (
              <RegisterForm
                active={isRegister}
                onBusyChange={setBusy}
                onSwitchToLogin={() => switchTo(false)}
              />
            ) : (
              <LoginForm
                active={!isRegister}
                onBusyChange={setBusy}
                onSwitchToRegister={() => switchTo(true)}
              />
            )}
          </View>
        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: {
    flex: 1,
    backgroundColor: '#E6F2EC',
  },
  keyboardView: {
    flex: 1,
  },
  scrollContent: {
    flexGrow: 1,
    justifyContent: 'center',
    alignItems: 'center',
    paddingVertical: 24,
    paddingHorizontal: 16,
  },
  cardContainer: {
    width: '100%',
    maxWidth: 440,
    backgroundColor: 'transparent',
  },
});
