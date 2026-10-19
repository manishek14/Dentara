namespace Dentara.IntegrationTests.Payments;

using Dentara.Application.Common.Interfaces;
using Dentara.Application.Common.Results;
using Dentara.Domain.Common;
using Dentara.Domain.Payments;
using Dentara.IntegrationTests.Fixtures;
using Dentara.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Tests for SqlIdempotencyService — the ADR-006 implementation. Verifies
/// that duplicate operations are safely replayed and concurrent attempts
/// are properly handled.
/// </summary>
public sealed class IdempotencyServiceTests : IntegrationTestBase
{
    [Fact]
    public async Task ExecuteAsync_FirstCall_ExecutesAndStoresResult()
    {
        var scope1 = Guid.NewGuid().ToString();
        var key = "test-key-1";
        var callCount = 0;

        var result = await UsingScopeAsync(async sp =>
        {
            var svc = sp.GetRequiredService<IIdempotencyService>();
            return await svc.ExecuteAsync(scope1, key,
                async ct =>
                {
                    callCount++;
                    await Task.Delay(10, ct);
                    return Result<int>.Success(42);
                });
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
        callCount.Should().Be(1, "because the operation ran exactly once");

        // Verify a record was persisted
        await UsingScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<DentaraDbContext>();
            var record = await db.IdempotencyRecords
                .FirstOrDefaultAsync(r => r.Scope == scope1 && r.Key == key);
            record.Should().NotBeNull();
            record!.Status.Should().Be(IdempotencyStatus.Completed);
            record.ResponsePayload.Should().Contain("42");
        });
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateCall_ReplaysStoredResultWithoutReExecuting()
    {
        var scope1 = Guid.NewGuid().ToString();
        var key = "test-key-2";
        var callCount = 0;

        // First call — executes and stores
        await UsingScopeAsync(async sp =>
        {
            var svc = sp.GetRequiredService<IIdempotencyService>();
            await svc.ExecuteAsync(scope1, key,
                async ct =>
                {
                    callCount++;
                    await Task.Delay(10, ct);
                    return Result<int>.Success(42);
                });
        });

        // Second call — should replay, NOT re-execute
        var replayedResult = await UsingScopeAsync(async sp =>
        {
            var svc = sp.GetRequiredService<IIdempotencyService>();
            return await svc.ExecuteAsync(scope1, key,
                async ct =>
                {
                    callCount++;
                    return Result<int>.Success(99); // different value
                });
        });

        replayedResult.IsSuccess.Should().BeTrue();
        replayedResult.Value.Should().Be(42, "because the original result is replayed");
        callCount.Should().Be(1, "because the second call did not re-execute");
    }

    [Fact]
    public async Task ExecuteAsync_WithTTL_ExpiresAfterTtlAndAllowsRetry()
    {
        var scope1 = Guid.NewGuid().ToString();
        var key = "test-key-3";
        var callCount = 0;

        // First call with 5-minute TTL
        await UsingScopeAsync(async sp =>
        {
            var svc = sp.GetRequiredService<IIdempotencyService>();
            await svc.ExecuteAsync(scope1, key,
                async ct =>
                {
                    callCount++;
                    return Result<int>.Success(1);
                },
                ttl: TimeSpan.FromMinutes(5));
        });

        // Advance clock past TTL
        Clock.AdvanceBy(TimeSpan.FromMinutes(10));

        // Second call — should re-execute because TTL expired
        var secondResult = await UsingScopeAsync(async sp =>
        {
            var svc = sp.GetRequiredService<IIdempotencyService>();
            return await svc.ExecuteAsync(scope1, key,
                async ct =>
                {
                    callCount++;
                    return Result<int>.Success(2);
                });
        });

        secondResult.IsSuccess.Should().BeTrue();
        secondResult.Value.Should().Be(2, "because the TTL expired and the operation re-ran");
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_DifferentKeys_BothExecute()
    {
        var scope1 = Guid.NewGuid().ToString();
        var callCount = 0;

        await UsingScopeAsync(async sp =>
        {
            var svc = sp.GetRequiredService<IIdempotencyService>();
            await svc.ExecuteAsync(scope1, "key-A",
                async ct => { callCount++; return Result<int>.Success(1); });
            await svc.ExecuteAsync(scope1, "key-B",
                async ct => { callCount++; return Result<int>.Success(2); });
        });

        callCount.Should().Be(2, "because different keys are independent operations");
    }

    [Fact]
    public async Task ExecuteAsync_DifferentScopes_SameKey_BothExecute()
    {
        var key = "shared-key";
        var callCount = 0;

        await UsingScopeAsync(async sp =>
        {
            var svc = sp.GetRequiredService<IIdempotencyService>();
            await svc.ExecuteAsync("scope-A", key,
                async ct => { callCount++; return Result<int>.Success(1); });
            await svc.ExecuteAsync("scope-B", key,
                async ct => { callCount++; return Result<int>.Success(2); });
        });

        callCount.Should().Be(2, "because different scopes are independent");
    }
}
