# Dentara

> سامانه رزرو آنلاین دندان‌پزشکی — نسخه ۰٫۲

Dentara یک پلتفرم رزرو نوبت آنلاین برای کلینیک‌های دندان‌پزشکی است که با تمرکز بر **صحت تراکنشی رزرو در شرایط هم‌زمانی** طراحی شده است. SQL Server مرجع قطعی ظرفیت است — نه فرانت‌اند، نه Redis، نه تایمر مرورگر.

---

## ✅ وضعیت پروژه

| متریک | مقدار |
|-------|-------|
| کامیت‌ها | ۳۶ |
| فایل‌های C# | ۲۶۹ |
| Unit tests | ۳۱۰ ✅ |
| Integration tests | ۲۰ ✅ |
| Skipped tests | ۰ |
| API Controllers | ۹ |
| API Endpoints | ۳۵+ |
| Background Workers | ۶ |
| ADRs | ۷ |
| ماژول‌های Domain | ۷ (Identity, Catalog, Scheduling, Booking, Payments, Notifications, Waitlist, Audit) |

🔗 ریپو: https://github.com/manishek14/Dentara

---

## تکنولوژی‌ها

| لایه | تکنولوژی |
|------|----------|
| Backend | ASP.NET Core Web API (C# 13 / .NET 10) |
| ORM | Entity Framework Core 10 |
| Database | Microsoft SQL Server (تولید) · SQLite (تست) |
| Architecture | Modular Monolith |
| CQRS | MediatR با Pipeline Behaviors (Logging, Validation, Idempotency) |
| Auth | JWT Bearer + Refresh Token |
| Background Jobs | IHostedService (WorkerBase) |
| Testing | xUnit + FluentAssertions + Moq + SQLite in-memory |

---

## ساختار راهکار

```
Dentara.sln
├── src/
│   ├── Dentara.Api/              # ۹ Controller، middleware، JWT auth، RBAC
│   ├── Dentara.Application/      # CQRS commands/queries/handlers، pipeline behaviors
│   ├── Dentara.Domain/           # ۷ ماژول، entities، state machines، domain events
│   └── Dentara.Infrastructure/   # EF Core، SQL Server، ۶ workers، gateways
└── tests/
    ├── Dentara.UnitTests/        # ۳۱۰ تست واحد
    └── Dentara.IntegrationTests/ # ۲۰ تست یکپارچگی (SQLite)
```

### ماژول‌های داخلی

| ماژول | Entities | مسئولیت |
|-------|----------|---------|
| **Identity** | User, Role, UserRole, PatientProfile, DentistProfile | احراز هویت OTP، JWT، مدیریت کاربران |
| **Catalog** | Service, DentistService, ClinicResource, ResourceServiceRequirement | خدمات، منابع، قیمت‌گذاری |
| **Scheduling** | WorkSchedule, ScheduleException, BookingWindow, PublishedSession | برنامه کاری، انتشار نوبت |
| **Booking** | Appointment, ResourceAllocation, AppointmentEvent | رزرو، تخصیص منابع، audit trail |
| **Payments** | PaymentAttempt, Refund, PaymentWebhookEvent, IdempotencyRecord | پرداخت، بازپرداخت، webhook |
| **Notifications** | Notification | یادآوری SMS، اطلاع‌رسانی |
| **Waitlist** | WaitlistEntry | لیست انتظار، پیشنهاد خودکار |
| **Audit** | AuditLog | تاریخچه تغییرات (INSERT-only) |

---

## اصول معماری (ADRs)

| ADR | عنوان | وضعیت |
|-----|-------|-------|
| [ADR-001](docs/adr/ADR-001-sql-server-and-ef-core.md) | SQL Server + EF Core | Accepted |
| [ADR-002](docs/adr/ADR-002-modular-monolith.md) | Modular Monolith | Accepted |
| [ADR-003](docs/adr/ADR-003-database-first-capacity-truth.md) | **SQL Server مرجع قطعی ظرفیت** | Accepted |
| [ADR-004](docs/adr/ADR-004-separate-booking-and-payment-states.md) | جدایی چرخه رزرو و پرداخت | Accepted |
| [ADR-005](docs/adr/ADR-005-utc-storage-and-persian-display.md) | UTC + تقویم شمسی | Accepted |
| [ADR-006](docs/adr/ADR-006-idempotent-external-operations.md) | Idempotency عملیات خارجی | Accepted |
| [ADR-007](docs/adr/ADR-007-snapshot-commercial-terms.md) | Snapshot شرایط تجاری | Accepted |

---

## 🎯 قابلیت‌های کلیدی

### جلوگیری از Overbooking (ADR-003)
تراکنش اتمیک + `UPDLOCK + HOLDLOCK` در SQL Server. **تأیید شده با تست رقابتی**: ۱۰۰ درخواست هم‌زمان → دقیقاً ۱۰ موفق، ۹۰ ناموفق.

### یادآوری خودکار نوبت
`ReminderSchedulingWorker` (۵ دقیقه) + `NotificationDispatcherWorker` (۳۰ ثانیه) — یادآوری SMS ۲۴ ساعت قبل از نوبت.

### لیست انتظار
وقتی نوبتی لغو می‌شود، `WaitlistProcessor` به‌طور خودکار اولین نفر در لیست (FIFO) پیشنهاد می‌دهد با مهلت ۳۰ دقیقه‌ای.

### RBAC Backend-enforced
۲۷ permission، ۴ role (Patient, Dentist, Receptionist, Admin). `[HasPermission]` attribute در همه endpoint‌ها.

### Audit History
`AuditLog` INSERT-only. تمام تغییرات حساس (لغو، تغییر قیمت، تخصیص نقش) با OldValue/NewValue JSON ثبت می‌شوند.

### داشبورد مدیریتی
آمار کامل: تعداد نوبت‌ها، نرخ عدم مراجعه، درآمد،利用率 دندان‌پزشک‌ها، خدمات پرتقاضا.

---

## 📡 API Reference

### Health
| Method | Path | Auth | Permission |
|--------|------|------|------------|
| GET | `/health` | — | — |

### Identity
| Method | Path | Auth | Permission |
|--------|------|------|------------|
| POST | `/api/identity/otp/request` | — | — (rate-limited ۶۰s) |
| POST | `/api/identity/otp/verify` | — | — |

### Appointments
| Method | Path | Auth | Permission |
|--------|------|------|------------|
| POST | `/api/appointments` | ✅ | `appointment.create` |
| POST | `/api/appointments/{id}/cancel` | ✅ | `appointment.cancel` |

### Payments
| Method | Path | Auth | Permission |
|--------|------|------|------------|
| POST | `/api/payments/initiate` | ✅ | `payment.initiate` |
| POST | `/api/payments/webhooks` | — | — (signature verified) |
| POST | `/api/payments/refunds` | ✅ | `refund.request` |

### Scheduling
| Method | Path | Auth | Permission |
|--------|------|------|------------|
| POST | `/api/scheduling/booking-windows/{id}/publish` | ✅ | `booking_window.publish` |

### Services
| Method | Path | Auth | Permission |
|--------|------|------|------------|
| GET | `/api/services` | ✅ | — |
| GET | `/api/services/{id}` | ✅ | — |
| POST | `/api/services` | ✅ | `service.manage` |
| PUT | `/api/services/{id}` | ✅ | `service.manage` |
| PUT | `/api/services/{id}/pricing` | ✅ | `pricing.manage` |
| POST | `/api/services/{id}/deactivate` | ✅ | `service.manage` |
| POST | `/api/services/link-dentist` | ✅ | `service.manage` |
| POST | `/api/services/unlink-dentist` | ✅ | `service.manage` |

### Resources
| Method | Path | Auth | Permission |
|--------|------|------|------------|
| GET | `/api/resources` | ✅ | — |
| GET | `/api/resources/{id}` | ✅ | — |
| POST | `/api/resources` | ✅ | `resource.manage` |
| PUT | `/api/resources/{id}` | ✅ | `resource.manage` |
| POST | `/api/resources/{id}/deactivate` | ✅ | `resource.manage` |

### Users
| Method | Path | Auth | Permission |
|--------|------|------|------------|
| GET | `/api/users` | ✅ | ownership / `user.manage` |
| GET | `/api/users/{id}` | ✅ | ownership / `user.manage` |
| POST | `/api/users/{id}/deactivate` | ✅ | `user.manage` |
| POST | `/api/users/{id}/activate` | ✅ | `user.manage` |
| POST | `/api/users/{id}/roles` | ✅ | `role.assign` |
| DELETE | `/api/users/{id}/roles/{roleName}` | ✅ | `role.assign` |
| PUT | `/api/users/{id}/patient-profile` | ✅ | ownership / `user.manage` |
| PUT | `/api/users/{id}/dentist-profile` | ✅ | `user.manage` |

### Waitlist
| Method | Path | Auth | Permission |
|--------|------|------|------------|
| POST | `/api/waitlist` | ✅ | `waitlist.join` |
| POST | `/api/waitlist/{id}/cancel` | ✅ | `waitlist.join` |
| GET | `/api/waitlist` | ✅ | ownership / `waitlist.view_all` |
| GET | `/api/waitlist/{id}` | ✅ | ownership / `waitlist.view_all` |

### Dashboard
| Method | Path | Auth | Permission |
|--------|------|------|------------|
| GET | `/api/dashboard/stats` | ✅ | `dashboard.view` |

### Audit Log
| Method | Path | Auth | Permission |
|--------|------|------|------------|
| GET | `/api/audit/logs` | ✅ | `audit_log.view` |
| GET | `/api/audit/entities/{type}/{id}` | ✅ | `audit_log.view` |

---

## 🔐 RBAC — نقش‌ها و مجوزها

| نقش | مجوزهای کلیدی |
|-----|---------------|
| **Patient** | `appointment.create`, `appointment.cancel`, `payment.initiate`, `waitlist.join`, `otp.request` |
| **Dentist** | `appointment.view_all`, `appointment.complete`, `appointment.mark_noshow`, `dashboard.view` |
| **Receptionist** | تمام مجوزهای Patient + `appointment.cancel_any`, `refund.request`, `schedule.manage`, `booking_window.publish`, `waitlist.view_all`, `waitlist.manage`, `dashboard.view` |
| **Admin** | تمام ۲۷ مجوز (شامل `user.manage`, `role.assign`, `service.manage`, `pricing.manage`, `audit_log.view`) |

---

## ⚙️ Background Workers

| Worker | فاصله | مسئولیت | ADR |
|--------|-------|---------|-----|
| `AppointmentExpiryWorker` | ۱ دقیقه | پردازش رزروهای منقضی + تأیید پرداخت با درگاه | ADR-004 |
| `BookingWindowPublicationWorker` | ۳۰ ثانیه | انتشار PublishedSessions | ADR-006 |
| `IdempotencyCleanupWorker` | روزانه | پاکسازی رکوردهای قدیمی (۹۰ روز) | ADR-006 |
| `ReminderSchedulingWorker` | ۵ دقیقه | ساخت یادآور ۲۴ ساعت قبل | — |
| `NotificationDispatcherWorker` | ۳۰ ثانیه | ارسال SMS (با retry ۳ بار) | — |
| `WaitlistOfferExpiryWorker` | ۱ دقیقه | منقضی‌کردن پیشنهادهای لیست انتظار | — |

---

## 🚀 راه‌اندازی

### پیش‌نیازها
- .NET 10 SDK
- SQL Server 2019+ (یا Docker)

### اجرا
```bash
dotnet restore
dotnet build
cd src/Dentara.Api
dotnet run
```
API روی `https://localhost:5001` در دسترس است. Swagger UI در `/swagger`.

### اجرای تست‌ها
```bash
dotnet test
```
Integration tests از SQLite in-memory استفاده می‌کنند (بدون Docker).

### اجرا با Docker Compose (توصیه‌شده برای توسعه محلی)

```bash
# ساخت و اجرای SQL Server + API
docker compose up -d

# مشاهده لاگ‌های API
docker compose logs -f api

# توقف و حذف container‌ها
docker compose down

# توقف + حذف volume دیتابیس (ریست کامل)
docker compose down -v
```

| سرویس | آدرس | توضیح |
|-------|------|-------|
| API (HTTP) | `http://localhost:8080` | Swagger UI در `/swagger` |
| SQL Server | `localhost:1433` | User: `sa`, Password: `YourStrong!Passw0rd` |

پس از `docker compose up`، API به‌طور خودکار به SQL Server متصل می‌شود. Migration باید به‌صورت دستی اجرا شود:
```bash
docker compose exec api dotnet Dentara.Api.dll --migrate
# یا از خارج container:
dotnet ef database update --project src/Dentara.Infrastructure --startup-project src/Dentara.Api
```

---

## 📊 مدل داده (ERD خلاصه)

```
Users ──┬── PatientProfiles
        ├── DentistProfiles
        └── UserRoles ── Roles

Services ──┬── DentistServices ── DentistProfiles
           └── ResourceServiceRequirements

ClinicResources ── ResourceAllocations ── Appointments
                                           ├── AppointmentEvents
                                           └── PaymentAttempts ── Refunds

WorkSchedules ── ScheduleExceptions
BookingWindows ── PublishedSessions

Notifications        WaitlistEntries        AuditLogs
IdempotencyRecords   PaymentWebhookEvents
```

---

## 📋 موارد شناخته‌شده (Technical Debt)

1. **Clinic timezone** — UTC+3:30 (Asia/Tehran) hardcoded. باید از `ClinicSettings` خوانده شود.
2. **Zarinpal gateway** — فقط Sandbox پیاده‌سازی شده. Zarinpal adapter واقعی نیاز به API keys.
3. **Refresh token revocation** — `JwtTokenService.RefreshAsync/RevokeAsync` stub هستند.
4. **SQL Server Testcontainers** — integration tests از SQLite استفاده می‌کنند. SQL Server Testcontainers در CI پیشنهاد می‌شود برای تست کامل `rowversion` و `UPDLOCK`.

---

## لایسنس

Proprietary — کلیه حقوق محفوظ است.
