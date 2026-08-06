// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.ExceptionServices;
using System.Threading;
using Forma.Xaml;

namespace Forma
{
    /// <summary>
    /// Opts a custom item container into pooling. Implementations reset container-local interaction and validation
    /// state in <see cref="OnRecycling"/> and prepare local state for the next occurrence in <see cref="OnReused"/>.
    /// </summary>
    public interface IRecyclableItemContainer
    {
        void OnRecycling();
        void OnReused(object item);
    }

    public sealed class ItemsControlRealizationDiagnostic
    {
        public ItemsControlRealizationDiagnostic(int index, object item, string message)
        {
            Index = index;
            Item = item;
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        public int Index { get; }
        public object Item { get; }
        public string Message { get; }
    }

    public class ItemsControl : TemplatedControl, IItemsPresenterOwner, IItemContainerGenerator, IItemContainerGeneratorDiagnostics, IItemContainerGeneratorAnchors
    {
        private static readonly ItemsPanelTemplate DefaultItemsPanel = new ItemsPanelTemplate(_ => new StackPanel());
        private readonly List<ItemsControlRealizationDiagnostic> _realizationDiagnostics = new List<ItemsControlRealizationDiagnostic>();
        private readonly Dictionary<Control, ItemSlot> _realizedByContainer = new Dictionary<Control, ItemSlot>();
        private readonly List<PooledContainer> _recyclePool = new List<PooledContainer>();
        private CollectionViewAdapter _itemsView;
        private IEnumerable _itemsSource;
        private DataTemplate _itemTemplate;
        private ItemsPanelTemplate _itemsPanel = DefaultItemsPanel;
        private Style _itemContainerStyle;
        private int _alternationCount;
        private ItemsPresenter _attachedPresenter;
        private Container _attachedPanel;
        private UIContext _poolContext;
        private long _poolThemeGeneration = -1;
        private int _recyclePoolCapacity = 64;
        private Func<object, object> _itemKeySelector;
        private FocusBookmark _focusBookmark;
        private event EventHandler<ItemGeneratorChangedEventArgs> GeneratorChanged;

        public ItemsControl()
        {
            _itemsView = new CollectionViewAdapter(null, OnCollectionChanged);
        }

        public IEnumerable ItemsSource
        {
            get => _itemsSource;
            set
            {
                if (ReferenceEquals(_itemsSource, value)) return;
                var itemsView = new CollectionViewAdapter(value, OnCollectionChanged);
                var previousView = _itemsView;
                CancelFocusBookmark();
                ClearRealizedItems();
                _itemsSource = value;
                _itemsView = itemsView;
                previousView.Dispose();
                OnItemsChanged();
                OnPropertyChanged(nameof(ItemsSource));
                EnsurePresenterAndRealize();
            }
        }

        public DataTemplate ItemTemplate
        {
            get => _itemTemplate;
            set
            {
                if (ReferenceEquals(_itemTemplate, value)) return;
                CancelFocusBookmark();
                ClearRealizedItems();
                DrainRecyclePool();
                _itemTemplate = value;
                OnPropertyChanged(nameof(ItemTemplate));
                EnsurePresenterAndRealize();
            }
        }

        public ItemsPanelTemplate ItemsPanel
        {
            get => _itemsPanel;
            set
            {
                value ??= DefaultItemsPanel;
                if (ReferenceEquals(_itemsPanel, value)) return;
                _itemsPanel = value;
                OnPropertyChanged(nameof(ItemsPanel));
                if (_attachedPresenter != null) _attachedPresenter.ItemsPanel = value;
            }
        }

        public Style ItemContainerStyle
        {
            get => _itemContainerStyle;
            set
            {
                if (ReferenceEquals(_itemContainerStyle, value)) return;
                CancelFocusBookmark();
                ClearRealizedItems();
                DrainRecyclePool();
                _itemContainerStyle = value;
                OnPropertyChanged(nameof(ItemContainerStyle));
                RebuildRealizedItems();
            }
        }

        public int AlternationCount
        {
            get => _alternationCount;
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
                if (_alternationCount == value) return;
                _alternationCount = value;
                OnPropertyChanged(nameof(AlternationCount));
                RebuildRealizedItems();
            }
        }

