using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.Core.Tools;

/// <summary>Shared validation order for router and nested tool invocations.</summary>
public sealed class ToolValidatorPipeline
{
    private readonly IToolPathArgumentValidator? _pathValidator;

    public ToolValidatorPipeline(IToolPathArgumentValidator? pathValidator = null)
        => _pathValidator = pathValidator;

    public OperationError? Validate(IMCPTool tool, ToolExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(context);

        // D-064 precedence is deliberate: a write tool is refused before parsing even invalid arguments.
        if (context.ReadOnly is { IsReadOnly: true } && ToolWriteClassification.RefusedInReadOnly(tool.Name))
        {
            return ToolWriteClassification.ReadOnlyRefusal(tool.Name);
        }

        var requiredError = ToolArgumentValidator.Validate(tool, context.Arguments);
        if (requiredError is not null)
        {
            return requiredError;
        }

        return _pathValidator?.Validate(tool, context.Arguments);
    }
}
