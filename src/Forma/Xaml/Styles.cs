using System;
using System.Collections.Generic;
using System.Linq;

namespace Forma.Xaml
{
    public sealed class Style
    {
        public Style(string selector) => Selector = StyleSelector.Parse(selector);
        public StyleSelector Selector { get; }
        public IList<IStyleSetter> Setters { get; } = new List<IStyleSetter>();
    }

    public interface IStyleSetter
    {
        IDisposable Apply(Control control, long priority);
    }

    public sealed class StyleSetter<T> : IStyleSetter
    {
        private readonly XamlProperty<T> _property;
        private readonly Func<Control, T> _value;

        public StyleSetter(XamlProperty<T> property, T value) : this(property, _ => value) { }
        public StyleSetter(XamlProperty<T> property, Func<Control, T> value)
        {
            _property = property ?? throw new ArgumentNullException(nameof(property));
            _value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public IDisposable Apply(Control control, long priority) =>
            XamlValues.Set(control, _property, XamlValueLayer.Style, _value(control), priority);
    }

    public sealed class StyleSelector
    {
        private static readonly HashSet<string> SupportedPseudoStates = new HashSet<string>(StringComparer.Ordinal)
        {
            "hover", "focus", "disabled", "pressed", "checked",
        };

        private StyleSelector(string typeName, string name, IReadOnlyList<string> classes, IReadOnlyList<string> pseudoStates)
        {
            TypeName = typeName;
            Name = name;
            Classes = classes;
            PseudoStates = pseudoStates;
            Specificity = (name == null ? 0 : 1_000_000) + (classes.Count + pseudoStates.Count) * 1_000 + (typeName == null ? 0 : 1);
        }

        public string TypeName { get; }
        public string Name { get; }
        public IReadOnlyList<string> Classes { get; }
        public IReadOnlyList<string> PseudoStates { get; }
        public int Specificity { get; }

        public static StyleSelector Parse(string selector)
        {
            if (string.IsNullOrWhiteSpace(selector)) throw new FormatException("A style selector is required.");
            if (selector.Any(char.IsWhiteSpace) || selector.IndexOfAny(new[] { '>', '+', '~', '[', ']', ',', '*' }) >= 0)
                throw new FormatException($"Selector '{selector}' uses syntax outside Forma XAML v1.");

            string typeName = null;
            string name = null;
            var classes = new List<string>();
            var pseudoStates = new List<string>();
            var index = 0;
            while (index < selector.Length && selector[index] != '.' && selector[index] != '#' && selector[index] != ':') index++;
            if (index > 0) typeName = selector.Substring(0, index);
            while (index < selector.Length)
            {
                var marker = selector[index++];
                var start = index;
                while (index < selector.Length && selector[index] != '.' && selector[index] != '#' && selector[index] != ':') index++;
                if (index == start) throw new FormatException($"Selector '{selector}' contains an empty term.");
                var value = selector.Substring(start, index - start);
                if (marker == '.') classes.Add(value);
                else if (marker == '#')
                {
                    if (name != null) throw new FormatException("A selector can contain only one name.");
                    name = value;
                }
                else
                {
                    if (!SupportedPseudoStates.Contains(value)) throw new FormatException($"Pseudo state ':{value}' is not supported.");
                    pseudoStates.Add(value);
                }
            }
            return new StyleSelector(typeName, name, classes, pseudoStates);
        }

        internal bool Matches(Control control, StyleControlState state)
        {
            if (TypeName != null && !MatchesType(control.GetType(), TypeName)) return false;
            if (Name != null && control.Name != Name) return false;
            foreach (var className in Classes) if (!control.Classes.Contains(className)) return false;
            foreach (var pseudoState in PseudoStates)
            {
                var matches = pseudoState switch
                {
                    "hover" => state.Hovered,
                    "focus" => state.Focused,
                    "disabled" => !control.Enabled,
                    "pressed" => control is BaseButton button && button.IsVisuallyPressed,
                    "checked" => control is BaseButton checkable && checkable.ButtonPressed,
                    _ => false,
                };
                if (!matches) return false;
            }
            return true;
        }

        private static bool MatchesType(Type type, string typeName)
        {
            for (var current = type; current != null && typeof(Control).IsAssignableFrom(current); current = current.BaseType)
                if (current.Name == typeName) return true;
            return false;
        }
    }

    public static class StyleEngine
    {
        public static IDisposable Attach(Control root, IEnumerable<Style> styles)
        {
            var engine = new StyleAttachment(root, styles?.ToArray() ?? throw new ArgumentNullException(nameof(styles)));
            XamlAttachment.RegisterDisposable(root, engine);
            return engine;
        }
    }

