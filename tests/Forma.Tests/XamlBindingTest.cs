// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
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

        private sealed class BindingViewModel : INotifyPropertyChanged
        {
            private int _score;
            private string _name;
            public event PropertyChangedEventHandler PropertyChanged;
            public int Score { get => _score; set { if (_score == value) return; _score = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Score))); } }
            public string Name { get => _name; set { if (_name == value) return; _name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); } }
        }
    }
}