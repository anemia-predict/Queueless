using System.Text;
using Microsoft.EntityFrameworkCore;
using QueueLess.Api.Data;
using QueueLess.Api.Models;

namespace QueueLess.Api.Services;

public class HospitalAiChatService : IHospitalAiChatService
{
    private readonly ApplicationDbContext _db;

    public HospitalAiChatService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<HospitalAiChatResponseDto> ProcessQueryAsync(Guid hospitalId, string userMessage)
    {
        var msg = userMessage.Trim().ToLowerInvariant();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayStart = DateTime.UtcNow.Date;

        var hospital = await _db.Hospitals.FirstOrDefaultAsync(h => h.Id == hospitalId);
        var hospitalName = hospital?.Name ?? "Hospital";

        // Category 1: Missed / No-Show tokens
        if (msg.Contains("miss") || msg.Contains("no show") || msg.Contains("noshow") || msg.Contains("forfeit") || msg.Contains("rejected"))
        {
            var noShows = await _db.Bookings
                .Include(b => b.Doctor)
                .Include(b => b.Department)
                .Where(b => b.HospitalId == hospitalId && b.Status == BookingStatus.NoShow && b.BookingTime >= todayStart)
                .ToListAsync();

            var cancelled = await _db.Bookings
                .Where(b => b.HospitalId == hospitalId && b.Status == BookingStatus.Cancelled && b.BookingTime >= todayStart)
                .CountAsync();

            var sb = new StringBuilder();
            sb.AppendLine($"### ❌ Missed & Forfeited Tokens Today ({hospitalName})\n");
            sb.AppendLine($"* **Total No-Shows Today:** **{noShows.Count}** token(s)");
            sb.AppendLine($"* **Patient Cancellations:** **{cancelled}** token(s)\n");

            if (noShows.Count > 0)
            {
                sb.AppendLine("Here is the breakdown of missed appointments:");
                foreach (var b in noShows.Take(10))
                {
                    var reason = string.IsNullOrWhiteSpace(b.CancellationReason) ? "2-min check-in window expired" : b.CancellationReason;
                    sb.AppendLine($"- **Token #{b.TokenNumber}** (Dr. {b.Doctor?.FullName ?? "N/A"}) — _{reason}_");
                }
            }
            else
            {
                sb.AppendLine("Great news! **Zero patients have missed** their consultations so far today.");
            }

            return new HospitalAiChatResponseDto
            {
                Reply = sb.ToString(),
                QueryCategory = "MISSED_TOKENS",
                StructuredData = new { NoShowCount = noShows.Count, CancelledCount = cancelled },
                SuggestedFollowUps = new List<string>
                {
                    "How many tokens gone today?",
                    "How many tokens remaining per doctor?",
                    "What are the things we did today?"
                }
            };
        }

        // Category 2: Remaining tokens per doctor / waiting per doctor
        if (msg.Contains("remaining") || msg.Contains("per doctor") || msg.Contains("doctor queue") || msg.Contains("left") || msg.Contains("how much token") || msg.Contains("waiting"))
        {
            var sessions = await _db.QueueSessions
                .Include(s => s.Doctor)
                .Include(s => s.Department)
                .Include(s => s.Bookings)
                .Where(s => s.HospitalId == hospitalId && (s.SessionDate == today || s.Status == QueueSessionStatus.Open))
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine($"### 🩺 Tokens Remaining Per Doctor ({hospitalName})\n");

            if (sessions.Count == 0)
            {
                sb.AppendLine("There are no active queue sessions running right now for today.");
            }
            else
            {
                sb.AppendLine("| Doctor | Department | Currently Serving | Remaining Waiting | Capacity |");
                sb.AppendLine("| :--- | :--- | :---: | :---: | :---: |");

                int totalRemaining = 0;
                int totalServing = 0;

                foreach (var s in sessions)
                {
                    var waitingCount = s.Bookings.Count(b => b.Status == BookingStatus.Waiting || b.Status == BookingStatus.Called);
                    var servingBooking = s.Bookings.FirstOrDefault(b => b.Status == BookingStatus.Serving);
                    var servingToken = servingBooking != null ? $"#{servingBooking.TokenNumber}" : "None";

                    totalRemaining += waitingCount;
                    if (servingBooking != null) totalServing++;

                    sb.AppendLine($"| **Dr. {s.Doctor?.FullName ?? "Doctor"}** | {s.Department?.Name ?? "Dept"} | {servingToken} | **{waitingCount}** | {s.Bookings.Count}/{s.MaxCapacity} |");
                }

                sb.AppendLine($"\n**Total Patients In Queue:** **{totalRemaining}** waiting, **{totalServing}** actively in consultation room.");
            }

            return new HospitalAiChatResponseDto
            {
                Reply = sb.ToString(),
                QueryCategory = "REMAINING_PER_DOCTOR",
                SuggestedFollowUps = new List<string>
                {
                    "How many tokens gone today?",
                    "How many has missed?",
                    "What did we do today?"
                }
            };
        }

        // Category 3: Tokens gone today / completed
        if (msg.Contains("gone today") || msg.Contains("completed") || msg.Contains("served") || msg.Contains("consulted") || msg.Contains("finished") || msg.Contains("tokens gone"))
        {
            var completedToday = await _db.Bookings
                .Include(b => b.Doctor)
                .Include(b => b.Department)
                .Where(b => b.HospitalId == hospitalId && b.Status == BookingStatus.Completed && (b.ActualEndTime >= todayStart || b.BookingTime >= todayStart))
                .ToListAsync();

            var records = await _db.ConsultationRecords
                .Where(cr => cr.HospitalId == hospitalId && cr.CreatedAt >= todayStart)
                .ToListAsync();

            var totalMins = records.Sum(r => r.DurationMinutes);
            var avgMins = records.Count > 0 ? Math.Round(records.Average(r => r.DurationMinutes), 1) : 0;

            var sb = new StringBuilder();
            sb.AppendLine($"### 🏁 Tokens Completed Today ({hospitalName})\n");
            sb.AppendLine($"* **Total Tokens Gone / Completed:** **{completedToday.Count}** patients served today.");
            if (records.Count > 0)
            {
                sb.AppendLine($"* **Total Clinical Consultation Time:** **{totalMins:F0}** minutes");
                sb.AppendLine($"* **Average Consultation Duration:** **~{avgMins}** minutes per patient\n");
            }

            if (completedToday.Count > 0)
            {
                var groupedByDoctor = completedToday.GroupBy(b => b.Doctor?.FullName ?? "General");
                sb.AppendLine("**Breakdown by Physician:**");
                foreach (var g in groupedByDoctor)
                {
                    sb.AppendLine($"- **Dr. {g.Key}:** {g.Count()} patient(s) completed");
                }
            }

            return new HospitalAiChatResponseDto
            {
                Reply = sb.ToString(),
                QueryCategory = "TOKENS_COMPLETED",
                StructuredData = new { CompletedCount = completedToday.Count, AvgDuration = avgMins },
                SuggestedFollowUps = new List<string>
                {
                    "How many tokens remaining per doctor?",
                    "How many has missed?",
                    "What are the things we did today?"
                }
            };
        }

        // Category 4: Activity Log / What did we do today
        if (msg.Contains("what") && (msg.Contains("did") || msg.Contains("do") || msg.Contains("action") || msg.Contains("activity") || msg.Contains("events") || msg.Contains("log") || msg.Contains("happened")))
        {
            var recentEvents = await _db.QueueEvents
                .Where(e => e.HospitalId == hospitalId && e.Timestamp >= todayStart)
                .OrderByDescending(e => e.Timestamp)
                .Take(8)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine($"### 📋 Hospital Activity & Actions Timeline Today\n");

            if (recentEvents.Count == 0)
            {
                sb.AppendLine("No queue operations have been logged yet today.");
            }
            else
            {
                sb.AppendLine("Here are the latest queue actions executed by your team today:");
                foreach (var ev in recentEvents)
                {
                    var timeStr = ev.Timestamp.ToLocalTime().ToString("hh:mm tt");
                    var icon = ev.EventType switch
                    {
                        "CREATED" => "🎫",
                        "CALLED" => "📢",
                        "BUILDING_CHECK_IN" => "🏢",
                        "DOCTOR_ROOM_CHECK_IN" => "🩺",
                        "STARTED" => "▶️",
                        "COMPLETED" => "✅",
                        "NO_SHOW" => "⚠️",
                        "PAUSED" => "⏸️",
                        _ => "📌"
                    };

                    sb.AppendLine($"- **{timeStr}** {icon} **{ev.EventType.Replace('_', ' ')}**");
                }
            }

            return new HospitalAiChatResponseDto
            {
                Reply = sb.ToString(),
                QueryCategory = "ACTIVITY_LOG",
                SuggestedFollowUps = new List<string>
                {
                    "How many tokens gone today?",
                    "How many tokens remaining per doctor?",
                    "How many has missed?"
                }
            };
        }

        // Category 5: Average consultation duration / AI predictions
        if (msg.Contains("average") || msg.Contains("duration") || msg.Contains("ai wait") || msg.Contains("waiting time") || msg.Contains("how long"))
        {
            var records = await _db.ConsultationRecords
                .Include(r => r.Doctor)
                .Include(r => r.Department)
                .Where(r => r.HospitalId == hospitalId)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine($"### ⏱️ Consultation Pacing & Duration Analysis ({hospitalName})\n");

            if (records.Count == 0)
            {
                sb.AppendLine("No recorded consultations yet. Estimates currently utilize baseline department schedules (10–15 mins).");
            }
            else
            {
                var overallAvg = Math.Round(records.Average(r => r.DurationMinutes), 1);
                var minDur = Math.Round(records.Min(r => r.DurationMinutes), 1);
                var maxDur = Math.Round(records.Max(r => r.DurationMinutes), 1);

                sb.AppendLine($"* **Overall Hospital Average:** **{overallAvg}** mins / consultation");
                sb.AppendLine($"* **Observed Range:** Fast: **{minDur}** mins | Extensive: **{maxDur}** mins");
                sb.AppendLine($"* **Dataset Size:** **{records.Count}** consultations feeding FastForest ML regression model.\n");

                var byDept = records.GroupBy(r => r.Department?.Name ?? "General");
                sb.AppendLine("**By Department:**");
                foreach (var d in byDept)
                {
                    sb.AppendLine($"- **{d.Key}:** ~{Math.Round(d.Average(r => r.DurationMinutes), 1)} mins avg ({d.Count()} cases)");
                }
            }

            return new HospitalAiChatResponseDto
            {
                Reply = sb.ToString(),
                QueryCategory = "AVERAGE_DURATION",
                SuggestedFollowUps = new List<string>
                {
                    "How many tokens gone today?",
                    "How many tokens remaining per doctor?",
                    "What are the things we did today?"
                }
            };
        }

        // Category 6: Doctors & Departments Overview
        if (msg.Contains("doctor") && (msg.Contains("who") || msg.Contains("list") || msg.Contains("available") || msg.Contains("staff")))
        {
            var doctors = await _db.Doctors
                .Include(d => d.DoctorDepartments)
                    .ThenInclude(dd => dd.Department)
                .Where(d => d.HospitalId == hospitalId)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine($"### 👨‍⚕️ Medical Staff & Availability ({hospitalName})\n");
            sb.AppendLine($"* **Total Registered Physicians:** **{doctors.Count}**\n");

            foreach (var doc in doctors)
            {
                var statusStr = doc.IsActive ? "🟢 Active" : "⚪ Inactive";
                var deptNames = doc.DoctorDepartments.Select(dd => dd.Department.Name);
                var deptStr = deptNames.Any() ? string.Join(", ", deptNames) : "General";
                sb.AppendLine($"- **Dr. {doc.FullName}** ({doc.Specialization}) — {deptStr} • {statusStr}");
            }

            return new HospitalAiChatResponseDto
            {
                Reply = sb.ToString(),
                QueryCategory = "DOCTORS_LIST",
                SuggestedFollowUps = new List<string>
                {
                    "How many tokens remaining per doctor?",
                    "How many tokens gone today?"
                }
            };
        }

        // Default: Full Executive Hospital Snapshot
        var totalBookedToday = await _db.Bookings
            .CountAsync(b => b.HospitalId == hospitalId && b.BookingTime >= todayStart);

        var completedTodayCount = await _db.Bookings
            .CountAsync(b => b.HospitalId == hospitalId && b.Status == BookingStatus.Completed && b.BookingTime >= todayStart);

        var waitingCountToday = await _db.Bookings
            .CountAsync(b => b.HospitalId == hospitalId && (b.Status == BookingStatus.Waiting || b.Status == BookingStatus.Called));

        var servingCountToday = await _db.Bookings
            .CountAsync(b => b.HospitalId == hospitalId && b.Status == BookingStatus.Serving);

        var missedCountToday = await _db.Bookings
            .CountAsync(b => b.HospitalId == hospitalId && b.Status == BookingStatus.NoShow && b.BookingTime >= todayStart);

        var activeDoctorsCount = await _db.QueueSessions
            .Where(s => s.HospitalId == hospitalId && s.Status == QueueSessionStatus.Open)
            .Select(s => s.DoctorId)
            .Distinct()
            .CountAsync();

        var defaultSb = new StringBuilder();
        defaultSb.AppendLine($"### 🏥 Hospital Queue Operations Overview ({hospitalName})\n");
        defaultSb.AppendLine($"Here is the live real-time status query for today:\n");
        defaultSb.AppendLine($"* 🎫 **Total Tokens Generated Today:** **{totalBookedToday}**");
        defaultSb.AppendLine($"* ✅ **Tokens Gone / Completed:** **{completedTodayCount}** patients finished");
        defaultSb.AppendLine($"* 🩺 **Currently With Doctor:** **{servingCountToday}** patient(s)");
        defaultSb.AppendLine($"* ⏳ **Tokens Remaining / Waiting:** **{waitingCountToday}** patient(s)");
        defaultSb.AppendLine($"* ❌ **Missed / No-Shows:** **{missedCountToday}** patient(s)");
        defaultSb.AppendLine($"* 👨‍⚕️ **Doctors Currently Consulting:** **{activeDoctorsCount}** active session(s)\n");
        defaultSb.AppendLine("You can ask me specific questions like: *\"How many tokens gone today?\"*, *\"How much token remaining per doctor?\"*, *\"How many has missed?\"*, or *\"What did we do today?\"*");

        return new HospitalAiChatResponseDto
        {
            Reply = defaultSb.ToString(),
            QueryCategory = "EXECUTIVE_OVERVIEW",
            StructuredData = new
            {
                TotalBooked = totalBookedToday,
                Completed = completedTodayCount,
                Serving = servingCountToday,
                Waiting = waitingCountToday,
                Missed = missedCountToday
            },
            SuggestedFollowUps = new List<string>
            {
                "How many tokens gone today?",
                "How much token remaining for per doctor?",
                "How many has missed?",
                "What did we do today?"
            }
        };
    }
}
