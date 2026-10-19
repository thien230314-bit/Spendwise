import React, { useState } from 'react';
import {
  ExternalLink,
  Layers,
  Sparkles,
  Code2,
  Eye,
  Sliders,
  ShieldCheck,
} from 'lucide-react';
import { useI18n } from '../../../i18n/I18nContext';
import { Button } from '../../../components/ui/Button';

/**
 * WalletStitchConceptB
 * Nguồn thiết kế: Google Stitch MCP (Project 5629563966578229323)
 * Định hướng phong cách: Concept B (Stitch Financial Command Center)
 * Phạm vi: Ví của tôi (Wallet) - USER-WALLET: 6 sections, 14 Feature IDs
 */
export const WalletStitchConceptB: React.FC = () => {
  const { locale } = useI18n();
  const [activeTab, setActiveTab] = useState<'interactive' | 'spec' | 'mockup'>('interactive');
  const [showDemoModal, setShowDemoModal] = useState(false);
  const [demoActionName, setDemoActionName] = useState<string>('');

  return (
    <div className="space-y-6">
      {/* Concept Header & Mode Switcher */}
      <div className="p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-xs flex flex-col lg:flex-row lg:items-center justify-between gap-4">
        <div>
          <div className="flex items-center gap-2 flex-wrap">
            <span className="px-2.5 py-0.5 rounded text-[10px] font-black uppercase tracking-wider bg-indigo-100 text-indigo-800 dark:bg-indigo-950 dark:text-indigo-300 border border-indigo-300 dark:border-indigo-800">
              STITCH MCP
            </span>
            <span className="px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300">
              1440PX DESKTOP
            </span>
            <h3 className="text-base font-extrabold text-slate-900 dark:text-white">
              {locale === 'vi-VN' ? 'Ví của tôi (Wallet) — Concept B (Stitch Financial Command Center)' : 'Ví của tôi (Wallet) — Concept B (Stitch Financial Command Center)'}
            </h3>
          </div>
          <p className="text-xs text-slate-500 dark:text-slate-400 mt-1 max-w-3xl">
            Phong cách Financial Command Center công nghệ cao với bảng màu Obsidian & Cyan Telemetry, mật độ thông tin tối ưu và các khối telemetry chuyên nghiệp.
          </p>
          <div className="mt-2 text-[11px] font-semibold text-emerald-600 dark:text-emerald-400 flex items-center gap-1.5">
            <ShieldCheck className="w-3.5 h-3.5" />
            <span>Đặc tả: USER-WALLET: 6 sections, 14 Feature IDs (Bảo toàn 100% UI layout & sections)</span>
          </div>
        </div>

        {/* View Mode Selector */}
        <div className="flex items-center gap-1 p-1 bg-slate-100 dark:bg-slate-800 rounded-xl text-xs font-bold shrink-0 self-start lg:self-auto">
          <button
            type="button"
            onClick={() => setActiveTab('interactive')}
            className={`px-3 py-1.5 rounded-lg transition-all cursor-pointer flex items-center gap-1.5 ${
              activeTab === 'interactive'
                ? 'bg-indigo-600 text-white shadow-xs'
                : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
            }`}
          >
            <Layers className="w-3.5 h-3.5" />
            <span>Interactive Frame</span>
          </button>
          <button
            type="button"
            onClick={() => setActiveTab('mockup')}
            className={`px-3 py-1.5 rounded-lg transition-all cursor-pointer flex items-center gap-1.5 ${
              activeTab === 'mockup'
                ? 'bg-indigo-600 text-white shadow-xs'
                : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
            }`}
          >
            <Eye className="w-3.5 h-3.5" />
            <span>High-Res Render</span>
          </button>
          <button
            type="button"
            onClick={() => setActiveTab('spec')}
            className={`px-3 py-1.5 rounded-lg transition-all cursor-pointer flex items-center gap-1.5 ${
              activeTab === 'spec'
                ? 'bg-indigo-600 text-white shadow-xs'
                : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
            }`}
          >
            <Code2 className="w-3.5 h-3.5" />
            <span>Stitch Spec</span>
          </button>
        </div>
      </div>

      {/* Interactive Container */}
      {activeTab === 'interactive' && (
        <div className="space-y-4">
          <div className="flex items-center justify-between text-xs text-slate-500 dark:text-slate-400 px-1">
            <span className="flex items-center gap-1.5">
              <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse" />
              Môi trường Render DOM trực tiếp từ Stitch HTML/CSS nguyên bản (1440px Canvas)
            </span>
            <a
              href="/stitch/wallet_concept_b.html"
              target="_blank"
              rel="noreferrer"
              className="text-indigo-600 dark:text-indigo-400 hover:underline flex items-center gap-1 font-semibold"
            >
              <span>Mở toàn màn hình tab mới</span>
              <ExternalLink className="w-3 h-3" />
            </a>
          </div>

          <div className="w-full rounded-2xl border border-slate-300 dark:border-slate-800 overflow-hidden bg-white dark:bg-slate-950 shadow-lg min-h-[950px] relative">
            <iframe
              src="/stitch/wallet_concept_b.html"
              title="WalletStitchConceptB"
              className="w-full h-[1200px] border-0"
            />
          </div>
        </div>
      )}

      {/* High-Res Mockup Mode */}
      {activeTab === 'mockup' && (
        <div className="space-y-4">
          <div className="flex items-center justify-between text-xs text-slate-500 dark:text-slate-400 px-1">
            <span>Bản dựng Ultra High-Res Pixel từ Stitch Engine Renderer (2560x1440 HiDPI)</span>
            <a
              href="/stitch/wallet_concept_b.png"
              target="_blank"
              rel="noreferrer"
              className="text-indigo-600 dark:text-indigo-400 hover:underline flex items-center gap-1 font-semibold"
            >
              <span>Mở ảnh gốc</span>
              <ExternalLink className="w-3 h-3" />
            </a>
          </div>
          <div className="w-full rounded-2xl border border-slate-300 dark:border-slate-800 overflow-hidden bg-slate-900 shadow-lg">
            <img
              src="/stitch/wallet_concept_b.png"
              alt="WalletStitchConceptB"
              className="w-full h-auto object-contain"
            />
          </div>
        </div>
      )}

      {/* Stitch Spec Mode */}
      {activeTab === 'spec' && (
        <div className="p-6 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 space-y-4 text-xs">
          <h4 className="text-sm font-bold text-slate-900 dark:text-white flex items-center gap-2">
            <Sliders className="w-4 h-4 text-indigo-500" />
            Đặc tả Kiến trúc Giao diện Stitch
          </h4>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-slate-600 dark:text-slate-300">
            <div className="p-4 rounded-xl bg-slate-50 dark:bg-slate-800/50 border border-slate-200 dark:border-slate-700 space-y-2">
              <div className="font-bold text-slate-900 dark:text-white">Thông số Layout</div>
              <ul className="list-disc list-inside space-y-1 text-slate-500 dark:text-slate-400">
                <li>Device Target: Desktop 1440px Grid</li>
                <li>Design Engine: Google Stitch MCP (Gemini 3.5 Flash-Lite)</li>
                <li>Design Tokens: Tương thích Tailwind CSS v3/v4 & SpendWise Palette</li>
                <li>Trạng thái: Visual Prototype Coverage 100%</li>
              </ul>
            </div>
            <div className="p-4 rounded-xl bg-slate-50 dark:bg-slate-800/50 border border-slate-200 dark:border-slate-700 space-y-2">
              <div className="font-bold text-slate-900 dark:text-white">Bảo toàn Chức năng (Blueprint)</div>
              <ul className="list-disc list-inside space-y-1 text-slate-500 dark:text-slate-400">
                <li>Scope: USER-WALLET: 6 sections, 14 Feature IDs</li>
                <li>Bảo lưu đầy đủ các nút bấm hành động chính, form dữ liệu, bộ lọc và modal triggers.</li>
                <li>Phân loại kiểm định: VISUAL_PRESENT & INTERACTION_PROTOTYPED.</li>
              </ul>
            </div>
          </div>
        </div>
      )}

      {/* Safety Demo Modal */}
      {showDemoModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-xs p-4">
          <div className="bg-white dark:bg-slate-900 rounded-2xl border border-slate-200 dark:border-slate-800 max-w-md w-full p-6 shadow-2xl space-y-4">
            <div className="flex items-center gap-2 text-indigo-600 dark:text-indigo-400 font-bold text-sm">
              <Sparkles className="w-4 h-4" />
              <span>DEV PREVIEW INTERACTION TRIGGER</span>
            </div>
            <h4 className="text-base font-extrabold text-slate-900 dark:text-white">
              Thao tác: {demoActionName}
            </h4>
            <p className="text-xs text-slate-600 dark:text-slate-400 leading-relaxed">
              Bạn đang trải nghiệm giao diện trong môi trường DEV Preview Center. Thao tác này được kích hoạt an toàn (Mock Event) nhằm xác thực phản hồi UX và luồng giao diện mà không gửi bất kỳ mutation nào tới cơ sở dữ liệu thật.
            </p>
            <div className="flex justify-end gap-2 pt-2">
              <Button
                variant="primary"
                size="sm"
                onClick={() => setShowDemoModal(false)}
              >
                Đóng thông báo
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
