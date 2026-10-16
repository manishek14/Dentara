namespace Dentara.Application.Common.DTOs;

/// <summary>
/// Display date in both UTC (canonical) and Persian Shamsi (display only, per ADR-005).
/// The Application layer computes the Persian string; the API layer returns it as-is.
/// </summary>
public sealed record DisplayDate(
    DateTimeOffset Utc,
    string PersianShamsiShort, // e.g. "1405/07/22"
    string PersianShamsiLong,  // e.g. "چهارشنبه 22 مهر 1405"
    string TimeOfDayLocal);    // e.g. "14:30"
