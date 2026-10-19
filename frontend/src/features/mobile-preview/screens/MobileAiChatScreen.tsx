import React, { useState } from 'react';
import { Send, Bot, User, Sparkles, AlertCircle, HelpCircle, ArrowLeft, RefreshCw, MessageSquare } from 'lucide-react';

interface ChatMessage {
  id: string;
  sender: 'user' | 'assistant';
  content: string;
  timestamp: string;
  suggestedActions?: string[];
}

interface MobileAiChatScreenProps {
  onBack?: () => void;
  onOpenSheet?: (sheetId: string) => void;
}

const PRESET_PROMPTS = [
  'Tháng này tôi đã chi bao nhiêu?',
  'Phân tích chi tiêu tuần qua',
  'Làm sao tiết kiệm thêm 2 triệu?',
  'Tôi có bị vượt ngân sách ăn uống không?'
];

export const MobileAiChatScreen: React.FC<MobileAiChatScreenProps> = ({ onBack, onOpenSheet }) => {
  const [messages, setMessages] = useState<ChatMessage[]>([
    {
      id: 'm1',
      sender: 'assistant',
      content: 'Chào bạn! Mình là Trợ lý Tài chính SpendWise AI. Bạn cần phân tích chi tiêu, tư vấn ngân sách hay tìm kiếm giao dịch nào hôm nay?',
      timestamp: '10:00',
      suggestedActions: ['Phân tích chi tiêu', 'Cảnh báo ngân sách']
    }
  ]);
  const [input, setInput] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const handleSend = (textToSend?: string) => {
    const text = (textToSend || input).trim();
    if (!text) return;

    const userMsg: ChatMessage = {
      id: `u-${Date.now()}`,
      sender: 'user',
      content: text,
      timestamp: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
    };

    setMessages(prev => [...prev, userMsg]);
    setInput('');
    setIsLoading(true);

    setTimeout(() => {
      let reply = 'Dựa trên dữ liệu giao dịch tháng này, tổng chi tiêu của bạn là 14.250.000 ₫. Bạn đã đạt 71% ngân sách tháng.';
      if (text.toLowerCase().includes('ăn uống') || text.toLowerCase().includes('budget')) {
        reply = 'Hạng mục Ăn uống hiện tiêu tốn 4.200.000 ₫ / 5.000.000 ₫ (84% ngân sách). Bạn nên hạn chế ăn ngoài trong 5 ngày tới!';
      } else if (text.toLowerCase().includes('tiết kiệm')) {
        reply = 'Để tiết kiệm thêm 2.000.000 ₫, bạn có thể cắt giảm 15% chi phí cà phê & giải trí, đồng thời chuyển khoản tự động vào Hũ mục tiêu.';
      }

      const botMsg: ChatMessage = {
        id: `a-${Date.now()}`,
        sender: 'assistant',
        content: reply,
        timestamp: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
        suggestedActions: ['Xem báo cáo ngân sách', 'Mở Hũ tích lũy']
      };

      setMessages(prev => [...prev, botMsg]);
      setIsLoading(false);
    }, 700);
  };

  return (
    <div className="flex flex-col h-full bg-[#f8fafc] dark:bg-[#090d16] text-[#0f172a] dark:text-[#f8fafc]">
      {/* Header */}
      <div className="flex items-center justify-between px-4 py-3 bg-white/90 dark:bg-[#111827]/90 backdrop-blur border-b border-gray-100 dark:border-gray-800 sticky top-0 z-10">
        <div className="flex items-center gap-2">
          {onBack && (
            <button 
              onClick={onBack}
              className="p-1.5 -ml-1.5 rounded-full hover:bg-gray-100 dark:hover:bg-gray-800 text-gray-600 dark:text-gray-300 transition"
              title="Quay lại"
            >
              <ArrowLeft size={18} />
            </button>
          )}
          <div className="w-8 h-8 rounded-full bg-gradient-to-tr from-emerald-500 to-teal-400 flex items-center justify-center text-white shadow-sm shadow-emerald-500/20">
            <Bot size={18} />
          </div>
          <div>
            <div className="flex items-center gap-1.5">
              <span className="font-semibold text-sm">Trợ lý AI SpendWise</span>
              <span className="inline-block w-2 h-2 rounded-full bg-emerald-500 animate-pulse" />
            </div>
            <p className="text-[11px] text-gray-500 dark:text-gray-400">Phân tích & Quản lý tài chính</p>
          </div>
        </div>

        <button 
          onClick={() => setMessages([messages[0]])}
          className="p-2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 transition"
          title="Làm mới cuộc trò chuyện"
        >
          <RefreshCw size={16} />
        </button>
      </div>

      {/* Message List */}
      <div className="flex-1 overflow-y-auto p-4 space-y-3.5">
        {messages.map(msg => (
          <div
            key={msg.id}
            className={`flex flex-col ${msg.sender === 'user' ? 'items-end' : 'items-start'}`}
          >
            <div className="flex items-end gap-2 max-w-[85%]">
              {msg.sender === 'assistant' && (
                <div className="w-6 h-6 rounded-full bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 flex items-center justify-center shrink-0 mb-1">
                  <Sparkles size={13} />
                </div>
              )}
              <div
                className={`px-3.5 py-2.5 rounded-2xl text-[13px] leading-relaxed shadow-sm ${
                  msg.sender === 'user'
                    ? 'bg-emerald-600 text-white rounded-br-xs'
                    : 'bg-white dark:bg-[#1a2234] border border-gray-100 dark:border-gray-800 text-gray-800 dark:text-gray-100 rounded-bl-xs'
                }`}
              >
                {msg.content}
              </div>
            </div>
            <span className="text-[10px] text-gray-400 mt-1 px-1">{msg.timestamp}</span>

            {/* Quick action chips if available */}
            {msg.suggestedActions && msg.suggestedActions.length > 0 && (
              <div className="flex flex-wrap gap-1.5 mt-2 ml-8">
                {msg.suggestedActions.map((action, idx) => (
                  <button
                    key={idx}
                    onClick={() => handleSend(action)}
                    className="text-[11px] px-2.5 py-1 bg-emerald-50 dark:bg-emerald-950/40 text-emerald-700 dark:text-emerald-300 border border-emerald-200/60 dark:border-emerald-800/60 rounded-full hover:bg-emerald-100 transition"
                  >
                    ✨ {action}
                  </button>
                ))}
              </div>
            )}
          </div>
        ))}

        {isLoading && (
          <div className="flex items-center gap-2 text-gray-400 text-xs py-2">
            <div className="w-6 h-6 rounded-full bg-emerald-500/10 text-emerald-600 flex items-center justify-center">
              <Bot size={13} />
            </div>
            <div className="flex gap-1 py-1 px-2.5 bg-white dark:bg-[#1a2234] rounded-full border border-gray-100 dark:border-gray-800 shadow-sm">
              <span className="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-bounce" style={{ animationDelay: '0ms' }} />
              <span className="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-bounce" style={{ animationDelay: '150ms' }} />
              <span className="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-bounce" style={{ animationDelay: '300ms' }} />
            </div>
          </div>
        )}
      </div>

      {/* Suggested Prompts Drawer */}
      <div className="px-3 pt-2 pb-1 overflow-x-auto scrollbar-none flex gap-1.5 bg-gray-50/50 dark:bg-gray-900/40 border-t border-gray-100/80 dark:border-gray-800/80">
        {PRESET_PROMPTS.map((prompt, index) => (
          <button
            key={index}
            onClick={() => handleSend(prompt)}
            className="shrink-0 text-[11px] px-2.5 py-1 bg-white dark:bg-gray-800 hover:bg-gray-100 text-gray-600 dark:text-gray-300 rounded-lg border border-gray-200 dark:border-gray-700 transition"
          >
            {prompt}
          </button>
        ))}
      </div>

      {/* Input area */}
      <div className="p-3 bg-white dark:bg-[#111827] border-t border-gray-100 dark:border-gray-800">
        <form
          onSubmit={(e) => {
            e.preventDefault();
            handleSend();
          }}
          className="flex items-center gap-2"
        >
          <input
            type="text"
            value={input}
            onChange={(e) => setInput(e.target.value)}
            placeholder="Hỏi AI về chi tiêu, ngân sách..."
            className="flex-1 px-3.5 py-2.5 rounded-xl bg-gray-100 dark:bg-gray-800/80 border border-transparent focus:border-emerald-500 focus:bg-white dark:focus:bg-gray-800 outline-none text-xs transition"
          />
          <button
            type="submit"
            disabled={!input.trim() || isLoading}
            className="w-9 h-9 rounded-xl bg-emerald-600 hover:bg-emerald-700 disabled:opacity-40 disabled:hover:bg-emerald-600 text-white flex items-center justify-center transition shrink-0 shadow-sm"
          >
            <Send size={15} />
          </button>
        </form>
      </div>
    </div>
  );
};
