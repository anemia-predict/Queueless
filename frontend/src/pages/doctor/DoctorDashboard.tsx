import React, { useState, useEffect } from 'react';
import { api } from '../../services/api';
import { getSignalRConnection, startSignalR } from '../../services/signalr';
import { QueueSession, Booking } from '../../types';
import { useAuth } from '../../contexts/AuthContext';
import { Badge } from '../../components/Badge';
import { Modal } from '../../components/Modal';
import { QRCameraScanner } from '../../components/QRCameraScanner';
import { Stethoscope, Users, Play, CheckCircle2, UserX, Pause, PlayCircle, Clock, FileText, AlertCircle, PhoneCall, Building, QrCode } from 'lucide-react';

const CountdownBadge: React.FC<{ callTime?: string; onExpire?: () => void }> = ({ callTime, onExpire }) => {
  const [secondsLeft, setSecondsLeft] = useState<number>(() => {
    if (!callTime) return 120;
    const diff = Math.floor((new Date(callTime).getTime() + 120000 - Date.now()) / 1000);
    return Math.max(0, diff);
  });

  useEffect(() => {
    const timer = setInterval(() => {
      if (!callTime) return;
      const diff = Math.floor((new Date(callTime).getTime() + 120000 - Date.now()) / 1000);
      if (diff <= 0) {
        setSecondsLeft(0);
        clearInterval(timer);
        onExpire?.();
      } else {
        setSecondsLeft(diff);
      }
    }, 1000);
    return () => clearInterval(timer);
  }, [callTime, onExpire]);

  const mins = Math.floor(secondsLeft / 60);
  const secs = secondsLeft % 60;

  return (
    <span className={`inline-flex items-center space-x-1 px-2.5 py-1 rounded-lg text-xs font-bold ${
      secondsLeft < 30 ? 'bg-rose-100 text-rose-700 animate-pulse' : 'bg-amber-100 text-amber-800'
    }`}>
      <Clock className="w-3.5 h-3.5" />
      <span>{secondsLeft <= 0 ? 'Timeout (Forfeited)' : `${String(mins).padStart(2, '0')}:${String(secs).padStart(2, '0')} to enter room`}</span>
    </span>
  );
};

