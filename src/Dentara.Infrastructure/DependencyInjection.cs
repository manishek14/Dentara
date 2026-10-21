namespace Dentara.Infrastructure;

using Dentara.Application.Common.Interfaces;
using Dentara.Domain.Common;
using Dentara.Domain.Identity;
using Dentara.Domain.Booking;
using Dentara.Domain.Payments;
using Dentara.Domain.Scheduling;
using Dentara.Domain.Catalog;
using Dentara.Domain.Services.Booking;
using Dentara.Domain.Services.Payments;
using Dentara.Domain.Services.Scheduling;
using Dentara.Infrastructure.BackgroundJobs;
using Dentara.Infrastructure.Booking;
using Dentara.Infrastructure.Clock;
using Dentara.Infrastructure.Payments;
using Dentara.Infrastructure.Persistence;
using Dentara.Infrastructure.Scheduling;
using Dentara.Infrastructure.Waitlist;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Top-level DI registration for the Infrastructure layer.
/// Api layer calls AddInfrastructure(configuration) from Program.cs.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Persistence (DbContext + repositories + UoW + dispatcher)
        services.AddPersistence(configuration);

        // Clock
        services.AddSingleton<IClock, Clock.SystemClock>();

        // Unit of Work — scoped per request
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // Generic repositories for each aggregate
        services.AddScoped(typeof(IRepository<>), typeof(EfCoreRepository<>));

        // Domain event dispatcher
        services.AddScoped<IDomainEventDispatcher, MediatrDomainEventDispatcher>();

        // Idempotency service
        services.AddScoped<IIdempotencyService, SqlIdempotencyService>();

        // Domain services
        services.AddScoped<IBookingTransactionService, SqlBookingTransactionService>();
        services.AddScoped<ICancellationService, SqlCancellationService>();
        services.AddScoped<WaitlistProcessor>();

        // Gateway adapters (sandbox for dev; swap to real adapters in production)
        services.AddScoped<IPaymentGateway, Gateways.Payments.SandboxPaymentGateway>();
        services.AddScoped<ISmsGateway, Gateways.Sms.SandboxSmsGateway>();

        // Identity services
        services.AddScoped<IJwtTokenService, Identity.JwtTokenService>();
        services.AddScoped<Domain.Services.Identity.IOtpService, Identity.SqlOtpService>();
        services.AddSingleton<IOtpStore, Identity.InMemoryOtpStore>();
        services.AddSingleton<IOtpHasher, Identity.Sha256OtpHasher>();
        services.AddSingleton<IOtpGenerator, Identity.CryptoOtpGenerator>();
        services.AddScoped<ICurrentUserContext, Identity.NoOpCurrentUserContext>();

        // Payment verification service (used by the ExpiryWorker per ADR-004)
        services.AddScoped<IPaymentVerificationService, StubPaymentVerificationService>();

        // Scheduling publication service (used by the PublicationWorker)
        services.AddScoped<IPublicationService, SqlPublicationService>();

        // Background workers — registered as hosted services (singletons).
        // Each worker creates its own DI scope per iteration so scoped services
        // (DbContext, repositories) are fresh.
        services.AddHostedService<AppointmentExpiryWorker>();
        services.AddHostedService<BookingWindowPublicationWorker>();
        services.AddHostedService<IdempotencyCleanupWorker>();
        services.AddHostedService<ReminderSchedulingWorker>();
        services.AddHostedService<NotificationDispatcherWorker>();
        services.AddHostedService<WaitlistOfferExpiryWorker>();

        return services;
    }
}
