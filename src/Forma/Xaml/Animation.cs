// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Microsoft.Xna.Framework;

namespace Forma.Xaml
{
    public enum FillBehavior { Stop, HoldEnd }
    public enum Easing { Linear, CubicIn, CubicOut, CubicInOut }

    public readonly struct RepeatBehavior
    {
        private RepeatBehavior(double count, bool forever)
        {
            Count = count;
            Forever = forever;
        }

        public double Count { get; }
        public bool Forever { get; }
        public static RepeatBehavior Once => new RepeatBehavior(1, false);
        public static RepeatBehavior ForCount(double count) => count > 0 ? new RepeatBehavior(count, false) : throw new ArgumentOutOfRangeException(nameof(count));
        public static RepeatBehavior ForeverValue => new RepeatBehavior(0, true);
    }

    public sealed class KeyFrame<T>
    {
        public KeyFrame(TimeSpan time, T value, Easing easing = Easing.Linear)
        {
            if (time < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(time));
            Time = time;
            Value = value;
            Easing = easing;
        }
        public TimeSpan Time { get; }
        public T Value { get; }
        public Easing Easing { get; }
    }

    public interface IStoryboardTimeline
    {
        TimeSpan Duration { get; }
        IActiveTimeline Activate(NameScope scope);
    }

    public abstract class Timeline<T> : IStoryboardTimeline
    {
        protected Timeline(string targetName, XamlProperty<T> property)
        {
            TargetName = string.IsNullOrWhiteSpace(targetName) ? throw new ArgumentException("A target name is required.", nameof(targetName)) : targetName;
            Property = property ?? throw new ArgumentNullException(nameof(property));
        }

        public string TargetName { get; }
        public XamlProperty<T> Property { get; }
        public IList<KeyFrame<T>> KeyFrames { get; } = new List<KeyFrame<T>>();
        public void AddKeyFrame(KeyFrame<T> keyFrame) => KeyFrames.Add(keyFrame ?? throw new ArgumentNullException(nameof(keyFrame)));
        public TimeSpan Duration
        {
            get
            {
                var duration = TimeSpan.Zero;
                foreach (var keyFrame in KeyFrames) if (keyFrame.Time > duration) duration = keyFrame.Time;
                return duration;
            }
        }

        public IActiveTimeline Activate(NameScope scope)
        {
            var target = scope.Find(TargetName) ?? throw new InvalidOperationException($"Storyboard target '{TargetName}' was not found.");
            if (KeyFrames.Count == 0) throw new InvalidOperationException("A timeline requires at least one keyframe.");
            var sorted = new List<KeyFrame<T>>(KeyFrames);
            sorted.Sort((left, right) => left.Time.CompareTo(right.Time));
            return new ActiveTimeline<T>(target, Property, sorted, Interpolate);
        }

        protected abstract T Interpolate(T from, T to, float progress);
    }

    public sealed class FloatTimeline : Timeline<float>
    {
        public FloatTimeline(string targetName, XamlProperty<float> property) : base(targetName, property) { }
        protected override float Interpolate(float from, float to, float progress) => from + (to - from) * progress;
    }

    public sealed class ColorTimeline : Timeline<Color>
    {
        public ColorTimeline(string targetName, XamlProperty<Color> property) : base(targetName, property) { }
        protected override Color Interpolate(Color from, Color to, float progress) => Color.Lerp(from, to, progress);
    }

    public sealed class Vector2Timeline : Timeline<Vector2>
    {
        public Vector2Timeline(string targetName, XamlProperty<Vector2> property) : base(targetName, property) { }
        protected override Vector2 Interpolate(Vector2 from, Vector2 to, float progress) => Vector2.Lerp(from, to, progress);
    }

    public sealed class ThicknessTimeline : Timeline<Thickness>
    {
        public ThicknessTimeline(string targetName, XamlProperty<Thickness> property) : base(targetName, property) { }
        protected override Thickness Interpolate(Thickness from, Thickness to, float progress) => new Thickness(
            from.Left + (to.Left - from.Left) * progress,
            from.Top + (to.Top - from.Top) * progress,
            from.Right + (to.Right - from.Right) * progress,
            from.Bottom + (to.Bottom - from.Bottom) * progress);
    }

