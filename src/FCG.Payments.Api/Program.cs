using Amazon;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using FCG.Payments.Application.Interfaces;
using FCG.Payments.Application.UseCases.Confirm;
using FCG.Payments.Application.UseCases.Create;
using FCG.Payments.Application.UseCases.Get;
using FCG.Payments.Application.UseCases.List;
using FCG.Payments.Domain.Interfaces;
using FCG.Payments.Infra.Clients;
using FCG.Payments.Infra.Data;
using FCG.Payments.Infra.Messaging;
using FCG.Payments.Infra.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

#region AWS SNS
builder.Services.AddSingleton<IAmazonSimpleNotificationService>(_ =>
{
    var region = builder.Configuration["AWS:Region"] ?? "us-east-2";
    return new AmazonSimpleNotificationServiceClient(RegionEndpoint.GetBySystemName(region));
});
builder.Services.AddSingleton<INotificationPublisher, SnsNotificationPublisher>();

builder.Services.AddSingleton<IAmazonSQS>(_ =>
{
    var region = builder.Configuration["AWS:Region"] ?? "us-east-2";
    return new AmazonSQSClient(RegionEndpoint.GetBySystemName(region));
});
#endregion

builder.Services.AddScoped<IEventStore, EventStoreEf>();
builder.Services.AddScoped<IMessageBus, SqsMessageBus>();
builder.Services.AddScoped<IGamesCatalogClient, GamesCatalogClient>();
builder.Services.AddScoped<IPaymentRepository, MySqlPaymentRepository>();

// Handlers
builder.Services.AddScoped<CreatePaymentHandler>();
builder.Services.AddScoped<ConfirmPaymentHandler>();
builder.Services.AddScoped<GetPaymentHandler>();
builder.Services.AddScoped<ListPaymentsHandler>();

// DbContext + Repo
builder.Services.AddDbContext<PaymentsDbContext>(opt =>
{
    var cs = builder.Configuration.GetConnectionString("PaymentsDb")
        ?? Environment.GetEnvironmentVariable("ConnectionStrings__PaymentsDb")
        ?? "Server=127.0.0.1;Port=3319;Database=fcg_payments;User=fcg;Password=fcgpwd;SslMode=None;Connect Timeout=5";
    opt.UseMySql(cs, ServerVersion.AutoDetect(cs));
});
builder.Services.AddScoped<IPaymentRepository, MySqlPaymentRepository>();
builder.Services.AddHttpClient<IGamesCatalogClient, GamesCatalogClient>(c =>
{
    // Em dev local use http://localhost:8082 ; em Docker use http://fcg-games:8080
    c.BaseAddress = new Uri(builder.Configuration["GamesApi:BaseUrl"] ?? "http://fcg-games:8080");
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "FCG Payments API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Cole APENAS o token (sem 'Bearer ')"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement{
        { new OpenApiSecurityScheme{
            Reference = new OpenApiReference{ Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }, Array.Empty<string>() }
    });
});

// JWT
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "fcg-users";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "fcg-clients";
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key not configured");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.RequireHttpsMetadata = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

#region AWS SQS
builder.Services.AddSingleton<IAmazonSQS>(_ =>
{
    var region = builder.Configuration["AWS:Region"] ?? "us-east-2";
    return new AmazonSQSClient(RegionEndpoint.GetBySystemName(region));
});
#endregion

builder.Services.AddSingleton<IMessageBus, SqsMessageBus>();


var app = builder.Build();

/*
 * if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
 */

var enableSwagger = builder.Configuration.GetValue<bool>("Swagger:EnableUI", false);
if (enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("v1/swagger.json", "FCG API v1");
        c.RoutePrefix = "swagger";
    });
}


// Health
app.MapGet("/", () => new { service = "fcg-payments-service", status = "ok" }).WithTags("Health");
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" })).WithTags("Health");
app.MapGet("/version", () => new { service = "fcg-payments-service", version = "1.0.0" }).WithTags("Health");

// Users (autenticado)
app.MapPost("/api/payments", async (
    CreatePaymentRequest req,
    ClaimsPrincipal user,
    IConfiguration cfg,
    CreatePaymentHandler handler,
    CancellationToken ct) =>
{
    var queueUrl = cfg["Queues:PaymentsRequested"];
    if(string.IsNullOrWhiteSpace(queueUrl))
        throw new InvalidOperationException("Queue URL not configured (PaymentsRequested).");
    var res = await handler.Handle(req, user, queueUrl!, ct);
    return Results.Created($"/api/payments/{res.Id}", res);
})
.WithTags("Payments")
.WithSummary("Cria um pagamento pendente com valor do Games")
.RequireAuthorization();

app.MapGet("/api/payments/{id:guid}", async (
    Guid id,
    GetPaymentHandler handler,
    ClaimsPrincipal user,
    CancellationToken ct) =>
{
    var res = await handler.Handle(id, user, ct);
    return res is null ? Results.Forbid() : Results.Ok(res);
})
.WithTags("User Payments")
.WithSummary("Obtem pagamento por Id")
.RequireAuthorization();

// Admin
app.MapPost("/api/payments/{id:guid}/confirm", async (
    Guid id,
    ConfirmPaymentHandler handler,
    CancellationToken ct) =>
{
    var ok = await handler.Handle(new ConfirmPaymentRequest(id), ct);
    return ok ? Results.NoContent() : Results.NotFound();
})
.WithTags("Admin Payments")
.WithSummary("Confirma pagamento por Id")
.RequireAuthorization("AdminOnly");

app.MapGet("/api/payments", async (
    int page, int size,
    ListPaymentsHandler handler,
    CancellationToken ct) =>
{
    var res = await handler.Handle(page, size, ct);
    return Results.Ok(res);
})
.WithTags("Admin Payments")
.WithSummary("Lista todos os pagamentos")
.RequireAuthorization("AdminOnly");

app.MapPost("/internal/payments/{id}/confirm", async (
    Guid id,
    HttpRequest request,
    IConfiguration config,
    ConfirmPaymentHandler handler,
    CancellationToken ct) =>
{
    var token = request.Headers["X-Internal-Token"].FirstOrDefault();
    var expected = config["InternalAuth:Token"];

    if (token != expected)
        return Results.Unauthorized();

    var ok = await handler.Handle(new ConfirmPaymentRequest(id), ct);
    return ok ? Results.NoContent() : Results.NotFound();
});

using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        await db.Database.MigrateAsync();
    }
    catch
    {
        // TODO: logar erro de migration
    }
}

app.Run();
