// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Forma.Xaml;
using Microsoft.Xna.Framework;

namespace Forma.Tests
{
    public class PresentersTest
    {
        [Test]
        public void ContentPresenter_ProjectsLogicalControlWithoutTakingOwnership()
        {
            using var context = new UIContext { ViewportSize = new Vector2(160, 80) };
            var root = new Control { Size = new Vector2(160, 80) };
            var owner = new Control { DataContext = "owner" };
            owner.Resources["Accent"] = "resource";
            var content = new Control { CustomMinimumSize = new Vector2(20, 10) };
            var presenter = new ContentPresenter
            {
                Position = new Vector2(10, 5),
                Size = new Vector2(80, 40),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
            };
            owner.AddChild(content);
            root.AddChild(owner);
            root.AddChild(presenter);
            context.Add(root);

            presenter.Content = content;
            context.Layout();

            Assert.That(content.Parent, Is.SameAs(owner));
            Assert.That(content.VisualParent, Is.SameAs(presenter));
            Assert.That(content.InheritanceParent, Is.SameAs(owner));
            Assert.That(content.DataContext, Is.EqualTo("owner"));
            Assert.That(content.TryFindResource("Accent", out var resource), Is.True);
            Assert.That(resource, Is.EqualTo("resource"));
            Assert.That(content.Position, Is.EqualTo(new Vector2(30, 30)));
            Assert.That(content.Size, Is.EqualTo(new Vector2(20, 10)));
            Assert.That(presenter.Children, Is.Empty);

            var other = new ContentPresenter();
            Assert.Throws<InvalidOperationException>(() => other.Content = content);
            Assert.That(content.VisualParent, Is.SameAs(presenter));

            presenter.Content = null;
            Assert.That(content.Parent, Is.SameAs(owner));
            Assert.That(content.VisualParent, Is.Null);
            Assert.That(content.InheritanceParent, Is.Null);
        }

        [Test]
        public void ContentPresenter_UsesDataTemplateBeforeScalarFallbackAndDisposesReplacement()
        {
            var disposed = 0;
            var presenter = new ContentPresenter
            {
                ContentTemplate = DataTemplate.Create<int>((context, item) =>
                {
                    context.RegisterAttachment(new DelegateDisposable(() => disposed++));
                    return new TextBlock { Text = $"Value {item}" };
                }),
                Content = 42,
            };

            Assert.That(presenter.PresentedControl, Is.TypeOf<TextBlock>());
            Assert.That(((TextBlock)presenter.PresentedControl).Text, Is.EqualTo("Value 42"));
            Assert.That(presenter.PresentedControl.Parent, Is.Null);
            Assert.That(presenter.PresentedControl.VisualParent, Is.SameAs(presenter));
            Assert.That(presenter.PresentedControl.InheritanceParent, Is.SameAs(presenter));

            presenter.ContentTemplate = null;
            Assert.That(disposed, Is.EqualTo(1));
            Assert.That(((TextBlock)presenter.PresentedControl).Text, Is.EqualTo("42"));
            presenter.Dispose();
            Assert.Throws<ObjectDisposedException>(() => presenter.Content = 7);
            Assert.That(presenter.Content, Is.Null);
        }

        [Test]
        public void ContentPresenter_FailedProjectionLeavesNoDanglingVisualOrStateMutation()
        {
            var owner = new Control();
            var content = new Control();
            owner.AddChild(content);
            content.ParentChanged += ThrowParentChanged;
            var presenter = new ContentPresenter { Content = "stable" };
            var previous = presenter.PresentedControl;

            Assert.Throws<InvalidOperationException>(() => presenter.Content = content);

            Assert.That(presenter.Content, Is.EqualTo("stable"));
            Assert.That(presenter.PresentedControl, Is.SameAs(previous));
            Assert.That(previous.VisualParent, Is.SameAs(presenter));
            Assert.That(content.Parent, Is.SameAs(owner));
            Assert.That(content.VisualParent, Is.Null);
            Assert.That(content.InheritanceParent, Is.Null);
        }

