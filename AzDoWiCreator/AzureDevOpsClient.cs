using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;

namespace AzDoWiCreator;

public class AzureDevOpsClient : IDisposable
{
    private readonly VssConnection _connection;
    private readonly WorkItemTrackingHttpClient _witClient;
    private readonly string _project;
    public const string ToolTag = "azdo-wi-creator";

    public AzureDevOpsClient(string organizationUrl, string project, string? pat = null)
    {
        _project = project;
        
        VssCredentials credentials;
        
        // Priority order: 1) --pat argument, 2) AZURE_DEVOPS_PAT env var, 3) Interactive browser login
        var effectivePat = pat ?? Environment.GetEnvironmentVariable("AZURE_DEVOPS_PAT");
        
        if (!string.IsNullOrEmpty(effectivePat))
        {
            // Use PAT authentication
            credentials = new VssBasicCredential(string.Empty, effectivePat);
            Console.WriteLine("Using Personal Access Token for authentication.");
        }
        else
        {
            // Use interactive browser-based authentication (OAuth)
            Console.WriteLine("No PAT provided. Using interactive browser-based sign-in...");
            Console.WriteLine("A browser window will open for you to sign in.");
            
            credentials = new VssClientCredentials(true)
            {
                PromptType = CredentialPromptType.PromptIfNeeded
            };
        }

        _connection = new VssConnection(new Uri(organizationUrl), credentials);
        _witClient = _connection.GetClient<WorkItemTrackingHttpClient>();
    }

    public async Task<List<WorkItem>> GetWorkItemsCreatedByToolAsync()
    {
        var wiql = new Wiql
        {
            Query = $@"
                SELECT [System.Id], [System.Title], [System.WorkItemType], [System.State], [System.AreaPath], [System.Tags]
                FROM WorkItems
                WHERE [System.TeamProject] = '{_project}'
                  AND [System.Tags] CONTAINS '{ToolTag}'
                ORDER BY [System.Id] DESC"
        };

        var result = await _witClient.QueryByWiqlAsync(wiql, _project);
        
        if (result.WorkItems.Count() == 0)
        {
            return new List<WorkItem>();
        }

        var ids = result.WorkItems.Select(wi => wi.Id).ToArray();
        var workItems = await _witClient.GetWorkItemsAsync(_project, ids, expand: WorkItemExpand.All);
        
        return workItems;
    }

    public async Task<WorkItem?> FindExistingWorkItemAsync(string workItemType, string title, string areaPath)
    {
        // Query for work items with same title, type, and area path
        var wiql = new Wiql
        {
            Query = $@"
                SELECT [System.Id]
                FROM WorkItems
                WHERE [System.TeamProject] = '{_project}'
                  AND [System.WorkItemType] = '{workItemType}'
                  AND [System.Title] = '{title.Replace("'", "''")}'
                  AND [System.AreaPath] = '{areaPath.Replace("'", "''")}'
                ORDER BY [System.ChangedDate] DESC"
        };

        try
        {
            var result = await _witClient.QueryByWiqlAsync(wiql, _project);
            
            if (result.WorkItems.Any())
            {
                var firstId = result.WorkItems.First().Id;
                var workItem = await _witClient.GetWorkItemAsync(_project, firstId, expand: WorkItemExpand.All);
                return workItem;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Error searching for existing work item: {ex.Message}");
        }

        return null;
    }

    public async Task<WorkItem> CreateWorkItemAsync(
        string workItemType,
        Dictionary<string, object> fields,
        string areaPath,
        string? tags = null)
    {
        var patchDocument = new JsonPatchDocument();

        // Add fields
        foreach (var field in fields)
        {
            patchDocument.Add(new JsonPatchOperation
            {
                Operation = Operation.Add,
                Path = $"/fields/{field.Key}",
                Value = field.Value
            });
        }

        // Set area path
        patchDocument.Add(new JsonPatchOperation
        {
            Operation = Operation.Add,
            Path = "/fields/System.AreaPath",
            Value = areaPath
        });

        // Set state to "New"
        patchDocument.Add(new JsonPatchOperation
        {
            Operation = Operation.Add,
            Path = "/fields/System.State",
            Value = "New"
        });

        // Add tags
        var allTags = ToolTag;
        if (!string.IsNullOrWhiteSpace(tags))
        {
            allTags = $"{ToolTag}; {tags}";
        }

        patchDocument.Add(new JsonPatchOperation
        {
            Operation = Operation.Add,
            Path = "/fields/System.Tags",
            Value = allTags
        });

        var workItem = await _witClient.CreateWorkItemAsync(patchDocument, _project, workItemType);
        return workItem;
    }

    public async Task<WorkItem> UpdateWorkItemAsync(
        int workItemId,
        Dictionary<string, object> fields,
        string? tags = null,
        bool preserveState = true)
    {
        var patchDocument = new JsonPatchDocument();

        // Get current work item to preserve state if needed
        var currentWorkItem = await _witClient.GetWorkItemAsync(_project, workItemId);
        
        // Update fields
        foreach (var field in fields)
        {
            patchDocument.Add(new JsonPatchOperation
            {
                Operation = Operation.Replace,
                Path = $"/fields/{field.Key}",
                Value = field.Value
            });
        }

        // Update tags (merge with existing)
        if (!string.IsNullOrWhiteSpace(tags))
        {
            var currentTags = currentWorkItem.Fields.ContainsKey("System.Tags") 
                ? currentWorkItem.Fields["System.Tags"]?.ToString() ?? string.Empty
                : string.Empty;

            var tagsList = currentTags.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Add new tags
            foreach (var tag in tags.Split(';', ','))
            {
                var trimmedTag = tag.Trim();
                if (!string.IsNullOrWhiteSpace(trimmedTag))
                {
                    tagsList.Add(trimmedTag);
                }
            }

            // Ensure tool tag is present
            tagsList.Add(ToolTag);

            patchDocument.Add(new JsonPatchOperation
            {
                Operation = Operation.Replace,
                Path = "/fields/System.Tags",
                Value = string.Join("; ", tagsList)
            });
        }

        var updatedWorkItem = await _witClient.UpdateWorkItemAsync(patchDocument, workItemId);
        return updatedWorkItem;
    }

    public bool HasToolTag(WorkItem workItem)
    {
        if (!workItem.Fields.ContainsKey("System.Tags"))
            return false;

        var tags = workItem.Fields["System.Tags"]?.ToString() ?? string.Empty;
        var tagsList = tags.Split(';', ',')
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t));

        return tagsList.Any(t => t.Equals(ToolTag, StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        _witClient?.Dispose();
        _connection?.Dispose();
    }
}
