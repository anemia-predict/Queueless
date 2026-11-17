import React, { useState, useEffect } from 'react';
import { api } from '../../services/api';
import { Hospital, PlatformAnalytics, AIModelMetrics } from '../../types';
import { Badge } from '../../components/Badge';
import { StatCard } from '../../components/StatCard';
import { Modal } from '../../components/Modal';
import {
  ShieldCheck, Building2, Users, Calendar, BrainCircuit, CheckCircle2,
  XCircle, AlertTriangle, Sparkles, Filter
} from 'lucide-react';

export const PlatformDashboard: React.FC = () => {
  const [hospitals, setHospitals] = useState<Hospital[]>([]);
  const [analytics, setAnalytics] = useState<PlatformAnalytics | null>(null);
  const [models, setModels] = useState<AIModelMetrics[]>([]);
  const [loading, setLoading] = useState(true);
  const [filterStatus, setFilterStatus] = useState<string>('ALL');

  // Decision Modal
  const [rejectModalOpen, setRejectModalOpen] = useState(false);
  const [selectedHospId, setSelectedHospId] = useState<string>('');
  const [rejectionReason, setRejectionReason] = useState('');
  const [actionLoading, setActionLoading] = useState(false);
  const [trainingSharedLoading, setTrainingSharedLoading] = useState(false);
  const [platformMessage, setPlatformMessage] = useState('');

  const loadData = async () => {
    try {
      const [allHosp, an, mdls] = await Promise.all([
        api.getAllHospitals(filterStatus === 'ALL' ? undefined : filterStatus),
        api.getPlatformAnalytics(),
        api.getAllPlatformModels(),
      ]);
      setHospitals(allHosp);
      setAnalytics(an);
      setModels(mdls);
    } catch (err) {
      console.error('Failed to load platform data:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, [filterStatus]);

  const handleApprove = async (id: string) => {
    setActionLoading(true);
    try {
      await api.approveHospital(id);
      setPlatformMessage('Hospital approved successfully!');
      await loadData();
    } catch (err: any) {
      alert(err.response?.data?.message || 'Approval failed');
    } finally {
      setActionLoading(false);
    }
  };

  const handleOpenRejectModal = (id: string) => {
    setSelectedHospId(id);
    setRejectionReason('');
    setRejectModalOpen(true);
  };

  const handleConfirmReject = async () => {
    if (!selectedHospId) return;
    setActionLoading(true);
    try {
      await api.rejectHospital(selectedHospId, rejectionReason);
      setRejectModalOpen(false);
      setPlatformMessage('Hospital rejected.');
      await loadData();
    } catch (err: any) {
      alert(err.response?.data?.message || 'Rejection failed');
    } finally {
      setActionLoading(false);
    }
  };

  const handleSuspend = async (id: string) => {
    if (!confirm('Suspend this hospital? Patients will not be able to book queue tokens.')) return;
    setActionLoading(true);
    try {
      await api.suspendHospital(id, 'Suspended by platform administrator');
      setPlatformMessage('Hospital suspended.');
      await loadData();
    } catch (err: any) {
      alert(err.response?.data?.message || 'Suspension failed');
    } finally {
      setActionLoading(false);
    }
  };

  const handleTrainSharedModel = async () => {
    setTrainingSharedLoading(true);
    setPlatformMessage('');
    try {
      const res = await api.trainSharedModel();
      setPlatformMessage(`Multi-hospital Shared Model trained! Version: ${res.versionTag}, MAE: ${res.meanAbsoluteError} mins (Baseline MAE: ${res.baselineMAE} mins). Status: ${res.status}`);
      await loadData();
    } catch (err: any) {
      setPlatformMessage(`Error training shared model: ${err.response?.data?.message || 'Failed'}`);
    } finally {
      setTrainingSharedLoading(false);
    }
  };

  if (loading) {
    return (
      <div className="max-w-7xl mx-auto px-4 py-20 text-center">
        <div className="w-10 h-10 border-4 border-teal-600 border-t-transparent rounded-full animate-spin mx-auto mb-3" />
        <p className="text-sm text-slate-500">Loading platform oversight...</p>
      </div>
    );
  }

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-10">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-8">
        <div>
          <div className="inline-flex items-center space-x-1.5 text-xs font-bold text-amber-700 bg-amber-50 px-2.5 py-0.5 rounded-full border border-amber-200 mb-1">
            <ShieldCheck className="w-3.5 h-3.5" />
            <span>Platform Governance Console</span>
          </div>
          <h1 className="text-3xl font-extrabold text-slate-900 tracking-tight">Platform Administration</h1>
          <p className="text-xs text-slate-500 mt-0.5">Manage hospital onboarding, tenant approvals, and shared AI models.</p>
        </div>

        <button
          disabled={trainingSharedLoading}
          onClick={handleTrainSharedModel}
          className="px-5 py-2.5 rounded-xl bg-teal-600 hover:bg-teal-700 text-white font-semibold text-xs shadow-md shadow-teal-600/20 transition flex items-center space-x-2 disabled:opacity-50"
        >
          <Sparkles className="w-4 h-4" />
          <span>{trainingSharedLoading ? 'Training Shared Model...' : 'Train Multi-Hospital Shared Model'}</span>
        </button>
      </div>

      {platformMessage && (
        <div className="mb-6 p-4 bg-teal-50 border border-teal-200 rounded-2xl text-xs font-semibold text-teal-900">
          {platformMessage}
        </div>
      )}

      {/* Metrics Row */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 mb-10">
        <StatCard title="Total Hospitals" value={analytics?.totalRegisteredHospitals || 0} icon={<Building2 className="w-4 h-4" />} color="blue" />
        <StatCard title="Pending Review" value={analytics?.pendingApprovals || 0} icon={<AlertTriangle className="w-4 h-4" />} color="amber" />
        <StatCard title="Approved Tenants" value={analytics?.approvedHospitals || 0} icon={<CheckCircle2 className="w-4 h-4" />} color="emerald" />
        <StatCard title="Total Patients" value={analytics?.totalPatients || 0} icon={<Users className="w-4 h-4" />} color="teal" />
      </div>

      {/* Hospital Registrations Oversight */}
      <div className="bg-white rounded-3xl border border-slate-200 overflow-hidden shadow-sm mb-10">
        <div className="p-6 border-b border-slate-100 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div>
            <h2 className="text-base font-bold text-slate-900">Hospital Tenants & Onboarding Requests</h2>
            <p className="text-xs text-slate-500 mt-0.5">Approve or suspend hospital operational privileges</p>
          </div>

          {/* Filter Pills */}
          <div className="flex bg-slate-100 p-1 rounded-xl text-xs font-semibold">
            {['ALL', 'PENDING', 'APPROVED', 'SUSPENDED'].map((st) => (
              <button
                key={st}
                onClick={() => setFilterStatus(st)}
                className={`px-3 py-1 rounded-lg transition ${
                  filterStatus === st ? 'bg-white text-teal-700 shadow-sm' : 'text-slate-500 hover:text-slate-900'
                }`}
              >
                {st}
              </button>
            ))}
          </div>
        </div>

        {hospitals.length === 0 ? (
          <div className="p-8 text-center text-xs text-slate-400">No hospitals matching the filter.</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs">
              <thead className="bg-slate-50 text-slate-500 uppercase tracking-wider font-semibold border-b border-slate-100">
                <tr>
                  <th className="py-3 px-4">Hospital Name</th>
                  <th className="py-3 px-4">Registration #</th>
                  <th className="py-3 px-4">City</th>
                  <th className="py-3 px-4">Contact</th>
                  <th className="py-3 px-4">Status</th>
                  <th className="py-3 px-4 text-right">Review Action</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {hospitals.map((h) => (
                  <tr key={h.id} className="hover:bg-slate-50 transition">
                    <td className="py-3.5 px-4 font-bold text-slate-900">{h.name}</td>
                    <td className="py-3.5 px-4 font-mono text-slate-600">{h.registrationNumber}</td>
                    <td className="py-3.5 px-4 text-slate-600">{h.city}</td>
                    <td className="py-3.5 px-4 text-slate-500">
                      <div>{h.contactEmail}</div>
                      <div className="text-[10px]">{h.contactPhone}</div>
                    </td>
                    <td className="py-3.5 px-4">
                      <Badge status={h.status} />
                    </td>
                    <td className="py-3.5 px-4 text-right space-x-2">
                      {h.status === 'PENDING' && (
                        <>
                          <button
                            onClick={() => handleApprove(h.id)}
                            disabled={actionLoading}
                            className="px-3 py-1.5 rounded-lg bg-emerald-600 hover:bg-emerald-700 text-white font-semibold text-[11px] shadow-sm transition"
                          >
                            Approve
                          </button>
                          <button
                            onClick={() => handleOpenRejectModal(h.id)}
                            disabled={actionLoading}
                            className="px-2.5 py-1.5 rounded-lg border border-rose-200 text-rose-700 hover:bg-rose-50 font-semibold text-[11px] transition"
                          >
                            Reject
                          </button>
                        </>
                      )}

                      {h.status === 'APPROVED' && (
                        <button
                          onClick={() => handleSuspend(h.id)}
                          disabled={actionLoading}
                          className="px-2.5 py-1.5 rounded-lg border border-amber-300 text-amber-800 hover:bg-amber-50 font-semibold text-[11px] transition"
                        >
                          Suspend
                        </button>
                      )}

                      {h.status === 'SUSPENDED' && (
                        <button
                          onClick={() => handleApprove(h.id)}
                          disabled={actionLoading}
                          className="px-3 py-1.5 rounded-lg bg-emerald-600 hover:bg-emerald-700 text-white font-semibold text-[11px] transition"
                        >
                          Re-activate
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Active AI Models across platform */}
      <div className="bg-white rounded-3xl border border-slate-200 overflow-hidden shadow-sm">
        <div className="p-6 border-b border-slate-100 flex items-center justify-between">
          <div className="flex items-center space-x-2">
            <BrainCircuit className="w-5 h-5 text-teal-600" />
            <h2 className="text-base font-bold text-slate-900">Registered ML.NET Models</h2>
          </div>
          <span className="text-xs text-slate-500">{models.length} model versions tracked</span>
        </div>

        {models.length === 0 ? (
          <div className="p-8 text-center text-xs text-slate-400">No trained models registered yet. Newly added hospitals operate on Mode 1 baseline calculations.</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs">
              <thead className="bg-slate-50 text-slate-500 uppercase tracking-wider font-semibold border-b border-slate-100">
                <tr>
                  <th className="py-3 px-4">Version Tag</th>
                  <th className="py-3 px-4">Model Type</th>
                  <th className="py-3 px-4">Training Records</th>
                  <th className="py-3 px-4">Model MAE</th>
                  <th className="py-3 px-4">Baseline MAE</th>
                  <th className="py-3 px-4">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {models.map((m, idx) => (
                  <tr key={idx} className="hover:bg-slate-50 transition">
                    <td className="py-3 px-4 font-mono font-semibold text-slate-800">{m.versionTag}</td>
                    <td className="py-3 px-4 font-semibold text-teal-700">{m.modelType}</td>
                    <td className="py-3 px-4 text-slate-600">{m.trainingRecordCount}</td>
                    <td className="py-3 px-4 font-bold text-slate-900">{m.meanAbsoluteError} mins</td>
                    <td className="py-3 px-4 text-slate-500">{m.baselineMAE} mins</td>
                    <td className="py-3 px-4">
                      <Badge status={m.isActive ? 'ACTIVE' : 'INACTIVE'} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Reject Reason Modal */}
      <Modal isOpen={rejectModalOpen} onClose={() => setRejectModalOpen(false)} title="Reject Hospital Registration">
        <div className="space-y-4">
          <p className="text-xs text-slate-600">Please provide a reason for rejecting this hospital onboarding request:</p>
          <textarea
            rows={3}
            value={rejectionReason}
            onChange={(e) => setRejectionReason(e.target.value)}
            placeholder="Incomplete regulatory documentation or unverified license number..."
            className="w-full p-2.5 text-xs rounded-xl border border-slate-200 focus:outline-none focus:border-rose-500"
          />
          <div className="pt-2 flex justify-end space-x-3">
            <button
              onClick={() => setRejectModalOpen(false)}
              className="px-4 py-2 text-xs font-semibold text-slate-600 hover:bg-slate-100 rounded-xl"
            >
              Cancel
            </button>
            <button
              disabled={actionLoading}
              onClick={handleConfirmReject}
              className="px-5 py-2 text-xs font-semibold text-white bg-rose-600 hover:bg-rose-700 rounded-xl shadow-md transition disabled:opacity-50"
            >
              Confirm Rejection
            </button>
          </div>
        </div>
      </Modal>
    </div>
  );
};
