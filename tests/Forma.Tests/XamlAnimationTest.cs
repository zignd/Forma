using System;
using System.ComponentModel;
using Forma.Xaml;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Forma.Tests
{
    public class XamlAnimationTest
    {
        private static readonly XamlProperty<Vector2> PositionProperty = new XamlProperty<Vector2>(
            nameof(Control.Position), target => ((Control)target).Position, (target, value) => ((Control)target).Position = value);

        [Test]
        public void Storyboard_AdvancesDeterministicallyAndRestoresUnderlyingValue()
        {
            using var context = new UIContext();
            var root = CreateNamedRoot(out var target);
            var storyboard = CreatePositionStoryboard(FillBehavior.Stop);
            storyboard.Begin(root);
            context.Add(root);

            context.Update(Time(TimeSpan.FromMilliseconds(500)), new MouseState(), new KeyboardState());
            Assert.That(target.Position.X, Is.EqualTo(5).Within(.001));
            context.Update(Time(TimeSpan.FromMilliseconds(500)), new MouseState(), new KeyboardState());
            Assert.That(target.Position, Is.EqualTo(new Vector2(2, 0)));
        }

        [Test]
        public void Storyboard_HoldEndAutoReverseAndStopUseAnimationLayer()
        {
            using var context = new UIContext();
            var root = CreateNamedRoot(out var target);
            var storyboard = CreatePositionStoryboard(FillBehavior.HoldEnd);
            storyboard.AutoReverse = true;
            var clock = storyboard.Begin(root);
            context.Add(root);

            context.Update(Time(TimeSpan.FromMilliseconds(1500)), new MouseState(), new KeyboardState());
            Assert.That(target.Position.X, Is.EqualTo(5).Within(.001));
            context.Update(Time(TimeSpan.FromMilliseconds(500)), new MouseState(), new KeyboardState());
            Assert.That(target.Position.X, Is.EqualTo(0).Within(.001));
            clock.Stop();
            Assert.That(target.Position, Is.EqualTo(new Vector2(2, 0)));
        }

        [Test]
        public void PropertyTrigger_StartsAndStopsStoryboard()
        {
            using var context = new UIContext();
            var root = CreateNamedRoot(out var target);
            var viewModel = new TriggerViewModel();
            var storyboard = CreatePositionStoryboard(FillBehavior.Stop);
            var path = new CompiledBindingPath<TriggerViewModel, bool>(
                source => BindingValue<bool>.FromValue(source.Active),
                (source, update) => BindingSubscriptions.PropertyChanged(source, nameof(TriggerViewModel.Active), update));
            StoryboardTriggers.AttachProperty(root, path, viewModel, true, storyboard);
            context.Add(root);
            viewModel.Active = true;
            context.Update(Time(TimeSpan.FromMilliseconds(500)), new MouseState(), new KeyboardState());
            Assert.That(target.Position.X, Is.EqualTo(5).Within(.001));
            viewModel.Active = false;
            Assert.That(target.Position, Is.EqualTo(new Vector2(2, 0)));
        }

        [Test]
        public void EventTrigger_StartsStoryboard()
        {
            using var context = new UIContext();
            var root = CreateNamedRoot(out var target);
            StoryboardTriggers.AttachEvent<EventHandler>(root, handler => target.Attached += handler, handler => target.Attached -= handler, update => (_, _) => update(), CreatePositionStoryboard(FillBehavior.Stop));
            context.Add(root);
            context.Update(Time(TimeSpan.FromMilliseconds(500)), new MouseState(), new KeyboardState());
            Assert.That(target.Position.X, Is.EqualTo(5).Within(.001));
        }

        [Test]
        public void PseudoStateTrigger_StartsAndStopsStoryboard()
        {
            using var context = new UIContext();
            var root = CreateNamedRoot(out var target);
            StoryboardTriggers.AttachPseudoState(root, target, StoryboardTriggers.PseudoState.Disabled, CreatePositionStoryboard(FillBehavior.Stop));
            context.Add(root);
            target.Enabled = false;
            context.Update(Time(TimeSpan.FromMilliseconds(500)), new MouseState(), new KeyboardState());
            Assert.That(target.Position.X, Is.EqualTo(5).Within(.001));
            target.Enabled = true;
            Assert.That(target.Position, Is.EqualTo(new Vector2(2, 0)));
        }

        [Test]
        public void Storyboard_RepeatAndEasingAreDeterministic()
        {
            using var context = new UIContext();
            var root = CreateNamedRoot(out var target);
            var timeline = new Vector2Timeline("Target", PositionProperty);
            timeline.KeyFrames.Add(new KeyFrame<Vector2>(TimeSpan.Zero, Vector2.Zero));
            timeline.KeyFrames.Add(new KeyFrame<Vector2>(TimeSpan.FromSeconds(1), new Vector2(10, 0), Easing.CubicIn));
            var storyboard = new Storyboard { RepeatBehavior = RepeatBehavior.ForCount(2) };
            storyboard.Timelines.Add(timeline);
            storyboard.Begin(root);
            context.Add(root);

            context.Update(Time(TimeSpan.FromMilliseconds(1500)), new MouseState(), new KeyboardState());
            Assert.That(target.Position.X, Is.EqualTo(1.25).Within(.001));
            context.Update(Time(TimeSpan.FromMilliseconds(500)), new MouseState(), new KeyboardState());
            Assert.That(target.Position, Is.EqualTo(new Vector2(2, 0)));
        }

        [Test]
        public void Storyboard_RejectsUnknownTargetName()
        {
            var root = CreateNamedRoot(out _);
            var timeline = new FloatTimeline("Missing", new XamlProperty<float>("Value", _ => 0, (_, _) => { }));
            timeline.KeyFrames.Add(new KeyFrame<float>(TimeSpan.Zero, 0));
            var storyboard = new Storyboard();
            storyboard.Timelines.Add(timeline);
            Assert.That(() => storyboard.Begin(root), Throws.InvalidOperationException.With.Message.Contains("Missing"));
        }

        [Test]
        public void TypedTimelinesInterpolateColorFloatVectorAndThickness()
        {
            var target = new TimelineTarget { Scalar = 2, Color = Color.Black, Thickness = Thickness.Zero };
            var scope = new NameScope();
            scope.Register("Target", target);
            AssertTimeline(new FloatTimeline("Target", new XamlProperty<float>(nameof(TimelineTarget.Scalar), value => ((TimelineTarget)value).Scalar, (value, current) => ((TimelineTarget)value).Scalar = current)), 0f, 10f, scope, () => target.Scalar, 5f);
            AssertTimeline(new ColorTimeline("Target", new XamlProperty<Color>(nameof(TimelineTarget.Color), value => ((TimelineTarget)value).Color, (value, current) => ((TimelineTarget)value).Color = current)), Color.Black, Color.White, scope, () => target.Color, new Color(127, 127, 127));
            AssertTimeline(new ThicknessTimeline("Target", new XamlProperty<Thickness>(nameof(TimelineTarget.Thickness), value => ((TimelineTarget)value).Thickness, (value, current) => ((TimelineTarget)value).Thickness = current)), Thickness.Zero, new Thickness(10), scope, () => target.Thickness, new Thickness(5));
        }

        private static void AssertTimeline<T>(Timeline<T> timeline, T from, T to, NameScope scope, Func<T> read, T expected)
        {
            timeline.KeyFrames.Add(new KeyFrame<T>(TimeSpan.Zero, from));
            timeline.KeyFrames.Add(new KeyFrame<T>(TimeSpan.FromSeconds(1), to));
            using var active = timeline.Activate(scope);
            active.Apply(TimeSpan.FromMilliseconds(500));
            Assert.That(read(), Is.EqualTo(expected));
        }

        private static Control CreateNamedRoot(out Control target)
        {
            var root = new Control();
            target = new Control { Position = new Vector2(2, 0) };
            root.AddChild(target);
            var scope = new NameScope();
            scope.Register("Target", target);
            NameScope.SetNameScope(root, scope);
            return root;
        }

        private static Storyboard CreatePositionStoryboard(FillBehavior fill)
        {
            var timeline = new Vector2Timeline("Target", PositionProperty);
            timeline.KeyFrames.Add(new KeyFrame<Vector2>(TimeSpan.Zero, Vector2.Zero));
            timeline.KeyFrames.Add(new KeyFrame<Vector2>(TimeSpan.FromSeconds(1), new Vector2(10, 0)));
            var storyboard = new Storyboard { FillBehavior = fill };
            storyboard.Timelines.Add(timeline);
            return storyboard;
        }

        private static GameTime Time(TimeSpan elapsed) => new GameTime(elapsed, elapsed);

        private sealed class TriggerViewModel : INotifyPropertyChanged
        {
            private bool _active;
            public event PropertyChangedEventHandler PropertyChanged;
            public bool Active { get => _active; set { if (_active == value) return; _active = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Active))); } }
        }

        private sealed class TimelineTarget
        {
            public float Scalar { get; set; }
            public Color Color { get; set; }
            public Thickness Thickness { get; set; }
        }
    }
}