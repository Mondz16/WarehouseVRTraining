using System;
using System.Collections.Generic;

namespace Valari.Services
{
    public sealed class ServiceLocator
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public void Add<T>(IServiceProvider serviceProvider)
        {
            _services.Add(typeof(T), serviceProvider);
        }

        public void Add<T>(T instance)
        {
            _services.Add(typeof(T), instance);
        }

        public void Remove<T>()
        {
            _services.Remove(typeof(T));
        }

        public T Get<T>()
        {
            object instance = _services[typeof(T)];
            if (instance is IServiceProvider serviceProvider)
                instance = serviceProvider.GetService();
            return (T)instance;
        }

        public void Clear()
        {
            _services.Clear();
        }
    }
}

