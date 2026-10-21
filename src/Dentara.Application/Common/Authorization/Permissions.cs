namespace Dentara.Application.Common.Authorization;

/// <summary>
/// All permissions in the system. Each permission is a string constant
/// that maps to a specific action. Roles are granted collections of permissions.
///
/// Permissions are enforced in the Backend via [HasPermission] attribute
/// on controller actions and via IAuthorizationService checks in handlers.
/// The UI may also hide/show elements based on permissions, but the Backend
/// is the authoritative enforcer.
/// </summary>
public static class Permissions
{
    // Appointment management
    public const string AppointmentCreate = "appointment.create";
    public const string AppointmentViewOwn = "appointment.view_own";     // patient sees their own
    public const string AppointmentViewAll = "appointment.view_all";     // staff sees all
    public const string AppointmentCancel = "appointment.cancel";
    public const string AppointmentCancelAny = "appointment.cancel_any"; // staff cancels any
    public const string AppointmentConfirm = "appointment.confirm";
    public const string AppointmentComplete = "appointment.complete";
    public const string AppointmentMarkNoShow = "appointment.mark_noshow";

    // Payment management
    public const string PaymentInitiate = "payment.initiate";
    public const string PaymentViewAll = "payment.view_all";
    public const string RefundRequest = "refund.request";
    public const string RefundApprove = "refund.approve";
    public const string WebhookProcess = "webhook.process";

    // Catalog management
    public const string ServiceManage = "service.manage";       // create/edit/deactivate services
    public const string ResourceManage = "resource.manage";     // manage clinic resources
    public const string PricingManage = "pricing.manage";       // change prices

    // Scheduling
    public const string ScheduleManage = "schedule.manage";     // work schedules, exceptions
    public const string BookingWindowPublish = "booking_window.publish";

    // Identity & user management
    public const string UserManage = "user.manage";             // create/edit/deactivate users
    public const string RoleAssign = "role.assign";             // assign/revoke roles
    public const string OtpRequest = "otp.request";

    // Waitlist
    public const string WaitlistJoin = "waitlist.join";
    public const string WaitlistViewAll = "waitlist.view_all";
    public const string WaitlistManage = "waitlist.manage";     // manually offer/decline

    // Dashboard & reports
    public const string DashboardView = "dashboard.view";
    public const string ReportsView = "reports.view";
    public const string AuditLogView = "audit_log.view";

    /// <summary>
    /// All permissions as a list (for validation and seeding).
    /// </summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        AppointmentCreate, AppointmentViewOwn, AppointmentViewAll,
        AppointmentCancel, AppointmentCancelAny, AppointmentConfirm,
        AppointmentComplete, AppointmentMarkNoShow,
        PaymentInitiate, PaymentViewAll, RefundRequest, RefundApprove,
        WebhookProcess,
        ServiceManage, ResourceManage, PricingManage,
        ScheduleManage, BookingWindowPublish,
        UserManage, RoleAssign, OtpRequest,
        WaitlistJoin, WaitlistViewAll, WaitlistManage,
        DashboardView, ReportsView, AuditLogView
    };
}

/// <summary>
/// Role-to-permission mappings. Each role gets a specific set of permissions.
/// These are enforced at the Backend; the UI uses them for display only.
/// </summary>
public static class RolePermissions
{
    /// <summary>
    /// Patient: can book, view own, cancel own, initiate payments, join waitlist.
    /// </summary>
    public static readonly IReadOnlyList<string> Patient = new[]
    {
        Permissions.AppointmentCreate,
        Permissions.AppointmentViewOwn,
        Permissions.AppointmentCancel,
        Permissions.PaymentInitiate,
        Permissions.OtpRequest,
        Permissions.WaitlistJoin
    };

    /// <summary>
    /// Dentist: can view their own appointments, complete them, mark no-show.
    /// </summary>
    public static readonly IReadOnlyList<string> Dentist = new[]
    {
        Permissions.AppointmentViewAll,
        Permissions.AppointmentComplete,
        Permissions.AppointmentMarkNoShow,
        Permissions.DashboardView
    };

    /// <summary>
    /// Receptionist: can manage appointments, publish windows, view waitlist.
    /// </summary>
    public static readonly IReadOnlyList<string> Receptionist = new[]
    {
        Permissions.AppointmentViewAll,
        Permissions.AppointmentCancelAny,
        Permissions.AppointmentConfirm,
        Permissions.PaymentViewAll,
        Permissions.RefundRequest,
        Permissions.ScheduleManage,
        Permissions.BookingWindowPublish,
        Permissions.WaitlistViewAll,
        Permissions.WaitlistManage,
        Permissions.DashboardView
    };

    /// <summary>
    /// Admin: full access to everything.
    /// </summary>
    public static readonly IReadOnlyList<string> Admin = Permissions.All.ToList();

    /// <summary>
    /// Returns the permissions for a given role name.
    /// </summary>
    public static IReadOnlyList<string> GetForRole(string roleName) => roleName switch
    {
        nameof(Domain.Identity.Role.Patient) => Patient,
        nameof(Domain.Identity.Role.Dentist) => Dentist,
        nameof(Domain.Identity.Role.Receptionist) => Receptionist,
        nameof(Domain.Identity.Role.Admin) => Admin,
        _ => Array.Empty<string>()
    };
}