export const DoctorDashboard: React.FC = () => {
  const { user } = useAuth();
  const [queues, setQueues] = useState<QueueSession[]>([]);
  const [selectedQueue, setSelectedQueue] = useState<QueueSession | null>(null);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  const [completeModalOpen, setCompleteModalOpen] = useState(false);
  const [doctorScanModalOpen, setDoctorScanModalOpen] = useState(false);
  const [consultationNotes, setConsultationNotes] = useState('');
  const [activeServingBooking, setActiveServingBooking] = useState<Booking | null>(null);

  const handleDoctorRoomCameraScan = async (decodedText: string) => {
    try {
      const verify = await api.verifyBooking(decodedText);
      const res = await api.roomCheckIn(verify.id);
      alert(res.message || `Patient ${verify.patientName} (Token #${verify.tokenNumber}) admitted into consultation room!`);
      setDoctorScanModalOpen(false);
      await fetchQueues();
    } catch (err: any) {
      alert(err.response?.data?.message || 'Error admitting patient via QR.');
    }
  };

  const fetchQueues = async () => {
    try {
      const data = await api.getQueues();
      setQueues(data);
      if (data.length > 0) {
        const current = selectedQueue ? data.find((q) => q.id === selectedQueue.id) || data[0] : data[0];
        setSelectedQueue(current);
        setActiveServingBooking(current.currentServingBooking || null);
      }
    } catch (err) {
      console.error('Failed to load queues:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchQueues();

    let conn: any;
    startSignalR().then((c) => {
      conn = c;
      conn.on('QueueUpdated', () => fetchQueues());
      conn.on('TokenCalled', () => fetchQueues());
      conn.on('ConsultationStarted', () => fetchQueues());
      conn.on('ConsultationCompleted', () => fetchQueues());
    });

    return () => {
      if (conn) {
        conn.off('QueueUpdated');
        conn.off('TokenCalled');
        conn.off('ConsultationStarted');
        conn.off('ConsultationCompleted');
      }
    };
  }, []);

  const handleCallNext = async () => {
    if (!selectedQueue) return;
    setActionLoading(true);
    try {
      const res = await api.callNext(selectedQueue.id);
      if (!res.success) {
        alert(res.message);
      }
      await fetchQueues();
    } catch (err: any) {
      alert(err.response?.data?.message || 'Error calling next patient.');
    } finally {
      setActionLoading(false);
    }
  };

  const handleStartConsultation = async (bookingId: string) => {
    setActionLoading(true);
    try {
      await api.startConsultation(bookingId);
      await fetchQueues();
    } catch (err: any) {
      alert(err.response?.data?.message || 'Error starting consultation.');
    } finally {
      setActionLoading(false);
    }
  };

  const handleOpenCompleteModal = (booking: Booking) => {
    setActiveServingBooking(booking);
    setConsultationNotes('');
    setCompleteModalOpen(true);
  };

  const handleConfirmComplete = async () => {
    if (!activeServingBooking) return;
    setActionLoading(true);
    try {
      await api.completeConsultation(activeServingBooking.id, consultationNotes);
      setCompleteModalOpen(false);
      await fetchQueues();
    } catch (err: any) {
      alert(err.response?.data?.message || 'Error completing consultation.');
    } finally {
      setActionLoading(false);
    }
  };

  const handleMarkNoShow = async (bookingId: string) => {
    if (!confirm('Mark this patient as NO_SHOW?')) return;
    setActionLoading(true);
    try {
      await api.markNoShow(bookingId);
      await fetchQueues();
    } catch (err: any) {
      alert(err.response?.data?.message || 'Error marking no-show.');
    } finally {
      setActionLoading(false);
    }
  };

  const handleRoomCheckIn = async (bookingId: string) => {
    setActionLoading(true);
    try {
      const res = await api.roomCheckIn(bookingId);
      alert(res.message || 'Patient successfully admitted into consultation room!');
      await fetchQueues();
    } catch (err: any) {
      alert(err.response?.data?.message || 'Error admitting patient.');
      await fetchQueues();
    } finally {
      setActionLoading(false);
    }
  };

  const handleForfeitExpired = async () => {
    if (!selectedQueue) return;
    try {
      await api.forfeitExpiredTokens(selectedQueue.id);
      await fetchQueues();
    } catch (err) {
      console.error('Failed to auto-forfeit expired tokens:', err);
    }
  };

  const handleTogglePause = async () => {
    if (!selectedQueue) return;
    setActionLoading(true);
    try {
      if (selectedQueue.status === 'PAUSED') {
        await api.resumeQueue(selectedQueue.id);
      } else {
        await api.pauseQueue(selectedQueue.id);
      }
      await fetchQueues();
    } catch (err: any) {
      alert(err.response?.data?.message || 'Error updating queue status.');
    } finally {
      setActionLoading(false);
    }
  };

  if (loading) {
    return (
      <div className="max-w-7xl mx-auto px-4 py-20 text-center">
        <div className="w-10 h-10 border-4 border-teal-600 border-t-transparent rounded-full animate-spin mx-auto mb-3" />
        <p className="text-sm text-slate-500">Loading doctor console...</p>
      </div>
    );
  }

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-10">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-8">
        <div>
          <h1 className="text-2xl sm:text-3xl font-extrabold text-slate-900 tracking-tight">
            Doctor Queue Console
          </h1>
          <p className="text-xs text-slate-500 mt-1">
            Doctor: <span className="font-semibold text-slate-700">{user?.fullName}</span> | All consultations are recorded to train the hospital ML prediction model.
          </p>
        </div>

        {selectedQueue && (
          <div className="flex items-center space-x-2">
            <button
              onClick={() => setDoctorScanModalOpen(true)}
              className="px-4 py-2 rounded-xl text-xs font-semibold bg-teal-600 hover:bg-teal-700 text-white flex items-center space-x-1.5 transition shadow-sm"
            >
              <QrCode className="w-4 h-4" />
              <span>Scan Patient Door QR</span>
            </button>

            <button
              onClick={handleTogglePause}
              disabled={actionLoading}
              className={`px-4 py-2 rounded-xl text-xs font-semibold flex items-center space-x-1.5 transition ${
                selectedQueue.status === 'PAUSED'
                  ? 'bg-emerald-600 hover:bg-emerald-700 text-white'
                  : 'bg-purple-50 hover:bg-purple-100 text-purple-700 border border-purple-200'
              }`}
            >
              {selectedQueue.status === 'PAUSED' ? (
                <>
                  <PlayCircle className="w-4 h-4" />
                  <span>Resume Queue</span>
                </>
              ) : (
                <>
                  <Pause className="w-4 h-4" />
                  <span>Pause Queue</span>
                </>
              )}
            </button>
          </div>
        )}
      </div>

      {queues.length === 0 ? (
        <div className="bg-white rounded-3xl border border-slate-200 p-8 text-center">
          <Stethoscope className="w-12 h-12 text-slate-300 mx-auto mb-3" />
          <h3 className="text-base font-bold text-slate-800">No Queue Sessions Found</h3>
          <p className="text-xs text-slate-500 mt-1">Hospital administrator can open a queue session from the management dashboard.</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
          {/* Active Serving Banner & Operations (Left 2 cols) */}
          <div className="lg:col-span-2 space-y-6">
            {/* Live Serving Banner */}
            <div className="bg-gradient-to-br from-slate-900 to-slate-800 text-white rounded-3xl p-8 shadow-xl relative overflow-hidden">
              <div className="flex items-center justify-between pb-6 border-b border-slate-700">
                <div className="flex items-center space-x-2">
                  <span className="w-3 h-3 rounded-full bg-teal-400 animate-ping" />
                  <span className="text-xs font-bold text-teal-400 uppercase tracking-wider">Currently Serving</span>
                </div>
                <Badge status={selectedQueue?.status || 'OPEN'} />
              </div>

              <div className="mt-6 grid grid-cols-1 sm:grid-cols-2 gap-6 items-center">
                <div>
                  <span className="text-xs font-medium text-slate-400 uppercase">Token Number</span>
                  <div className="text-6xl font-black text-teal-400 tracking-tight mt-1">
                    {selectedQueue?.currentServingBooking ? `#${selectedQueue.currentServingBooking.tokenNumber}` : 'None'}
                  </div>
                  <div className="text-sm font-semibold text-slate-200 mt-2">
                    {selectedQueue?.currentServingBooking ? selectedQueue.currentServingBooking.patientName : 'No active consultation'}
                  </div>
                </div>

                <div className="flex flex-col gap-3">
                  {selectedQueue?.currentServingBooking ? (
                    <button
                      onClick={() => handleOpenCompleteModal(selectedQueue.currentServingBooking!)}
                      disabled={actionLoading}
                      className="w-full py-3 px-4 rounded-xl bg-teal-500 hover:bg-teal-400 text-slate-900 font-bold text-xs shadow-lg shadow-teal-500/20 transition flex items-center justify-center space-x-2"
                    >
                      <CheckCircle2 className="w-4 h-4" />
                      <span>Complete Consultation</span>
                    </button>
                  ) : (
                    <button
                      onClick={handleCallNext}
                      disabled={actionLoading || selectedQueue?.status === 'PAUSED'}
                      className="w-full py-3.5 px-4 rounded-xl bg-teal-500 hover:bg-teal-400 text-slate-900 font-bold text-xs shadow-lg shadow-teal-500/20 transition flex items-center justify-center space-x-2 disabled:opacity-50"
                    >
                      <PhoneCall className="w-4 h-4" />
                      <span>CALL NEXT PATIENT</span>
                    </button>
                  )}
                </div>
              </div>
            </div>

            {/* Waiting Queue Table */}
            <div className="bg-white rounded-3xl border border-slate-200 overflow-hidden shadow-sm">
              <div className="p-6 border-b border-slate-100 flex items-center justify-between">
                <div>
                  <h3 className="font-bold text-slate-900 text-base">Waiting & Called Queue</h3>
                  <p className="text-xs text-slate-500 mt-0.5">Patients queued for this session in token order</p>
                </div>
                <span className="text-xs font-semibold px-2.5 py-1 bg-teal-50 text-teal-800 rounded-lg">
                  {selectedQueue?.waitingBookings.length || 0} waiting
                </span>
              </div>

              {!selectedQueue || selectedQueue.waitingBookings.length === 0 ? (
                <div className="p-8 text-center text-xs text-slate-400">No patients currently waiting in queue.</div>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full text-left text-xs">
                    <thead className="bg-slate-50 text-slate-500 uppercase tracking-wider font-semibold border-b border-slate-100">
                      <tr>
                        <th className="py-3 px-4">Token</th>
                        <th className="py-3 px-4">Patient Name</th>
                        <th className="py-3 px-4">Status & 2-Min Timer</th>
                        <th className="py-3 px-4">Check-In Stages</th>
                        <th className="py-3 px-4 text-right">Actions</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-100">
                      {selectedQueue.waitingBookings.map((b) => (
                        <tr key={b.id} className="hover:bg-slate-50 transition">
                          <td className="py-3 px-4 font-bold text-teal-700">#{b.tokenNumber}</td>
                          <td className="py-3 px-4 font-semibold text-slate-800">{b.patientName}</td>
                          <td className="py-3 px-4 space-y-1">
                            <div><Badge status={b.status} /></div>
                            {b.status === 'CALLED' && (
                              <div>
                                <CountdownBadge callTime={b.actualCallTime} onExpire={handleForfeitExpired} />
                              </div>
                            )}
                          </td>
                          <td className="py-3 px-4 space-y-1">
                            <div className="flex items-center space-x-1.5 text-[11px]">
                              <span className={`w-2 h-2 rounded-full ${b.isBuildingCheckedIn ? 'bg-emerald-500' : 'bg-slate-300'}`} />
                              <span className={b.isBuildingCheckedIn ? 'text-emerald-700 font-semibold' : 'text-slate-400'}>
                                {b.isBuildingCheckedIn ? '1. Building In' : '1. Building Pending'}
                              </span>
                            </div>
                            <div className="flex items-center space-x-1.5 text-[11px]">
                              <span className={`w-2 h-2 rounded-full ${b.isDoctorRoomCheckedIn ? 'bg-teal-500' : 'bg-slate-300'}`} />
                              <span className={b.isDoctorRoomCheckedIn ? 'text-teal-700 font-semibold' : 'text-slate-400'}>
                                {b.isDoctorRoomCheckedIn ? '2. In Room' : '2. Room Pending'}
                              </span>
                            </div>
                          </td>
                          <td className="py-3 px-4 text-right space-x-2">
                            {b.status === 'CALLED' ? (
                              <button
                                onClick={() => handleRoomCheckIn(b.id)}
                                disabled={actionLoading}
                                className="px-3 py-1.5 rounded-lg bg-teal-600 hover:bg-teal-700 text-white font-semibold text-[11px] transition shadow-xs"
                                title="Verify patient is entering room and start consultation"
                              >
                                Admit & Start
                              </button>
                            ) : (
                              <button
                                onClick={handleCallNext}
                                disabled={actionLoading}
                                className="px-3 py-1.5 rounded-lg bg-slate-100 hover:bg-slate-200 text-slate-700 font-semibold text-[11px] transition"
                              >
                                Call Patient
                              </button>
                            )}

                            <button
                              onClick={() => handleMarkNoShow(b.id)}
                              disabled={actionLoading}
                              className="px-2.5 py-1.5 rounded-lg text-rose-600 hover:bg-rose-50 font-semibold text-[11px] transition"
                            >
                              Forfeit / No-Show
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          </div>

          {/* Sessions & Stats Sidebar */}
          <div className="space-y-6">
            <h3 className="font-bold text-slate-900 text-base">Select Session</h3>
            <div className="space-y-2">
              {queues.map((q) => (
                <div
                  key={q.id}
                  onClick={() => {
                    setSelectedQueue(q);
                    setActiveServingBooking(q.currentServingBooking || null);
                  }}
                  className={`p-4 rounded-2xl border cursor-pointer transition ${
                    selectedQueue?.id === q.id
                      ? 'bg-teal-50/60 border-teal-500 shadow-sm'
                      : 'bg-white border-slate-200 hover:border-slate-300'
                  }`}
                >
                  <div className="flex items-center justify-between">
                    <span className="font-bold text-slate-900 text-xs">{q.departmentName}</span>
                    <Badge status={q.status} />
                  </div>
                  <div className="text-xs text-slate-500 mt-1">{q.doctorName}</div>
                  <div className="mt-3 pt-3 border-t border-slate-100 flex items-center justify-between text-[11px] text-slate-500">
                    <span>{q.waitingCount} waiting</span>
                    <span>{q.completedCount} completed</span>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}

      {/* Complete Consultation Modal */}
      <Modal
        isOpen={completeModalOpen}
        onClose={() => setCompleteModalOpen(false)}
        title={`Complete Consultation – Token #${activeServingBooking?.tokenNumber}`}
      >
        <div className="space-y-4">
          <div className="p-3 bg-teal-50 border border-teal-200 rounded-xl text-xs text-teal-800">
            Consultation completion calculates the exact duration (Start to End) and saves it in PostgreSQL to feed the AI waiting-time prediction engine.
          </div>

          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">Clinical Notes (Optional)</label>
            <textarea
              rows={3}
              value={consultationNotes}
              onChange={(e) => setConsultationNotes(e.target.value)}
              placeholder="Diagnosis summary, prescription advised, follow-up notes..."
              className="w-full p-2.5 text-xs rounded-xl border border-slate-200 focus:outline-none focus:border-teal-500"
            />
          </div>

          <div className="pt-2 flex justify-end space-x-3">
            <button
              onClick={() => setCompleteModalOpen(false)}
              className="px-4 py-2 text-xs font-semibold text-slate-600 hover:bg-slate-100 rounded-xl transition"
            >
              Cancel
            </button>
            <button
              disabled={actionLoading}
              onClick={handleConfirmComplete}
              className="px-5 py-2 text-xs font-semibold text-white bg-teal-600 hover:bg-teal-700 rounded-xl shadow-md shadow-teal-600/20 transition disabled:opacity-50"
            >
              {actionLoading ? 'Saving...' : 'Confirm & Save Consultation'}
            </button>
          </div>
        </div>
      </Modal>

      {/* Doctor Door Live QR Camera Scanner Modal */}
      <Modal
        isOpen={doctorScanModalOpen}
        onClose={() => setDoctorScanModalOpen(false)}
        title="Doctor Consultation Door – Patient Admission"
      >
        <div className="space-y-4">
          {(() => {
            const calledPatient = selectedQueue?.waitingBookings?.find((b) => b.status === 'CALLED');
            if (!calledPatient) return null;
            return (
              <div className="p-4 bg-teal-50 border border-teal-200 rounded-2xl flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3 shadow-xs">
                <div>
                  <div className="flex items-center space-x-1.5 text-teal-700 font-bold text-xs uppercase tracking-wide">
                    <span className="w-2 h-2 rounded-full bg-teal-500 animate-pulse" />
                    <span>Called Patient at Door</span>
                  </div>
                  <div className="text-sm font-extrabold text-slate-900 mt-0.5">
                    Token #{calledPatient.tokenNumber} – {calledPatient.patientName}
                  </div>
                  <p className="text-[11px] text-slate-500">Patient has been called and is entering consultation chamber.</p>
                </div>
                <button
                  type="button"
                  onClick={async () => {
                    await handleRoomCheckIn(calledPatient.id);
                    setDoctorScanModalOpen(false);
                  }}
                  disabled={actionLoading}
                  className="w-full sm:w-auto px-4 py-2.5 bg-teal-600 hover:bg-teal-700 text-white font-bold text-xs rounded-xl shadow-md shadow-teal-600/20 transition shrink-0 cursor-pointer"
                >
                  1-Click Admit & Start
                </button>
              </div>
            );
          })()}

          <p className="text-xs text-slate-500">
            Scan the patient's E-Token pass at the consultation door, take a photo with your mobile camera, or enter the Token #. When admitted, the patient enters the consultation room and the timer begins.
          </p>

          <QRCameraScanner
            defaultStage="ROOM"
            allowedStages={['ROOM']}
            onScan={handleDoctorRoomCameraScan}
            onClose={() => setDoctorScanModalOpen(false)}
          />
        </div>
      </Modal>
    </div>
  );
};
