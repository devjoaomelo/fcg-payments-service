using System.Reflection;


var builder = WebApplication.CreateBuilder(args);


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();


var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.MapGet("/", () => new { service = "fcg-payments-service", status = "ok" });
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.MapGet("/version", () => new
{
    service = "fcg-payments-service",
    version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0"
});


app.Run();