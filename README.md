# 🏥 Medicare – Doctor Appointment & Booking System

**Medicare** is a healthcare management system designed to simplify the process of **booking doctor appointments**, **managing patient data**, and **handling payments**.  
The system allows users to select doctors, choose appointment schedules, and complete payments through an organized and user-friendly workflow.

---

## 📌 Project Overview
The goal of this project is to provide a digital solution for healthcare services where patients can easily:
- Find available doctors
- Book appointments
- Select appointment dates and time slots
- Choose payment methods
- Store and manage medical-related data efficiently

---

## 🛠️ Technologies Used
- **C#**
- **ASP.NET / .NET Framework**
- MVC Architecture
- Entity Framework (for database handling)
- SQL Database

---

## ✨ Key Features
- User registration and authentication
- Doctor listing and availability management
- Appointment booking system
- Date and time slot selection
- Payment option selection
- Secure data storage for patients and appointments
- Admin-side management for doctors and schedules

---

## 🧑‍⚕️ User Workflow
1. User registers or logs in
2. Selects a doctor
3. Chooses available appointment date and time
4. Confirms appointment details
5. Selects a payment method
6. Appointment is successfully booked and stored in the system

---

## 🚀 Getting Started

1. Point `ConnectionStrings:DefaultConnection` in `appsettings.json` (or an environment
   variable) at a SQL Server instance. Migrations run automatically on startup.
2. Set the seed admin credentials via configuration rather than editing source:
   - Development: already set in `appsettings.Development.json` (`Root@12345` — dev-only,
     do not use elsewhere).
   - Any other environment: set `Seed:RootAdmin:Email` / `Seed:RootAdmin:Password` via
     environment variables (`MediCare__Seed__RootAdmin__Password`, etc.) or a secret store.
     The app refuses to start without these outside Development.
3. `dotnet run`.

## ⚠️ Known Limitations

- **Payments are a demo flow, not a real integration.** Card payments only validate that a
  card number is ≥12 digits and are marked "Paid" immediately — there's no payment gateway.
  Cash-at-clinic bookings are marked "Paid" manually by an admin. Don't point this at real
  card data.
- **The contact form is logged, not delivered.** `Contact/Send` validates and logs the
  submission server-side but there's no SMTP integration or a message table yet — messages
  aren't emailed or persisted anywhere durable.
- **Double-booking has an application-level guard, not a DB-level one yet.** Run
  `dotnet ef migrations add AddUniqueAppointmentSlotIndex` to add a unique index on
  `Appointments (DoctorId, DutyDate, StartTime, EndTime)` for full protection against a
  race condition between two near-simultaneous bookings.
- **Cancelling an appointment is a hard delete**, not a soft-cancel/audit trail. Any
  associated prescription/feedback rows cascade-delete with it.

---

