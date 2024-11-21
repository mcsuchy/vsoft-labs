// <snippet_all>
using NSwag.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Kubernetes;

using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Messaging.ServiceBus;

var builder = WebApplication.CreateBuilder(args);

// Kolejność ma znaczenie - zmienne środowiskowe powinny być ostatnie aby nadpisać 
// wcześniejsze konfiguracje
builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();    // Ostatni w kolejności, aby miał najwyższy priorytet

builder.Services.AddDbContext<TodoDb>(opt => opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddEndpointsApiExplorer();

var keyVaultUri = new Uri($"https://{builder.Configuration["KeyVaultName"]}.vault.azure.net/");
var secretClient = new SecretClient(keyVaultUri, new DefaultAzureCredential());
var serviceBusConnection = secretClient.GetSecret("ServiceBusConnection").Value.Value;

builder.Services.AddSingleton(new ServiceBusClient(serviceBusConnection));

builder.Services.AddOpenApiDocument(config =>
{
    config.DocumentName = "TodoAPI";
    config.Title = "TodoAPI v1";
    config.Version = "v1";
});

builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.EnableAdaptiveSampling = false; // Wyłączenie adaptacyjnego samplingu
    options.EnableDependencyTrackingTelemetryModule = true; // Distributed tracing
    options.EnablePerformanceCounterCollectionModule = true; // Metryki wydajności
    options.EnableAppServicesHeartbeatTelemetryModule = true; // Heartbeat
    options.EnableDebugLogger = true; // Debugowanie w konsoli (dla deweloperów)
});

// Dodanie Kubernetes Enricher
builder.Services.AddApplicationInsightsKubernetesEnricher();

var app = builder.Build();

await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
var databaseFacade = scope.ServiceProvider.GetRequiredService<TodoDb>().Database;

await databaseFacade.MigrateAsync();

if (app.Environment.IsDevelopment())
{
    app.UseOpenApi();
    app.UseSwaggerUi(config =>
    {
        config.DocumentTitle = "TodoAPI";
        config.Path = "/swagger";
        config.DocumentPath = "/swagger/{documentName}/swagger.json";
        config.DocExpansion = "list";
    });
}

// <snippet_group>
RouteGroupBuilder todoItems = app.MapGroup("/todoitems");

todoItems.MapGet("/", GetAllTodos);
todoItems.MapGet("/complete", GetCompleteTodos);
todoItems.MapGet("/{id}", GetTodo);
todoItems.MapPost("/", CreateTodo);
todoItems.MapPut("/{id}", UpdateTodo);
todoItems.MapDelete("/{id}", DeleteTodo);
// </snippet_group>

app.Run();

// <snippet_handlers>
// <snippet_getalltodos>
static async Task<IResult> GetAllTodos(TodoDb db)
{
    return TypedResults.Ok(await db.Todos.Select(x => new TodoItemDTO(x)).ToArrayAsync());
}
// </snippet_getalltodos>

static async Task<IResult> GetCompleteTodos(TodoDb db)
{
    return TypedResults.Ok(await db.Todos.Where(t => t.IsComplete).Select(x => new TodoItemDTO(x)).ToListAsync());
}

static async Task<IResult> GetTodo(int id, TodoDb db, ServiceBusService serviceBus)
{
    var dto = new TodoItemDTO()
    {
        Id = id
    };
    await serviceBus.SendMessageAsync(new TodoEvent("GetTodo", dto));

    return await db.Todos.FindAsync(id)
        is Todo todo
            ? TypedResults.Ok(new TodoItemDTO(todo))
            : TypedResults.NotFound();
}

static async Task<IResult> CreateTodo(TodoItemDTO todoItemDTO, TodoDb db, ServiceBusService serviceBus)
{
    var todoItem = new Todo
    {
        IsComplete = todoItemDTO.IsComplete,
        Name = todoItemDTO.Name
    };

    db.Todos.Add(todoItem);
    await db.SaveChangesAsync();

    todoItemDTO = new TodoItemDTO(todoItem);

    var dto = new TodoItemDTO(todoItem);
    await serviceBus.SendMessageAsync(new TodoEvent("TodoCreated", dto));

    return TypedResults.Created($"/todoitems/{todoItem.Id}", todoItemDTO);
}

static async Task<IResult> UpdateTodo(int id, TodoItemDTO todoItemDTO, TodoDb db, ServiceBusService serviceBus)
{
    var todo = await db.Todos.FindAsync(id);

    if (todo is null) return TypedResults.NotFound();

    todo.Name = todoItemDTO.Name;
    todo.IsComplete = todoItemDTO.IsComplete;

    await db.SaveChangesAsync();

    await serviceBus.SendMessageAsync(new TodoEvent("UpdateTodo", todoItemDTO));

    return TypedResults.NoContent();
}

static async Task<IResult> DeleteTodo(int id, TodoDb db, ServiceBusService serviceBus)
{
    if (await db.Todos.FindAsync(id) is Todo todo)
    {
        db.Todos.Remove(todo);
        await db.SaveChangesAsync();

        var dto = new TodoItemDTO();
        dto.Id = id;

        await serviceBus.SendMessageAsync(new TodoEvent("DeleteTodo", dto));

        return TypedResults.NoContent();
    }

    return TypedResults.NotFound();
}
// <snippet_handlers>
// </snippet_all>
