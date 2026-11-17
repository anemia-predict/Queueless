import React, { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { api } from '../../services/api';
import { Activity, Lock, Mail, User, Phone, Building2, AlertCircle, CheckCircle2 } from 'lucide-react';

export const Register: React.FC = () => {
  const { register } = useAuth();
  const navigate = useNavigate();

  const [tab, setTab] = useState<'patient' | 'hospital'>('patient');

  // Patient Registration Form
  const [patientEmail, setPatientEmail] = useState('');
  const [patientPassword, setPatientPassword] = useState('');
  const [patientFullName, setPatientFullName] = useState('');
  const [patientPhone, setPatientPhone] = useState('');

  // Hospital Onboarding Form
  const [hospAdminEmail, setHospAdminEmail] = useState('');
  const [hospAdminPass, setHospAdminPass] = useState('');
  const [hospAdminName, setHospAdminName] = useState('');
  const [hospName, setHospName] = useState('');
  const [hospRegNum, setHospRegNum] = useState('');
  const [hospAddress, setHospAddress] = useState('');
  const [hospCity, setHospCity] = useState('');
  const [hospPhone, setHospPhone] = useState('');
  const [hospInitMinutes, setHospInitMinutes] = useState(10);

  const [error, setError] = useState('');
  const [successMsg, setSuccessMsg] = useState('');
  const [loading, setLoading] = useState(false);

  const handlePatientSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      await register({
        email: patientEmail,
        password: patientPassword,
        fullName: patientFullName,
        phoneNumber: patientPhone,
        role: 'Patient',
      });
      navigate('/patient');
    } catch (err: any) {
      setError(err.response?.data?.message || 'Registration failed.');
    } finally {
      setLoading(false);
    }
  };

  const handleHospitalSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setSuccessMsg('');
    setLoading(true);
    try {
      // 1. Register admin user first
      await register({
        email: hospAdminEmail,
        password: hospAdminPass,
        fullName: hospAdminName,
        phoneNumber: hospPhone,
        role: 'HospitalAdmin',
      });

      // 2. Submit hospital registration
      const newHosp = await api.registerHospital({
        name: hospName,
        registrationNumber: hospRegNum,
        address: hospAddress,
        city: hospCity,
        contactPhone: hospPhone,
        contactEmail: hospAdminEmail,
        initialConsultationMinutes: hospInitMinutes,
      });

      setSuccessMsg(`Hospital "${newHosp.name}" submitted! Status: PENDING platform administrator review.`);
      setTimeout(() => navigate('/hospital'), 2000);
    } catch (err: any) {
      setError(err.response?.data?.message || 'Hospital onboarding submission failed.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-slate-50 flex items-center justify-center p-4 py-12">
      <div className="max-w-xl w-full bg-white rounded-3xl border border-slate-200 p-8 shadow-xl">
        <div className="text-center mb-6">
          <div className="w-12 h-12 rounded-2xl bg-teal-600 flex items-center justify-center text-white mx-auto shadow-lg shadow-teal-600/25 mb-3">
            <Activity className="w-7 h-7 stroke-[2.5]" />
          </div>
          <h2 className="text-2xl font-extrabold text-slate-900 tracking-tight">Create an Account</h2>
          <p className="text-xs text-slate-500 mt-1">Join the QueueLess Multi-Hospital Health Network</p>
        </div>

        {/* Tab Switcher */}
        <div className="flex p-1 bg-slate-100 rounded-2xl mb-6">
          <button
            onClick={() => setTab('patient')}
            className={`flex-1 py-2 text-xs font-semibold rounded-xl transition ${
              tab === 'patient' ? 'bg-white text-teal-700 shadow-sm' : 'text-slate-500 hover:text-slate-900'
            }`}
          >
            👤 Patient Registration
          </button>
          <button
            onClick={() => setTab('hospital')}
            className={`flex-1 py-2 text-xs font-semibold rounded-xl transition ${
              tab === 'hospital' ? 'bg-white text-teal-700 shadow-sm' : 'text-slate-500 hover:text-slate-900'
            }`}
          >
            🏥 Hospital Onboarding
          </button>
        </div>

        {error && (
          <div className="p-3 mb-4 rounded-xl bg-rose-50 border border-rose-200 text-rose-700 text-xs flex items-center space-x-2">
            <AlertCircle className="w-4 h-4 shrink-0" />
            <span>{error}</span>
          </div>
        )}

        {successMsg && (
          <div className="p-3 mb-4 rounded-xl bg-emerald-50 border border-emerald-200 text-emerald-700 text-xs flex items-center space-x-2">
            <CheckCircle2 className="w-4 h-4 shrink-0" />
            <span>{successMsg}</span>
          </div>
        )}

        {tab === 'patient' ? (
          <form onSubmit={handlePatientSubmit} className="space-y-4">
            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1">Full Name</label>
              <div className="relative">
                <User className="w-4 h-4 absolute left-3 top-3 text-slate-400" />
                <input
                  type="text"
                  required
                  value={patientFullName}
                  onChange={(e) => setPatientFullName(e.target.value)}
                  placeholder="Arun Kumar"
                  className="w-full pl-9 pr-3 py-2 text-xs rounded-xl border border-slate-200 focus:outline-none focus:border-teal-500"
                />
              </div>
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1">Email Address</label>
              <div className="relative">
                <Mail className="w-4 h-4 absolute left-3 top-3 text-slate-400" />
                <input
                  type="email"
                  required
                  value={patientEmail}
                  onChange={(e) => setPatientEmail(e.target.value)}
                  placeholder="arun@example.com"
                  className="w-full pl-9 pr-3 py-2 text-xs rounded-xl border border-slate-200 focus:outline-none focus:border-teal-500"
                />
              </div>
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1">Phone Number</label>
              <div className="relative">
                <Phone className="w-4 h-4 absolute left-3 top-3 text-slate-400" />
                <input
                  type="tel"
                  value={patientPhone}
                  onChange={(e) => setPatientPhone(e.target.value)}
                  placeholder="+91 98765 43210"
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
                  minLength={6}
                  value={patientPassword}
                  onChange={(e) => setPatientPassword(e.target.value)}
                  placeholder="Min. 6 characters"
                  className="w-full pl-9 pr-3 py-2 text-xs rounded-xl border border-slate-200 focus:outline-none focus:border-teal-500"
                />
              </div>
            </div>

            <button
              type="submit"
              disabled={loading}
              className="w-full py-2.5 px-4 rounded-xl bg-teal-600 hover:bg-teal-700 text-white font-semibold text-xs shadow-md shadow-teal-600/20 transition disabled:opacity-50"
            >
              {loading ? 'Creating Account...' : 'Complete Patient Registration'}
            </button>
          </form>
        ) : (
          <form onSubmit={handleHospitalSubmit} className="space-y-3">
            <div className="p-3 bg-teal-50 border border-teal-200 rounded-xl text-xs text-teal-800">
              Hospital registrations are created with <span className="font-bold">PENDING</span> status and require platform admin approval before opening live queues.
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1">Hospital Name</label>
                <input
                  type="text"
                  required
                  value={hospName}
                  onChange={(e) => setHospName(e.target.value)}
                  placeholder="City Care Hospital"
                  className="w-full p-2 text-xs rounded-xl border border-slate-200 focus:outline-none focus:border-teal-500"
                />
              </div>
              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1">Registration #</label>
                <input
                  type="text"
                  required
                  value={hospRegNum}
                  onChange={(e) => setHospRegNum(e.target.value)}
                  placeholder="HOSP-REG-101"
                  className="w-full p-2 text-xs rounded-xl border border-slate-200 focus:outline-none focus:border-teal-500"
                />
              </div>
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1">City</label>
                <input
                  type="text"
                  required
                  value={hospCity}
                  onChange={(e) => setHospCity(e.target.value)}
                  placeholder="Bengaluru"
                  className="w-full p-2 text-xs rounded-xl border border-slate-200 focus:outline-none focus:border-teal-500"
                />
              </div>
              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1">Address</label>
                <input
                  type="text"
                  required
                  value={hospAddress}
                  onChange={(e) => setHospAddress(e.target.value)}
                  placeholder="123 Hospital Road"
                  className="w-full p-2 text-xs rounded-xl border border-slate-200 focus:outline-none focus:border-teal-500"
                />
              </div>
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1">Contact Phone</label>
                <input
                  type="tel"
                  required
                  value={hospPhone}
                  onChange={(e) => setHospPhone(e.target.value)}
                  placeholder="+91 80 1234 5678"
                  className="w-full p-2 text-xs rounded-xl border border-slate-200 focus:outline-none focus:border-teal-500"
                />
              </div>
              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1">Initial Consultation Est. (mins)</label>
                <input
                  type="number"
                  min={5}
                  max={60}
                  value={hospInitMinutes}
                  onChange={(e) => setHospInitMinutes(Number(e.target.value))}
                  className="w-full p-2 text-xs rounded-xl border border-slate-200 focus:outline-none focus:border-teal-500"
                />
              </div>
            </div>

            <div className="pt-2 border-t border-slate-100">
              <span className="text-xs font-bold text-slate-700 block mb-2">Hospital Administrator Login</span>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <input
                    type="text"
                    required
                    placeholder="Admin Full Name"
                    value={hospAdminName}
                    onChange={(e) => setHospAdminName(e.target.value)}
                    className="w-full p-2 text-xs rounded-xl border border-slate-200 focus:outline-none focus:border-teal-500"
                  />
                </div>
                <div>
                  <input
                    type="email"
                    required
                    placeholder="Admin Email"
                    value={hospAdminEmail}
                    onChange={(e) => setHospAdminEmail(e.target.value)}
                    className="w-full p-2 text-xs rounded-xl border border-slate-200 focus:outline-none focus:border-teal-500"
                  />
                </div>
              </div>
              <div className="mt-2">
                <input
                  type="password"
                  required
                  minLength={6}
                  placeholder="Admin Password"
                  value={hospAdminPass}
                  onChange={(e) => setHospAdminPass(e.target.value)}
                  className="w-full p-2 text-xs rounded-xl border border-slate-200 focus:outline-none focus:border-teal-500"
                />
              </div>
            </div>

            <button
              type="submit"
              disabled={loading}
              className="w-full py-2.5 px-4 rounded-xl bg-teal-600 hover:bg-teal-700 text-white font-semibold text-xs shadow-md shadow-teal-600/20 transition disabled:opacity-50 mt-2"
            >
              {loading ? 'Submitting Hospital...' : 'Submit Hospital for Platform Approval'}
            </button>
          </form>
        )}

        <div className="mt-6 text-center text-xs text-slate-500">
          Already have an account?{' '}
          <Link to="/login" className="text-teal-600 font-semibold hover:underline">
            Sign In
          </Link>
        </div>
      </div>
    </div>
  );
};
