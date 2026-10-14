# ADR-005: ذخیرهٔ UTC + نمایش تقویم شمسی

- **Status**: Accepted
- **Date**: 2026-10-14
- **Decision Owner**: Architecture Team

## Context

Dentara در ایران راه‌اندازی می‌شود. کاربران با تقویم شمسی آشنایند و انتظار دارند تقویم رزرو به‌صورت شمسی نمایش داده شود. با این حال، سیستم باید قابل‌اعتماد و قابل‌ممیزی باشد:

- DST (daylight saving time) در ایران تغییر می‌کند (گاهی بدون اطلاع قبلی).
- یک کلینیک ممکن است در آینده در منطقهٔ زمانی دیگری باز شود (امکان خارج از محدوده نسخه اول).
- لاگ‌ها و گزارش‌های مالی باید برای مرجع زمانی ثابت قابل بازخوانی باشند.
- مقایسهٔ زمان رزرو با زمان فعلی نباید وابسته به timezone سرور باشد.

## Decision

### قاعدهٔ ذخیره‌سازی

- **تمام زمان‌های واقعی به‌صورت UTC** در دیتابیس ذخیره می‌شوند.
- ستون‌ها با پسوند `Utc` نام‌گذاری می‌شوند: `StartAtUtc`, `EndAtUtc`, `ExpiresAtUtc`, `CreatedAtUtc`, `ConfirmedAtUtc`.
- نوع داده در SQL Server: `DATETIME2(0)` با دقت ثانیه. اگر در آینده دقت میلی‌ثانیه لازم شد، `DATETIME2(3)`.

### قاعدهٔ نمایش

- تقویم شمسی **صرفاً قالب نمایش و ورود** است. هرگز در دیتابیس ذخیره نمی‌شود.
- تبدیل با کتابخانهٔ [`MDPersianDateTime`](https://github.com/Mds92/MD.PersianDateTime) یا معادل استاندارد انجام می‌شود.
- منطقهٔ زمانی کلینیک در `ClinicSettings` با فرمت IANA (مثلاً `Asia/Tehran`) ذخیره می‌شود.

### قاعدهٔ تبدیل

```
[User input: "1405/07/22 14:30"]
    ↓ (PersianCalendar + clinic timezone)
[UTC storage: "2026-10-14 11:00:00Z"]
    ↓ (reverse conversion for display)
[User display: "1405/07/22 14:30"]
```

### API Contract

- API ورودی و خروجی زمان را به‌صورت **ISO 8601 UTC** می‌پذیرد: `2026-10-14T11:00:00Z`.
- فرانت‌اند مسئول تبدیل به شمسی برای نمایش و از شمسی به UTC برای ارسال است.
- در پاسخ‌های API، علاوه بر ISO UTC، می‌توان فیلد `DisplayDateLocal` (به‌صورت رشتهٔ شمسی) هم فراهم کرد برای راحتی فرانت‌های ساده.

### ساعت کاری

`WorkSchedules` روز هفته و بازهٔ ساعت را ذخیره می‌کند (نه timestamp مطلق). روز هفته با `DayOfWeek` استاندارد دات‌نایت (Sunday=0...Saturday=6) نگه‌داری می‌شود. ساعت به‌صورت `TimeSpan` (مثلاً `09:00:00` تا `17:00:00`) در منطقهٔ زمانی کلینیک معنا دارد.

هنگام تولید `PublishedSessions`، timestampهای مطلق UTC محاسبه و ذخیره می‌شوند. این محاسبه فقط هنگام انتشار انجام می‌شود و پس از آن `PublishedSessions.StartAtUtc` مرجع است.

## Alternatives Considered

### ذخیرهٔ زمان محلی (Tehran time) در دیتابیس
- **دلیل رد**: 
  - DST در ایران تغییر می‌کند. اگر رزرو در تابستان ثبت شده باشد و در زمستان نمایش داده شود، یک ساعت اختلاف ایجاد می‌شود.
  - اگر سرور به منطقهٔ زمانی دیگری منتقل شود، همهٔ timestampها اشتباه می‌شوند.
  - مقایسهٔ `StartAtUtc` با `GETUTCDATE()` ساده است؛ مقایسهٔ `StartAtLocal` با `SYSDATETIMEOFFSET()` پیچیده.

### DateTimeOffset در دیتابیس
- **مزیت**: offset همراه زمان ذخیره می‌شود.
- **دلیل رد**: 
  - همان مشکل DST: offset در زمان ثبت ممکن است `+04:30` باشد اما در زمان نمایش `+03:30` شود.
  - مقایسهٔ دو `DateTimeOffset` با offset متفاوت نیاز به normalization دارد.
  - در عمل، تیم با UTC سازگارتر است.

### PersianDateTime به‌عنوان نوع اصلی
- **دلیل رد**: 
  - تقویم شمسی ماه‌های ۳۱ و ۳۰ روزه دارد؛ محاسبهٔ فاصلهٔ زمانی پیچیده می‌شود.
  - کتابخانه‌های PersianDateTime بلوغ `DateTime` را ندارند.
  - در صورت نیاز به انتشار بین‌المللی در آینده، مهاجرت غیرممکن می‌شود.

## Consequences

### مثبت

- **بدون ابهام**: هر timestamp در دیتابیس یک معنای یکتا دارد.
- **مقاوم در برابر DST**: تغییر ساعت تابستانه روی داده‌های ذخیره‌شده اثر نمی‌گذارد.
- **ممیزی ساده**: `GETUTCDATE()` در SQL Server مرجع ثابت است.
- **قابلیت مقیاس**: در آینده اگر منطقهٔ زمانی دیگری اضافه شود، تنها تنظیمات کلینیک تغییر می‌کند.

### منفی

- **سرباری تبدیل**: هر نمایش نیاز به تبدیل UTC → شمسی دارد. این کار در لایهٔ presentation با helper انجام می‌شود.
- **نیاز به انضباط تیم**: developerها باید همیشه `Utc` پسوند را ببینند و در ذهن داشته باشند که این UTC است. code review باید این را چک کند.
- **لاگ‌ها بر اساس UTC**: support باید بتواند UTC را به شمسی تبدیل کند. ابزارهای internal این تبدیل را به‌صورت خودکار انجام می‌دهند.

### مزیت جانبی: تست

تست‌های یکپارچگی می‌توانند با timestampهای UTC ثابت کار کنند و نگران timezone سرور CI نباشند. این تست‌پذیری را به‌شدت ساده می‌کند.

## Implementation Notes

- helper class `IClock` برای abstraction زمان فعلی (تست‌پذیری):
  ```csharp
  public interface IClock
  {
      DateTimeOffset UtcNow { get; }
  }
  ```
- helper `IPersianCalendar` برای تبدیل UTC ↔ شمسی.
- در `appsettings.json`:
  ```json
  "Clinic": {
    "TimeZoneId": "Asia/Tehran"
  }
  ```

## References

- [UTC is enough for everyone](https://yellerapp.com/posts/2015-01-12-the-problem-with-datetimes.html)
- [IANA Time Zone Database](https://www.iana.org/time-zones)
- ADR-001 (SQL Server + EF Core)
