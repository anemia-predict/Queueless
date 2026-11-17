import React, { useEffect } from 'react';
import { X } from 'lucide-react';

interface ModalProps {
  isOpen: boolean;
  onClose: () => void;
  title: string;
  children: React.ReactNode;
  maxWidth?: string;
}

export const Modal: React.FC<ModalProps> = ({
  isOpen,
  onClose,
  title,
  children,
  maxWidth = 'max-w-lg',
}) => {
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    if (isOpen) window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  return (
    <div
      className="fixed inset-0 z-50 overflow-y-auto bg-slate-900/60 backdrop-blur-sm flex items-center justify-center p-3 sm:p-4"
      onClick={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
    >
      <div className={`bg-white rounded-2xl shadow-2xl border border-slate-100 w-full ${maxWidth} overflow-hidden transform transition-all my-8 max-h-[90vh] flex flex-col`}>
        {/* Sticky Header with prominent X button */}
        <div className="sticky top-0 bg-white z-20 px-5 sm:px-6 py-4 border-b border-slate-100 flex items-center justify-between shrink-0">
          <h3 className="text-base font-bold text-slate-900 pr-2 truncate">{title}</h3>
          <button
            type="button"
            onClick={onClose}
            aria-label="Close dialog"
            title="Close dialog (Esc)"
            className="p-1.5 -mr-1.5 rounded-xl text-slate-400 hover:text-slate-700 hover:bg-slate-100 active:bg-slate-200 transition focus:outline-none focus:ring-2 focus:ring-teal-500 shrink-0"
          >
            <X className="w-5 h-5 stroke-[2.5]" />
          </button>
        </div>
        <div className="p-5 sm:p-6 overflow-y-auto">{children}</div>
      </div>
    </div>
  );
};