        [Test]
        public void ItemsPresenter_CreatesFreshPanelWithOwnerInheritanceAndOrderedCallbacks()
        {
            var events = new List<string>();
            var firstOwner = new ItemsOwnerProbe("first", events);
            var secondOwner = new ItemsOwnerProbe("second", events);
            var presenter = new ItemsPresenter
            {
                ItemsPanel = new ItemsPanelTemplate(_ => new StackPanel()),
                Owner = firstOwner,
            };
            var firstPanel = presenter.Panel;

            Assert.That(firstPanel, Is.TypeOf<StackPanel>());
            Assert.That(firstPanel.Parent, Is.Null);
            Assert.That(firstPanel.VisualParent, Is.SameAs(presenter));
            Assert.That(firstPanel.InheritanceParent, Is.SameAs(firstOwner.ItemsPresenterInheritanceParent));
            Assert.That(events, Is.EqualTo(new[] { "first:attach" }));

            presenter.Owner = secondOwner;
            var secondPanel = presenter.Panel;
            Assert.That(secondPanel, Is.Not.SameAs(firstPanel));
            Assert.That(firstPanel.VisualParent, Is.Null);
            Assert.That(secondPanel.InheritanceParent, Is.SameAs(secondOwner.ItemsPresenterInheritanceParent));
            Assert.That(events, Is.EqualTo(new[] { "first:attach", "first:detach", "second:attach" }));

            presenter.Dispose();
            Assert.That(events[^1], Is.EqualTo("second:detach"));
            Assert.That(secondPanel.VisualParent, Is.Null);
        }

        [Test]
        public void ItemsPresenter_DetachesOwnerAfterPanelTemplateScopeWasDisposed()
        {
            var events = new List<string>();
            var owner = new ItemsOwnerProbe("owner", events);
            var presenter = new ItemsPresenter
            {
                ItemsPanel = new ItemsPanelTemplate(_ => new StackPanel()),
                Owner = owner,
            };
            var panel = presenter.Panel;

            XamlAttachment.PromoteTemplateScope(panel).DisposeOwner();

            Assert.DoesNotThrow(() => presenter.Owner = null);
            Assert.Multiple(() =>
            {
                Assert.That(presenter.Panel, Is.Null);
                Assert.That(panel.VisualParent, Is.Null);
                Assert.That(events, Is.EqualTo(new[] { "owner:attach", "owner:detach" }));
            });
        }

        [Test]
        public void ItemsPresenter_FailedReplacementRestoresPanelAndBalancesInvokedCallbacks()
        {
            var events = new List<string>();
            var stableOwner = new ItemsOwnerProbe("stable", events);
            var presenter = new ItemsPresenter
            {
                ItemsPanel = new ItemsPanelTemplate(_ => new StackPanel()),
                Owner = stableOwner,
            };
            var stablePanel = presenter.Panel;
            var invalidOwner = new ItemsOwnerProbe("invalid", events, nullInheritanceParent: true);

            Assert.Throws<InvalidOperationException>(() => presenter.Owner = invalidOwner);
            Assert.That(presenter.Owner, Is.SameAs(stableOwner));
            Assert.That(presenter.Panel, Is.SameAs(stablePanel));
            Assert.That(stablePanel.VisualParent, Is.SameAs(presenter));
            Assert.That(events, Is.EqualTo(new[] { "stable:attach", "stable:detach", "stable:attach" }));

            var throwingOwner = new ItemsOwnerProbe("throwing", events) { ThrowOnAttach = true };
            Assert.Throws<InvalidOperationException>(() => presenter.Owner = throwingOwner);
            Assert.That(presenter.Owner, Is.SameAs(stableOwner));
            Assert.That(presenter.Panel, Is.SameAs(stablePanel));
            Assert.That(stablePanel.VisualParent, Is.SameAs(presenter));
            Assert.That(events, Does.Contain("throwing:attach"));
            Assert.That(events, Does.Contain("throwing:detach"));
            Assert.That(events[^1], Is.EqualTo("stable:attach"));
            Assert.That(throwingOwner.AttachedPanel.VisualParent, Is.Null);
        }

