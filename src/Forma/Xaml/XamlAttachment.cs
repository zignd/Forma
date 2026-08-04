// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;

namespace Forma.Xaml
{
    public interface IXamlUpdateParticipant
    {
        void Update(GameTime gameTime);
    }

    public static class XamlAttachment
    {
        private static readonly ConditionalWeakTable<Control, XamlAttachmentScope> Scopes =
            new ConditionalWeakTable<Control, XamlAttachmentScope>();

        public static void RegisterDisposable(Control root, IDisposable attachment)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (attachment == null) throw new ArgumentNullException(nameof(attachment));
            Scopes.GetValue(root, control => new XamlAttachmentScope(control)).RegisterDisposable(attachment);
        }

        public static void RegisterUpdateParticipant(Control root, IXamlUpdateParticipant participant)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (participant == null) throw new ArgumentNullException(nameof(participant));
            Scopes.GetValue(root, control => new XamlAttachmentScope(control)).RegisterUpdateParticipant(participant);
        }

        internal static void ContextChanged(Control control, UIContext previous, UIContext current)
        {
            if (Scopes.TryGetValue(control, out var scope)) scope.ContextChanged(previous, current);
        }
    }

    public static class CompiledEvent
    {
        public static IDisposable Attach<TTarget, THandler>(
            Control root,
            TTarget target,
            THandler handler,
            Action<TTarget, THandler> add,
            Action<TTarget, THandler> remove)
            where TTarget : class
            where THandler : Delegate
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (add == null) throw new ArgumentNullException(nameof(add));
            if (remove == null) throw new ArgumentNullException(nameof(remove));
            var subscription = BindingSubscriptions.Event<THandler>(
                value => add(target, value),
                value => remove(target, value),
                handler);
            XamlAttachment.RegisterDisposable(root, subscription);
            return subscription;
        }
    }

    internal sealed class XamlAttachmentScope : IDisposable
    {
        private readonly List<IDisposable> _owned = new List<IDisposable>();
        private readonly List<IXamlUpdateParticipant> _updateParticipants = new List<IXamlUpdateParticipant>();
        private UIContext _context;
        private bool _disposed;

        public XamlAttachmentScope(Control root)
        {
            if (root.Context != null) Activate(root.Context);
        }

        public void RegisterDisposable(IDisposable attachment)
        {
            ThrowIfDisposed();
            _owned.Add(attachment);
        }

        public void RegisterUpdateParticipant(IXamlUpdateParticipant participant)
        {
            ThrowIfDisposed();
            _updateParticipants.Add(participant);
        }

        public void ContextChanged(UIContext previous, UIContext current)
        {
            if (_disposed) return;
            if (previous != null && previous != current)
            {
                Dispose();
                return;
            }
            if (previous == null && current != null) Activate(current);
        }

        public void Update(GameTime gameTime)
        {
            if (_disposed || _context == null) return;
            var snapshot = _updateParticipants.ToArray();
            foreach (var participant in snapshot) participant.Update(gameTime);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _context?.UnregisterXamlScope(this);
            _context = null;
            for (var index = _owned.Count - 1; index >= 0; index--) _owned[index].Dispose();
            _owned.Clear();
            _updateParticipants.Clear();
        }

        private void Activate(UIContext context)
        {
            _context = context;
            context.RegisterXamlScope(this);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(XamlAttachmentScope));
        }
    }
}