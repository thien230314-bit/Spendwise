import { Redirect } from 'expo-router';
import { StyleSheet, View } from 'react-native';
import { useAuth } from '@/auth/AuthProvider';
import { useTheme } from '@/theme/useTheme';
import { HomeSkeleton } from '@/components/MobileSkeletons';

export default function Index() {
  const { status } = useAuth();
  const { colors } = useTheme();
  if (status === 'initializing') {
    return (
      <View style={[styles.loading, { backgroundColor: colors.background, paddingHorizontal: 16, paddingTop: 40 }]}>
        <HomeSkeleton />
      </View>
    );
  }
  if (status === 'authenticated') return <Redirect href="/(authenticated)/home" />;
  if (status === 'challenge-pending') return <Redirect href="/(guest)/pin" />;
  return <Redirect href="/(guest)/login" />;
}

const styles = StyleSheet.create({ loading: { alignItems: 'center', flex: 1, justifyContent: 'center' } });
