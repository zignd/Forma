using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Forma.Xaml
{
    public enum XamlValueLayer
    {
        Style = 1,
        Local = 2,
        Animation = 3,
    }

    public sealed class XamlProperty<T>
    {
        public XamlProperty(string name, Func<object, T> getValue, Action<object, T> setValue)
        {
            Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("A property name is required.", nameof(name)) : name;
            GetValue = getValue ?? throw new ArgumentNullException(nameof(getValue));
            SetValue = setValue ?? throw new ArgumentNullException(nameof(setValue));
        }

        public string Name { get; }
        internal Func<object, T> GetValue { get; }
        internal Action<object, T> SetValue { get; }
    }

    public sealed class XamlValueContribution<T> : IDisposable
    {
        private XamlValueEntry<T> _entry;
        internal XamlValueContribution(XamlValueEntry<T> entry, long sequence, XamlValueLayer layer, long priority, T value)
        {
            _entry = entry;
            Sequence = sequence;
            Layer = layer;
            Priority = priority;
            _value = value;
        }

        private T _value;
        internal long Sequence { get; }
        internal XamlValueLayer Layer { get; }
        internal long Priority { get; }
        internal T CurrentValue => _value;

        public T Value
        {
            get => _value;
            set
            {
                if (_entry == null) throw new ObjectDisposedException(nameof(XamlValueContribution<T>));
                _value = value;
                _entry.Apply();
            }
        }

        public void Dispose()
        {
            var entry = _entry;
            if (entry == null) return;
            _entry = null;
            entry.Remove(this);
        }
    }

    public static class XamlValues
    {
        private sealed class EntryMap
        {
            public readonly Dictionary<object, IXamlValueEntry> Entries = new Dictionary<object, IXamlValueEntry>();
        }

        private static readonly ConditionalWeakTable<object, EntryMap> Values = new ConditionalWeakTable<object, EntryMap>();

        public static XamlValueContribution<T> Set<T>(
            object target,
            XamlProperty<T> property,
            XamlValueLayer layer,
            T value,
            long priority = 0)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (property == null) throw new ArgumentNullException(nameof(property));
            if (!Enum.IsDefined(typeof(XamlValueLayer), layer)) throw new ArgumentOutOfRangeException(nameof(layer));
            return GetEntry(target, property).Add(layer, priority, value);
        }

        public static T GetEffectiveValue<T>(object target, XamlProperty<T> property)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (property == null) throw new ArgumentNullException(nameof(property));
            return GetEntry(target, property).EffectiveValue;
        }

        public static void RefreshBaseValue<T>(object target, XamlProperty<T> property)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (property == null) throw new ArgumentNullException(nameof(property));
            GetEntry(target, property).RefreshBaseValue();
        }

        private static XamlValueEntry<T> GetEntry<T>(object target, XamlProperty<T> property)
        {
            var entries = Values.GetOrCreateValue(target).Entries;
            if (entries.TryGetValue(property, out var existing)) return (XamlValueEntry<T>)existing;
            var created = new XamlValueEntry<T>(target, property);
            entries.Add(property, created);
            return created;
        }
    }

    internal interface IXamlValueEntry { }

    internal sealed class XamlValueEntry<T> : IXamlValueEntry
    {
        private readonly object _target;
        private readonly XamlProperty<T> _property;
        private readonly List<XamlValueContribution<T>> _contributions = new List<XamlValueContribution<T>>();
        private T _baseValue;
        private long _nextSequence;

        public XamlValueEntry(object target, XamlProperty<T> property)
        {
            _target = target;
            _property = property;
            _baseValue = property.GetValue(target);
            EffectiveValue = _baseValue;
        }

        public T EffectiveValue { get; private set; }

        public XamlValueContribution<T> Add(XamlValueLayer layer, long priority, T value)
        {
            var contribution = new XamlValueContribution<T>(this, ++_nextSequence, layer, priority, value);
            _contributions.Add(contribution);
            Apply();
            return contribution;
        }

        public void Remove(XamlValueContribution<T> contribution)
        {
            _contributions.Remove(contribution);
            Apply();
        }

        public void RefreshBaseValue()
        {
            _baseValue = _property.GetValue(_target);
            Apply();
        }

        public void Apply()
        {
            XamlValueContribution<T> winner = null;
            foreach (var contribution in _contributions)
            {
                if (winner == null || contribution.Layer > winner.Layer ||
                    contribution.Layer == winner.Layer && contribution.Priority > winner.Priority ||
                    contribution.Layer == winner.Layer && contribution.Priority == winner.Priority && contribution.Sequence > winner.Sequence)
                    winner = contribution;
            }

            EffectiveValue = winner == null ? _baseValue : winner.CurrentValue;
            _property.SetValue(_target, EffectiveValue);
        }
    }
}