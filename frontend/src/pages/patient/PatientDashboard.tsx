import React, { useState, useEffect, useRef } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../../services/api';
import { getSignalRConnection, startSignalR } from '../../services/signalr';
import { Booking } from '../../types';
import { useAuth } from '../../contexts/AuthContext';
import { Badge } from '../../components/Badge';
import { Modal } from '../../components/Modal';
import { QRCodeSVG } from 'qrcode.react';
import { Clock, Activity, AlertCircle, CheckCircle2, XCircle, Search, Sparkles, QrCode, Printer, Building, Stethoscope, Bell, X } from 'lucide-react';

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
    <div className={`p-2 rounded-xl text-center font-bold text-xs ${
      secondsLeft < 30 ? 'bg-rose-100 text-rose-800 border border-rose-300 animate-pulse' : 'bg-amber-100 text-amber-900 border border-amber-300'
    }`}>
      <div className="flex items-center justify-center space-x-1">
        <Clock className="w-4 h-4 shrink-0" />
        <span>Time to Enter Doctor Room:</span>
      </div>
      <div className="text-xl font-black mt-0.5 tracking-tight">
        {secondsLeft <= 0 ? '00:00 (Window Expired)' : `${String(mins).padStart(2, '0')}:${String(secs).padStart(2, '0')}`}
      </div>
    </div>
  );
};