        public int RealizedCount
            => _realizedByContainer.Count;
        public int RecycledCount => _recyclePool.Count;
        public int PinnedCount => 0;
        public int RecyclePoolCapacity
        {
            get => _recyclePoolCapacity;
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
                if (_recyclePoolCapacity == value) return;
                _recyclePoolCapacity = value;
                while (_recyclePool.Count > value) DisposePooledAt(0);
            }
        }
        public Func<object, object> ItemKeySelector
        {
            get => _itemKeySelector;
            set
            {
                if (ReferenceEquals(_itemKeySelector, value)) return;
                _itemKeySelector = value;
                CancelFocusBookmark();
                OnPropertyChanged(nameof(ItemKeySelector));
            }
        }
        public IReadOnlyList<ItemsControlRealizationDiagnostic> RealizationDiagnostics => _realizationDiagnostics;
        public override AccessibilityRole AccessibilityRole => AccessibilityRole.List;
        public Control GetRealizedContainer(int index) => GetRealizedItem(index).Container;
        public int GetAlternationIndex(int index) => GetRealizedItem(index).AlternationIndex;
        protected virtual bool CanRealizeItems => _itemTemplate != null;
        protected virtual void UpdateContainerPosition(Control container, object item, int index, int alternationIndex) { }
        Control IItemsPresenterOwner.ItemsPresenterInheritanceParent => this;
        int IItemContainerGenerator.Count => CanRealizeItems ? _itemsView.Slots.Count : 0;
        Control IItemContainerGenerator.ContainerInheritanceParent => this;
        object IItemContainerGenerator.EstimateScope => _itemTemplate ?? (object)GetType();
        event EventHandler<ItemGeneratorChangedEventArgs> IItemContainerGenerator.Changed
        {
            add => GeneratorChanged += value;
            remove => GeneratorChanged -= value;
        }
        object IItemContainerGenerator.GetToken(int index) => _itemsView.Slots[index];
        Control IItemContainerGenerator.Realize(int index) => CreateRealization(_itemsView.Slots[index], index);
        void IItemContainerGenerator.Recycle(int index, Control container) => RecycleGeneratedContainer(index, container);
        bool IItemContainerGeneratorAnchors.HasItemKeys => _itemKeySelector != null;
        object IItemContainerGeneratorAnchors.GetItemKey(int index) => _itemKeySelector?.Invoke(_itemsView.Slots[index].Item);
        int IItemContainerGeneratorAnchors.FindIndexByKey(object key)
        {
            if (_itemKeySelector == null) return -1;
            for (var index = 0; index < _itemsView.Slots.Count; index++)
                if (Equals(_itemKeySelector(_itemsView.Slots[index].Item), key)) return index;
            return -1;
        }
        void IItemContainerGeneratorAnchors.OnContainerAttached(int index, Control container) => RestoreFocusBookmark(index, container);
        void IItemContainerGeneratorAnchors.OnContainerRecycling(int index, Control container) => CaptureFocusBookmark(index, container);

        protected virtual Control GetContainerForItem(object item) => new GeneratedItemContainer();

        protected virtual void PrepareContainerForItem(Control container, object item)
        {
            if (container is not ContentControl contentContainer)
                throw new InvalidOperationException("Base ItemsControl containers must derive from ContentControl.");
            container.DataContext = item;
            container.Classes.Add("item-container");
            contentContainer.ContentTemplate = _itemTemplate;
            contentContainer.SetGeneratedContent(item);
        }

        protected virtual void PrepareContainerForItem(Control container, object item, int index) =>
            PrepareContainerForItem(container, item);

        protected virtual void ClearContainerForItem(Control container, object item)
        {
            if (container is ContentControl contentContainer)
            {
                contentContainer.Content = null;
                contentContainer.ContentTemplate = null;
            }
            container.Classes.Remove("item-container");
            container.DataContext = null;
        }

        protected virtual bool IsContainerCompatibleForItem(Control container, object item) =>
            container is GeneratedItemContainer;

        protected int ItemCount => _itemsView.Slots.Count;
        protected object GetItemAt(int index) => _itemsView.Slots[index].Item;
        protected object GetItemToken(int index) => _itemsView.Slots[index];
        protected int IndexOfItemToken(object token)
        {
            for (var index = 0; index < _itemsView.Slots.Count; index++)
                if (ReferenceEquals(_itemsView.Slots[index], token)) return index;
            return -1;
        }
        protected bool TryGetRealizedContainer(int index, out Control container)
        {
            var realization = _itemsView.Slots[index].Realization;
            container = realization?.Container;
            return container != null;
        }
        internal int GetAccessibilityItemIndex(object token) => IndexOfItemToken(token);
        internal object GetAccessibilityItem(object token)
        {
            var index = IndexOfItemToken(token);
            return index < 0 ? null : GetItemAt(index);
        }
        internal bool TryGetAccessibilityItemContainer(int index, out Control container) => TryGetRealizedContainer(index, out container);
        internal virtual AccessibilityRole GetAccessibilityItemRole(int index) => AccessibilityRole.ListItem;
        internal virtual bool IsAccessibilityItemSelected(int index) => false;
        internal virtual bool IsAccessibilityItemCurrent(int index) => false;
        public override IReadOnlyList<AccessibilityPeer> GetAccessibilityChildren()
        {
            if (_itemsView.Slots.Count == 0) return Array.Empty<AccessibilityPeer>();
            var peers = new AccessibilityPeer[_itemsView.Slots.Count];
            for (var index = 0; index < _itemsView.Slots.Count; index++)
            {
                var slot = _itemsView.Slots[index];
                peers[index] = slot.AccessibilityPeer ??= new ItemAccessibilityPeer(this, slot);
            }
            return peers;
        }
        protected virtual void OnItemsChanged() { }

