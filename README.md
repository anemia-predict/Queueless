# QueueLess – Multi-Hospital Queue Management & AI Waiting-Time Prediction

QueueLess is an enterprise multi-hospital queue management platform with real-time SignalR broadcasts and an ML.NET regression engine for transparent waiting-time predictions.

---

## 1. Prerequisites

- **.NET 8 SDK**
- **Node.js 18+ & npm**
- **PostgreSQL 16+** (via Docker Compose or local instance on port 5433/5432)

---

## 2. Quick Local Startup (Windows)

### Step 1: Start PostgreSQL Database
Choose either Docker Compose or local PostgreSQL:

**Option A (Docker Compose):**
```powershell
docker compose up -d
```

**Option B (Local Native PostgreSQL on Port 5433):**
```powershell
& "C:\Program Files\PostgreSQL\18\bin\postgres.exe" -D "c:\Users\maria\Desktop\qms\db_data" -p 5433
```

### Step 2: Run EF Core Database Migrations & Seeding
*(The API automatically applies migrations and seeds demo data on launch!)*
```powershell
dotnet run --project backend/QueueLess.Api --urls "http://localhost:5000"
```
- **Backend API**: `http://localhost:5000`
- **Interactive Swagger UI**: `http://localhost:5000/swagger`
- **SignalR Hub**: `http://localhost:5000/hubs/queue`

### Step 3: Run Frontend (React + Vite)
In a separate terminal:
```powershell
cd frontend
npm install
npm run dev
```
- **Frontend URL**: `http://localhost:5173`

---

## 3. Demo Login Credentials (Seeded Automatically)

The application provides a **1-click Demo Persona switcher** in the top navigation bar, or you can log in manually:

| Persona | Email | Password | Role Description |
|---|---|---|---|
| **Patient Arun** | `patient.arun@queueless.local` | `Patient@123` | Searches hospitals, books tokens, tracks live queue |
| **Apollo Admin** | `apollo.admin@queueless.local` | `Hospital@123` | Manages Apollo City Hospital (Approved) |
| **Dr. Rajesh Sharma** | `doctor.rajesh@queueless.local` | `Doctor@123` | Doctor console: calls tokens, starts & completes consultations |
| **Platform Admin** | `platformadmin@queueless.local` | `Admin@123456` | Approves/rejects hospitals, monitors platform models |

---

## 4. Running Automated Tests

### Backend xUnit Tests:
```powershell
dotnet test backend/QueueLess.Tests
```
*Result: 18 passed, 0 failed.*

### Frontend Vitest Tests:
```powershell
cd frontend
npm test
```
*Result: 5 passed, 0 failed.*

---

## 5. End-to-End Workflow Verification Scenario

1. **Hospital Onboarding**: Navigate to `/register`, click "Hospital Onboarding", and register a new clinic. It will be created with `PENDING` status.
2. **Platform Approval**: Switch persona to **Platform Admin** (`/platform`), view the pending request, and click **Approve**.
3. **Queue Configuration**: Switch to **Hospital Admin** (`/hospital`), create departments (e.g. Ophthalmology), add doctors, and open a queue session.
4. **Patient Discovery & Booking**: Switch to **Patient Arun** (`/hospitals`), find Apollo Hospital, select Dr. Rajesh, and click **Book Queue Token**.
5. **Instant Token Generation**: An atomic token number (e.g. `#1`) is generated along with an AI waiting time prediction.
6. **Live Doctor Console**: Switch to **Dr. Rajesh Sharma** (`/doctor`), see the waiting patient, click **CALL NEXT PATIENT** -> **Start Consultation** -> **Complete Consultation**.
7. **Consultation Record & AI Pipeline**: Actual duration ($EndTime - StartTime$) is persisted in `ConsultationRecords` and used to calibrate the ML model.
8. **Multi-Tenant Protection**: Hospital A administrators are barred from viewing or altering Hospital B doctors, schedules, or private bookings.

---

## 6. Architecture & Documentation Directory

- [Architecture Overview](file:///c:/Users/maria/Desktop/qms/docs/architecture.md)
- [Database Schema & ER Diagram](file:///c:/Users/maria/Desktop/qms/docs/database-design.md)
- [API Specifications](file:///c:/Users/maria/Desktop/qms/docs/api-documentation.md)
- [AI Prediction Methodology](file:///c:/Users/maria/Desktop/qms/docs/ai-methodology.md)
- [Security & Tenant Isolation](file:///c:/Users/maria/Desktop/qms/docs/security.md)
- [Testing Report](file:///c:/Users/maria/Desktop/qms/docs/testing-report.md)
- [Development Status](file:///c:/Users/maria/Desktop/qms/DEVELOPMENT_STATUS.md)
