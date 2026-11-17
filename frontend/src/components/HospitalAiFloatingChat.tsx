import React, { useState, useRef, useEffect } from 'react';
import {
  Bot,
  X,
  Send,
  Sparkles,
  Maximize2,
  Minimize2,
  Trash2,
  HelpCircle,
  Stethoscope,
  Clock,
  CheckCircle2,
  AlertTriangle,
  Activity,
  ChevronDown
} from 'lucide-react';
import { api } from '../services/api';

interface ChatMessage {
  id: string;
  sender: 'user' | 'ai';
  text: string;
  category?: string;
  timestamp: string;
  suggestedFollowUps?: string[];
}

export const HospitalAiFloatingChat: React.FC = () => {
  const [isOpen, setIsOpen] = useState(false);
  const [input, setInput] = useState('');
  const [loading, setLoading] = useState(false);
  const [messages, setMessages] = useState<ChatMessage[]>([
    {
      id: 'welcome',
      sender: 'ai',
      text: "👋 **Hello Admin!** I'm your **QueueLess Database & AI Assistant**.\n\nI can query live database records to answer questions about today's tokens, missed appointments, doctor queues, and hospital activity.\n\nWhat would you like to check?",
      timestamp: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
      suggestedFollowUps: [
        'How many tokens gone today?',
        'How much token remaining for per doctor?',
        'How many has missed?',
        'What are the things we did today?'
      ]
    }
  ]);

  const messagesEndRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  useEffect(() => {
    if (isOpen) {
      scrollToBottom();
      inputRef.current?.focus();
    }
  }, [isOpen, messages]);

  const handleSendMessage = async (queryText?: string) => {
    const textToSend = queryText || input.trim();
    if (!textToSend || loading) return;

    const userMsg: ChatMessage = {
      id: `user-${Date.now()}`,
      sender: 'user',
      text: textToSend,
      timestamp: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
    };

    setMessages((prev) => [...prev, userMsg]);
    if (!queryText) setInput('');
    setLoading(true);

    try {
      const res = await api.askHospitalAi(textToSend);
      const aiMsg: ChatMessage = {
        id: `ai-${Date.now()}`,
        sender: 'ai',
        text: res.reply,
        category: res.queryCategory,
        timestamp: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
        suggestedFollowUps: res.suggestedFollowUps
      };
      setMessages((prev) => [...prev, aiMsg]);
    } catch (err: any) {
      const errorMsg: ChatMessage = {
        id: `err-${Date.now()}`,
        sender: 'ai',
        text: `⚠️ **Unable to query database:** ${err.response?.data?.message || err.message || 'Server error. Please verify database connection.'}`,
        timestamp: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
      };
      setMessages((prev) => [...prev, errorMsg]);
    } finally {
      setLoading(false);
    }
  };

  const handleClearChat = () => {
    setMessages([
      {
        id: 'welcome-reset',
        sender: 'ai',
        text: "✨ Chat cleared. How can I assist you with your hospital's queues and tokens?",
        timestamp: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
        suggestedFollowUps: [
          'How many tokens gone today?',
          'How much token remaining for per doctor?',
          'How many has missed?',
          'What are the things we did today?'
        ]
      }
    ]);
  };

  const formatMarkdown = (content: string) => {
    // Process markdown headers, bold, bullets, and tables
    const lines = content.split('\n');
    const rendered: React.ReactNode[] = [];

    let tableRows: string[] = [];
    let inTable = false;

    const renderTable = (rows: string[], idx: number) => {
      if (rows.length < 2) return null;
      const headerCols = rows[0].split('|').map((c) => c.trim()).filter(Boolean);
      const dataRows = rows.slice(2).map((r) => r.split('|').map((c) => c.trim()).filter(Boolean));

      return (
        <div key={`table-${idx}`} className="my-2.5 overflow-x-auto rounded-xl border border-slate-200">
          <table className="min-w-full text-[11px] text-left">
            <thead className="bg-slate-100 text-slate-700 font-bold border-b border-slate-200">
              <tr>
                {headerCols.map((col, cIdx) => (
                  <th key={cIdx} className="px-2.5 py-1.5">{col.replace(/\*\*/g, '')}</th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 bg-white">
              {dataRows.map((row, rIdx) => (
                <tr key={rIdx} className="hover:bg-slate-50">
                  {row.map((cell, cIdx) => (
                    <td key={cIdx} className="px-2.5 py-1.5 font-medium text-slate-800">
                      {cell.startsWith('**') && cell.endsWith('**') ? <strong>{cell.replace(/\*\*/g, '')}</strong> : cell}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      );
    };

    lines.forEach((line, i) => {
      const trimmed = line.trim();

      if (trimmed.startsWith('|') && trimmed.endsWith('|')) {
        inTable = true;
        tableRows.push(trimmed);
        return;
      } else if (inTable) {
        rendered.push(renderTable(tableRows, i));
        tableRows = [];
        inTable = false;
      }

      if (trimmed.startsWith('### ')) {
        rendered.push(
          <div key={i} className="font-extrabold text-sm text-slate-900 mt-2 mb-1 flex items-center space-x-1">
            <span>{trimmed.replace('### ', '')}</span>
          </div>
        );
      } else if (trimmed.startsWith('* ') || trimmed.startsWith('- ')) {
        const itemText = trimmed.substring(2);
        const parts = itemText.split('**');
        rendered.push(
          <li key={i} className="text-xs text-slate-700 ml-3.5 list-disc my-0.5">
            {parts.map((p, pIdx) => (pIdx % 2 === 1 ? <strong key={pIdx} className="text-slate-900">{p}</strong> : p))}
          </li>
        );
      } else if (trimmed === '') {
        rendered.push(<div key={i} className="h-1.5" />);
      } else {
        const parts = line.split('**');
        rendered.push(
          <p key={i} className="text-xs text-slate-700 leading-relaxed">
            {parts.map((p, pIdx) => (pIdx % 2 === 1 ? <strong key={pIdx} className="text-slate-900">{p}</strong> : p))}
          </p>
        );
      }
    });

    if (inTable && tableRows.length > 0) {
      rendered.push(renderTable(tableRows, 9999));
    }

    return rendered;
  };

  return (
    <>
      {/* Floating Chat Trigger Button (Bottom-Right) */}
      {!isOpen && (
        <button
          type="button"
          onClick={() => setIsOpen(true)}
          className="fixed bottom-4 right-4 sm:bottom-6 sm:right-6 z-50 flex items-center space-x-2 px-4 py-3 rounded-full bg-gradient-to-r from-teal-600 to-emerald-600 hover:from-teal-700 hover:to-emerald-700 text-white font-bold text-xs shadow-2xl shadow-teal-700/40 hover:scale-105 active:scale-95 transition-all cursor-pointer group"
          title="Ask Hospital AI & Database Assistant"
        >
          <div className="relative">
            <Bot className="w-5 h-5 text-teal-100 group-hover:rotate-12 transition-transform" />
            <span className="absolute -top-1 -right-1 w-2.5 h-2.5 bg-amber-400 rounded-full animate-ping" />
            <span className="absolute -top-1 -right-1 w-2.5 h-2.5 bg-amber-400 rounded-full border border-teal-800" />
          </div>
          <span>Hospital AI Bot</span>
          <Sparkles className="w-3.5 h-3.5 text-amber-300" />
        </button>
      )}

      {/* Floating Chatbot Window */}
      {isOpen && (
        <div className="fixed bottom-4 right-4 sm:bottom-6 sm:right-6 z-50 w-[calc(100vw-32px)] sm:w-[420px] h-[560px] max-h-[85vh] bg-white rounded-3xl shadow-2xl border border-slate-200 flex flex-col overflow-hidden animate-in fade-in slide-in-from-bottom-5 duration-200">
          {/* Header */}
          <div className="px-4 py-3.5 bg-gradient-to-r from-teal-600 to-emerald-700 text-white flex items-center justify-between shrink-0 shadow-sm">
            <div className="flex items-center space-x-2.5">
              <div className="p-1.5 rounded-xl bg-white/15 backdrop-blur-xs">
                <Bot className="w-5 h-5 text-amber-300" />
              </div>
              <div>
                <h3 className="text-xs font-black tracking-tight leading-none flex items-center space-x-1.5">
                  <span>Hospital AI Assistant</span>
                  <span className="px-1.5 py-0.5 rounded-full bg-emerald-500/30 text-[9px] font-semibold text-emerald-200 border border-emerald-400/30">
                    Live SQL Agent
                  </span>
                </h3>
                <p className="text-[10px] text-teal-100 mt-0.5">
                  Real-time PostgreSQL query & analytics
                </p>
              </div>
            </div>

            <div className="flex items-center space-x-1">
              <button
                type="button"
                onClick={handleClearChat}
                className="p-1.5 rounded-lg hover:bg-white/10 text-teal-100 hover:text-white transition"
                title="Clear conversation"
              >
                <Trash2 className="w-3.5 h-3.5" />
              </button>
              <button
                type="button"
                onClick={() => setIsOpen(false)}
                className="p-1.5 rounded-lg hover:bg-white/10 text-teal-100 hover:text-white transition"
                title="Close chat"
              >
                <X className="w-4 h-4" />
              </button>
            </div>
          </div>

          {/* Quick Suggestion Pills Bar */}
          <div className="px-3 py-2 bg-slate-50 border-b border-slate-100 flex items-center gap-1.5 overflow-x-auto no-scrollbar shrink-0">
            <button
              type="button"
              onClick={() => handleSendMessage('How many tokens gone today?')}
              className="whitespace-nowrap px-2.5 py-1 rounded-full bg-white border border-slate-200 hover:border-teal-400 hover:bg-teal-50 text-[10px] font-bold text-slate-700 transition shrink-0 flex items-center space-x-1 shadow-2xs"
            >
              <span>🏁</span>
              <span>Tokens Gone Today</span>
            </button>
            <button
              type="button"
              onClick={() => handleSendMessage('How much token remaining for per doctor?')}
              className="whitespace-nowrap px-2.5 py-1 rounded-full bg-white border border-slate-200 hover:border-teal-400 hover:bg-teal-50 text-[10px] font-bold text-slate-700 transition shrink-0 flex items-center space-x-1 shadow-2xs"
            >
              <span>🩺</span>
              <span>Remaining Per Doctor</span>
            </button>
            <button
              type="button"
              onClick={() => handleSendMessage('How many has missed?')}
              className="whitespace-nowrap px-2.5 py-1 rounded-full bg-white border border-slate-200 hover:border-teal-400 hover:bg-teal-50 text-[10px] font-bold text-slate-700 transition shrink-0 flex items-center space-x-1 shadow-2xs"
            >
              <span>❌</span>
              <span>Missed / No-Shows</span>
            </button>
            <button
              type="button"
              onClick={() => handleSendMessage('What are the things we did today?')}
              className="whitespace-nowrap px-2.5 py-1 rounded-full bg-white border border-slate-200 hover:border-teal-400 hover:bg-teal-50 text-[10px] font-bold text-slate-700 transition shrink-0 flex items-center space-x-1 shadow-2xs"
            >
              <span>📋</span>
              <span>Today's Actions</span>
            </button>
          </div>

          {/* Messages Scroll Area */}
          <div className="flex-1 p-3.5 overflow-y-auto space-y-3 bg-slate-50/50">
            {messages.map((m) => (
              <div
                key={m.id}
                className={`flex flex-col ${m.sender === 'user' ? 'items-end' : 'items-start'}`}
              >
                <div
                  className={`max-w-[88%] rounded-2xl px-3.5 py-2.5 text-xs shadow-xs ${
                    m.sender === 'user'
                      ? 'bg-teal-600 text-white font-medium rounded-br-xs'
                      : 'bg-white border border-slate-200 text-slate-800 rounded-bl-xs'
                  }`}
                >
                  {m.sender === 'ai' ? (
                    <div className="space-y-1">{formatMarkdown(m.text)}</div>
                  ) : (
                    <span>{m.text}</span>
                  )}
                </div>

                <span className="text-[9px] text-slate-400 mt-0.5 px-1 font-medium">
                  {m.timestamp}
                </span>

                {/* Suggested follow-up buttons if provided */}
                {m.sender === 'ai' && m.suggestedFollowUps && m.suggestedFollowUps.length > 0 && (
                  <div className="flex flex-wrap gap-1 mt-1.5 max-w-[95%]">
                    {m.suggestedFollowUps.map((q, qIdx) => (
                      <button
                        key={qIdx}
                        type="button"
                        onClick={() => handleSendMessage(q)}
                        className="text-[10px] font-semibold text-teal-700 bg-teal-50 hover:bg-teal-100 border border-teal-200 rounded-lg px-2 py-0.5 transition cursor-pointer text-left"
                      >
                        ↳ {q}
                      </button>
                    ))}
                  </div>
                )}
              </div>
            ))}

            {loading && (
              <div className="flex items-center space-x-2 text-xs text-slate-500 bg-white border border-slate-200 rounded-2xl px-3.5 py-2.5 w-fit shadow-xs">
                <div className="w-3.5 h-3.5 border-2 border-teal-600 border-t-transparent rounded-full animate-spin" />
                <span className="font-semibold text-teal-700">Querying database...</span>
              </div>
            )}

            <div ref={messagesEndRef} />
          </div>

          {/* Chat Input Bar */}
          <form
            onSubmit={(e) => {
              e.preventDefault();
              handleSendMessage();
            }}
            className="p-3 bg-white border-t border-slate-200 flex items-center gap-2 shrink-0"
          >
            <input
              ref={inputRef}
              type="text"
              placeholder="Ask anything about today's tokens, doctors..."
              value={input}
              onChange={(e) => setInput(e.target.value)}
              className="flex-1 p-2.5 rounded-xl border border-slate-200 text-xs text-slate-800 focus:outline-none focus:border-teal-500 bg-slate-50 focus:bg-white transition"
              disabled={loading}
            />
            <button
              type="submit"
              disabled={loading || !input.trim()}
              className="p-2.5 rounded-xl bg-teal-600 hover:bg-teal-700 disabled:opacity-40 text-white font-bold transition shadow-md shadow-teal-600/20 cursor-pointer"
              title="Send question"
            >
              <Send className="w-4 h-4" />
            </button>
          </form>
        </div>
      )}
    </>
  );
};
