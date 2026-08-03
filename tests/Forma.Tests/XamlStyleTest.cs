// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using Forma.Xaml;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Forma.Tests
{
    public class XamlStyleTest
    {
        private static readonly XamlProperty<string> TooltipProperty = new XamlProperty<string>(
            nameof(Control.TooltipText), target => ((Control)target).TooltipText, (target, value) => ((Control)target).TooltipText = value);

        [Test]
        public void Resources_UseLexicalLookupAndDynamicRestoration()
        {
            using var context = new UIContext();
            context.Resources["accent"] = "context";
            var root = new Control();
            root.Resources["accent"] = "root";
            var child = new Control { TooltipText = "base" };
            root.AddChild(child);
            DynamicResource.Attach(root, child, TooltipProperty, "accent");
            context.Add(root);

            Assert.That(StaticResource.Resolve<string>(child, "accent"), Is.EqualTo("root"));
            Assert.That(child.TooltipText, Is.EqualTo("root"));
            root.Resources["accent"] = "changed";
            Assert.That(child.TooltipText, Is.EqualTo("changed"));
            root.Resources.Remove("accent");
            Assert.That(child.TooltipText, Is.EqualTo("context"));
            context.Remove(root);
            Assert.That(child.TooltipText, Is.EqualTo("base"));
        }

        [Test]
        public void Styles_ResolveSpecificityOrderAndLocalPrecedence()
        {
            var button = new Button { Name = "Action", TooltipText = "base" };
            button.Classes.Add("primary");
            var typeStyle = CreateStyle("Button", "type");
            var classStyle = CreateStyle("Button.primary", "class");
            var nameStyle = CreateStyle("#Action", "name");
            StyleEngine.Attach(button, new[] { typeStyle, classStyle, nameStyle });
            Assert.That(button.TooltipText, Is.EqualTo("name"));

            using var local = XamlValues.Set(button, TooltipProperty, XamlValueLayer.Local, "local");
            button.Name = "Other";
            Assert.That(button.TooltipText, Is.EqualTo("local"));
            local.Dispose();
            Assert.That(button.TooltipText, Is.EqualTo("class"));
            button.Classes.Remove("primary");
            Assert.That(button.TooltipText, Is.EqualTo("type"));
        }

        [Test]
        public void Styles_TrackPseudoStatesAndNewChildren()
        {
            using var context = new UIContext { ViewportSize = new Vector2(200, 100) };
            var root = new Control { Size = new Vector2(200, 100) };
            var hoverStyle = CreateStyle("Button:hover", "hover");
            var disabledStyle = CreateStyle("Button:disabled", "disabled");
            StyleEngine.Attach(root, new[] { hoverStyle, disabledStyle });
            context.Add(root);
            var button = new Button { Position = new Vector2(10, 10), Size = new Vector2(80, 30), TooltipText = "base" };
            root.AddChild(button);

            context.Update(new GameTime(), new MouseState(20, 20, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released), new KeyboardState());
            Assert.That(button.TooltipText, Is.EqualTo("hover"));
            button.Enabled = false;
            Assert.That(button.TooltipText, Is.EqualTo("disabled"));
            root.RemoveChild(button);
            Assert.That(button.TooltipText, Is.EqualTo("base"));
        }

        [Test]
        public void Selectors_RejectCombinatorsAndUnknownPseudoStates()
        {
            Assert.Throws<FormatException>(() => StyleSelector.Parse("Panel Button"));
            Assert.Throws<FormatException>(() => StyleSelector.Parse("Button:unknown"));
        }

        private static Style CreateStyle(string selector, string value)
        {
            var style = new Style(selector);
            style.Setters.Add(new StyleSetter<string>(TooltipProperty, value));
            return style;
        }
    }
}