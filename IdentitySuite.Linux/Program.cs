
using IdentitySuite;

var builder = WebApplication.CreateBuilder(args);

// Registers all required services (authentication, authorization, Blazor, OpenIddict, etc.)
builder.AddIdentitySuiteServices();

var app = builder.Build();

// Creates or migrates the database based on the current configuration
await app.SetupIdentitySuiteDbAsync();

// Enables all runtime middleware (authentication, routing, Blazor, etc.)
app.UseIdentitySuiteServices();

await app.RunAsync();
