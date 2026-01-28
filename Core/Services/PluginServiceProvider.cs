using Microsoft.Extensions.DependencyInjection;

namespace DataverseMCPToolBox.Services;

/// <summary>
/// Simple service provider implementation for plugin initialization
/// </summary>
public class PluginServiceProvider : IServiceProvider, IDisposable
{
    private readonly IServiceProvider _serviceProvider;

    public PluginServiceProvider()
    {
        var services = new ServiceCollection();
        
        // Register core services that plugins might need
        // Add more services here as needed
        
        _serviceProvider = services.BuildServiceProvider();
    }

    public object? GetService(Type serviceType)
    {
        return _serviceProvider.GetService(serviceType);
    }

    public void Dispose()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
