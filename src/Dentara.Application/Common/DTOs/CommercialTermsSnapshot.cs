namespace Dentara.Application.Common.DTOs;

/// <summary>
/// Snapshot of an appointment's commercial terms at booking time (ADR-007).
/// Used by both booking commands (to capture the snapshot) and audit reports
/// (to display what was agreed).
/// </summary>
public sealed record CommercialTermsSnapshot(
    string ServiceName,
    int ServiceDurationMinutes,
    decimal PriceAmount,
    string Currency,
    string DentistDisplayName,
    int CancellationPolicyVersion,
    decimal CancellationFeePercent,
    decimal RefundablePercent,
    string? TermsSnapshotHash = null);
