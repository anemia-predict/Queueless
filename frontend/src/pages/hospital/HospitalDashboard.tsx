import React, { useState, useEffect } from 'react';
import { api } from '../../services/api';
import { Hospital, Department, Doctor, DoctorSchedule, QueueSession, HospitalAnalytics, AIModelMetrics } from '../../types';
import { useAuth } from '../../contexts/AuthContext';
import { Badge } from '../../components/Badge';
import { StatCard } from '../../components/StatCard';
import { Modal } from '../../components/Modal';
import { QRCameraScanner } from '../../components/QRCameraScanner';
import { HospitalAiFloatingChat } from '../../components/HospitalAiFloatingChat';
import {
  Building2, Users, Stethoscope, Clock, BrainCircuit, Calendar, BarChart3,
  Plus, Sparkles, CheckCircle2, AlertCircle, PlayCircle, Layers, QrCode, Search, UserCheck, Camera, Keyboard, X
} from 'lucide-react';
import { ResponsiveContainer, BarChart, Bar, XAxis, YAxis, Tooltip, CartesianGrid, LineChart, Line, Legend } from 'recharts';

export const HospitalDashboard: React.FC = () => {
  const { user } = useAuth();
  const [tab, setTab] = useState<'overview' | 'departments' | 'doctors' | 'schedules' | 'ai'>('overview');

  const [hospital, setHospital] = useState<Hospital | null>(null);
  const [analytics, setAnalytics] = useState<HospitalAnalytics | null>(null);
  const [departments, setDepartments] = useState<Department[]>([]);
  const [doctors, setDoctors] = useState<Doctor[]>([]);
  const [schedules, setSchedules] = useState<DoctorSchedule[]>([]);
  const [queues, setQueues] = useState<QueueSession[]>([]);
  const [aiMetrics, setAiMetrics] = useState<AIModelMetrics | null>(null);

  const [loading, setLoading] = useState(true);

  // Modals
  const [deptModalOpen, setDeptModalOpen] = useState(false);
  const [newDeptName, setNewDeptName] = useState('');
  const [newDeptDesc, setNewDeptDesc] = useState('');
  const [newDeptDuration, setNewDeptDuration] = useState(10);

  const [doctorModalOpen, setDoctorModalOpen] = useState(false);
  const [newDocName, setNewDocName] = useState('');
  const [newDocSpec, setNewDocSpec] = useState('');
  const [newDocLicense, setNewDocLicense] = useState('');
  const [newDocDeptIds, setNewDocDeptIds] = useState<string[]>([]);
  const [newDocEmail, setNewDocEmail] = useState('');
  const [newDocPassword, setNewDocPassword] = useState('');

  const [queueModalOpen, setQueueModalOpen] = useState(false);
  const [newQueueDocId, setNewQueueDocId] = useState('');
  const [newQueueDeptId, setNewQueueDeptId] = useState('');
  const [newQueueCapacity, setNewQueueCapacity] = useState(30);

  // Scan / Check-In Modal state
  const [scanModalOpen, setScanModalOpen] = useState(false);
  const [scanTab, setScanTab] = useState<'camera' | 'manual'>('camera');
  const [scanInput, setScanInput] = useState('');
  const [scanResult, setScanResult] = useState<any>(null);
  const [scanLoading, setScanLoading] = useState(false);
  const [checkInLoading, setCheckInLoading] = useState(false);
  const [scanError, setScanError] = useState('');
  const [scanSuccess, setScanSuccess] = useState('');

  // AI Actions state
  const [seedingLoading, setSeedingLoading] = useState(false);
  const [trainingLoading, setTrainingLoading] = useState(false);
  const [aiMessage, setAiMessage] = useState('');

  const handleLiveCameraScan = async (decodedText: string, stage: 'AUTO' | 'BUILDING' | 'ROOM') => {
    setScanLoading(true);
    setScanError('');
    setScanSuccess('');
    try {
      const res = await api.verifyBooking(decodedText);
      setScanResult(res);

      // Perform automatic action based on selected stage
      if (stage === 'BUILDING' || (stage === 'AUTO' && !res.isBuildingCheckedIn)) {
        const checkInRes = await api.buildingCheckIn(res.id);
        setScanSuccess(`Stage 1 Complete: Patient ${res.patientName} (Token #${res.tokenNumber}) checked in at Building Entrance!`);
        setScanResult((prev: any) => ({ ...prev, isBuildingCheckedIn: true }));
        await loadAll();
      } else if (stage === 'ROOM' || (stage === 'AUTO' && res.isBuildingCheckedIn && res.status === 'CALLED')) {
        const roomRes = await api.roomCheckIn(res.id);
        setScanSuccess(`Stage 2 Complete: Patient ${res.patientName} (Token #${res.tokenNumber}) admitted to Doctor Room!`);
        setScanResult((prev: any) => ({ ...prev, isDoctorRoomCheckedIn: true, status: 'SERVING' }));
        await loadAll();
      } else {
        setScanSuccess(`Token #${res.tokenNumber} verified for ${res.patientName}. Ready for check-in.`);
      }
    } catch (err: any) {
      setScanError(err.response?.data?.message || 'Token not found or does not belong to this hospital.');
    } finally {
      setScanLoading(false);
    }
  };

  const handleVerifyScan = async (queryToVerify?: string) => {
    const q = queryToVerify || scanInput;
    if (!q || !q.trim()) return;
    setScanLoading(true);
    setScanError('');
    setScanSuccess('');
    setScanResult(null);
    try {
      const res = await api.verifyBooking(q.trim());
      setScanResult(res);
    } catch (err: any) {
      setScanError(err.response?.data?.message || 'Token not found or does not belong to this hospital.');
    } finally {
      setScanLoading(false);
    }
  };

  const handleConfirmCheckIn = async () => {
    if (!scanResult) return;
    setCheckInLoading(true);
    setScanError('');
    try {
      const res = await api.checkInBooking(scanResult.id);
      setScanSuccess(res.message || 'Patient successfully checked in!');
      setScanResult((prev: any) => ({ ...prev, isBuildingCheckedIn: true }));
      await loadAll();
    } catch (err: any) {
      setScanError(err.response?.data?.message || 'Failed to check in patient.');
    } finally {
      setCheckInLoading(false);
    }
  };

  const handleBuildingCheckIn = async () => {
    if (!scanResult) return;
    setCheckInLoading(true);
    setScanError('');
    try {
      const res = await api.buildingCheckIn(scanResult.id);
      setScanSuccess(res.message || 'Building entry verified!');
      setScanResult((prev: any) => ({ ...prev, isBuildingCheckedIn: true }));
      await loadAll();
    } catch (err: any) {
      setScanError(err.response?.data?.message || 'Failed to check in patient at building.');
    } finally {
      setCheckInLoading(false);
    }
  };

  const handleRoomCheckIn = async () => {
    if (!scanResult) return;
    setCheckInLoading(true);
    setScanError('');
    try {
      const res = await api.roomCheckIn(scanResult.id);
      setScanSuccess(res.message || 'Doctor room entry verified!');
      setScanResult((prev: any) => ({ ...prev, isDoctorRoomCheckedIn: true, status: 'SERVING' }));
      await loadAll();
    } catch (err: any) {
      setScanError(err.response?.data?.message || 'Failed to admit patient into doctor room.');
    } finally {
      setCheckInLoading(false);
    }
  };

  const loadAll = async () => {
    try {
      const [h, a, depts, docs, scheds, q, ai] = await Promise.all([
        api.getMyHospital(),
        api.getHospitalAnalytics().catch(() => null),
        api.getDepartments(),
        api.getDoctors(),
        api.getSchedules(),
        api.getQueues(),
        api.getHospitalAIMetrics().catch(() => null),
      ]);
      setHospital(h);
      setAnalytics(a);
      setDepartments(depts);
      setDoctors(docs);
      setSchedules(scheds);
      setQueues(q);
      setAiMetrics(ai);
    } catch (err) {
      console.error('Failed to load hospital dashboard:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadAll();
  }, []);

  const handleCreateDepartment = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await api.createDepartment({
        name: newDeptName,
        description: newDeptDesc,
        defaultConsultationMinutes: newDeptDuration,
      });
      setDeptModalOpen(false);
      setNewDeptName('');
      setNewDeptDesc('');
      await loadAll();
    } catch (err: any) {
      alert(err.response?.data?.message || 'Failed to create department');
    }
  };

  const handleCreateDoctor = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await api.createDoctor({
        fullName: newDocName,
        specialization: newDocSpec,
        licenseNumber: newDocLicense,
        departmentIds: newDocDeptIds,
        doctorEmail: newDocEmail || undefined,
        doctorPassword: newDocPassword || undefined,
      });
      setDoctorModalOpen(false);
      setNewDocName('');
      setNewDocSpec('');
      setNewDocLicense('');
      setNewDocEmail('');
      setNewDocPassword('');
      await loadAll();
    } catch (err: any) {
      alert(err.response?.data?.message || 'Failed to create doctor');
    }
  };

  const handleOpenQueue = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await api.openQueueSession({
        doctorId: newQueueDocId,
        departmentId: newQueueDeptId,
        maxCapacity: newQueueCapacity,
      });
      setQueueModalOpen(false);
      await loadAll();
    } catch (err: any) {
      alert(err.response?.data?.message || 'Failed to open queue session');
    }
  };

  const handleSeedSyntheticData = async () => {
    setSeedingLoading(true);
    setAiMessage('');
    try {
      const res = await api.seedSyntheticData(60, hospital?.id);
      setAiMessage(`Success: ${res.message}`);
      await loadAll();
    } catch (err: any) {
      setAiMessage(`Error: ${err.response?.data?.message || 'Seeding failed'}`);
    } finally {
      setSeedingLoading(false);
    }
  };

  const handleTrainModel = async () => {
    setTrainingLoading(true);
    setAiMessage('');
    try {
      const res = await api.trainHospitalModel();
      setAiMessage(`Model trained successfully! Version: ${res.versionTag}, MAE: ${res.meanAbsoluteError} mins (Baseline MAE: ${res.baselineMAE} mins). Status: ${res.status}`);
      await loadAll();
    } catch (err: any) {
      setAiMessage(`Training error: ${err.response?.data?.message || 'Model training failed'}`);
    } finally {
      setTrainingLoading(false);
    }
  };

  if (loading) {
    return (
      <div className="max-w-7xl mx-auto px-4 py-20 text-center">
        <div className="w-10 h-10 border-4 border-teal-600 border-t-transparent rounded-full animate-spin mx-auto mb-3" />
        <p className="text-sm text-slate-500">Loading hospital administration...</p>
      </div>
    );
  }

  if (!hospital) {
    return (
      <div className="max-w-7xl mx-auto px-4 py-20 text-center">
        <h2 className="text-xl font-bold text-slate-800">No Hospital Profile Found</h2>
        <p className="text-xs text-slate-500 mt-2">Submit hospital registration to gain access.</p>
      </div>
    );
  }

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-10">
      {/* Hospital Banner */}
      <div className="bg-white rounded-3xl border border-slate-200 p-8 shadow-sm mb-8 flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <div className="flex items-center space-x-3">
            <h1 className="text-3xl font-extrabold text-slate-900 tracking-tight">{hospital.name}</h1>
            <Badge status={hospital.status} />
          </div>
          <p className="text-xs text-slate-500 mt-1">
            Reg: <span className="font-semibold text-slate-700">{hospital.registrationNumber}</span> | {hospital.address}, {hospital.city} | Tenant ID: {hospital.id}
          </p>
        </div>

        <div className="flex items-center gap-3">
          <button
            onClick={() => {
              setScanModalOpen(true);
              setScanResult(null);
              setScanError('');
              setScanSuccess('');
              setScanInput('');
            }}
            className="flex items-center space-x-2 px-5 py-2.5 rounded-2xl bg-teal-600 hover:bg-teal-700 text-white font-bold text-xs shadow-md shadow-teal-600/20 transition cursor-pointer shrink-0"
          >
            <QrCode className="w-4 h-4" />
            <span>Scan & Check-In Patient QR</span>
          </button>
        </div>

        {hospital.status !== 'APPROVED' && (
          <div className="p-3 bg-amber-50 border border-amber-200 rounded-xl text-xs text-amber-800 flex items-center space-x-2">
            <AlertCircle className="w-4 h-4 shrink-0 text-amber-600" />
            <span>Hospital is {hospital.status}. Operational queues and patient bookings require platform administrator approval.</span>
          </div>
        )}
      </div>

      {/* Tabs */}
      <div className="flex border-b border-slate-200 mb-8 space-x-8 text-sm font-semibold">
        <button
          onClick={() => setTab('overview')}
          className={`pb-3 transition flex items-center space-x-2 ${
            tab === 'overview' ? 'border-b-2 border-teal-600 text-teal-600' : 'text-slate-500 hover:text-slate-900'
          }`}
        >
          <BarChart3 className="w-4 h-4" />
          <span>Analytics & Overview</span>
        </button>
        <button
          onClick={() => setTab('departments')}
          className={`pb-3 transition flex items-center space-x-2 ${
            tab === 'departments' ? 'border-b-2 border-teal-600 text-teal-600' : 'text-slate-500 hover:text-slate-900'
          }`}
        >
          <Layers className="w-4 h-4" />
          <span>Departments ({departments.length})</span>
        </button>
        <button
          onClick={() => setTab('doctors')}
          className={`pb-3 transition flex items-center space-x-2 ${
            tab === 'doctors' ? 'border-b-2 border-teal-600 text-teal-600' : 'text-slate-500 hover:text-slate-900'
          }`}
        >
          <Stethoscope className="w-4 h-4" />
          <span>Doctors ({doctors.length})</span>
        </button>
        <button
          onClick={() => setTab('schedules')}
          className={`pb-3 transition flex items-center space-x-2 ${
            tab === 'schedules' ? 'border-b-2 border-teal-600 text-teal-600' : 'text-slate-500 hover:text-slate-900'
          }`}
        >
          <Calendar className="w-4 h-4" />
          <span>Queues & Schedules ({queues.length})</span>
        </button>
        <button
          onClick={() => setTab('ai')}
          className={`pb-3 transition flex items-center space-x-2 ${
            tab === 'ai' ? 'border-b-2 border-teal-600 text-teal-600' : 'text-slate-500 hover:text-slate-900'
          }`}
        >
          <BrainCircuit className="w-4 h-4 text-teal-600" />
          <span>AI Prediction Engine</span>
        </button>
      </div>

      {/* Tab: Overview */}
      {tab === 'overview' && (
        <div className="space-y-8">
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
            <StatCard title="Total Bookings" value={analytics?.totalBookingsToday || 0} icon={<Users className="w-4 h-4" />} color="teal" />
            <StatCard title="Waiting Patients" value={analytics?.waitingPatients || 0} icon={<Clock className="w-4 h-4" />} color="amber" />
            <StatCard title="Consultations Done" value={analytics?.completedConsultations || 0} icon={<CheckCircle2 className="w-4 h-4" />} color="emerald" />
            <StatCard title="Avg. Consultation" value={`${analytics?.averageActualConsultationMinutes || 10}m`} icon={<Stethoscope className="w-4 h-4" />} color="blue" />
          </div>

          {/* Department Breakdown & Hourly Chart */}
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
            <div className="bg-white p-6 rounded-3xl border border-slate-200 shadow-sm">
              <h3 className="text-base font-bold text-slate-900 mb-4">Department Queue Load</h3>
              {analytics?.departmentStats && analytics.departmentStats.length > 0 ? (
                <div className="h-64">
                  <ResponsiveContainer width="100%" height="100%">
                    <BarChart data={analytics.departmentStats}>
                      <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#f1f5f9" />
                      <XAxis dataKey="departmentName" stroke="#94a3b8" fontSize={11} />
                      <YAxis stroke="#94a3b8" fontSize={11} />
                      <Tooltip />
                      <Bar dataKey="totalBookings" fill="#0d9488" radius={[6, 6, 0, 0]} name="Total Bookings" />
                      <Bar dataKey="completedCount" fill="#10b981" radius={[6, 6, 0, 0]} name="Completed" />
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              ) : (
                <div className="h-64 flex items-center justify-center text-xs text-slate-400">No queue activity yet</div>
              )}
            </div>

            <div className="bg-white p-6 rounded-3xl border border-slate-200 shadow-sm">
              <h3 className="text-base font-bold text-slate-900 mb-4">Predicted vs. Actual Wait Times (Minutes)</h3>
              {analytics?.hourlyStats && analytics.hourlyStats.length > 0 ? (
                <div className="h-64">
                  <ResponsiveContainer width="100%" height="100%">
                    <LineChart data={analytics.hourlyStats}>
                      <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#f1f5f9" />
                      <XAxis dataKey="hour" stroke="#94a3b8" fontSize={11} tickFormatter={(h) => `${h}:00`} />
                      <YAxis stroke="#94a3b8" fontSize={11} />
                      <Tooltip />
                      <Legend />
                      <Line type="monotone" dataKey="avgPredictedWait" stroke="#0d9488" strokeWidth={2} name="Predicted Wait" />
                      <Line type="monotone" dataKey="avgActualWait" stroke="#6366f1" strokeWidth={2} name="Actual Wait" strokeDasharray="4 4" />
                    </LineChart>
                  </ResponsiveContainer>
                </div>
              ) : (
                <div className="h-64 flex items-center justify-center text-xs text-slate-400">No consultation records recorded yet</div>
              )}
            </div>
          </div>
        </div>
      )}

      {/* Tab: Departments */}
      {tab === 'departments' && (
        <div className="space-y-6">
          <div className="flex items-center justify-between">
            <div>
              <h2 className="text-xl font-bold text-slate-900">Hospital Departments</h2>
              <p className="text-xs text-slate-500 mt-0.5">Configure consultation durations and clinical units</p>
            </div>
            <button
              onClick={() => setDeptModalOpen(true)}
              className="px-4 py-2 rounded-xl bg-teal-600 hover:bg-teal-700 text-white font-semibold text-xs flex items-center space-x-1.5 shadow-md shadow-teal-600/20 transition"
            >
              <Plus className="w-4 h-4" />
              <span>Add Department</span>
            </button>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
            {departments.map((d) => (
              <div key={d.id} className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm">
                <div className="flex items-center justify-between">
                  <h3 className="font-bold text-slate-900 text-base">{d.name}</h3>
                  <Badge status={d.isActive ? 'ACTIVE' : 'INACTIVE'} />
                </div>
                <div className="mt-3 flex items-center space-x-2 text-xs">
                  <span className="font-bold text-teal-700 bg-teal-50 px-2 py-0.5 rounded-lg">
                    {d.defaultConsultationMinutes} mins baseline
                  </span>
                  <span className="text-slate-500">{d.doctorCount} doctors</span>
                </div>
                {d.description && <p className="mt-2 text-xs text-slate-500">{d.description}</p>}
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Tab: Doctors */}
      {tab === 'doctors' && (
        <div className="space-y-6">
          <div className="flex items-center justify-between">
            <div>
              <h2 className="text-xl font-bold text-slate-900">Doctor Roster</h2>
              <p className="text-xs text-slate-500 mt-0.5">Assign doctors to departments and manage clinical licenses</p>
            </div>
            <button
              onClick={() => {
                setNewDocDeptIds([]);
                setDoctorModalOpen(true);
              }}
              className="px-4 py-2 rounded-xl bg-teal-600 hover:bg-teal-700 text-white font-semibold text-xs flex items-center space-x-1.5 shadow-md shadow-teal-600/20 transition"
            >
              <Plus className="w-4 h-4" />
              <span>Add Doctor</span>
            </button>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            {doctors.map((doc) => (
              <div key={doc.id} className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm flex items-start justify-between">
                <div>
                  <div className="flex items-center space-x-2">
                    <h3 className="font-bold text-slate-900 text-base">{doc.fullName}</h3>
                    <Badge status={doc.isActive ? 'ACTIVE' : 'INACTIVE'} />
                  </div>
                  <div className="text-xs font-semibold text-teal-700 mt-1">{doc.specialization}</div>
                  <div className="text-[11px] text-slate-400 mt-0.5">License: {doc.licenseNumber}</div>

                  <div className="mt-3 flex flex-wrap gap-1.5">
                    {doc.departments.map((d) => (
                      <span key={d.id} className="px-2 py-0.5 rounded-md bg-slate-100 text-slate-600 text-[10px] font-medium">
                        {d.name}
                      </span>
                    ))}
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Tab: Queues & Schedules */}
      {tab === 'schedules' && (
        <div className="space-y-6">
          <div className="flex items-center justify-between">
            <div>
              <h2 className="text-xl font-bold text-slate-900">Queue Sessions</h2>
              <p className="text-xs text-slate-500 mt-0.5">Active daily queue sessions and patient capacities</p>
            </div>
            <button
              onClick={() => {
                if (doctors.length > 0) setNewQueueDocId(doctors[0].id);
                if (departments.length > 0) setNewQueueDeptId(departments[0].id);
                setQueueModalOpen(true);
              }}
              className="px-4 py-2 rounded-xl bg-teal-600 hover:bg-teal-700 text-white font-semibold text-xs flex items-center space-x-1.5 shadow-md shadow-teal-600/20 transition"
            >
              <Plus className="w-4 h-4" />
              <span>Open Queue Session</span>
            </button>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
            {queues.map((q) => (
              <div key={q.id} className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm">
                <div className="flex items-center justify-between">
                  <h3 className="font-bold text-slate-900 text-base">{q.departmentName}</h3>
                  <Badge status={q.status} />
                </div>
                <div className="text-xs font-semibold text-slate-700 mt-1">{q.doctorName}</div>
                <div className="text-xs text-slate-500 mt-0.5">{q.startTime} - {q.endTime}</div>

                <div className="mt-4 pt-4 border-t border-slate-100 grid grid-cols-3 gap-2 text-center text-xs">
                  <div className="bg-slate-50 p-2 rounded-xl">
                    <span className="block font-bold text-slate-900">{q.currentTokenNumber}</span>
                    <span className="text-[10px] text-slate-500">Current #</span>
                  </div>
                  <div className="bg-slate-50 p-2 rounded-xl">
                    <span className="block font-bold text-slate-900">{q.waitingCount}</span>
                    <span className="text-[10px] text-slate-500">Waiting</span>
                  </div>
                  <div className="bg-slate-50 p-2 rounded-xl">
                    <span className="block font-bold text-slate-900">{q.completedCount}</span>
                    <span className="text-[10px] text-slate-500">Done</span>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Tab: AI Prediction Engine */}
      {tab === 'ai' && (
        <div className="space-y-6">
          <div className="bg-white p-8 rounded-3xl border border-slate-200 shadow-sm">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
              <div>
                <div className="inline-flex items-center space-x-2 text-teal-600 text-xs font-bold uppercase tracking-wider mb-2">
                  <BrainCircuit className="w-4 h-4" />
                  <span>Hospital AI Regression Architecture</span>
                </div>
                <h2 className="text-2xl font-extrabold text-slate-900">Waiting-Time Prediction Calibration</h2>
                <p className="text-xs text-slate-500 mt-1 max-w-xl">
                  QueueLess employs a 3-tier approach: newly registered hospitals start with Mode 1 baseline calculations.
                  Once genuine completed consultations are recorded, an ML.NET FastForest model is evaluated against the baseline using MAE.
                </p>
              </div>

              <div className="flex flex-col gap-2">
                <button
                  disabled={seedingLoading}
                  onClick={handleSeedSyntheticData}
                  className="px-4 py-2.5 rounded-xl border border-teal-200 bg-teal-50 hover:bg-teal-100 text-teal-900 text-xs font-semibold transition disabled:opacity-50"
                >
                  {seedingLoading ? 'Generating...' : 'Seed 60 Demo Records'}
                </button>
                <button
                  disabled={trainingLoading}
                  onClick={handleTrainModel}
                  className="px-4 py-2.5 rounded-xl bg-teal-600 hover:bg-teal-700 text-white text-xs font-semibold shadow-md shadow-teal-600/20 transition disabled:opacity-50 flex items-center justify-center space-x-1.5"
                >
                  <Sparkles className="w-3.5 h-3.5" />
                  <span>{trainingLoading ? 'Training ML.NET...' : 'Train Hospital Model'}</span>
                </button>
              </div>
            </div>

            {aiMessage && (
              <div className="mt-4 p-4 bg-teal-50 border border-teal-200 rounded-2xl text-xs text-teal-900 font-medium">
                {aiMessage}
              </div>
            )}

            {/* Model Metrics Display */}
            <div className="mt-8 pt-8 border-t border-slate-100 grid grid-cols-1 md:grid-cols-4 gap-4">
              <div className="p-4 bg-slate-50 rounded-2xl border border-slate-200">
                <span className="text-[11px] font-semibold text-slate-500 uppercase">Active Status</span>
                <div className="mt-1">
                  <Badge status={aiMetrics?.isActive ? 'ACTIVE' : 'BASELINE'} />
                </div>
                <div className="text-[11px] text-slate-500 mt-1">{aiMetrics?.status || 'Baseline Active'}</div>
              </div>

              <div className="p-4 bg-slate-50 rounded-2xl border border-slate-200">
                <span className="text-[11px] font-semibold text-slate-500 uppercase">Model Version</span>
                <div className="mt-1 text-sm font-bold text-slate-900 font-mono">
                  {aiMetrics?.versionTag || 'None (Mode 1 Baseline)'}
                </div>
                <div className="text-[11px] text-slate-500 mt-1">{aiMetrics?.trainingRecordCount || 0} training samples</div>
              </div>

              <div className="p-4 bg-slate-50 rounded-2xl border border-slate-200">
                <span className="text-[11px] font-semibold text-slate-500 uppercase">Model MAE</span>
                <div className="mt-1 text-xl font-bold text-teal-700">
                  {aiMetrics?.meanAbsoluteError ? `${aiMetrics.meanAbsoluteError} mins` : 'N/A'}
                </div>
                <div className="text-[11px] text-slate-500 mt-1">Mean Absolute Error</div>
              </div>

              <div className="p-4 bg-slate-50 rounded-2xl border border-slate-200">
                <span className="text-[11px] font-semibold text-slate-500 uppercase">Baseline MAE</span>
                <div className="mt-1 text-xl font-bold text-slate-700">
                  {aiMetrics?.baselineMAE ? `${aiMetrics.baselineMAE} mins` : 'N/A'}
                </div>
                <div className="text-[11px] text-slate-500 mt-1">Acceptance Benchmark</div>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Add Department Modal */}
      <Modal isOpen={deptModalOpen} onClose={() => setDeptModalOpen(false)} title="Create New Department">
        <form onSubmit={handleCreateDepartment} className="space-y-4">
          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">Department Name</label>
            <input
              type="text"
              required
              value={newDeptName}
              onChange={(e) => setNewDeptName(e.target.value)}
              placeholder="e.g. Ophthalmology"
              className="w-full p-2.5 text-xs rounded-xl border border-slate-200 focus:outline-none focus:border-teal-500"
            />
          </div>
          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">Default Consultation Minutes</label>
            <input
              type="number"
              min={1}
              max={180}
              value={newDeptDuration}
              onChange={(e) => setNewDeptDuration(Number(e.target.value))}
              className="w-full p-2.5 text-xs rounded-xl border border-slate-200 focus:outline-none focus:border-teal-500"
            />
          </div>
          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">Description</label>
            <textarea
              rows={2}
              value={newDeptDesc}
              onChange={(e) => setNewDeptDesc(e.target.value)}
              placeholder="Brief description of department scope..."
              className="w-full p-2.5 text-xs rounded-xl border border-slate-200 focus:outline-none focus:border-teal-500"
            />
          </div>
          <div className="pt-2 flex justify-end space-x-3">
            <button
              type="button"
              onClick={() => setDeptModalOpen(false)}
              className="px-4 py-2 text-xs font-semibold text-slate-600 hover:bg-slate-100 rounded-xl"
            >
              Cancel
            </button>
            <button
              type="submit"
              className="px-5 py-2 text-xs font-semibold text-white bg-teal-600 hover:bg-teal-700 rounded-xl shadow-md shadow-teal-600/20"
            >
              Save Department
            </button>
          </div>
        </form>
      </Modal>

      {/* Add Doctor Modal */}
      <Modal isOpen={doctorModalOpen} onClose={() => setDoctorModalOpen(false)} title="Add Doctor to Roster">
        <form onSubmit={handleCreateDoctor} className="space-y-3">
          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">Doctor Full Name</label>
            <input
              type="text"
              required
              value={newDocName}
              onChange={(e) => setNewDocName(e.target.value)}
              placeholder="Dr. Sunita Rao"
              className="w-full p-2 text-xs rounded-xl border border-slate-200 focus:outline-none focus:border-teal-500"
            />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1">Specialization</label>
              <input
                type="text"
                required
                value={newDocSpec}
                onChange={(e) => setNewDocSpec(e.target.value)}
                placeholder="Cardiologist"
                className="w-full p-2 text-xs rounded-xl border border-slate-200 focus:outline-none focus:border-teal-500"
              />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1">Medical License #</label>
              <input
                type="text"
                required
                value={newDocLicense}
                onChange={(e) => setNewDocLicense(e.target.value)}
                placeholder="MCI-12345"
                className="w-full p-2 text-xs rounded-xl border border-slate-200 focus:outline-none focus:border-teal-500"
              />
            </div>
          </div>
          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">Assign Departments</label>
            <div className="max-h-28 overflow-y-auto border border-slate-200 rounded-xl p-2 space-y-1">
              {departments.map((d) => (
                <label key={d.id} className="flex items-center space-x-2 text-xs text-slate-700 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={newDocDeptIds.includes(d.id)}
                    onChange={(e) => {
                      if (e.target.checked) setNewDocDeptIds([...newDocDeptIds, d.id]);
                      else setNewDocDeptIds(newDocDeptIds.filter((id) => id !== d.id));
                    }}
                    className="rounded text-teal-600 focus:ring-teal-500"
                  />
                  <span>{d.name}</span>
                </label>
              ))}
            </div>
          </div>

          <div className="pt-2 border-t border-slate-100">
            <span className="text-[11px] font-bold text-slate-700 block mb-1.5">Doctor Portal Account (Optional)</span>
            <div className="grid grid-cols-2 gap-3">
              <input
                type="email"
                placeholder="doctor@hospital.com"
                value={newDocEmail}
                onChange={(e) => setNewDocEmail(e.target.value)}
                className="w-full p-2 text-xs rounded-xl border border-slate-200 focus:outline-none focus:border-teal-500"
              />
              <input
                type="password"
                placeholder="Password"
                value={newDocPassword}
                onChange={(e) => setNewDocPassword(e.target.value)}
                className="w-full p-2 text-xs rounded-xl border border-slate-200 focus:outline-none focus:border-teal-500"
              />
            </div>
          </div>

          <div className="pt-3 flex justify-end space-x-3">
            <button
              type="button"
              onClick={() => setDoctorModalOpen(false)}
              className="px-4 py-2 text-xs font-semibold text-slate-600 hover:bg-slate-100 rounded-xl"
            >
              Cancel
            </button>
            <button
              type="submit"
              className="px-5 py-2 text-xs font-semibold text-white bg-teal-600 hover:bg-teal-700 rounded-xl shadow-md shadow-teal-600/20"
            >
              Save Doctor
            </button>
          </div>
        </form>
      </Modal>

      {/* Open Queue Session Modal */}
      <Modal isOpen={queueModalOpen} onClose={() => setQueueModalOpen(false)} title="Open Live Queue Session">
        <form onSubmit={handleOpenQueue} className="space-y-4">
          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">Select Doctor</label>
            <select
              value={newQueueDocId}
              onChange={(e) => setNewQueueDocId(e.target.value)}
              className="w-full p-2.5 text-xs rounded-xl border border-slate-200 focus:outline-none focus:border-teal-500"
            >
              {doctors.map((doc) => (
                <option key={doc.id} value={doc.id}>{doc.fullName} ({doc.specialization})</option>
              ))}
            </select>
          </div>

          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">Select Department</label>
            <select
              value={newQueueDeptId}
              onChange={(e) => setNewQueueDeptId(e.target.value)}
              className="w-full p-2.5 text-xs rounded-xl border border-slate-200 focus:outline-none focus:border-teal-500"
            >
              {departments.map((d) => (
                <option key={d.id} value={d.id}>{d.name}</option>
              ))}
            </select>
          </div>

          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">Max Patient Capacity</label>
            <input
              type="number"
              min={1}
              max={200}
              value={newQueueCapacity}
              onChange={(e) => setNewQueueCapacity(Number(e.target.value))}
              className="w-full p-2.5 text-xs rounded-xl border border-slate-200 focus:outline-none focus:border-teal-500"
            />
          </div>

          <div className="pt-2 flex justify-end space-x-3">
            <button
              type="button"
              onClick={() => setQueueModalOpen(false)}
              className="px-4 py-2 text-xs font-semibold text-slate-600 hover:bg-slate-100 rounded-xl"
            >
              Cancel
            </button>
            <button
              type="submit"
              className="px-5 py-2 text-xs font-semibold text-white bg-teal-600 hover:bg-teal-700 rounded-xl shadow-md shadow-teal-600/20"
            >
              Open Session
            </button>
          </div>
        </form>
      </Modal>

      {/* Scan & Check-In Patient QR Modal */}
      <Modal isOpen={scanModalOpen} onClose={() => setScanModalOpen(false)} title="Scan & Check-In Patient QR Token">
        <div className="space-y-4">
          {/* Scanner Mode Tabs */}
          <div className="flex border-b border-slate-200 space-x-6 text-xs font-semibold">
            <button
              type="button"
              onClick={() => setScanTab('camera')}
              className={`pb-2.5 transition flex items-center space-x-1.5 ${
                scanTab === 'camera' ? 'border-b-2 border-teal-600 text-teal-600' : 'text-slate-500 hover:text-slate-800'
              }`}
            >
              <Camera className="w-3.5 h-3.5" />
              <span>Live Camera Scanner</span>
            </button>
            <button
              type="button"
              onClick={() => setScanTab('manual')}
              className={`pb-2.5 transition flex items-center space-x-1.5 ${
                scanTab === 'manual' ? 'border-b-2 border-teal-600 text-teal-600' : 'text-slate-500 hover:text-slate-800'
              }`}
            >
              <Keyboard className="w-3.5 h-3.5" />
              <span>Manual / USB Scanner</span>
            </button>
          </div>

          {scanTab === 'camera' ? (
            <QRCameraScanner
              onScan={handleLiveCameraScan}
              defaultStage="AUTO"
              allowedStages={['BUILDING', 'ROOM']}
              onClose={() => setScanModalOpen(false)}
            />
          ) : (
            <div className="space-y-3">
              <p className="text-xs text-slate-500">
                Type the Token # (e.g. 2), Booking ID, or use an external USB handheld barcode scanner.
              </p>

              <div className="flex gap-2">
                <input
                  type="text"
                  placeholder="Enter Token # (e.g. 2, #2) or paste QR payload..."
                  value={scanInput}
                  onChange={(e) => setScanInput(e.target.value)}
                  onKeyDown={(e) => { if (e.key === 'Enter') handleVerifyScan(); }}
                  className="flex-1 p-3 text-xs rounded-xl border border-slate-200 focus:outline-none focus:border-teal-500"
                />
                <button
                  type="button"
                  disabled={scanLoading || !scanInput.trim()}
                  onClick={() => handleVerifyScan()}
                  className="px-4 py-2 text-xs font-semibold text-white bg-teal-600 hover:bg-teal-700 rounded-xl transition shadow-md shadow-teal-600/20 disabled:opacity-50 flex items-center space-x-1"
                >
                  <Search className="w-4 h-4" />
                  <span>{scanLoading ? 'Checking...' : 'Verify'}</span>
                </button>
              </div>

              {/* Quick Shortcuts */}
              <div className="flex items-center space-x-2 text-[11px] text-slate-500">
                <span>Quick Test:</span>
                <button
                  type="button"
                  onClick={() => { setScanInput('2'); handleVerifyScan('2'); }}
                  className="px-2 py-0.5 rounded-md bg-slate-100 hover:bg-teal-50 hover:text-teal-700 font-medium transition"
                >
                  Verify Token #2 (Arun)
                </button>
                <button
                  type="button"
                  onClick={() => { setScanInput('1'); handleVerifyScan('1'); }}
                  className="px-2 py-0.5 rounded-md bg-slate-100 hover:bg-teal-50 hover:text-teal-700 font-medium transition"
                >
                  Verify Token #1
                </button>
              </div>
            </div>
          )}

          {scanError && (
            <div className="p-3 bg-rose-50 border border-rose-200 rounded-xl text-rose-700 text-xs flex items-center space-x-2">
              <AlertCircle className="w-4 h-4 shrink-0" />
              <span>{scanError}</span>
            </div>
          )}

          {scanSuccess && (
            <div className="p-3 bg-emerald-50 border border-emerald-200 rounded-xl text-emerald-800 text-xs flex items-center space-x-2">
              <CheckCircle2 className="w-4 h-4 shrink-0 text-emerald-600" />
              <span className="font-semibold">{scanSuccess}</span>
            </div>
          )}

          {scanResult && (
            <div className="bg-slate-50 rounded-2xl border border-slate-200 p-4 space-y-3">
              <div className="flex items-center justify-between pb-2 border-b border-slate-200">
                <div className="flex items-center space-x-2">
                  <CheckCircle2 className="w-5 h-5 text-emerald-600" />
                  <span className="text-xs font-bold text-emerald-800 uppercase tracking-wider">Valid Hospital Token</span>
                </div>
                <Badge status={scanResult.status} />
              </div>

              <div className="grid grid-cols-2 gap-2 text-xs">
                <div>
                  <span className="text-slate-400">Token Number:</span>
                  <div className="text-2xl font-black text-teal-600">#{scanResult.tokenNumber}</div>
                </div>
                <div>
                  <span className="text-slate-400">Patient Name:</span>
                  <div className="font-bold text-slate-800">{scanResult.patientName}</div>
                  <div className="text-[11px] text-slate-500">{scanResult.patientEmail}</div>
                </div>
                <div>
                  <span className="text-slate-400">Consulting Doctor:</span>
                  <div className="font-semibold text-slate-800">{scanResult.doctorName}</div>
                </div>
                <div>
                  <span className="text-slate-400">Department:</span>
                  <div className="font-semibold text-slate-800">{scanResult.departmentName}</div>
                </div>
              </div>

              {/* Two-Stage Check-in Progress */}
              <div className="bg-white rounded-xl border border-slate-200 p-3 space-y-2 text-xs">
                <span className="font-bold text-slate-700 block">Two-Stage Check-In Status</span>
                <div className="flex items-center justify-between text-xs">
                  <span className="text-slate-500">Stage 1: Building Entrance</span>
                  <span className={`font-bold px-2 py-0.5 rounded-md text-[11px] ${scanResult.isBuildingCheckedIn ? 'bg-emerald-100 text-emerald-800' : 'bg-amber-100 text-amber-800'}`}>
                    {scanResult.isBuildingCheckedIn ? 'Checked In' : 'Pending'}
                  </span>
                </div>
                <div className="flex items-center justify-between text-xs">
                  <span className="text-slate-500">Stage 2: Doctor Consultation Room</span>
                  <span className={`font-bold px-2 py-0.5 rounded-md text-[11px] ${
                    scanResult.status === 'COMPLETED'
                      ? 'bg-emerald-100 text-emerald-800'
                      : scanResult.isDoctorRoomCheckedIn
                      ? 'bg-teal-100 text-teal-800'
                      : 'bg-slate-100 text-slate-600'
                  }`}>
                    {scanResult.status === 'COMPLETED'
                      ? 'Consultation Completed'
                      : scanResult.isDoctorRoomCheckedIn
                      ? 'Admitted / Serving'
                      : 'Pending Entry'}
                  </span>
                </div>
              </div>

              <div className="pt-2 grid grid-cols-2 gap-2">
                <button
                  type="button"
                  disabled={checkInLoading || scanResult.isBuildingCheckedIn || scanResult.status === 'COMPLETED'}
                  onClick={handleBuildingCheckIn}
                  className="py-2.5 px-3 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white font-bold text-xs shadow-md shadow-emerald-600/20 transition flex items-center justify-center space-x-1.5 disabled:opacity-50"
                >
                  <UserCheck className="w-4 h-4" />
                  <span>{scanResult.isBuildingCheckedIn ? '1. Building Checked In' : '1. Check-In Building'}</span>
                </button>
                <button
                  type="button"
                  disabled={checkInLoading || scanResult.isDoctorRoomCheckedIn || scanResult.status === 'COMPLETED'}
                  onClick={handleRoomCheckIn}
                  className="py-2.5 px-3 rounded-xl bg-teal-600 hover:bg-teal-700 text-white font-bold text-xs shadow-md shadow-teal-600/20 transition flex items-center justify-center space-x-1.5 disabled:opacity-50"
                >
                  <Stethoscope className="w-4 h-4" />
                  <span>
                    {scanResult.status === 'COMPLETED'
                      ? 'Completed'
                      : scanResult.isDoctorRoomCheckedIn
                      ? '2. Room Admitted'
                      : '2. Admit to Room'}
                  </span>
                </button>
              </div>
            </div>
          )}

          {/* Bottom Close Button for Manual Mode or Done State */}
          {scanTab === 'manual' && (
            <div className="pt-3 border-t border-slate-100 flex justify-end">
              <button
                type="button"
                onClick={() => setScanModalOpen(false)}
                className="w-full sm:w-auto px-5 py-2.5 rounded-xl bg-slate-800 hover:bg-slate-900 text-white font-bold text-xs flex items-center justify-center space-x-2 transition shadow-sm cursor-pointer"
              >
                <X className="w-4 h-4 stroke-[2.5]" />
                <span>Done / Close</span>
              </button>
            </div>
          )}
        </div>
      </Modal>

      {/* Floating AI & Database Chatbot Widget */}
      <HospitalAiFloatingChat />
    </div>
  );
};
