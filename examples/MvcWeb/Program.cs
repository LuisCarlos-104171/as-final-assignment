using Microsoft.EntityFrameworkCore;
using MvcWeb;
using Piranha;
using Piranha.AspNetCore.Identity.SQLite;
using Piranha.AttributeBuilder;
using Piranha.Data.EF.SQLite;
using Piranha.Manager.Editor;
using MvcWeb.Models;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Configure OpenTelemetry
var serviceName = "MvcWeb";
var serviceVersion = "1.0.0";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = builder.Environment.EnvironmentName
        }))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(options =>
        {
            options.RecordException = true;
        })
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation(options =>
        {
            options.SetDbStatementForText = true;
        })
        .AddJaegerExporter(options =>
        {
            options.AgentHost = builder.Configuration.GetValue<string>("Jaeger:AgentHost") ?? "localhost";
            options.AgentPort = builder.Configuration.GetValue<int>("Jaeger:AgentPort", 6831);
        }))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddPrometheusExporter());

builder.AddPiranha(options =>
{
    /**
     * This will enable automatic reload of .cshtml
     * without restarting the application. However since
     * this adds a slight overhead it should not be
     * enabled in production.
     */
    options.AddRazorRuntimeCompilation = true;

    options.UseCms();
    options.UseManager();

    options.UseFileStorage(naming: Piranha.Local.FileStorageNaming.UniqueFolderNames);
    options.UseImageSharp();
    options.UseTinyMCE();
    options.UseMemoryCache();

    var connectionString = builder.Configuration.GetConnectionString("piranha");
    options.UseEF<SQLiteDb>(db => db.UseSqlite(connectionString));
    options.UseIdentityWithSeed<IdentitySQLiteDb>(db => db.UseSqlite(connectionString));

    // Add our article database context
    builder.Services.AddDbContext<ArticleDbContext>(options => 
        options.UseSqlite(connectionString));
        
    // Register our custom repository
    builder.Services.AddScoped<ArticleSubmissionRepository>();
    
    // Configure the different permissions for securing content in the application
    options.UseSecurity(o =>
    {
        o.UsePermission("WebUser", "Web User");
        
        // Register workflow permissions for policy-based authorization
        foreach (var permission in Piranha.Manager.WorkflowPermissions.All())
        {
            o.UsePermission(permission);
        }
    });

    /**
     * Here you can specify the login url for the front end
     * application. This does not affect the login url of
     * the manager interface.
    options.LoginUrl = "login";
     */
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UsePiranha(options =>
{
    // Initialize Piranha
    App.Init(options.Api);

    // Build content types
    new ContentTypeBuilder(options.Api)
        .AddAssembly(typeof(Program).Assembly)
        .Build()
        .DeleteOrphans();

    // Configure Tiny MCE
    EditorConfig.FromFile("editorconfig.json");

    options.UseManager();
    options.UseTinyMCE();
    options.UseIdentity();
    
    // Seed the application with example data
    Seed.RunAsync(options.Api).GetAwaiter().GetResult();
});

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var articleDbContext = scope.ServiceProvider.GetService<ArticleDbContext>();
    articleDbContext?.Database.EnsureCreated();
}

// Map Prometheus metrics endpoint
app.MapPrometheusScrapingEndpoint();

app.Run();