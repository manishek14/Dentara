# ADR-007: Snapshot شرایط تجاری هنگام رزرو

- **Status**: Accepted
- **Date**: 2026-10-14
- **Decision Owner**: Architecture Team

## Context

Dentara شرایط تجاری متغیری دارد:

- **قیمت خدمت** ممکن است به‌دلیل تورم یا تغییر سیاست کلینیک تغییر کند.
- **مدت خدمت** ممکن است با تجربه بهبود یابد (مثلاً عقب‌کشیدن دندان از ۳۰ دقیقه به ۲۰ دقیقه).
- **سیاست لغو** ممکن است در آینده تنظیم شود (مثلاً ۸۵٪ بازپرداخت در نسخه اول، اما شاید در نسخه بعدی ۷۰٪ برای لغو کمتر از ۲۴ ساعت).
- **نام خدمت** و **توضیحات** ممکن است تغییر کند.
- **نام پزشک** ممکن است تغییر کند (مثلاً تغییر نام خانوادگی).

هر رزرو باید **با همان شرایطی که در زمان ثبت توافق شده** اجرا شود. اگر یک بیمار در تاریخ ۱ رزرو می‌کند و در تاریخ ۱۵ خدمت دریافت می‌کند، قیمت نهایی باید همان قیمتی باشد که در تاریخ ۱ توافق شده، حتی اگر در تاریخ ۱۰ قیمت افزایش یافته باشد.

به‌علاوه، سیاست لغو نیز باید در زمان رزرو snapshot شود. اگر کلینیک در آینده سیاست را به "بدون بازپرداخت" تغییر دهد، رزروهای قدیمی باید همچنان قابل لغو با بازپرداخت ۸۵٪ باشند.

## Decision

هنگام ایجاد `Appointment`، چند فیلد به‌صورت **snapshot** کپی و در ردیف رزرو ذخیره می‌شوند:

### فیلدهای snapshot شده

| فیلد در Appointment | منبع در زمان رزرو |
|---------------------|------------------|
| `ServiceId` | `Services.Id` (FK، اما نسخه snapshot هم نگه می‌داریم) |
| `ServiceNameSnapshot` | `Services.Name` |
| `ServiceDurationMinutesSnapshot` | `Services.DurationMinutes` |
| `PriceAmountSnapshot` | `Services.Price` یا قیمت خاص پزشک |
| `PriceCurrencySnapshot` | `Services.Currency` |
| `DentistId` | `Dentists.Id` (FK) |
| `DentistDisplayNameSnapshot` | `DentistProfiles.DisplayName` |
| `CancellationPolicyVersionSnapshot` | نسخهٔ سیاست لغو فعال در زمان رزرو |
| `CancellationFeePercentSnapshot` | مثلاً `15.0` |
| `RefundablePercentSnapshot` | مثلاً `85.0` |
| `TermsSnapshotHash` | هش کل شرایط تجاری (برای audit) |

### رابطه با `CancellationPolicy`

یک جدول مرجع `CancellationPolicies` نگه می‌داریم:

| ستون | نوع |
|------|-----|
| `Id` | GUID |
| `Version` | int (رو به افزایش) |
| `Name` | nvarchar(128) |
| `RefundablePercent` | decimal(5,2) |
| `EffectiveFromUtc` | DATETIME2 |
| `EffectiveUntilUtc` | DATETIME2 nullable |
| `IsActive` | bit |

هنگام رزرو، policy فعال پیدا می‌شود و `Version` + `RefundablePercent` در رزرو snapshot می‌شود.

### محاسبهٔ مبلغ بازپرداخت

هنگام لغو:

```csharp
var refundAmount = appointment.PriceAmountSnapshot
    * (appointment.RefundablePercentSnapshot / 100m);
```

این محاسبه با مقادیر snapshot انجام می‌شود، نه با policy فعلی. به این ترتیب، تغییر policy در آینده روی رزروهای قدیمی اثر نمی‌گذارد.

### تغییرات مدت خدمت

اگر `Services.DurationMinutes` بعد از رزرو تغییر کند، مدت رزرو قدیمی ثابت می‌ماند (`ServiceDurationMinutesSnapshot`). اما اگر کلینیک بخواهد رزرو قدیمی را به مدت جدید تغییر دهد، باید یک `AppointmentReschedule` صریح انجام شود که رزرو جدید با snapshot جدید می‌سازد.

