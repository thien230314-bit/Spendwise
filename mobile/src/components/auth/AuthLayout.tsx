import { forwardRef } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import type { PropsWithChildren } from 'react';
import { TeddyBearAvatar, type TeddyBearHandle } from '../TeddyBearAvatar';

interface AuthLayoutProps {
  title: string;
  subtitle?: string;
  isAnimatingS?: boolean;
  bearVariant?: 'login' | 'register';
  children: React.ReactNode;
}

export const AuthLayout = forwardRef<TeddyBearHandle, PropsWithChildren<AuthLayoutProps>>(
  function AuthLayout({ title, subtitle, isAnimatingS = false, bearVariant = 'login', children }, ref) {
    return (
      <View style={styles.container}>
        <View style={styles.header}>
          <View
            style={[
              styles.logoBadge,
              isAnimatingS ? styles.logoBadgeActive : null,
            ]}
          >
            <Text style={styles.logoText}>S</Text>
          </View>

          <View style={styles.bearWrapper}>
            <TeddyBearAvatar ref={ref} variant={bearVariant} />
          </View>

          <Text style={styles.title}>{title}</Text>
          {Boolean(subtitle) && <Text style={styles.subtitle}>{subtitle}</Text>}
        </View>

        <View style={styles.panel}>
          {children}
        </View>
      </View>
    );
  }
);

const styles = StyleSheet.create({
  container: {
    width: '100%',
    paddingVertical: 12,
  },
  header: {
    alignItems: 'center',
    marginBottom: 16,
  },
  logoBadge: {
    width: 38,
    height: 38,
    borderRadius: 12,
    backgroundColor: '#0F9D6E',
    alignItems: 'center',
    justifyContent: 'center',
    shadowColor: '#0F9D6E',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.25,
    shadowRadius: 8,
    elevation: 3,
  },
  logoBadgeActive: {
    transform: [{ scale: 1.05 }],
  },
  logoText: {
    fontSize: 20,
    fontWeight: '800',
    color: '#FFFFFF',
  },
  bearWrapper: {
    marginTop: 4,
  },
  title: {
    fontSize: 24,
    fontWeight: '800',
    color: '#0B2A22',
    marginTop: 8,
    textAlign: 'center',
  },
  subtitle: {
    fontSize: 14,
    color: '#4B6358',
    marginTop: 4,
    textAlign: 'center',
    paddingHorizontal: 16,
    lineHeight: 20,
  },
  panel: {
    backgroundColor: 'rgba(255, 255, 255, 0.85)',
    borderRadius: 20,
    padding: 20,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.6)',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.05,
    shadowRadius: 12,
    elevation: 2,
  },
});
