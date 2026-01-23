using DataverseMCPToolBox.Extensibility.Abstractions;
using DataverseMCPToolBox.Models;

namespace DataverseMCPToolBox.Services;

/// <summary>
/// Service for indexing and looking up MCP tools from loaded plugins
/// </summary>
public class ToolRegistryService
{
    private readonly Dictionary<string, (IPlugin Plugin, IMcpTool Tool)> _toolRegistry = new();
    private readonly Dictionary<IPlugin, List<IMcpTool>> _pluginTools = new();

    /// <summary>
    /// Register all tools from a list of plugins
    /// </summary>
    public void RegisterPlugins(List<IPlugin> plugins)
    {
        _toolRegistry.Clear();
        _pluginTools.Clear();

        Console.Error.WriteLine($"Registering tools from {plugins.Count} plugins");

        foreach (var plugin in plugins)
        {
            try
            {
                // Check if plugin implements IToolProvider
                if (plugin is not IToolProvider toolProvider)
                {
                    Console.Error.WriteLine($"Plugin {plugin.GetType().Name} does not implement IToolProvider - skipping");
                    continue;
                }

                var tools = toolProvider.GetTools();
                var toolsList = tools.ToList();
                _pluginTools[plugin] = toolsList;

                Console.Error.WriteLine($"Plugin {plugin.GetType().Name} exposes {toolsList.Count()} tools:");

                foreach (var tool in tools)
                {
                    var toolName = tool.Name;
                    
                    if (_toolRegistry.ContainsKey(toolName))
                    {
                        Console.Error.WriteLine($"  WARNING: Duplicate tool name '{toolName}' - skipping");
                        continue;
                    }

                    _toolRegistry[toolName] = (plugin, tool);
                    Console.Error.WriteLine($"  - {toolName}: {tool.Description}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error registering tools from plugin {plugin.GetType().Name}: {ex.Message}");
            }
        }

        Console.Error.WriteLine($"Total tools registered: {_toolRegistry.Count}");
    }

    /// <summary>
    /// Get all registered tools
    /// </summary>
    public List<ToolInfo> GetAllTools()
    {
        return _toolRegistry.Values.Select(entry => new ToolInfo
        {
            Name = entry.Tool.Name,
            Description = entry.Tool.Description,
            InputSchema = entry.Tool.InputSchema
        }).ToList();
    }

    /// <summary>
    /// Get all plugins with their tools
    /// </summary>
    public List<PluginInfo> GetAllPlugins()
    {
        var pluginInfos = new List<PluginInfo>();

        foreach (var kvp in _pluginTools)
        {
            var plugin = kvp.Key;
            var tools = kvp.Value;

            // Get plugin type to extract metadata
            var pluginType = plugin.GetType();
            var mcpPluginAttr = pluginType.GetCustomAttributes(typeof(DataverseMCPToolBox.Extensibility.Attributes.McpPluginAttribute), false)
                .FirstOrDefault() as DataverseMCPToolBox.Extensibility.Attributes.McpPluginAttribute;

            var pluginInfo = new PluginInfo
            {
                Name = pluginType.Name,
                Version = pluginType.Assembly.GetName().Version?.ToString() ?? "0.0.0",
                Author = mcpPluginAttr?.Author ?? "Unknown",
                Description = mcpPluginAttr?.Description ?? "",
                Tools = tools.Select(t => new ToolInfo
                {
                    Name = t.Name,
                    Description = t.Description,
                    InputSchema = t.InputSchema
                }).ToList()
            };

            pluginInfos.Add(pluginInfo);
        }

        return pluginInfos;
    }

    /// <summary>
    /// Lookup a tool by name
    /// </summary>
    public (IPlugin? Plugin, IMcpTool? Tool) GetTool(string toolName)
    {
        if (_toolRegistry.TryGetValue(toolName, out var entry))
        {
            return (entry.Plugin, entry.Tool);
        }

        return (null, null);
    }

    /// <summary>
    /// Check if a tool exists
    /// </summary>
    public bool HasTool(string toolName)
    {
        return _toolRegistry.ContainsKey(toolName);
    }

    /// <summary>
    /// Get count of registered tools
    /// </summary>
    public int GetToolCount()
    {
        return _toolRegistry.Count;
    }
}
