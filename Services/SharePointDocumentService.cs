using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;

namespace NorthrailCRM.Services;

public sealed class SharePointDocumentService
{
    private const string GraphRoot = "https://graph.microsoft.com/v1.0";
    private static readonly TokenRequestContext GraphTokenContext = new(["https://graph.microsoft.com/.default"]);
    private readonly HttpClient httpClient;
    private readonly IConfiguration configuration;
    private readonly ILogger<SharePointDocumentService> logger;
    private readonly TokenCredential? credential;

    public SharePointDocumentService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<SharePointDocumentService> logger)
    {
        this.httpClient = httpClient;
        this.configuration = configuration;
        this.logger = logger;
        this.httpClient.Timeout = TimeSpan.FromSeconds(30);

        var tenantId = configuration["SharePoint:TenantId"];
        var clientId = configuration["SharePoint:ClientId"];
        var clientSecret = configuration["SharePoint:ClientSecret"];
        if (!string.IsNullOrWhiteSpace(tenantId)
            && !string.IsNullOrWhiteSpace(clientId)
            && !string.IsNullOrWhiteSpace(clientSecret))
        {
            credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        }
    }

    public async Task<SharePointDocumentFolderContents> GetCustomerFolderContentsAsync(
        string customerName,
        CancellationToken cancellationToken = default)
    {
        var siteUrl = configuration["SharePoint:SiteUrl"];
        if (!Uri.TryCreate(siteUrl, UriKind.Absolute, out var siteUri)
            || siteUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new SharePointDocumentException("Die SharePoint-Site ist nicht korrekt konfiguriert.");
        }

        var libraryName = configuration["SharePoint:DocumentLibrary"] ?? "Freigegebene Dokumente";
        var customersPath = configuration["SharePoint:CustomersFolder"] ?? "General/Kunden";
        var sitePath = $"{GraphRoot}/sites/{siteUri.Host}:{siteUri.AbsolutePath}?$select=id";
        using var site = await GetGraphJsonAsync(sitePath, cancellationToken);
        var siteId = site.RootElement.GetProperty("id").GetString();
        if (string.IsNullOrWhiteSpace(siteId))
        {
            throw new SharePointDocumentException("Die SharePoint-Site wurde nicht gefunden.");
        }

        if (string.IsNullOrWhiteSpace(customerName))
        {
            throw new SharePointDocumentException("Für diesen Kontakt ist keine Kurzbezeichnung hinterlegt.");
        }

        var drives = await GetCollectionAsync(
            $"{GraphRoot}/sites/{Uri.EscapeDataString(siteId)}/drives?$select=id,name,webUrl,driveType",
            cancellationToken);
        if (drives.Count == 0)
        {
            logger.LogWarning("Microsoft Graph returned no document libraries for SharePoint site {SiteUrl}.", siteUri);
            throw new SharePointDocumentException(
                "Microsoft Graph liefert keine zugängliche Dokumentbibliothek. Die App-Identität benötigt Leserechte auf diese Site (Sites.Selected mit Site-Freigabe oder Sites.Read.All mit Admin-Consent).");
        }

        var drive = drives.FirstOrDefault(item =>
        {
            var name = GetString(item, "name");
            var webUrl = GetString(item, "webUrl");
            return string.Equals(name, libraryName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(GetLastPathSegment(webUrl), libraryName, StringComparison.OrdinalIgnoreCase);
        });
        if (drive.ValueKind != JsonValueKind.Object)
        {
            var documentLibraries = drives.Where(item =>
                string.Equals(GetString(item, "driveType"), "documentLibrary", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (documentLibraries.Length == 1)
            {
                drive = documentLibraries[0];
            }
        }

        var driveId = drive.ValueKind == JsonValueKind.Object ? GetString(drive, "id") : null;
        if (string.IsNullOrWhiteSpace(driveId))
        {
            var availableLibraries = string.Join(", ", drives.Select(item => GetString(item, "name")).Where(name => name is not null));
            logger.LogWarning("Configured SharePoint document library {LibraryName} was not found. Available drives: {AvailableLibraries}", libraryName, availableLibraries);
            throw new SharePointDocumentException(
                $"Die konfigurierte Dokumentbibliothek '{libraryName}' wurde nicht gefunden. Verfügbare Bibliotheken: {availableLibraries}.");
        }

        var customerRootPath = string.Join("/", customersPath.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString));
        var customerFolders = await GetCollectionAsync(
            $"{GraphRoot}/drives/{Uri.EscapeDataString(driveId)}/root:/{customerRootPath}:/children?$select=id,name,folder",
            cancellationToken);
        var matchingFolders = customerFolders
            .Where(item => item.TryGetProperty("folder", out _))
            .Where(item => GetString(item, "name") is { } name
                && name.Contains(customerName.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var customerFolder = matchingFolders.FirstOrDefault(item =>
            string.Equals(GetString(item, "name")?.Trim(), customerName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (customerFolder.ValueKind != JsonValueKind.Object && matchingFolders.Length == 1)
        {
            customerFolder = matchingFolders[0];
        }

        if (customerFolder.ValueKind != JsonValueKind.Object && matchingFolders.Length > 1)
        {
            throw new SharePointDocumentException(
                $"Mehrere SharePoint-Kundenordner enthalten die Kurzbezeichnung '{customerName.Trim()}'. Die Zuordnung ist nicht eindeutig.");
        }

        var customerFolderId = customerFolder.ValueKind == JsonValueKind.Object
            ? GetString(customerFolder, "id")
            : null;
        if (string.IsNullOrWhiteSpace(customerFolderId))
        {
            throw new SharePointDocumentException($"Der SharePoint-Kundenordner '{customerName.Trim()}' wurde unter '{customersPath}' nicht gefunden.");
        }

        return await GetFolderContentsAsync(
            driveId,
            new SharePointDocumentFolder(customerFolderId, GetString(customerFolder, "name")?.Trim() ?? customerName.Trim()),
            cancellationToken);
    }

    public async Task<SharePointDocumentFolderContents> GetFolderContentsAsync(
        string driveId,
        SharePointDocumentFolder folder,
        CancellationToken cancellationToken = default)
    {
        var siteUrl = configuration["SharePoint:SiteUrl"];
        if (!Uri.TryCreate(siteUrl, UriKind.Absolute, out var siteUri)
            || siteUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new SharePointDocumentException("Die SharePoint-Site ist nicht korrekt konfiguriert.");
        }

        var items = await GetCollectionAsync(
            $"{GraphRoot}/drives/{Uri.EscapeDataString(driveId)}/items/{Uri.EscapeDataString(folder.Id)}/children?$select=id,name,webUrl,size,lastModifiedDateTime,folder,file",
            cancellationToken);

        var documents = items
            .Where(item => item.TryGetProperty("folder", out _) || item.TryGetProperty("file", out _))
            .Select(item => CreateDocumentItem(item, siteUri.Host))
            .Where(item => item is not null)
            .Select(item => item!)
            .OrderByDescending(item => item.IsFolder)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

            return new SharePointDocumentFolderContents(driveId, folder, documents);
    }

    private async Task<List<JsonElement>> GetCollectionAsync(string url, CancellationToken cancellationToken)
    {
        var items = new List<JsonElement>();
        string? nextUrl = url;

        while (!string.IsNullOrWhiteSpace(nextUrl))
        {
            using var response = await GetGraphJsonAsync(nextUrl, cancellationToken);
            if (response.RootElement.TryGetProperty("value", out var values)
                && values.ValueKind == JsonValueKind.Array)
            {
                items.AddRange(values.EnumerateArray().Select(item => item.Clone()));
            }

            nextUrl = response.RootElement.TryGetProperty("@odata.nextLink", out var nextLink)
                ? nextLink.GetString()
                : null;
        }

        return items;
    }

    private async Task<JsonDocument> GetGraphJsonAsync(string url, CancellationToken cancellationToken)
    {
        if (credential is null)
        {
            throw new SharePointDocumentException(
                "Die App-Registrierung nrsharepointappreg ist nicht vollständig konfiguriert. Hinterlege das Client-Secret sicher unter SharePoint:ClientSecret.");
        }

        AccessToken accessToken;
        try
        {
            accessToken = await credential.GetTokenAsync(GraphTokenContext, cancellationToken);
        }
        catch (Exception exception) when (exception is AuthenticationFailedException or CredentialUnavailableException)
        {
            logger.LogWarning(exception, "Could not acquire a Microsoft Graph token for SharePoint documents.");
            throw new SharePointDocumentException(
                "Die Anwendung konnte keine Microsoft-Graph-Anmeldung für SharePoint herstellen.", exception);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Microsoft Graph request for SharePoint documents failed.");
            throw new SharePointDocumentException("SharePoint ist momentan nicht erreichbar.", exception);
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new SharePointDocumentException(
                    "Der Microsoft-Graph-Zugriff auf diese SharePoint-Site ist nicht freigegeben.");
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new SharePointDocumentException("Die SharePoint-Site, Bibliothek oder der Ordner wurde nicht gefunden.");
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Microsoft Graph returned status code {StatusCode} for SharePoint documents.", response.StatusCode);
                throw new SharePointDocumentException("Die SharePoint-Dokumente konnten nicht geladen werden.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }
    }

    private static SharePointDocumentItem? CreateDocumentItem(JsonElement item, string expectedHost)
    {
        var id = GetString(item, "id");
        var name = GetString(item, "name");
        var webUrl = GetString(item, "webUrl");
        if (string.IsNullOrWhiteSpace(id)
            || string.IsNullOrWhiteSpace(name)
            || !Uri.TryCreate(webUrl, UriKind.Absolute, out var documentUri)
            || !string.Equals(documentUri.Host, expectedHost, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        long? size = item.TryGetProperty("size", out var sizeElement) && sizeElement.TryGetInt64(out var value)
            ? value
            : null;
        DateTimeOffset? modified = item.TryGetProperty("lastModifiedDateTime", out var modifiedElement)
            && DateTimeOffset.TryParse(modifiedElement.GetString(), out var modifiedValue)
                ? modifiedValue
                : null;

        return new SharePointDocumentItem(id, name, webUrl!, item.TryGetProperty("folder", out _), size, modified);
    }

    private static string? GetString(JsonElement item, string propertyName) =>
        item.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? GetLastPathSegment(string? webUrl) =>
        Uri.TryCreate(webUrl, UriKind.Absolute, out var uri)
            ? Uri.UnescapeDataString(uri.AbsolutePath.TrimEnd('/').Split('/').Last())
            : null;

}

public sealed record SharePointDocumentFolder(string Id, string Name);

public sealed record SharePointDocumentFolderContents(
    string DriveId,
    SharePointDocumentFolder Folder,
    IReadOnlyList<SharePointDocumentItem> Items);

public sealed record SharePointDocumentItem(
    string Id,
    string Name,
    string WebUrl,
    bool IsFolder,
    long? SizeBytes,
    DateTimeOffset? LastModified);

public sealed class SharePointDocumentException : Exception
{
    public SharePointDocumentException(string message) : base(message)
    {
    }

    public SharePointDocumentException(string message, Exception innerException) : base(message, innerException)
    {
    }
}