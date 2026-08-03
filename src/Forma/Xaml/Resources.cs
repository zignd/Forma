using System;
using System.Collections.Generic;

namespace Forma.Xaml
{
    public static class StaticResource
    {
        public static T Resolve<T>(Control target, string key)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (!target.TryFindResource(key, out var value)) throw new KeyNotFoundException($"Resource '{key}' was not found.");
            if (value is T typed) return typed;
            throw new InvalidCastException($"Resource '{key}' is {value?.GetType().FullName ?? "null"}, not {typeof(T).FullName}.");
        }
    }

    public static class DynamicResource
    {
        public static IDisposable Attach<T>(
            Control root,
            Control target,
            XamlProperty<T> property,
            string key,
            Func<object, T> convert = null,
            XamlValueLayer layer = XamlValueLayer.Local,
            long priority = 0)
        {
            var expression = new DynamicResourceExpression<T>(target, property, key, convert, layer, priority);
            XamlAttachment.RegisterDisposable(root, expression);
            return expression;
        }
    }

    internal sealed class DynamicResourceExpression<T> : IDisposable
    {
        private readonly Control _target;
        private readonly XamlProperty<T> _property;
        private readonly string _key;
        private readonly Func<object, T> _convert;
        private readonly XamlValueLayer _layer;
        private readonly long _priority;
        private readonly List<ResourceDictionary> _dictionaries = new List<ResourceDictionary>();
        private XamlValueContribution<T> _value;
        private bool _disposed;

        public DynamicResourceExpression(Control target, XamlProperty<T> property, string key, Func<object, T> convert, XamlValueLayer layer, long priority)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
            _property = property ?? throw new ArgumentNullException(nameof(property));
            _key = string.IsNullOrEmpty(key) ? throw new ArgumentException("A resource key is required.", nameof(key)) : key;
            _convert = convert ?? (value => (T)value);
            _layer = layer;
            _priority = priority;
            target.Attached += TargetContextChanged;
            target.Detached += TargetContextChanged;
            Subscribe();
            Update();
        }

        private void TargetContextChanged(object sender, EventArgs args)
        {
            Subscribe();
            Update();
        }

        private void Subscribe()
        {
            foreach (var dictionary in _dictionaries) dictionary.Changed -= ResourceChanged;
            _dictionaries.Clear();
            for (var control = _target; control != null; control = control.Parent)
            {
                _dictionaries.Add(control.Resources);
                control.Resources.Changed += ResourceChanged;
            }
            if (_target.Context != null)
            {
                _dictionaries.Add(_target.Context.Resources);
                _target.Context.Resources.Changed += ResourceChanged;
            }
        }

        private void ResourceChanged(object sender, EventArgs args) => Update();

        private void Update()
        {
            if (_target.TryFindResource(_key, out var found))
            {
                var converted = _convert(found);
                if (_value == null) _value = XamlValues.Set(_target, _property, _layer, converted, _priority);
                else _value.Value = converted;
            }
            else
            {
                _value?.Dispose();
                _value = null;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _target.Attached -= TargetContextChanged;
            _target.Detached -= TargetContextChanged;
            foreach (var dictionary in _dictionaries) dictionary.Changed -= ResourceChanged;
            _dictionaries.Clear();
            _value?.Dispose();
            _value = null;
        }
    }
}