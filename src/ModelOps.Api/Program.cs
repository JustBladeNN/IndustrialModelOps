var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=localhost;Port=5432;Database=modelops;Username=modelops;Password=modelops";

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddDbContext<ModelOpsDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddSingleton<IValidationQueue, ValidationQueue>();
builder.Services.AddSingleton<IElementRuleEvaluator, ElementRuleEvaluator>();
builder.Services.AddHostedService<ValidationWorker>();

var app = builder.Build();

app.UseExceptionHandler();
app.MapOpenApi();
app.MapModelOpsEndpoints();

await DatabaseInitializer.InitializeAsync(app.Services, app.Lifetime.ApplicationStopping);

app.Run();

public partial class Program;
