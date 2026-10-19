import React, { useState } from 'react';
import {
  GraduationCap,
  Briefcase,
  Store,
  Heart,
  UserCheck,
  HelpCircle,
  Laptop,
  BookOpen,
  Stethoscope,
  ShoppingBag,
  Wrench,
  Compass,
  TrendingUp,
  PieChart,
  Target,
  Users,
  Send,
  Calendar,
  Check,
  CheckCircle2,
  ArrowRight,
  ArrowLeft,
  RotateCcw,
  Sun,
  Moon,
  Monitor,
  Bell,
  Type,
} from 'lucide-react';
import { SpendWiseLogo } from '../brand/SpendWiseLogo';
import { Button } from '../ui/Button';
import { cn } from '../../utils/cn';

export interface OnboardingState {
  lifeStage: string;
  careerField: string;
  careerCustom: string;
  financialGoals: string[];
  theme: 'system' | 'light' | 'dark';
  language: 'vi-VN' | 'en-US';
  largeText: boolean;
  enableNotifications: boolean;
}

export const INITIAL_ONBOARDING_STATE: OnboardingState = {
  lifeStage: '',
  careerField: '',
  careerCustom: '',
  financialGoals: [],
  theme: 'system',
  language: 'vi-VN',
  largeText: false,
  enableNotifications: true,
};

// Bước 1: Một chút về bạn - 6 nhóm tổng quát
const LIFE_STAGES = [
  { id: 'student', title: 'Học sinh / Sinh viên', icon: GraduationCap },
  { id: 'employee', title: 'Đang đi làm', icon: Briefcase },
  { id: 'business_freelance', title: 'Kinh doanh / Tự do', icon: Store },
  { id: 'family', title: 'Chăm sóc gia đình', icon: Heart },
  { id: 'retired', title: 'Đã nghỉ hưu', icon: UserCheck },
  { id: 'other', title: 'Khác', icon: HelpCircle },
];

// Bước 2: Lĩnh vực học tập hoặc công việc - Phụ thuộc vào Bước 1
interface CareerOption {
  id: string;
  title: string;
  icon: React.ElementType;
  isCustom?: boolean;
}

const CAREER_MAP: Record<string, CareerOption[]> = {
  student: [
    { id: 'it', title: 'Công nghệ thông tin', icon: Laptop },
    { id: 'business_econ', title: 'Kinh tế / Kinh doanh', icon: TrendingUp },
    { id: 'healthcare', title: 'Y tế / Sức khỏe', icon: Stethoscope },
    { id: 'engineering', title: 'Kỹ thuật', icon: Wrench },
    { id: 'arts_design', title: 'Nghệ thuật / Thiết kế', icon: BookOpen },
    { id: 'other_field', title: 'Ngành khác', icon: Compass, isCustom: true },
  ],
  employee: [
    { id: 'tech_eng', title: 'Công nghệ / Kỹ thuật', icon: Laptop },
    { id: 'education', title: 'Giáo dục', icon: BookOpen },
    { id: 'healthcare', title: 'Y tế', icon: Stethoscope },
    { id: 'finance_biz', title: 'Kinh doanh / Tài chính', icon: TrendingUp },
    { id: 'service_sales', title: 'Dịch vụ / Bán hàng', icon: ShoppingBag },
    { id: 'other_field', title: 'Lĩnh vực khác', icon: Compass, isCustom: true },
  ],
  business_freelance: [
    { id: 'commerce_retail', title: 'Bán hàng / Thương mại', icon: ShoppingBag },
    { id: 'service', title: 'Dịch vụ', icon: Store },
    { id: 'creative_content', title: 'Sáng tạo nội dung / Thiết kế', icon: Laptop },
    { id: 'tech', title: 'Công nghệ', icon: Laptop },
    { id: 'other_field', title: 'Làm nghề tự do khác', icon: Compass, isCustom: true },
  ],
  family: [
    { id: 'fulltime_family', title: 'Chăm sóc gia đình toàn thời gian', icon: Heart },
    { id: 'working_family', title: 'Vừa làm việc vừa chăm sóc gia đình', icon: Briefcase },
    { id: 'other_field', title: 'Công việc khác', icon: Compass, isCustom: true },
  ],
  retired: [
    { id: 'fully_retired', title: 'Đã nghỉ làm', icon: UserCheck },
    { id: 'still_working', title: 'Vẫn đang làm việc', icon: Briefcase },
    { id: 'other_field', title: 'Công việc khác', icon: Compass, isCustom: true },
  ],
  other: [
    { id: 'studying', title: 'Đang học tập', icon: GraduationCap },
    { id: 'working', title: 'Đang làm việc', icon: Briefcase },
    { id: 'freelancing', title: 'Làm việc tự do', icon: Store },
    { id: 'job_seeking', title: 'Đang tìm việc', icon: Compass },
    { id: 'family_care', title: 'Chăm sóc gia đình', icon: Heart },
    { id: 'retired', title: 'Nghỉ hưu', icon: UserCheck },
    { id: 'other_field', title: 'Khác', icon: HelpCircle, isCustom: true },
  ],
};

