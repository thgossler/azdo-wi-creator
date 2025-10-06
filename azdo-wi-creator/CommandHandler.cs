using System.CommandLine;

namespace AzDoWiCreator;

public static class CommandHandler
{
    public static Command BuildCreateCommand()
    {
        var organizationOption = new Option<string>("--organization", "-o") { Required = true, Description = "Azure DevOps organization URL (e.g., https://dev.azure.com/myorg)" };
        var projectOption = new Option<string>("--project", "-p") { Required = false, Description = "Default Azure DevOps project name (optional if project is specified in spec file per work item)" };
        var workItemTypeOption = new Option<string>("--type", "-t") { Required = true, Description = "Work item type (e.g., Bug, User Story, Task)" };
        var specOption = new Option<string>("--spec", "-s") { Required = true, Description = "Path to JSON specification file (local path, URL, or name like 'feature')" };
        var patOption = new Option<string>("--pat") { Description = "Personal Access Token for authentication. If not provided, uses AZURE_DEVOPS_PAT or interactive browser-based sign-in." };
        var interactiveSignInOption = new Option<bool>("--interactive-signin") { Description = "Force interactive browser-based sign-in, ignoring PAT token and AZURE_DEVOPS_PAT environment variable" };
        var simulateOption = new Option<bool>("--simulate") { Description = "Simulate the operation without making any changes (dry-run mode)" };
        var forceOption = new Option<bool>("--force") { Description = "⚠️  WARNING: Force update of work items even if they don't have the 'azdo-wi-creator' tag. Use with caution!" };

        var command = new Command("create", "Create or update work items")
        {
            organizationOption,
            projectOption,
            workItemTypeOption,
            specOption,
            patOption,
            interactiveSignInOption,
            simulateOption,
            forceOption
        };

        command.SetAction((parseResult) =>
        {
            var organization = parseResult.GetValue(organizationOption)!;
            var project = parseResult.GetValue(projectOption);
            var workItemType = parseResult.GetValue(workItemTypeOption)!;
            var specPath = parseResult.GetValue(specOption)!;
            var pat = parseResult.GetValue(patOption);
            var interactiveSignIn = parseResult.GetValue(interactiveSignInOption);
            var simulate = parseResult.GetValue(simulateOption);
            var force = parseResult.GetValue(forceOption);

            var executor = new WorkItemExecutor(organization, project, workItemType, pat, interactiveSignIn);
            return executor.ExecuteCreateAsync(specPath, simulate, force);
        });

        return command;
    }

    public static Command BuildListCommand()
    {
        var organizationOption = new Option<string>("--organization", "-o") { Required = true, Description = "Azure DevOps organization URL (e.g., https://dev.azure.com/myorg)" };
        var projectOption = new Option<string>("--project", "-p") { Required = true, Description = "Azure DevOps project name" };
        var patOption = new Option<string>("--pat") { Description = "Personal Access Token for authentication. If not provided, uses AZURE_DEVOPS_PAT or interactive browser-based sign-in." };
        var interactiveSignInOption = new Option<bool>("--interactive-signin") { Description = "Force interactive browser-based sign-in, ignoring PAT token and AZURE_DEVOPS_PAT environment variable" };

        var command = new Command("list", "List all work items created by this tool")
        {
            organizationOption,
            projectOption,
            patOption,
            interactiveSignInOption
        };

        command.SetAction((parseResult) =>
        {
            var organization = parseResult.GetValue(organizationOption)!;
            var project = parseResult.GetValue(projectOption)!;
            var pat = parseResult.GetValue(patOption);
            var interactiveSignIn = parseResult.GetValue(interactiveSignInOption);

            var executor = new WorkItemExecutor(organization, project, string.Empty, pat, interactiveSignIn);
            return executor.ExecuteListAsync();
        });

        return command;
    }
}