        void IItemsPresenterOwner.AttachItemsPanel(ItemsPresenter presenter, Container panel)
        {
            if (_attachedPanel != null && !ReferenceEquals(_attachedPanel, panel))
                throw new InvalidOperationException("An ItemsControl can attach only one items panel.");
            _attachedPresenter = presenter;
            _attachedPanel = panel;
            if (panel is VirtualizingPanel virtualizingPanel)
            {
                UpdateMissingTemplateDiagnostic();
                virtualizingPanel.Generator = this;
            }
            else RebuildRealizedItems();
        }

        void IItemsPresenterOwner.DetachItemsPanel(ItemsPresenter presenter, Container panel)
        {
            if (!ReferenceEquals(_attachedPanel, panel)) return;
            if (panel is VirtualizingPanel virtualizingPanel) virtualizingPanel.Generator = null;
            ClearRealizedItems();
            _attachedPresenter = null;
            _attachedPanel = null;
            DrainRecyclePool();
        }

        private void EnsurePresenterAndRealize()
        {
            if (TemplateRoot == null && (_itemTemplate != null || _itemsView.Slots.Count != 0)) ApplyTemplate();
            else if (_attachedPanel is VirtualizingPanel) ResetVirtualizedItems();
            else RebuildRealizedItems();
        }

        private void RebuildRealizedItems()
        {
            if (_attachedPanel == null) return;
            if (_attachedPanel is VirtualizingPanel)
            {
                ResetVirtualizedItems();
                return;
            }
            ClearRealizedItems();
            _realizationDiagnostics.Clear();
            if (_itemsView.Slots.Count == 0) return;
            if (!CanRealizeItems)
            {
                _realizationDiagnostics.Add(new ItemsControlRealizationDiagnostic(-1, null,
                    "ItemsControl requires an explicit ItemTemplate before items can be realized."));
                return;
            }

            for (var index = 0; index < _itemsView.Slots.Count; index++)
            {
                try
                {
                    RealizeSlot(_itemsView.Slots[index], index);
                }
                catch (Exception exception)
                {
                    _realizationDiagnostics.Add(new ItemsControlRealizationDiagnostic(index, _itemsView.Slots[index].Item, exception.Message));
                    ClearRealizedItems();
                    return;
                }
            }
            _attachedPanel.QueueLayout();
        }

        private void ClearRealizedItems()
        {
            for (var index = _itemsView.Slots.Count - 1; index >= 0; index--)
            {
                ClearSlot(_itemsView.Slots[index]);
            }
        }

        private void OnCollectionChanged(CollectionViewChange change)
        {
            OnItemsChanged();
            if (_attachedPanel == null) return;
            _realizationDiagnostics.Clear();
            if (_attachedPanel is VirtualizingPanel)
            {
                foreach (var slot in change.RemovedSlots) ClearSlot(slot);
                UpdateRealizationPositions();
                UpdateMissingTemplateDiagnostic();
                GeneratorChanged?.Invoke(this, new ItemGeneratorChangedEventArgs(
                    ToGeneratorAction(change.Action), change.OldIndex, change.NewIndex,
                    change.Count));
                return;
            }
            if (change.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var slot in change.RemovedSlots) ClearSlot(slot);
                RebuildRealizedItems();
                return;
            }
            if (!CanRealizeItems || !HasCompleteRealization(change))
            {
                RebuildRealizedItems();
                return;
            }

