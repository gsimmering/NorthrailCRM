using NorthrailCRM.Components;
using NorthrailCRM.Data;
using NorthrailCRM.Models;
using NorthrailCRM.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<FoundryAgentChatService>();
builder.Services.AddScoped<ChatWorkspaceState>();
builder.Services.AddSingleton<ChatMarkdownRenderer>();
builder.Services.AddSingleton<FoundryFileDownloadStore>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    if (!await db.Contacts.AnyAsync())
    {
        db.Contacts.AddRange(
            new Contact { FirstName = "Maya", LastName = "Weber", Email = "maya.weber@northstar.example", PhoneNumber = "+49 30 555 01 21", Company = "Northstar Studio", JobTitle = "Geschäftsführerin", Notes = "Interessiert an einer langfristigen Partnerschaft. Folgetermin für nächste Woche vormerken." },
            new Contact { FirstName = "Jonas", LastName = "Fischer", Email = "jonas.fischer@orbit.example", PhoneNumber = "+49 89 555 02 34", Company = "Orbit Systems", JobTitle = "Leiter Vertrieb", Notes = "Hat die Produktdemo gesehen und möchte ein Angebot für sein Team." },
            new Contact { FirstName = "Leonie", LastName = "Schmidt", Email = "leonie.schmidt@formwerk.example", PhoneNumber = "+49 40 555 03 45", Company = "Formwerk GmbH", JobTitle = "Produktdesignerin", Notes = "Bevorzugt Kontakt per E-Mail. Budgetfreigabe steht noch aus." },
            new Contact { FirstName = "David", LastName = "Keller", Email = "david.keller@klarwerk.example", PhoneNumber = "+49 221 555 04 56", Company = "Klarwerk Digital", JobTitle = "Technischer Einkäufer", Notes = "Technische Anforderungen zugesendet; Rückmeldung nach interner Prüfung." },
            new Contact { FirstName = "Amira", LastName = "Yilmaz", Email = "amira.yilmaz@gruenpunkt.example", PhoneNumber = "+49 711 555 05 67", Company = "Grünpunkt Energie", JobTitle = "Partnerships Managerin", Notes = "Mögliche Kooperation für das kommende Quartal besprochen." });

        await db.SaveChangesAsync();
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapGet("/api/agent-files/{token}", async (
    string token,
    FoundryFileDownloadStore fileDownloadStore,
    FoundryAgentChatService chatService,
    CancellationToken cancellationToken) =>
{
    if (!fileDownloadStore.TryGet(token, out var file))
    {
        return Results.NotFound();
    }

    var content = await chatService.DownloadGeneratedFileAsync(file, cancellationToken);
    var contentTypeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
    if (!contentTypeProvider.TryGetContentType(file.FileName, out var contentType))
    {
        contentType = "application/octet-stream";
    }

    return Results.File(content, contentType, file.FileName);
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
