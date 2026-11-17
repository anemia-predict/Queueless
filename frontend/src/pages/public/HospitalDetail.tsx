import React, { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { api } from '../../services/api';
import { Hospital, Department, Doctor, QueueSession, Booking } from '../../types';
import { useAuth } from '../../contexts/AuthContext';
import { Badge } from '../../components/Badge';
import { Modal } from '../../components/Modal';
import { QRCodeSVG } from 'qrcode.react';
import { Building2, MapPin, Phone, Mail, Stethoscope, Clock, Calendar, CheckCircle2, AlertCircle, QrCode } from 'lucide-react';

export const HospitalDetail: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const { user } = useAuth();
  const navigate = useNavigate();

  const [hospital, setHospital] = useState<Hospital | null>(null);
  const [departments, setDepartments] = useState<Department[]>([]);
  const [doctors, setDoctors] = useState<Doctor[]>([]);
  const [queues, setQueues] = useState<QueueSession[]>([]);
  const [loading, setLoading] = useState(true);

  // Booking Modal
  const [bookingModalOpen, setBookingModalOpen] = useState(false);
  const [selectedDoctor, setSelectedDoctor] = useState<Doctor | null>(null);
  const [selectedDeptId, setSelectedDeptId] = useState<string>('');
  const [selectedSessionId, setSelectedSessionId] = useState<string>('');
  const [bookingSubmitting, setBookingSubmitting] = useState(false);
  const [bookingResult, setBookingResult] = useState<Booking | null>(null);
  const [errorMessage, setErrorMessage] = useState('');

  useEffect(() => {
    if (!id) return;
    const loadDetails = async () => {
      setLoading(true);
      try {
        const [h, depts, docs, q] = await Promise.all([
          api.getHospitalById(id),
          api.getHospitalDepartments(id),
          api.getHospitalDoctors(id),
          api.getHospitalQueueSessions(id).catch(() => [] as QueueSession[]),
        ]);
        setHospital(h);
        setDepartments(depts);
        setDoctors(docs);
        setQueues(q);
      } catch (err) {
        console.error('Failed to load hospital details:', err);
      } finally {
        setLoading(false);
      }
    };

    loadDetails();
  }, [id]);

  const handleOpenBooking = (doc: Doctor) => {
    if (!user) {
      navigate('/login');
      return;
    }
    setSelectedDoctor(doc);
    const initialDept = doc.departments[0]?.id || '';
    setSelectedDeptId(initialDept);

    // Find open queue session for this doctor
    const matchingSession = queues.find((q) => q.doctorId === doc.id && q.status === 'OPEN');
    setSelectedSessionId(matchingSession ? matchingSession.id : '');
    setBookingResult(null);
    setErrorMessage('');
    setBookingModalOpen(true);
  };

  const handleConfirmBooking = async () => {
    if (!hospital || !selectedDoctor || !selectedDeptId) return;

    setBookingSubmitting(true);
    setErrorMessage('');
    try {
      let sessionId = selectedSessionId;
      if (!sessionId) {
        const matching = queues.find((q) => q.doctorId === selectedDoctor.id && q.status === 'OPEN');
        if (matching) sessionId = matching.id;
      }

      const res = await api.createBooking({
        hospitalId: hospital.id,
        departmentId: selectedDeptId,
        doctorId: selectedDoctor.id,
        queueSessionId: sessionId || undefined,
        idempotencyKey: `user_${user?.id}_${Date.now()}`,
      });

      setBookingResult(res);
      api.getHospitalQueueSessions(hospital.id).then(setQueues).catch(() => {});
    } catch (err: any) {
      setErrorMessage(err.response?.data?.message || 'Failed to complete booking. Please try again.');
    } finally {
      setBookingSubmitting(false);
    }
  };

  if (loading) {
    return (
      <div className="max-w-7xl mx-auto px-4 py-20 text-center">
        <div className="w-10 h-10 border-4 border-teal-600 border-t-transparent rounded-full animate-spin mx-auto mb-3" />
        <p className="text-sm text-slate-500">Loading hospital profile...</p>
      </div>
    );
  }

  if (!hospital) {
    return (
      <div className="max-w-7xl mx-auto px-4 py-20 text-center">
        <h2 className="text-xl font-bold text-slate-800">Hospital Not Found</h2>
      </div>
    );
  }

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-10">
      {/* Hospital Banner */}
      <div className="bg-white rounded-3xl border border-slate-200 p-8 shadow-sm mb-10">
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div>
            <div className="flex items-center space-x-3">
              <h1 className="text-3xl font-extrabold text-slate-900">{hospital.name}</h1>
              <Badge status={hospital.status} />
            </div>
            <div className="mt-2 flex flex-wrap items-center gap-4 text-xs text-slate-500">
              <span className="flex items-center space-x-1">
                <MapPin className="w-3.5 h-3.5 text-slate-400" />
                <span>{hospital.address}, {hospital.city}</span>
              </span>
              <span className="flex items-center space-x-1">
                <Phone className="w-3.5 h-3.5 text-slate-400" />
                <span>{hospital.contactPhone}</span>
              </span>
              <span className="flex items-center space-x-1">
                <Mail className="w-3.5 h-3.5 text-slate-400" />
                <span>{hospital.contactEmail}</span>
              </span>
            </div>
          </div>

          <div className="bg-teal-50 border border-teal-200/80 p-3.5 rounded-2xl text-center md:text-right">
            <div className="text-xs font-semibold text-teal-800">Initial Avg. Consultation</div>
            <div className="text-xl font-bold text-teal-900 mt-0.5">{hospital.initialConsultationMinutes} mins</div>
            <div className="text-[10px] text-teal-600">Standard starting baseline</div>
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
        {/* Doctors & Queues (Left 2 cols) */}
        <div className="lg:col-span-2 space-y-6">
          <div className="flex items-center justify-between">
            <h2 className="text-xl font-bold text-slate-900">Available Doctors & Sessions</h2>
            <span className="text-xs text-slate-500">{doctors.length} doctors registered</span>
          </div>

          {doctors.length === 0 ? (
            <div className="p-8 bg-white rounded-2xl border border-slate-200 text-center text-xs text-slate-500">
              No doctors listed for this hospital yet.
            </div>
          ) : (
            <div className="grid grid-cols-1 gap-4">
              {doctors.map((doc) => (
                <div
                  key={doc.id}
                  className="bg-white rounded-2xl border border-slate-200 p-5 shadow-sm hover:border-teal-300 transition flex flex-col sm:flex-row sm:items-center justify-between gap-4"
                >
                  <div>
                    <div className="flex items-center space-x-2">
                      <h3 className="font-bold text-slate-900 text-base">{doc.fullName}</h3>
                      <Badge status={doc.isActive ? 'ACTIVE' : 'INACTIVE'} />
                    </div>
                    <div className="text-xs font-medium text-teal-700 mt-1">{doc.specialization}</div>
                    <div className="text-[11px] text-slate-400 mt-0.5">License: {doc.licenseNumber}</div>

                    <div className="mt-2.5 flex flex-wrap gap-1.5">
                      {doc.departments.map((d) => (
                        <span
                          key={d.id}
                          className="px-2 py-0.5 rounded-lg bg-slate-100 text-slate-600 text-[11px] font-medium"
                        >
                          {d.name} ({d.defaultConsultationMinutes}m)
                        </span>
                      ))}
                    </div>
                  </div>

                  <div>
                    <button
                      onClick={() => handleOpenBooking(doc)}
                      className="w-full sm:w-auto px-5 py-2.5 rounded-xl bg-teal-600 hover:bg-teal-700 text-white font-semibold text-xs shadow-md shadow-teal-600/20 transition"
                    >
                      Book Queue Token
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Departments Sidebar */}
        <div className="space-y-6">
          <h2 className="text-xl font-bold text-slate-900">Departments</h2>
          <div className="bg-white rounded-2xl border border-slate-200 divide-y divide-slate-100 overflow-hidden shadow-sm">
            {departments.map((d) => (
              <div key={d.id} className="p-4 hover:bg-slate-50 transition">
                <div className="flex items-center justify-between">
                  <span className="font-semibold text-slate-800 text-sm">{d.name}</span>
                  <span className="text-xs font-medium text-teal-600 bg-teal-50 px-2 py-0.5 rounded-md">
                    {d.defaultConsultationMinutes} min/patient
                  </span>
                </div>
                {d.description && <p className="text-xs text-slate-500 mt-1">{d.description}</p>}
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Booking Modal */}
      <Modal
        isOpen={bookingModalOpen}
        onClose={() => setBookingModalOpen(false)}
        title={bookingResult ? 'Booking Confirmed!' : `Book Queue Token – ${selectedDoctor?.fullName}`}
      >
        {bookingResult ? (
          <div className="text-center py-2 space-y-4">
            <div className="w-12 h-12 rounded-full bg-emerald-100 text-emerald-600 flex items-center justify-center mx-auto">
              <CheckCircle2 className="w-7 h-7" />
            </div>

            <div>
              <div className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Your Assigned Token</div>
              <div className="text-5xl font-black text-teal-600 mt-1">#{bookingResult.tokenNumber}</div>
            </div>

            {/* QR Code Pass */}
            <div className="bg-slate-50 border border-slate-200 rounded-2xl p-4 flex flex-col items-center justify-center space-y-2">
              <div className="p-3 bg-white rounded-xl shadow-sm border border-slate-200">
                <QRCodeSVG
                  value={JSON.stringify({
                    type: 'QUEUELess_TOKEN',
                    bookingId: bookingResult.id,
                    tokenNumber: bookingResult.tokenNumber,
                    hospital: bookingResult.hospitalName,
                    doctor: bookingResult.doctorName,
                    patient: user?.fullName || bookingResult.patientName,
                    bookingTime: bookingResult.bookingTime,
                  })}
                  size={140}
                  level="H"
                />
              </div>
              <div className="flex items-center space-x-1 text-slate-600 text-xs font-medium">
                <QrCode className="w-3.5 h-3.5 text-teal-600" />
                <span>Digital E-Token QR Code</span>
              </div>
              <p className="text-[11px] text-slate-400">
                Show or scan this QR code at the hospital check-in kiosk or doctor desk.
              </p>
            </div>

            <div className="bg-slate-50 p-4 rounded-xl text-left text-xs space-y-2 border border-slate-200">
              <div className="flex justify-between">
                <span className="text-slate-500">Hospital:</span>
                <span className="font-semibold text-slate-800">{bookingResult.hospitalName}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">Doctor:</span>
                <span className="font-semibold text-slate-800">{bookingResult.doctorName}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">Position in Queue:</span>
                <span className="font-semibold text-slate-800">{bookingResult.positionInQueue} people ahead</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">Estimated Waiting Time:</span>
                <span className="font-bold text-teal-700">~{bookingResult.estimatedWaitMinutes} minutes</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">Prediction Method:</span>
                <Badge status={bookingResult.predictionMethod || 'BASELINE'} />
              </div>
            </div>

            <div className="pt-2 flex gap-3">
              <button
                onClick={() => {
                  setBookingModalOpen(false);
                  navigate('/patient');
                }}
                className="flex-1 py-2.5 rounded-xl bg-teal-600 hover:bg-teal-700 text-white font-semibold text-xs transition shadow-md shadow-teal-600/20"
              >
                Track in Live Queue
              </button>
            </div>
          </div>
        ) : (
          <div className="space-y-4">
            {errorMessage && (
              <div className="p-3 rounded-xl bg-rose-50 border border-rose-200 text-rose-700 text-xs flex items-center space-x-2">
                <AlertCircle className="w-4 h-4 shrink-0" />
                <span>{errorMessage}</span>
              </div>
            )}

            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1">Select Department</label>
              <select
                value={selectedDeptId}
                onChange={(e) => setSelectedDeptId(e.target.value)}
                className="w-full p-2.5 text-xs rounded-xl border border-slate-200 focus:outline-none focus:border-teal-500"
              >
                {selectedDoctor?.departments.map((d) => (
                  <option key={d.id} value={d.id}>
                    {d.name} ({d.defaultConsultationMinutes} mins)
                  </option>
                ))}
              </select>
            </div>

            <div className="bg-amber-50 border border-amber-200 p-3 rounded-xl text-xs text-amber-800 space-y-1">
              <div className="font-semibold">AI Waiting-Time Transparency</div>
              <p className="text-[11px] text-amber-700 leading-relaxed">
                Initial estimates use live queue depth multiplied by the department consultation standard.
                Once sufficient historical consultation data is captured, calibrated FastForest regression models refine estimates.
              </p>
            </div>

            <div className="pt-3 flex justify-end space-x-3">
              <button
                type="button"
                onClick={() => setBookingModalOpen(false)}
                className="px-4 py-2 text-xs font-semibold text-slate-600 hover:bg-slate-100 rounded-xl transition"
              >
                Cancel
              </button>
              <button
                type="button"
                disabled={bookingSubmitting}
                onClick={handleConfirmBooking}
                className="px-5 py-2 text-xs font-semibold text-white bg-teal-600 hover:bg-teal-700 rounded-xl shadow-md shadow-teal-600/20 transition disabled:opacity-50"
              >
                {bookingSubmitting ? 'Allocating Token...' : 'Confirm & Generate Token'}
              </button>
            </div>
          </div>
        )}
      </Modal>
    </div>
  );
};
