// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
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
            TemplateBuildContext.TrackXamlRoot(root);
            Scopes.GetValue(root, control => new XamlAttachmentScope(control)).RegisterDisposable(attachment);
        }

        public static IDisposable RegisterReactivatable(Control root, Func<IDisposable> attach)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (attach == null) throw new ArgumentNullException(nameof(attach));
            TemplateBuildContext.TrackXamlRoot(root);
            var registration = new ReactivatableXamlAttachment(attach);
            try
            {
                Scopes.GetValue(root, control => new XamlAttachmentScope(control)).RegisterReactivatable(registration);
                return registration;
            }
            catch
            {
                registration.Dispose();
                throw;
            }
        }

        internal static void RegisterActivationDisposable(Control root, IDisposable attachment)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (attachment == null) throw new ArgumentNullException(nameof(attachment));
            TemplateBuildContext.TrackXamlRoot(root);
            Scopes.GetValue(root, control => new XamlAttachmentScope(control)).RegisterActivationDisposable(attachment);
        }

        public static void RegisterUpdateParticipant(Control root, IXamlUpdateParticipant participant)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (participant == null) throw new ArgumentNullException(nameof(participant));
            TemplateBuildContext.TrackXamlRoot(root);
            Scopes.GetValue(root, control => new XamlAttachmentScope(control)).RegisterUpdateParticipant(participant);
        }

        internal static void UnregisterActivation(Control root, IDisposable attachment, IXamlUpdateParticipant participant)
        {
            if (root != null && Scopes.TryGetValue(root, out var scope)) scope.UnregisterActivation(attachment, participant);
        }

        internal static XamlAttachmentScope PromoteTemplateScope(Control root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            var scope = Scopes.GetValue(root, control => new XamlAttachmentScope(control));
            scope.PromoteToTemplateOwned();
            return scope;
        }

        internal static void DisposeScope(Control root)
        {
            if (root != null && Scopes.TryGetValue(root, out var scope)) scope.Dispose();
        }

        internal static (int Disposables, int Participants) GetActiveSessionCounts(Control root)
        {
            if (root == null || !Scopes.TryGetValue(root, out var scope)) return (0, 0);
            return scope.ActiveSessionCounts;
        }

        internal static void ContextChanged(Control control, UIContext previous, UIContext current)
        {
            if (Scopes.TryGetValue(control, out var scope)) scope.ContextChanged(previous, current);
        }

        internal static void RenewDisposedScope(Control root)
        {
            if (root != null && Scopes.TryGetValue(root, out var scope) && scope.IsDisposed) Scopes.Remove(root);
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
            return XamlAttachment.RegisterReactivatable(root, () => BindingSubscriptions.Event<THandler>(
                value => add(target, value),
                value => remove(target, value),
                handler));
        }
    }

    internal sealed class XamlAttachmentScope : IDisposable
    {
        private readonly List<IDisposable> _owned = new List<IDisposable>();
        private readonly List<IDisposable> _activationOwned = new List<IDisposable>();
        private readonly List<ReactivatableXamlAttachment> _reactivatable = new List<ReactivatableXamlAttachment>();
        private readonly List<IXamlUpdateParticipant> _updateParticipants = new List<IXamlUpdateParticipant>();
        private readonly List<IXamlUpdateParticipant> _activationUpdateParticipants = new List<IXamlUpdateParticipant>();
        private UIContext _context;
        private Action _disposeOwner;
        private bool _templateOwned;
        private bool _activationRequested;
        private bool _active;
        private bool _disposed;

        public XamlAttachmentScope(Control root)
        {
            if (root.Context != null)
            {
                Associate(root.Context);
                _active = true;
            }
        }

        public void RegisterDisposable(IDisposable attachment)
        {
            ThrowIfDisposed();
            if (_templateOwned && _activationRequested) _activationOwned.Add(attachment);
            else _owned.Add(attachment);
        }

        public void RegisterActivationDisposable(IDisposable attachment)
        {
            ThrowIfDisposed();
            _activationOwned.Add(attachment);
        }

        public void UnregisterActivation(IDisposable attachment, IXamlUpdateParticipant participant)
        {
            if (_disposed) return;
            if (attachment != null) _activationOwned.Remove(attachment);
            if (participant != null)
            {
                _activationUpdateParticipants.Remove(participant);
                _updateParticipants.Remove(participant);
            }
        }

        public (int Disposables, int Participants) ActiveSessionCounts =>
            (_activationOwned.Count, _activationUpdateParticipants.Count + _updateParticipants.Count);

        public bool IsDisposed => _disposed;

        public void RegisterReactivatable(ReactivatableXamlAttachment attachment)
        {
            ThrowIfDisposed();
            _reactivatable.Add(attachment);
        }

        public void RegisterUpdateParticipant(IXamlUpdateParticipant participant)
        {
            ThrowIfDisposed();
            if (_templateOwned && _activationRequested) _activationUpdateParticipants.Add(participant);
            else _updateParticipants.Add(participant);
        }

        public void SetOwnerDispose(Action disposeOwner)
        {
            ThrowIfDisposed();
            if (_disposeOwner != null) throw new InvalidOperationException("The XAML attachment scope already has an owner.");
            _disposeOwner = disposeOwner ?? throw new ArgumentNullException(nameof(disposeOwner));
        }

        public void DisposeOwner()
        {
            if (_disposed) return;
            var disposeOwner = _disposeOwner;
            if (disposeOwner != null) disposeOwner();
            else Dispose();
        }

        public void PromoteToTemplateOwned()
        {
            ThrowIfDisposed();
            if (_templateOwned) return;
            _templateOwned = true;
            _activationRequested = false;
            _active = false;
            DeactivateAttachments()?.Throw();
        }

        public void Activate()
        {
            ThrowIfDisposed();
            if (!_templateOwned) throw new InvalidOperationException("Only template-owned XAML scopes can be reactivated.");
            if (_activationRequested) return;
            _activationRequested = true;
            try
            {
                foreach (var attachment in _reactivatable) attachment.Activate();
            }
            catch
            {
                _activationRequested = false;
                _active = false;
                DeactivateAttachments();
                throw;
            }
            _active = _context != null;
        }

        public void Deactivate()
        {
            if (_disposed) return;
            if (!_templateOwned) throw new InvalidOperationException("Only template-owned XAML scopes can be deactivated.");
            if (!_activationRequested) return;
            _activationRequested = false;
            _active = false;
            DeactivateAttachments()?.Throw();
        }

        public void ContextChanged(UIContext previous, UIContext current)
        {
            if (_disposed) return;
            if (_templateOwned)
            {
                if (current != null && !ReferenceEquals(_context, current)) Associate(current);
                _active = current != null && _activationRequested;
                return;
            }
            if (previous != null && previous != current)
            {
                Dispose();
                return;
            }
            if (previous == null && current != null)
            {
                Associate(current);
                _active = true;
            }
        }

        public void Update(GameTime gameTime)
        {
            if (_disposed || !_active) return;
            var snapshot = _updateParticipants.ToArray();
            foreach (var participant in snapshot) participant.Update(gameTime);
            snapshot = _activationUpdateParticipants.ToArray();
            foreach (var participant in snapshot) participant.Update(gameTime);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _context?.UnregisterXamlScope(this);
            _context = null;
            _activationRequested = false;
            _active = false;
            _disposeOwner = null;
            ExceptionDispatchInfo failure = null;
            var activationOwned = _activationOwned.ToArray();
            _activationOwned.Clear();
            _activationUpdateParticipants.Clear();
            for (var index = activationOwned.Length - 1; index >= 0; index--)
            {
                try { activationOwned[index].Dispose(); }
                catch (Exception exception) { failure ??= ExceptionDispatchInfo.Capture(exception); }
            }
            for (var index = _reactivatable.Count - 1; index >= 0; index--)
            {
                try { _reactivatable[index].Dispose(); }
                catch (Exception exception) { failure ??= ExceptionDispatchInfo.Capture(exception); }
            }
            for (var index = _owned.Count - 1; index >= 0; index--)
            {
                try { _owned[index].Dispose(); }
                catch (Exception exception) { failure ??= ExceptionDispatchInfo.Capture(exception); }
            }
            _reactivatable.Clear();
            _owned.Clear();
            _updateParticipants.Clear();
            _activationUpdateParticipants.Clear();
            failure?.Throw();
        }

        private void Associate(UIContext context)
        {
            _context?.UnregisterXamlScope(this);
            _context = context;
            context.RegisterXamlScope(this);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(XamlAttachmentScope));
        }

        private ExceptionDispatchInfo DeactivateAttachments()
        {
            ExceptionDispatchInfo failure = null;
            var activationOwned = _activationOwned.ToArray();
            _activationOwned.Clear();
            _activationUpdateParticipants.Clear();
            for (var index = activationOwned.Length - 1; index >= 0; index--)
            {
                try { activationOwned[index].Dispose(); }
                catch (Exception exception) { failure ??= ExceptionDispatchInfo.Capture(exception); }
            }
            for (var index = _reactivatable.Count - 1; index >= 0; index--)
            {
                try { _reactivatable[index].Deactivate(); }
                catch (Exception exception) { failure ??= ExceptionDispatchInfo.Capture(exception); }
            }
            return failure;
        }
    }

    internal sealed class ReactivatableXamlAttachment : IDisposable
    {
        private readonly Func<IDisposable> _attach;
        private IDisposable _activeAttachment;
        private bool _disposed;

        public ReactivatableXamlAttachment(Func<IDisposable> attach)
        {
            _attach = attach;
            Activate();
        }

        public void Activate()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ReactivatableXamlAttachment));
            if (_activeAttachment != null) return;
            _activeAttachment = _attach() ?? throw new InvalidOperationException("A reactivatable XAML attachment factory returned null.");
        }

        public void Deactivate()
        {
            var attachment = _activeAttachment;
            _activeAttachment = null;
            attachment?.Dispose();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Deactivate();
        }
    }
}