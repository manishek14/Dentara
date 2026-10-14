# ADR-006: Idempotency برای عملیات خارجی

- **Status**: Accepted
- **Date**: 2026-10-14
- **Decision Owner**: Architecture Team

## Context

Dentara با چند سیستم خارجی صحبت می‌کند:

1. **درگاه پرداخت** — کاربر به‌سادگی می‌تواند دکمهٔ "pay" را دو بار بزند یا درگاه ممکن است callback را چند بار بفرستد.
2. **سرویس SMS (sms.ir)** — اگر timeout شود، سیستم retry می‌کند؛ نباید OTP دومی برای همان درخواست ارسال شود.
3. **Background Worker** — اگر روی چند instance اجرا شود یا crash کند و restart شود، نباید همان رزرو را دو بار منقضی کند یا همان بازپرداخت را دو بار بزند.
4. **API endpoints حساس** — لغو رزرو، درخواست بازپرداخت، ایجاد رزرو: کاربر ممکن است به‌دلیل network timeout دکمه را دو بار بزند.

در همهٔ این موارد، **هدف یکسانی برای تکرار درخواست**: نتیجهٔ نهایی باید همان نتیجهٔ اول باشد. هیچ‌گاه نباید دو رزرو، دو پرداخت، دو بازپرداخت یا دو رویداد ممیزی از یک درخواست منطقی واحد ساخته شود.

## Decision

هر عملیات خارجی یا حساس با یک **Idempotency Key** همراه است. این کلید در `IdempotencyRecords` ذخیره می‌شود و پیش از اجرای عملیات بررسی می‌شود.

### ساختار IdempotencyRecords

| ستون | نوع | توضیح |
|------|-----|------|
| `Id` | GUID | PK |
| `Scope` | nvarchar(64) | "payment_callback", "refund", "appointment_cancel", "sms_otp" و ... |
| `Key` | nvarchar(256) | کلید یکتا در محدوده Scope (مثلاً شناسه رویداد درگاه) |
| `UserId` | GUID nullable | کاربر آغازکننده (در صورت وجود) |
| `RequestHash` | nvarchar(512) | هش پارامترهای درخواست برای تشخیص تفاوت |
| `Status` | nvarchar(32) | `InProgress`, `Completed`, `Failed` |
| `ResponsePayload` | nvarchar(max) nullable | پاسخ ذخیره‌شده برای بازگشت به کلاینت تکراری |
| `CreatedAtUtc` | DATETIME2 | — |
| `CompletedAtUtc` | DATETIME2 nullable | — |
| `ExpiresAtUtc` | DATETIME2 nullable | اگر کلید مدت‌دار است |

**قید یکتا**: `UNIQUE (Scope, Key)`.

### جریان عملیات idempotent

```
1. درخواست با IdempotencyKey می‌رسد.
2. INSERT INTO IdempotencyRecords با Status=InProgress.
   - اگر unique constraint شکست خورد: کلید تکراری است.
     - رکورد موجود را بخوان.
     - اگر Status=Completed: پاسخ ذخیره‌شده را برگردان.
     - اگر Status=InProgress: درخواست در حال انجام است؛ 409 Conflict یا 425 Too Early برگردان.
     - اگر Status=Failed: ممکن است retry مجاز باشد؛ بسته به Scope.
3. عملیات اصلی را اجرا کن.
4. رکورد را به Completed یا Failed به‌روز کن.
5. پاسخ را برگردان.
```

### استراتژی برای callbackهای درگاه پرداخت

هر callback از درگاه دارای `ProviderEventId` است (به‌علاوهٔ `OrderId` و `ProviderTransactionId`).

1. هنگام دریافت callback:
   - ابتدا امضای پیام اعتبارسنجی شود.
   - سپس `PaymentWebhookEvents` با `(Provider, ProviderEventId)` یکتا INSERT می‌شود. اگر شکست خورد → callback تکراری است؛ ack برگردان و ترک کن.
2. سپس پرداخت مرتبط پیدا می‌شود و `PaymentStatus` تغییر می‌کند — اما این تغییر داخل یک تراکنش است که شامل INSERT وب‌هوک هم می‌شود تا از race بین دو callback هم‌زمان جلوگیری کند.

### استراتژی برای SMS OTP

- `Scope = "sms_otp"`, `Key = phoneNumber + purpose` (مثلاً `+98912...:login`).
- TTL: ۶۰ ثانیه. اگر درخواست تکراری در این بازه بیاید، همان OTP قبلی دوباره ارسال می‌شود (نه OTP جدید).
- اگر TTL گذشته بود، OTP جدید تولید و ارسال می‌شود.

