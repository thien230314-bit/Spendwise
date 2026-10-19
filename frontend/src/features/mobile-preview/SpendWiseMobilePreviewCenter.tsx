import React, { useState, useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
import { cn } from '../../utils/cn';
import { useTheme } from '../../providers/ThemeProvider';
import { useI18n } from '../../i18n/I18nContext';
import { soundService } from '../../services/soundService';
import {
  Smartphone,
  Sun,
  Moon,
  RotateCcw,
  Layers,
  Sparkles,
  Wifi,
  WifiOff,
  Clock,
  AlertCircle,
  ShieldCheck,
  Home,
  Wallet,
  ArrowLeftRight,
  CalendarCheck,
  Grid2X2,
  Lock,
  Compass,
  Volume2,
  VolumeX,
} from 'lucide-react';
import { MobileDeviceFrame } from './MobileDeviceFrame';
import { MobileBottomTabs, MobileTab } from './MobileBottomTabs';
import { MobileAppHeader } from './screens/MobileAppHeader';
import { MobileSplashScreen } from './screens/MobileSplashScreen';
import { MobileAuthScreen } from './screens/MobileAuthScreen';
import { MobileHomeScreen } from './screens/MobileHomeScreen';
import { MobileWalletScreen } from './screens/MobileWalletScreen';
import { MobileTransferScreen } from './screens/MobileTransferScreen';
import { MobilePlanningScreen } from './screens/MobilePlanningScreen';
import { MobileMenuScreen } from './screens/MobileMenuScreen';
import { MobileFriendsScreen } from './screens/MobileFriendsScreen';
import { MobileAiChatScreen } from './screens/MobileAiChatScreen';
import { MobileBottomSheetHost } from './sheets/MobileBottomSheetHost';
import { OnboardingPreviewModal } from '../../components/onboarding/OnboardingPreviewModal';
import { SpendWiseSLoading } from '../../components/ui/SpendWiseSLoading';

export type MobileScreenRoute =
  | 'splash'
  | 'auth'
  | 'home'
  | 'concept-a'
  | 'concept-b'
  | 'concept-b-v2'
  | 'wallet'
  | 'transfer'
  | 'planning'
  | 'menu'
  | 'friends'
  | 'ai-chat';

export const SpendWiseMobilePreviewCenter: React.FC = () => {
  const { theme, resolvedTheme, setTheme } = useTheme();
  const { locale, setLocale } = useI18n();
  const [searchParams, setSearchParams] = useSearchParams();

  // URL-driven Screen: read ?screen= or default to 'auth'
  const screenFromUrl = (searchParams.get('screen') as MobileScreenRoute) || 'auth';
  const validScreens: MobileScreenRoute[] = [
    'splash', 'auth', 'home', 'concept-a', 'concept-b', 'concept-b-v2',
    'wallet', 'transfer', 'planning', 'menu', 'friends', 'ai-chat'
  ];

  const [activeScreen, setActiveScreenState] = useState<MobileScreenRoute>(
    validScreens.includes(screenFromUrl) ? screenFromUrl : 'auth'
  );

  // Sync state if URL changes (Back / Forward button or direct link)
  useEffect(() => {
    const s = searchParams.get('screen') as MobileScreenRoute;
    if (s && validScreens.includes(s) && s !== activeScreen) {
      setActiveScreenState(s);
    }
  }, [searchParams]);

  const setActiveScreen = (screen: MobileScreenRoute) => {
    setActiveScreenState(screen);
    const newParams = new URLSearchParams(searchParams);
    newParams.set('screen', screen);
    setSearchParams(newParams);
  };

  // Sound preference state
  const [soundEnabled, setSoundEnabled] = useState<boolean>(soundService.isEnabled());

  useEffect(() => {
    const handleSoundChange = (e: any) => {
      setSoundEnabled(e.detail);
    };
    window.addEventListener('spendwise_sound_preference_changed', handleSoundChange);
    return () => window.removeEventListener('spendwise_sound_preference_changed', handleSoundChange);
  }, []);

  const handleToggleSound = () => {
    const next = !soundEnabled;
    soundService.setEnabled(next);
    setSoundEnabled(next);
  };

  const [activeSheet, setActiveSheet] = useState<string | null>(null);

  // States: Normal, Skeleton, Loading, Error, Offline, Timeout
  const [statusMode, setStatusMode] = useState<
    'normal' | 'skeleton' | 'loading' | 'error' | 'offline' | 'timeout'
  >('normal');

  // Phone Viewport
  const [viewport, setViewport] = useState<'iphone15' | 'iphone14' | 'pixel'>('iphone15');

  // Onboarding Modal
  const [showOnboarding, setShowOnboarding] = useState<boolean>(false);

  // Splash key
  const [splashKey, setSplashKey] = useState<number>(1);

  const isDark = resolvedTheme === 'dark';

  const handleTabChange = (tab: MobileTab) => {
    setActiveScreen(tab);
  };

  const getScreenTitle = () => {
    switch (activeScreen) {
      case 'concept-b-v2':
        return { title: '⭐ Concept B V2 (Stitch Full Features)', subtitle: 'Bản nâng cấp hoàn thiện 100% tính năng tài chính từ Google Stitch' };
      case 'concept-a':
        return { title: 'Concept A (Stitch)', subtitle: 'Soft Premium Fintech — Thiết kế từ Google Stitch MCP' };
      case 'concept-b':
        return { title: 'Concept B (Stitch V1)', subtitle: 'Modern Financial Lifestyle — Thiết kế từ Google Stitch MCP' };
      case 'home':
        return { title: 'Trang chủ', subtitle: 'Tình hình tài chính của bạn hôm nay.' };
      case 'wallet':
        return { title: 'Ví & Giao dịch', subtitle: 'Quản lý ví SpendWise và lịch sử thu chi.' };
      case 'transfer':
        return { title: 'Chuyển tiền', subtitle: 'Từ ví SpendWise đến tài khoản người nhận.' };
      case 'planning':
        return { title: 'Kế hoạch tài chính', subtitle: 'Ngân sách, mục tiêu và giao dịch định kỳ.' };
      case 'menu':
        return { title: 'Tiện ích', subtitle: 'Kết nối, hỗ trợ và quản lý tài khoản.' };
      case 'friends':
        return { title: 'Bạn bè & Tìm kiếm', subtitle: 'Tìm bạn bè qua SĐT, SpendId và kết nối chia tiền.' };
      case 'ai-chat':
        return { title: 'Trợ lý AI SpendWise', subtitle: 'Tư vấn thông minh và phân tích chi tiêu cá nhân.' };
      default:
        return { title: 'SpendWise Mobile', subtitle: '' };
    }
  };

  const headerInfo = getScreenTitle();

  return (
    <div className="min-h-screen bg-[#F1F5F9] dark:bg-[#070B14] text-slate-900 dark:text-slate-100 flex flex-col font-sans transition-colors duration-200">
      {/* Top Navbar */}
      <header className="sticky top-0 z-40 bg-white/90 dark:bg-slate-900/90 backdrop-blur-md border-b border-slate-200 dark:border-slate-800 px-4 sm:px-8 py-3 flex items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 rounded-xl bg-emerald-600 text-white flex items-center justify-center font-bold text-sm shadow-xs">
            <Smartphone className="w-4 h-4" />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-sm font-bold text-slate-900 dark:text-white leading-tight">
                SpendWise Mobile Preview Center
              </h1>
              <span className="px-2 py-0.5 rounded-full bg-emerald-100 dark:bg-emerald-950/60 text-emerald-800 dark:text-emerald-300 font-mono text-[10px] font-bold border border-emerald-300 dark:border-emerald-800">
                Mobile-Only App
              </span>
            </div>
            <p className="text-[11px] text-slate-400">
              Trải nghiệm ứng dụng di động SpendWise chính thức cho User.
            </p>
          </div>
        </div>

        {/* Global Controls */}
        <div className="flex items-center gap-2 flex-wrap">
          <button
            type="button"
            onClick={() => setShowOnboarding(true)}
            className="px-3 py-1.5 rounded-xl border border-indigo-200 dark:border-indigo-800 bg-indigo-50 dark:bg-indigo-950/40 text-indigo-700 dark:text-indigo-300 text-xs font-bold transition-all cursor-pointer flex items-center gap-1.5"
          >
            <Compass className="w-3.5 h-3.5" />
            <span>Onboarding 4 Bước</span>
          </button>

          <button
            type="button"
            onClick={() => setTheme(isDark ? 'light' : 'dark')}
            className="p-2 rounded-xl border border-slate-200 dark:border-slate-800 hover:bg-slate-100 dark:hover:bg-slate-800 text-xs font-semibold cursor-pointer"
            title="Đổi Light/Dark"
          >
            {isDark ? <Sun className="w-4 h-4 text-amber-400" /> : <Moon className="w-4 h-4 text-slate-600" />}
          </button>

          <button
            type="button"
            onClick={handleToggleSound}
            className={cn(
              "px-3 py-1.5 rounded-xl border text-xs font-semibold cursor-pointer flex items-center gap-1.5 transition-all",
              soundEnabled
                ? "border-emerald-500 bg-emerald-50 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-300 font-bold"
                : "border-slate-200 dark:border-slate-800 text-slate-500 hover:bg-slate-100 dark:hover:bg-slate-800"
            )}
            title="Bật/Tắt âm thanh thao tác (Haptic Audio Feedback)"
          >
            {soundEnabled ? <Volume2 className="w-3.5 h-3.5 text-emerald-600 dark:text-emerald-400" /> : <VolumeX className="w-3.5 h-3.5 text-slate-400" />}
            <span>{soundEnabled ? 'Âm thanh: BẬT' : 'Âm thanh: TẮT'}</span>
          </button>

          <button
            type="button"
            onClick={() => setLocale(locale === 'vi-VN' ? 'en-US' : 'vi-VN')}
            className="px-2.5 py-1.5 rounded-xl border border-slate-200 dark:border-slate-800 text-xs font-bold font-mono cursor-pointer"
          >
            {locale === 'vi-VN' ? 'EN' : 'VI'}
          </button>
        </div>
      </header>

      {/* Main Preview Studio Body */}
      <div className="flex-1 max-w-7xl w-full mx-auto p-4 sm:p-6 grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        {/* LEFT COLUMN: SCREEN SELECTOR MENU (3 cols) */}
        <aside className="lg:col-span-3 space-y-4">
          <div className="p-4 rounded-3xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-sm space-y-3">
            <span className="text-xs font-bold uppercase tracking-wider text-slate-400 block px-1">
              MÀN HÌNH ỨNG DỤNG MOBILE
            </span>

            {/* Google Stitch Concepts */}
            <div className="space-y-1">
              <span className="text-[10px] font-bold text-emerald-600 dark:text-emerald-400 uppercase px-2 flex items-center justify-between">
                <span>Google Stitch Design (Mới)</span>
                <span className="px-1.5 py-0.5 rounded bg-emerald-100 dark:bg-emerald-950/80 text-[9px] font-bold">2 Concepts</span>
              </span>
              {[
                { id: 'concept-b-v2', label: '🔥 Concept B V2 (Đầy đủ tính năng)', icon: Sparkles },
                { id: 'concept-b', label: 'Concept B V1 (Modern Lifestyle)', icon: Layers },
                { id: 'concept-a', label: 'Concept A (Soft Premium)', icon: Sparkles },
              ].map((item) => {
                const Icon = item.icon;
                const isActive = activeScreen === item.id;
                return (
                  <button
                    key={item.id}
                    type="button"
                    onClick={() => setActiveScreen(item.id as any)}
                    className={cn(
                      'w-full p-2.5 rounded-2xl flex items-center gap-2.5 text-xs font-semibold transition-all text-left cursor-pointer border',
                      isActive
                        ? 'bg-emerald-600 text-white shadow-xs font-bold border-emerald-500'
                        : 'text-emerald-700 dark:text-emerald-400 bg-emerald-50/50 dark:bg-emerald-950/20 border-emerald-200/50 dark:border-emerald-800/30 hover:bg-emerald-100/60'
                    )}
                  >
                    <Icon className="w-4 h-4 shrink-0 text-emerald-500" />
                    <span>{item.label}</span>
                  </button>
                );
              })}
            </div>

            {/* Launch & Auth screens */}
            <div className="space-y-1 pt-2 border-t border-slate-100 dark:border-slate-800">
              <span className="text-[10px] font-bold text-slate-400 uppercase px-2 block">
                Khởi động & Xác thực
              </span>
              {[
                { id: 'splash', label: '1. Splash & Logo Intro', icon: Sparkles },
                { id: 'auth', label: '2. Đăng nhập / Đăng ký', icon: Lock },
              ].map((item) => {
                const Icon = item.icon;
                const isActive = activeScreen === item.id;
                return (
                  <button
                    key={item.id}
                    type="button"
                    onClick={() => setActiveScreen(item.id as any)}
                    className={cn(
                      'w-full p-2.5 rounded-2xl flex items-center gap-2.5 text-xs font-semibold transition-all text-left cursor-pointer',
                      isActive
                        ? 'bg-emerald-600 text-white shadow-xs font-bold'
                        : 'text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800'
                    )}
                  >
                    <Icon className="w-4 h-4 shrink-0" />
                    <span>{item.label}</span>
                  </button>
                );
              })}
            </div>

            {/* Main Tab screens */}
            <div className="space-y-1 pt-2 border-t border-slate-100 dark:border-slate-800">
              <span className="text-[10px] font-bold text-slate-400 uppercase px-2 block">
                5 Tabs Chính (Authenticated)
              </span>
              {[
                { id: 'home', label: 'Trang chủ (Home)', icon: Home },
                { id: 'wallet', label: 'Ví & Giao dịch', icon: Wallet },
                { id: 'transfer', label: 'Chuyển tiền (Transfer)', icon: ArrowLeftRight },
                { id: 'planning', label: 'Kế hoạch (Planning)', icon: CalendarCheck },
                { id: 'menu', label: 'Tiện ích (Menu)', icon: Grid2X2 },
              ].map((item) => {
                const Icon = item.icon;
                const isActive = activeScreen === item.id;
                return (
                  <button
                    key={item.id}
                    type="button"
                    onClick={() => setActiveScreen(item.id as any)}
                    className={cn(
                      'w-full p-2.5 rounded-2xl flex items-center gap-2.5 text-xs font-semibold transition-all text-left cursor-pointer',
                      isActive
                        ? 'bg-emerald-600 text-white shadow-xs font-bold'
                        : 'text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800'
                    )}
                  >
                    <Icon className="w-4 h-4 shrink-0" />
                    <span>{item.label}</span>
                  </button>
                );
              })}
            </div>

            {/* Feature Sub-Screens */}
            <div className="space-y-1 pt-2 border-t border-slate-100 dark:border-slate-800">
              <span className="text-[10px] font-bold text-emerald-600 dark:text-emerald-400 uppercase px-2 block">
                Màn hình tính năng mới
              </span>
              {[
                { id: 'friends', label: '3. Bạn bè & Tìm kiếm', icon: Compass },
                { id: 'ai-chat', label: '4. Trợ lý AI (Mở từ FAB)', icon: Sparkles },
              ].map((item) => {
                const Icon = item.icon;
                const isActive = activeScreen === item.id;
                return (
                  <button
                    key={item.id}
                    type="button"
                    onClick={() => setActiveScreen(item.id as any)}
                    className={cn(
                      'w-full p-2.5 rounded-2xl flex items-center gap-2.5 text-xs font-semibold transition-all text-left cursor-pointer',
                      isActive
                        ? 'bg-emerald-600 text-white shadow-xs font-bold'
                        : 'text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800'
                    )}
                  >
                    <Icon className="w-4 h-4 shrink-0" />
                    <span>{item.label}</span>
                  </button>
                );
              })}
            </div>
          </div>

          {/* Quick Bottom Sheet Inspector */}
          <div className="p-4 rounded-3xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-sm space-y-2.5">
            <span className="text-xs font-bold uppercase tracking-wider text-slate-400 block px-1">
              XEM BOTTOM SHEET / MODAL
            </span>
            <div className="grid grid-cols-1 gap-1.5">
              {[
                { id: 'pin-challenge', label: 'Xác thực Mã PIN' },
                { id: 'add-transaction', label: 'Thêm giao dịch mới' },
                { id: 'wallet-switcher', label: 'Đổi ví nguồn' },
                { id: 'link-bank', label: 'Liên kết ngân hàng' },
                { id: 'notifications', label: 'Hộp thư thông báo' },
              ].map((sheet) => (
                <button
                  key={sheet.id}
                  type="button"
                  onClick={() => setActiveSheet(sheet.id)}
                  className="w-full py-2 px-3 rounded-xl border border-slate-200 dark:border-slate-800 text-left text-xs font-medium hover:bg-slate-50 dark:hover:bg-slate-800 transition cursor-pointer flex items-center justify-between"
                >
                  <span>{sheet.label}</span>
                  <span className="text-[10px] text-emerald-600 dark:text-emerald-400 font-bold">Mở ▾</span>
                </button>
              ))}
            </div>
          </div>
        </aside>

        {/* CENTER COLUMN: REAL PHONE DISPLAY FRAME (6 cols) */}
        <main className="lg:col-span-6 flex flex-col items-center">
          <MobileDeviceFrame theme={isDark ? 'dark' : 'light'} viewport={viewport}>
            {/* Offline banner state */}
            {statusMode === 'offline' && (
              <div className="bg-amber-500 text-white text-[11px] font-bold px-4 py-1.5 text-center flex items-center justify-center gap-1.5 shrink-0 animate-in slide-in-from-top">
                <WifiOff className="w-3.5 h-3.5" />
                <span>Không có kết nối mạng (Offline)</span>
              </div>
            )}

            {/* Timeout banner state */}
            {statusMode === 'timeout' && (
              <div className="bg-rose-500 text-white text-[11px] font-bold px-4 py-1.5 text-center flex items-center justify-center gap-1.5 shrink-0 animate-in slide-in-from-top">
                <Clock className="w-3.5 h-3.5" />
                <span>Quá thời gian chờ phản hồi từ máy chủ</span>
              </div>
            )}

            {/* Top Screen Header if inside authenticated secondary tabs */}
            {!['splash', 'auth', 'home', 'concept-a', 'concept-b', 'concept-b-v2'].includes(activeScreen) && (
              <MobileAppHeader
                title={headerInfo.title}
                subtitle={headerInfo.subtitle}
                theme={isDark ? 'dark' : 'light'}
              />
            )}

            {/* Active Screen Render */}
            <div className={cn(
              "flex-1 flex flex-col no-scrollbar relative",
              activeScreen === 'auth' ? "overflow-y-auto" : "overflow-y-auto"
            )}>
              {activeScreen === 'splash' && (
                <MobileSplashScreen
                  key={splashKey}
                  theme={isDark ? 'dark' : 'light'}
                  onReplay={() => setSplashKey((k) => k + 1)}
                  onProceed={() => setActiveScreen('home')}
                />
              )}

              {activeScreen === 'auth' && (
                <MobileAuthScreen
                  theme={isDark ? 'dark' : 'light'}
                  onLoginSuccess={() => setActiveScreen('home')}
                  statusMode={statusMode === 'skeleton' ? 'normal' : (statusMode as any)}
                />
              )}

              {activeScreen === 'concept-b-v2' && (
                <div className="flex-1 flex flex-col overflow-y-auto bg-[#F4F7F6] select-none">
                  <div className="p-3 bg-emerald-900 text-white text-[11px] font-semibold text-center flex items-center justify-between shadow-xs sticky top-0 z-10 backdrop-blur-md">
                    <span className="flex items-center gap-1.5">
                      <Sparkles className="w-3.5 h-3.5 text-emerald-400" />
                      <span>Google Stitch: Concept B V2</span>
                    </span>
                    <span className="text-[10px] bg-emerald-700/80 px-2 py-0.5 rounded-full font-bold">100% Đầy đủ tính năng</span>
                  </div>
                  <img
                    src="/stitch_concept_b_v2.png"
                    alt="SpendWise Concept B V2 - 100% Feature Complete (Google Stitch)"
                    className="w-full h-auto block"
                  />
                </div>
              )}

              {activeScreen === 'concept-a' && (
                <div className="flex-1 flex flex-col overflow-y-auto bg-[#FAF8FF] select-none">
                  <div className="p-3 bg-emerald-700 text-white text-[11px] font-semibold text-center flex items-center justify-between shadow-xs">
                    <span>⭐ Google Stitch: Concept A</span>
                    <span className="text-[10px] bg-emerald-800 px-2 py-0.5 rounded-full">Soft Premium</span>
                  </div>
                  <img
                    src="/stitch_concept_a.png"
                    alt="SpendWise Concept A - Soft Premium Fintech (Google Stitch)"
                    className="w-full h-auto block"
                  />
                </div>
              )}

              {activeScreen === 'concept-b' && (
                <div className="flex-1 flex flex-col overflow-y-auto bg-[#F4F7F6] select-none">
                  <div className="p-3 bg-slate-900 text-white text-[11px] font-semibold text-center flex items-center justify-between shadow-xs">
                    <span>⭐ Google Stitch: Concept B</span>
                    <span className="text-[10px] bg-slate-800 px-2 py-0.5 rounded-full">Modern Lifestyle</span>
                  </div>
                  <img
                    src="/stitch_concept_b.png"
                    alt="SpendWise Concept B - Modern Financial Lifestyle (Google Stitch)"
                    className="w-full h-auto block"
                  />
                </div>
              )}

              {activeScreen === 'home' && (
                <MobileHomeScreen
                  theme={isDark ? 'dark' : 'light'}
                  onNavigateTab={(tab) => setActiveScreen(tab)}
                  onOpenSheet={(id) => setActiveSheet(id)}
                  statusMode={statusMode as any}
                />
              )}

              {activeScreen === 'wallet' && (
                <MobileWalletScreen
                  theme={isDark ? 'dark' : 'light'}
                  onOpenSheet={(id) => setActiveSheet(id)}
                  statusMode={statusMode as any}
                />
              )}

              {activeScreen === 'transfer' && (
                <MobileTransferScreen
                  theme={isDark ? 'dark' : 'light'}
                  onOpenSheet={(id) => setActiveSheet(id)}
                  statusMode={statusMode as any}
                />
              )}

              {activeScreen === 'planning' && (
                <MobilePlanningScreen
                  theme={isDark ? 'dark' : 'light'}
                  onOpenSheet={(id) => setActiveSheet(id)}
                  statusMode={statusMode as any}
                />
              )}

              {activeScreen === 'menu' && (
                <MobileMenuScreen
                  theme={isDark ? 'dark' : 'light'}
                  onOpenSheet={(id) => setActiveSheet(id)}
                  onNavigateScreen={(scr) => setActiveScreen(scr as any)}
                  onLogout={() => setActiveScreen('auth')}
                />
              )}

              {activeScreen === 'friends' && (
                <MobileFriendsScreen
                  theme={isDark ? 'dark' : 'light'}
                  onBack={() => setActiveScreen('menu')}
                  onOpenSheet={(id) => setActiveSheet(id)}
                />
              )}

              {activeScreen === 'ai-chat' && (
                <MobileAiChatScreen
                  onBack={() => setActiveScreen('menu')}
                  onOpenSheet={(id) => setActiveSheet(id)}
                />
              )}

              {/* Bottom Sheet Modal Host */}
              <MobileBottomSheetHost
                sheetId={activeSheet}
                onClose={() => setActiveSheet(null)}
                theme={isDark ? 'dark' : 'light'}
              />
            </div>

            {/* Global Floating AI Assistant (FAB) - Fixed over screen and above bottom tabs */}
            {!['splash', 'auth', 'ai-chat'].includes(activeScreen) && (
              <div
                style={{ position: 'absolute', right: 16, bottom: 80, zIndex: 99 }}
                className="pointer-events-auto"
              >
                <button
                  type="button"
                  onClick={() => setActiveScreen('ai-chat')}
                  aria-label="Trợ lý AI SpendWise"
                  title="Mở Trợ lý AI SpendWise"
                  className="w-12 h-12 rounded-full bg-emerald-600 hover:bg-emerald-700 text-white flex items-center justify-center shadow-2xl transition-all transform active:scale-95 cursor-pointer ring-4 ring-emerald-500/20"
                >
                  <Sparkles className="w-5 h-5 text-white animate-pulse" />
                </button>
              </div>
            )}

            {/* Bottom Tabs Bar if inside authenticated tabs */}
            {!['splash', 'auth'].includes(activeScreen) && (
              <MobileBottomTabs
                activeTab={activeScreen as MobileTab}
                onTabChange={handleTabChange}
                theme={isDark ? 'dark' : 'light'}
              />
            )}
          </MobileDeviceFrame>
        </main>

        {/* RIGHT COLUMN: PREVIEW CONTROLS & STATE SWITCHER (3 cols) */}
        <aside className="lg:col-span-3 space-y-4">
          {/* Trạng thái UI Lifecycle */}
          <div className="p-4 rounded-3xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-sm space-y-3">
            <span className="text-xs font-bold uppercase tracking-wider text-slate-400 block px-1">
              TRẠNG THÁI GIAO DIỆN (UI STATES)
            </span>

            <div className="space-y-1.5">
              {[
                { id: 'normal', label: '1. Bình thường (Normal Data)' },
                { id: 'skeleton', label: '2. Skeleton Loading' },
                { id: 'loading', label: '3. Đang xử lý (S-Loading Pending)' },
                { id: 'error', label: '4. Thất bại (Error State)' },
                { id: 'offline', label: '5. Mất mạng (Offline Banner)' },
                { id: 'timeout', label: '6. Quá thời gian (Timeout)' },
              ].map((st) => (
                <button
                  key={st.id}
                  type="button"
                  onClick={() => setStatusMode(st.id as any)}
                  className={cn(
                    'w-full py-2 px-3 rounded-xl border text-left text-xs font-semibold transition-all cursor-pointer',
                    statusMode === st.id
                      ? 'border-emerald-500 bg-emerald-50 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-300 font-bold'
                      : 'border-slate-200 dark:border-slate-800 text-slate-600 dark:text-slate-400 hover:bg-slate-50 dark:hover:bg-slate-800'
                  )}
                >
                  {st.label}
                </button>
              ))}
            </div>
          </div>

          {/* Thiết bị Viewport */}
          <div className="p-4 rounded-3xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-sm space-y-3">
            <span className="text-xs font-bold uppercase tracking-wider text-slate-400 block px-1">
              KÍCH THƯỚC ĐIỆN THOẠI
            </span>
            <div className="flex gap-1.5">
              {[
                { id: 'iphone15', label: '393 × 852' },
                { id: 'iphone14', label: '375 × 812' },
                { id: 'pixel', label: '412 × 892' },
              ].map((vp) => (
                <button
                  key={vp.id}
                  type="button"
                  onClick={() => setViewport(vp.id as any)}
                  className={cn(
                    'flex-1 py-1.5 rounded-xl border text-center text-[10px] font-mono font-semibold transition-all cursor-pointer',
                    viewport === vp.id
                      ? 'border-emerald-500 bg-emerald-50 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-300 font-bold'
                      : 'border-slate-200 dark:border-slate-800 text-slate-500 hover:bg-slate-100 dark:hover:bg-slate-800'
                  )}
                >
                  {vp.label}
                </button>
              ))}
            </div>
          </div>

          {/* S-Loading Showcase preview */}
          <div className="p-4 rounded-3xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-sm space-y-2.5">
            <span className="text-xs font-bold uppercase tracking-wider text-slate-400 block px-1">
              S-LOADING BRANDED ANIMATION
            </span>
            <div className="p-3 rounded-2xl bg-slate-50 dark:bg-slate-800/50 flex flex-col items-center justify-center">
              <SpendWiseSLoading status="pending" size="sm" isPaused={false} />
              <span className="text-[11px] text-slate-400 font-mono mt-2">S-Loading Blue S</span>
            </div>
          </div>
        </aside>
      </div>

      {/* Onboarding 4 Steps Modal */}
      <OnboardingPreviewModal
        isOpen={showOnboarding}
        onClose={() => setShowOnboarding(false)}
      />
    </div>
  );
};
