using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace Forma.Xaml
{
    public enum BindingMode { OneTime, OneWay, TwoWay }
    public enum UpdateSourceTrigger { Default, PropertyChanged, LostFocus }

    public readonly struct BindingValue<T>
    {
        private BindingValue(bool hasValue, T value)
        {
            HasValue = hasValue;
            Value = value;
        }

        public bool HasValue { get; }
        public T Value { get; }
        public static BindingValue<T> FromValue(T value) => new BindingValue<T>(true, value);
        public static BindingValue<T> Unset => default;
    }

    public sealed class CompiledBindingPath<TSource, TValue>
    {
        public CompiledBindingPath(
            Func<TSource, BindingValue<TValue>> read,
            Func<TSource, Action, IDisposable> subscribe = null,
            Action<TSource, TValue> write = null)
        {
            Read = read ?? throw new ArgumentNullException(nameof(read));
            Subscribe = subscribe;
            Write = write;
        }

        public Func<TSource, BindingValue<TValue>> Read { get; }
        public Func<TSource, Action, IDisposable> Subscribe { get; }
        public Action<TSource, TValue> Write { get; }
    }

    public sealed class BindingOptions<TTarget>
    {
        public BindingMode Mode { get; set; } = BindingMode.OneWay;
        public UpdateSourceTrigger UpdateSourceTrigger { get; set; } = UpdateSourceTrigger.Default;
        public bool HasFallbackValue { get; set; }
        public TTarget FallbackValue { get; set; }
        public bool HasTargetNullValue { get; set; }
        public TTarget TargetNullValue { get; set; }
    }

    public sealed class BindingTargetAdapter<T>
    {
        public BindingTargetAdapter(
            XamlProperty<T> property,
            Func<object, Action, IDisposable> subscribeChanged = null,
            Func<object, Action, IDisposable> subscribeCommit = null,
            UpdateSourceTrigger defaultTrigger = UpdateSourceTrigger.PropertyChanged)
        {
            Property = property ?? throw new ArgumentNullException(nameof(property));
            SubscribeChanged = subscribeChanged;
            SubscribeCommit = subscribeCommit;
            DefaultTrigger = defaultTrigger;
        }

        public XamlProperty<T> Property { get; }
        public Func<object, Action, IDisposable> SubscribeChanged { get; }
        public Func<object, Action, IDisposable> SubscribeCommit { get; }
        public UpdateSourceTrigger DefaultTrigger { get; }
    }

    public static class BindingTargetAdapters
    {
        public static readonly BindingTargetAdapter<string> LineEditText = new BindingTargetAdapter<string>(
            new XamlProperty<string>(nameof(LineEdit.Text), target => ((LineEdit)target).Text, (target, value) => ((LineEdit)target).Text = value),
            (target, update) => BindingSubscriptions.Event<Action<LineEdit, string>>(
                handler => ((LineEdit)target).TextChanged += handler,
                handler => ((LineEdit)target).TextChanged -= handler,
                (_, _) => update()),
            (target, update) => BindingSubscriptions.Event<EventHandler>(
                handler => ((LineEdit)target).FocusExited += handler,
                handler => ((LineEdit)target).FocusExited -= handler,
                (_, _) => update()),
            UpdateSourceTrigger.PropertyChanged);

        public static readonly BindingTargetAdapter<float> RangeValue = new BindingTargetAdapter<float>(
            new XamlProperty<float>(nameof(Range.Value), target => ((Range)target).Value, (target, value) => ((Range)target).Value = value),
            (target, update) => BindingSubscriptions.Event<Action<Range, float>>(
                handler => ((Range)target).ValueChanged += handler,
                handler => ((Range)target).ValueChanged -= handler,
                (_, _) => update()));

        public static readonly BindingTargetAdapter<bool> ButtonPressed = new BindingTargetAdapter<bool>(
            new XamlProperty<bool>(nameof(BaseButton.ButtonPressed), target => ((BaseButton)target).ButtonPressed, (target, value) => ((BaseButton)target).ButtonPressed = value),
            (target, update) => BindingSubscriptions.Event<Action<BaseButton, bool>>(
                handler => ((BaseButton)target).Toggled += handler,
                handler => ((BaseButton)target).Toggled -= handler,
                (_, _) => update()));

        public static readonly BindingTargetAdapter<bool> CheckBoxChecked = new BindingTargetAdapter<bool>(
            new XamlProperty<bool>(nameof(CheckBox.Checked), target => ((CheckBox)target).Checked, (target, value) => ((CheckBox)target).Checked = value),
            (target, update) => BindingSubscriptions.Event<Action<BaseButton, bool>>(
                handler => ((CheckBox)target).Toggled += handler,
                handler => ((CheckBox)target).Toggled -= handler,
                (_, _) => update()));

        public static readonly BindingTargetAdapter<int> OptionButtonSelected = new BindingTargetAdapter<int>(
            new XamlProperty<int>(nameof(OptionButton.Selected), target => ((OptionButton)target).Selected, (target, value) => ((OptionButton)target).Select(value)),
            (target, update) => BindingSubscriptions.Event<Action<OptionButton, int>>(
                handler => ((OptionButton)target).ItemSelected += handler,
                handler => ((OptionButton)target).ItemSelected -= handler,
                (_, _) => update()));
    }

    public static class CompiledBinding
    {
        public static IDisposable AttachOneWay<TSource, TValue>(
            Control root,
            object target,
            Func<TSource, TValue> read,
            string sourcePropertyName,
            Func<object, TValue> getTarget,
            Action<object, TValue> setTarget)
            where TSource : class
        {
            if (read == null) throw new ArgumentNullException(nameof(read));
            var path = new CompiledBindingPath<TSource, TValue>(
                source => BindingValue<TValue>.FromValue(read(source)),
                (source, update) => source is INotifyPropertyChanged notifications
                    ? BindingSubscriptions.PropertyChanged(notifications, sourcePropertyName, update)
                    : null);
            var property = new XamlProperty<TValue>(sourcePropertyName, getTarget, setTarget);
            return Attach(root, target, path, new BindingTargetAdapter<TValue>(property), value => value);
        }

        public static IDisposable AttachTwoWay<TSource, TValue>(
            Control root,
            object target,
            Func<TSource, TValue> read,
            Action<TSource, TValue> write,
            string sourcePropertyName,
            BindingTargetAdapter<TValue> targetAdapter,
            UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.Default)
            where TSource : class
        {
            if (read == null) throw new ArgumentNullException(nameof(read));
            if (write == null) throw new ArgumentNullException(nameof(write));
            var path = new CompiledBindingPath<TSource, TValue>(
                source => BindingValue<TValue>.FromValue(read(source)),
                (source, update) => source is INotifyPropertyChanged notifications
                    ? BindingSubscriptions.PropertyChanged(notifications, sourcePropertyName, update)
                    : null,
                write);
            return Attach(root, target, path, targetAdapter, value => value, value => value, new BindingOptions<TValue>
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = updateSourceTrigger,
            });
        }

        public static IDisposable Attach<TSource, TSourceValue, TTargetValue>(
            Control root,
            object target,
            CompiledBindingPath<TSource, TSourceValue> path,
            BindingTargetAdapter<TTargetValue> targetAdapter,
            Func<TSourceValue, TTargetValue> convert,
            Func<TTargetValue, TSourceValue> convertBack = null,
            BindingOptions<TTargetValue> options = null)
            where TSource : class
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            var expression = new CompiledBindingExpression<TSource, TSourceValue, TTargetValue>(
                root,
                target ?? throw new ArgumentNullException(nameof(target)),
                path ?? throw new ArgumentNullException(nameof(path)),
                targetAdapter ?? throw new ArgumentNullException(nameof(targetAdapter)),
                convert ?? throw new ArgumentNullException(nameof(convert)),
                convertBack,
                options ?? new BindingOptions<TTargetValue>());
            XamlAttachment.RegisterDisposable(root, expression);
            return expression;
        }
    }

    internal sealed class CompiledBindingExpression<TSource, TSourceValue, TTargetValue> : IDisposable
        where TSource : class
    {
        private readonly Control _root;
        private readonly object _target;
        private readonly CompiledBindingPath<TSource, TSourceValue> _path;
        private readonly BindingTargetAdapter<TTargetValue> _targetAdapter;
        private readonly Func<TSourceValue, TTargetValue> _convert;
        private readonly Func<TTargetValue, TSourceValue> _convertBack;
        private readonly BindingOptions<TTargetValue> _options;
        private readonly XamlValueContribution<TTargetValue> _targetValue;
        private IDisposable _sourceSubscription;
        private IDisposable _targetSubscription;
        private IDisposable _commitSubscription;
        private TSource _source;
        private bool _updatingTarget;
        private bool _pendingTargetUpdate;
        private bool _disposed;

        public CompiledBindingExpression(
            Control root,
            object target,
            CompiledBindingPath<TSource, TSourceValue> path,
            BindingTargetAdapter<TTargetValue> targetAdapter,
            Func<TSourceValue, TTargetValue> convert,
            Func<TTargetValue, TSourceValue> convertBack,
            BindingOptions<TTargetValue> options)
        {
            _root = root;
            _target = target;
            _path = path;
            _targetAdapter = targetAdapter;
            _convert = convert;
            _convertBack = convertBack;
            _options = options;
            Validate();
            _targetValue = XamlValues.Set(target, targetAdapter.Property, XamlValueLayer.Local, targetAdapter.Property.GetValue(target));
            if (_options.Mode != BindingMode.OneTime) _root.DataContextChanged += DataContextChanged;
            BindSource();
            BindTarget();
        }

        private void Validate()
        {
            if (_options.Mode != BindingMode.TwoWay) return;
            if (_path.Write == null) throw new ArgumentException("A two-way binding requires a writable source path.", nameof(_path));
            if (_convertBack == null) throw new ArgumentException("A two-way binding requires reverse conversion.", nameof(_convertBack));
            if (_targetAdapter.SubscribeChanged == null) throw new ArgumentException("The target property has no two-way change adapter.", nameof(_targetAdapter));
            var trigger = EffectiveTrigger;
            if (trigger == UpdateSourceTrigger.LostFocus && _targetAdapter.SubscribeCommit == null)
                throw new ArgumentException("The target property does not support LostFocus updates.", nameof(_options));
        }

        private UpdateSourceTrigger EffectiveTrigger => _options.UpdateSourceTrigger == UpdateSourceTrigger.Default
            ? _targetAdapter.DefaultTrigger
            : _options.UpdateSourceTrigger;

        private void DataContextChanged(object sender, DataContextChangedEventArgs args) => BindSource();

        private void BindSource()
        {
            _sourceSubscription?.Dispose();
            _sourceSubscription = null;
            _source = _root.DataContext as TSource;
            UpdateTarget();
            if (_options.Mode != BindingMode.OneTime && _source != null && _path.Subscribe != null)
                _sourceSubscription = _path.Subscribe(_source, UpdateTarget);
        }

        private void BindTarget()
        {
            if (_options.Mode != BindingMode.TwoWay) return;
            _targetSubscription = _targetAdapter.SubscribeChanged(_target, TargetChanged);
            if (EffectiveTrigger == UpdateSourceTrigger.LostFocus)
                _commitSubscription = _targetAdapter.SubscribeCommit(_target, CommitPendingTarget);
        }

        private void UpdateTarget()
        {
            TTargetValue value;
            try
            {
                if (_source == null)
                {
                    if (!_options.HasFallbackValue) return;
                    value = _options.FallbackValue;
                }
                else
                {
                    var result = _path.Read(_source);
                    if (!result.HasValue)
                    {
                        if (!_options.HasFallbackValue) return;
                        value = _options.FallbackValue;
                    }
                    else if ((object)result.Value == null && _options.HasTargetNullValue)
                        value = _options.TargetNullValue;
                    else
                        value = _convert(result.Value);
                }
            }
            catch
            {
                if (!_options.HasFallbackValue) return;
                value = _options.FallbackValue;
            }

            _updatingTarget = true;
            try { _targetValue.Value = value; }
            finally { _updatingTarget = false; }
        }

        private void TargetChanged()
        {
            if (_updatingTarget || _source == null) return;
            if (EffectiveTrigger == UpdateSourceTrigger.LostFocus)
            {
                _pendingTargetUpdate = true;
                return;
            }
            WriteSource();
        }

        private void CommitPendingTarget()
        {
            if (!_pendingTargetUpdate) return;
            _pendingTargetUpdate = false;
            WriteSource();
        }

        private void WriteSource()
        {
            if (_source == null) return;
            var value = _targetAdapter.Property.GetValue(_target);
            _path.Write(_source, _convertBack(value));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_options.Mode != BindingMode.OneTime) _root.DataContextChanged -= DataContextChanged;
            _sourceSubscription?.Dispose();
            _targetSubscription?.Dispose();
            _commitSubscription?.Dispose();
            _targetValue.Dispose();
        }
    }

    public static class BindingSubscriptions
    {
        public static IDisposable PropertyChanged(INotifyPropertyChanged source, string propertyName, Action update)
        {
            if (source == null) return EmptyDisposable.Instance;
            PropertyChangedEventHandler handler = (_, args) =>
            {
                if (string.IsNullOrEmpty(args.PropertyName) || args.PropertyName == propertyName) update();
            };
            source.PropertyChanged += handler;
            return new ActionDisposable(() => source.PropertyChanged -= handler);
        }

        public static IDisposable Combine(params IDisposable[] subscriptions) => new CompositeDisposable(subscriptions);

        public static IDisposable Event<THandler>(Action<THandler> add, Action<THandler> remove, THandler handler) where THandler : Delegate
        {
            add(handler);
            return new ActionDisposable(() => remove(handler));
        }

        private sealed class CompositeDisposable : IDisposable
        {
            private IDisposable[] _subscriptions;
            public CompositeDisposable(IDisposable[] subscriptions) => _subscriptions = subscriptions ?? Array.Empty<IDisposable>();
            public void Dispose()
            {
                var subscriptions = _subscriptions;
                _subscriptions = Array.Empty<IDisposable>();
                for (var index = subscriptions.Length - 1; index >= 0; index--) subscriptions[index]?.Dispose();
            }
        }

        private sealed class ActionDisposable : IDisposable
        {
            private Action _dispose;
            public ActionDisposable(Action dispose) => _dispose = dispose;
            public void Dispose()
            {
                var dispose = _dispose;
                _dispose = null;
                dispose?.Invoke();
            }
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new EmptyDisposable();
            public void Dispose() { }
        }
    }
}