### استراتژی برای Background Worker

- هر job یک `JobRunId` دارد که در `IdempotencyRecords` با `Scope = "worker_job"` ذخیره می‌شود.
- اگر Worker restart شود و همان job دوباره اجرا کند، رکورد `Completed` پیدا می‌شود و اجرا دوباره skip می‌شود.
- برای jobهای رزرو انقضا: تغییر وضعیت در یک تراکنش اتمیک با `AppointmentEvents` INSERT انجام می‌شود. اگر `AppointmentEvents` با همان `EventId` یکتا از قبل وجود داشت، skip می‌شود.

### استراتژی برای API endpoints

- endpointهای حساس (Create Booking, Cancel Appointment, Request Refund) یک هدر `Idempotency-Key` می‌پذیرند.
- اگر هدر وجود داشت، جریان بالا اجرا می‌شود.
- اگر هدر نبود، عملیات بدون idempotency اجرا می‌شود (در نسخه اول همهٔ endpointهای mutation باید هدر را اجباری کنند).

## Alternatives Considered

### فقط UNIQUE constraint روی شناسه درگاه
- **مزیت**: ساده، یک جدول کمتر.
- **دلیل رد**: 
  - فقط برای callbackهای درگاه کار می‌کند، نه برای SMS یا Worker.
  - اگر callback اول fail شد و callback دومی آمد، UNIQUE constraint جلوی INSERT دوم را می‌گیرد اما پاسخ ذخیره‌شده‌ای برای بازگشت نیست.
  - درخواست‌های API (لغو، بازپرداخت) شناسهٔ خارجی ندارند که UNIQUE باشد.

### Idempotency در Redis
- **مزیت**: سریع‌تر، TTL خودکار.
- **دلیل رد**: 
  - Redis volatile است. اگر callback دیرهنگام بعد از TTL بیاید، idempotency از بین رفته.
  - نمی‌توان در تراکنش دیتابیس atomic اجرا کرد.
  - برای صحت مالی، مرجع idempotency باید در همان SQL Server باشد که مرجع رزرو است.

### درج رویداد تکراری و سپس dedup در query
- **دلیل رد**: پیچیدگی queryها، احتمال باگ بالا، عدم تشخیص سریع.

## Consequences

### مثبت

- **صحت مالی**: هیچ callback تکراری باعث دو بار تأیید پرداخت یا دو بار بازپرداخت نمی‌شود.
- **UX بهتر**: کاربر می‌تواند دکمه را retry کند بدون اینکه نگران دو بار کسر شود.
- **پایداری Worker**: restart یا crash باعث اجرای دوبارهٔ عملیات حساس نمی‌شود.
- **ممیزی**: `IdempotencyRecords` تاریخچهٔ کاملی از درخواست‌ها و پاسخ‌ها دارد.

### منفی

- **پیچیدگی توسعه**: developerها باید برای هر endpoint حساس، جریان idempotency را پیاده کنند. این با یک `IdempotencyFilter` (Action Filter در ASP.NET Core) یا MediatR pipeline behavior کپسوله می‌شود.
- **افزایش حجم دیتابیس**: رکوردهای IdempotencyRecords باید پاک شوند (مثلاً بعد از ۹۰ روز) تا حجم کنترل شود. یک cleanup job ماهانه این کار را انجام می‌دهد.
- **احتمال deadlock در race condition**: اگر دو درخواست هم‌زمان با یک Key بیایند، یکی باید شکست بخورد. این با `INSERT` و UNIQUE constraint طبیعی است؛ handler باید 409 برگرداند.

## Implementation Notes

- helper `IIdempotencyService` در Application:
  ```csharp
  public interface IIdempotencyService
  {
      Task<TResponse> ExecuteAsync<TResponse>(
          string scope,
          string key,
          Func<Task<TResponse>> action,
          CancellationToken ct);
  }
  ```
- MediatR Pipeline Behavior برای اعمال خودکار روی Commandها با `[Idempotent]` attribute.
- برای callback درگاه، یک `IWebhookHandler` interface که در یک تراکنش اجرا می‌شود.

## References

- [Idempotency in HTTP APIs](https://datatracker.ietf.org/doc/html/draft-ietf-httpapi-idempotency-key-header)
- [Stripe Idempotency](https://stripe.com/docs/api/idempotent_requests)
- ADR-003 (Database-first capacity truth) — تأکید مجدد بر SQL Server به‌عنوان مرجع
- ADR-004 (Separate booking and payment states)
