// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Microsoft.Xna.Framework;

namespace Forma.Tests
{
    public class ScrollViewportControllerTest
    {
        [Test]
        public void Controller_ClampsMetricsAndRevealsBounds()
        {
            var controller = new ScrollViewportController();
            controller.UpdateMetrics(new Vector2(100, 80), new Vector2(300, 240));
            controller.Offset = new Vector2(500, 500);
            Assert.That(controller.Offset, Is.EqualTo(new Vector2(200, 160)));

            controller.Offset = Vector2.Zero;
            controller.BringIntoView(new Rectangle(0, 0, 100, 80), new Rectangle(140, 120, 20, 20));
            Assert.That(controller.Offset, Is.EqualTo(new Vector2(60, 60)));

            controller.HorizontalEnabled = false;
            controller.Offset = new Vector2(100, 100);
            Assert.That(controller.Offset, Is.EqualTo(new Vector2(0, 100)));
        }

        [Test]
        public void Controller_HandlesWheelAndTouchPolicyWithoutVisualState()
        {
            var started = 0;
            var ended = 0;
            var controller = new ScrollViewportController { ScrollDeadzone = 5 };
            controller.UpdateMetrics(new Vector2(100, 80), new Vector2(300, 240));
            controller.ScrollStarted += (_, _) => started++;
            controller.ScrollEnded += (_, _) => ended++;

            Assert.That(controller.ScrollWheel(-1, false, 100, 80), Is.True);
            Assert.That(controller.Offset.Y, Is.EqualTo(10));
            controller.BeginTouchDrag();
            controller.TouchDragBy(new Vector2(0, -3));
            Assert.That(controller.IsBeyondScrollDeadzone, Is.False);
            controller.TouchDragBy(new Vector2(0, -8));
            controller.Process(.05f);
            Assert.Multiple(() =>
            {
                Assert.That(started, Is.EqualTo(1));
                Assert.That(controller.IsBeyondScrollDeadzone, Is.True);
                Assert.That(controller.Offset.Y, Is.GreaterThan(10));
            });
            controller.CancelTouchDrag();
            Assert.That(ended, Is.EqualTo(1));
        }

        [Test]
        public void ScrollContainer_DefaultTemplateProjectsLogicalContentThroughRequiredPresenter()
        {
            var content = new Control { CustomMinimumSize = new Vector2(240, 180) };
            var scroll = new ScrollContainer
            {
                Size = new Vector2(100, 80),
                HorizontalScrollMode = ScrollBarVisibility.Never,
                VerticalScrollMode = ScrollBarVisibility.Never,
            };
            scroll.AddChild(content);
            var context = new UIContext();
            context.Add(scroll);
            context.Layout();

            var presenter = scroll.GetTemplateChild(ScrollContainer.ScrollPresenterPartName) as ScrollPresenter;
            Assert.Multiple(() =>
            {
                Assert.That(presenter, Is.Not.Null);
                Assert.That(presenter.Content, Is.SameAs(content));
                Assert.That(content.Parent, Is.SameAs(scroll));
                Assert.That(content.VisualParent, Is.SameAs(presenter));
                Assert.That(scroll.Extent, Is.EqualTo(new Vector2(240, 180)));
                Assert.That(scroll.MaxScrollOffset, Is.EqualTo(new Vector2(140, 100)));
            });
        }

        [Test]
        public void Controller_ReportsMetricsAndRestoresTypedAnchor()
        {
            var changes = 0;
            var token = new object();
            var controller = new ScrollViewportController();
            controller.MetricsChanged += (_, _) => changes++;
            controller.UpdateMetrics(new Vector2(100, 80), new Vector2(300, 240));
            controller.Offset = new Vector2(20, 30);
            var anchor = controller.CaptureAnchor(token, new Vector2(30, 50));
            controller.Offset = new Vector2(80, 90);

            Assert.That(controller.RestoreAnchor(token, new Vector2(50, 90)), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(changes, Is.EqualTo(4));
                Assert.That(anchor.ViewportOffset, Is.EqualTo(new Vector2(10, 20)));
                Assert.That(controller.Offset, Is.EqualTo(new Vector2(40, 70)));
                Assert.That(controller.Metrics.Offset, Is.EqualTo(controller.Offset));
            });
        }

        [Test]
        public void ScrollContainer_BringsProvidedIndexIntoViewAndRaisesOffsetChange()
        {
            var content = new IndexedContent(new Rectangle(140, 120, 20, 20)) { CustomMinimumSize = new Vector2(300, 240) };
            var scroll = new ScrollContainer
            {
                Size = new Vector2(100, 80),
                HorizontalScrollMode = ScrollBarVisibility.Never,
                VerticalScrollMode = ScrollBarVisibility.Never,
            };
            var changes = 0;
            scroll.ScrollOffsetChanged += (_, _) => changes++;
            scroll.AddChild(content);
            var context = new UIContext();
            context.Add(scroll);
            context.Layout();

            Assert.That(scroll.BringIndexIntoView(7), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(content.RequestedIndex, Is.EqualTo(7));
                Assert.That(scroll.ScrollOffset, Is.EqualTo(new Vector2(60, 60)));
                Assert.That(changes, Is.EqualTo(1));
            });
        }

        [Test]
        public void ScrollContainer_RejectsTemplateWithoutRequiredPresenterAndRestoresDefault()
        {
            var scroll = new ScrollContainer();
            var defaultPresenter = scroll.TemplateRoot;
            var invalid = ControlTemplate.Create<ScrollContainer>((_, _) => new Border());

            var error = Assert.Throws<InvalidOperationException>(() => scroll.Template = invalid);

            Assert.Multiple(() =>
            {
                Assert.That(error.Message, Does.Contain(ScrollContainer.ScrollPresenterPartName));
                Assert.That(scroll.Template, Is.Null);
                Assert.That(scroll.TemplateRoot, Is.SameAs(defaultPresenter));
                Assert.That(defaultPresenter.VisualParent, Is.SameAs(scroll));
            });
        }

        private sealed class IndexedContent : Control, IScrollIndexProvider
        {
            private readonly Rectangle _bounds;

            public IndexedContent(Rectangle bounds) => _bounds = bounds;

            public int RequestedIndex { get; private set; } = -1;

            public bool TryGetIndexBounds(int index, out Rectangle bounds)
            {
                RequestedIndex = index;
                bounds = _bounds;
                return true;
            }
        }
    }
}