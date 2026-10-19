import { Slot, usePathname } from 'expo-router';
import { View } from 'react-native';
import { AuthComposition } from '@/components/auth/AuthComposition';

export default function GuestLayout() {
  const path = usePathname();
  const paired = path === '/login' || path === '/register';
  return <View style={{ flex: 1 }}>
    <View style={paired ? { display: 'none' } : { flex: 1 }}><Slot /></View>
    {paired && <AuthComposition register={path === '/register'} />}
  </View>;
}