export const PatientDashboard: React.FC = () => {
  const { user } = useAuth();
  const [bookings, setBookings] = useState<Booking[]>([]);
  const bookingsRef = useRef(bookings);
  useEffect(() => {
    bookingsRef.current = bookings;
  }, [bookings]);

  const [loading, setLoading] = useState(true);
  const [cancellingId, setCancellingId] = useState<string | null>(null);
  const [selectedQrBooking, setSelectedQrBooking] = useState<Booking | null>(null);
  const [liveAlert, setLiveAlert] = useState<{ title: string; message: string } | null>(null);

  const fetchBookings = async () => {
    try {
      const data = await api.getMyBookings();
      setBookings(data);
      bookingsRef.current = data;
    } catch (err) {
      console.error('Failed to load bookings:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchBookings();

    // SignalR Real-Time Listener
    let conn: any;
    startSignalR().then((c) => {
      conn = c;
      if (user) {
        conn.invoke('JoinPatientChannel', user.id).catch(() => {});
      }
      conn.on('QueueUpdated', () => {
        fetchBookings();
      });
      conn.on('TokenCalled', (data: any) => {
        fetchBookings();
        // Only show "Your Token Is Being Called!" alert if THIS logged-in user owns the called token!
        const isMyToken = bookingsRef.current.some(
          (b) => b.id === data.bookingId || b.tokenNumber === data.tokenNumber
        );
        if (isMyToken) {
          setLiveAlert({
            title: 'Your Token Is Being Called!',
            message: `Doctor is calling your Token #${data.tokenNumber}. You have 2 minutes to enter the consultation room!`
          });
          try {
            const ctx = new (window.AudioContext || (window as any).webkitAudioContext)();
            const osc = ctx.createOscillator();
            osc.connect(ctx.destination);
            osc.frequency.setValueAtTime(440, ctx.currentTime);
            osc.frequency.setValueAtTime(880, ctx.currentTime + 0.15);
            osc.start();
            osc.stop(ctx.currentTime + 0.35);
          } catch (_) {}
        }
      });
      conn.on('YouAreNext', (data: any) => {
        fetchBookings();
        setLiveAlert({
          title: "You're Next in Line!",
          message: `Token #${data.currentServingToken} is consulting with ${data.doctorName}. You are Token #${data.tokenNumber} and next! Please wait near the consultation room.`
        });
        try {
          const ctx = new (window.AudioContext || (window as any).webkitAudioContext)();
          const osc = ctx.createOscillator();
          osc.connect(ctx.destination);
          osc.frequency.setValueAtTime(587.33, ctx.currentTime);
          osc.frequency.setValueAtTime(880, ctx.currentTime + 0.15);
          osc.start();
          osc.stop(ctx.currentTime + 0.35);
        } catch (_) {}
      });
      conn.on('ConsultationStarted', () => {
        fetchBookings();
      });
      conn.on('ConsultationCompleted', () => {
        fetchBookings();
      });
    });

    return () => {
      if (conn) {
        conn.off('QueueUpdated');
        conn.off('TokenCalled');
        conn.off('YouAreNext');
        conn.off('ConsultationStarted');
        conn.off('ConsultationCompleted');
      }
    };
  }, [user]);

  const handleCancelBooking = async (id: string) => {
    if (!confirm('Are you sure you want to cancel this booking token?')) return;
    setCancellingId(id);
    try {
      await api.cancelBooking(id, 'Patient cancelled from portal');
      await fetchBookings();
    } catch (err: any) {
      alert(err.response?.data?.message || 'Failed to cancel booking.');
    } finally {
      setCancellingId(null);
    }
  };

  const activeBooking =
    bookings.find((b) => b.status === 'SERVING') ||
    bookings.find((b) => b.status === 'CALLED') ||
    bookings.find((b) => b.status === 'WAITING');

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-10">
      {/* Welcome & Action Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-8">
        <div>
          <h1 className="text-2xl sm:text-3xl font-extrabold text-slate-900 tracking-tight">
            Patient Portal & Queue Tracker
          </h1>
          <p className="text-xs text-slate-500 mt-1">
            Logged in as <span className="font-semibold text-slate-700">{user?.fullName}</span> ({user?.email})
          </p>
        </div>

        <Link
          to="/hospitals"
          className="px-5 py-2.5 rounded-xl bg-teal-600 hover:bg-teal-700 text-white font-semibold text-xs shadow-md shadow-teal-600/20 transition flex items-center justify-center space-x-1.5"
        >
          <Search className="w-3.5 h-3.5" />
          <span>Find Hospitals & Book New Token</span>
        </Link>
      </div>

      {/* Real-time Push Alert Banner */}
      {liveAlert && (
        <div className="mb-6 p-4 rounded-2xl bg-teal-600 text-white shadow-lg flex items-start justify-between border-2 border-teal-400">
          <div className="flex items-center space-x-3">
            <div className="p-2 bg-white/20 rounded-xl shrink-0">
              <Bell className="w-6 h-6 text-amber-300 animate-bounce" />
            </div>
            <div>
              <h4 className="font-extrabold text-sm">{liveAlert.title}</h4>
              <p className="text-xs text-teal-100 mt-0.5">{liveAlert.message}</p>
            </div>
          </div>
          <button
            onClick={() => setLiveAlert(null)}
            className="p-1 rounded-lg hover:bg-white/10 text-white/80 transition shrink-0"
          >
            <X className="w-4 h-4" />
          </button>
        </div>
      )}

      {/* Active Live Queue Tracker Card */}
      {activeBooking ? (
        <div className="bg-white rounded-3xl border border-teal-200/80 p-6 sm:p-8 shadow-lg shadow-teal-600/5 mb-10 relative overflow-hidden">
          <div className="absolute top-0 right-0 transform translate-x-4 -translate-y-4 w-32 h-32 bg-teal-50 rounded-full blur-xl pointer-events-none" />

          <div className="flex items-center justify-between pb-6 border-b border-slate-100">
            <div className="flex items-center space-x-2">
              <span className="w-2.5 h-2.5 rounded-full bg-emerald-500 animate-ping" />
              <span className="text-xs font-bold text-teal-700 uppercase tracking-wider">Live Queue Tracker</span>
            </div>
            <Badge status={activeBooking.status} />
          </div>

          <div className="mt-6 grid grid-cols-1 md:grid-cols-4 gap-6 items-center">
            {/* Token & QR Code Display */}
            <div className="text-center md:text-left md:border-r md:border-slate-100 md:pr-6 flex flex-col sm:flex-row md:flex-col items-center sm:items-start md:items-start gap-4">
              <div>
                <span className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Your Token Number</span>
                <div className="text-6xl font-black text-teal-600 tracking-tight mt-1">#{activeBooking.tokenNumber}</div>
                <div className="text-xs font-medium text-slate-500 mt-1">{activeBooking.departmentName}</div>
              </div>

              {/* QR Code Pass Card */}
              <div
                onClick={() => setSelectedQrBooking(activeBooking)}
                className="group cursor-pointer p-2.5 bg-slate-50 hover:bg-teal-50/50 border border-slate-200 hover:border-teal-300 rounded-2xl flex items-center space-x-3 transition shadow-sm"
                title="Click to view full digital QR pass"
              >
                <div className="p-1.5 bg-white rounded-xl shadow-xs border border-slate-100 group-hover:scale-105 transition-transform">
                  <QRCodeSVG
                    value={JSON.stringify({
                      bookingId: activeBooking.id,
                      tokenNumber: activeBooking.tokenNumber,
                    })}
                    size={56}
                    level="M"
                    includeMargin={true}
                  />
                </div>
                <div className="text-left">
                  <div className="flex items-center space-x-1 text-teal-700 font-bold text-xs">
                    <QrCode className="w-3.5 h-3.5" />
                    <span>Digital Pass</span>
                  </div>
                  <div className="text-[10px] text-slate-400 group-hover:text-teal-600 font-medium transition-colors">
                    Click to enlarge QR
                  </div>
                </div>
              </div>
            </div>

            {/* Hospital & Doctor Details */}
            <div className="space-y-3 md:col-span-2">
              <div>
                <span className="text-xs font-semibold text-slate-400 uppercase">Hospital</span>
                <div className="text-base font-bold text-slate-900">{activeBooking.hospitalName}</div>
              </div>
              <div>
                <span className="text-xs font-semibold text-slate-400 uppercase">Consulting Physician</span>
                <div className="text-sm font-semibold text-slate-800">{activeBooking.doctorName}</div>
              </div>

              {/* Two-Stage Check-in Progress Tracker */}
              <div className="bg-slate-50 border border-slate-200 rounded-2xl p-3 grid grid-cols-2 gap-3 text-xs">
                <div>
                  <span className="text-[10px] font-bold text-slate-400 uppercase tracking-wider block">Stage 1: Building Entry</span>
                  <div className="flex items-center space-x-1.5 mt-0.5">
                    <span className={`w-2 h-2 rounded-full ${activeBooking.isBuildingCheckedIn ? 'bg-emerald-500' : 'bg-amber-400 animate-pulse'}`} />
                    <span className={`font-bold ${activeBooking.isBuildingCheckedIn ? 'text-emerald-700' : 'text-amber-700'}`}>
                      {activeBooking.isBuildingCheckedIn ? 'Checked In' : 'Scan at Building Kiosk'}
                    </span>
                  </div>
                </div>
                <div>
                  <span className="text-[10px] font-bold text-slate-400 uppercase tracking-wider block">Stage 2: Doctor Room Entry</span>
                  <div className="flex items-center space-x-1.5 mt-0.5">
                    <span className={`w-2 h-2 rounded-full ${
                      activeBooking.status === 'COMPLETED'
                        ? 'bg-emerald-500'
                        : activeBooking.isDoctorRoomCheckedIn
                        ? 'bg-teal-500'
                        : activeBooking.status === 'CALLED'
                        ? 'bg-amber-500 animate-ping'
                        : 'bg-slate-300'
                    }`} />
                    <span className={`font-bold ${
                      activeBooking.status === 'COMPLETED'
                        ? 'text-emerald-700'
                        : activeBooking.isDoctorRoomCheckedIn
                        ? 'text-teal-700'
                        : activeBooking.status === 'CALLED'
                        ? 'text-rose-600 animate-pulse'
                        : 'text-slate-500'
                    }`}>
                      {activeBooking.status === 'COMPLETED'
                        ? 'Completed'
                        : activeBooking.isDoctorRoomCheckedIn
                        ? 'In Consultation'
                        : activeBooking.status === 'CALLED'
                        ? 'Enter Room Now!'
                        : 'Waiting for Call'}
                    </span>
                  </div>
                </div>
              </div>

              <div className="pt-1 flex flex-wrap gap-2 text-xs">
                <span className={`px-3 py-1 rounded-xl font-medium ${
                  activeBooking.status === 'SERVING'
                    ? 'bg-emerald-100 text-emerald-900 border border-emerald-300 font-bold'
                    : activeBooking.status === 'CALLED'
                    ? 'bg-amber-100 text-amber-900 border border-amber-300 font-bold'
                    : activeBooking.positionInQueue === 0
                    ? 'bg-amber-100 text-amber-900 border border-amber-300 font-bold'
                    : 'bg-slate-100 text-slate-700'
                }`}>
                  Queue Position:{' '}
                  <strong className="text-slate-900">
                    {activeBooking.status === 'SERVING'
                      ? 'Currently with Doctor (In Consultation)'
                      : activeBooking.status === 'CALLED'
                      ? 'Called – Proceed to Room'
                      : activeBooking.positionInQueue === 0
                      ? '0 ahead (You are NEXT!)'
                      : `${activeBooking.positionInQueue} ahead`}
                  </strong>
                </span>
                <span className="px-3 py-1 rounded-xl bg-teal-50 text-teal-800 font-medium flex items-center space-x-1">
                  <Sparkles className="w-3.5 h-3.5 text-teal-600" />
                  <span>Est. Waiting: <strong>~{activeBooking.estimatedWaitMinutes} mins</strong></span>
                </span>
              </div>
            </div>

            {/* Action buttons */}
            <div className="flex flex-col gap-2">
              {activeBooking.status === 'WAITING' && activeBooking.positionInQueue === 0 && (
                <div className="p-3.5 rounded-2xl bg-gradient-to-r from-teal-600 to-emerald-600 text-white shadow-md space-y-1">
                  <div className="flex items-center space-x-1.5 font-black text-xs uppercase tracking-wider">
                    <Bell className="w-4 h-4 text-amber-300 animate-bounce" />
                    <span>YOU'RE NEXT IN LINE!</span>
                  </div>
                  <p className="text-[11px] text-teal-50 leading-relaxed">
                    Doctor is currently seeing the previous patient. You are Token #{activeBooking.tokenNumber} and next! Please stand by near the consultation room.
                  </p>
                </div>
              )}

              <button
                onClick={() => setSelectedQrBooking(activeBooking)}
                className="w-full py-2 px-3 rounded-xl border border-teal-200 bg-teal-50/60 hover:bg-teal-100 text-teal-800 font-semibold text-xs flex items-center justify-center space-x-1.5 transition"
              >
                <QrCode className="w-3.5 h-3.5 text-teal-600" />
                <span>Show Token QR Pass</span>
              </button>

              {activeBooking.status === 'WAITING' && (
                <button
                  disabled={cancellingId === activeBooking.id}
                  onClick={() => handleCancelBooking(activeBooking.id)}
                  className="w-full py-2.5 px-4 rounded-xl border border-rose-200 bg-rose-50 hover:bg-rose-100 text-rose-700 font-semibold text-xs transition disabled:opacity-50"
                >
                  {cancellingId === activeBooking.id ? 'Cancelling...' : 'Cancel Token'}
                </button>
              )}
              {activeBooking.status === 'CALLED' && (
                <div className="p-3 rounded-2xl bg-amber-50 border-2 border-amber-300 text-amber-900 text-xs font-semibold text-center space-y-2 shadow-sm">
                  <div className="text-amber-800 font-black flex items-center justify-center space-x-1.5">
                    <span className="w-2.5 h-2.5 rounded-full bg-rose-500 animate-ping" />
                    <span>DOCTOR IS CALLING YOUR TOKEN!</span>
                  </div>
                  <p className="text-[11px] text-amber-700">
                    Proceed to the doctor consultation room immediately.
                  </p>
                  <CountdownBadge callTime={activeBooking.actualCallTime} onExpire={fetchBookings} />
                  <p className="text-[10px] text-rose-600 font-bold">
                    Tokens are forfeited after 2 minutes if room entry is missed.
                  </p>
                </div>
              )}
              {activeBooking.status === 'SERVING' && (
                <div className="p-3 rounded-xl bg-blue-50 border border-blue-200 text-blue-800 text-xs font-semibold text-center">
                  Consultation currently in progress.
                </div>
              )}
            </div>
          </div>

          {/* AI Model Methodology Transparency Note */}
          <div className="mt-6 pt-4 border-t border-slate-100 flex items-start space-x-2 text-[11px] text-slate-500">
            <Sparkles className="w-3.5 h-3.5 text-teal-600 shrink-0 mt-0.5" />
            <span>
              Prediction method: <strong className="text-slate-700">{activeBooking.predictionMethod || 'BASELINE'}</strong>.
              Calculated dynamically from queue depth and doctor consultation pacing. Transparently audited with zero artificial inflation.
            </span>
          </div>
        </div>
      ) : (
        <div className="bg-white rounded-3xl border border-slate-200 p-8 shadow-sm mb-10 text-center">
          <Activity className="w-12 h-12 text-teal-500/50 mx-auto mb-3" />
          <h3 className="text-base font-bold text-slate-800">No Active Queue Token</h3>
          <p className="mt-1 text-xs text-slate-500 max-w-sm mx-auto">
            You currently have no waiting consultations. Search our multi-hospital network to book your next appointment token.
          </p>
          <div className="mt-4">
            <Link
              to="/hospitals"
              className="inline-flex items-center space-x-1.5 px-5 py-2.5 rounded-xl bg-teal-600 hover:bg-teal-700 text-white font-semibold text-xs shadow-md shadow-teal-600/20 transition"
            >
              <Search className="w-3.5 h-3.5" />
              <span>Browse Hospitals</span>
            </Link>
          </div>
        </div>
      )}

      {/* Booking History Table */}
      <div className="bg-white rounded-3xl border border-slate-200 overflow-hidden shadow-sm">
        <div className="p-6 border-b border-slate-100 flex items-center justify-between">
          <h2 className="text-base font-bold text-slate-900">Booking History</h2>
          <span className="text-xs text-slate-500">{bookings.length} total bookings</span>
        </div>

        {bookings.length === 0 ? (
          <div className="p-8 text-center text-xs text-slate-400">No previous bookings found.</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs">
              <thead className="bg-slate-50 text-slate-500 uppercase tracking-wider font-semibold border-b border-slate-100">
                <tr>
                  <th className="py-3 px-4">Token</th>
                  <th className="py-3 px-4">Hospital</th>
                  <th className="py-3 px-4">Doctor</th>
                  <th className="py-3 px-4">Department</th>
                  <th className="py-3 px-4">Booked At</th>
                  <th className="py-3 px-4">Status</th>
                  <th className="py-3 px-4 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {bookings.map((b) => (
                  <tr key={b.id} className="hover:bg-slate-50 transition">
                    <td className="py-3 px-4 font-bold text-teal-700">#{b.tokenNumber}</td>
                    <td className="py-3 px-4 font-semibold text-slate-800">{b.hospitalName}</td>
                    <td className="py-3 px-4 text-slate-600">{b.doctorName}</td>
                    <td className="py-3 px-4 text-slate-500">{b.departmentName}</td>
                    <td className="py-3 px-4 text-slate-500">
                      {new Date(b.bookingTime).toLocaleString([], {
                        month: 'short',
                        day: 'numeric',
                        hour: '2-digit',
                        minute: '2-digit',
                      })}
                    </td>
                    <td className="py-3 px-4">
                      <Badge status={b.status} />
                    </td>
                    <td className="py-3 px-4 text-right">
                      <div className="flex items-center justify-end space-x-2">
                        <button
                          onClick={() => setSelectedQrBooking(b)}
                          className="inline-flex items-center space-x-1 px-2.5 py-1 rounded-lg border border-slate-200 hover:border-teal-500 hover:text-teal-600 text-slate-600 font-semibold text-[11px] transition shadow-2xs"
                          title="View QR Code Pass"
                        >
                          <QrCode className="w-3.5 h-3.5 text-teal-600" />
                          <span>QR Pass</span>
                        </button>
                        {b.status === 'WAITING' && (
                          <button
                            onClick={() => handleCancelBooking(b.id)}
                            className="text-rose-600 hover:text-rose-800 font-semibold text-[11px] px-1 py-1"
                          >
                            Cancel
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Digital QR Token Pass Modal */}
      <Modal
        isOpen={!!selectedQrBooking}
        onClose={() => setSelectedQrBooking(null)}
        title="Digital E-Token Pass"
      >
        {selectedQrBooking && (
          <div className="text-center py-2 space-y-4">
            <div className="p-5 bg-slate-50 border border-slate-200 rounded-2xl flex flex-col items-center justify-center space-y-3">
              <div className="p-3 bg-white rounded-2xl shadow-sm border border-slate-200">
                <QRCodeSVG
                  value={JSON.stringify({
                    bookingId: selectedQrBooking.id,
                    tokenNumber: selectedQrBooking.tokenNumber,
                  })}
                  size={210}
                  level="M"
                  includeMargin={true}
                />
              </div>

              <div>
                <span className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Queue Token</span>
                <div className="text-5xl font-black text-teal-600">#{selectedQrBooking.tokenNumber}</div>
              </div>

              <Badge status={selectedQrBooking.status} />

              <div className="p-2.5 rounded-xl bg-amber-50 border border-amber-200/60 text-amber-800 text-[11px] leading-tight max-w-xs mx-auto">
                📱 <strong>Camera Tip:</strong> Set phone screen brightness to ~70% and hold steady 15–25 cm (6–10 in) in front of the laptop camera.
              </div>
            </div>

            <div className="bg-slate-50 p-4 rounded-xl text-left text-xs space-y-2 border border-slate-200">
              <div className="flex justify-between">
                <span className="text-slate-500">Patient Name:</span>
                <span className="font-semibold text-slate-800">{user?.fullName || selectedQrBooking.patientName}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">Hospital:</span>
                <span className="font-semibold text-slate-800">{selectedQrBooking.hospitalName}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">Doctor:</span>
                <span className="font-semibold text-slate-800">{selectedQrBooking.doctorName}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">Department:</span>
                <span className="font-semibold text-slate-800">{selectedQrBooking.departmentName}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">Booking ID:</span>
                <span className="font-mono text-[10px] text-slate-600 truncate max-w-[200px]">{selectedQrBooking.id}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">Booked At:</span>
                <span className="text-slate-700">{new Date(selectedQrBooking.bookingTime).toLocaleString()}</span>
              </div>
            </div>

            <div className="pt-2 flex gap-3">
              <button
                type="button"
                onClick={() => window.print()}
                className="flex-1 py-2.5 rounded-xl border border-slate-200 hover:bg-slate-50 text-slate-700 font-semibold text-xs flex items-center justify-center space-x-1.5 transition"
              >
                <Printer className="w-4 h-4 text-slate-500" />
                <span>Print Pass</span>
              </button>
              <button
                type="button"
                onClick={() => setSelectedQrBooking(null)}
                className="flex-1 py-2.5 rounded-xl bg-teal-600 hover:bg-teal-700 text-white font-semibold text-xs transition shadow-md shadow-teal-600/20 flex items-center justify-center space-x-1.5 cursor-pointer"
              >
                <X className="w-4 h-4 stroke-[2.5]" />
                <span>Close</span>
              </button>
            </div>
          </div>
        )}
      </Modal>
    </div>
  );
};
