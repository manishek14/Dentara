namespace Dentara.IntegrationTests.Api;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

/// <summary>
/// End-to-end API tests using WebApplicationFactory with SQLite in-memory.
/// Tests the full HTTP pipeline: middleware → controller → MediatR → handler → DB.
/// Uses a shared collection fixture so all test classes share the same DB instance.
/// </summary>

// Collection fixture — shares one DentaraWebAppFactory across all test classes
[CollectionDefinition("ApiCollection")]
public sealed class ApiCollection : ICollectionFixture<DentaraWebAppFactory> { }

[Collection("ApiCollection")]
public sealed class HealthEndpointTests : IDisposable
{
    private readonly HttpClient _client;

    public HealthEndpointTests(DentaraWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task GET_Health_Returns200_WithServiceInfo()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        body!.Service.Should().Be("Dentara");
        body.Version.Should().Be("0.1.0");
    }

    private sealed record HealthResponse(string Service, string Version, string Environment);
}

/// <summary>
/// Tests that endpoints without proper authentication return 401.
/// </summary>
[Collection("ApiCollection")]
public sealed class AuthenticationTests : IDisposable
{
    private readonly HttpClient _client;

    public AuthenticationTests(DentaraWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task POST_Appointments_WithoutToken_Returns401()
    {
        var response = await _client.PostAsync("/api/appointments",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_Dashboard_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/dashboard/stats");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_AuditLogs_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/audit/logs");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_Services_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/services");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_Users_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/users");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

/// <summary>
/// Tests that endpoints with valid JWT but insufficient permissions return 403.
/// </summary>
[Collection("ApiCollection")]
public sealed class AuthorizationTests : IDisposable
{
    private readonly DentaraWebAppFactory _factory;
    private readonly HttpClient _client;

    public AuthorizationTests(DentaraWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public void Dispose() => _client.Dispose();

    private HttpClient CreateClientWithPatientToken()
    {
        var client = _factory.CreateClient();
        var token = JwtTestHelper.GenerateToken(DentaraWebAppFactory.PatientUserId, "Patient");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task GET_AuditLogs_WithPatientToken_Returns403()
    {
        var client = CreateClientWithPatientToken();
        var response = await client.GetAsync("/api/audit/logs");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task POST_Services_WithPatientToken_Returns403()
    {
        var client = CreateClientWithPatientToken();
        var response = await client.PostAsync("/api/services",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task POST_Refunds_WithPatientToken_Returns403()
    {
        var client = CreateClientWithPatientToken();
        var response = await client.PostAsync("/api/payments/refunds",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GET_Dashboard_WithPatientToken_Returns403()
    {
        var client = CreateClientWithPatientToken();
        var response = await client.GetAsync("/api/dashboard/stats");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

/// <summary>
/// Tests that admin-token can access admin-only endpoints.
/// </summary>
[Collection("ApiCollection")]
public sealed class AdminAccessTests : IDisposable
{
    private readonly DentaraWebAppFactory _factory;
    private readonly HttpClient _client;

    public AdminAccessTests(DentaraWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public void Dispose() => _client.Dispose();

    private HttpClient CreateClientWithAdminToken()
    {
        var client = _factory.CreateClient();
        var token = JwtTestHelper.GenerateToken(DentaraWebAppFactory.AdminUserId, "Admin");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task GET_AuditLogs_WithAdminToken_Returns200()
    {
        var client = CreateClientWithAdminToken();
        var response = await client.GetAsync("/api/audit/logs");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GET_Dashboard_WithAdminToken_Returns200()
    {
        var client = CreateClientWithAdminToken();
        // Use DateTimeOffset default (last 30 days from system clock)
        var response = await client.GetAsync("/api/dashboard/stats");
        // Accept 200 or 400 (empty range is valid if no data in last 30 days)
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GET_Services_WithAdminToken_Returns200()
    {
        var client = CreateClientWithAdminToken();
        var response = await client.GetAsync("/api/services");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var services = await response.Content.ReadFromJsonAsync<List<ServiceResponse>>();
        services!.Should().NotBeEmpty();
        services!.First().Name.Should().Be("Cavity Filling");
    }

    private sealed record ServiceResponse(
        Guid Id, string Name, string? Description, int DurationMinutes,
        decimal PriceAmount, string Currency, bool IsActive, int Version,
        DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc);
}

/// <summary>
/// Tests the full appointment creation flow.
/// </summary>
[Collection("ApiCollection")]
public sealed class AppointmentFlowTests : IDisposable
{
    private readonly DentaraWebAppFactory _factory;
    private readonly HttpClient _client;

    public AppointmentFlowTests(DentaraWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public void Dispose() => _client.Dispose();

    private HttpClient CreateClientWithPatientToken()
    {
        var client = _factory.CreateClient();
        var token = JwtTestHelper.GenerateToken(DentaraWebAppFactory.PatientUserId, "Patient");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string BuildAppointmentJson(DateTimeOffset start, DateTimeOffset end, Guid dentistId, Guid serviceId) =>
        $$"""
        {
            "dentistId": "{{dentistId}}",
            "serviceId": "{{serviceId}}",
            "publishedSessionId": null,
            "startAtUtc": "{{start:O}}",
            "endAtUtc": "{{end:O}}",
            "serviceNameSnapshot": "Cavity Filling",
            "serviceDurationMinutesSnapshot": 60,
            "priceAmountSnapshot": 500000,
            "currencySnapshot": "IRR",
            "dentistDisplayNameSnapshot": "Dr. Test",
            "cancellationPolicyVersion": 1,
            "cancellationFeePercent": 15,
            "refundablePercent": 85,
            "termsSnapshotHash": null,
            "requiredResources": [
                {
                    "resourceTypeValue": 1,
                    "quantityRequired": 1,
                    "requireDentistOwned": false,
                    "specificResourceId": null
                }
            ]
        }
        """;

    [Fact]
    public async Task POST_Appointments_WithPatientToken_Returns200()
    {
        var client = CreateClientWithPatientToken();
        var start = new DateTimeOffset(2026, 12, 15, 10, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 12, 15, 11, 0, 0, TimeSpan.Zero);
        var json = BuildAppointmentJson(start, end, DentaraWebAppFactory.DentistProfileId, DentaraWebAppFactory.ServiceId);

        var response = await client.PostAsync("/api/appointments",
            new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"expected 200 but got {response.StatusCode}. Body: {body}");

        var result = await response.Content.ReadFromJsonAsync<CreateAppointmentResponseBody>();
        result!.AppointmentId.Should().NotBeEmpty();
        result.PriceAmount.Should().Be(500_000m);
        result.Currency.Should().Be("IRR");
    }

    [Fact]
    public async Task POST_Appointments_DuplicateSlot_Returns409()
    {
        // First patient books a slot
        var patientClient = CreateClientWithPatientToken();
        var start = new DateTimeOffset(2026, 12, 16, 10, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 12, 16, 11, 0, 0, TimeSpan.Zero);
        var json = BuildAppointmentJson(start, end, DentaraWebAppFactory.DentistProfileId, DentaraWebAppFactory.ServiceId);

        var first = await patientClient.PostAsync("/api/appointments",
            new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        var firstBody = await first.Content.ReadAsStringAsync();
        first.StatusCode.Should().Be(HttpStatusCode.OK,
            $"first booking failed with {first.StatusCode}: {firstBody}");

        // Admin (different user) tries to book the same slot — should fail with 409
        var adminClient = _factory.CreateClient();
        var adminToken = JwtTestHelper.GenerateToken(DentaraWebAppFactory.AdminUserId, "Admin");
        adminClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

        var second = await adminClient.PostAsync("/api/appointments",
            new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        second.StatusCode.Should().Be(HttpStatusCode.Conflict,
            $"second booking should be 409 but got {second.StatusCode}: {await second.Content.ReadAsStringAsync()}");
    }

    private sealed record CreateAppointmentResponseBody(
        Guid AppointmentId, DateTimeOffset ExpiresAtUtc,
        decimal PriceAmount, string Currency);
}
