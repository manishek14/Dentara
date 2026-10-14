# ADR-002: Modular Monolith به‌جای Microservices

- **Status**: Accepted
- **Date**: 2026-10-14
- **Decision Owner**: Architecture Team

## Context

نسخهٔ اول Dentara برای یک کلینیک دندان‌پزشکی طراحی می‌شود. محدودهٔ اولیه شامل ۵ حوزهٔ مشخص است:

1. **Identity** — احراز هویت، بیماران، دندان‌پزشک‌ها، نقش‌ها
2. **Catalog** — خدمات، منابع (یونیت‌ها)، پیش‌نیازها
3. **Scheduling** — برنامهٔ کاری، استثناها، پنجرهٔ انتشار نوبت
4. **Booking** — رزرو، تخصیص منابع، رویدادها
5. **Payments** — تلاش پرداخت، بازپرداخت، callback، idempotency

این حوزه‌ها به‌شدت به هم وابسته‌اند: رزرو نیازمند بررسی برنامهٔ کاری، منابع و خدمات است؛ پرداخت با رزرو یک‌به‌یک مرتبط است؛ لغو باید هم رزرو و هم پرداخت را به‌روز کند.

تیم کوچک است و یک کلینیک در نسخهٔ اول سرو می‌شود. هیچ نیازی به deploy مستقل یا scale مستقل حوزه‌ها وجود ندارد.

## Decision

از الگوی **Modular Monolith** استفاده می‌کنیم: یک Process واحد، یک دیتابیس واحد، اما با ماژول‌های داخلی مشخص.

### ساختار ماژول‌ها

هر ماژول به‌صورت پوشه‌ای در هر لایه (Domain/Application/Infrastructure) ظاهر می‌شود:

```
src/Dentara.Domain/
├── Identity/
│   ├── User.cs
│   ├── PatientProfile.cs
│   └── ...
├── Catalog/
│   ├── Service.cs
│   └── ...
├── Scheduling/
├── Booking/
└── Payments/

src/Dentara.Application/
├── Identity/
├── Catalog/
├── Scheduling/
├── Booking/
└── Payments/
```

### قواعد ماژول‌بندی

1. **ارتباط مستقیم بین Entityهای ماژول‌ها ممنوع**. مثلاً `Appointment` نباید navigation property به `Service` داشته باشد؛ به‌جای آن با `ServiceId` (GUID) ارجاع می‌دهد.
2. **Use caseها در Application** می‌توانند چند ماژول را coordinate کنند. مثلاً `BookingFlowService` هم `Scheduling` (بررسی برنامهٔ کاری) و هم `Catalog` (پیدا کردن منبع سازگار) و هم `Booking` (درج رزرو) را صدا می‌زند.
3. **DbContext واحد** با schema یکپارچه. هر ماژول DbSetهای خود را expose می‌کند، اما همه در یک دیتابیس ذخیره می‌شوند.
4. **بسته‌بندی فیزیکی مجزا نداریم**. همه در یک assembly (`Dentara.Domain.dll`) هستند. مرز ماژول با namespace و قواعد review حفظ می‌شود.

## Alternatives Considered

### Microservices کامل
- **مزیت**: deploy مستقل، scale مستقل، fault isolation.
- **دلیل رد**: هزینهٔ عملیاتی بالا (service discovery, distributed tracing, saga, eventual consistency). برای یک کلینیک و تیم کوچک، overhead بیش از فایده است. وابستگی شدید بین حوزه‌ها به saga pattern نیاز دارد که پیچیدگی را به‌شدت بالا می‌برد.

### Single-layer Monolith
- **مزیت**: ساده‌ترین شروع.
- **دلیل رد**: در شش ماه آینده کد قابل‌نگهداری نخواهد بود. مرزهای ماژول به‌مرور محو می‌شوند و refactor بعدی غیرممکن می‌شود.

### Clean Architecture (لایه‌بندی خالص بدون ماژول)
- **مزیت**: آشنایی تیم، سادگی آموزش.
- **دلیل رد**: در عمل منجر به "god services" می‌شود که هم Identity و هم Booking و هم Payments را در یک کلاس می‌بینند. ماژول‌بندی صریح الزامی است.

## Consequences

### مثبت

- **سادگی توسعه**: developer محلی با یک Solution کار می‌کند، بدون نیاز به چند Repo یا docker-compose پیچیده.
- **Refactor آسان**: تغییر boundary بین ماژول‌ها در آینده (مثلاً استخراج Payments به Microservice) تنها در سطح namespace انجام می‌شود.
- **تست یکپارچگی واقعی**: یک test container با SQL Server کافی است؛ نیازی به mock چند سرویس نیست.
- **تراکنش ACID یکپارچه**: رزرو + تخصیص منابع + درج event در یک تراکنش انجام می‌شود. این در Microservices نیاز به saga دارد.

### منفی

- **مرز فیزیکی وجود ندارد**: اگر تیم به قواعد namespace پایبند نباشد، ماژول‌ها آلوده می‌شوند. کنترل با code review و ArchUnit-style tests لازم است.
- **عدم deploy مستقل**: تغییر در یک ماژول نیازمند deploy کل سیستم است. برای نسخهٔ اول قابل‌قبول.
- **اشتراک دیتابیس**: اگر در آینده یک ماژول بخواهد جدا شود، استخراج schema کار سنگینی است. اما با رعایت قاعدهٔ "نبود navigation بین ماژول‌ها"، این ریسک کاهش می‌یابد.

### مهاجرت آینده

اگر در نسخهٔ دوم نیاز به مقیاس چندکلینیکی داشته باشیم، ماژول **Payments** اولین کاندیدای استخراج به Microservice است (به‌دلیل ماهیت مستقل و بار PCI). ماژول **Catalog** و **Scheduling** احتمالاً در monolith می‌مانند چون با Booking به‌شدت coupling هستند.

## References

- [Modular Monoliths - Simon Brown](https://simonbrown.je/modular-monoliths/)
- [Microsoft Modular Monolith reference architecture](https://learn.microsoft.com/dotnet/architecture/modular-monoliths/)
- ADR-001 (SQL Server + EF Core)