    internal sealed class StyleControlState
    {
        public bool Hovered;
        public bool Focused;
    }

    internal sealed class StyleAttachment : IDisposable
    {
        private sealed class ControlRegistration
        {
            public readonly StyleControlState State = new StyleControlState();
            public readonly Dictionary<int, List<IDisposable>> Applied = new Dictionary<int, List<IDisposable>>();
            public readonly List<Action> Unsubscribe = new List<Action>();
        }

        private readonly Style[] _styles;
        private readonly Dictionary<Control, ControlRegistration> _controls = new Dictionary<Control, ControlRegistration>();
        private bool _disposed;

        public StyleAttachment(Control root, Style[] styles)
        {
            _styles = styles;
            AttachTree(root);
        }

        private void AttachTree(Control control)
        {
            if (_controls.ContainsKey(control)) return;
            var registration = new ControlRegistration();
            _controls.Add(control, registration);
            Subscribe(control, registration);
            Evaluate(control, registration);
            foreach (var child in control.Children) AttachTree(child);
        }

        private void DetachTree(Control control)
        {
            foreach (var child in control.Children.ToArray()) DetachTree(child);
            if (!_controls.Remove(control, out var registration)) return;
            foreach (var unsubscribe in registration.Unsubscribe) unsubscribe();
            foreach (var applied in registration.Applied.Values)
                for (var index = applied.Count - 1; index >= 0; index--) applied[index].Dispose();
        }

        private void Subscribe(Control control, ControlRegistration registration)
        {
            EventHandler changed = (_, _) => Evaluate(control, registration);
            EventHandler entered = (_, _) => { registration.State.Hovered = true; Evaluate(control, registration); };
            EventHandler exited = (_, _) => { registration.State.Hovered = false; Evaluate(control, registration); };
            EventHandler focused = (_, _) => { registration.State.Focused = true; Evaluate(control, registration); };
            EventHandler unfocused = (_, _) => { registration.State.Focused = false; Evaluate(control, registration); };
            Action<Control, Control> added = (_, child) => AttachTree(child);
            Action<Control, Control> removed = (_, child) => DetachTree(child);
            control.Classes.Changed += changed;
            control.NameChanged += changed;
            control.EnabledChanged += changed;
            control.MouseEntered += entered;
            control.MouseExited += exited;
            control.FocusEntered += focused;
            control.FocusExited += unfocused;
            control.ChildAdded += added;
            control.ChildRemoved += removed;
            registration.Unsubscribe.Add(() => control.Classes.Changed -= changed);
            registration.Unsubscribe.Add(() => control.NameChanged -= changed);
            registration.Unsubscribe.Add(() => control.EnabledChanged -= changed);
            registration.Unsubscribe.Add(() => control.MouseEntered -= entered);
            registration.Unsubscribe.Add(() => control.MouseExited -= exited);
            registration.Unsubscribe.Add(() => control.FocusEntered -= focused);
            registration.Unsubscribe.Add(() => control.FocusExited -= unfocused);
            registration.Unsubscribe.Add(() => control.ChildAdded -= added);
            registration.Unsubscribe.Add(() => control.ChildRemoved -= removed);
            if (control is BaseButton button)
            {
                EventHandler buttonChanged = (_, _) => Evaluate(control, registration);
                Action<BaseButton, bool> toggled = (_, _) => Evaluate(control, registration);
                button.ButtonDown += buttonChanged;
                button.ButtonUp += buttonChanged;
                button.Toggled += toggled;
                registration.Unsubscribe.Add(() => button.ButtonDown -= buttonChanged);
                registration.Unsubscribe.Add(() => button.ButtonUp -= buttonChanged);
                registration.Unsubscribe.Add(() => button.Toggled -= toggled);
            }
        }

        private void Evaluate(Control control, ControlRegistration registration)
        {
            if (_disposed) return;
            for (var styleIndex = 0; styleIndex < _styles.Length; styleIndex++)
            {
                var matches = _styles[styleIndex].Selector.Matches(control, registration.State);
                if (matches == registration.Applied.ContainsKey(styleIndex)) continue;
                if (!matches)
                {
                    var old = registration.Applied[styleIndex];
                    registration.Applied.Remove(styleIndex);
                    for (var index = old.Count - 1; index >= 0; index--) old[index].Dispose();
                    continue;
                }
                var priority = (long)_styles[styleIndex].Selector.Specificity * (_styles.Length + 1L) + styleIndex;
                var applied = new List<IDisposable>();
                foreach (var setter in _styles[styleIndex].Setters) applied.Add(setter.Apply(control, priority));
                registration.Applied.Add(styleIndex, applied);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var control in _controls.Keys.ToArray()) DetachTree(control);
        }
    }
}