            try
            {
                foreach (var slot in change.RemovedSlots) ClearSlot(slot);
                if (change.Action == NotifyCollectionChangedAction.Add || change.Action == NotifyCollectionChangedAction.Replace)
                {
                    for (var index = 0; index < change.AddedSlots.Count; index++)
                        RealizeSlot(change.AddedSlots[index], change.NewIndex + index);
                }
                ReorderRealizedItems();
            }
            catch (Exception exception)
            {
                var index = Math.Max(0, change.NewIndex);
                var item = index < _itemsView.Slots.Count ? _itemsView.Slots[index].Item : null;
                _realizationDiagnostics.Add(new ItemsControlRealizationDiagnostic(index, item, exception.Message));
                ClearRealizedItems();
            }
        }

        private bool HasCompleteRealization(CollectionViewChange change)
        {
            foreach (var slot in _itemsView.Slots)
            {
                var isAdded = false;
                foreach (var added in change.AddedSlots)
                    if (ReferenceEquals(slot, added)) { isAdded = true; break; }
                if (!isAdded && slot.Realization == null) return false;
            }
            return true;
        }

        private void RealizeSlot(ItemSlot slot, int index)
        {
            var container = CreateRealization(slot, index);
            try
            {
                _attachedPanel.AddVisualChild(container, this);
            }
            catch
            {
                ClearSlot(slot);
                throw;
            }
        }

        private Control CreateRealization(ItemSlot slot, int index)
        {
            if (slot.Realization != null) return slot.Realization.Container;
            var container = TakePooledContainer(slot.Item);
            if (container != null) XamlAttachment.RenewDisposedScope(container);
            IDisposable style = null;
            try
            {
                container ??= GetContainerForItem(slot.Item)
                    ?? throw new InvalidOperationException("GetContainerForItem returned null.");
                if (container.Parent != null || container.VisualParent != null)
                    throw new InvalidOperationException("A generated item container must be detached before preparation.");
                (container as IRecyclableItemContainer)?.OnReused(slot.Item);
                PrepareContainerForItem(container, slot.Item, index);
                if (_itemContainerStyle != null) style = StyleEngine.Attach(container, new[] { _itemContainerStyle });
                slot.RealizationGeneration++;
                slot.Realization = new RealizedItem(container, slot.Item, index,
                    _alternationCount == 0 ? 0 : index % _alternationCount, slot.RealizationGeneration, style, _itemContainerStyle);
                _realizedByContainer.Add(container, slot);
                return container;
            }
            catch
            {
                style?.Dispose();
                if (container != null)
                {
                    try { ClearContainerForItem(container, slot.Item); } catch { }
                    if (container is IDisposable disposable) disposable.Dispose();
                }
                throw;
            }
        }

        private void ClearSlot(ItemSlot slot, bool preserveFocusBookmark = false)
        {
            if (!preserveFocusBookmark && ReferenceEquals(_focusBookmark?.Token, slot)) CancelFocusBookmark();
            var realization = slot.Realization;
            if (realization == null) return;
            slot.Realization = null;
            _realizedByContainer.Remove(realization.Container);
            var context = realization.Container.Context ?? Context;
            context?.ResetInteractionState(realization.Container);
            if (realization.Container.VisualParent == _attachedPanel) _attachedPanel.RemoveVisualChild(realization.Container);
            if (TryPool(realization)) return;
            ClearContainerForItem(realization.Container, slot.Item);
            realization.Dispose();
        }

        private void ReorderRealizedItems()
        {
            for (var index = 0; index < _itemsView.Slots.Count; index++)
            {
                var slot = _itemsView.Slots[index];
                var container = slot.Realization.Container;
                var alternationIndex = _alternationCount == 0 ? 0 : index % _alternationCount;
                slot.Realization.UpdatePosition(index, alternationIndex);
                UpdateContainerPosition(container, slot.Item, index, alternationIndex);
                _attachedPanel.MoveVisualChild(container, index);
            }
            _attachedPanel.QueueLayout();
        }

        private void UpdateRealizationPositions()
        {
            for (var index = 0; index < _itemsView.Slots.Count; index++)
            {
                var slot = _itemsView.Slots[index];
                var realization = slot.Realization;
                if (realization == null) continue;
                var alternationIndex = _alternationCount == 0 ? 0 : index % _alternationCount;
                realization.UpdatePosition(index, alternationIndex);
                UpdateContainerPosition(realization.Container, slot.Item, index, alternationIndex);
            }
        }

        private void RecycleGeneratedContainer(int index, Control container)
        {
            if (_realizedByContainer.TryGetValue(container, out var slot)) ClearSlot(slot, true);
        }

        private void CaptureFocusBookmark(int index, Control container)
        {
            if (Context == null || !_realizedByContainer.TryGetValue(container, out var slot)) return;
            var focused = Context.FocusedControl;
            if (!TryGetVisualPath(container, focused, out var path)) return;
            var generation = slot.Realization?.Generation ?? -1;
            Context.SetFocus(this);
            if (!ReferenceEquals(Context.FocusedControl, this)) return;
            _focusBookmark = new FocusBookmark(
                slot,
                path,
                generation,
                _itemTemplate?.FactoryVersion ?? 0,
                Context.ThemeGeneration);
        }

        private void RestoreFocusBookmark(int index, Control container)
        {
            var bookmark = _focusBookmark;
            if (bookmark == null) return;
            if (Context == null || !ReferenceEquals(Context.FocusedControl, this) ||
                (_itemTemplate?.FactoryVersion ?? 0) != bookmark.DataTemplateVersion ||
                Context.ThemeGeneration != bookmark.ThemeGeneration)
            {
                _focusBookmark = null;
                return;
            }
            if (index < 0 || index >= _itemsView.Slots.Count || !ReferenceEquals(_itemsView.Slots[index], bookmark.Token)) return;
            _focusBookmark = null;
            if (_itemsView.Slots[index].Realization?.Generation != bookmark.RealizationGeneration + 1) return;
            var target = FollowVisualPath(container, bookmark.FocusPath);
            if (target == null || target.Context != Context || !target.IsEffectivelyEnabled || target.FocusMode == FocusMode.None) return;
            Context.SetFocus(target);
        }

        private void CancelFocusBookmark() => _focusBookmark = null;

        private static bool TryGetVisualPath(Control root, Control descendant, out int[] path)
        {
            var reversed = new List<int>();
            var current = descendant;
            while (current != null && !ReferenceEquals(current, root))
            {
                var parent = current.VisualParent;
                if (parent == null)
                {
                    path = null;
                    return false;
                }
                var childIndex = IndexOfVisualChild(parent, current);
                if (childIndex < 0)
                {
                    path = null;
                    return false;
                }
                reversed.Add(childIndex);
                current = parent;
            }
            if (!ReferenceEquals(current, root))
            {
                path = null;
                return false;
            }
            reversed.Reverse();
            path = reversed.ToArray();
            return true;
        }

        private static Control FollowVisualPath(Control root, IReadOnlyList<int> path)
        {
            var current = root;
            foreach (var index in path)
            {
                if (index < 0 || index >= current.VisualChildren.Count) return null;
                current = current.VisualChildren[index];
            }
            return current;
        }

        private static int IndexOfVisualChild(Control parent, Control child)
        {
            for (var index = 0; index < parent.VisualChildren.Count; index++)
                if (ReferenceEquals(parent.VisualChildren[index], child)) return index;
            return -1;
        }

        private bool TryPool(RealizedItem realization)
        {
            if (_attachedPanel is not VirtualizingPanel || RecyclePoolCapacity == 0 || Context == null) return false;
            if (realization.Container is not GeneratedItemContainer && realization.Container is not IRecyclableItemContainer) return false;
            if (realization.Container is not ContentControl content || !content.TryDeactivateGeneratedContentForRecycle()) return false;
            try
            {
                EnsureRecyclePoolGeneration();
                realization.DisposeStyle();
                realization.Container.Classes.Remove("item-container");
                realization.Container.DataContext = null;
                (realization.Container as IRecyclableItemContainer)?.OnRecycling();
                var controlTemplate = (realization.Container as TemplatedControl)?.AppliedTemplate;
                var dataTemplate = content.ContentTemplate;
                while (_recyclePool.Count >= RecyclePoolCapacity) DisposePooledAt(0);
                _recyclePool.Add(new PooledContainer(
                    realization.Container,
                    controlTemplate,
                    dataTemplate,
                    realization.StyleIdentity,
                    Context,
                    Context.ThemeGeneration));
                return true;
            }
            catch
            {
                realization.Dispose();
                throw;
            }
        }

        private Control TakePooledContainer(object item)
        {
            if (_attachedPanel is not VirtualizingPanel || _recyclePool.Count == 0) return null;
            EnsureRecyclePoolGeneration();
            for (var index = _recyclePool.Count - 1; index >= 0; index--)
            {
                var candidate = _recyclePool[index];
                if (!candidate.IsCurrent(this))
                {
                    DisposePooledAt(index);
                    continue;
                }
                if (!IsContainerCompatibleForItem(candidate.Container, item)) continue;
                _recyclePool.RemoveAt(index);
                return candidate.Container;
            }
            return null;
        }

        private void EnsureRecyclePoolGeneration()
        {
            var generation = Context?.ThemeGeneration ?? -1;
            if (ReferenceEquals(_poolContext, Context) && _poolThemeGeneration == generation) return;
            DrainRecyclePool();
            _poolContext = Context;
            _poolThemeGeneration = generation;
        }

        private void DrainRecyclePool()
        {
            ExceptionDispatchInfo failure = null;
            for (var index = _recyclePool.Count - 1; index >= 0; index--)
            {
                try { DisposePooledAt(index); }
                catch (Exception exception) { failure ??= ExceptionDispatchInfo.Capture(exception); }
            }
            failure?.Throw();
        }

        private void DisposePooledAt(int index)
        {
            var pooled = _recyclePool[index];
            _recyclePool.RemoveAt(index);
            if (pooled.Container is IDisposable disposable) disposable.Dispose();
        }

        private void ResetVirtualizedItems()
        {
            ClearRealizedItems();
            _realizationDiagnostics.Clear();
            UpdateMissingTemplateDiagnostic();
            GeneratorChanged?.Invoke(this, new ItemGeneratorChangedEventArgs(
                ItemGeneratorChangeAction.Reset, -1, 0, CanRealizeItems ? _itemsView.Slots.Count : 0));
        }

        private void UpdateMissingTemplateDiagnostic()
        {
            if (_itemsView.Slots.Count != 0 && !CanRealizeItems)
                _realizationDiagnostics.Add(new ItemsControlRealizationDiagnostic(-1, null,
                    "ItemsControl requires an explicit ItemTemplate before items can be realized."));
        }

        private static ItemGeneratorChangeAction ToGeneratorAction(NotifyCollectionChangedAction action) => action switch
        {
            NotifyCollectionChangedAction.Add => ItemGeneratorChangeAction.Add,
            NotifyCollectionChangedAction.Remove => ItemGeneratorChangeAction.Remove,
            NotifyCollectionChangedAction.Replace => ItemGeneratorChangeAction.Replace,
            NotifyCollectionChangedAction.Move => ItemGeneratorChangeAction.Move,
            NotifyCollectionChangedAction.Reset => ItemGeneratorChangeAction.Reset,
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };

        private RealizedItem GetRealizedItem(int index)
        {
            var realization = _itemsView.Slots[index].Realization;
            if (realization == null) throw new InvalidOperationException($"Item at index {index} is not realized.");
            return realization;
        }

        protected override void OnContextChanged(UIContext previous, UIContext current)
        {
            if (previous != null) previous.ThemeGenerationChanged -= OnThemeGenerationChanged;
            DrainRecyclePool();
            _poolContext = current;
            _poolThemeGeneration = current?.ThemeGeneration ?? -1;
            if (current != null) current.ThemeGenerationChanged += OnThemeGenerationChanged;
            base.OnContextChanged(previous, current);
        }

        private void OnThemeGenerationChanged(object sender, EventArgs args)
        {
            CancelFocusBookmark();
            DrainRecyclePool();
            _poolThemeGeneration = Context?.ThemeGeneration ?? -1;
        }

        public override void Dispose()
        {
            if (Context != null) Context.ThemeGenerationChanged -= OnThemeGenerationChanged;
            if (_attachedPresenter != null) _attachedPresenter.Owner = null;
            _itemsView.Dispose();
            try { base.Dispose(); }
            finally { DrainRecyclePool(); }
        }

        private sealed class RealizedItem : IDisposable
        {
            private IDisposable _style;
            private bool _disposed;

            public RealizedItem(Control container, object item, int index, int alternationIndex, long generation, IDisposable style, Style styleIdentity)
            {
                Container = container;
                Item = item;
                Index = index;
                AlternationIndex = alternationIndex;
                Generation = generation;
                _style = style;
                StyleIdentity = styleIdentity;
            }

            public Control Container { get; }
            public object Item { get; }
            public int Index { get; private set; }
            public int AlternationIndex { get; private set; }
            public long Generation { get; }
            public Style StyleIdentity { get; }

            public void UpdatePosition(int index, int alternationIndex)
            {
                Index = index;
                AlternationIndex = alternationIndex;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                DisposeStyle();
                if (Container is IDisposable disposable) disposable.Dispose();
            }

            public void DisposeStyle()
            {
                var style = _style;
                _style = null;
                style?.Dispose();
            }
        }

        private sealed class GeneratedItemContainer : ContentControl, IRecyclableItemContainer
        {
            public void OnRecycling() { }
            public void OnReused(object item) { }
        }

        private sealed class PooledContainer
        {
            public PooledContainer(
                Control container,
                ControlTemplate controlTemplate,
                DataTemplate dataTemplate,
                Style style,
                UIContext context,
                long themeGeneration)
            {
                Container = container;
                ContainerType = container.GetType();
                ControlTemplate = controlTemplate;
                ControlTemplateVersion = controlTemplate?.FactoryVersion ?? 0;
                DataTemplate = dataTemplate;
                DataTemplateVersion = dataTemplate?.FactoryVersion ?? 0;
                Style = style;
                StyleGeneration = style?.Generation ?? 0;
                Context = context;
                ThemeGeneration = themeGeneration;
            }

            public Control Container { get; }
            public Type ContainerType { get; }
            public ControlTemplate ControlTemplate { get; }
            public long ControlTemplateVersion { get; }
            public DataTemplate DataTemplate { get; }
            public long DataTemplateVersion { get; }
            public Style Style { get; }
            public long StyleGeneration { get; }
            public UIContext Context { get; }
            public long ThemeGeneration { get; }

            public bool IsCurrent(ItemsControl owner)
            {
                var currentControlTemplate = (Container as TemplatedControl)?.ResolveTemplate();
                return ReferenceEquals(Context, owner.Context) && ThemeGeneration == (owner.Context?.ThemeGeneration ?? -1) &&
                    ReferenceEquals(DataTemplate, owner._itemTemplate) && DataTemplateVersion == (owner._itemTemplate?.FactoryVersion ?? 0) &&
                    Container.GetType() == ContainerType && ReferenceEquals(Style, owner._itemContainerStyle) &&
                    StyleGeneration == (owner._itemContainerStyle?.Generation ?? 0) &&
                    ReferenceEquals(ControlTemplate, currentControlTemplate) &&
                    ControlTemplateVersion == (currentControlTemplate?.FactoryVersion ?? 0);
            }
        }

        private sealed class ItemSlot
        {
            public ItemSlot(object item) => Item = item;
            public object Item { get; }
            public RealizedItem Realization { get; set; }
            public long RealizationGeneration { get; set; }
            public ItemAccessibilityPeer AccessibilityPeer { get; set; }
        }

        private sealed class FocusBookmark
        {
            public FocusBookmark(ItemSlot token, int[] focusPath, long realizationGeneration, long dataTemplateVersion, long themeGeneration)
            {
                Token = token;
                FocusPath = focusPath;
                RealizationGeneration = realizationGeneration;
                DataTemplateVersion = dataTemplateVersion;
                ThemeGeneration = themeGeneration;
            }

            public ItemSlot Token { get; }
            public int[] FocusPath { get; }
            public long RealizationGeneration { get; }
            public long DataTemplateVersion { get; }
            public long ThemeGeneration { get; }
        }

        private sealed class CollectionViewChange
        {
            public CollectionViewChange(
                NotifyCollectionChangedAction action,
                int oldIndex,
                int newIndex,
                int count,
                IReadOnlyList<ItemSlot> addedSlots,
                IReadOnlyList<ItemSlot> removedSlots)
            {
                Action = action;
                OldIndex = oldIndex;
                NewIndex = newIndex;
                Count = count;
                AddedSlots = addedSlots;
                RemovedSlots = removedSlots;
            }

            public NotifyCollectionChangedAction Action { get; }
            public int OldIndex { get; }
            public int NewIndex { get; }
            public int Count { get; }
            public IReadOnlyList<ItemSlot> AddedSlots { get; }
            public IReadOnlyList<ItemSlot> RemovedSlots { get; }
        }

        private sealed class CollectionViewAdapter : IDisposable
        {
            private readonly IEnumerable _source;
            private readonly IList _indexedSource;
            private readonly INotifyCollectionChanged _observableSource;
            private readonly Action<CollectionViewChange> _changed;
            private readonly int _attachedThreadId;
            private readonly List<ItemSlot> _slots;
            private bool _disposed;

            public CollectionViewAdapter(IEnumerable source, Action<CollectionViewChange> changed)
            {
                _source = source;
                _indexedSource = source as IList;
                _observableSource = source as INotifyCollectionChanged;
                _changed = changed;
                _attachedThreadId = Environment.CurrentManagedThreadId;
                _slots = CreateSlots(source, _indexedSource);
                if (_observableSource != null) _observableSource.CollectionChanged += OnCollectionChanged;
            }

            public IReadOnlyList<ItemSlot> Slots => _slots;

            private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
            {
                if (Environment.CurrentManagedThreadId != _attachedThreadId)
                    throw new InvalidOperationException("ItemsSource collection notifications must be raised on the thread where the source was attached.");

                var proposed = new List<ItemSlot>(_slots);
                var added = new List<ItemSlot>();
                var removed = new List<ItemSlot>();
                var newIndex = args.NewStartingIndex;
                switch (args.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        RequireItems(args.NewItems, "add");
                        ValidateInsertIndex(args.NewStartingIndex, proposed.Count, "add");
                        foreach (var item in args.NewItems) added.Add(new ItemSlot(item));
                        proposed.InsertRange(args.NewStartingIndex, added);
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        RequireItems(args.OldItems, "remove");
                        ValidateRange(args.OldStartingIndex, args.OldItems.Count, proposed.Count, "remove");
                        ValidateItems(proposed, args.OldStartingIndex, args.OldItems, "remove");
                        removed.AddRange(proposed.GetRange(args.OldStartingIndex, args.OldItems.Count));
                        proposed.RemoveRange(args.OldStartingIndex, args.OldItems.Count);
                        newIndex = -1;
                        break;
                    case NotifyCollectionChangedAction.Replace:
                        RequireItems(args.OldItems, "replace");
                        RequireItems(args.NewItems, "replace");
                        if (args.OldItems.Count != args.NewItems.Count)
                            throw new InvalidOperationException("Replace notifications must contain the same number of old and new items.");
                        ValidateRange(args.OldStartingIndex, args.OldItems.Count, proposed.Count, "replace");
                        ValidateItems(proposed, args.OldStartingIndex, args.OldItems, "replace");
                        removed.AddRange(proposed.GetRange(args.OldStartingIndex, args.OldItems.Count));
                        proposed.RemoveRange(args.OldStartingIndex, args.OldItems.Count);
                        foreach (var item in args.NewItems) added.Add(new ItemSlot(item));
                        proposed.InsertRange(args.NewStartingIndex, added);
                        break;
                    case NotifyCollectionChangedAction.Move:
                        RequireItems(args.OldItems, "move");
                        ValidateRange(args.OldStartingIndex, args.OldItems.Count, proposed.Count, "move");
                        ValidateItems(proposed, args.OldStartingIndex, args.OldItems, "move");
                        var moved = proposed.GetRange(args.OldStartingIndex, args.OldItems.Count);
                        proposed.RemoveRange(args.OldStartingIndex, args.OldItems.Count);
                        ValidateInsertIndex(args.NewStartingIndex, proposed.Count, "move");
                        proposed.InsertRange(args.NewStartingIndex, moved);
                        break;
                    case NotifyCollectionChangedAction.Reset:
                        removed.AddRange(proposed);
                        proposed = CreateSlots(_source, _indexedSource);
                        added.AddRange(proposed);
                        newIndex = 0;
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported collection action '{args.Action}'.");
                }

                ValidateIndexedSource(proposed);
                _slots.Clear();
                _slots.AddRange(proposed);
                var count = Math.Max(args.OldItems?.Count ?? 0, args.NewItems?.Count ?? 0);
                _changed?.Invoke(new CollectionViewChange(args.Action, args.OldStartingIndex, newIndex, count, added, removed));
            }

            private void ValidateIndexedSource(IReadOnlyList<ItemSlot> proposed)
            {
                if (_indexedSource == null) return;
                if (_indexedSource.Count != proposed.Count)
                    throw new InvalidOperationException("The collection notification count does not match the indexed ItemsSource.");
                for (var index = 0; index < proposed.Count; index++)
                    if (!ItemsEqual(proposed[index].Item, _indexedSource[index]))
                        throw new InvalidOperationException($"The collection notification does not match ItemsSource at index {index}.");
            }

            private static List<ItemSlot> CreateSlots(IEnumerable source, IList indexedSource)
            {
                var slots = new List<ItemSlot>();
                if (indexedSource != null)
                {
                    for (var index = 0; index < indexedSource.Count; index++) slots.Add(new ItemSlot(indexedSource[index]));
                }
                else if (source != null)
                {
                    foreach (var item in source) slots.Add(new ItemSlot(item));
                }
                return slots;
            }

            private static void RequireItems(IList items, string action)
            {
                if (items == null || items.Count == 0)
                    throw new InvalidOperationException($"Collection {action} notifications must contain items.");
            }

            private static void ValidateInsertIndex(int index, int count, string action)
            {
                if (index < 0 || index > count)
                    throw new InvalidOperationException($"Collection {action} index {index} is outside the valid range.");
            }

            private static void ValidateRange(int index, int length, int count, string action)
            {
                if (index < 0 || length < 1 || index > count - length)
                    throw new InvalidOperationException($"Collection {action} range is outside the current item slots.");
            }

            private static void ValidateItems(IReadOnlyList<ItemSlot> slots, int index, IList items, string action)
            {
                for (var offset = 0; offset < items.Count; offset++)
                    if (!ItemsEqual(slots[index + offset].Item, items[offset]))
                        throw new InvalidOperationException($"Collection {action} items do not match the current item slots.");
            }

            private static bool ItemsEqual(object left, object right) => ReferenceEquals(left, right) || Equals(left, right);

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                if (_observableSource != null) _observableSource.CollectionChanged -= OnCollectionChanged;
            }
        }
    }
}