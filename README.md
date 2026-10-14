# Dentara

> سامانه رزرو آنلاین دندان‌پزشکی — نسخه ۰٫۱

Dentara یک پلتفرم رزرو نوبت آنلاین برای کلینیک‌های دندان‌پزشکی است که با تمرکز بر **صحت تراکنشی رزرو در شرایط هم‌زمانی** طراحی شده است. این سند خلاصهٔ معماری است؛ برای جزئیات کامل به اسناد ADR در `docs/adr/` مراجعه کنید.

---

## تکنولوژی‌ها

| لایه | تکنولوژی |
|------|----------|
| Backend | ASP.NET Core Web API (C# 13 / .NET 10) |
| ORM | Entity Framework Core 10 |
| Database | Microsoft SQL Server |
| Architecture | Modular Monolith |
| Testing | xUnit + FluentAssertions + Testcontainers |

---

## ساختار راهکار

```
Dentara.sln
├── src/
│   ├── Dentara.Api/              # endpointها، احراز هویت، نگاشت درخواست/پاسخ
│   ├── Dentara.Application/      # جریان‌های رزرو، لغو، پرداخت، انتشار
│   ├── Dentara.Domain/           # مفاهیم اصلی، وضعیت‌ها، قواعد کسب‌وکار
│   └── Dentara.Infrastructure/   # EF Core، SQL Server، درگاه پرداخت، Background Worker
└── tests/
    ├── Dentara.UnitTests/        # تست واحد قواعد دامنه
    └── Dentara.IntegrationTests/ # تست یکپارچگی روی SQL Server واقعی
```

### ماژول‌های داخلی (در لایه‌های Domain/Application/Infrastructure)

1. **Identity** — Users, Roles, PatientProfiles, DentistProfiles
2. **Catalog** — Services, ClinicResources, ResourceServiceRequirements
3. **Scheduling** — WorkSchedules, ScheduleExceptions, BookingWindows, PublishedSessions
4. **Booking** — Appointments, ResourceAllocations, AppointmentEvents
5. **Payments** — PaymentAttempts, Refunds, PaymentWebhookEvents, IdempotencyRecords

---

## اصول معماری

بر اساس ADRهای مصوب:

- **ADR-001** — SQL Server + EF Core به‌عنوان مرجع داده
- **ADR-002** — Modular Monolith (بدون Microservices در نسخه اول)
- **ADR-003** — **SQL Server مرجع قطعی ظرفیت است**؛ نه فرانت‌اند، نه Redis، نه تایمر مرورگر
- **ADR-004** — وضعیت رزرو و وضعیت پرداخت دو چرخه مستقل‌اند
- **ADR-005** — UTC برای ذخیره‌سازی، منطقهٔ زمانی کلینیک در تنظیمات، تقویم شمسی صرفاً نمایش
- **ADR-006** — همهٔ عملیات خارجی (callback، لغو، انقضا، بازپرداخت) idempotent
- **ADR-007** — مبلغ، مدت و سیاست لغو هنگام رزرو به‌صورت snapshot ذخیره می‌شوند

---

## راه‌اندازی

### پیش‌نیازها

- .NET 10 SDK
- SQL Server 2019+ (یا SQL Server در Docker)
- (اختیاری) Docker برای Testcontainers

### اجرا

```bash
dotnet restore
dotnet build
cd src/Dentara.Api
dotnet run
```

API روی `https://localhost:5001` و `http://localhost:5000` در دسترس خواهد بود.

### اجرای تست‌ها

```bash
dotnet test
```

---

## جلوگیری از Overbooking

هستهٔ طراحی این سامانه **تراکنش اتمیک + قفل‌گذاری پایدار در SQL Server** است. سه اصل:

1. قفل روی ردیف‌های پایدار پزشک و منابع (نه روی ردیف رزرو)
2. بررسی هم‌پوشانی پس از گرفتن قفل
3. درج رزرو + همهٔ تخصیص‌ها در همان تراکنش

به این ترتیب، حتی با ۱۰۰ درخواست هم‌زمان برای ۱۰ ظرفیت، هرگز بیش از ۱۰ رزرو موفق ثبت نمی‌شود. برای جزئیات به `docs/adr/ADR-003-database-first-capacity-truth.md` مراجعه کنید.

---

## وضعیت پروژه

🚧 در حال توسعه — فاز پایه (Domain + مستندات)

---

## لایسنس

Proprietary — کلیهٔ حقوق محفوظ است.