    public static class CompiledTimeline
    {
        public static Timeline<T> Create<T>(string timelineTypeName, string targetName, XamlProperty<T> property)
        {
            if (timelineTypeName == "FloatTimeline" && typeof(T) == typeof(float))
                return (Timeline<T>)(object)new FloatTimeline(targetName, (XamlProperty<float>)(object)property);
            if (timelineTypeName == "ColorTimeline" && typeof(T) == typeof(Color))
                return (Timeline<T>)(object)new ColorTimeline(targetName, (XamlProperty<Color>)(object)property);
            if (timelineTypeName == "Vector2Timeline" && typeof(T) == typeof(Vector2))
                return (Timeline<T>)(object)new Vector2Timeline(targetName, (XamlProperty<Vector2>)(object)property);
            if (timelineTypeName == "ThicknessTimeline" && typeof(T) == typeof(Thickness))
                return (Timeline<T>)(object)new ThicknessTimeline(targetName, (XamlProperty<Thickness>)(object)property);
            throw new InvalidOperationException($"Timeline '{timelineTypeName}' cannot animate '{typeof(T).FullName}'.");
        }
    }

    public interface IActiveTimeline : IDisposable
    {
        void Apply(TimeSpan time);
    }

    internal sealed class ActiveTimeline<T> : IActiveTimeline
    {
        private readonly object _target;
        private readonly XamlProperty<T> _property;
        private readonly IReadOnlyList<KeyFrame<T>> _keyFrames;
        private readonly Func<T, T, float, T> _interpolate;
        private XamlValueContribution<T> _value;

        public ActiveTimeline(object target, XamlProperty<T> property, IReadOnlyList<KeyFrame<T>> keyFrames, Func<T, T, float, T> interpolate)
        {
            _target = target;
            _property = property;
            _keyFrames = keyFrames;
            _interpolate = interpolate;
        }

        public void Apply(TimeSpan time)
        {
            var value = Sample(time);
            if (_value == null) _value = XamlValues.Set(_target, _property, XamlValueLayer.Animation, value);
            else _value.Value = value;
        }

        private T Sample(TimeSpan time)
        {
            if (time <= _keyFrames[0].Time) return _keyFrames[0].Value;
            for (var index = 1; index < _keyFrames.Count; index++)
            {
                var current = _keyFrames[index];
                if (time > current.Time) continue;
                var previous = _keyFrames[index - 1];
                var duration = (current.Time - previous.Time).TotalSeconds;
                var progress = duration <= 0 ? 1f : (float)((time - previous.Time).TotalSeconds / duration);
                return _interpolate(previous.Value, current.Value, ApplyEasing(MathHelper.Clamp(progress, 0, 1), current.Easing));
            }
            return _keyFrames[_keyFrames.Count - 1].Value;
        }

        private static float ApplyEasing(float value, Easing easing) => easing switch
        {
            Easing.CubicIn => value * value * value,
            Easing.CubicOut => 1f - MathF.Pow(1f - value, 3),
            Easing.CubicInOut => value < .5f ? 4f * value * value * value : 1f - MathF.Pow(-2f * value + 2f, 3) / 2f,
            _ => value,
        };

        public void Dispose()
        {
            _value?.Dispose();
            _value = null;
        }
    }

    public sealed class Storyboard
    {
        private readonly ConditionalWeakTable<Control, List<StoryboardClock>> _clocks = new ConditionalWeakTable<Control, List<StoryboardClock>>();

        public IList<IStoryboardTimeline> Timelines { get; } = new List<IStoryboardTimeline>();
        public RepeatBehavior RepeatBehavior { get; set; } = RepeatBehavior.Once;
        public bool AutoReverse { get; set; }
        public FillBehavior FillBehavior { get; set; } = FillBehavior.Stop;
        public void AddTimeline(IStoryboardTimeline timeline) => Timelines.Add(timeline ?? throw new ArgumentNullException(nameof(timeline)));

