export type UserRole = 'PlatformAdmin' | 'HospitalAdmin' | 'Doctor' | 'Patient';

export interface UserProfile {
  id: string;
  email: string;
  fullName: string;
  phoneNumber?: string;
  role: UserRole;
  hospitalId?: string;
  hospitalName?: string;
  hospitalStatus?: string;
}

export interface AuthResponse {
  token: string;
  user: UserProfile;
}

export interface Hospital {
  id: string;
  name: string;
  registrationNumber: string;
  address: string;
  city: string;
  area?: string;
  state?: string;
  contactPhone: string;
  contactEmail: string;
  status: 'PENDING' | 'APPROVED' | 'REJECTED' | 'SUSPENDED';
  rejectionReason?: string;
  initialConsultationMinutes: number;
  departmentCount: number;
  doctorCount: number;
  createdAt: string;
}

export interface Department {
  id: string;
  hospitalId: string;
  name: string;
  description?: string;
  defaultConsultationMinutes: number;
  isActive: boolean;
  doctorCount: number;
}

export interface Doctor {
  id: string;
  hospitalId: string;
  userId?: string;
  fullName: string;
  specialization: string;
  licenseNumber: string;
  isActive: boolean;
  departments: Department[];
}

export interface DoctorSchedule {
  id: string;
  hospitalId: string;
  doctorId: string;
  doctorName: string;
  departmentId: string;
  departmentName: string;
  dayOfWeek: number;
  dayName: string;
  startTime: string;
  endTime: string;
  slotDurationMinutes: number;
  maxPatientsPerSession: number;
  isActive: boolean;
}

export interface QueueSession {
  id: string;
  hospitalId: string;
  doctorId: string;
  doctorName: string;
  departmentId: string;
  departmentName: string;
  sessionDate: string;
  startTime: string;
  endTime: string;
  status: 'SCHEDULED' | 'OPEN' | 'PAUSED' | 'CLOSED';
  maxCapacity: number;
  currentTokenNumber: number;
  totalBookings: number;
  waitingCount: number;
  completedCount: number;
  currentServingBooking?: Booking;
  waitingBookings: Booking[];
}

export interface Booking {
  id: string;
  hospitalId: string;
  hospitalName: string;
  queueSessionId: string;
  departmentId: string;
  departmentName: string;
  doctorId: string;
  doctorName: string;
  patientUserId: string;
  patientName: string;
  tokenNumber: number;
  status: 'WAITING' | 'CALLED' | 'SERVING' | 'COMPLETED' | 'CANCELLED' | 'NO_SHOW';
  bookingTime: string;
  estimatedServiceTime?: string;
  actualCallTime?: string;
  actualStartTime?: string;
  actualEndTime?: string;
  positionInQueue: number;
  estimatedWaitMinutes: number;
  predictionMethod?: string;
  cancellationReason?: string;
  isBuildingCheckedIn?: boolean;
  buildingCheckInTime?: string;
  isDoctorRoomCheckedIn?: boolean;
  doctorRoomCheckInTime?: string;
  callTimeoutTime?: string;
}

export interface QueueStatus {
  bookingId: string;
  tokenNumber: number;
  bookingStatus: string;
  positionInQueue: number;
  currentlyServingToken: number;
  totalWaiting: number;
  estimatedWaitMinutes: number;
  predictionMethod: string;
  methodExplanation: string;
  sessionStatus: string;
  lastUpdated: string;
}

export interface AIModelMetrics {
  id?: string;
  modelType: string;
  hospitalId?: string;
  versionTag: string;
  trainingRecordCount: number;
  meanAbsoluteError: number;
  baselineMAE: number;
  isActive: boolean;
  trainedAt: string;
  status: string;
}

export interface HospitalAnalytics {
  hospitalId: string;
  hospitalName: string;
  totalBookingsToday: number;
  waitingPatients: number;
  completedConsultations: number;
  cancelledBookings: number;
  noShowCount: number;
  averageActualConsultationMinutes: number;
  averageActualWaitingMinutes: number;
  estimatedVsActualVarianceMinutes: number;
  departmentStats: {
    departmentId: string;
    departmentName: string;
    totalBookings: number;
    waitingCount: number;
    completedCount: number;
    averageConsultationDuration: number;
  }[];
  hourlyStats: {
    hour: number;
    avgPredictedWait: number;
    avgActualWait: number;
  }[];
}

export interface PlatformAnalytics {
  totalRegisteredHospitals: number;
  pendingApprovals: number;
  approvedHospitals: number;
  suspendedHospitals: number;
  totalPatients: number;
  totalBookings: number;
  totalConsultationsCompleted: number;
  activeModels: AIModelMetrics[];
}

export interface NotificationItem {
  id: string;
  title: string;
  message: string;
  type: string;
  relatedEntityId?: string;
  isRead: boolean;
  createdAt: string;
}
