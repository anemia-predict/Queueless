# QueueLess REST API Documentation

All endpoints are hosted under `/api/v1` and return JSON payloads. Authentication uses standard HTTP `Authorization: Bearer <token>` headers.

---

## 1. Authentication (`/api/v1/auth`)

| Method | Endpoint | Access | Description |
|---|---|---|---|
| `POST` | `/api/v1/auth/register` | Public | Register new Patient or Hospital Admin |
| `POST` | `/api/v1/auth/login` | Public | Authenticate user and receive JWT Bearer token |
| `GET` | `/api/v1/auth/me` | Authenticated | Retrieve profile and hospital membership |

---

## 2. Hospital Discovery (`/api/v1/hospitals`)

| Method | Endpoint | Access | Description |
|---|---|---|---|
| `GET` | `/api/v1/hospitals` | Public | Search approved hospitals by name, city, department |
| `GET` | `/api/v1/hospitals/{id}` | Public | Get hospital profile details |
| `GET` | `/api/v1/hospitals/{id}/departments` | Public | List active departments in hospital |
| `GET` | `/api/v1/hospitals/{id}/doctors` | Public | List active doctors (optional `?departmentId=`) |

---

## 3. Hospital Registration (`/api/v1/hospital-registration`)

| Method | Endpoint | Access | Description |
|---|---|---|---|
| `POST` | `/api/v1/hospital-registration` | Authenticated | Submit hospital details for platform approval (Status: `PENDING`) |

---

## 4. Hospital Management (`/api/v1/hospital`)

| Method | Endpoint | Access | Description |
|---|---|---|---|
| `GET` | `/api/v1/hospital/me` | HospitalAdmin | Get authenticated admin's hospital profile |
| `PATCH` | `/api/v1/hospital/me` | HospitalAdmin | Update hospital profile & initial consultation estimate |
| `GET` | `/api/v1/hospital/departments` | HospitalAdmin | List hospital departments |
| `POST` | `/api/v1/hospital/departments` | HospitalAdmin | Create new department |
| `PUT` | `/api/v1/hospital/departments/{id}` | HospitalAdmin | Update department name or default duration |
| `GET` | `/api/v1/hospital/doctors` | HospitalAdmin | List hospital doctors |
| `POST` | `/api/v1/hospital/doctors` | HospitalAdmin | Register new doctor and assign departments |
| `PUT` | `/api/v1/hospital/doctors/{id}` | HospitalAdmin | Update doctor details |
| `GET` | `/api/v1/hospital/schedules` | HospitalAdmin | List doctor schedules |
| `POST` | `/api/v1/hospital/schedules` | HospitalAdmin | Create schedule for doctor & department |
| `GET` | `/api/v1/hospital/analytics` | HospitalAdmin | Hospital analytics: bookings, durations, waiting stats |

---

## 5. Patient Bookings (`/api/v1/bookings`)

| Method | Endpoint | Access | Description |
|---|---|---|---|
| `POST` | `/api/v1/bookings` | Patient | Book token with atomic transaction & idempotency check |
| `GET` | `/api/v1/bookings/me` | Patient | List patient's bookings across all hospitals |
| `GET` | `/api/v1/bookings/{id}` | Authenticated | Get booking details & estimated wait time |
| `POST` | `/api/v1/bookings/{id}/cancel` | Patient/Admin | Cancel waiting booking token |
| `GET` | `/api/v1/bookings/{id}/queue-status` | Authenticated | Live queue status, position, wait prediction |

---

## 6. Live Queue Operations (`/api/v1/hospital/queues` & `/api/v1/hospital/bookings`)

| Method | Endpoint | Access | Description |
|---|---|---|---|
| `GET` | `/api/v1/hospital/queues` | Doctor/Admin | List active and scheduled queue sessions |
| `GET` | `/api/v1/hospital/queues/{id}` | Doctor/Admin | Get queue session details and waiting list |
| `POST` | `/api/v1/hospital/queues` | Doctor/Admin | Open new live queue session |
| `POST` | `/api/v1/hospital/queues/{id}/call-next` | Doctor/Operator | Call next waiting patient (emits SignalR event) |
| `POST` | `/api/v1/hospital/bookings/{id}/start` | Doctor/Operator | Start consultation and record `ActualStartTime` |
| `POST` | `/api/v1/hospital/bookings/{id}/complete` | Doctor/Operator | Complete consultation, record duration in `ConsultationRecords` |
| `POST` | `/api/v1/hospital/bookings/{id}/no-show` | Doctor/Operator | Mark patient as `NO_SHOW` |
| `POST` | `/api/v1/hospital/queues/{id}/pause` | Doctor/Operator | Pause queue session |
| `POST` | `/api/v1/hospital/queues/{id}/resume` | Doctor/Operator | Resume paused queue session |

---

## 7. Platform Administration (`/api/v1/platform`)

| Method | Endpoint | Access | Description |
|---|---|---|---|
| `GET` | `/api/v1/platform/hospitals` | PlatformAdmin | List all hospitals across the platform |
| `GET` | `/api/v1/platform/hospitals/pending` | PlatformAdmin | List pending hospital registrations |
| `POST` | `/api/v1/platform/hospitals/{id}/approve` | PlatformAdmin | Approve hospital onboarding request |
| `POST` | `/api/v1/platform/hospitals/{id}/reject` | PlatformAdmin | Reject hospital onboarding request |
| `POST` | `/api/v1/platform/hospitals/{id}/suspend` | PlatformAdmin | Suspend hospital operations |
| `GET` | `/api/v1/platform/analytics` | PlatformAdmin | Platform-wide metrics, active models, hospital count |

---

## 8. AI Waiting-Time Engine (`/api/v1/ai`, `/api/v1/hospital/ai`, `/api/v1/platform/ai`)

| Method | Endpoint | Access | Description |
|---|---|---|---|
| `GET` | `/api/v1/ai/waiting-time/{bookingId}` | Authenticated | Calculate waiting time & explanation |
| `GET` | `/api/v1/hospital/ai/metrics` | HospitalAdmin | Get hospital model metrics, version, and MAE |
| `POST` | `/api/v1/hospital/ai/seed-data` | HospitalAdmin | Seed clearly marked synthetic records for testing |
| `POST` | `/api/v1/hospital/ai/train` | HospitalAdmin | Train ML.NET FastForest model on consultation data |
| `GET` | `/api/v1/platform/ai/models` | PlatformAdmin | List all trained ML model versions |
| `POST` | `/api/v1/platform/ai/train-shared` | PlatformAdmin | Train multi-hospital shared ML model |

---

## 9. Real-Time SignalR Hub (`/hubs/queue`)

- Client methods: `JoinQueueSession(sessionId)`, `LeaveQueueSession(sessionId)`, `JoinPatientChannel(patientUserId)`
- Server events: `QueueUpdated`, `TokenCalled`, `ConsultationStarted`, `ConsultationCompleted`, `NotificationReceived`
