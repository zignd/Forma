// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.Globalization;
using System.ComponentModel;
using Forma.Xaml;
using Microsoft.Xna.Framework;

namespace Forma.Tests
{
    public class XamlBindingTest
    {
        [Test]
        public void OneWayBinding_UsesTypedPathFormattingAndDisposesWithTree()
        {
            using var context = new UIContext();
            var label = new Label();
            var viewModel = new BindingViewModel { Score = 2 };
            label.DataContext = viewModel;
            var path = new CompiledBindingPath<BindingViewModel, int>(
                source => BindingValue<int>.FromValue(source.Score),
                (source, update) => BindingSubscriptions.PropertyChanged(source, nameof(BindingViewModel.Score), update));
            var adapter = new BindingTargetAdapter<string>(
                new XamlProperty<string>(nameof(Label.Text), target => ((Label)target).Text, (target, value) => ((Label)target).Text = value));
            CompiledBinding.Attach(label, label, path, adapter, score => $"Score: {score}");

            context.Add(label);
            Assert.That(label.Text, Is.EqualTo("Score: 2"));
            viewModel.Score = 3;
            Assert.That(label.Text, Is.EqualTo("Score: 3"));

            context.Remove(label);
            viewModel.Score = 4;
            Assert.That(label.Text, Is.EqualTo(string.Empty));
        }