        public StoryboardClock Begin(Control root)
        {
            var scope = NameScope.GetNameScope(root) ?? throw new InvalidOperationException("The storyboard root has no namescope.");
            var clocks = _clocks.GetOrCreateValue(root);
            foreach (var existing in clocks.ToArray()) existing.Stop();
            var clock = new StoryboardClock(this, scope, root);
            try
            {
                clocks.Add(clock);
                XamlAttachment.RegisterActivationDisposable(root, clock);
                XamlAttachment.RegisterUpdateParticipant(root, clock);
                return clock;
            }
            catch
            {
                clock.Dispose();
                throw;
            }
        }

        public void Stop(Control root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (!_clocks.TryGetValue(root, out var clocks)) return;
            foreach (var clock in clocks.ToArray()) clock.Stop();
        }

        internal void Unregister(Control root, StoryboardClock clock)
        {
            if (_clocks.TryGetValue(root, out var clocks)) clocks.Remove(clock);
        }
    }

    public sealed class StoryboardClock : IXamlUpdateParticipant, IDisposable
    {
        private readonly Storyboard _storyboard;
        private readonly Control _root;
        private readonly List<IActiveTimeline> _timelines = new List<IActiveTimeline>();
        private readonly TimeSpan _duration;
        private TimeSpan _elapsed;
        private bool _disposed;

        internal StoryboardClock(Storyboard storyboard, NameScope scope, Control root)
        {
            _storyboard = storyboard;
            _root = root;
            try
            {
                foreach (var timeline in storyboard.Timelines)
                {
                    _timelines.Add(timeline.Activate(scope));
                    if (timeline.Duration > _duration) _duration = timeline.Duration;
                }
                Apply(TimeSpan.Zero);
            }
            catch
            {
                DisposeTimelines();
                throw;
            }
        }

        public bool IsRunning { get; private set; } = true;

        public void Update(GameTime gameTime)
        {
            if (!IsRunning || _disposed) return;
            _elapsed += gameTime?.ElapsedGameTime ?? TimeSpan.Zero;
            var totalCycles = _storyboard.RepeatBehavior.Forever ? double.PositiveInfinity : _storyboard.RepeatBehavior.Count;
            var cycleDuration = _storyboard.AutoReverse ? _duration + _duration : _duration;
            if (cycleDuration <= TimeSpan.Zero)
            {
                Complete();
                return;
            }
            var totalDuration = TimeSpan.FromTicks((long)(cycleDuration.Ticks * Math.Min(totalCycles, long.MaxValue / (double)cycleDuration.Ticks)));
            if (!_storyboard.RepeatBehavior.Forever && _elapsed >= totalDuration)
            {
                var finalTime = _storyboard.AutoReverse ? TimeSpan.Zero : _duration;
                Apply(finalTime);
                Complete();
                return;
            }

            var cycleTicks = _elapsed.Ticks % cycleDuration.Ticks;
            var local = TimeSpan.FromTicks(cycleTicks);
            if (_storyboard.AutoReverse && local > _duration) local = _duration - (local - _duration);
            Apply(local);
        }

        public void Stop()
        {
            if (_disposed) return;
            IsRunning = false;
            ExceptionDispatchInfo failure = null;
            try { DisposeTimelines(); }
            catch (Exception exception) { failure = ExceptionDispatchInfo.Capture(exception); }
            _storyboard.Unregister(_root, this);
            XamlAttachment.UnregisterActivation(_root, this, this);
            failure?.Throw();
        }

        private void Complete()
        {
            IsRunning = false;
            if (_storyboard.FillBehavior == FillBehavior.Stop)
            {
                Dispose();
                return;
            }
            XamlAttachment.UnregisterActivation(_root, null, this);
        }

        private void Apply(TimeSpan time)
        {
            foreach (var timeline in _timelines) timeline.Apply(time);
        }

        private void DisposeTimelines()
        {
            ExceptionDispatchInfo failure = null;
            foreach (var timeline in _timelines)
            {
                try { timeline.Dispose(); }
                catch (Exception exception) { failure ??= ExceptionDispatchInfo.Capture(exception); }
            }
            _timelines.Clear();
            failure?.Throw();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            IsRunning = false;
            ExceptionDispatchInfo failure = null;
            try { DisposeTimelines(); }
            catch (Exception exception) { failure = ExceptionDispatchInfo.Capture(exception); }
            _storyboard.Unregister(_root, this);
            XamlAttachment.UnregisterActivation(_root, this, this);
            failure?.Throw();
        }
    }

