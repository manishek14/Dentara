# ADR-003: SQL Server مرجع قطعی ظرفیت است (Database-first capacity truth)

- **Status**: Accepted
- **Date**: 2026-10-14
- **Decision Owner**: Architecture Team

## Context

مهم‌ترین ریسک فنی Dentara **Overbooking** است: فروش یک ظرفیت به دو بیمار.

### سناریوی خطا

فرض کنیم یک یونیت دندان‌پزشکی در ساعت ۱۰ فقط یک ظرفیت دارد. دو بیمار تقریباً هم‌زمان درخواست رزرو می‌فرستند:

1. بیمار A → ظرفیت آزاد را می‌خواند (دیتابیس می‌گوید: آزاد)
2. بیمار B → ظرفیت آزاد را می‌خواند (دیتابیس می‌گوید: آزاد)
3. هر دو رزرو را ثبت می‌کنند → **یک ظرفیت به دو بیمار فروخته شده است!**

این خطا با الگوی "read-then-write" غیرقابل‌اجتناب است، حتی با cache یا index.

### تصمیم کسب‌وکار

SQL Server باید مرجع قطعی ظرفیت باشد. نه شمارندهٔ فرانت‌اند، نه تایمر مرورگر، نه Redis — هیچ‌کدام به‌تنهایی نمی‌توانند بگویند "این نوبت آزاد است". این تصمیم به‌خاطر حساسیت مالی و اعتماد بیمار ضروری است.

## Decision

عملیات "بررسی ظرفیت + تخصیص منابع + ایجاد رزرو موقت" در **یک تراکنش SQL Server اتمیک** انجام می‌شود.

### چهار اصل طراحی

1. **قفل‌گذاری روی ردیف‌های پایدار** — قفل روی ردیف `Dentist` و `ClinicResource` گرفته می‌شود، نه روی ردیف رزرو. ردیف رزرو ممکن است هنوز وجود نداشته باشد.
2. **بررسی هم‌پوشانی پس از گرفتن قفل** — داخل تراکنش، کوئری overlap روی `Appointments` و `ResourceAllocations` اجرا می‌شود.
3. **درج رزرو + همهٔ تخصیص‌ها در همان تراکنش** — اگر حتی یک منبع در دسترس نباشد، کل تراکنش rollback می‌شود.
4. **Commit تنها با موفقیت همهٔ بررسی‌ها** — در غیر این صورت rollback کامل.

### راهبرد قفل‌گذاری

```sql
-- 1. قفل روی ردیف دندان‌پزشک
SELECT * FROM Dentists WITH (UPDLOCK, HOLDLOCK) WHERE Id = @dentistId;

-- 2. قفل روی ردیف‌های منابع (به ترتیب ثابت برای جلوگیری از deadlock)
SELECT * FROM ClinicResources WITH (UPDLOCK, HOLDLOCK)
WHERE Id IN (@resourceId1, @resourceId2, ...)
ORDER BY Id;

-- 3. بررسی overlap روی رزروهای فعال پزشک
SELECT COUNT(*) FROM Appointments WITH (HOLDLOCK)
WHERE DentistId = @dentistId
  AND Status IN ('PendingPayment','Confirmed')
  AND StartAtUtc < @endAt
  AND EndAtUtc > @startAt;

-- 4. بررسی overlap روی تخصیص‌های فعال هر منبع
-- (به ترتیب منابع)
-- ...

-- 5. اگر همه چیز آزاد بود: درج Appointment + ResourceAllocations
-- 6. Commit
```

### چرا `UPDLOCK + HOLDLOCK`؟

- **UPDLOCK**: قفل update می‌گیرد (نه shared lock). دو تراکنش نمی‌توانند هم‌زمان UPDLOCK روی یک ردیف بگیرند.
- **HOLDLOCK**: قفل را تا پایان تراکنش نگه می‌دارد (معادل SERIALIZABLE روی محدودهٔ قفل).
- **ترکیب**: اطمینان می‌دهد که بین خواندن وضعیت و درج رزرو، هیچ تراکنش دیگری نمی‌تواند آن ردیف را تغییر دهد.

### چرا `SERIALIZABLE` به‌تنهایی کافی نیست؟

`SERIALIZABLE` روی کل range قفل می‌گیرد و در عمل deadlock بین تراکنش‌های مختلف را افزایش می‌دهد. `UPDLOCK + HOLDLOCK` روی ردیف‌های مشخص (پزشک و منابع) قفل می‌گیرد و کارایی بهتری دارد.

## Alternatives Considered

