# Architecture Decision Records (ADR)

این پوشه شامل تصمیم‌های معماری پروژه Dentara است. هر ADR یک تصمیم غیرقابل‌برگشت یا کم‌قابل‌برگشت را ثبت می‌کند تا در آینده دلیل آن مشخص باشد.

## فهرست ADRها

| شماره | عنوان | وضعیت |
|------|-------|--------|
| [ADR-001](./ADR-001-sql-server-and-ef-core.md) | SQL Server + EF Core به‌عنوان مرجع داده | Accepted |
| [ADR-002](./ADR-002-modular-monolith.md) | Modular Monolith به‌جای Microservices | Accepted |
| [ADR-003](./ADR-003-database-first-capacity-truth.md) | SQL Server مرجع قطعی ظرفیت است | Accepted |
| [ADR-004](./ADR-004-separate-booking-and-payment-states.md) | جدایی چرخهٔ وضعیت رزرو و پرداخت | Accepted |
| [ADR-005](./ADR-005-utc-storage-and-persian-display.md) | ذخیرهٔ UTC + نمایش تقویم شمسی | Accepted |
| [ADR-006](./ADR-006-idempotent-external-operations.md) | Idempotency برای عملیات خارجی | Accepted |
| [ADR-007](./ADR-007-snapshot-commercial-terms.md) | Snapshot شرایط تجاری هنگام رزرو | Accepted |

## قالب ADR

```markdown
# ADR-XXX: Title

- **Status**: Accepted | Superseded by ADR-YYY | Deprecated
- **Date**: YYYY-MM-DD
- **Decision Owner**: Team

## Context
چرا این تصمیم لازم است؟ چه محدودیت‌ها و الزاماتی وجود دارد؟

## Decision
تصمیم نهایی چیست؟

## Alternatives Considered
چه گزینه‌های دیگری بررسی شدند و چرا رد شدند؟

## Consequences
پیامدهای مثبت و منفی این تصمیم چیست؟
```

## چگونه ADR جدید اضافه کنیم؟

1. شمارهٔ بعدی را انتخاب کنید (مثلاً `ADR-008`)
2. فایل جدید با نام `ADR-008-short-slug.md` بسازید
3. در جدول بالا یک سطر اضافه کنید
4. PR با حداقل یک reviewer
