import React from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider, useAuth } from './contexts/AuthContext';
import { Navbar } from './components/Navbar';
import { Home } from './pages/public/Home';
import { HospitalSearch } from './pages/public/HospitalSearch';
import { HospitalDetail } from './pages/public/HospitalDetail';
import { Login } from './pages/public/Login';
import { Register } from './pages/public/Register';
import { PatientDashboard } from './pages/patient/PatientDashboard';
import { HospitalDashboard } from './pages/hospital/HospitalDashboard';
import { DoctorDashboard } from './pages/doctor/DoctorDashboard';
import { PlatformDashboard } from './pages/platform-admin/PlatformDashboard';

const ProtectedRoute: React.FC<{ children: React.ReactNode; allowedRoles?: string[] }> = ({
  children,
  allowedRoles,
}) => {
  const { user, isLoading } = useAuth();

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="w-8 h-8 border-4 border-teal-600 border-t-transparent rounded-full animate-spin" />
      </div>
    );
  }

  if (!user) {
    return <Navigate to="/login" replace />;
  }

  if (allowedRoles && !allowedRoles.includes(user.role)) {
    return <Navigate to="/" replace />;
  }

  return <>{children}</>;
};

export const AppContent: React.FC = () => {
  return (
    <div className="min-h-screen bg-slate-50 flex flex-col font-sans">
      <Navbar />
      <main className="flex-1">
        <Routes>
          {/* Public Routes */}
          <Route path="/" element={<Home />} />
          <Route path="/hospitals" element={<HospitalSearch />} />
          <Route path="/hospitals/:id" element={<HospitalDetail />} />
          <Route path="/login" element={<Login />} />
          <Route path="/register" element={<Register />} />

          {/* Role Protected Routes */}
          <Route
            path="/patient"
            element={
              <ProtectedRoute allowedRoles={['Patient', 'PlatformAdmin', 'HospitalAdmin']}>
                <PatientDashboard />
              </ProtectedRoute>
            }
          />
          <Route
            path="/hospital"
            element={
              <ProtectedRoute allowedRoles={['HospitalAdmin', 'PlatformAdmin']}>
                <HospitalDashboard />
              </ProtectedRoute>
            }
          />
          <Route
            path="/doctor"
            element={
              <ProtectedRoute allowedRoles={['Doctor', 'HospitalAdmin', 'PlatformAdmin']}>
                <DoctorDashboard />
              </ProtectedRoute>
            }
          />
          <Route
            path="/platform"
            element={
              <ProtectedRoute allowedRoles={['PlatformAdmin']}>
                <PlatformDashboard />
              </ProtectedRoute>
            }
          />

          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </main>
      <footer className="bg-white border-t border-slate-200 py-6 text-center text-xs text-slate-500">
        QueueLess – Multi-Hospital Queue Management & AI Waiting-Time Engine &copy; 2026. Local MVP running on localhost.
      </footer>
    </div>
  );
};

export const App: React.FC = () => {
  return (
    <BrowserRouter>
      <AuthProvider>
        <AppContent />
      </AuthProvider>
    </BrowserRouter>
  );
};

export default App;
