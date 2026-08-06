// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Forma.Xaml;

namespace Forma.Tests
{
    public class ContentControlTest
    {
        [Test]
        public void ContentControl_OwnsAndProjectsControlContent()
        {
            var first = new Control();
            var second = new Control();
            var control = new ContentControl
            {
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Bottom,
                Content = first,
            };
            var presenter = (ContentPresenter)control.TemplateRoot;

            Assert.Multiple(() =>
            {
                Assert.That(first.Parent, Is.SameAs(control));
                Assert.That(first.VisualParent, Is.SameAs(presenter));
                Assert.That(presenter.PresentedControl, Is.SameAs(first));
                Assert.That(presenter.HorizontalContentAlignment, Is.EqualTo(HorizontalAlignment.Center));
                Assert.That(presenter.VerticalContentAlignment, Is.EqualTo(VerticalAlignment.Bottom));
            });

            control.Content = second;
            Assert.Multiple(() =>
            {
                Assert.That(first.Parent, Is.Null);
                Assert.That(first.VisualParent, Is.Null);
                Assert.That(second.Parent, Is.SameAs(control));
                Assert.That(second.VisualParent, Is.SameAs(presenter));
            });
            control.Dispose();
        }

        [Test]
        public void ContentControl_UsesTemplatesAndScalarFallback()
        {
            var control = new ContentControl { Content = 42 };
            var presenter = (ContentPresenter)control.TemplateRoot;
            Assert.That(((TextBlock)presenter.PresentedControl).Text, Is.EqualTo("42"));

            control.ContentTemplate = DataTemplate.Create<int>((context, item) => new TextBlock { Text = $"item:{item}" });
            Assert.That(((TextBlock)presenter.PresentedControl).Text, Is.EqualTo("item:42"));
            control.Dispose();
        }

        [Test]
        public void ContentControl_TreatsControlAsDataWhenTemplateIsExplicit()
        {
            var item = new Control();
            var control = new ContentControl
            {
                ContentTemplate = DataTemplate.Create<Control>((context, value) => new TextBlock { Text = "templated" }),
                Content = item,
            };
            var presenter = (ContentPresenter)control.TemplateRoot;

            Assert.Multiple(() =>
            {
                Assert.That(item.Parent, Is.Null);
                Assert.That(item.VisualParent, Is.Null);
                Assert.That(presenter.PresentedControl, Is.TypeOf<TextBlock>());
            });
            control.Dispose();
        }

        [Test]
        public void Button_AdoptsArbitraryContentWithoutChangingActivationBehavior()
        {
            var content = new Control();
            var button = new Button { Content = content };
            var presenter = (ContentPresenter)button.GetTemplateChild(ContentControl.ContentPresenterPartName);

            Assert.Multiple(() =>
            {
                Assert.That(button.TemplateRoot, Is.Not.SameAs(presenter));
                Assert.That(content.Parent, Is.SameAs(button));
                Assert.That(content.VisualParent, Is.SameAs(presenter));
                Assert.That(presenter.PresentedControl, Is.SameAs(content));
                Assert.That(button.FocusMode, Is.EqualTo(FocusMode.All));
            });
            button.Dispose();
        }
    }
}