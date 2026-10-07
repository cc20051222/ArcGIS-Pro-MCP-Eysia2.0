namespace ArcGISProMCP.Core.Container;

/// <summary>
/// 轻量依赖容器。避免为 DI 引入 Microsoft.Extensions.DependencyInjection 等外部依赖。
/// </summary>
public sealed class ServiceContainer
{
    private readonly Dictionary<Type, Func<ServiceContainer, object>> _factories = new();
    private readonly Dictionary<Type, object> _singletons = new();
    private readonly object _gate = new();

    public void Register<T>(Func<ServiceContainer, T> factory, bool singleton = true)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        lock (_gate)
        {
            if (singleton)
            {
                _factories[typeof(T)] = c => factory(c);
            }
            else
            {
                _factories[typeof(T)] = c => factory(c);
            }
        }
    }

    public void RegisterInstance<T>(T instance)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(instance);
        lock (_gate)
        {
            _singletons[typeof(T)] = instance;
        }
    }

    public T Resolve<T>()
        where T : class
    {
        var result = TryResolve<T>();
        if (result is null)
        {
            throw new InvalidOperationException($"Service '{typeof(T).FullName}' is not registered.");
        }

        return result!;
    }

    public T? TryResolve<T>()
        where T : class
    {
        lock (_gate)
        {
            if (_singletons.TryGetValue(typeof(T), out var instance))
            {
                return (T)instance;
            }

            if (_factories.TryGetValue(typeof(T), out var factory))
            {
                var created = factory(this);
                _singletons[typeof(T)] = created;
                return (T)created;
            }

            return null;
        }
    }

    /// <summary>
    /// Reads only an already-created singleton. Unlike <see cref="TryResolve{T}"/>,
    /// this never invokes a factory and therefore has no initialization side effect.
    /// </summary>
    public T? TryResolveExisting<T>()
        where T : class
    {
        lock (_gate)
        {
            return _singletons.TryGetValue(typeof(T), out var instance)
                ? (T)instance
                : null;
        }
    }
}