        [Test]
        public void ScrollPresenter_ProjectsClipsClampsAndReportsMetrics()
        {
            using var context = new UIContext { ViewportSize = new Vector2(200, 120) };
            var root = new Control { Size = new Vector2(200, 120) };
            var logicalOwner = new Control();
            var content = new Control { CustomMinimumSize = new Vector2(180, 90) };
            var scrollOwner = new ScrollOwnerProbe { ScrollOffset = new Vector2(200, 60) };
            var presenter = new ScrollPresenter { Size = new Vector2(100, 40), Owner = scrollOwner };
            logicalOwner.AddChild(content);
            root.AddChild(logicalOwner);
            root.AddChild(presenter);
            context.Add(root);

            presenter.Content = content;
            context.Layout();

            Assert.That(presenter.ClipToBounds, Is.True);
            Assert.That(content.Parent, Is.SameAs(logicalOwner));
            Assert.That(content.VisualParent, Is.SameAs(presenter));
            Assert.That(content.InheritanceParent, Is.SameAs(logicalOwner));
            Assert.That(presenter.Viewport, Is.EqualTo(new Vector2(100, 40)));
            Assert.That(presenter.Extent, Is.EqualTo(new Vector2(180, 90)));
            Assert.That(presenter.Offset, Is.EqualTo(new Vector2(80, 50)));
            Assert.That(scrollOwner.ScrollOffset, Is.EqualTo(new Vector2(80, 50)));
            Assert.That(content.Position, Is.EqualTo(new Vector2(-80, -50)));
            Assert.That(scrollOwner.MetricsChanges, Is.EqualTo(1));

            content.BringIntoView();
            Assert.That(scrollOwner.BroughtTarget, Is.SameAs(content));
            Assert.That(scrollOwner.BroughtBounds, Is.EqualTo(content.VisualBounds));

            content.CustomMinimumSize = new Vector2(60, 20);
            context.Layout();
            Assert.That(presenter.Extent, Is.EqualTo(new Vector2(100, 40)));
            Assert.That(presenter.Offset, Is.EqualTo(Vector2.Zero));
            Assert.That(scrollOwner.ScrollOffset, Is.EqualTo(Vector2.Zero));
            Assert.That(scrollOwner.MetricsChanges, Is.EqualTo(2));
        }

        [Test]
        public void ScrollPresenter_FailedProjectionRestoresPreviousContent()
        {
            var firstOwner = new Control();
            var first = new Control();
            firstOwner.AddChild(first);
            var presenter = new ScrollPresenter { Content = first };
            var secondOwner = new Control();
            var second = new Control();
            secondOwner.AddChild(second);
            second.ParentChanged += ThrowParentChanged;

            Assert.Throws<InvalidOperationException>(() => presenter.Content = second);

            Assert.That(presenter.Content, Is.SameAs(first));
            Assert.That(first.VisualParent, Is.SameAs(presenter));
            Assert.That(second.VisualParent, Is.Null);
            Assert.That(second.Parent, Is.SameAs(secondOwner));
            presenter.Dispose();
            Assert.Throws<ObjectDisposedException>(() => presenter.Content = second);
            Assert.That(presenter.Content, Is.Null);
        }

        private sealed class ItemsOwnerProbe : IItemsPresenterOwner
        {
            private readonly string _name;
            private readonly IList<string> _events;

            public ItemsOwnerProbe(string name, IList<string> events, Control inheritanceParent = default, bool nullInheritanceParent = false)
            {
                _name = name;
                _events = events;
                ItemsPresenterInheritanceParent = nullInheritanceParent ? null : inheritanceParent ?? new Control { DataContext = name };
            }

            public Control ItemsPresenterInheritanceParent { get; }
            public bool ThrowOnAttach { get; set; }
            public Container AttachedPanel { get; private set; }
            public void AttachItemsPanel(ItemsPresenter presenter, Container panel)
            {
                AttachedPanel = panel;
                _events.Add($"{_name}:attach");
                if (ThrowOnAttach) throw new InvalidOperationException("Attach failed.");
            }
            public void DetachItemsPanel(ItemsPresenter presenter, Container panel) => _events.Add($"{_name}:detach");
        }

        private sealed class ScrollOwnerProbe : IScrollViewportOwner
        {
            public Vector2 ScrollOffset { get; set; }
            public int MetricsChanges { get; private set; }
            public Control BroughtTarget { get; private set; }
            public Rectangle BroughtBounds { get; private set; }

            public void OnScrollMetricsChanged(ScrollPresenter presenter, ScrollMetrics metrics) => MetricsChanges++;
            public void BringIntoView(ScrollPresenter presenter, Control target, Rectangle targetBounds)
            {
                BroughtTarget = target;
                BroughtBounds = targetBounds;
            }
        }

        private sealed class DelegateDisposable : IDisposable
        {
            private readonly Action _dispose;
            public DelegateDisposable(Action dispose) => _dispose = dispose;
            public void Dispose() => _dispose();
        }

        private static void ThrowParentChanged(object sender, ControlParentChangedEventArgs args) =>
            throw new InvalidOperationException("Parent notification failed.");
    }
}