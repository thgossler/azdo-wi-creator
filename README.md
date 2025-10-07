# Azure DevOps Work Item Creator

A command-line tool for creating and managing Azure DevOps work items in bulk from JSON specification files.

## Features

- ✅ Create or update multiple work items from JSON specifications
- ✅ Support for comments in JSON spec files (single-line `//` and multi-line `/* */`)
- ✅ Support for multiple projects in a single spec file
- ✅ Support for multiple area paths (creates one work item per area path)
- ✅ Automatic markdown-to-HTML conversion for rich text fields
- ✅ Automatic tagging with `azdo-wi-creator` for tracking
- ✅ Smart spec file resolution (local paths, URLs, or simple names)
- ✅ Simulation mode (dry-run) to preview changes
- ✅ Work item protection (prevents accidental updates of non-tool-created items)
- ✅ List all work items created by the tool
- ✅ Cross-platform support (Windows, Linux, macOS - x64 and ARM64)
- ✅ Self-contained single-file executable
- ✅ Short field name support (use "Title" instead of "System.Title")

## Installation

Download the pre-built executable for your platform from the [Releases](../../releases) page:

- **Windows x64**: `azdo-wi-creator-win-x64.exe`
- **Windows ARM**: `azdo-wi-creator-win-arm64.exe`
- **Linux x64**: `azdo-wi-creator-linux-x64`
- **Linux ARM**: `azdo-wi-creator-linux-arm64`
- **macOS x64**: `azdo-wi-creator-osx-x64`
- **macOS ARM (Apple Silicon)**: `azdo-wi-creator-osx-arm64`

Or build from source:

```bash
git clone <repository-url>
cd azdo-wi-creator
dotnet publish -c Release
```

## Authentication

The tool supports multiple authentication methods in priority order:

1. **Force Interactive Sign-in**: Use the `--interactive-signin` flag to force browser-based authentication (ignores PAT and environment variable)
   ```bash
   azdo-wi-creator create -o "https://dev.azure.com/myorg" -p "MyProject" -t "Bug" -s "feature" --interactive-signin
   ```

2. **Command-line PAT option**: Use the `--pat` option to provide a Personal Access Token directly
   ```bash
   azdo-wi-creator create -o "https://dev.azure.com/myorg" -p "MyProject" -t "Bug" -s "feature" --pat "your-pat-token"
   ```

3. **Environment Variable**: Set the `AZURE_DEVOPS_PAT` environment variable
   ```bash
   export AZURE_DEVOPS_PAT="your-pat-token"
   azdo-wi-creator create -o "https://dev.azure.com/myorg" -p "MyProject" -t "Bug" -s "feature"
   ```

4. **Interactive Browser Sign-in** (default): If no PAT is provided, the tool will open a browser for you to sign in interactively (OAuth)
   ```bash
   # No PAT needed - browser will open for authentication
   azdo-wi-creator create -o "https://dev.azure.com/myorg" -p "MyProject" -t "Bug" -s "feature"
   ```

**Creating a PAT:**
1. Go to Azure DevOps → User Settings → Personal Access Tokens
2. Create a new token with **Work Items (Read, Write)** scope
3. Copy the token and use it with `--pat` option or set as environment variable

## Usage

### Create/Update Work Items

```bash
azdo-wi-creator create \
  --organization "https://dev.azure.com/myorg" \
  --project "MyProject" \
  --type "Bug" \
  --spec "feature-spec.json"
```

