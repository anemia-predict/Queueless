import React, { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../../services/api';
import { Hospital } from '../../types';
import { Search, MapPin, Building2, Stethoscope, ArrowRight, Activity, Filter } from 'lucide-react';
import { Badge } from '../../components/Badge';

export const HospitalSearch: React.FC = () => {
  const [hospitals, setHospitals] = useState<Hospital[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchName, setSearchName] = useState('');
  const [searchCity, setSearchCity] = useState('');
  const [searchDept, setSearchDept] = useState('');

  const fetchHospitals = async () => {
    setLoading(true);
    try {
      const data = await api.searchHospitals({
        name: searchName || undefined,
        city: searchCity || undefined,
        department: searchDept || undefined,
      });
      setHospitals(data);
    } catch (err) {
      console.error('Failed to load hospitals:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchHospitals();
  }, []);

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    fetchHospitals();
  };

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-10">
      {/* Header */}
      <div className="mb-8">
        <h1 className="text-3xl font-extrabold text-slate-900 tracking-tight">Search Verified Hospitals</h1>
        <p className="mt-1 text-sm text-slate-500">
          Discover approved multi-hospital partners, browse medical departments, and reserve your queue slot.
        </p>
      </div>

      {/* Filter Bar */}
      <form
        onSubmit={handleSearchSubmit}
        className="bg-white p-4 rounded-2xl border border-slate-200 shadow-sm mb-8 grid grid-cols-1 sm:grid-cols-4 gap-3"
      >
        <div className="relative">
          <Search className="w-4 h-4 absolute left-3.5 top-3.5 text-slate-400" />
          <input
            type="text"
            placeholder="Hospital name..."
            value={searchName}
            onChange={(e) => setSearchName(e.target.value)}
            className="w-full pl-9 pr-3 py-2 text-sm rounded-xl border border-slate-200 focus:outline-none focus:border-teal-500"
          />
        </div>

        <div className="relative">
          <MapPin className="w-4 h-4 absolute left-3.5 top-3.5 text-slate-400" />
          <input
            type="text"
            placeholder="City (e.g. Bengaluru, Mumbai)..."
            value={searchCity}
            onChange={(e) => setSearchCity(e.target.value)}
            className="w-full pl-9 pr-3 py-2 text-sm rounded-xl border border-slate-200 focus:outline-none focus:border-teal-500"
          />
        </div>

        <div className="relative">
          <Stethoscope className="w-4 h-4 absolute left-3.5 top-3.5 text-slate-400" />
          <input
            type="text"
            placeholder="Department (e.g. Cardiology)..."
            value={searchDept}
            onChange={(e) => setSearchDept(e.target.value)}
            className="w-full pl-9 pr-3 py-2 text-sm rounded-xl border border-slate-200 focus:outline-none focus:border-teal-500"
          />
        </div>

        <button
          type="submit"
          className="w-full py-2 px-4 rounded-xl bg-teal-600 text-white font-semibold text-sm hover:bg-teal-700 transition flex items-center justify-center space-x-2 shadow-sm"
        >
          <Filter className="w-4 h-4" />
          <span>Filter Results</span>
        </button>
      </form>

      {/* Results */}
      {loading ? (
        <div className="text-center py-16">
          <div className="w-10 h-10 border-4 border-teal-600 border-t-transparent rounded-full animate-spin mx-auto mb-3" />
          <p className="text-sm text-slate-500">Searching active hospitals...</p>
        </div>
      ) : hospitals.length === 0 ? (
        <div className="text-center py-16 bg-white rounded-2xl border border-slate-200 p-8">
          <Building2 className="w-12 h-12 text-slate-300 mx-auto mb-3" />
          <h3 className="text-base font-bold text-slate-700">No Hospitals Found</h3>
          <p className="mt-1 text-sm text-slate-500">Try clearing filters or searching another city or department.</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {hospitals.map((h) => (
            <div
              key={h.id}
              className="bg-white rounded-2xl border border-slate-200 p-6 shadow-sm hover:shadow-md transition flex flex-col justify-between"
            >
              <div>
                <div className="flex items-start justify-between">
                  <h3 className="text-lg font-bold text-slate-900">{h.name}</h3>
                  <Badge status={h.status} />
                </div>

                <div className="mt-2 flex items-center text-xs text-slate-500 space-x-1">
                  <MapPin className="w-3.5 h-3.5 text-slate-400" />
                  <span>{h.city}{h.area ? `, ${h.area}` : ''}</span>
                </div>

                <p className="mt-2 text-xs text-slate-600 line-clamp-2">{h.address}</p>

                <div className="mt-4 pt-4 border-t border-slate-100 grid grid-cols-2 gap-2 text-xs text-slate-600">
                  <div className="bg-slate-50 p-2 rounded-xl text-center">
                    <span className="block font-bold text-slate-900 text-sm">{h.departmentCount}</span>
                    <span className="text-slate-500 text-[11px]">Departments</span>
                  </div>
                  <div className="bg-slate-50 p-2 rounded-xl text-center">
                    <span className="block font-bold text-slate-900 text-sm">{h.doctorCount}</span>
                    <span className="text-slate-500 text-[11px]">Doctors</span>
                  </div>
                </div>
              </div>

              <div className="mt-6">
                <Link
                  to={`/hospitals/${h.id}`}
                  className="w-full py-2.5 px-4 rounded-xl bg-teal-50 hover:bg-teal-100 text-teal-800 text-xs font-semibold flex items-center justify-center space-x-1.5 transition"
                >
                  <span>View Doctors & Book Token</span>
                  <ArrowRight className="w-3.5 h-3.5" />
                </Link>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};
