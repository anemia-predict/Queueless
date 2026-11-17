using Microsoft.EntityFrameworkCore;
using QueueLess.Api.Models;

namespace QueueLess.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Hospital> Hospitals => Set<Hospital>();
    public DbSet<HospitalMember> HospitalMembers => Set<HospitalMember>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<DoctorDepartment> DoctorDepartments => Set<DoctorDepartment>();
    public DbSet<DoctorSchedule> DoctorSchedules => Set<DoctorSchedule>();
    public DbSet<QueueSession> QueueSessions => Set<QueueSession>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<QueueEvent> QueueEvents => Set<QueueEvent>();
    public DbSet<ConsultationRecord> ConsultationRecords => Set<ConsultationRecord>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<MLModelVersion> MLModelVersions => Set<MLModelVersion>();
    public DbSet<MLPrediction> MLPredictions => Set<MLPrediction>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasIndex(u => u.Role);
        });

        // Hospital
        modelBuilder.Entity<Hospital>(entity =>
        {
            entity.HasIndex(h => h.RegistrationNumber).IsUnique();
            entity.HasIndex(h => h.City);
            entity.HasIndex(h => h.Status);
        });

        // HospitalMember
        modelBuilder.Entity<HospitalMember>(entity =>
        {
            entity.HasIndex(hm => new { hm.HospitalId, hm.UserId }).IsUnique();
            entity.HasOne(hm => hm.Hospital)
                .WithMany(h => h.Members)
                .HasForeignKey(hm => hm.HospitalId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(hm => hm.User)
                .WithMany(u => u.HospitalMemberships)
                .HasForeignKey(hm => hm.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Department
        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasIndex(d => new { d.HospitalId, d.Name });
            entity.HasOne(d => d.Hospital)
                .WithMany(h => h.Departments)
                .HasForeignKey(d => d.HospitalId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Doctor
        modelBuilder.Entity<Doctor>(entity =>
        {
            entity.HasIndex(d => d.HospitalId);
            entity.HasOne(d => d.Hospital)
                .WithMany(h => h.Doctors)
                .HasForeignKey(d => d.HospitalId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // DoctorDepartment
        modelBuilder.Entity<DoctorDepartment>(entity =>
        {
            entity.HasIndex(dd => new { dd.DoctorId, dd.DepartmentId }).IsUnique();
            entity.HasOne(dd => dd.Hospital)
                .WithMany()
                .HasForeignKey(dd => dd.HospitalId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(dd => dd.Doctor)
                .WithMany(d => d.DoctorDepartments)
                .HasForeignKey(dd => dd.DoctorId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(dd => dd.Department)
                .WithMany(d => d.DoctorDepartments)
                .HasForeignKey(dd => dd.DepartmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // DoctorSchedule
        modelBuilder.Entity<DoctorSchedule>(entity =>
        {
            entity.HasIndex(s => new { s.HospitalId, s.DoctorId, s.DayOfWeek });
            entity.HasOne(s => s.Hospital)
                .WithMany(h => h.Schedules)
                .HasForeignKey(s => s.HospitalId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(s => s.Doctor)
                .WithMany(d => d.Schedules)
                .HasForeignKey(s => s.DoctorId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(s => s.Department)
                .WithMany(d => d.Schedules)
                .HasForeignKey(s => s.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // QueueSession
        modelBuilder.Entity<QueueSession>(entity =>
        {
            entity.HasIndex(qs => new { qs.HospitalId, qs.SessionDate, qs.Status });
            entity.HasOne(qs => qs.Hospital)
                .WithMany(h => h.QueueSessions)
                .HasForeignKey(qs => qs.HospitalId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(qs => qs.Doctor)
                .WithMany(d => d.QueueSessions)
                .HasForeignKey(qs => qs.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(qs => qs.Department)
                .WithMany(d => d.QueueSessions)
                .HasForeignKey(qs => qs.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(qs => qs.Schedule)
                .WithMany(s => s.QueueSessions)
                .HasForeignKey(qs => qs.ScheduleId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Booking
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasIndex(b => new { b.QueueSessionId, b.TokenNumber }).IsUnique();
            entity.HasIndex(b => b.Status);
            entity.HasIndex(b => b.PatientUserId);
            entity.HasIndex(b => b.IdempotencyKey);
            entity.HasOne(b => b.Hospital)
                .WithMany(h => h.Bookings)
                .HasForeignKey(b => b.HospitalId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(b => b.QueueSession)
                .WithMany(qs => qs.Bookings)
                .HasForeignKey(b => b.QueueSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(b => b.Patient)
                .WithMany(u => u.Bookings)
                .HasForeignKey(b => b.PatientUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(b => b.Doctor)
                .WithMany(d => d.Bookings)
                .HasForeignKey(b => b.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(b => b.Department)
                .WithMany(d => d.Bookings)
                .HasForeignKey(b => b.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // QueueEvent
        modelBuilder.Entity<QueueEvent>(entity =>
        {
            entity.HasIndex(qe => new { qe.QueueSessionId, qe.Timestamp });
            entity.HasOne(qe => qe.Hospital)
                .WithMany()
                .HasForeignKey(qe => qe.HospitalId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(qe => qe.QueueSession)
                .WithMany(qs => qs.QueueEvents)
                .HasForeignKey(qe => qe.QueueSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(qe => qe.Booking)
                .WithMany(b => b.QueueEvents)
                .HasForeignKey(qe => qe.BookingId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(qe => qe.TriggeredByUser)
                .WithMany()
                .HasForeignKey(qe => qe.TriggeredByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ConsultationRecord
        modelBuilder.Entity<ConsultationRecord>(entity =>
        {
            entity.HasIndex(cr => cr.BookingId).IsUnique();
            entity.HasIndex(cr => new { cr.HospitalId, cr.StartTime });
            entity.HasIndex(cr => cr.DoctorId);
            entity.HasIndex(cr => cr.DepartmentId);

            entity.HasOne(cr => cr.Hospital)
                .WithMany(h => h.ConsultationRecords)
                .HasForeignKey(cr => cr.HospitalId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(cr => cr.Booking)
                .WithOne(b => b.ConsultationRecord)
                .HasForeignKey<ConsultationRecord>(cr => cr.BookingId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(cr => cr.Doctor)
                .WithMany(d => d.ConsultationRecords)
                .HasForeignKey(cr => cr.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(cr => cr.Department)
                .WithMany()
                .HasForeignKey(cr => cr.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(cr => cr.QueueSession)
                .WithMany()
                .HasForeignKey(cr => cr.QueueSessionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Notification
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasIndex(n => new { n.UserId, n.IsRead });
            entity.HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AuditLog
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(al => new { al.HospitalId, al.Timestamp });
            entity.HasOne(al => al.Hospital)
                .WithMany()
                .HasForeignKey(al => al.HospitalId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(al => al.User)
                .WithMany()
                .HasForeignKey(al => al.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // MLModelVersion
        modelBuilder.Entity<MLModelVersion>(entity =>
        {
            entity.HasIndex(m => new { m.ModelType, m.HospitalId, m.IsActive });
            entity.HasOne(m => m.Hospital)
                .WithMany(h => h.MLModelVersions)
                .HasForeignKey(m => m.HospitalId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // MLPrediction
        modelBuilder.Entity<MLPrediction>(entity =>
        {
            entity.HasIndex(p => p.BookingId);
            entity.HasIndex(p => new { p.HospitalId, p.GeneratedAt });
            entity.HasOne(p => p.Booking)
                .WithMany(b => b.MLPredictions)
                .HasForeignKey(p => p.BookingId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(p => p.Hospital)
                .WithMany()
                .HasForeignKey(p => p.HospitalId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(p => p.ModelVersion)
                .WithMany(mv => mv.Predictions)
                .HasForeignKey(p => p.ModelVersionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // IdempotencyRecord
        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.HasIndex(i => i.ExpiresAt);
            entity.HasOne(i => i.User)
                .WithMany()
                .HasForeignKey(i => i.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
