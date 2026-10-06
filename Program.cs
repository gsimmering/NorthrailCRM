using NorthrailCRM.Components;
using NorthrailCRM.Services;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddScoped<ContactService>();
builder.Services.AddHttpClient<SharePointDocumentService>();
builder.Services.AddScoped<FoundryAgentChatService>();
builder.Services.AddScoped<ChatWorkspaceState>();
builder.Services.AddSingleton<ChatMarkdownRenderer>();
builder.Services.AddSingleton<FoundryFileDownloadStore>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapGet("/api/current-user", (HttpContext context) =>
{
    string? displayName = null;
    var encodedPrincipal = context.Request.Headers["X-MS-CLIENT-PRINCIPAL"].FirstOrDefault();

    if (!string.IsNullOrWhiteSpace(encodedPrincipal))
    {
        try
        {
            using var principal = JsonDocument.Parse(Convert.FromBase64String(encodedPrincipal));
            if (principal.RootElement.TryGetProperty("claims", out var claims)
                && claims.ValueKind == JsonValueKind.Array)
            {
                string[] nameClaimTypes =
                [
                    "name",
                    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name",
                    "preferred_username",
                    "upn",
                    "email"
                ];

                foreach (var claimType in nameClaimTypes)
                {
                    displayName = claims.EnumerateArray()
                        .Where(claim => claim.TryGetProperty("typ", out var type)
                            && type.GetString() == claimType)
                        .Select(claim => claim.TryGetProperty("val", out var value) ? value.GetString() : null)
                        .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

                    if (displayName is not null)
                    {
                        break;
                    }
                }
            }
        }
        catch (FormatException)
        {
        }
        catch (JsonException)
        {
        }
    }

    displayName ??= context.Request.Headers["X-MS-CLIENT-PRINCIPAL-NAME"].FirstOrDefault();
    return Results.Ok(new { name = displayName });
});

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
