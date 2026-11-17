import React, { useState, useEffect } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { api } from '../services/api';
import { getSignalRConnection, startSignalR } from '../services/signalr';
import { NotificationItem, UserRole } from '../types';
import { Bell, Activity, User, LogOut, ChevronDown, CheckCircle2, Menu, X } from 'lucide-react';

export const Navbar: React.FC = () => {
  const { user, logout, quickSwitchAccount } = useAuth();
  const navigate = useNavigate();
  const [notifications, setNotifications] = useState<NotificationItem[]>([]);
  const [showNotifications, setShowNotifications] = useState(false);
  const [showSwitchMenu, setShowSwitchMenu] = useState(false);
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  useEffect(() => {
    if (user) {
      api.getNotifications().then(setNotifications).catch(() => {});

      startSignalR().then((conn) => {
        conn.invoke('JoinPatientChannel', user.id).catch(() => {});
        conn.on('NotificationReceived', (newNotif: NotificationItem) => {
          setNotifications((prev) => [newNotif, ...prev]);
        });
      });
    }
    return () => {
      const conn = getSignalRConnection();
      conn.off('NotificationReceived');
    };
  }, [user]);

  const unreadCount = notifications.filter((n) => !n.isRead).length;

  const handleMarkRead = async (id: string) => {
    await api.markNotificationRead(id);
    setNotifications((prev) => prev.map((n) => (n.id === id ? { ...n, isRead: true } : n)));
  };

  const handleRoleSwitch = async (role: UserRole, customEmail?: string) => {
    setShowSwitchMenu(false);
    await quickSwitchAccount(role, customEmail);
    if (role === 'PlatformAdmin') navigate('/platform');
    else if (role === 'HospitalAdmin') navigate('/hospital');
    else if (role === 'Doctor') navigate('/doctor');
    else navigate('/patient');
  };

  return (
    <header className="sticky top-0 z-40 bg-white border-b border-slate-200 shadow-sm">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex items-center justify-between h-16">
          {/* Logo */}
          <div className="flex items-center space-x-3">
            <Link to="/" className="flex items-center space-x-2.5">
              <div className="w-10 h-10 rounded-xl bg-teal-600 flex items-center justify-center text-white shadow-md shadow-teal-600/20">
                <Activity className="w-6 h-6 stroke-[2.5]" />
              </div>
              <span className="text-xl font-bold tracking-tight text-slate-900">
                Queue<span className="text-teal-600">Less</span>
              </span>
            </Link>
            <span className="hidden md:inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-teal-50 text-teal-700 border border-teal-200">
              AI-Powered Multi-Hospital
            </span>
          </div>

          {/* Nav links */}
          <nav className="hidden md:flex items-center space-x-6 text-sm font-medium text-slate-600">
            <Link to="/hospitals" className="hover:text-teal-600 transition">Find Hospitals</Link>
            {user?.role === 'Patient' && (
              <Link to="/patient" className="hover:text-teal-600 transition">My Queue & Bookings</Link>
            )}
            {user?.role === 'HospitalAdmin' && (
              <Link to="/hospital" className="hover:text-teal-600 transition">Hospital Management</Link>
            )}
            {user?.role === 'Doctor' && (
              <Link to="/doctor" className="hover:text-teal-600 transition">Doctor Queue Console</Link>
            )}
            {user?.role === 'PlatformAdmin' && (
              <Link to="/platform" className="hover:text-teal-600 transition">Platform Admin</Link>
            )}
          </nav>

          {/* Right section: demo switcher, notifications, user profile */}
          <div className="flex items-center space-x-3">
            {/* Quick Demo Role Switcher */}
            <div className="relative">
              <button
                onClick={() => setShowSwitchMenu(!showSwitchMenu)}
                className="flex items-center space-x-1.5 px-3 py-1.5 rounded-lg border border-amber-300 bg-amber-50 text-amber-900 text-xs font-semibold hover:bg-amber-100 transition shadow-sm"
                title="Switch persona for evaluation"
              >
                <span>Demo Persona: {user ? user.role : 'Guest'}</span>
                <ChevronDown className="w-3.5 h-3.5" />
              </button>

              {showSwitchMenu && (
                <div className="absolute right-0 mt-2 w-64 bg-white border border-slate-200 rounded-xl shadow-xl py-2 z-50">
                  <div className="px-3 py-1.5 text-xs font-semibold text-slate-400 uppercase tracking-wider border-b border-slate-100">
                    Switch Test Account
                  </div>
                  <button
                    onClick={() => handleRoleSwitch('Patient', 'patient.arun@queueless.local')}
                    className="w-full text-left px-3 py-2 text-xs hover:bg-teal-50 flex items-center justify-between text-slate-700"
                  >
                    <div>
                      <div className="font-semibold text-slate-900">Patient 1 (Arun Kumar)</div>
                      <div className="text-[10px] text-slate-500">patient.arun@queueless.local</div>
                    </div>
                    {user?.email === 'patient.arun@queueless.local' && <CheckCircle2 className="w-4 h-4 text-teal-600" />}
                  </button>
                  <button
                    onClick={() => handleRoleSwitch('Patient', 'patient.priya@queueless.local')}
                    className="w-full text-left px-3 py-2 text-xs hover:bg-teal-50 flex items-center justify-between text-slate-700"
                  >
                    <div>
                      <div className="font-semibold text-slate-900">Patient 2 (Priya Sharma)</div>
                      <div className="text-[10px] text-slate-500">patient.priya@queueless.local</div>
                    </div>
                    {user?.email === 'patient.priya@queueless.local' && <CheckCircle2 className="w-4 h-4 text-teal-600" />}
                  </button>
                  <button
                    onClick={() => handleRoleSwitch('Patient', 'patient.rahul@queueless.local')}
                    className="w-full text-left px-3 py-2 text-xs hover:bg-teal-50 flex items-center justify-between text-slate-700"
                  >
                    <div>
                      <div className="font-semibold text-slate-900">Patient 3 (Rahul Verma)</div>
                      <div className="text-[10px] text-slate-500">patient.rahul@queueless.local</div>
                    </div>
                    {user?.email === 'patient.rahul@queueless.local' && <CheckCircle2 className="w-4 h-4 text-teal-600" />}
                  </button>
                  <button
                    onClick={() => handleRoleSwitch('HospitalAdmin')}
                    className="w-full text-left px-3 py-2 text-xs hover:bg-teal-50 flex items-center justify-between text-slate-700"
                  >
                    <div>
                      <div className="font-semibold text-slate-900">Apollo Admin</div>
                      <div className="text-slate-500">apollo.admin@queueless.local</div>
                    </div>
                    {user?.role === 'HospitalAdmin' && <CheckCircle2 className="w-4 h-4 text-teal-600" />}
                  </button>
                  <button
                    onClick={() => handleRoleSwitch('Doctor')}
                    className="w-full text-left px-3 py-2 text-xs hover:bg-teal-50 flex items-center justify-between text-slate-700"
                  >
                    <div>
                      <div className="font-semibold text-slate-900">Dr. Rajesh (Doctor)</div>
                      <div className="text-slate-500">doctor.rajesh@queueless.local</div>
                    </div>
                    {user?.role === 'Doctor' && <CheckCircle2 className="w-4 h-4 text-teal-600" />}
                  </button>
                  <button
                    onClick={() => handleRoleSwitch('PlatformAdmin')}
                    className="w-full text-left px-3 py-2 text-xs hover:bg-teal-50 flex items-center justify-between text-slate-700"
                  >
                    <div>
                      <div className="font-semibold text-slate-900">Platform Admin</div>
                      <div className="text-slate-500">platformadmin@queueless.local</div>
                    </div>
                    {user?.role === 'PlatformAdmin' && <CheckCircle2 className="w-4 h-4 text-teal-600" />}
                  </button>
                </div>
              )}
            </div>

            {/* Notifications */}
            {user && (
              <div className="relative">
                <button
                  onClick={() => setShowNotifications(!showNotifications)}
                  className="p-2 text-slate-500 hover:text-slate-700 hover:bg-slate-100 rounded-lg relative transition"
                >
                  <Bell className="w-5 h-5" />
                  {unreadCount > 0 && (
                    <span className="absolute top-1 right-1 w-4 h-4 rounded-full bg-rose-500 text-white text-[10px] font-bold flex items-center justify-center">
                      {unreadCount}
                    </span>
                  )}
                </button>

                {showNotifications && (
                  <div className="absolute right-0 mt-2 w-80 bg-white border border-slate-200 rounded-xl shadow-xl py-2 z-50">
                    <div className="px-4 py-2 border-b border-slate-100 flex items-center justify-between">
                      <span className="font-semibold text-sm text-slate-800">Notifications</span>
                      <span className="text-xs text-slate-500">{notifications.length} total</span>
                    </div>
                    <div className="max-h-72 overflow-y-auto divide-y divide-slate-100">
                      {notifications.length === 0 ? (
                        <div className="p-4 text-center text-xs text-slate-400">No notifications yet</div>
                      ) : (
                        notifications.map((n) => (
                          <div
                            key={n.id}
                            onClick={() => handleMarkRead(n.id)}
                            className={`p-3 text-xs cursor-pointer hover:bg-slate-50 transition ${
                              !n.isRead ? 'bg-teal-50/50 font-medium' : 'text-slate-600'
                            }`}
                          >
                            <div className="font-semibold text-slate-800">{n.title}</div>
                            <div className="text-slate-600 mt-0.5">{n.message}</div>
                            <div className="text-[10px] text-slate-400 mt-1">
                              {new Date(n.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                            </div>
                          </div>
                        ))
                      )}
                    </div>
                  </div>
                )}
              </div>
            )}

            {/* User Login/Logout */}
            {user ? (
              <div className="flex items-center space-x-2 pl-2 border-l border-slate-200">
                <div className="text-right hidden sm:block">
                  <div className="text-xs font-semibold text-slate-800">{user.fullName}</div>
                  <div className="text-[10px] font-medium text-teal-600">{user.role}</div>
                </div>
                <button
                  onClick={logout}
                  className="p-2 text-slate-500 hover:text-rose-600 hover:bg-rose-50 rounded-lg transition"
                  title="Logout"
                >
                  <LogOut className="w-4 h-4" />
                </button>
              </div>
            ) : (
              <div className="hidden sm:flex items-center space-x-2">
                <Link
                  to="/login"
                  className="px-3 py-1.5 text-xs font-medium text-slate-700 hover:text-teal-600 transition"
                >
                  Sign In
                </Link>
                <Link
                  to="/register"
                  className="px-3.5 py-1.5 text-xs font-semibold text-white bg-teal-600 rounded-lg hover:bg-teal-700 transition shadow-sm"
                >
                  Register
                </Link>
              </div>
            )}

            {/* Mobile Hamburger Button */}
            <div className="flex md:hidden items-center ml-1">
              <button
                onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
                className="p-2 rounded-lg text-slate-600 hover:text-slate-900 hover:bg-slate-100 transition focus:outline-none"
                aria-label="Toggle menu"
              >
                {mobileMenuOpen ? <X className="w-5 h-5" /> : <Menu className="w-5 h-5" />}
              </button>
            </div>
          </div>
        </div>
      </div>

      {/* Mobile Drawer Menu */}
      {mobileMenuOpen && (
        <div className="md:hidden border-t border-slate-200 bg-white px-4 pt-3 pb-6 space-y-3 shadow-lg animate-in slide-in-from-top-2 duration-150">
          <nav className="flex flex-col space-y-2 text-sm font-medium text-slate-700">
            <Link
              to="/hospitals"
              onClick={() => setMobileMenuOpen(false)}
              className="px-3 py-2 rounded-lg hover:bg-slate-100 transition"
            >
              Find Hospitals
            </Link>
            {user?.role === 'Patient' && (
              <Link
                to="/patient"
                onClick={() => setMobileMenuOpen(false)}
                className="px-3 py-2 rounded-lg hover:bg-slate-100 transition"
              >
                My Queue & Bookings
              </Link>
            )}
            {user?.role === 'HospitalAdmin' && (
              <Link
                to="/hospital"
                onClick={() => setMobileMenuOpen(false)}
                className="px-3 py-2 rounded-lg hover:bg-slate-100 transition"
              >
                Hospital Management
              </Link>
            )}
            {user?.role === 'Doctor' && (
              <Link
                to="/doctor"
                onClick={() => setMobileMenuOpen(false)}
                className="px-3 py-2 rounded-lg hover:bg-slate-100 transition"
              >
                Doctor Queue Console
              </Link>
            )}
            {user?.role === 'PlatformAdmin' && (
              <Link
                to="/platform"
                onClick={() => setMobileMenuOpen(false)}
                className="px-3 py-2 rounded-lg hover:bg-slate-100 transition"
              >
                Platform Admin
              </Link>
            )}
          </nav>

          {!user && (
            <div className="pt-3 border-t border-slate-100 flex items-center space-x-3">
              <Link
                to="/login"
                onClick={() => setMobileMenuOpen(false)}
                className="flex-1 py-2 text-center text-xs font-semibold text-slate-700 border border-slate-200 rounded-lg hover:bg-slate-50 transition"
              >
                Sign In
              </Link>
              <Link
                to="/register"
                onClick={() => setMobileMenuOpen(false)}
                className="flex-1 py-2 text-center text-xs font-semibold text-white bg-teal-600 rounded-lg hover:bg-teal-700 transition"
              >
                Register
              </Link>
            </div>
          )}
        </div>
      )}
    </header>
  );
};
