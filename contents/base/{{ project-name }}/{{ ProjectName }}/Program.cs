using System.Net;
using {{ ProjectName }};
using {{ ProjectName }}.Services;
{% if persistence ~= 'None' or cache ~= 'None' or messaging ~= 'None' or has_s3 or has_azure_blob %}
using {{ ProjectName }}.Resources;
{% endif %}
{% if persistence ~= 'None' %}
using Microsoft.EntityFrameworkCore;
{% endif %}
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;
using Serilog;
using Serilog.Formatting.Json;

// Configure Serilog early so bootstrap errors are captured.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new JsonFormatter())
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // The platform env contract: PAO injects UPPER_SNAKE variables (GRPC_PORT, MANAGEMENT_PORT,
    // DB_HOST, ...). .NET's default binder matches property names, not those, so map every
    // contract variable that is present onto its Settings key before binding. Property-name env
    // (Port=...) still works — the contract layers on top.
    var platformEnv = new Dictionary<string, string>
    {
        ["HOST"] = "Host",
        ["GRPC_PORT"] = "Port",
        ["MANAGEMENT_PORT"] = "ManagementPort",
        ["DB_HOST"] = "DbHost",
        ["DB_PORT"] = "DbPort",
        ["DB_USERNAME"] = "DbUsername",
        ["DB_PASSWORD"] = "DbPassword",
        ["DB_DBNAME"] = "DbDbname",
        ["CACHE_HOST"] = "CacheHost",
        ["CACHE_PORT"] = "CachePort",
        ["CACHE_USERNAME"] = "CacheUsername",
        ["CACHE_PASSWORD"] = "CachePassword",
        ["MESSAGING_BROKERS"] = "MessagingBrokers",
        ["MESSAGING_BROKER_URL"] = "MessagingBrokerUrl",
        ["MESSAGING_TOPIC"] = "MessagingTopic",
        ["MESSAGING_USERNAME"] = "MessagingUsername",
        ["MESSAGING_PASSWORD"] = "MessagingPassword",
        ["MESSAGING_SASL_MECHANISM"] = "MessagingSaslMechanism",
        ["MESSAGING_JWT_TOKEN"] = "MessagingJwtToken",
        ["MESSAGING_SUBSCRIPTION_NAME"] = "MessagingSubscriptionName",
        ["MESSAGING_ACCESS"] = "MessagingAccess",
        ["S3_ENDPOINT"] = "S3Endpoint",
        ["S3_BUCKET"] = "S3Bucket",
        ["S3_PREFIX"] = "S3Prefix",
        ["S3_ACCESS_KEY"] = "S3AccessKey",
        ["S3_SECRET_KEY"] = "S3SecretKey",
        ["AZURE_ENDPOINT"] = "AzureEndpoint",
        ["AZURE_CONTAINER"] = "AzureContainer",
        ["AZURE_ACCOUNT_NAME"] = "AzureAccountName",
        ["AZURE_ACCOUNT_KEY"] = "AzureAccountKey",
    };
    var contractOverrides = new Dictionary<string, string?>();
    foreach (var (env, key) in platformEnv)
    {
        if (builder.Configuration[env] is { Length: > 0 } value) contractOverrides[key] = value;
    }
    builder.Configuration.AddInMemoryCollection(contractOverrides);

    var settings = builder.Configuration.Get<Settings>() ?? new Settings();

    // Structured logging via Serilog (JSON when LOGGING_STRUCTURED=true)
    builder.Host.UseSerilog((ctx, services, cfg) =>
    {
        var structured = ctx.Configuration["LOGGING_STRUCTURED"]?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;
        if (structured)
            cfg.WriteTo.Console(new JsonFormatter());
        else
            cfg.WriteTo.Console();
        cfg.ReadFrom.Configuration(ctx.Configuration);
    });

    // OpenTelemetry tracing (fail-open: no-op when OTEL_EXPORTER_OTLP_ENDPOINT is absent)
    var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService(
            serviceName: builder.Configuration["OTEL_SERVICE_NAME"] ?? "{{ project-name }}"))
        .WithTracing(t =>
        {
            t.AddAspNetCoreInstrumentation();
            t.AddGrpcClientInstrumentation();
            if (!string.IsNullOrEmpty(otlpEndpoint))
                t.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
        });

    builder.Services.AddGrpc();
    // A baseline liveness check so grpc.health.v1 reports SERVING (not UNKNOWN) — required by
    // k8s gRPC probes and anything else that gates on the standard health protocol.
    builder.Services.AddGrpcHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy());
    // Server reflection: schema discovery for dynamic clients (grpcurl, prova, GUIs).
    builder.Services.AddGrpcReflection();

{% if persistence ~= 'None' %}
    if (!builder.Environment.IsEnvironment("Testing"))
        builder.Services.AddPersistence(settings);
{% endif %}
{% if cache ~= 'None' %}
    if (!builder.Environment.IsEnvironment("Testing"))
        builder.Services.AddCache(settings);
{% endif %}
{% if messaging ~= 'None' %}
    if (!builder.Environment.IsEnvironment("Testing"))
        builder.Services.AddMessaging(settings);
{% endif %}
{% if has_s3 %}
    if (!builder.Environment.IsEnvironment("Testing"))
        builder.Services.AddStorageS3(settings);
{% endif %}
{% if has_azure_blob %}
    if (!builder.Environment.IsEnvironment("Testing"))
        builder.Services.AddStorageAzure(settings);
{% endif %}

    // Kestrel: gRPC on HTTP/2 at service_port, management (HTTP/1) at management_port.
    // Skipped in Testing so the test HTTP client uses a single in-process server.
    if (!builder.Environment.IsEnvironment("Testing"))
    {
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Any, settings.Port,
                o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
            options.Listen(IPAddress.Any, settings.ManagementPort,
                o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
        });
    }

    var app = builder.Build();

    // gRPC service and gRPC health check protocol on service_port
    app.MapGrpcService<{{ EntityName }}ServiceImpl>();
    app.MapGrpcHealthChecksService();
    app.MapGrpcReflectionService();

    // HTTP management endpoints: health + Prometheus metrics on management_port
    app.MapGet("/health/readiness", () => Results.Ok(new { status = "ok" }));
    app.MapGet("/health/liveness", () => Results.Ok(new { status = "ok" }));
    app.UseMetricServer(settings.ManagementPort); // prometheus-net: GET /metrics on management_port
{% if persistence ~= 'None' %}

    // Sample scaffold: create the schema for the Item entity (Domain/Item.cs). Replace with real
    // migrations as your domain model solidifies.
    if (!builder.Environment.IsEnvironment("Testing"))
    {
        using (var scope = app.Services.CreateScope())
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
    }
{% endif %}

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

return 0;

public partial class Program { }
