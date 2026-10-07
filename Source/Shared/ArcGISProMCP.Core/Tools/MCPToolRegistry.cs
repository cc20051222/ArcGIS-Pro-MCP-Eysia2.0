namespace ArcGISProMCP.Core.Tools;

/// <summary>
/// 线程安全的 MCP 工具注册表。工具名称唯一，重复注册抛异常，列表稳定排序。
/// </summary>
public sealed class MCPToolRegistry
{
    private readonly object _gate = new();
    private readonly SortedDictionary<string, IMCPTool> _tools = new(StringComparer.Ordinal);

    public void Register(IMCPTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        if (string.IsNullOrWhiteSpace(tool.Name))
        {
            throw new ArgumentException("Tool name must not be empty.", nameof(tool));
        }

        lock (_gate)
        {
            if (_tools.ContainsKey(tool.Name))
            {
                throw new InvalidOperationException($"Tool '{tool.Name}' is already registered.");
            }

            _tools.Add(tool.Name, tool);
        }
    }

    public bool Unregister(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        lock (_gate)
        {
            return _tools.Remove(name);
        }
    }

    public IMCPTool? Get(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        lock (_gate)
        {
            return _tools.TryGetValue(name, out var tool) ? tool : null;
        }
    }

    public IReadOnlyList<IMCPTool> List()
    {
        lock (_gate)
        {
            return _tools.Values.ToArray();
        }
    }

    public bool Contains(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        lock (_gate)
        {
            return _tools.ContainsKey(name);
        }
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _tools.Count;
            }
        }
    }
}
