namespace AzDoWiCreator;

/// <summary>
/// Resolves field names from short names (e.g., "Description") to fully qualified reference names (e.g., "System.Description").
/// Provides ambiguity detection and helpful error messages.
/// </summary>
public class FieldNameResolver
{
    // Common Azure DevOps field mappings for Scrum process template
    private static readonly Dictionary<string, string> _fieldMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        // Core System fields
        { "Title", "System.Title" },
        { "Description", "System.Description" },
        { "State", "System.State" },
        { "Reason", "System.Reason" },
        { "AssignedTo", "System.AssignedTo" },
        { "CreatedDate", "System.CreatedDate" },
        { "CreatedBy", "System.CreatedBy" },
        { "ChangedDate", "System.ChangedDate" },
        { "ChangedBy", "System.ChangedBy" },
        { "AreaPath", "System.AreaPath" },
        { "IterationPath", "System.IterationPath" },
        { "Tags", "System.Tags" },
        { "History", "System.History" },
        { "WorkItemType", "System.WorkItemType" },
        { "Id", "System.Id" },
        
        // VSTS Common fields
        { "AcceptanceCriteria", "Microsoft.VSTS.Common.AcceptanceCriteria" },
        { "BusinessValue", "Microsoft.VSTS.Common.BusinessValue" },
        { "ValueArea", "Microsoft.VSTS.Common.ValueArea" },
        { "Risk", "Microsoft.VSTS.Common.Risk" },
        { "Priority", "Microsoft.VSTS.Common.Priority" }, // Note: Maps to VSTS.Common.Priority (use System.Priority if you need the system field)
        { "Severity", "Microsoft.VSTS.Common.Severity" },
        { "StackRank", "Microsoft.VSTS.Common.StackRank" },
        { "BacklogPriority", "Microsoft.VSTS.Common.BacklogPriority" },
        { "TimeCriticality", "Microsoft.VSTS.Common.TimeCriticality" },
        { "Activity", "Microsoft.VSTS.Common.Activity" },
        { "ResolvedDate", "Microsoft.VSTS.Common.ResolvedDate" },
        { "ResolvedBy", "Microsoft.VSTS.Common.ResolvedBy" },
        { "ResolvedReason", "Microsoft.VSTS.Common.ResolvedReason" },
        { "ClosedDate", "Microsoft.VSTS.Common.ClosedDate" },
        { "ClosedBy", "Microsoft.VSTS.Common.ClosedBy" },
        
        // VSTS Scheduling fields
        { "Effort", "Microsoft.VSTS.Scheduling.Effort" },
        { "StoryPoints", "Microsoft.VSTS.Scheduling.StoryPoints" },
        { "OriginalEstimate", "Microsoft.VSTS.Scheduling.OriginalEstimate" },
        { "RemainingWork", "Microsoft.VSTS.Scheduling.RemainingWork" },
        { "CompletedWork", "Microsoft.VSTS.Scheduling.CompletedWork" },
        { "TargetDate", "Microsoft.VSTS.Scheduling.TargetDate" },
        { "StartDate", "Microsoft.VSTS.Scheduling.StartDate" },
        { "FinishDate", "Microsoft.VSTS.Scheduling.FinishDate" },
        { "DueDate", "Microsoft.VSTS.Scheduling.DueDate" },
        
        // VSTS Build fields
        { "IntegrationBuild", "Microsoft.VSTS.Build.IntegrationBuild" },
        { "FoundIn", "Microsoft.VSTS.Build.FoundIn" },
        
        // VSTS CMMI fields (optional attendees, etc.)
        { "RequiredAttendee1", "Microsoft.VSTS.CMMI.RequiredAttendee1" },
        { "RequiredAttendee2", "Microsoft.VSTS.CMMI.RequiredAttendee2" },
        { "RequiredAttendee3", "Microsoft.VSTS.CMMI.RequiredAttendee3" },
        { "OptionalAttendee1", "Microsoft.VSTS.CMMI.OptionalAttendee1" },
        { "OptionalAttendee2", "Microsoft.VSTS.CMMI.OptionalAttendee2" },
        { "OptionalAttendee3", "Microsoft.VSTS.CMMI.OptionalAttendee3" },
        
