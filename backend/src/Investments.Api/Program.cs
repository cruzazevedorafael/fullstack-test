using System.Text.Json.Serialization;
using Investments.Api.Data;
using Investments.Api.Errors;
using Investments.Api.Services;
using Investments.Api.Services.Notifications;
using Investments.Api.Time;
using Investments.Domain.Calculations;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddDbContext<InvestmentsDbContext>(options =>
    options.UseSqlite(configuration.GetConnectionString("Default")));

builder.Services.AddSingleton(configuration.GetSection("InvestmentRules").Get<InvestmentRules>() ?? InvestmentRules.Default);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(TimeZoneInfo.FindSystemTimeZoneById(configuration["BusinessTimeZone"] ?? "America/Sao_Paulo"));
builder.Services.AddSingleton<IBusinessCalendar, BusinessCalendar>();

builder.Services.AddScoped<OwnerService>();
builder.Services.AddScoped<InvestmentService>();
builder.Services.AddScoped<IInvestmentNotifier, InvestmentNotifier>();

builder.Services.Configure<SmtpOptions>(configuration.GetSection("Smtp"));
if (string.IsNullOrWhiteSpace(configuration["Smtp:Host"]))
{
    builder.Services.AddSingleton<IEmailSender, LoggingEmailSender>();
}
else
{
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
}

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();

// The documentation is part of the deliverable, so it is exposed in every environment.
app.MapOpenApi();
app.MapScalarApiReference("/docs", options => options.WithTitle("Investments API"));
app.MapControllers();

// Good enough for a single instance on SQLite; a real deployment would run migrations as a separate step.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<InvestmentsDbContext>().Database.Migrate();
}

app.Run();
