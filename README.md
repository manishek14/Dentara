# Dentara

> سامانه رزرو آنلاین دندان‌پزشکی — نسخه ۰٫۱

Dentara یک پلتفرم رزرو نوبت آنلاین برای کلینیک‌های دندان‌پزشکی است که با تمرکز بر **صحت تراکنشی رزرو در شرایط هم‌زمانی** طراحی شده است.

---

## ✅ وضعیت پروژه

**لایه‌های کامل:** Domain · Application · Infrastructure · API · Background Workers · Integration Tests

**۲۵ کامیت، ۳۲۲ تست موفق** (۳۱۰ unit + ۱۲ integration) + ۸ تست skipped با دلیل مستند.

🔗 ریپو: https://github.com/manishek14/Dentara

---

## تکنولوژی‌ها

| لایه | تکنولوژی |
|------|----------|
| Backend | ASP.NET Core Web API (C# 13 / .NET 10) |
| ORM | Entity Framework Core 10 |
| Database | Microsoft SQL Server (تولید) · SQLite (تست) |
| Architecture | Modular Monolith (۵ ماژول) |
| CQRS | MediatR با Pipeline Behaviors |
| Auth | JWT Bearer + Refresh Token |
| Testing | xUnit + FluentAssertions + Moq + SQLite in-memory |

---

## ساختار راهکار

```
Dentara.sln
├── src/
│   ├── Dentara.Api/              # endpointها، احراز هویت، middleware
│   ├── Dentara.Application/      # جریان‌های رزرو، لغو، پرداخت، انتشار
│   ├── Dentara.Domain/           # مفاهیم اصلی، وضعیت‌ها، قواعد کسب‌وکار
│   └── Dentara.Infrastructure/   # EF Core، SQL Server، workers، gateways
└── tests/
    ├── Dentara.UnitTests/        # تست واحد قواعد دامنه (۳۱۰ تست)
    └── Dentara.IntegrationTests/ # تست یکپارچگی روی SQLite (۱۲ + ۸ skipped)
```

### ماژول‌های داخلی (در لایه‌های Domain/Application/Infrastructure)

1. **Identity** — Users, Roles, PatientProfiles, DentistProfiles, OTP
2. **Catalog** — Services, ClinicResources, ResourceServiceRequirements
3. **Scheduling** — WorkSchedules, ScheduleExceptions, BookingWindows, PublishedSessions
4. **Booking** — Appointments, ResourceAllocations, AppointmentEvents
5. **Payments** — PaymentAttempts, Refunds, PaymentWebhookEvents, IdempotencyRecords

---

## اصول معماری (ADRs)

- **ADR-001** — SQL Server + EF Core به‌عنوان مرجع داده
- **ADR-002** — Modular Monolith (بدون Microservices در نسخه اول)
- **ADR-003** — **SQL Server مرجع قطعی ظرفیت است**؛ نه فرانت‌اند، نه Redis، نه تایمر مرورگر
- **ADR-004** — وضعیت رزرو و وضعیت پرداخت دو چرخه مستقل‌اند
- **ADR-005** — UTC برای ذخیره‌سازی، منطقهٔ زمانی کلینیک در تنظیمات، تقویم شمسی صرفاً نمایش
- **ADR-006** — همهٔ عملیات خارجی (callback، لغو، انقضا، بازپرداخت) idempotent
- **ADR-007** — مبلغ، مدت و سیاست لغو هنگام رزرو به‌صورت snapshot ذخیره می‌شوند

---

## 🎯 نکات کلیدی پیاده‌سازی

### جلوگیری از Overbooking (ADR-003)

هستهٔ طراحی: تراکنش اتمیک + قفل‌گذاری پایدار در SQL Server. پیاده‌سازی در `SqlBookingTransactionService`:

1. قفل روی ردیف دندان‌پزشک با `UPDLOCK + HOLDLOCK`
2. قفل روی ردیف‌های منابع با ترتیب ثابت (ORDER BY Id) برای جلوگیری از deadlock
3. بررسی overlap روی Appointments و ResourceAllocations
4. INSERT Appointment + ResourceAllocations + Events
5. COMMIT یا Rollback کامل

**تأیید شده با تست رقابتی:** ۱۰۰ درخواست هم‌زمان → دقیقاً ۱۰ موفق، ۹۰ ناموفق با `SlotUnavailableException`.

### Background Workers

| Worker | فاصله | مسئولیت |
|--------|-------|---------|
| `AppointmentExpiryWorker` | 1 دقیقه | پردازش رزروهای منقضی‌شده با تأیید پرداخت (ADR-004) |
| `BookingWindowPublicationWorker` | 30 ثانیه | انتشار PublishedSessions برای BookingWindows باز شده |
| `IdempotencyCleanupWorker` | روزانه | پاکسازی IdempotencyRecords قدیمی (۹۰ روز) |

### Idempotency (ADR-006)

- `IdempotencyRecords` با `UNIQUE(Scope, Key)`
- Pipeline Behavior برای خودکار کردن idempotency روی commandهای حساس
- Webhook deduplication با `UNIQUE(Provider, ProviderEventId)`

### Snapshot Commercial Terms (ADR-007)

هنگام رزرو، تمام شرایط تجاری snapshot می‌شوند:
- `ServiceNameSnapshot`, `PriceAmountSnapshot`, `CurrencySnapshot`
- `CancellationPolicyVersionSnapshot`, `RefundablePercentSnapshot`
- تغییرات بعدی در Services یا Policies روی رزروهای قدیمی اثر نمی‌گذارد

---

## API Endpoints

### Health
- `GET /health` — no auth, no DB

### Identity (`/api/identity`)
- `POST /otp/request` — درخواست OTP (rate-limited)
- `POST /otp/verify` — تأیید OTP، دریافت JWT

### Appointments (`/api/appointments`) [Authorize]
- `POST /` — رزرو نوبت (idempotent با X-Idempotency-Key)
- `POST /{id}/cancel` — لغو نوبت

### Payments (`/api/payments`)
- `POST /initiate` [Authorize] — شروع پرداخت
- `POST /webhooks` [AllowAnonymous] — دریافت callback از درگاه
- `POST /refunds` [Authorize(Roles=Admin,Receptionist)] — درخواست بازپرداخت

### Scheduling (`/api/scheduling`) [Authorize(Roles=Admin)]
- `POST /booking-windows/{id}/publish` — انتشار دستی نوبت‌ها

---

## راه‌اندازی

### پیش‌نیازها

- .NET 10 SDK
- SQL Server 2019+ (یا SQL Server در Docker برای تولید)

### اجرا

```bash
dotnet restore
dotnet build
cd src/Dentara.Api
dotnet run
```

API روی `https://localhost:5001` و `http://localhost:5000` در دسترس خواهد بود.
Swagger UI در `/swagger` در دسترس است (Development).

### اجرای تست‌ها

```bash
dotnet test
```

Integration tests از SQLite in-memory استفاده می‌کنند (بدون نیاز به Docker).

---

## 📋 موارد شناخته‌شده (Technical Debt)

1. **CancellationService و AppointmentExpiryWorker integration tests** — ۸ تست به‌خاطر conflict بین EF Core 10 + SQLite + IsRowVersion skip شده‌اند. این تست‌ها وقتی CI به SQL Server Testcontainers migrate شود، re-enable خواهند شد. منطق CQRS و state machine در unit tests پوشش داده شده است.

2. **Clinic timezone** — در حال حاضر UTC+3:30 (Asia/Tehran) hardcoded است. باید از `ClinicSettings` خوانده شود.

3. **Zarinpal gateway adapter** — فقط Sandbox پیاده‌سازی شده. Zarinpal adapter واقعی نیاز به API keys دارد.

4. **Refresh token revocation** — `JwtTokenService.RefreshAsync` و `RevokeAsync` stub هستند. production نیاز به RefreshTokens table دارد.

---

## لایسنس

Proprietary — کلیهٔ حقوق محفوظ است.