        // Repro Steps (Bug specific)
        { "ReproSteps", "Microsoft.VSTS.TCM.ReproSteps" },
        { "SystemInfo", "Microsoft.VSTS.TCM.SystemInfo" },
    };

    // Build reverse mapping for ambiguity detection
    private static readonly Dictionary<string, List<string>> _reverseMapping;
    
    static FieldNameResolver()
    {
        _reverseMapping = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var kvp in _fieldMappings)
        {
            var shortName = kvp.Key;
            var fullName = kvp.Value;
            
            if (!_reverseMapping.ContainsKey(shortName))
            {
                _reverseMapping[shortName] = new List<string>();
            }
            _reverseMapping[shortName].Add(fullName);
        }
    }

    /// <summary>
    /// Resolves a field name to its fully qualified reference name.
    /// Supports both short names (e.g., "Description") and fully qualified names (e.g., "System.Description").
    /// </summary>
    /// <param name="fieldName">The field name to resolve</param>
    /// <returns>The fully qualified field reference name</returns>
    /// <exception cref="ArgumentException">Thrown when field name is ambiguous or invalid</exception>
    public static string ResolveFieldName(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            throw new ArgumentException("Field name cannot be null or empty.", nameof(fieldName));
        }

        // If it's already a fully qualified name (contains a dot), return it as-is
        if (fieldName.Contains('.'))
        {
            return fieldName;
        }

        // Try to resolve short name
        if (_fieldMappings.TryGetValue(fieldName, out var fullName))
        {
            // Check for ambiguity
            if (_reverseMapping.TryGetValue(fieldName, out var possibleFields) && possibleFields.Count > 1)
            {
                throw new ArgumentException(
                    $"Ambiguous field name '{fieldName}'. Multiple fields match:\n" +
                    string.Join("\n", possibleFields.Select(f => $"  - {f}")) +
                    $"\n\nPlease use the fully qualified field reference name.",
                    nameof(fieldName));
            }
            
            return fullName;
        }

        // Field name not found in mappings - might be a custom field or typo
        // Return it as-is and let Azure DevOps API validate it
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Warning: Unknown field '{fieldName}'. If this is a custom field, use the fully qualified reference name (e.g., 'Custom.MyField').");
        Console.ResetColor();
        
        return fieldName;
    }

    /// <summary>
    /// Resolves all field names in a dictionary.
    /// </summary>
    /// <param name="fields">Dictionary with potentially short field names</param>
    /// <returns>Dictionary with fully qualified field names</returns>
    public static Dictionary<string, object> ResolveFields(Dictionary<string, object> fields)
    {
        var resolved = new Dictionary<string, object>();
        
        foreach (var field in fields)
        {
            try
            {
                var resolvedName = ResolveFieldName(field.Key);
                
                // Handle JsonElement from System.Text.Json deserialization
                var value = field.Value;
                if (value is System.Text.Json.JsonElement jsonElement)
                {
                    value = ConvertJsonElement(jsonElement);
                }
                
                resolved[resolvedName] = value;
            }
            catch (ArgumentException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error resolving field name: {ex.Message}");
                Console.ResetColor();
                throw;
            }
        }
        
        return resolved;
    }

    /// <summary>
    /// Converts a JsonElement to its appropriate .NET type.
    /// </summary>
    private static object ConvertJsonElement(System.Text.Json.JsonElement element)
    {
        return element.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => element.GetString() ?? "",
            System.Text.Json.JsonValueKind.Number => element.TryGetInt32(out var intVal) ? intVal : element.GetDouble(),
            System.Text.Json.JsonValueKind.True => true,
            System.Text.Json.JsonValueKind.False => false,
            System.Text.Json.JsonValueKind.Null => "",
            System.Text.Json.JsonValueKind.Array => element.EnumerateArray().Select(e => ConvertJsonElement(e)).ToList(),
            System.Text.Json.JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value)),
            _ => element.GetRawText()
        };
    }

    /// <summary>
    /// Gets all known field mappings for reference.
    /// </summary>
    public static IReadOnlyDictionary<string, string> GetKnownFields()
    {
        return _fieldMappings;
    }

    /// <summary>
    /// Checks if a field name is ambiguous.
    /// </summary>
    public static bool IsAmbiguous(string fieldName)
    {
        if (fieldName.Contains('.'))
        {
            return false; // Fully qualified names are never ambiguous
        }

        return _reverseMapping.TryGetValue(fieldName, out var possibleFields) && possibleFields.Count > 1;
    }
}