// Bước 3: Đúng 6 tính năng cốt lõi người dùng quan tâm (0 đến 6 mục)
const ALL_GOALS = [
  { id: 'track_cashflow', title: 'Theo dõi thu chi', icon: TrendingUp },
  { id: 'manage_budget', title: 'Quản lý ngân sách', icon: PieChart },
  { id: 'save_goals', title: 'Tiết kiệm cho mục tiêu', icon: Target },
  { id: 'group_funds', title: 'Quản lý quỹ nhóm', icon: Users },
  { id: 'bank_transfer', title: 'Chuyển tiền', icon: Send },
  { id: 'recurring_tx', title: 'Theo dõi khoản định kỳ', icon: Calendar },
];

export interface OnboardingPreviewModalProps {
  isOpen?: boolean;
  onClose?: () => void;
  className?: string;
}

export const OnboardingPreviewModal: React.FC<OnboardingPreviewModalProps> = ({
  isOpen = true,
  onClose,
  className,
}) => {
  const [step, setStep] = useState<1 | 2 | 3 | 4 | 5>(1);
  const [maxStepReached, setMaxStepReached] = useState<number>(1);
  const [data, setData] = useState<OnboardingState>(INITIAL_ONBOARDING_STATE);
  const [direction, setDirection] = useState<'next' | 'back'>('next');
  const [transitionKey, setTransitionKey] = useState<number>(0);
  const [isTransitioning, setIsTransitioning] = useState<boolean>(false);

  // Get available career options based on selected lifeStage (or fallback to 'other')
  const availableCareers = (data.lifeStage && CAREER_MAP[data.lifeStage]) ? CAREER_MAP[data.lifeStage] : CAREER_MAP.other;

  // Handle Step 1 Life Stage Selection (if changing, reset Step 2 if invalid)
  const handleSelectLifeStage = (stageId: string) => {
    setData((prev) => {
      const newCareers = CAREER_MAP[stageId] || CAREER_MAP.other;
      const isExistingCareerValid = newCareers.some((c) => c.id === prev.careerField);
      return {
        ...prev,
        lifeStage: stageId,
        careerField: isExistingCareerValid ? prev.careerField : '',
        careerCustom: isExistingCareerValid ? prev.careerCustom : '',
      };
    });
  };

  // Handle Step 2 Career Field Selection
  const handleSelectCareerField = (careerId: string) => {
    setData((prev) => ({
      ...prev,
      careerField: careerId,
      careerCustom: careerId === 'other_field' ? prev.careerCustom : '',
    }));
  };

  // Handle Step 3 Goals Toggle
  const handleSelectGoal = (goalId: string) => {
    setData((prev) => {
      const exists = prev.financialGoals.includes(goalId);
      return {
        ...prev,
        financialGoals: exists
          ? prev.financialGoals.filter((id) => id !== goalId)
          : [...prev.financialGoals, goalId],
      };
    });
  };

  const handleSelectAllGoals = () => {
    setData((prev) => ({
      ...prev,
      financialGoals: ALL_GOALS.map((g) => g.id),
    }));
  };

  const handleDeselectAllGoals = () => {
    setData((prev) => ({
      ...prev,
      financialGoals: [],
    }));
  };

  // Stepper Transition Helpers with directional slide
  const goToStep = (targetStep: 1 | 2 | 3 | 4 | 5, navDirection?: 'next' | 'back') => {
    if (isTransitioning) return;
    const dir = navDirection || (targetStep > step ? 'next' : 'back');
    setDirection(dir);
    setTransitionKey((prev) => prev + 1);
    setStep(targetStep);
    setMaxStepReached((prev) => Math.max(prev, targetStep));
    setIsTransitioning(true);
    setTimeout(() => {
      setIsTransitioning(false);
    }, 1000);
  };

  const handleNext = () => {
    if (isTransitioning) return;
    if (step < 4) {
      goToStep((step + 1) as any, 'next');
    } else {
      goToStep(5, 'next');
    }
  };

  const handleSkip = () => {
    if (isTransitioning) return;
    if (step === 3) {
      // Skip at Step 3 clears any half-selected goals
      setData((prev) => ({ ...prev, financialGoals: [] }));
    }
    if (step < 4) {
      goToStep((step + 1) as any, 'next');
    } else {
      goToStep(5, 'next');
    }
  };

  const handleBack = () => {
    if (isTransitioning) return;
    if (step > 1) {
      goToStep((step - 1) as any, 'back');
    }
  };

  const handleReset = () => {
    setDirection('back');
    setTransitionKey((prev) => prev + 1);
    setStep(1);
    setMaxStepReached(1);
    setData(INITIAL_ONBOARDING_STATE);
  };

  // Validation for "Next" Button
  const isNextDisabled = () => {
    if (step === 1) {
      return !data.lifeStage;
    }
    if (step === 2) {
      if (!data.careerField) return true;
      if (data.careerField === 'other_field' && !data.careerCustom.trim()) return true;
      return false;
    }
    // Step 3 (0-6 goals) and Step 4 (defaults present) are always valid
    return false;
  };

  if (!isOpen) return null;

  return (
    <div
      className={cn(
        'w-full max-w-2xl mx-auto bg-white dark:bg-[#0F172A] rounded-3xl border border-slate-200/90 dark:border-slate-800 shadow-xl overflow-hidden flex flex-col',
        className
      )}
    >
      {/* Top Banner / Progress Indicator */}
      <div className="p-5 sm:p-6 border-b border-slate-100 dark:border-slate-800/80 bg-slate-50/50 dark:bg-slate-900/50 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <SpendWiseLogo size={36} variant="badge" />
          <div>
            <h2 className="text-base font-bold text-slate-900 dark:text-white leading-tight">
              SpendWise Onboarding
            </h2>
            <p className="text-xs text-slate-500 dark:text-slate-400">
              Thiết lập ban đầu dành cho bạn (Xem trước)
            </p>
          </div>
        </div>

        {/* Step Numbers: In Step 5 show completed checks + '4/4 Hoàn tất', in Steps 1..4 show 1 2 3 4 */}
        <div className="flex items-center gap-2">
          {step === 5 ? (
            <div className="flex items-center gap-1.5">
              {[1, 2, 3, 4].map((s) => (
                <button
                  key={s}
                  type="button"
                  onClick={() => !isTransitioning && goToStep(s as any, 'back')}
                  className="w-7 h-7 rounded-full text-xs font-bold transition-all flex items-center justify-center bg-emerald-100 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-300 hover:scale-105 cursor-pointer"
                  title={`Xem lại Bước ${s}`}
                >
                  <Check className="h-3.5 w-3.5 stroke-[2.5]" />
                </button>
              ))}
              <span className="ml-1.5 px-2.5 py-0.5 rounded-full text-[11px] font-bold bg-emerald-100 dark:bg-emerald-950/60 text-emerald-800 dark:text-emerald-300 border border-emerald-300 dark:border-emerald-800">
                4/4 Hoàn tất
              </span>
            </div>
          ) : (
            [1, 2, 3, 4].map((s) => {
              const isCurrent = step === s;
              const isCompleted = step > s || maxStepReached > s;
              const isClickable = s <= maxStepReached;

              return (
                <button
                  key={s}
                  type="button"
                  disabled={!isClickable || isTransitioning}
                  onClick={() => isClickable && !isTransitioning && goToStep(s as any, s > step ? 'next' : 'back')}
                  className={cn(
                    'w-7 h-7 rounded-full text-xs font-bold transition-all flex items-center justify-center',
                    isClickable ? 'cursor-pointer' : 'cursor-not-allowed opacity-50',
                    isCurrent
                      ? 'bg-emerald-600 text-white ring-2 ring-emerald-500/30 shadow-xs scale-105'
                      : isCompleted
                      ? 'bg-emerald-100 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-300 hover:scale-105'
                      : 'bg-slate-100 dark:bg-slate-800 text-slate-400'
                  )}
                  title={`Bước ${s}`}
                >
                  {s}
                </button>
              );
            })
          )}
        </div>
      </div>

      {/* Synchronized Progress Track (1000ms ease transition matching step changes) */}
      <div className="w-full h-1 bg-slate-100 dark:bg-slate-800/80 overflow-hidden relative">
        <div
          className="h-full bg-emerald-500 transition-all duration-[1000ms] ease-[cubic-bezier(0.45,0,0.55,1)]"
          style={{
            width: `${Math.min(100, Math.round(((step - 1) / 4) * 100))}%`,
          }}
        />
      </div>

      {/* Main Form Body with Directional Step Transitions */}
      <div className="p-6 sm:p-8 flex-1 overflow-x-hidden overflow-y-auto min-h-[380px] relative">
        {/* ================= BƯỚC 1: MỘT CHÚT VỀ BẠN ================= */}
        {step === 1 && (
          <div
            key={`onboarding-step-1-${transitionKey}`}
            className={cn(
              'space-y-6',
              direction === 'next' ? 'animate-onboarding-next' : 'animate-onboarding-back'
            )}
          >
            <div>
              <h3 className="text-lg sm:text-xl font-bold text-slate-900 dark:text-white">
                Một chút về bạn
              </h3>
              <p className="text-xs sm:text-sm text-slate-500 dark:text-slate-400 mt-1">
                Hiện tại, lựa chọn nào phù hợp với bạn nhất? Bạn có thể thay đổi sau.
              </p>
            </div>

            {/* 6 Clean Option Cards */}
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              {LIFE_STAGES.map((item) => {
                const Icon = item.icon;
                const isSelected = data.lifeStage === item.id;
                return (
                  <button
                    key={item.id}
                    type="button"
                    onClick={() => handleSelectLifeStage(item.id)}
                    className={cn(
                      'p-3.5 sm:p-4 rounded-2xl border text-left transition-all duration-200 flex items-center gap-3 cursor-pointer',
                      isSelected
                        ? 'border-emerald-500 bg-emerald-50/70 dark:bg-emerald-950/40 ring-2 ring-emerald-500/30 shadow-xs'
                        : 'border-slate-200/80 dark:border-slate-800 bg-white dark:bg-slate-900 hover:border-slate-300 dark:hover:border-slate-700'
                    )}
                  >
                    <div
                      className={cn(
                        'p-2.5 rounded-xl shrink-0 transition-colors duration-200',
                        isSelected
                          ? 'bg-emerald-600 text-white'
                          : 'bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300'
                      )}
                    >
                      <Icon className="h-5 w-5" />
                    </div>
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center justify-between">
                        <span className="text-sm font-semibold text-slate-900 dark:text-white truncate">
                          {item.title}
                        </span>
                        {isSelected && <Check className="h-4 w-4 text-emerald-600 dark:text-emerald-400 shrink-0 ml-1" />}
                      </div>
                    </div>
                  </button>
                );
              })}
            </div>
          </div>
        )}

        {/* ================= BƯỚC 2: LĨNH VỰC HỌC TẬP HOẶC CÔNG VIỆC ================= */}
        {step === 2 && (
          <div
            key={`onboarding-step-2-${transitionKey}`}
            className={cn(
              'space-y-6',
              direction === 'next' ? 'animate-onboarding-next' : 'animate-onboarding-back'
            )}
          >
            <div>
              <h3 className="text-lg sm:text-xl font-bold text-slate-900 dark:text-white">
                Bạn học hoặc làm việc trong lĩnh vực nào?
              </h3>
              <p className="text-xs sm:text-sm text-slate-500 dark:text-slate-400 mt-1">
                Chọn một lĩnh vực phù hợp. Bạn có thể bỏ qua nếu không muốn trả lời.
              </p>
            </div>

            {/* Conditional Career Options */}
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              {availableCareers.map((c) => {
                const Icon = c.icon;
                const isSelected = data.careerField === c.id;
                return (
                  <button
                    key={c.id}
                    type="button"
                    onClick={() => handleSelectCareerField(c.id)}
                    className={cn(
                      'p-3.5 sm:p-4 rounded-2xl border text-left transition-all duration-200 flex items-center justify-between gap-3 cursor-pointer',
                      isSelected
                        ? 'border-emerald-500 bg-emerald-50/70 dark:bg-emerald-950/40 ring-2 ring-emerald-500/30 shadow-xs'
                        : 'border-slate-200/80 dark:border-slate-800 bg-white dark:bg-slate-900 hover:border-slate-300 dark:hover:border-slate-700'
                    )}
                  >
                    <div className="flex items-center gap-3 min-w-0">
                      <div
                        className={cn(
                          'p-2.5 rounded-xl shrink-0 transition-colors duration-200',
                          isSelected
                            ? 'bg-emerald-600 text-white'
                            : 'bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300'
                        )}
                      >
                        <Icon className="h-4 w-4" />
                      </div>
                      <span className="text-sm font-semibold text-slate-800 dark:text-slate-200 truncate">
                        {c.title}
                      </span>
                    </div>
                    {isSelected && <Check className="h-4 w-4 text-emerald-600 dark:text-emerald-400 shrink-0" />}
                  </button>
                );
              })}
            </div>

            {/* Custom Job / Field Input: CHỈ HIỂN THỊ KHI CHỌN MỤC KHÁC */}
            {data.careerField === 'other_field' && (
              <div className="pt-2 animate-in fade-in-50 duration-200">
                <label className="block text-xs font-bold text-slate-700 dark:text-slate-300 mb-1.5 uppercase tracking-wider">
                  Nhập lĩnh vực cụ thể của bạn: <span className="text-rose-500">*</span>
                </label>
                <input
                  type="text"
                  autoFocus
                  value={data.careerCustom}
                  onChange={(e) => setData((prev) => ({ ...prev, careerCustom: e.target.value }))}
                  placeholder="Ví dụ: Kỹ sư xây dựng, Nhà báo, Biên dịch viên..."
                  className="w-full px-4 py-2.5 rounded-xl border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-900 text-slate-900 dark:text-white text-xs sm:text-sm focus:outline-hidden focus:ring-2 focus:ring-emerald-500"
                />
                {!data.careerCustom.trim() && (
                  <p className="text-[11px] text-amber-600 dark:text-amber-400 mt-1">
                    Vui lòng nhập nội dung để Tiếp tục, hoặc nhấn Bỏ qua bên dưới.
                  </p>
                )}
              </div>
            )}
          </div>
        )}

        {/* ================= BƯỚC 3: MỤC TIÊU SỬ DỤNG ================= */}
        {step === 3 && (
          <div
            key={`onboarding-step-3-${transitionKey}`}
            className={cn(
              'space-y-6',
              direction === 'next' ? 'animate-onboarding-next' : 'animate-onboarding-back'
            )}
          >
            <div>
              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2">
                <h3 className="text-lg sm:text-xl font-bold text-slate-900 dark:text-white">
                  Bạn muốn sử dụng SpendWise để làm gì?
                </h3>
                {/* Select All / Deselect All Controls */}
                <div className="flex items-center gap-2">
                  <button
                    type="button"
                    onClick={handleSelectAllGoals}
                    className="text-xs font-bold text-emerald-600 dark:text-emerald-400 hover:underline cursor-pointer"
                  >
                    Chọn tất cả (6)
                  </button>
                  <span className="text-slate-300 dark:text-slate-700">•</span>
                  <button
                    type="button"
                    onClick={handleDeselectAllGoals}
                    className="text-xs font-semibold text-slate-500 hover:text-slate-700 dark:hover:text-slate-300 cursor-pointer"
                  >
                    Bỏ chọn
                  </button>
                </div>
              </div>
              <p className="text-xs sm:text-sm text-slate-500 dark:text-slate-400 mt-1">
                Chọn những tính năng bạn quan tâm. Bạn có thể chọn bao nhiêu tùy thích (0 đến 6).
              </p>
            </div>

            {/* Exactly 6 Options, multiple select enabled */}
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              {ALL_GOALS.map((g) => {
                const Icon = g.icon;
                const isSelected = data.financialGoals.includes(g.id);
                return (
                  <button
                    key={g.id}
                    type="button"
                    onClick={() => handleSelectGoal(g.id)}
                    className={cn(
                      'p-4 rounded-2xl border text-left transition-all duration-200 flex items-center justify-between gap-3 cursor-pointer',
                      isSelected
                        ? 'border-emerald-500 bg-emerald-50/70 dark:bg-emerald-950/40 ring-2 ring-emerald-500/30 shadow-xs'
                        : 'border-slate-200/80 dark:border-slate-800 bg-white dark:bg-slate-900 hover:border-slate-300 dark:hover:border-slate-700'
                    )}
                  >
                    <div className="flex items-center gap-3 min-w-0">
                      <div
                        className={cn(
                          'p-2.5 rounded-xl shrink-0 transition-colors duration-200',
                          isSelected
                            ? 'bg-emerald-600 text-white'
                            : 'bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300'
                        )}
                      >
                        <Icon className="h-5 w-5" />
                      </div>
                      <span className="text-sm font-semibold text-slate-900 dark:text-white truncate">
                        {g.title}
                      </span>
                    </div>

                    <div
                      className={cn(
                        'w-5 h-5 rounded-md flex items-center justify-center border transition-colors shrink-0',
                        isSelected
                          ? 'bg-emerald-600 border-emerald-600 text-white'
                          : 'border-slate-300 dark:border-slate-700 bg-transparent'
                      )}
                    >
                      {isSelected && <Check className="h-3.5 w-3.5 stroke-[3]" />}
                    </div>
                  </button>
                );
              })}
            </div>
          </div>
        )}

        {/* ================= BƯỚC 4: THIẾT LẬP BAN ĐẦU ================= */}
        {step === 4 && (
          <div
            key={`onboarding-step-4-${transitionKey}`}
            className={cn(
              'space-y-6',
              direction === 'next' ? 'animate-onboarding-next' : 'animate-onboarding-back'
            )}
          >
            <div>
              <h3 className="text-lg sm:text-xl font-bold text-slate-900 dark:text-white">
                Thiết lập theo cách bạn thích
              </h3>
              <p className="text-xs sm:text-sm text-slate-500 dark:text-slate-400 mt-1">
                Lựa chọn giao diện, ngôn ngữ và tùy chọn tiện ích ban đầu.
              </p>
            </div>

            {/* Theme Picker: Sáng / Tối / Theo hệ thống */}
            <div className="space-y-2">
              <label className="text-xs font-bold text-slate-700 dark:text-slate-300 uppercase tracking-wider">
                Giao diện
              </label>
              <div className="grid grid-cols-3 gap-3">
                {[
                  { id: 'light', title: 'Sáng', icon: Sun },
                  { id: 'dark', title: 'Tối', icon: Moon },
                  { id: 'system', title: 'Theo hệ thống', icon: Monitor },
                ].map((item) => {
                  const Icon = item.icon;
                  const isSelected = data.theme === item.id;
                  return (
                    <button
                      key={item.id}
                      type="button"
                      onClick={() => setData((prev) => ({ ...prev, theme: item.id as any }))}
                      className={cn(
                        'p-3.5 rounded-2xl border text-center transition-all duration-200 flex flex-col items-center gap-2 cursor-pointer',
                        isSelected
                          ? 'border-emerald-500 bg-emerald-50/70 dark:bg-emerald-950/40 ring-2 ring-emerald-500/30'
                          : 'border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900'
                      )}
                    >
                      <Icon className="h-5 w-5 text-emerald-600 dark:text-emerald-400" />
                      <span className="text-xs font-bold text-slate-800 dark:text-slate-200">
                        {item.title}
                      </span>
                    </button>
                  );
                })}
              </div>
            </div>

            {/* Language Picker: Tiếng Việt / English */}
            <div className="space-y-2">
              <label className="text-xs font-bold text-slate-700 dark:text-slate-300 uppercase tracking-wider">
                Ngôn ngữ
              </label>
              <div className="grid grid-cols-2 gap-3">
                {[
                  { id: 'vi-VN', title: 'Tiếng Việt' },
                  { id: 'en-US', title: 'English' },
                ].map((item) => {
                  const isSelected = data.language === item.id;
                  return (
                    <button
                      key={item.id}
                      type="button"
                      onClick={() => setData((prev) => ({ ...prev, language: item.id as any }))}
                      className={cn(
                        'p-3.5 rounded-2xl border text-left transition-all duration-200 flex items-center justify-between cursor-pointer',
                        isSelected
                          ? 'border-emerald-500 bg-emerald-50/70 dark:bg-emerald-950/40 ring-2 ring-emerald-500/30'
                          : 'border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900'
                      )}
                    >
                      <span className="text-xs font-bold text-slate-900 dark:text-white">
                        {item.title}
                      </span>
                      {isSelected && <Check className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />}
                    </button>
                  );
                })}
              </div>
            </div>

            {/* Toggles: Chữ lớn & Nhận thông báo */}
            <div className="space-y-3 pt-2">
              <div className="flex items-center justify-between p-4 rounded-2xl border border-slate-200 dark:border-slate-800 bg-slate-50/60 dark:bg-slate-900/60">
                <div className="flex items-center gap-3">
                  <Type className="h-5 w-5 text-slate-500" />
                  <div>
                    <h4 className="text-xs font-bold text-slate-900 dark:text-white">
                      Chữ lớn
                    </h4>
                    <p className="text-[11px] text-slate-500 dark:text-slate-400">
                      Tăng cỡ chữ để dễ đọc hơn
                    </p>
                  </div>
                </div>
                <input
                  type="checkbox"
                  checked={data.largeText}
                  onChange={(e) => setData((prev) => ({ ...prev, largeText: e.target.checked }))}
                  className="h-5 w-5 rounded-md accent-emerald-600 cursor-pointer"
                />
              </div>

              <div className="flex items-center justify-between p-4 rounded-2xl border border-slate-200 dark:border-slate-800 bg-slate-50/60 dark:bg-slate-900/60">
                <div className="flex items-center gap-3">
                  <Bell className="h-5 w-5 text-slate-500" />
                  <div>
                    <h4 className="text-xs font-bold text-slate-900 dark:text-white">
                      Nhận thông báo
                    </h4>
                    <p className="text-[11px] text-slate-500 dark:text-slate-400">
                      Nhắc nhở ngân sách và biến động số dư
                    </p>
                  </div>
                </div>
                <input
                  type="checkbox"
                  checked={data.enableNotifications}
                  onChange={(e) => setData((prev) => ({ ...prev, enableNotifications: e.target.checked }))}
                  className="h-5 w-5 rounded-md accent-emerald-600 cursor-pointer"
                />
              </div>
            </div>
          </div>
        )}

        {/* ================= BƯỚC 5: COMPLETION SCREEN (SPENDWISE ĐÃ SẴN SÀNG) ================= */}
        {step === 5 && (() => {
          const selectedLifeStage = LIFE_STAGES.find((s) => s.id === data.lifeStage);
          const careerTitle = data.careerCustom
            ? data.careerCustom
            : availableCareers.find((c) => c.id === data.careerField)?.title;
          const selectedGoals = ALL_GOALS.filter((g) => data.financialGoals.includes(g.id));
          const hasAnyProfileInfo = Boolean(selectedLifeStage || careerTitle || selectedGoals.length > 0);

          return (
            <div
              key={`onboarding-step-5-${transitionKey}`}
              className="space-y-6 text-center py-2 animate-in fade-in duration-300"
            >
              {/* Hero Icon with Emerald Ring & Draw Check Animation (0-450ms) */}
              <div className="animate-completion-hero flex items-center justify-center">
                <div className="relative w-20 h-20 rounded-full bg-emerald-50 dark:bg-emerald-950/60 border border-emerald-200 dark:border-emerald-800/80 flex items-center justify-center shadow-xs">
                  {/* Subtle Ambient Glow */}
                  <div
                    className="absolute inset-0 rounded-full bg-emerald-500/15 dark:bg-emerald-500/20 blur-md pointer-events-none"
                    aria-hidden="true"
                  />
                  {/* Draw Check SVG */}
                  <svg
                    width="40"
                    height="40"
                    viewBox="0 0 48 48"
                    fill="none"
                    className="relative z-10 text-emerald-600 dark:text-emerald-400"
                  >
                    <path
                      d="M 14 25 L 21 32 L 34 17"
                      stroke="currentColor"
                      strokeWidth="4"
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      className="animate-spendwise-check"
                    />
                  </svg>
                </div>
              </div>

              {/* Completion Headline & Subtitle (250-650ms) */}
              <div className="space-y-1.5 animate-completion-title">
                <h3 className="text-2xl sm:text-3xl font-extrabold text-slate-900 dark:text-white font-display tracking-tight">
                  SpendWise đã sẵn sàng!
                </h3>
                <p className="text-xs sm:text-sm text-slate-500 dark:text-slate-400 max-w-md mx-auto">
                  Bạn có thể bắt đầu khám phá và quản lý tài chính theo cách của mình.
                </p>
              </div>

              {/* Personalized Summary Card ("Thiết lập của bạn") (450-800ms) */}
              <div className="animate-completion-summary p-5 rounded-2xl bg-slate-50/80 dark:bg-slate-900/60 border border-slate-200/80 dark:border-slate-800 text-left space-y-4 shadow-2xs">
                <div className="flex items-center justify-between border-b border-slate-200/60 dark:border-slate-800 pb-2">
                  <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                    Thiết lập của bạn
                  </span>
                  <button
                    type="button"
                    onClick={() => goToStep(1, 'back')}
                    className="text-xs font-semibold text-emerald-600 dark:text-emerald-400 hover:underline cursor-pointer"
                  >
                    Chỉnh sửa
                  </button>
                </div>

                {hasAnyProfileInfo ? (
                  <div className="space-y-3.5">
                    {/* Life Stage & Career (only if selected) */}
                    {(selectedLifeStage || careerTitle) && (
                      <div className="flex flex-wrap items-center gap-2 text-xs">
                        <span className="text-slate-500 dark:text-slate-400 font-medium">Hồ sơ:</span>
                        {selectedLifeStage && (
                          <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-xl bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-slate-800 dark:text-slate-200 font-semibold shadow-2xs">
                            <selectedLifeStage.icon className="h-3.5 w-3.5 text-emerald-600 dark:text-emerald-400" />
                            {selectedLifeStage.title}
                          </span>
                        )}
                        {careerTitle && (
                          <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-xl bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-slate-800 dark:text-slate-200 font-semibold shadow-2xs">
                            <Briefcase className="h-3.5 w-3.5 text-emerald-600 dark:text-emerald-400" />
                            {careerTitle}
                          </span>
                        )}
                      </div>
                    )}

                    {/* Financial Goals (only if selected > 0) */}
                    {selectedGoals.length > 0 && (
                      <div className="space-y-1.5">
                        <span className="text-xs text-slate-500 dark:text-slate-400 font-medium">
                          Tính năng quan tâm ({selectedGoals.length}):
                        </span>
                        <div className="flex flex-wrap gap-1.5">
                          {selectedGoals.map((g) => {
                            const Icon = g.icon;
                            return (
                              <span
                                key={g.id}
                                className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-800/80 text-emerald-800 dark:text-emerald-300 text-xs font-semibold"
                              >
                                <Icon className="h-3.5 w-3.5 text-emerald-600 dark:text-emerald-400" />
                                {g.title}
                              </span>
                            );
                          })}
                        </div>
                      </div>
                    )}

                    {/* Preferences */}
                    <div className="flex flex-wrap items-center gap-2 text-xs pt-1 border-t border-slate-200/40 dark:border-slate-800/60">
                      <span className="text-slate-500 dark:text-slate-400 font-medium">Tùy chọn:</span>
                      <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-lg bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300 font-medium text-[11px]">
                        {data.theme === 'light' ? <Sun className="h-3 w-3 text-amber-500" /> : data.theme === 'dark' ? <Moon className="h-3 w-3 text-emerald-400" /> : <Monitor className="h-3 w-3 text-slate-500" />}
                        {data.theme === 'light' ? 'Sáng' : data.theme === 'dark' ? 'Tối' : 'Hệ thống'}
                      </span>
                      <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-lg bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300 font-medium text-[11px]">
                        {data.language === 'vi-VN' ? 'Tiếng Việt' : 'English'}
                      </span>
                      {data.largeText && (
                        <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-lg bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300 font-medium text-[11px]">
                          <Type className="h-3 w-3 text-slate-500" />
                          Chữ lớn
                        </span>
                      )}
                      {data.enableNotifications && (
                        <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-lg bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300 font-medium text-[11px]">
                          <Bell className="h-3 w-3 text-slate-500" />
                          Thông báo
                        </span>
                      )}
                    </div>
                  </div>
                ) : (
                  /* Empty state when user skipped everything */
                  <div className="space-y-2 py-1">
                    <p className="text-xs text-slate-500 dark:text-slate-400">
                      Bạn có thể bổ sung sở thích và thay đổi cài đặt bất cứ lúc nào trong mục Cài đặt.
                    </p>
                    <div className="flex flex-wrap items-center gap-2 text-xs pt-1 border-t border-slate-200/40 dark:border-slate-800/60">
                      <span className="text-slate-500 dark:text-slate-400 font-medium">Tùy chọn mặc định:</span>
                      <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-lg bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300 font-medium text-[11px]">
                        {data.theme === 'light' ? 'Sáng' : data.theme === 'dark' ? 'Tối' : 'Hệ thống'}
                      </span>
                      <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-lg bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300 font-medium text-[11px]">
                        {data.language === 'vi-VN' ? 'Tiếng Việt' : 'English'}
                      </span>
                    </div>
                  </div>
                )}
              </div>

              {/* Primary & Secondary Action CTAs (600-900ms) */}
              <div className="animate-completion-actions flex flex-col sm:flex-row items-center justify-center gap-3 pt-2">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => goToStep(1, 'back')}
                  className="w-full sm:w-auto gap-1.5 text-xs font-semibold cursor-pointer"
                >
                  <RotateCcw className="h-3.5 w-3.5" />
                  <span>Xem lại lựa chọn</span>
                </Button>

                <Button
                  size="sm"
                  onClick={() => {
                    if (onClose) onClose();
                  }}
                  className="w-full sm:w-auto gap-2 text-xs font-bold bg-emerald-600 hover:bg-emerald-700 text-white shadow-md shadow-emerald-950/20 cursor-pointer"
                >
                  <span>Bắt đầu khám phá SpendWise</span>
                  <ArrowRight className="h-3.5 w-3.5" />
                </Button>
              </div>
            </div>
          );
        })()}
      </div>

      {/* Navigation Footer (Steps 1 to 4 Only) */}
      {step <= 4 && (
        <div className="p-4 sm:p-5 border-t border-slate-100 dark:border-slate-800/80 bg-slate-50/50 dark:bg-slate-900/50 flex items-center justify-between gap-3">
          <div>
            {step > 1 ? (
              <Button
                variant="outline"
                size="sm"
                disabled={isTransitioning}
                onClick={handleBack}
                className="gap-1.5 text-xs font-semibold cursor-pointer disabled:opacity-50"
              >
                <ArrowLeft className="h-3.5 w-3.5" />
                <span>Quay lại</span>
              </Button>
            ) : (
              <Button
                variant="ghost"
                size="sm"
                onClick={handleReset}
                className="gap-1.5 text-xs text-slate-500 cursor-pointer"
              >
                <RotateCcw className="h-3.5 w-3.5" />
                <span>Thiết lập lại</span>
              </Button>
            )}
          </div>

          <div className="flex items-center gap-2">
            {/* Nút Bỏ qua: Chỉ bỏ qua đúng 1 bước hiện tại */}
            <Button
              variant="ghost"
              size="sm"
              disabled={isTransitioning}
              onClick={handleSkip}
              className="text-xs text-slate-500 hover:text-slate-800 dark:hover:text-slate-200 cursor-pointer disabled:opacity-50"
            >
              <span>Bỏ qua</span>
            </Button>

            {/* Nút Tiếp tục / Hoàn tất: Disabled khi chưa thỏa validation hoặc đang chuyển bước */}
            <Button
              size="sm"
              disabled={isNextDisabled() || isTransitioning}
              onClick={handleNext}
              className="gap-1.5 text-xs font-bold bg-emerald-600 hover:bg-emerald-700 text-white cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <span>{step === 4 ? 'Hoàn tất' : 'Tiếp tục'}</span>
              <ArrowRight className="h-3.5 w-3.5" />
            </Button>
          </div>
        </div>
      )}
    </div>
  );
};