    public static class StoryboardTriggers
    {
        public enum PseudoState { Hover, Focus, Disabled, Pressed, Checked }

        public static IDisposable AttachEvent<THandler>(Control root, Action<THandler> add, Action<THandler> remove, Func<Action, THandler> createHandler, Storyboard storyboard)
            where THandler : Delegate
        {
            return XamlAttachment.RegisterReactivatable(root, () =>
            {
                StoryboardClock clock = null;
                var subscription = BindingSubscriptions.Event(add, remove, createHandler(() =>
                {
                    clock?.Stop();
                    clock = storyboard.Begin(root);
                }));
                return BindingSubscriptions.Combine(subscription, new ActionDisposable(() => clock?.Stop()));
            });
        }

        public static IDisposable AttachProperty<TSource, TValue>(Control root, CompiledBindingPath<TSource, TValue> path, TSource source, TValue expected, Storyboard storyboard)
            where TSource : class
        {
            return XamlAttachment.RegisterReactivatable(root, () =>
            {
                StoryboardClock clock = null;
                Action update = () =>
                {
                    var current = path.Read(source);
                    var matches = current.HasValue && EqualityComparer<TValue>.Default.Equals(current.Value, expected);
                    if (matches && clock == null) clock = storyboard.Begin(root);
                    else if (!matches && clock != null) { clock.Stop(); clock = null; }
                };
                var subscription = path.Subscribe?.Invoke(source, update);
                try
                {
                    update();
                    return BindingSubscriptions.Combine(subscription, new ActionDisposable(() => clock?.Stop()));
                }
                catch
                {
                    try { subscription?.Dispose(); }
                    catch { }
                    try { clock?.Stop(); }
                    catch { }
                    throw;
                }
            });
        }

        public static IDisposable AttachPseudoState(Control root, Control target, PseudoState state, Storyboard storyboard)
        {
            return XamlAttachment.RegisterReactivatable(root, () =>
            {
                StoryboardClock clock = null;
                var hovered = false;
                var focused = false;
                Func<bool> matches = state switch
                {
                    PseudoState.Hover => () => hovered,
                    PseudoState.Focus => () => focused,
                    PseudoState.Disabled => () => !target.Enabled,
                    PseudoState.Pressed when target is BaseButton button => () => button.IsVisuallyPressed,
                    PseudoState.Checked when target is BaseButton button => () => button.ButtonPressed,
                    PseudoState.Pressed or PseudoState.Checked => throw new InvalidOperationException($"Pseudo state '{state}' requires a BaseButton target."),
                    _ => throw new ArgumentOutOfRangeException(nameof(state)),
                };
                Action update = () =>
                {
                    if (matches() && clock == null) clock = storyboard.Begin(root);
                    else if (!matches() && clock != null) { clock.Stop(); clock = null; }
                };
                EventHandler entered = (_, _) => { hovered = true; update(); };
                EventHandler exited = (_, _) => { hovered = false; update(); };
                EventHandler focusedHandler = (_, _) => { focused = true; update(); };
                EventHandler unfocused = (_, _) => { focused = false; update(); };
                EventHandler changed = (_, _) => update();
                target.MouseEntered += entered;
                target.MouseExited += exited;
                target.FocusEntered += focusedHandler;
                target.FocusExited += unfocused;
                target.EnabledChanged += changed;
                IDisposable buttonSubscription = null;
                if (target is BaseButton targetButton)
                {
                    Action<BaseButton, bool> toggled = (_, _) => update();
                    targetButton.ButtonDown += changed;
                    targetButton.ButtonUp += changed;
                    targetButton.Toggled += toggled;
                    buttonSubscription = new ActionDisposable(() =>
                    {
                        targetButton.ButtonDown -= changed;
                        targetButton.ButtonUp -= changed;
                        targetButton.Toggled -= toggled;
                    });
                }
                var targetSubscription = new ActionDisposable(() =>
                {
                    target.MouseEntered -= entered;
                    target.MouseExited -= exited;
                    target.FocusEntered -= focusedHandler;
                    target.FocusExited -= unfocused;
                    target.EnabledChanged -= changed;
                });
                try
                {
                    update();
                    return BindingSubscriptions.Combine(
                        targetSubscription,
                        buttonSubscription,
                        new ActionDisposable(() => clock?.Stop()));
                }
                catch
                {
                    try { buttonSubscription?.Dispose(); }
                    catch { }
                    try { targetSubscription.Dispose(); }
                    catch { }
                    try { clock?.Stop(); }
                    catch { }
                    throw;
                }
            });
        }

