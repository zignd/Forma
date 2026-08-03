// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using Forma.Xaml;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Forma.Tests
{
    public class XamlRuntimeTest
    {
        [Test]
        public void Control_ImplementsContentAndInheritedDataContextContracts()
        {
            var root = new Control();
            var child = new Control();
            var grandchild = new Control();
            ((IAddChild<Control>)root).AddChild(child);
            child.AddChild(grandchild);

            var inheritedChanges = 0;
            grandchild.DataContextChanged += (_, _) => inheritedChanges++;
            var first = new object();
            root.DataContext = first;

            Assert.That(child.DataContext, Is.SameAs(first));
            Assert.That(grandchild.DataContext, Is.SameAs(first));
            Assert.That(inheritedChanges, Is.EqualTo(1));

            var local = new object();
            child.DataContext = local;
            root.DataContext = new object();
            Assert.That(grandchild.DataContext, Is.SameAs(local));

            child.ClearDataContext();
            Assert.That(child.DataContext, Is.SameAs(root.DataContext));
            Assert.That(grandchild.DataContext, Is.SameAs(root.DataContext));
        }

        [Test]
        public void Control_AttachDetachEventsFollowContextTransitions()
        {
            using var first = new UIContext();
            using var second = new UIContext();
            var root = new Control();
            var child = new Control();
            root.AddChild(child);
            var attached = 0;
            var detached = 0;
            child.Attached += (_, _) => attached++;
            child.Detached += (_, _) => detached++;

            first.Add(root);
            first.Remove(root);
            second.Add(root);

            Assert.That(attached, Is.EqualTo(2));
            Assert.That(detached, Is.EqualTo(1));
            Assert.That(child.Context, Is.SameAs(second));
        }

        [Test]
        public void ResourcesClassesAndNamescopeUseDeterministicLookup()
        {
            var merged = new ResourceDictionary { ["accent"] = "merged" };
            var root = new Control();
            root.Resources.MergedDictionaries.Add(merged);
            root.Resources["local"] = 42;
            var child = new Control();
            root.AddChild(child);

            Assert.That(child.TryFindResource("accent", out var accent), Is.True);
            Assert.That(accent, Is.EqualTo("merged"));
            Assert.That(child.TryFindResource("local", out var local), Is.True);
            Assert.That(local, Is.EqualTo(42));

            var classChanges = 0;
            child.Classes.Changed += (_, _) => classChanges++;
            child.Classes.Set("primary primary compact");
            Assert.That(child.Classes, Is.EquivalentTo(new[] { "primary", "compact" }));
            Assert.That(classChanges, Is.EqualTo(1));

            var scope = new NameScope();
            scope.Register("Child", child);
            NameScope.SetNameScope(root, scope);
            Assert.That(root.FindName<Control>("Child"), Is.SameAs(child));
            Assert.Throws<InvalidOperationException>(() => scope.Register("Child", new Control()));
        }

        [Test]
        public void FormaXamlLoader_UsesExplicitTypedRegistration()
        {
            FormaXamlLoader.Register<RegisteredView>(
                _ => new RegisteredView { Value = "built" },
                (_, view) => view.Value = "populated");

            Assert.That(FormaXamlLoader.Load<RegisteredView>().Value, Is.EqualTo("built"));
            var existing = new RegisteredView();
            FormaXamlLoader.Load(existing);
            Assert.That(existing.Value, Is.EqualTo("populated"));
        }

        [Test]
        public void XamlValueConverter_HandlesFormaLiteralTypes()
        {
            Assert.That(XamlValueConverter.Convert("Fill, Expand", typeof(SizeFlags)), Is.EqualTo(SizeFlags.Fill | SizeFlags.Expand));
            Assert.That(XamlValueConverter.Convert("#80402010", typeof(Color)), Is.EqualTo(new Color(0x40, 0x20, 0x10, 0x80)));
            Assert.That(XamlValueConverter.Convert("CornflowerBlue", typeof(Color)), Is.EqualTo(new Color(100, 149, 237)));
            Assert.That(XamlValueConverter.Convert("2.5,4", typeof(Vector2)), Is.EqualTo(new Vector2(2.5f, 4f)));
            Assert.That(XamlValueConverter.Convert("1,2,3,4", typeof(Thickness)), Is.EqualTo(new Thickness(1, 2, 3, 4)));
            Assert.That(XamlValueConverter.Convert("0:0:0.35", typeof(TimeSpan)), Is.EqualTo(TimeSpan.FromMilliseconds(350)));
            Assert.That(XamlValueConverter.Convert("", typeof(float?)), Is.Null);
            Assert.That(XamlValueConverter.TryConvert("not-a-color", typeof(Color), out _), Is.False);
        }

        [Test]
        public void ThemeAndStyleBoxesRemainDirectlyDeclarative()
        {
            var theme = new Theme { AccentColor = (Color)XamlValueConverter.Convert("#FF56C596", typeof(Color)) };
            var style = new StyleBoxFlat
            {
                BackgroundColor = (Color)XamlValueConverter.Convert("#E6202630", typeof(Color)),
                ContentMargin = (Thickness)XamlValueConverter.Convert("16", typeof(Thickness)),
            };
            var control = new Control { ThemeOverride = theme };
            control.ThemeStyleOverrides["panel"] = style;

            Assert.That(control.ThemeOverride.AccentColor, Is.EqualTo(new Color(0x56, 0xC5, 0x96, 0xFF)));
            Assert.That(control.GetThemeStyleBox("panel"), Is.SameAs(style));
        }

        [Test]
        public void XamlAttachment_UpdatesWhileAttachedAndDisposesOnDetach()
        {
            using var context = new UIContext();
            var root = new Control();
            var participant = new AttachmentProbe();
            XamlAttachment.RegisterDisposable(root, participant);
            XamlAttachment.RegisterUpdateParticipant(root, participant);

            context.Add(root);
            context.Update(new GameTime(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)), new MouseState(), new KeyboardState());
            Assert.That(participant.UpdateCount, Is.EqualTo(1));

            context.Remove(root);
            context.Update(new GameTime(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1)), new MouseState(), new KeyboardState());
            Assert.That(participant.UpdateCount, Is.EqualTo(1));
            Assert.That(participant.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void Control_ExposesOnlyRequiredStateAndTreeTransitions()
        {
            var parent = new Control();
            var child = new Control();
            var enabledChanges = 0;
            var added = 0;
            var removed = 0;
            child.EnabledChanged += (_, _) => enabledChanges++;
            parent.ChildAdded += (_, value) => { if (value == child) added++; };
            parent.ChildRemoved += (_, value) => { if (value == child) removed++; };

            child.Enabled = false;
            child.Enabled = false;
            parent.AddChild(child);
            parent.RemoveChild(child);

            Assert.That(enabledChanges, Is.EqualTo(1));
            Assert.That(added, Is.EqualTo(1));
            Assert.That(removed, Is.EqualTo(1));
        }

        [Test]
        public void XamlValues_ResolvePrecedenceAndRestoreUnderlyingValues()
        {
            var target = new ValueTarget { Value = 10 };
            var property = new XamlProperty<int>(nameof(ValueTarget.Value), value => ((ValueTarget)value).Value, (value, current) => ((ValueTarget)value).Value = current);
            using var style = XamlValues.Set(target, property, XamlValueLayer.Style, 20);
            using var local = XamlValues.Set(target, property, XamlValueLayer.Local, 30);
            using (var animation = XamlValues.Set(target, property, XamlValueLayer.Animation, 40))
            {
                Assert.That(target.Value, Is.EqualTo(40));
                animation.Value = 41;
                Assert.That(target.Value, Is.EqualTo(41));
            }
            Assert.That(target.Value, Is.EqualTo(30));
            local.Dispose();
            Assert.That(target.Value, Is.EqualTo(20));
            style.Dispose();
            Assert.That(target.Value, Is.EqualTo(10));
        }

        [Test]
        public void XamlValues_UsePriorityThenDeclarationOrderAndExplicitBaseRefresh()
        {
            var target = new ValueTarget { Value = 1 };
            var property = new XamlProperty<int>(nameof(ValueTarget.Value), value => ((ValueTarget)value).Value, (value, current) => ((ValueTarget)value).Value = current);
            using var earlier = XamlValues.Set(target, property, XamlValueLayer.Style, 2, priority: 10);
            using var later = XamlValues.Set(target, property, XamlValueLayer.Style, 3, priority: 10);
            using var specific = XamlValues.Set(target, property, XamlValueLayer.Style, 4, priority: 20);
            Assert.That(target.Value, Is.EqualTo(4));
            specific.Dispose();
            Assert.That(target.Value, Is.EqualTo(3));

            target.Value = 9;
            XamlValues.RefreshBaseValue(target, property);
            Assert.That(target.Value, Is.EqualTo(3));
            later.Dispose();
            earlier.Dispose();
            Assert.That(target.Value, Is.EqualTo(9));
        }

        private sealed class RegisteredView
        {
            public string Value { get; set; }
        }

        private sealed class AttachmentProbe : IDisposable, IXamlUpdateParticipant
        {
            public int UpdateCount { get; private set; }
            public int DisposeCount { get; private set; }
            public void Update(GameTime gameTime) => UpdateCount++;
            public void Dispose() => DisposeCount++;
        }

        private sealed class ValueTarget
        {
            public int Value { get; set; }
        }
    }
}