**Note**: The `--project` option is now optional if you specify the project in your spec file. See [Multi-Project Support](#multi-project-support) below.

**Short form:**
```bash
azdo-wi-creator create -o "https://dev.azure.com/myorg" -p "MyProject" -t "Bug" -s "feature"
```

**With PAT token:**
```bash
azdo-wi-creator create -o "https://dev.azure.com/myorg" -p "MyProject" -t "Bug" -s "feature" --pat "your-pat-token"
```

**Force interactive sign-in** (ignores PAT and environment variable):
```bash
azdo-wi-creator create -o "https://dev.azure.com/myorg" -p "MyProject" -t "Bug" -s "feature" --interactive-signin
```

**Default command** (can omit "create"):
```bash
azdo-wi-creator -o "https://dev.azure.com/myorg" -p "MyProject" -t "Bug" -s "feature"
```

### Simulation Mode (Dry-Run)

Preview what would be created without making any changes:

```bash
azdo-wi-creator create -o "https://dev.azure.com/myorg" -p "MyProject" -t "Bug" -s "feature" --simulate
```

### List Work Items Created by Tool

```bash
azdo-wi-creator list -o "https://dev.azure.com/myorg" -p "MyProject"
```

### Force Update

⚠️ **Use with caution!** Updates work items even if they weren't created by this tool:

```bash
azdo-wi-creator create -o "https://dev.azure.com/myorg" -p "MyProject" -t "Bug" -s "feature" --force
```

## Spec File Format

Create a JSON file with your work item specifications:

```json
{
  "workItems": [
    {
      "fields": {
        "Title": "My Bug Title",
        "Description": "<div>Bug description in HTML or markdown</div>",
        "AcceptanceCriteria": "- Criteria 1\n- Criteria 2",
        "Priority": 1,
        "Severity": "2 - High"
      },
      "areaPaths": [
        "MyProject\\Team1",
        "MyProject\\Team2"
      ],
      "tags": "bug-bash, high-priority"
    }
  ]
}
```

### Comments Support

You can use comments to document your spec files, explain decisions, or temporarily disable sections:

```json
{
    // This is a single-line comment
    /* 
     * This is a multi-line comment
     * explaining the work items below
     */
    "workItems": [
        {
            "project": "MyProject",
            "fields": {
                // Title field is required
                "Title": "Example Feature",
                "Description": "<div>Feature description</div>",
                "State": "New" // Can be: New, Active, Resolved, Closed
            },
            // Area paths define where the work item belongs
            "areaPaths": [
                "MyProject\\Team1",
                // You can comment out paths you don't need:
                // "MyProject\\Team2",
                "MyProject\\Team3"
            ],
            /* Tags help categorize work items */
            "tags": "example, feature"
        }
        // Add more work items here
    ]
}
```

**Note**: Your editor may show syntax warnings about comments in JSON files, but the tool handles them correctly. In Visual Studio Code use the JSONC syntax mode to avoid syntax error for "JSON with Comments" (jsonc).

### Field Name Resolution

You can use **short field names** for convenience! The tool automatically resolves them to fully qualified Azure DevOps reference names.

**Both formats work:**

```json
{
  "fields": {
    "Title": "My Feature",                    // Short name (easier to read)
    "System.Title": "My Feature"              // Fully qualified (explicit)
  }
}
```

**Supported short names** (40+ common fields):
- `Title` → `System.Title`
- `Description` → `System.Description`
- `State` → `System.State`
- `Priority` → `Microsoft.VSTS.Common.Priority`
- `AcceptanceCriteria` → `Microsoft.VSTS.Common.AcceptanceCriteria`
- `BusinessValue` → `Microsoft.VSTS.Common.BusinessValue`
- `Effort` → `Microsoft.VSTS.Scheduling.Effort`
- `Risk` → `Microsoft.VSTS.Common.Risk`
- `TargetDate` → `Microsoft.VSTS.Scheduling.TargetDate`
- And many more...

**Custom fields**: Always use fully qualified names (e.g., `Custom.MyCompanyField`)

### Automatic Markdown Detection

The tool **automatically detects markdown syntax and HTML tags** in string field values and creates corresponding HTML fields for rich text rendering in Azure DevOps!

**How it works:**

1. **System Fields** (e.g., `System.Description`, `Microsoft.VSTS.Common.AcceptanceCriteria`):
   - Stores original markdown/HTML in the field (e.g., `System.Description`)
   - Creates a separate `.Html` field with the HTML version (e.g., `System.Description.Html`)
   
2. **Custom Fields** (e.g., `Custom.ProblemStatement`, `Custom.FeatureHypothesis`):
   - Converts markdown to HTML and stores it directly in the field
   - No separate `.Html` field is created (custom fields don't support this in Azure DevOps)
   - Azure DevOps displays the HTML if the field is configured to support markdown/HTML

**Example with System Field:**

```json
{
  "fields": {
    "Description": "# Overview\n\n- Item 1\n- Item 2"
  }
}
```

Result:
- `System.Description` = "# Overview\n\n- Item 1\n- Item 2" (original markdown)
- `System.Description.Html` = "<h1>Overview</h1><ul><li>Item 1</li><li>Item 2</li></ul>" (auto-generated)

**Example with Custom Field:**

```json
{
  "fields": {
    "Custom.ProblemStatement": "# Problem\n\n- Issue 1\n- Issue 2"
  }
}
```

Result:
- `Custom.ProblemStatement` = "<h1>Problem</h1><ul><li>Issue 1</li><li>Issue 2</li></ul>" (converted to HTML)

**System fields that support .Html:**
- `System.Description`
- `System.History`
- `Microsoft.VSTS.Common.AcceptanceCriteria`
- `Microsoft.VSTS.TCM.ReproSteps`
- `Microsoft.VSTS.TCM.SystemInfo`

**Example with Markdown:**

```json
{
  "workItems": [
    {
      "fields": {
        "Title": "Implement User Authentication",
        "Description": "# Overview\n\nThis feature adds user authentication with the following:\n\n- **OAuth 2.0** support\n- *JWT tokens* for session management\n- Password reset functionality\n\n## Technical Details\n\n```javascript\nconst token = jwt.sign({ userId }, secret);\n```\n\nSee the [design doc](https://example.com) for more info.",
        "Custom.TechSpec": "## Architecture\n\n- Microservices\n- Event-driven\n- REST APIs"
      },
      "areaPaths": ["MyProject\\Team1"]
    }
  ]
}
```

**What happens:**
- `System.Description`: Markdown detected → creates both `System.Description` (original) and `System.Description.Html` (converted)
- `Custom.TechSpec`: Markdown detected → converts to HTML and stores in `Custom.TechSpec`
- Azure DevOps renders both beautifully

**Example with HTML:**

```json
{
  "workItems": [
    {
      "fields": {
        "Title": "Bug Fix",
        "Description": "<div>This is a critical bug that needs attention.</div><br/><p>Steps to reproduce:</p><ul><li>Step 1</li><li>Step 2</li></ul>",
        "Custom.Notes": "Some text with <br/> and <strong>emphasis</strong>"
      },
      "areaPaths": ["MyProject\\Team1"]
    }
  ]
}
```

**What happens:**
- `System.Description`: HTML detected → creates both `System.Description` (original) and `System.Description.Html` (same HTML)
- `Custom.Notes`: HTML detected → stores HTML directly in `Custom.Notes`
- Azure DevOps renders the HTML properly

**Supported Markdown:**
- Headers: `# H1`, `## H2`, `### H3`, etc.
- Bold: `**bold**` or `__bold__`
- Italic: `*italic*` or `_italic_`
- Links: `[text](url)`
- Code blocks: `` ```code``` ``
- Inline code: `` `code` ``
- Lists: `- item` or `* item` or `1. item`
- Blockquotes: `> quote`

**Supported HTML Tags:**
- Any valid HTML tags: `<div>`, `<span>`, `<p>`, `<br/>`, `<strong>`, `<em>`, `<a>`, `<ul>`, `<ol>`, `<li>`, `<h1>`-`<h6>`, `<pre>`, `<code>`, `<blockquote>`, etc.

**Plain text is left unchanged** - only fields with markdown syntax or HTML tags get HTML versions!

### Field Mapping

Common work item fields:

- `Title` (or `System.Title`) - Work item title (required)
- `Description` (or `System.Description`) - Description (supports HTML/markdown)
- `AssignedTo` (or `System.AssignedTo`) - Email or display name of assignee
- `Priority` (or `Microsoft.VSTS.Common.Priority`) - Priority (1-4)
- `AcceptanceCriteria` (or `Microsoft.VSTS.Common.AcceptanceCriteria`) - Acceptance criteria
- `Severity` (or `Microsoft.VSTS.Common.Severity`) - Severity level
- `Tags` (or `System.Tags`) - Will be merged with the tool tag

### Spec File Loading

The tool supports multiple ways to specify the spec file:

1. **Local file path** (absolute or relative):
   ```bash
   --spec "/path/to/spec.json"
   --spec "./specs/feature-spec.json"
   ```

2. **HTTP(S) URL**:
   ```bash
   --spec "https://example.com/specs/feature-spec.json"
   ```

3. **Simple name** (smart resolution):
   ```bash
   --spec "feature"
   ```
   The tool will search for:
   - `feature` (exact match)
   - `feature-spec.json` (case-insensitive)
   - In the current working directory

### Multi-Project Support

You can create work items across multiple projects in a single spec file!

Each work item can specify its own `project` field:

```json
{
  "workItems": [
    {
      "project": "ProjectAlpha",
      "fields": {
        "Title": "Feature in Project Alpha",
        "Description": "<div>This goes to ProjectAlpha</div>"
      },
      "areaPaths": ["ProjectAlpha\\Team1"]
    },
    {
      "project": "ProjectBeta",
      "fields": {
        "Title": "Feature in Project Beta",
        "Description": "<div>This goes to ProjectBeta</div>"
      },
      "areaPaths": ["ProjectBeta\\Team2"]
    }
  ]
}
```

**Usage options:**

1. **All projects in spec file** (no --project needed):
   ```bash
   azdo-wi-creator create -o "https://dev.azure.com/myorg" -t "Feature" -s "multi-project-spec.json"
   ```

2. **Mix of spec and command-line** (--project as fallback):
   ```bash
   # Work items without "project" field will use "DefaultProject"
   azdo-wi-creator create -o "https://dev.azure.com/myorg" -p "DefaultProject" -t "Feature" -s "spec.json"
   ```

3. **Single project** (legacy style, still supported):
   ```bash
   azdo-wi-creator create -o "https://dev.azure.com/myorg" -p "MyProject" -t "Feature" -s "spec.json"
   ```

## How It Works

1. **Creation**: When creating work items, the tool automatically adds a `azdo-wi-creator` tag
2. **Updates**: The tool checks for existing work items with the same title, type, and area path
3. **Protection**: By default, the tool refuses to update work items without the `azdo-wi-creator` tag
4. **State Preservation**: When updating, the State field is preserved (not reset to "New")
5. **Multiple Area Paths**: Each area path specified creates a separate work item with the same details
6. **Multiple Projects**: Work items are grouped by project for efficient processing

## Examples

### Example Files

See the [examples/](examples/) directory for complete sample spec files:
- **[Feature-spec.json](examples/Feature-spec.json)** - Comprehensive Feature work items with all Azure DevOps Scrum fields
- **[multi-project-spec.json](examples/multi-project-spec.json)** - Work items across multiple projects
- **[short-names-spec.json](examples/short-names-spec.json)** - Demonstrates short field name usage
- **[comments-example-spec.json](examples/comments-example-spec.json)** - Shows how to use comments in spec files

### Example 1: Create bugs for multiple teams

**bug-spec.json:**
```json
{
  "workItems": [
    {
      "fields": {
        "Title": "Fix login page issue",
        "Description": "Users unable to login on mobile devices",
        "Severity": "1 - Critical"
      },
      "areaPaths": [
        "MyProject\\Frontend",
        "MyProject\\Backend"
      ],
      "tags": "mobile, login"
    }
  ]
}
```

```bash
azdo-wi-creator -o "https://dev.azure.com/myorg" -p "MyProject" -t "Bug" -s "bug-spec.json"
```

This creates **2 work items** (one for Frontend, one for Backend).

### Example 2: Simulation mode

```bash
azdo-wi-creator -o "https://dev.azure.com/myorg" -p "MyProject" -t "Task" -s "tasks" --simulate
```

Output:
```
=== SIMULATION MODE - No changes will be made ===

Would create work item #1:
  Type: Task
  Area Path: MyProject\Team1
  State: New
  Title: Implement feature X
  Fields:
    System.Title: Implement feature X
    System.Description: Detailed description...
  Tags: azdo-wi-creator; feature-x

=== SIMULATION COMPLETE ===
Would create 1 work item(s)
```

### Example 3: Short field names

**my-feature-spec.json:**
```json
{
  "workItems": [
    {
      "fields": {
        "Title": "Customer Portal Redesign",
        "Description": "<div><h2>Overview</h2><p>Modernize the customer portal...</p></div>",
        "Priority": 1,
        "BusinessValue": 500,
        "Effort": 40,
        "Risk": "High",
        "TargetDate": "2025-06-30"
      },
      "areaPaths": ["MyProject\\CustomerExperience"]
    }
  ]
}
```

All short names like `Title`, `Priority`, `BusinessValue` are automatically resolved!

### Example 4: Multi-project work items

**multi-project-spec.json:**
```json
{
  "workItems": [
    {
      "project": "ProjectAlpha",
      "fields": {
        "Title": "Implement User Authentication",
        "Priority": 1,
        "BusinessValue": 300
      },
      "areaPaths": ["ProjectAlpha\\Backend", "ProjectAlpha\\Security"]
    },
    {
      "project": "ProjectBeta",
      "fields": {
        "Title": "API Rate Limiting",
        "Priority": 1,
        "BusinessValue": 200
      },
      "areaPaths": ["ProjectBeta\\API"]
    }
  ]
}
```

```bash
# No --project needed when all work items specify their project
azdo-wi-creator create -o "https://dev.azure.com/myorg" -t "Feature" -s "multi-project-spec.json"
```

Output shows projects being processed:
```
Loaded 2 work item specification(s)
Organization: https://dev.azure.com/myorg
Projects: ProjectAlpha, ProjectBeta (2 projects)
Work Item Type: Feature

--- Processing project: ProjectAlpha ---
✓ Created work item #123: Implement User Authentication
✓ Created work item #124: Implement User Authentication

--- Processing project: ProjectBeta ---
✓ Created work item #125: API Rate Limiting
```

### Example 5: Loading from URL

```bash
azdo-wi-creator -o "https://dev.azure.com/myorg" -p "MyProject" -t "User Story" \
  -s "https://raw.githubusercontent.com/myorg/specs/main/feature-spec.json"
```

## Building from Source

Requirements:
- .NET 9 SDK

### Build for all platforms:

```bash
# Windows x64
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./publish/win-x64

# Windows ARM64
dotnet publish -c Release -r win-arm64 --self-contained -p:PublishSingleFile=true -o ./publish/win-arm64

# Linux x64
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o ./publish/linux-x64

# Linux ARM64
dotnet publish -c Release -r linux-arm64 --self-contained -p:PublishSingleFile=true -o ./publish/linux-arm64

# macOS x64
dotnet publish -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true -o ./publish/osx-x64

# macOS ARM64 (Apple Silicon)
dotnet publish -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true -o ./publish/osx-arm64
```

## GitHub Actions

See the `.github/workflows/` directory for automated build workflows that create releases for all platforms.

## Troubleshooting

### Authentication Issues

If you get authentication errors:

1. **Verify PAT**: Ensure `AZURE_DEVOPS_PAT` is set correctly
2. **Check permissions**: PAT needs `Work Items (Read, Write)` scope
3. **Force interactive sign-in**: Use `--interactive-signin` to bypass PAT and environment variables
   ```bash
   azdo-wi-creator create -o "..." -p "..." -t "..." -s "..." --interactive-signin
   ```
4. **Try Azure CLI**: Run `az login` and try again

**When to use `--interactive-signin`:**
- You have `AZURE_DEVOPS_PAT` set but want to sign in as a different user
- You want to ensure you're using OAuth tokens instead of PAT
- Troubleshooting PAT-related authentication issues

### Field Validation Errors

If work items fail to create:

1. **Required fields**: Check that all mandatory fields for your work item type are included
2. **Field names**: You can use short names (e.g., `Title`) or fully qualified names (e.g., `System.Title`)
3. **Simulation mode**: Use `--simulate` to preview without creating
4. **Check warnings**: Unknown field names will show warnings - verify spelling or use fully qualified names for custom fields

### Area Path Issues

- Area paths must exist in your project
- Use backslashes: `MyProject\\Team1\\SubTeam`
- If not specified, defaults to project root

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

MIT License - see [LICENSE](LICENSE) file for details.