### تغییرات قیمت

اگر `Services.Price` افزایش یابد، رزروهای قدیمی با قیمت قدیمی اجرا می‌شوند. اگر کلینیک بخواهد افزایش را روی رزروهای آینده اعمال کند، باید صبر کند تا snapshot در رزرو جدید اعمال شود.

## Alternatives Considered

### نگه‌داری فقط FK و join در زمان اجرا
- **مزیت**: یک منبع حقیقت، بدون redundant data.
- **دلیل رد**: 
  - اگر قیمت تغییر کند، رزرو قدیمی با قیمت جدید محاسبه می‌شود → اختلاف مالی.
  - اگر خدمت حذف شود (soft delete)، رزرو قدیمی بدون اطلاعات می‌ماند.
  - در صورت dispute مالی، نمی‌توان ثابت کرد که "در زمان رزرو قیمت این بود".

### Event Sourcing کامل
- **مزیت**: تاریخچهٔ کامل تغییرات.
- **دلیل رد**: overhead برای نسخهٔ اول. snapshot به همان اندازه کافی است. Event Sourcing می‌تواند در آینده روی `AppointmentEvents` (که در ERD هست) اضافه شود.

### نگه‌داری نسخه‌های Services به‌صورت temporal table
- **مزیت**: SQL Server temporal tables به‌صورت native تاریخچه نگه می‌دارد.
- **دلیل رد**: 
  - پیچیدگی query برای join با `FOR SYSTEM_TIME AS OF`.
  - برای سیاست لغو که در جدول مجزا است، temporal table سود کمتری دارد.
  - snapshot ساده‌تر و قابل‌ممیزی‌تر است.

## Consequences

### مثبت

- **صحت مالی**: هر رزرو با شرایط توافق‌شده اجرا می‌شود.
- **ممیزی**: در صورت dispute، می‌توان با `TermsSnapshotHash` تأیید کرد که شرایط چه بوده.
- **سادگی query**: برای محاسبهٔ مبلغ بازپرداخت، نیازی به join با `Services` یا `CancellationPolicies` نیست.
- **پایداری در برابر تغییر**: تغییرات در `Services` یا `CancellationPolicies` روی رزروهای قدیمی اثر نمی‌گذارد.

### منفی

- **افزایش حجم ردیف رزرو**: هر رزرو حدود ۱۰ فیلد اضافی دارد. در مقیاس نسخهٔ اول (هزاران رزرو در سال) قابل‌چشم‌پوشی است.
- **نیاز به انضباط**: developerها باید در زمان create/update رزرو، snapshot را به‌روز کنند. این با یک متد factory `Appointment.Create(...)` در Domain کپسوله می‌شود.
- **تغییرات همزمان**: اگر در حین رزرو، قیمت در حال تغییر باشد، ممکن است snapshot با قیمت جدید یا قدیمی بسته به timing گرفته شود. این مورد با تراکنش اتمیک (ADR-003) کنترل می‌شود.

### محدودیت‌های اعمال‌شده

- **امکان reschedule محدود**: اگر کاربر بخواهد زمان رزرو را تغییر دهد، باید snapshot جدید با قیمت فعلی گرفته شود (یا همان snapshot قبلی حفظ شود — تصمیم کسب‌وکار).
- **گزارش‌های مالی باید snapshot را در نظر بگیرند**: گزارش درآمد نباید از `Services.Price` فعلی استفاده کند.

## Implementation Notes

- متد factory `Appointment.Create(...)` تمام پارامترهای snapshot را اجباری می‌کند.
- هنگام لغو، `RefundService` از فیلدهای snapshot استفاده می‌کند:
  ```csharp
  public decimal CalculateRefund(Appointment appointment)
  {
      if (appointment.Status != AppointmentStatus.Confirmed) return 0;
      return appointment.PriceAmountSnapshot
          * (appointment.RefundablePercentSnapshot / 100m);
  }
  ```
- `TermsSnapshotHash` برای audit: هش JSON حاوی `{serviceId, price, duration, cancellationPolicyVersion}`.

## References

- [Snapshot Pattern](https://martinfowler.com/eaaDev/EventSourcing.html)
- ADR-003 (Database-first capacity truth)
- ADR-004 (Separate booking and payment states)
