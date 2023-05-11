using Azure.Storage.Blobs;
using System.Reflection;
using Azure.Storage.Queues;
using Microsoft.EntityFrameworkCore;
using HW6NoteKeeperSolution;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Configure DbContext with connection string from appsettings.json file
var telemetryConfiguration = TelemetryConfiguration.CreateDefault();
telemetryConfiguration.ConnectionString = builder.Configuration.GetConnectionString("Telemetry");
var telemetryClient = new TelemetryClient(telemetryConfiguration);
var queueClient = builder.Configuration.GetSection("NotesSettings")["useAzureWebJob"] == "no" ?
    new QueueClient(builder.Configuration.GetConnectionString("Storage"), "attachment-zip-requests-wj") :
    new QueueClient(builder.Configuration.GetConnectionString("Storage"), "attachment-zip-requests");
builder.Services.AddSingleton(telemetryClient);
builder.Services.AddSingleton(queueClient);
builder.Services.AddDbContext<NoteContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SQL")));
var notesSettings = new NotesSettings();
builder.Configuration.GetSection(nameof(notesSettings)).Bind(notesSettings);
builder.Services.AddSingleton(implementationInstance: notesSettings);
builder.Services.AddSingleton(implementationInstance: notesSettings);
var blobServiceClient = new BlobServiceClient(builder.Configuration.GetConnectionString("Storage"));
builder.Services.AddSingleton(implementationInstance: blobServiceClient);
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory,
        $"{Assembly.GetExecutingAssembly().GetName().Name}.xml"));
    // Add nice title
    c.SwaggerDoc("v1",
        new OpenApiInfo
        {
            Title = "Customer Custom Validation Demo API",
            Version = "v1",
            Description = "Simple, non thread safe, Customer Custom Validation API Mgmt example"
        });

    // DEMO: API Management Ensure unique IDs for all APIs
    c.CustomOperationIds(apiDesc => apiDesc.TryGetMethodInfo(out MethodInfo methodInfo) ?
        methodInfo.Name : null);

    // Add documentation via C# XML Comments
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);

    // Note: Don't forget to add this to the csproj file (removing /* and */)
    /*
      	<!-- DEMO: Enable documentation file so API has user defined documentation via XML comments -->
	    <PropertyGroup>
		    <GenerateDocumentationFile>true</GenerateDocumentationFile>

		<!-- Disable warning: Missing XML comment for publicly visible type or member 'Type_or_Member'-->
		<NoWarn>$(NoWarn);1591</NoWarn>
	    </PropertyGroup>      
    */
});
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

// Get the dependency injection for creating services, from EFCoreDemoSolution
using (var scope = app.Services.CreateScope())
{
    // Get the service provider so services can be called
    var services = scope.ServiceProvider;
    try
    {
        // Get the database context service
        var context = services.GetRequiredService<NoteContext>();
        var blobServiceClientContext = services.GetRequiredService<BlobServiceClient>();
        // Initialize the data
        DbInitializer.Initialize(context, blobServiceClientContext);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "*** ERROR *** An error occurred while seeding the database. *** ERROR ***");
    }
}

// Configure the HTTP request pipeline.

// Code Note: Moved outside of env.IsDevelopment() so both 
// Debug and Release are supported
app.UseSwagger(c =>
{
    //�DEMO:�API�Management�Host�Name�requirement 
    //�Modify�Swagger�with�Request�Context�to�include�host�information 
    // Allows API Management to import the OpenAPI Definition
    c.PreSerializeFilters.Add((swagger, httpReq) =>
    {
        swagger.Servers =
            new List<OpenApiServer>{
                new() {Url = $"{httpReq.Scheme}://{httpReq.Host.Value}"}
            };
    });
});// Make Swagger load at the base URL
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
    c.RoutePrefix = "";
    
});
app.UseCors();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();