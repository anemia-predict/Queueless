import React, { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { Activity, Lock, Mail, AlertCircle, ArrowRight } from 'lucide-react';
import { UserRole } from '../../types';

export const Login: React.FC = () => {
  const { login, quickSwitchAccount } = useAuth();
  const navigate = useNavigate();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      const user = await login(email, password);
      if (user.role === 'PlatformAdmin') navigate('/platform');
      else if (user.role === 'HospitalAdmin') navigate('/hospital');
      else if (user.role === 'Doctor') navigate('/doctor');
      else navigate('/patient');
    } catch (err: any) {
      setError(err.response?.data?.message || 'Invalid email or password.');
    } finally {
      setLoading(false);
    }
  };

  const handleQuickLogin = async (role: UserRole, customEmail?: string) => {
    setError('');
    setLoading(true);
    try {
      const emailToUse = customEmail || (
        role === 'Patient' ? 'patient.arun@queueless.local' :
        role === 'Doctor' ? 'doctor.rajesh@queueless.local' :
        role === 'HospitalAdmin' ? 'apollo.admin@queueless.local' :
        'platformadmin@queueless.local'
      );
      const passToUse = (
        role === 'PlatformAdmin' ? 'Admin@123456' :
        role === 'HospitalAdmin' ? 'Hospital@123' :
        role === 'Doctor' ? 'Doctor@123' :
        'Patient@123'
      );

      setEmail(emailToUse);
      setPassword(passToUse);

      await quickSwitchAccount(role, customEmail);
      if (role === 'PlatformAdmin') navigate('/platform');
      else if (role === 'HospitalAdmin') navigate('/hospital');
      else if (role === 'Doctor') navigate('/doctor');
      else navigate('/patient');
    } catch (err: any) {
      setError(err.response?.data?.message || 'Login failed.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-slate-50 flex items-center justify-center p-4">
      <div className="max-w-md w-full bg-white rounded-3xl border border-slate-200 p-8 shadow-xl">
        <div className="text-center mb-6">
          <div className="w-12 h-12 rounded-2xl bg-teal-600 flex items-center justify-center text-white mx-auto shadow-lg shadow-teal-600/25 mb-3">
            <Activity className="w-7 h-7 stroke-[2.5]" />
          </div>
          <h2 className="text-2xl font-extrabold text-slate-900 tracking-tight">Sign In to QueueLess</h2>
          <p className="text-xs text-slate-500 mt-1">Multi-Hospital Queue Management & AI Waiting-Time Engine</p>
        </div>

        {error && (
          <div className="p-3 mb-4 rounded-xl bg-rose-50 border border-rose-200 text-rose-700 text-xs flex items-center space-x-2">
            <AlertCircle className="w-4 h-4 shrink-0" />
            <span>{error}</span>
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">Email Address</label>
            <div className="relative">
              <Mail className="w-4 h-4 absolute left-3 top-3 text-slate-400" />
              <input
                type="email"
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="name@hospital.com"
                className="w-full pl-9 pr-3 py-2 text-xs rounded-xl border border-slate-200 focus:outline-none focus:border-teal-500"
              />
            </div>
          </div>

          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">Password</label>
            <div className="relative">
              <Lock className="w-4 h-4 absolute left-3 top-3 text-slate-400" />
              <input
                type="password"
                required
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="••••••••"
                className="w-full pl-9 pr-3 py-2 text-xs rounded-xl border border-slate-200 focus:outline-none focus:border-teal-500"
              />
            </div>
          </div>

          <button
            type="submit"
            disabled={loading}
            className="w-full py-2.5 px-4 rounded-xl bg-teal-600 hover:bg-teal-700 text-white font-semibold text-xs shadow-md shadow-teal-600/20 transition disabled:opacity-50 flex items-center justify-center space-x-1"
          >
            <span>{loading ? 'Signing in...' : 'Sign In'}</span>
            <ArrowRight className="w-3.5 h-3.5" />
          </button>
        </form>

        {/* 1-Click Persona Login */}
        <div className="mt-6 pt-6 border-t border-slate-100 space-y-4">
          <div className="text-[11px] font-semibold text-slate-400 uppercase tracking-wider text-center">
            Demo Patients (For 3-Friend Sequential Queue Test)
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
            <button
              type="button"
              onClick={() => handleQuickLogin('Patient', 'patient.arun@queueless.local')}
              className="p-2.5 rounded-xl border border-teal-200 bg-teal-50 hover:bg-teal-100 text-teal-900 text-xs font-semibold text-left transition shadow-xs"
            >
              <div className="font-bold flex items-center justify-between">
                <span>👤 Friend 1</span>
                <span className="text-[9px] px-1.5 py-0.5 rounded-md bg-teal-200 text-teal-800">Token 1</span>
              </div>
              <div className="text-[10px] text-teal-800 font-medium mt-0.5">Arun Kumar</div>
              <div className="text-[9px] text-teal-600 font-mono">Patient@123</div>
            </button>

            <button
              type="button"
              onClick={() => handleQuickLogin('Patient', 'patient.priya@queueless.local')}
              className="p-2.5 rounded-xl border border-emerald-200 bg-emerald-50 hover:bg-emerald-100 text-emerald-900 text-xs font-semibold text-left transition shadow-xs"
            >
              <div className="font-bold flex items-center justify-between">
                <span>👤 Friend 2</span>
                <span className="text-[9px] px-1.5 py-0.5 rounded-md bg-emerald-200 text-emerald-800">Token 2</span>
              </div>
              <div className="text-[10px] text-emerald-800 font-medium mt-0.5">Priya Sharma</div>
              <div className="text-[9px] text-emerald-600 font-mono">Patient@123</div>
            </button>

            <button
              type="button"
              onClick={() => handleQuickLogin('Patient', 'patient.rahul@queueless.local')}
              className="p-2.5 rounded-xl border border-cyan-200 bg-cyan-50 hover:bg-cyan-100 text-cyan-900 text-xs font-semibold text-left transition shadow-xs"
            >
              <div className="font-bold flex items-center justify-between">
                <span>👤 Friend 3</span>
                <span className="text-[9px] px-1.5 py-0.5 rounded-md bg-cyan-200 text-cyan-800">Token 3</span>
              </div>
              <div className="text-[10px] text-cyan-800 font-medium mt-0.5">Rahul Verma</div>
              <div className="text-[9px] text-cyan-600 font-mono">Patient@123</div>
            </button>
          </div>

          <div className="text-[11px] font-semibold text-slate-400 uppercase tracking-wider text-center pt-2">
            Hospital Staff Accounts
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
            <button
              type="button"
              onClick={() => handleQuickLogin('Doctor')}
              className="p-2.5 rounded-xl border border-purple-200 bg-purple-50 hover:bg-purple-100 text-purple-900 text-xs font-semibold text-left transition shadow-xs"
            >
              <div className="font-bold">🩺 Dr. Rajesh</div>
              <div className="text-[10px] text-purple-700/80 font-normal truncate">doctor.rajesh@queueless.local</div>
              <div className="text-[9px] text-purple-600 font-mono">Doctor@123</div>
            </button>
            <button
              type="button"
              onClick={() => handleQuickLogin('HospitalAdmin')}
              className="p-2.5 rounded-xl border border-blue-200 bg-blue-50 hover:bg-blue-100 text-blue-900 text-xs font-semibold text-left transition shadow-xs"
            >
              <div className="font-bold">🏥 Apollo Admin</div>
              <div className="text-[10px] text-blue-700/80 font-normal truncate">apollo.admin@queueless.local</div>
              <div className="text-[9px] text-blue-600 font-mono">Hospital@123</div>
            </button>
            <button
              type="button"
              onClick={() => handleQuickLogin('PlatformAdmin')}
              className="p-2.5 rounded-xl border border-amber-200 bg-amber-50 hover:bg-amber-100 text-amber-900 text-xs font-semibold text-left transition shadow-xs"
            >
              <div className="font-bold">🛡️ Platform Admin</div>
              <div className="text-[10px] text-amber-700/80 font-normal truncate">platformadmin@queueless.local</div>
              <div className="text-[9px] text-amber-600 font-mono">Admin@123456</div>
            </button>
          </div>

          {/* Registered Friends Quick Logins */}
          <div className="pt-2">
            <div className="text-[11px] font-semibold text-slate-400 uppercase tracking-wider text-center mb-1.5">
              Or Tap Your Registered Account
            </div>
            <div className="flex flex-wrap gap-1.5 justify-center">
              {[
                { name: 'Rakash', email: 'rakash@gmail.com' },
                { name: 'Thavish', email: 'thavish@gmail.com' },
                { name: 'Nandhu', email: 'nandhu123@gmail.com' },
                { name: 'Virat', email: 'virat18@gmail.com' },
                { name: 'Eren', email: 'eren@gmail.com' },
                { name: 'Jaivikash', email: 'jaiv21114.ad@rmkec.ac.in' },
              ].map((friend) => (
                <button
                  key={friend.email}
                  type="button"
                  onClick={() => handleQuickLogin('Patient', friend.email)}
                  className="px-2.5 py-1 rounded-lg bg-slate-100 hover:bg-teal-50 border border-slate-200 hover:border-teal-300 text-[11px] font-semibold text-slate-700 hover:text-teal-800 transition"
                >
                  {friend.name}
                </button>
              ))}
            </div>
          </div>
        </div>

        <div className="mt-6 text-center text-xs text-slate-500">
          Don't have an account?{' '}
          <Link to="/register" className="text-teal-600 font-semibold hover:underline">
            Register now
          </Link>
        </div>
      </div>
    </div>
  );
};
