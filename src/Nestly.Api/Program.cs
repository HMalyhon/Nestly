using System.Text.Json.Serialization;
using Nestly.Api.Infrastructure;
using Nestly.Search;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNestlySearch(builder.Configuration);

builder.Services
    .AddControllers()

    // Enums cross the wire as names: a client sending {"sort": 1} is one reorder away from
    // meaning something else.
    .AddJsonOptions(json => json.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.Configure<CorsOptions>(builder.Configuration.GetSection(CorsOptions.SectionName));

builder.Services.AddCors(cors => cors.AddPolicy(
    CorsOptions.PolicyName,
    policy =>
    {
        var origins = builder.Configuration
            .GetSection(CorsOptions.SectionName)
            .Get<CorsOptions>()?.AllowedOrigins ?? [];

        policy.WithOrigins([.. origins]).AllowAnyHeader().AllowAnyMethod();
    }));

builder.Services.AddHealthChecks()
    .AddCheck<ElasticsearchHealthCheck>("elasticsearch", tags: ["ready"]);

// One error shape for everything, including failures the framework raises.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<SearchExceptionHandler>();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapOpenApi();
app.MapScalarApiReference();

app.UseCors(CorsOptions.PolicyName);

app.MapControllers();
app.MapHealthChecks("/health");

await app.RunAsync();
