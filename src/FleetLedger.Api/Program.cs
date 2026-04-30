using FleetLedger.Application;
using FleetLedger.Application.Handlers;
using FleetLedger.Api.Middleware;
using FleetLedger.Api.Validators;
using FleetLedger.Infrastructure;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' not found.");

builder.Services.AddDbContext<FleetLedgerDbContext>(opts =>
    opts.UseNpgsql(connectionString));

builder.Services.AddControllers();

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CreateDriverRequestValidator>();

builder.Services.AddScoped<IDepotRepository, DepotRepository>();
builder.Services.AddScoped<DepotHandler>();
builder.Services.AddScoped<IDriverRepository, DriverRepository>();
builder.Services.AddScoped<DriverHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FleetLedger API",
        Description = "API REST de gestión de flotas vehiculares con Event Sourcing + CQRS",
        Version = "v1"
    });
});

var app = builder.Build();

app.UseDomainExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "FleetLedger API v1");
    c.RoutePrefix = string.Empty;
});

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow
}))
.WithName("Health Check")
.WithTags("Health");

app.Run();