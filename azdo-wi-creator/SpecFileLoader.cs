namespace AzDoWiCreator;

public static class SpecFileLoader
{
    private static readonly HttpClient _httpClient = new();

    public static async Task<string> LoadSpecContentAsync(string specPath)
    {
        // Check if it's an HTTP(S) URL
        if (specPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            specPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Downloading spec file from: {specPath}");
            return await _httpClient.GetStringAsync(specPath);
        }

        // Check if it's a file path with extension
        if (File.Exists(specPath))
        {
            Console.WriteLine($"Reading spec file: {specPath}");
            return await File.ReadAllTextAsync(specPath);
        }

        // Try smart name resolution
        var currentDir = Directory.GetCurrentDirectory();
        
        // Try exact name
        var exactPath = Path.Combine(currentDir, specPath);
        if (File.Exists(exactPath))
        {
            Console.WriteLine($"Reading spec file: {exactPath}");
            return await File.ReadAllTextAsync(exactPath);
        }

        // Try with -spec.local.json and -spec.json suffixes (case-insensitive)
        // Priority: .local.json > .json (local overrides take precedence)
        var specLocalJsonName = $"{specPath}-spec.local.json";
        var specJsonName = $"{specPath}-spec.json";
        var files = Directory.GetFiles(currentDir, "*.json", SearchOption.TopDirectoryOnly);
        
        // First try to find .local.json variant
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            if (string.Equals(fileName, specLocalJsonName, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Reading spec file: {file}");
                return await File.ReadAllTextAsync(file);
            }
        }
        
        // Then try to find .json variant
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            if (string.Equals(fileName, specJsonName, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Reading spec file: {file}");
                return await File.ReadAllTextAsync(file);
            }
        }

        throw new FileNotFoundException($"Could not find spec file '{specPath}'. Tried:\n" +
            $"  - HTTP(S) URL\n" +
            $"  - Exact path: {specPath}\n" +
            $"  - Current directory: {exactPath}\n" +
            $"  - Case-insensitive search for: {specLocalJsonName}\n" +
            $"  - Case-insensitive search for: {specJsonName}");
    }
}
