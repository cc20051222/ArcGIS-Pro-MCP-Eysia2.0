using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>
/// Central invocation-level path guard for schema fields that declare an output/target path.
/// Tool-specific guards remain as defense in depth for domain-specific inputs and outputs.
/// </summary>
public sealed class ProtectedOutputPathArgumentValidator : IToolPathArgumentValidator
{
    public OperationError? Validate(IMCPTool tool, IReadOnlyDictionary<string, object?>? arguments)
    {
        if (arguments is null || arguments.Count == 0
            || !tool.InputSchema.TryGetValue("properties", out var propertiesRaw)
            || propertiesRaw is not IReadOnlyDictionary<string, object?> properties)
        {
            return null;
        }

        foreach (var pair in arguments)
        {
            if (!properties.TryGetValue(pair.Key, out var schemaRaw)
                || !IsOutputPathProperty(pair.Key, schemaRaw))
            {
                continue;
            }

            foreach (var path in EnumeratePaths(pair.Value))
            {
                if (!Path.IsPathRooted(path))
                {
                    continue;
                }

                var hit = ProtectedOutputPathGuard.Match(path);
                if (hit is not null)
                {
                    return new OperationError(
                        ErrorCodes.PathEscapeRejected,
                        $"Tool '{tool.Name}' output path argument '{pair.Key}' was refused by the shared path guard ('{hit}').");
                }
            }
        }

        return null;
    }

    private static bool IsOutputPathProperty(string name, object? schemaRaw)
    {
        var keySignalsOutput = name.StartsWith("out", StringComparison.OrdinalIgnoreCase)
                               || name.Contains("output", StringComparison.OrdinalIgnoreCase)
                               || name.Contains("target", StringComparison.OrdinalIgnoreCase)
                               || name.Contains("destination", StringComparison.OrdinalIgnoreCase);
        if (keySignalsOutput)
        {
            return true;
        }

        if (schemaRaw is IReadOnlyDictionary<string, object?> schema
            && schema.TryGetValue("description", out var descriptionRaw)
            && descriptionRaw is string description)
        {
            return description.Contains("output", StringComparison.OrdinalIgnoreCase)
                   || description.Contains("target", StringComparison.OrdinalIgnoreCase)
                   || description.Contains("输出", StringComparison.Ordinal)
                   || description.Contains("目标", StringComparison.Ordinal);
        }

        return false;
    }

    private static IEnumerable<string> EnumeratePaths(object? value)
    {
        if (value is string single)
        {
            yield return single;
            yield break;
        }

        if (value is IEnumerable<string> strings)
        {
            foreach (var item in strings)
            {
                yield return item;
            }
        }
    }
}