        [Test]
        public void TwoWayBinding_UpdatesSourceWithoutFeedbackLoops()
        {
            using var context = new UIContext();
            var edit = new LineEdit();
            var viewModel = new BindingViewModel { Name = "Ada" };
            edit.DataContext = viewModel;
            var path = new CompiledBindingPath<BindingViewModel, string>(
                source => BindingValue<string>.FromValue(source.Name),
                (source, update) => BindingSubscriptions.PropertyChanged(source, nameof(BindingViewModel.Name), update),
                (source, value) => source.Name = value);
            CompiledBinding.Attach(
                edit,
                edit,
                path,
                BindingTargetAdapters.LineEditText,
                value => value,
                value => value,
                new BindingOptions<string> { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

            context.Add(edit);
            Assert.That(edit.Text, Is.EqualTo("Ada"));
            edit.Text = "Grace";
            Assert.That(viewModel.Name, Is.EqualTo("Grace"));
            viewModel.Name = "Lin";
            Assert.That(edit.Text, Is.EqualTo("Lin"));
        }

        [Test]
        public void Binding_UsesTargetNullAndFallbackValues()
        {
            var label = new Label();
            var viewModel = new BindingViewModel { Name = null };
            label.DataContext = viewModel;
            var path = new CompiledBindingPath<BindingViewModel, string>(source => BindingValue<string>.FromValue(source.Name));
            var adapter = new BindingTargetAdapter<string>(
                new XamlProperty<string>(nameof(Label.Text), target => ((Label)target).Text, (target, value) => ((Label)target).Text = value));
            CompiledBinding.Attach(
                label,
                label,
                path,
                adapter,
                value => value,
                options: new BindingOptions<string>
                {
                    HasFallbackValue = true,
                    FallbackValue = "fallback",
                    HasTargetNullValue = true,
                    TargetNullValue = "guest",
                });
            Assert.That(label.Text, Is.EqualTo("guest"));

            label.DataContext = new object();
            Assert.That(label.Text, Is.EqualTo("fallback"));
        }

        [Test]
        public void TwoWayBinding_RejectsReadOnlyPaths()
        {
            var edit = new LineEdit { DataContext = new BindingViewModel() };
            var path = new CompiledBindingPath<BindingViewModel, string>(source => BindingValue<string>.FromValue(source.Name));
            Assert.Throws<ArgumentException>(() => CompiledBinding.Attach(
                edit,
                edit,
                path,
                BindingTargetAdapters.LineEditText,
                value => value,
                value => value,
                new BindingOptions<string> { Mode = BindingMode.TwoWay }));
        }

        [Test]
        public void OneTimeBinding_DoesNotObserveSourceOrDataContextChanges()
        {
            var label = new Label { DataContext = new BindingViewModel { Score = 1 } };
            var path = new CompiledBindingPath<BindingViewModel, int>(
                source => BindingValue<int>.FromValue(source.Score),
                (source, update) => BindingSubscriptions.PropertyChanged(source, nameof(BindingViewModel.Score), update));
            var adapter = new BindingTargetAdapter<string>(
                new XamlProperty<string>(nameof(Label.Text), target => ((Label)target).Text, (target, value) => ((Label)target).Text = value));
            CompiledBinding.Attach(label, label, path, adapter, value => value.ToString(), options: new BindingOptions<string> { Mode = BindingMode.OneTime });

            ((BindingViewModel)label.DataContext).Score = 2;
            label.DataContext = new BindingViewModel { Score = 3 };
            Assert.That(label.Text, Is.EqualTo("1"));
        }

        [Test]
        public void LostFocusBinding_CommitsOnlyWhenFocusLeavesTarget()
        {
            using var context = new UIContext();
            var root = new Control();
            var edit = new LineEdit { DataContext = new BindingViewModel { Name = "start" } };
            var other = new LineEdit();
            root.AddChild(edit);
            root.AddChild(other);
            var path = new CompiledBindingPath<BindingViewModel, string>(
                source => BindingValue<string>.FromValue(source.Name),
                (source, update) => BindingSubscriptions.PropertyChanged(source, nameof(BindingViewModel.Name), update),
                (source, value) => source.Name = value);
            CompiledBinding.Attach(edit, edit, path, BindingTargetAdapters.LineEditText, value => value, value => value,
                new BindingOptions<string> { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus });
            context.Add(root);
            context.SetFocus(edit);

            edit.Text = "pending";
            Assert.That(((BindingViewModel)edit.DataContext).Name, Is.EqualTo("start"));
            context.SetFocus(other);
            Assert.That(((BindingViewModel)edit.DataContext).Name, Is.EqualTo("pending"));
        }

        [Test]
        public void ExplicitTargetAdapters_ExposeExpectedPropertiesAndEvents()
        {
            var slider = new HSlider();
            var checkBox = new CheckBox();
            var option = new OptionButton();
            option.AddItem("A");
            option.AddItem("B");
            var sliderChanges = 0;
            var checkChanges = 0;
            var optionChanges = 0;
            using var sliderSubscription = BindingTargetAdapters.RangeValue.SubscribeChanged(slider, () => sliderChanges++);
            using var checkSubscription = BindingTargetAdapters.CheckBoxChecked.SubscribeChanged(checkBox, () => checkChanges++);
            using var optionSubscription = BindingTargetAdapters.OptionButtonSelected.SubscribeChanged(option, () => optionChanges++);

            BindingTargetAdapters.RangeValue.Property.SetValue(slider, 25);
            BindingTargetAdapters.CheckBoxChecked.Property.SetValue(checkBox, true);
            option.Select(1, true);

            Assert.That(sliderChanges, Is.EqualTo(1));
            Assert.That(checkChanges, Is.EqualTo(1));
            Assert.That(optionChanges, Is.EqualTo(1));
            Assert.That(BindingTargetAdapters.OptionButtonSelected.Property.GetValue(option), Is.EqualTo(1));
        }

        [Test]
        public void Control_PropertyChangedEmitsOnlyForRealTemplateFacingChanges()
        {
            var control = new Control();
            var changes = new List<string>();
            control.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

            control.Name = "Root";
            control.Name = "Root";
            control.Enabled = false;
            control.Enabled = false;
            control.FontSize = 18;
            control.FontSize = 18;

            Assert.That(changes, Is.EqualTo(new[]
            {
                nameof(Control.Name),
                nameof(Control.Enabled),
                nameof(Control.IsEffectivelyEnabled),
                nameof(Control.FontSize),
            }));
        }

        [Test]
        public void Control_PropertyChangedCoversAliasesInputRenderingLayoutAndClasses()
        {
            var control = new Control();
            var changes = new List<string>();
            control.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

            control.Visibility = Visibility.Hidden;
            control.Visibility = Visibility.Hidden;
            control.Visible = true;
            control.MouseFilter = MouseFilter.Pass;
            control.MouseFilter = MouseFilter.Pass;
            control.ClipToBounds = true;
            control.ClipToBounds = true;
            control.Margin = new Thickness(1);
            control.Margins = new Thickness(1);
            control.Width = 40;
            control.Width = 40;
            control.ZIndex = 2;
            control.ZIndex = 2;
            control.Classes.Add("selected");
            control.Classes.Add("selected");

            Assert.That(changes, Is.EqualTo(new[]
            {
                nameof(Control.Visibility),
                nameof(Control.Visibility),
                nameof(Control.MouseFilter),
                nameof(Control.ClipToBounds),
                nameof(Control.Margins),
                nameof(Control.Margin),
                nameof(Control.Width),
                nameof(Control.ZIndex),
                nameof(Control.Classes),
            }));
        }

        [Test]
        public void Control_InheritedEffectivePropertiesNotifyOnlyAffectedDescendants()
        {
            var root = new Control();
            var inherited = new Control();
            var overridden = new Control { FontSize = 24, Cursor = Cursor.IBeam };
            root.AddChild(inherited);
            root.AddChild(overridden);
            var inheritedChanges = new List<string>();
            var overriddenChanges = new List<string>();
            inherited.PropertyChanged += (_, args) => inheritedChanges.Add(args.PropertyName);
            overridden.PropertyChanged += (_, args) => overriddenChanges.Add(args.PropertyName);

            root.FontSize = 18;
            root.Foreground = Color.Red;
            root.Cursor = Cursor.Crosshair;
            root.PixelSnapping = PixelSnapping.Disabled;
            root.Enabled = false;

            Assert.That(inheritedChanges, Is.EquivalentTo(new[]
            {
                nameof(Control.FontSize),
                nameof(Control.Foreground),
                nameof(Control.EffectiveCursor),
                nameof(Control.IsPixelSnappingEnabled),
                nameof(Control.IsEffectivelyEnabled),
            }));
            Assert.That(overriddenChanges, Is.EquivalentTo(new[]
            {
                nameof(Control.Foreground),
                nameof(Control.IsPixelSnappingEnabled),
                nameof(Control.IsEffectivelyEnabled),
            }));
        }

        [Test]
        public void Control_ReparentNotifiesChangedEffectiveInheritedValues()
        {
            var first = new Control { FontSize = 12, Language = "en", Cursor = Cursor.Arrow };
            var second = new Control { FontSize = 20, Language = "fr", Cursor = Cursor.IBeam };
            var child = new Control();
            first.AddChild(child);
            var changes = new List<string>();
            child.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

            second.AddChild(child);

            Assert.That(child.FontSize, Is.EqualTo(20));
            Assert.That(child.Language, Is.EqualTo("fr"));
            Assert.That(child.EffectiveCursor, Is.EqualTo(Cursor.IBeam));
            Assert.That(changes, Does.Contain(nameof(Control.FontSize)));
            Assert.That(changes, Does.Contain(nameof(Control.Language)));
            Assert.That(changes, Does.Contain(nameof(Control.EffectiveCursor)));
        }

        [Test]
        public void Control_LayoutResolvedGeometryNotifiesOnlyOnActualChanges()
        {
            var parent = new Control { Size = new Vector2(100, 50) };
            var child = new Control();
            child.SetAnchorsAndOffsets(0, 0, 1, 1);
            parent.AddChild(child);
            var changes = new List<string>();
            child.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

            parent.LayoutTree();
            parent.LayoutTree();

            Assert.That(changes.Count(name => name == nameof(Control.Size)), Is.EqualTo(1));
            Assert.That(changes, Does.Not.Contain(nameof(Control.Position)));
        }

        [Test]
        public void Control_DataContextNotifiesForEqualDistinctSourcesAndLocalState()
        {
            var control = new Control();
            var first = new EqualSource(1);
            var second = new EqualSource(1);
            var changes = new List<string>();
            control.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
            control.DataContext = first;
            changes.Clear();

            control.DataContext = second;

            Assert.That(changes, Is.EqualTo(new[] { nameof(Control.DataContext) }));
            Assert.That(control.DataContext, Is.SameAs(second));
        }

        [Test]
        public void Control_ThemeStyleOverridesNotifyOnlyForRealDictionaryChanges()
        {
            var control = new Control();
            var style = new StyleBoxFlat();
            var changes = new List<string>();
            control.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

            control.ThemeStyleOverrides["panel"] = style;
            control.ThemeStyleOverrides["panel"] = style;
            control.ThemeStyleOverrides.Remove("missing");
            control.ThemeStyleOverrides.Remove("panel");

            Assert.That(changes, Is.EqualTo(new[]
            {
                nameof(Control.ThemeStyleOverrides),
                nameof(Control.ThemeStyleOverrides),
            }));
        }

        [Test]
        public void Control_ComputedGeometryNotifiesAffectedVisualSubtree()
        {
            var parent = new Control();
            var child = new Control { Position = new Vector2(5, 0), Size = new Vector2(10, 10) };
            parent.AddChild(child);
            var parentChanges = new List<string>();
            var childChanges = new List<string>();
            parent.PropertyChanged += (_, args) => parentChanges.Add(args.PropertyName);
            child.PropertyChanged += (_, args) => childChanges.Add(args.PropertyName);

            parent.Position = new Vector2(10, 0);

            Assert.That(parentChanges, Does.Contain(nameof(Control.Bounds)));
            Assert.That(parentChanges, Does.Contain(nameof(Control.GlobalPosition)));
            Assert.That(parentChanges, Does.Contain(nameof(Control.VisualBounds)));
            Assert.That(childChanges, Does.Contain(nameof(Control.GlobalPosition)));
            Assert.That(childChanges, Does.Contain(nameof(Control.VisualBounds)));
        }

        [Test]
        public void Control_ContextDirectionNotifiesOnlyWhenEffectiveDirectionChanges()
        {
            using var context = new UIContext();
            var root = new Control { LayoutDirection = LayoutDirection.ApplicationLocale };
            var child = new Control();
            root.AddChild(child);
            context.Add(root);
            var changes = new List<string>();
            child.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

            context.ApplicationCulture = CultureInfo.GetCultureInfo("en-US");
            Assert.That(changes, Does.Not.Contain(nameof(Control.EffectiveLayoutDirection)));
            context.ApplicationCulture = CultureInfo.GetCultureInfo("ar-SA");

            Assert.That(changes.Count(name => name == nameof(Control.EffectiveLayoutDirection)), Is.EqualTo(1));
            Assert.That(child.EffectiveLayoutDirection, Is.EqualTo(LayoutDirection.RightToLeft));
        }

        [Test]
        public void AncestorSourceBinding_RebindsWhenVisualParentChanges()
        {
            var first = new AncestorProbe { Caption = "first" };
            var second = new AncestorProbe { Caption = "second" };
            var label = new Label();
            var path = CreateAncestorPath();
            var adapter = CreateLabelTextAdapter();
            CompiledBinding.Attach(
                label,
                label,
                control => FindVisualAncestor<AncestorProbe>(control),
                path,
                adapter,
                value => value);

            first.AddChild(label);
            Assert.That(label.Text, Is.EqualTo("first"));
            second.AddChild(label);
            Assert.That(label.Text, Is.EqualTo("second"));
            second.Caption = "updated";
            Assert.That(label.Text, Is.EqualTo("updated"));
        }

        [Test]
        public void AncestorSourceBinding_RebindsProjectedVisualDescendantAndReleasesOldSource()
        {
            var logicalOwner = new Control();
            var projectedHost = new Control();
            var label = new Label();
            logicalOwner.AddChild(label);
            projectedHost.ProjectVisualChild(label);
            var first = new AncestorProbe { Caption = "first" };
            var second = new AncestorProbe { Caption = "second" };
            var path = CreateAncestorPath();
            var adapter = CreateLabelTextAdapter();
            CompiledBinding.Attach(label, label, control => FindVisualAncestor<AncestorProbe>(control), path, adapter, value => value);

            first.AddChild(projectedHost);
            Assert.That(label.Text, Is.EqualTo("first"));
            Assert.That(first.SubscriberCount, Is.EqualTo(1));
            second.AddChild(projectedHost);

            Assert.That(label.Text, Is.EqualTo("second"));
            Assert.That(first.SubscriberCount, Is.Zero);
            Assert.That(second.SubscriberCount, Is.EqualTo(1));
        }

        [Test]
        public void OneTimeAncestorBinding_WaitsForFirstResolvedSourceThenStopsObserving()
        {
            var label = new Label();
            var first = new AncestorProbe { Caption = "first" };
            var second = new AncestorProbe { Caption = "second" };
            CompiledBinding.Attach(
                label,
                label,
                control => FindVisualAncestor<AncestorProbe>(control),
                CreateAncestorPath(),
                CreateLabelTextAdapter(),
                value => value,
                options: new BindingOptions<string> { Mode = BindingMode.OneTime });

            first.AddChild(label);
            Assert.That(label.Text, Is.EqualTo("first"));
            second.AddChild(label);
            second.Caption = "updated";

            Assert.That(label.Text, Is.EqualTo("first"));
            Assert.That(first.SubscriberCount, Is.Zero);
            Assert.That(second.SubscriberCount, Is.Zero);
        }

        [Test]
        public void AncestorBinding_SourceReplacementCancelsPendingLostFocusWrite()
        {
            var first = new AncestorProbe { Caption = "first" };
            var second = new AncestorProbe { Caption = "second" };
            var root = new Control();
            var target = new BindingTargetProbe();
            first.AddChild(root);
            CompiledBinding.Attach(
                root,
                target,
                control => FindVisualAncestor<AncestorProbe>(control),
                new CompiledBindingPath<AncestorProbe, string>(
                    source => BindingValue<string>.FromValue(source.Caption),
                    (source, update) => source.SubscribeCaptionChanged(update),
                    (source, value) => source.Caption = value),
                BindingTargetProbe.Adapter,
                value => value,
                value => value,
                new BindingOptions<string> { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus });

            target.SetFromUser("pending");
            second.AddChild(root);
            target.Commit();

            Assert.That(first.Caption, Is.EqualTo("first"));
            Assert.That(second.Caption, Is.EqualTo("second"));
            Assert.That(target.Value, Is.EqualTo("second"));
        }

        [Test]
        public void AncestorBinding_ReentrantDisposalDoesNotCreateNewSourceSubscription()
        {
            var first = new AncestorProbe { Caption = "first" };
            var second = new AncestorProbe { Caption = "second" };
            var root = new Control();
            first.AddChild(root);
            IDisposable binding = null;
            root.ParentChanged += (_, _) => binding.Dispose();
            binding = CompiledBinding.Attach(
                root,
                new BindingTargetProbe(),
                control => FindVisualAncestor<AncestorProbe>(control),
                CreateAncestorPath(),
                BindingTargetProbe.Adapter,
                value => value);

            second.AddChild(root);

            Assert.That(first.SubscriberCount, Is.Zero);
            Assert.That(second.SubscriberCount, Is.Zero);
        }

        private static CompiledBindingPath<AncestorProbe, string> CreateAncestorPath() =>
            new CompiledBindingPath<AncestorProbe, string>(
                source => BindingValue<string>.FromValue(source.Caption),
                (source, update) => source.SubscribeCaptionChanged(update));

        private static BindingTargetAdapter<string> CreateLabelTextAdapter() =>
            new BindingTargetAdapter<string>(
                new XamlProperty<string>(nameof(Label.Text), target => ((Label)target).Text, (target, value) => ((Label)target).Text = value));

        private static T FindVisualAncestor<T>(Control control) where T : Control
        {
            for (var ancestor = control.VisualParent; ancestor != null; ancestor = ancestor.VisualParent)
                if (ancestor is T match) return match;
            return null;
        }

        private sealed class BindingViewModel : INotifyPropertyChanged
        {
            private int _score;
            private string _name;
            public event PropertyChangedEventHandler PropertyChanged;
            public int Score { get => _score; set { if (_score == value) return; _score = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Score))); } }
            public string Name { get => _name; set { if (_name == value) return; _name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); } }
        }

        private sealed class EqualSource
        {
            public EqualSource(int value) => Value = value;
            public int Value { get; }
            public override bool Equals(object obj) => obj is EqualSource other && other.Value == Value;
            public override int GetHashCode() => Value;
        }

        private sealed class AncestorProbe : Control
        {
            private string _caption;
            private event Action CaptionChanged;
            public int SubscriberCount { get; private set; }
            public IDisposable SubscribeCaptionChanged(Action update)
            {
                CaptionChanged += update;
                SubscriberCount++;
                return BindingSubscriptions.Event<Action>(handler => { }, handler =>
                {
                    CaptionChanged -= handler;
                    SubscriberCount--;
                }, update);
            }
            public string Caption
            {
                get => _caption;
                set
                {
                    if (_caption == value) return;
                    _caption = value;
                    CaptionChanged?.Invoke();
                }
            }
        }

        private sealed class BindingTargetProbe
        {
            public static readonly BindingTargetAdapter<string> Adapter = new BindingTargetAdapter<string>(
                new XamlProperty<string>(nameof(Value), target => ((BindingTargetProbe)target).Value, (target, value) => ((BindingTargetProbe)target).Value = value),
                (target, update) => BindingSubscriptions.Event<Action>(handler => ((BindingTargetProbe)target).Changed += handler, handler => ((BindingTargetProbe)target).Changed -= handler, update),
                (target, update) => BindingSubscriptions.Event<Action>(handler => ((BindingTargetProbe)target).Committed += handler, handler => ((BindingTargetProbe)target).Committed -= handler, update));

            private event Action Changed;
            private event Action Committed;
            public string Value { get; set; }
            public void SetFromUser(string value) { Value = value; Changed?.Invoke(); }
            public void Commit() => Committed?.Invoke();
        }
    }
}