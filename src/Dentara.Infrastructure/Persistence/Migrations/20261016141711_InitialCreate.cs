using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Dentara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Appointments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DentistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublishedSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    EndAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    StatusValue = table.Column<int>(type: "int", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    ServiceNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ServiceDurationMinutesSnapshot = table.Column<int>(type: "int", nullable: false),
                    PriceAmountSnapshot = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrencySnapshot = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    DentistDisplayNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CancellationPolicyVersionSnapshot = table.Column<int>(type: "int", nullable: false),
                    CancellationFeePercentSnapshot = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    RefundablePercentSnapshot = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    TermsSnapshotHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ConfirmedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    ExpiredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    NoShowAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    CancelledBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointments", x => x.Id);
                    table.CheckConstraint("CK_Appointments_FeePlusRefundableEqual100", "ABS(CancellationFeePercentSnapshot + RefundablePercentSnapshot - 100.0) < 0.01");
                    table.CheckConstraint("CK_Appointments_StartBeforeEnd", "StartAtUtc < EndAtUtc");
                    table.CheckConstraint("CK_Appointments_StatusValue", "StatusValue IN (1, 2, 3, 4, 5, 6)");
                });

            migrationBuilder.CreateTable(
                name: "BookingWindows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpensAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    ClosesAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    ApplicableDateFromUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    ApplicableDateUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    StatusValue = table.Column<int>(type: "int", nullable: false),
                    DentistIdFilter = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OpenedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingWindows", x => x.Id);
                    table.CheckConstraint("CK_BookingWindows_ApplicableRangeValid", "ApplicableDateFromUtc < ApplicableDateUntilUtc");
                    table.CheckConstraint("CK_BookingWindows_OpensBeforeCloses", "OpensAtUtc < ClosesAtUtc");
                    table.CheckConstraint("CK_BookingWindows_StatusValue", "StatusValue IN (1, 2, 3, 4)");
                });

            migrationBuilder.CreateTable(
                name: "ClinicResources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TypeValue = table.Column<int>(type: "int", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    OwnerDentistId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicResources", x => x.Id);
                    table.CheckConstraint("CK_ClinicResources_CapacityPositive", "Capacity >= 1");
                    table.CheckConstraint("CK_ClinicResources_TypeValue", "TypeValue IN (1, 2, 3, 4)");
                });

            migrationBuilder.CreateTable(
                name: "DentistProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Specialization = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LicenseNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Bio = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DentistProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    StatusValue = table.Column<int>(type: "int", nullable: false),
                    ResponsePayload = table.Column<string>(type: "nvarchar(max)", maxLength: 2147483647, nullable: true),
                    ResponseStatusCode = table.Column<int>(type: "int", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecords", x => x.Id);
                    table.CheckConstraint("CK_IdempotencyRecords_StatusValue", "StatusValue IN (1, 2, 3)");
                });

            migrationBuilder.CreateTable(
                name: "PatientProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NationalCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DateOfBirth = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderValue = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    StatusValue = table.Column<int>(type: "int", nullable: false),
                    GatewayAuthority = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ProviderTransactionId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GatewayRedirectUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    InitiatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    SucceededAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    FailedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    LastCallbackAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TermsSnapshotHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAttempts", x => x.Id);
                    table.CheckConstraint("CK_PaymentAttempts_AmountPositive", "Amount > 0");
                    table.CheckConstraint("CK_PaymentAttempts_CurrencyLength", "LEN(Currency) = 3");
                    table.CheckConstraint("CK_PaymentAttempts_ProviderValue", "ProviderValue IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_PaymentAttempts_StatusValue", "StatusValue IN (1, 2, 3, 4, 5, 6, 7, 8, 9)");
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Value = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Value);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleExceptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DentistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TypeValue = table.Column<int>(type: "int", nullable: false),
                    EffectiveFromUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    EffectiveUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    WindowStartTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    WindowEndTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleExceptions", x => x.Id);
                    table.CheckConstraint("CK_ScheduleExceptions_FromBeforeUntil", "EffectiveFromUtc < EffectiveUntilUtc");
                    table.CheckConstraint("CK_ScheduleExceptions_TypeValue", "TypeValue IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_ScheduleExceptions_WindowConsistency", "((TypeValue IN (3, 4) AND WindowStartTime IS NOT NULL AND WindowEndTime IS NOT NULL) OR  (TypeValue IN (1, 2) AND WindowStartTime IS NULL AND WindowEndTime IS NULL))");
                });

            migrationBuilder.CreateTable(
                name: "Services",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    PriceAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.Id);
                    table.CheckConstraint("CK_Services_CurrencyLength", "LEN(Currency) = 3");
                    table.CheckConstraint("CK_Services_DurationPositive", "DurationMinutes > 0 AND DurationMinutes <= 1440");
                    table.CheckConstraint("CK_Services_PriceNonNegative", "PriceAmount >= 0");
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleValue = table.Column<int>(type: "int", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => x.Id);
                    table.CheckConstraint("CK_UserRoles_RoleValue", "RoleValue IN (1, 2, 3, 4)");
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PhoneNumberNormalized = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsPhoneVerified = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LastLoginAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    PhoneVerifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DentistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayOfWeekValue = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EffectiveFromUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    EffectiveUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkSchedules", x => x.Id);
                    table.CheckConstraint("CK_WorkSchedules_DayOfWeekRange", "DayOfWeekValue >= 0 AND DayOfWeekValue <= 6");
                    table.CheckConstraint("CK_WorkSchedules_StartBeforeEnd", "StartTime < EndTime");
                });

            migrationBuilder.CreateTable(
                name: "AppointmentEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventTypeValue = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", maxLength: 2147483647, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentEvents", x => x.Id);
                    table.CheckConstraint("CK_AppointmentEvents_EventTypeValue", "EventTypeValue IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11)");
                    table.ForeignKey(
                        name: "FK_AppointmentEvents_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResourceAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceTypeSnapshotValue = table.Column<int>(type: "int", nullable: false),
                    StartAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    EndAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    StatusValue = table.Column<int>(type: "int", nullable: false),
                    ReleasedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    ReleaseReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceAllocations", x => x.Id);
                    table.CheckConstraint("CK_ResourceAllocations_StartBeforeEnd", "StartAtUtc < EndAtUtc");
                    table.CheckConstraint("CK_ResourceAllocations_StatusValue", "StatusValue IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_ResourceAllocations_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PublishedSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookingWindowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DentistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    EndAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    HasEverBeenBooked = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublishedSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PublishedSessions_BookingWindows_BookingWindowId",
                        column: x => x.BookingWindowId,
                        principalTable: "BookingWindows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentWebhookEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderValue = table.Column<int>(type: "int", nullable: false),
                    ProviderEventId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    RawPayload = table.Column<string>(type: "nvarchar(max)", maxLength: 2147483647, nullable: false),
                    SignatureHeader = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SignatureVerified = table.Column<bool>(type: "bit", nullable: false),
                    ProcessedSuccessfully = table.Column<bool>(type: "bit", nullable: false),
                    ProcessingNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentWebhookEvents", x => x.Id);
                    table.CheckConstraint("CK_PaymentWebhookEvents_ProviderValue", "ProviderValue IN (1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_PaymentWebhookEvents_PaymentAttempts_PaymentAttemptId",
                        column: x => x.PaymentAttemptId,
                        principalTable: "PaymentAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Refunds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    StatusValue = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ProviderRefundId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    SucceededAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    FailedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Refunds", x => x.Id);
                    table.CheckConstraint("CK_Refunds_AmountPositive", "Amount > 0");
                    table.CheckConstraint("CK_Refunds_StatusValue", "StatusValue IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_Refunds_PaymentAttempts_PaymentAttemptId",
                        column: x => x.PaymentAttemptId,
                        principalTable: "PaymentAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DentistServices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DentistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DurationMinutesOverride = table.Column<int>(type: "int", nullable: true),
                    PriceAmountOverride = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CurrencyOverride = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DentistServices", x => x.Id);
                    table.CheckConstraint("CK_DentistServices_OverrideConsistency", "(PriceAmountOverride IS NULL AND CurrencyOverride IS NULL) OR (PriceAmountOverride IS NOT NULL AND CurrencyOverride IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_DentistServices_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResourceServiceRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TypeValue = table.Column<int>(type: "int", nullable: false),
                    QuantityRequired = table.Column<int>(type: "int", nullable: false),
                    RequireDentistOwned = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    SpecificResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClinicResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceServiceRequirements", x => x.Id);
                    table.CheckConstraint("CK_ResourceServiceRequirements_MutualExclusivity", "NOT (RequireDentistOwned = 1 AND SpecificResourceId IS NOT NULL)");
                    table.CheckConstraint("CK_ResourceServiceRequirements_QuantityPositive", "QuantityRequired >= 1");
                    table.CheckConstraint("CK_ResourceServiceRequirements_TypeValue", "TypeValue IN (1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_ResourceServiceRequirements_ClinicResources_ClinicResourceId",
                        column: x => x.ClinicResourceId,
                        principalTable: "ClinicResources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceServiceRequirements_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Value", "Name" },
                values: new object[,]
                {
                    { 1, "Patient" },
                    { 2, "Dentist" },
                    { 3, "Receptionist" },
                    { 4, "Admin" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentEvents_AppointmentId",
                table: "AppointmentEvents",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentEvents_AppointmentId_OccurredAtUtc",
                table: "AppointmentEvents",
                columns: new[] { "AppointmentId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentEvents_CorrelationId",
                table: "AppointmentEvents",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_DentistId_StartAtUtc_EndAtUtc",
                table: "Appointments",
                columns: new[] { "DentistId", "StartAtUtc", "EndAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_PatientId_CreatedAtUtc",
                table: "Appointments",
                columns: new[] { "PatientId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_PendingPayment_ExpiresAtUtc",
                table: "Appointments",
                columns: new[] { "StatusValue", "ExpiresAtUtc" },
                filter: "StatusValue = 1");

            migrationBuilder.CreateIndex(
                name: "IX_BookingWindows_Status_OpensAtUtc",
                table: "BookingWindows",
                columns: new[] { "StatusValue", "OpensAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicResources_IsActive",
                table: "ClinicResources",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicResources_TypeValue",
                table: "ClinicResources",
                column: "TypeValue");

            migrationBuilder.CreateIndex(
                name: "IX_DentistProfiles_UserId",
                table: "DentistProfiles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DentistServices_DentistId_ServiceId",
                table: "DentistServices",
                columns: new[] { "DentistId", "ServiceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DentistServices_ServiceId",
                table: "DentistServices",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_InProgress",
                table: "IdempotencyRecords",
                column: "StatusValue",
                filter: "StatusValue = 1");

            migrationBuilder.CreateIndex(
                name: "UX_IdempotencyRecords_Scope_Key",
                table: "IdempotencyRecords",
                columns: new[] { "Scope", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientProfiles_Name",
                table: "PatientProfiles",
                columns: new[] { "FirstName", "LastName" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientProfiles_UserId",
                table: "PatientProfiles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAttempts_AppointmentId",
                table: "PaymentAttempts",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAttempts_Status_LastCallbackAtUtc",
                table: "PaymentAttempts",
                columns: new[] { "StatusValue", "LastCallbackAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_PaymentAttempts_Provider_TransactionId",
                table: "PaymentAttempts",
                columns: new[] { "ProviderValue", "ProviderTransactionId" },
                unique: true,
                filter: "ProviderTransactionId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentWebhookEvents_PaymentAttemptId",
                table: "PaymentWebhookEvents",
                column: "PaymentAttemptId");

            migrationBuilder.CreateIndex(
                name: "UX_PaymentWebhookEvents_Provider_EventId",
                table: "PaymentWebhookEvents",
                columns: new[] { "ProviderValue", "ProviderEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublishedSessions_BookingWindowId",
                table: "PublishedSessions",
                column: "BookingWindowId");

            migrationBuilder.CreateIndex(
                name: "IX_PublishedSessions_DentistId_StartAtUtc",
                table: "PublishedSessions",
                columns: new[] { "DentistId", "StartAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_PublishedSessions_IdempotencyKey",
                table: "PublishedSessions",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_AppointmentId",
                table: "Refunds",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_PaymentAttemptId",
                table: "Refunds",
                column: "PaymentAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAllocations_Active_Overlap",
                table: "ResourceAllocations",
                columns: new[] { "ResourceId", "StartAtUtc", "EndAtUtc" },
                filter: "StatusValue = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAllocations_AppointmentId",
                table: "ResourceAllocations",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceServiceRequirements_ClinicResourceId",
                table: "ResourceServiceRequirements",
                column: "ClinicResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceServiceRequirements_ServiceId",
                table: "ResourceServiceRequirements",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleExceptions_DentistId",
                table: "ScheduleExceptions",
                column: "DentistId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleExceptions_EffectiveFromUtc",
                table: "ScheduleExceptions",
                column: "EffectiveFromUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Services_IsActive",
                table: "Services",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UserId_RoleValue",
                table: "UserRoles",
                columns: new[] { "UserId", "RoleValue" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_PhoneNumberNormalized",
                table: "Users",
                column: "PhoneNumberNormalized",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkSchedules_DentistId",
                table: "WorkSchedules",
                column: "DentistId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkSchedules_DentistId_DayOfWeek",
                table: "WorkSchedules",
                columns: new[] { "DentistId", "DayOfWeekValue" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppointmentEvents");

            migrationBuilder.DropTable(
                name: "DentistProfiles");

            migrationBuilder.DropTable(
                name: "DentistServices");

            migrationBuilder.DropTable(
                name: "IdempotencyRecords");

            migrationBuilder.DropTable(
                name: "PatientProfiles");

            migrationBuilder.DropTable(
                name: "PaymentWebhookEvents");

            migrationBuilder.DropTable(
                name: "PublishedSessions");

            migrationBuilder.DropTable(
                name: "Refunds");

            migrationBuilder.DropTable(
                name: "ResourceAllocations");

            migrationBuilder.DropTable(
                name: "ResourceServiceRequirements");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "ScheduleExceptions");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "WorkSchedules");

            migrationBuilder.DropTable(
                name: "BookingWindows");

            migrationBuilder.DropTable(
                name: "PaymentAttempts");

            migrationBuilder.DropTable(
                name: "Appointments");

            migrationBuilder.DropTable(
                name: "ClinicResources");

            migrationBuilder.DropTable(
                name: "Services");
        }
    }
}
