import axios from 'axios';
import {
  AuthResponse,
  UserProfile,
  Hospital,
  Department,
  Doctor,
  DoctorSchedule,
  QueueSession,
  Booking,
  QueueStatus,
  HospitalAnalytics,
  PlatformAnalytics,
  AIModelMetrics,
  NotificationItem,
} from '../types';

const API_BASE = '/api/v1';

export const apiClient = axios.create({
  baseURL: API_BASE,
  headers: {
    'Content-Type': 'application/json',
  },
});

apiClient.interceptors.request.use((config) => {
  const token = localStorage.getItem('queueless_token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      console.warn('Session expired or invalid authentication token. Clearing local state.');
      localStorage.removeItem('queueless_token');
      localStorage.removeItem('queueless_user');
      if (typeof window !== 'undefined' && !['/login', '/', '/register'].includes(window.location.pathname)) {
        window.location.href = '/login';
      }
    }
    return Promise.reject(error);
  }
);

export const api = {
  // Auth
  register: (data: any) => apiClient.post<AuthResponse>('/auth/register', data).then((res) => res.data),
  login: (data: any) => apiClient.post<AuthResponse>('/auth/login', data).then((res) => res.data),
  getMe: () => apiClient.get<UserProfile>('/auth/me').then((res) => res.data),

  // Public Discovery
  searchHospitals: (params?: { name?: string; city?: string; department?: string }) =>
    apiClient.get<Hospital[]>('/hospitals', { params }).then((res) => res.data),
  getHospitalById: (id: string) => apiClient.get<Hospital>(`/hospitals/${id}`).then((res) => res.data),
  getHospitalDepartments: (id: string) => apiClient.get<Department[]>(`/hospitals/${id}/departments`).then((res) => res.data),
  getHospitalDoctors: (id: string, departmentId?: string) =>
    apiClient.get<Doctor[]>(`/hospitals/${id}/doctors`, { params: { departmentId } }).then((res) => res.data),
  getHospitalQueueSessions: (id: string, doctorId?: string) =>
    apiClient.get<QueueSession[]>(`/hospitals/${id}/queue-sessions`, { params: { doctorId } }).then((res) => res.data),

  // Hospital Registration
  registerHospital: (data: any) => apiClient.post<Hospital>('/hospital-registration', data).then((res) => res.data),

  // Hospital Management
  getMyHospital: () => apiClient.get<Hospital>('/hospital/me').then((res) => res.data),
  updateMyHospital: (data: any) => apiClient.patch<Hospital>('/hospital/me', data).then((res) => res.data),
  getDepartments: () => apiClient.get<Department[]>('/hospital/departments').then((res) => res.data),
  createDepartment: (data: any) => apiClient.post<Department>('/hospital/departments', data).then((res) => res.data),
  updateDepartment: (id: string, data: any) => apiClient.put<Department>(`/hospital/departments/${id}`, data).then((res) => res.data),
  getDoctors: () => apiClient.get<Doctor[]>('/hospital/doctors').then((res) => res.data),
  createDoctor: (data: any) => apiClient.post<Doctor>('/hospital/doctors', data).then((res) => res.data),
  updateDoctor: (id: string, data: any) => apiClient.put<Doctor>(`/hospital/doctors/${id}`, data).then((res) => res.data),
  getSchedules: (doctorId?: string) => apiClient.get<DoctorSchedule[]>('/hospital/schedules', { params: { doctorId } }).then((res) => res.data),
  createSchedule: (data: any) => apiClient.post<DoctorSchedule>('/hospital/schedules', data).then((res) => res.data),
  getHospitalAnalytics: () => apiClient.get<HospitalAnalytics>('/hospital/analytics').then((res) => res.data),

  // Bookings
  createBooking: (data: any) => apiClient.post<Booking>('/bookings', data).then((res) => res.data),
  getMyBookings: () => apiClient.get<Booking[]>('/bookings/me').then((res) => res.data),
  getBookingById: (id: string) => apiClient.get<Booking>(`/bookings/${id}`).then((res) => res.data),
  cancelBooking: (id: string, reason?: string) => apiClient.post<Booking>(`/bookings/${id}/cancel`, { reason }).then((res) => res.data),
  getQueueStatus: (id: string) => apiClient.get<QueueStatus>(`/bookings/${id}/queue-status`).then((res) => res.data),

  // Queue Operations
  getQueues: (doctorId?: string) => apiClient.get<QueueSession[]>('/hospital/queues', { params: { doctorId } }).then((res) => res.data),
  getQueueDetails: (id: string) => apiClient.get<QueueSession>(`/hospital/queues/${id}`).then((res) => res.data),
  openQueueSession: (data: any) => apiClient.post<QueueSession>('/hospital/queues', data).then((res) => res.data),
  callNext: (sessionId: string) => apiClient.post<any>(`/hospital/queues/${sessionId}/call-next`).then((res) => res.data),
  startConsultation: (bookingId: string) => apiClient.post<any>(`/hospital/bookings/${bookingId}/start`).then((res) => res.data),
  completeConsultation: (bookingId: string, notes?: string) =>
    apiClient.post<any>(`/hospital/bookings/${bookingId}/complete`, { notes }).then((res) => res.data),
  markNoShow: (bookingId: string) => apiClient.post<any>(`/hospital/bookings/${bookingId}/no-show`).then((res) => res.data),
  pauseQueue: (sessionId: string) => apiClient.post<any>(`/hospital/queues/${sessionId}/pause`).then((res) => res.data),
  resumeQueue: (sessionId: string) => apiClient.post<any>(`/hospital/queues/${sessionId}/resume`).then((res) => res.data),
  verifyBooking: (query: string) => apiClient.get<any>('/hospital/bookings/verify', { params: { query } }).then((res) => res.data),
  checkInBooking: (id: string) => apiClient.post<any>(`/hospital/bookings/${id}/check-in`).then((res) => res.data),
  buildingCheckIn: (bookingId: string) => apiClient.post<any>(`/hospital/bookings/${bookingId}/building-check-in`).then((res) => res.data),
  roomCheckIn: (bookingId: string) => apiClient.post<any>(`/hospital/bookings/${bookingId}/room-check-in`).then((res) => res.data),
  forfeitExpiredTokens: (sessionId: string) => apiClient.post<any>(`/hospital/queues/${sessionId}/forfeit-expired`).then((res) => res.data),

  // Platform Admin
  getAllHospitals: (status?: string) => apiClient.get<Hospital[]>('/platform/hospitals', { params: { status } }).then((res) => res.data),
  getPendingHospitals: () => apiClient.get<Hospital[]>('/platform/hospitals/pending').then((res) => res.data),
  approveHospital: (id: string) => apiClient.post<Hospital>(`/platform/hospitals/${id}/approve`).then((res) => res.data),
  rejectHospital: (id: string, reason?: string) => apiClient.post<Hospital>(`/platform/hospitals/${id}/reject`, { reason }).then((res) => res.data),
  suspendHospital: (id: string, reason?: string) => apiClient.post<Hospital>(`/platform/hospitals/${id}/suspend`, { reason }).then((res) => res.data),
  getPlatformAnalytics: () => apiClient.get<PlatformAnalytics>('/platform/analytics').then((res) => res.data),

  // AI & Machine Learning
  getWaitingTime: (bookingId: string) => apiClient.get<any>(`/ai/waiting-time/${bookingId}`).then((res) => res.data),
  getHospitalAIMetrics: () => apiClient.get<AIModelMetrics>('/hospital/ai/metrics').then((res) => res.data),
  seedSyntheticData: (recordCount = 60, hospitalId?: string) =>
    apiClient.post<any>('/hospital/ai/seed-data', { recordCount, hospitalId }).then((res) => res.data),
  trainHospitalModel: () => apiClient.post<AIModelMetrics>('/hospital/ai/train').then((res) => res.data),
  getAllPlatformModels: () => apiClient.get<AIModelMetrics[]>('/platform/ai/models').then((res) => res.data),
  trainSharedModel: () => apiClient.post<AIModelMetrics>('/platform/ai/train-shared').then((res) => res.data),

  // Hospital AI Chatbot Agent
  askHospitalAi: (message: string) => apiClient.post<any>('/hospital/ai-chat', { message }).then((res) => res.data),

  // Notifications
  getNotifications: () => apiClient.get<NotificationItem[]>('/notifications').then((res) => res.data),
  markNotificationRead: (id: string) => apiClient.post<any>(`/notifications/${id}/read`).then((res) => res.data),
};
