# ADR-001: SQL Server + EF Core به‌عنوان مرجع داده

- **Status**: Accepted
- **Date**: 2026-10-14
- **Decision Owner**: Architecture Team

## Context

نسخهٔ اول Dentara برای یک کلینیک دندان‌پزشکی طراحی می‌شود. محدودهٔ نسخهٔ اول شامل ثبت‌نام بیمار، مدیریت برنامهٔ دندان‌پزشک‌ها، انتشار نوبت، رزرو موقت با مهلت ۱۰ دقیقه‌ای، پرداخت آنلاین و لغو با سیاست بازپرداخت ۸۵٪ است.

نقاط حساس کسب‌وکار:

1. **هم‌زمانی رزرو**: دو بیمار ممکن است هم‌زمان یک ظرفیت را درخواست کنند. اگر هر دو موفق شوند، اعتماد کلینیک و بیمار از بین می‌رود.
2. **صحت مالی**: تراکنش‌های پرداخت و بازپرداخت باید قابل ممیزی باشند. یک callback تکراری نباید باعث بازپرداخت دوگانه شود.
3. **پیچیدگی قابل‌کنترل**: تیم کوچک است؛ نباید از روز اول با پیچیدگی Microservices غرق شود.

## Decision

از **Microsoft SQL Server** به‌عنوان دیتابیس رابطه‌ای و **Entity Framework Core** به‌عنوان ORM استفاده می‌کنیم.

### دلایل اصلی

- **پشتیبانی از تراکنش ACID**:SQL Server از isolation levelهای مختلف، `UPDLOCK`، `HOLDLOCK` و `SERIALIZABLE` پشتیبانی می‌کند که برای جلوگیری از Overbooking ضروری است (نمایش در ADR-003).
- **قیدهای دیتابیس**: قیدهای یکتا، CHECK constraint و کلید خارجی به‌صورت native قابل تعریف هستند. این قیود حتی در صورت باگ در لایهٔ Application نیز از صحت داده محافظت می‌کنند.
- **ایندکس‌های فیلترشده**: SQL Server از filtered index پشتیبانی می‌کند که برای یافتن رزروهای فعال (`Status IN ('PendingPayment','Confirmed')`) مناسب است.
- **بلوغ اکوسیستم**: EF Core ابزارهای migration، tracking،并发 کنترل و integration test با Testcontainers را به‌صورت پایدار ارائه می‌دهد.

## Alternatives Considered

### PostgreSQL + Npgsql
- **مزیت**: متن‌باز، هزینهٔ پایین‌تر، پشتیبانی از exclusion constraint برای بازه‌های زمانی.
- **دلیل رد**: تیم با SQL Server آشنایی بیشتری دارد. استعداد موجود روی T-SQL و SQL Server Agent قابل‌استفاده است. PostgreSQL exclusion constraint جذاب است اما با ترکیب `UPDLOCK + HOLDLOCK + index` در SQL Server نیز همان نتیجه قابل دستیابی است.

### MongoDB
- **مزیت**: schema انعطاف‌پذیر، مقیاس افقی ساده.
- **دلیل رد**: مشکل اصلی ما هم‌زمانی رزرو است، نه مقیاس افقی. تراکنش‌های چندسندی در MongoDB ممکن است اما طبیعی نیستند. قیود یکپارچگی native نیستند.

### Redis به‌عنوان منبع حقیقت رزرو
- **مزیت**: سرعت بالا، atomic operations با Lua.
- **دلیل رد**: Redis volatile است؛ انقضای قفل توزیع‌شده می‌تواند باعث Overbooking شود. طبق ADR-003، SQL Server مرجع قطعی ظرفیت خواهد بود؛ Redis در صورت لزوم فقط cache است.

## Consequences

### مثبت

- تراکنش‌های ACID و قفل‌گذاری قابل اتکا برای جلوگیری از Overbooking.
- قیود یکپارچگی در سطح دیتابیس، لایهٔ دوم دفاعی در برابر باگ‌های Application.
- اکوسیستم بلوغ‌یافتهٔ EF Core و Testcontainers برای تست یکپارچگی واقعی.
- پشتیبانی از SQL Server Agent برای کارهای زمان‌بندی‌شدهٔ داخلی (در صورت نیاز).

### منفی

- وابستگی به پلتفرم مایکروسافت (لاisenس SQL Server).
- طراحی اولیهٔ schema زمان‌بر است اما در نسخهٔ اول با ماژول‌بندی داخلی کنترل می‌شود.
- در آینده اگر به مقیاس چندکلینیکی برسیم، sharding در SQL Server ساده نیست؛ اما در نسخهٔ اول خارج از محدوده است.

## References

- [EF Core Documentation](https://learn.microsoft.com/ef/core/)
- [SQL Server Locking](https://learn.microsoft.com/sql/relational-databases/sql-server-locking)
- ADR-003 (Database-first capacity truth) — مفصل‌ترین استفاده از این تصمیم
