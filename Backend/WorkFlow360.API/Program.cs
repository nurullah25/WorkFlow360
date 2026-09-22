using System.Text.Json.Serialization;
using Serilog;
using WorkFlow360.API.Extensions;
using WorkFlow360.API.Filters;
using WorkFlow360.API.Middleware;
using WorkFlow360.API.Services;
using WorkFlow360.Application;
using WorkFlow360.Application.Attendance;
using WorkFlow360.Application.Common;
using WorkFlow360.Infrastructure;
using WorkFlow360.Infrastructure.Persistence.Seed;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, config) => config
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services));

    builder.Services.Configure<CompanyOptions>(builder.Configuration.GetSection(CompanyOptions.SectionName));
    builder.Services.Configure<AttendanceOptions>(builder.Configuration.GetSection(AttendanceOptions.SectionName));
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, CurrentUser>();

    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddAppRateLimiting();

    builder.Services.AddControllers(options => options.Filters.Add<ValidationFilter>())
        .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddSwaggerWithJwt();
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    app.UseExceptionHandler();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
        await DevDataSeeder.SeedAsync(app.Services);
    }
    else
    {
        app.UseHsts();
        app.UseHttpsRedirection();
    }

    app.UseSerilogRequestLogging();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health").AllowAnonymous();

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "WorkFlow360 API terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