### Optimistic Concurrency (Row Version)
- **روش**: در زمان update، `WHERE RowVersion = @expectedVersion`. اگر تغییر کرده بود، retry.
- **دلیل رد**: برای رزرو جدید (INSERT) کار نمی‌کند چون ردیفی برای مقایسه وجود ندارد. به‌علاوه، retry باعث UX بدی می‌شود (بیمار "رزرو شد" می‌بیند اما بعد "ظرفیت پر" دریافت می‌کند).

### Redis Distributed Lock (Redlock)
- **روش**: قبل از رزرو، قفل در Redis بگیر، رزرو کن، قفل را آزاد کن.
- **دلیل رد**: 
  - Redis volatile است؛ انقضای قفل می‌تواند باعث Overbooking شود.
  - اگر حافظهٔ Redis از بین برود (network partition، restart)، قفل‌ها از بین می‌روند.
  - TTL باید با مدت رزرو هماهنگ باشد؛ این محاسبه شکننده است.
  - در صورت Redis-down، کل سیستم رزرو از کار می‌افتد.
- **نتیجه**: Redis می‌تواند cache باشد اما هرگز منبع حقیقت رزرو.

### SQL Server Serializable Isolation برای کل تراکنش
- **دلیل رد**: افزایش شدید deadlock. قفل روی range کامل گرفته می‌شود نه روی ردیف مشخص. کارایی پایین‌تر از ترکیب `UPDLOCK + HOLDLOCK` روی ردیف‌های مشخص.

### Application-Level Mutex / In-process Lock
- **دلیل رد**: در صورت scale-out به چند instance (که در آینده محتمل است)، بین instanceها هماهنگی وجود ندارد. حتی در نسخهٔ اول، اگر Api و Background Worker هم‌زمان رزرو را دستکاری کنند، باگ ظاهر می‌شود.

## Consequences

### مثبت

- **تضمین صحت**: حتی با ۱۰۰ درخواست هم‌زمان برای ۱۰ ظرفیت، حداکثر ۱۰ رزرو موفق ثبت می‌شود.
- **بدون وابستگی به Redis**: یک وابستگی عملیاتی کمتر.
- **تست‌پذیر**: تست رقابتی روی SQL Server واقعی (با Testcontainers) می‌تواند این رفتار را تأیید کند.
- **ممیزی کامل**: همهٔ تغییرات در یک تراکنش atomic، یا هیچ‌کدام.

### منفی

- **کارایی تحت بار رقابتی**: تراکنش‌ها سریال می‌شوند؛ latency بالا می‌رود. برای کلینیک دندان‌پزشکی (صدها رزرو در روز) قابل‌قبول است؛ برای فروش بلیط کنسرت کافی نیست.
- **احتمال Deadlock**: با رعایت ترتیب ثابت قفل‌ها (مرتب‌سازی منابع به ترتیب Guid) قابل کنترل. نیاز به retry policy با backoff.
- **نزدیکی به دیتابیس**: لایهٔ Application باید به isolation level و lock hintها آگاه باشد. این پیچیدگی به Application با الگوی UnitOfWork و یک `ReservationTransactionService` کپسوله می‌شود.

### محدودیت‌های اعمال‌شده

- **هیچ تماس شبکه‌ای در تراکنش**: call به درگاه پرداخت، SMS یا سرویس خارجی نباید داخل تراکنش دیتابیس باشد. درگاه ممکن است کند باشد و قفل‌ها را طولانی کند.
- **Background Worker باید همین پروتکل را رعایت کند**: هنگام انقضای رزرو موقت، آزادسازی ظرفیت در تراکنش اتمیک انجام می‌شود.
- **EF Core InMemory برای تست کافی نیست**: تست هم‌زمانی باید روی SQL Server واقعی یا LocalDB انجام شود.

## Test Strategy

1. **Unit test**: قواعد هم‌پوشانی بازه‌ها، ماشین حالت رزرو، محاسبه قیمت.
2. **Integration test**: رزرو واقعی روی SQL Server در Testcontainer، با تأیید قیود دیتابیس.
3. **Concurrency test** (Acceptance criteria):
   - ۱۰۰ task هم‌زمان → رزرو همان بازه
   - فقط ۱۰ باید موفق شوند (`PendingPayment` یا `Confirmed`)
   - بقیه باید خطای "ظرفیت پر" دریافت کنند
   - هیچ دو رزرو فعال هم‌پوشانی روی یک منبع نباید وجود داشته باشد
4. **Deadlock test**: چند workflow هم‌زمان با ترتیب منابع متفاوت → نباید deadlock دائمی ایجاد شود.

## References

- [SQL Server Locking and Blocking](https://learn.microsoft.com/sql/relational-databases/sql-server-transaction-locking-and-row-versioning-guide)
- [EF Core Transactions](https://learn.microsoft.com/ef/core/saving/transactions)
- ADR-001 (SQL Server + EF Core)
- ADR-004 (Separate booking and payment states)
