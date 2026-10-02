using System.Diagnostics;
using System.Runtime.CompilerServices;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;
using OpenAI.Responses;

namespace NorthrailCRM.Services;

public sealed class FoundryAgentChatService
{
    private readonly AIProjectClient? projectClient;
    private readonly string? agentName;
    private readonly FoundryFileDownloadStore fileDownloadStore;
    private readonly ILogger<FoundryAgentChatService> logger;

    public FoundryAgentChatService(
        IConfiguration configuration,
        FoundryFileDownloadStore fileDownloadStore,
        ILogger<FoundryAgentChatService> logger)
    {
        this.fileDownloadStore = fileDownloadStore;
        this.logger = logger;
        var endpoint = configuration["Foundry:ProjectEndpoint"];
        agentName = configuration["Foundry:AgentName"];

        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var projectEndpoint)
            && projectEndpoint.Scheme == Uri.UriSchemeHttps
            && !string.IsNullOrWhiteSpace(agentName))
        {
            projectClient = new AIProjectClient(
                endpoint: projectEndpoint,
                tokenProvider: new DefaultAzureCredential());
        }
    }

    public bool IsConfigured => projectClient is not null && !string.IsNullOrWhiteSpace(agentName);

#pragma warning disable OPENAI001
    public async Task<byte[]> DownloadGeneratedFileAsync(
        FoundryDownloadRecord file,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Microsoft Foundry ist noch nicht konfiguriert.");
        }

        var containerClient = projectClient!.ProjectOpenAIClient.GetContainerClient();
        var content = await Task.Run(
            () => containerClient.DownloadContainerFile(file.ContainerId, file.FileId),
            cancellationToken);
        return content.Value.ToArray();
    }
    #pragma warning restore OPENAI001

    public async Task<string> CreateConversationAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Microsoft Foundry ist noch nicht konfiguriert.");
        }

        var stopwatch = Stopwatch.StartNew();
        var conversation = await Task.Run(
            () => projectClient!.ProjectOpenAIClient
                .GetProjectConversationsClient()
                .CreateProjectConversation(),
            cancellationToken);
        logger.LogInformation("Foundry conversation creation took {ElapsedMilliseconds} ms.", stopwatch.ElapsedMilliseconds);

        return conversation.Value.Id;
    }

#pragma warning disable OPENAI001
    public async IAsyncEnumerable<FoundryAgentStreamUpdate> StreamMessageAsync(
        string conversationId,
        string message,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Microsoft Foundry ist noch nicht konfiguriert.");
        }

        var stopwatch = Stopwatch.StartNew();
        long? firstTextDeltaMilliseconds = null;
        var client = projectClient!.ProjectOpenAIClient
            .GetProjectResponsesClientForAgent(
                defaultAgent: agentName!,
                defaultConversationId: conversationId);
        var options = new CreateResponseOptions
        {
            StreamingEnabled = true
        };
        options.InputItems.Add(ResponseItem.CreateUserMessageItem(message));
        var seenFiles = new HashSet<string>(StringComparer.Ordinal);

        await foreach (var update in client.CreateResponseStreamingAsync(options, cancellationToken)
            .ConfigureAwait(false))
        {
            if (update is StreamingResponseOutputTextDeltaUpdate textDelta
                && !string.IsNullOrEmpty(textDelta.Delta))
            {
                if (firstTextDeltaMilliseconds is null)
                {
                    firstTextDeltaMilliseconds = stopwatch.ElapsedMilliseconds;
                    logger.LogInformation("Foundry time to first text delta: {ElapsedMilliseconds} ms.", firstTextDeltaMilliseconds);
                }

                yield return new FoundryAgentStreamUpdate(TextDelta: textDelta.Delta);
            }
            else if (update is StreamingResponseOutputItemDoneUpdate itemDone
                && itemDone.Item is MessageResponseItem messageItem)
            {
                foreach (var content in messageItem.Content)
                {
                    foreach (var annotation in content.OutputTextAnnotations)
                    {
                        if (annotation is not ContainerFileCitationMessageAnnotation fileCitation
                            || !seenFiles.Add(fileCitation.FileId))
                        {
                            continue;
                        }

                        var attachment = fileDownloadStore.CreateAttachment(
                            fileCitation.ContainerId,
                            fileCitation.FileId,
                            fileCitation.Filename);

                        yield return new FoundryAgentStreamUpdate(Attachment: attachment);
                    }
                }
            }
        }

        logger.LogInformation(
            "Foundry response stream completed in {ElapsedMilliseconds} ms; first text delta at {FirstTextDeltaMilliseconds} ms.",
            stopwatch.ElapsedMilliseconds,
            firstTextDeltaMilliseconds);
    }
#pragma warning restore OPENAI001
}

public sealed record FoundryAgentStreamUpdate(string? TextDelta = null, AgentChatAttachment? Attachment = null);