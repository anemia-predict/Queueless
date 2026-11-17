import React, { createContext, useContext, useState, useEffect } from 'react';
import { UserProfile, UserRole } from '../types';
import { api } from '../services/api';

interface AuthContextType {
  user: UserProfile | null;
  token: string | null;
  isLoading: boolean;
  login: (email: string, pass: string) => Promise<UserProfile>;
  register: (data: any) => Promise<UserProfile>;
  logout: () => void;
  quickSwitchAccount: (role: UserRole, customEmail?: string) => Promise<void>;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<UserProfile | null>(null);
  const [token, setToken] = useState<string | null>(localStorage.getItem('queueless_token'));
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const initAuth = async () => {
      const storedToken = localStorage.getItem('queueless_token');
      if (storedToken) {
        try {
          const profile = await api.getMe();
          setUser(profile);
        } catch {
          localStorage.removeItem('queueless_token');
          setToken(null);
          setUser(null);
        }
      }
      setIsLoading(false);
    };

    initAuth();
  }, []);

  const login = async (email: string, pass: string): Promise<UserProfile> => {
    const res = await api.login({ email, password: pass });
    localStorage.setItem('queueless_token', res.token);
    setToken(res.token);
    setUser(res.user);
    return res.user;
  };

  const register = async (data: any): Promise<UserProfile> => {
    const res = await api.register(data);
    localStorage.setItem('queueless_token', res.token);
    setToken(res.token);
    setUser(res.user);
    return res.user;
  };

  const logout = () => {
    localStorage.removeItem('queueless_token');
    setToken(null);
    setUser(null);
  };

  const quickSwitchAccount = async (role: UserRole, customEmail?: string) => {
    let creds = { email: customEmail || '', pass: 'Patient@123' };
    if (!customEmail) {
      switch (role) {
        case 'PlatformAdmin':
          creds = { email: 'platformadmin@queueless.local', pass: 'Admin@123456' };
          break;
        case 'HospitalAdmin':
          creds = { email: 'apollo.admin@queueless.local', pass: 'Hospital@123' };
          break;
        case 'Doctor':
          creds = { email: 'doctor.rajesh@queueless.local', pass: 'Doctor@123' };
          break;
        case 'Patient':
          creds = { email: 'patient.arun@queueless.local', pass: 'Patient@123' };
          break;
      }
    }
    await login(creds.email, creds.pass);
  };

  return (
    <AuthContext.Provider
      value={{
        user,
        token,
        isLoading,
        login,
        register,
        logout,
        quickSwitchAccount,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth must be used within an AuthProvider');
  return context;
};