        private sealed class ActionDisposable : IDisposable
        {
            private Action _dispose;
            public ActionDisposable(Action dispose) => _dispose = dispose;
            public void Dispose() { var dispose = _dispose; _dispose = null; dispose?.Invoke(); }
        }
    }

    public static class CompiledStoryboardTrigger
    {
        public static IDisposable AttachEvent<TTarget, THandler>(
            Control root,
            TTarget target,
            Action<TTarget, THandler> add,
            Action<TTarget, THandler> remove,
            Func<Action, THandler> createHandler,
            Storyboard storyboard)
            where TTarget : class
            where THandler : Delegate
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (add == null) throw new ArgumentNullException(nameof(add));
            if (remove == null) throw new ArgumentNullException(nameof(remove));
            return StoryboardTriggers.AttachEvent(
                root,
                handler => add(target, handler),
                handler => remove(target, handler),
                createHandler,
                storyboard);
        }

        public static IDisposable AttachProperty<TSource, TValue>(
            Control root,
            Func<TSource, TValue> read,
            string propertyName,
            TValue expected,
            Storyboard storyboard)
            where TSource : class
        {
            return XamlAttachment.RegisterReactivatable(root, () =>
                new CompiledPropertyStoryboardTrigger<TSource, TValue>(root, read, propertyName, expected, storyboard));
        }

        public static IDisposable AttachStopEvent<TTarget, THandler>(
            Control root,
            TTarget target,
            Action<TTarget, THandler> add,
            Action<TTarget, THandler> remove,
            Func<Action, THandler> createHandler,
            Storyboard storyboard)
            where TTarget : class
            where THandler : Delegate
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            return XamlAttachment.RegisterReactivatable(root, () => BindingSubscriptions.Event(
                handler => add(target, handler),
                handler => remove(target, handler),
                createHandler(() => storyboard.Stop(root))));
        }
    }

    internal sealed class CompiledPropertyStoryboardTrigger<TSource, TValue> : IDisposable where TSource : class
    {
        private readonly Control _root;
        private readonly Func<TSource, TValue> _read;
        private readonly string _propertyName;
        private readonly TValue _expected;
        private readonly Storyboard _storyboard;
        private IDisposable _subscription;
        private TSource _source;
        private StoryboardClock _clock;
        private bool _disposed;

        public CompiledPropertyStoryboardTrigger(Control root, Func<TSource, TValue> read, string propertyName, TValue expected, Storyboard storyboard)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _read = read ?? throw new ArgumentNullException(nameof(read));
            _propertyName = string.IsNullOrWhiteSpace(propertyName) ? throw new ArgumentException("A source property is required.", nameof(propertyName)) : propertyName;
            _expected = expected;
            _storyboard = storyboard ?? throw new ArgumentNullException(nameof(storyboard));
            try
            {
                root.DataContextChanged += DataContextChanged;
                AttachSource(root.DataContext as TSource);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void DataContextChanged(object sender, DataContextChangedEventArgs args) => AttachSource(args.CurrentValue as TSource);

        private void AttachSource(TSource source)
        {
            _subscription?.Dispose();
            _subscription = null;
            _source = source;
            if (source is System.ComponentModel.INotifyPropertyChanged notifications)
                _subscription = BindingSubscriptions.PropertyChanged(notifications, _propertyName, Update);
            Update();
        }

        private void Update()
        {
            if (_disposed) return;
            var matches = _source != null && EqualityComparer<TValue>.Default.Equals(_read(_source), _expected);
            if (matches && _clock == null) _clock = _storyboard.Begin(_root);
            else if (!matches && _clock != null)
            {
                _clock.Stop();
                _clock = null;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _root.DataContextChanged -= DataContextChanged;
            _subscription?.Dispose();
            _subscription = null;
            _source = null;
            _clock?.Stop();
            _clock = null;
        }
    }
}