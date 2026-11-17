import React from 'react';

interface BadgeProps {
  status: string;
  className?: string;
}

export const Badge: React.FC<BadgeProps> = ({ status, className = '' }) => {
  const getStyles = () => {
    switch (status?.toUpperCase()) {
      case 'APPROVED':
      case 'COMPLETED':
      case 'OPEN':
      case 'ACTIVE':
      case 'HOSPITAL_MODEL':
        return 'bg-emerald-50 text-emerald-700 border-emerald-200';
      case 'PENDING':
      case 'WAITING':
      case 'SCHEDULED':
      case 'BASELINE':
        return 'bg-amber-50 text-amber-700 border-amber-200';
      case 'CALLED':
      case 'SERVING':
      case 'SHARED_MODEL':
        return 'bg-blue-50 text-blue-700 border-blue-200';
      case 'PAUSED':
        return 'bg-purple-50 text-purple-700 border-purple-200';
      case 'REJECTED':
      case 'SUSPENDED':
      case 'CANCELLED':
      case 'NO_SHOW':
      case 'INACTIVE':
      case 'CLOSED':
        return 'bg-rose-50 text-rose-700 border-rose-200';
      default:
        return 'bg-slate-100 text-slate-700 border-slate-200';
    }
  };

  return (
    <span
      className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold border ${getStyles()} ${className}`}
    >
      {status}
    </span>
  );
};
