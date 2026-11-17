import React from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { Activity, ShieldCheck, Clock, Users, ArrowRight, BrainCircuit, Building2, CheckCircle2 } from 'lucide-react';

export const Home: React.FC = () => {
  const { user, quickSwitchAccount } = useAuth();

  return (
    <div className="min-h-screen bg-slate-50 flex flex-col">
      {/* Hero Section */}
      <section className="relative overflow-hidden bg-gradient-to-b from-teal-50/60 to-white py-16 sm:py-24 border-b border-slate-200">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 text-center">
          <div className="inline-flex items-center space-x-2 px-3 py-1 rounded-full bg-teal-100 text-teal-800 text-xs font-semibold mb-6">
            <BrainCircuit className="w-4 h-4 text-teal-600" />
            <span>AI-Assisted Multi-Hospital Queue Management Platform</span>
          </div>

          <h1 className="text-4xl sm:text-6xl font-extrabold text-slate-900 tracking-tight leading-tight max-w-4xl mx-auto">
            Say goodbye to waiting rooms with <span className="text-teal-600">QueueLess</span>.
          </h1>

          <p className="mt-6 text-lg sm:text-xl text-slate-600 max-w-2xl mx-auto">
            Book smart queue tokens across multiple verified hospitals, track your live position in real-time,
            and view transparent AI-predicted consultation waiting times.
          </p>

          <div className="mt-10 flex flex-col sm:flex-row items-center justify-center gap-4">
            <Link
              to="/hospitals"
              className="w-full sm:w-auto px-7 py-3.5 rounded-xl bg-teal-600 text-white font-semibold shadow-lg shadow-teal-600/25 hover:bg-teal-700 transition flex items-center justify-center space-x-2"
            >
              <span>Find Hospitals & Doctors</span>
              <ArrowRight className="w-4 h-4" />
            </Link>

            <Link
              to="/register"
              className="w-full sm:w-auto px-7 py-3.5 rounded-xl bg-white border border-slate-300 text-slate-700 font-semibold hover:bg-slate-100 transition flex items-center justify-center space-x-2"
            >
              <Building2 className="w-4 h-4 text-slate-500" />
              <span>Register a Hospital</span>
            </Link>
          </div>

          {/* Quick Persona Demo Switch Banner */}
          <div className="mt-14 max-w-3xl mx-auto p-4 bg-white rounded-2xl border border-slate-200 shadow-sm">
            <div className="text-xs font-semibold text-slate-500 uppercase tracking-wider mb-2">
              Instant Demo Evaluator – 1-Click Persona Access
            </div>
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-2">
              <button
                onClick={() => quickSwitchAccount('Patient')}
                className="p-2.5 rounded-xl border border-teal-200 bg-teal-50 hover:bg-teal-100 text-teal-900 text-xs font-semibold transition"
              >
                👤 Patient (Arun)
              </button>
              <button
                onClick={() => quickSwitchAccount('HospitalAdmin')}
                className="p-2.5 rounded-xl border border-blue-200 bg-blue-50 hover:bg-blue-100 text-blue-900 text-xs font-semibold transition"
              >
                🏥 Apollo Hospital Admin
              </button>
              <button
                onClick={() => quickSwitchAccount('Doctor')}
                className="p-2.5 rounded-xl border border-purple-200 bg-purple-50 hover:bg-purple-100 text-purple-900 text-xs font-semibold transition"
              >
                🩺 Dr. Rajesh (Doctor)
              </button>
              <button
                onClick={() => quickSwitchAccount('PlatformAdmin')}
                className="p-2.5 rounded-xl border border-amber-200 bg-amber-50 hover:bg-amber-100 text-amber-900 text-xs font-semibold transition"
              >
                🛡️ Platform Admin
              </button>
            </div>
          </div>
        </div>
      </section>

      {/* Features Grid */}
      <section className="py-16 bg-white">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="text-center mb-12">
            <h2 className="text-2xl sm:text-3xl font-bold text-slate-900">Engineered for Transparency & Scalability</h2>
            <p className="mt-2 text-sm text-slate-500">End-to-end multi-tenant architecture with real-time SignalR updates.</p>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
            <div className="p-6 rounded-2xl border border-slate-200 bg-slate-50/50">
              <div className="w-12 h-12 rounded-xl bg-teal-100 text-teal-600 flex items-center justify-center mb-4">
                <BrainCircuit className="w-6 h-6" />
              </div>
              <h3 className="text-lg font-bold text-slate-900">3-Tier AI Waiting-Time Engine</h3>
              <p className="mt-2 text-sm text-slate-600 leading-relaxed">
                Mode 1 Baseline for new hospitals, Mode 2 multi-hospital shared FastForest model, and Mode 3 hospital-specific calibrated model with transparent methodology explanations.
              </p>
            </div>

            <div className="p-6 rounded-2xl border border-slate-200 bg-slate-50/50">
              <div className="w-12 h-12 rounded-xl bg-blue-100 text-blue-600 flex items-center justify-center mb-4">
                <Clock className="w-6 h-6" />
              </div>
              <h3 className="text-lg font-bold text-slate-900">Live SignalR Queue Broadcasts</h3>
              <p className="mt-2 text-sm text-slate-600 leading-relaxed">
                Tokens, queue positions and doctor call announcements update instantaneously on patient screens without manual polling or browser refreshes.
              </p>
            </div>

            <div className="p-6 rounded-2xl border border-slate-200 bg-slate-50/50">
              <div className="w-12 h-12 rounded-xl bg-amber-100 text-amber-600 flex items-center justify-center mb-4">
                <ShieldCheck className="w-6 h-6" />
              </div>
              <h3 className="text-lg font-bold text-slate-900">Strict Tenant Separation</h3>
              <p className="mt-2 text-sm text-slate-600 leading-relaxed">
                UUID primary keys, membership-derived claims, and composite boundary constraints prevent cross-hospital data leakage between Hospital A and Hospital B.
              </p>
            </div>
          </div>
        </div>
      </section>
    </div>
  );
};
