// Copyright (c) 2026 Igor Hipolito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Forma
{
    public enum DataGridSortDirection { Ascending, Descending }
    public enum DataGridFilterMode { IndependentRows, IncludeAncestorsOfMatches }

    public sealed class DataGridSortDescription<T>
    {
        private readonly Comparison<T> _comparison;

        public DataGridSortDescription(Comparison<T> comparison, DataGridSortDirection direction = DataGridSortDirection.Ascending)
        {
            _comparison = comparison ?? throw new ArgumentNullException(nameof(comparison));
            if (!Enum.IsDefined(typeof(DataGridSortDirection), direction)) throw new ArgumentOutOfRangeException(nameof(direction));
            Direction = direction;
        }

        public DataGridSortDirection Direction { get; }

        public static DataGridSortDescription<T> Create<TKey>(
            Func<T, TKey> keySelector,
            DataGridSortDirection direction = DataGridSortDirection.Ascending,
            IComparer<TKey> comparer = null)
        {
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            comparer ??= Comparer<TKey>.Default;
            return new DataGridSortDescription<T>(
                (left, right) => comparer.Compare(keySelector(left), keySelector(right)),
                direction);
        }

        internal int Compare(T left, T right)
        {
            var result = _comparison(left, right);
            return Direction == DataGridSortDirection.Descending ? -result : result;
        }
    }

    public readonly struct IndexPath : IEquatable<IndexPath>
    {
        private readonly int[] _components;

        public IndexPath(params int[] components)
        {
            if (components == null) throw new ArgumentNullException(nameof(components));
            if (components.Length == 0) throw new ArgumentException("An index path requires at least one component.", nameof(components));
            _components = components.ToArray();
        }

        public int Count => _components?.Length ?? 0;
        public int this[int index] => _components[index];
        public IReadOnlyList<int> Components => _components ?? Array.Empty<int>();

        public IndexPath Append(int component)
        {
            var components = new int[Count + 1];
            if (Count != 0) Array.Copy(_components, components, Count);
            components[^1] = component;
            return new IndexPath(components);
        }

        public bool Equals(IndexPath other)
        {
            if (Count != other.Count) return false;
            for (var index = 0; index < Count; index++)
                if (this[index] != other[index]) return false;
            return true;
        }

        public override bool Equals(object obj) => obj is IndexPath other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            for (var index = 0; index < Count; index++) hash.Add(this[index]);
            return hash.ToHashCode();
        }

        public override string ToString() => string.Join("/", Components);
        public static bool operator ==(IndexPath left, IndexPath right) => left.Equals(right);
        public static bool operator !=(IndexPath left, IndexPath right) => !left.Equals(right);
    }

    public sealed class DataGridExpansionChangingEventArgs<T> : EventArgs
    {
        internal DataGridExpansionChangingEventArgs(IndexPath path, T item)
        {
            Path = path;
            Item = item;
        }

        public IndexPath Path { get; }
        public T Item { get; }
        public bool Cancel { get; set; }
    }

    public sealed class DataGridExpansionChangedEventArgs<T> : EventArgs
    {
        internal DataGridExpansionChangedEventArgs(IndexPath path, T item)
        {
            Path = path;
            Item = item;
        }

        public IndexPath Path { get; }
        public T Item { get; }
    }

    public interface IDataGridSource
    {
        IndexPath GetPath(int visibleIndex);
        int IndexOfPath(IndexPath path);
        bool IsExpanded(IndexPath path);
        bool HasChildren(IndexPath path);
        bool Expand(IndexPath path);
        bool Collapse(IndexPath path);
        bool Toggle(IndexPath path);
    }

    public sealed class DataGridSource<T> : IList, IReadOnlyList<T>, INotifyCollectionChanged, IDataGridSource, IDisposable
    {
        internal sealed class PreparedSort
        {
            private DataGridSource<T> _owner;
            internal readonly int Version;
            internal readonly List<Node> Target;
            internal readonly Dictionary<Node, int> VisibleCounts;
            internal readonly DataGridSortDescription<T>[] Descriptions;

            internal PreparedSort(
                DataGridSource<T> owner,
                int version,
                List<Node> target,
                Dictionary<Node, int> visibleCounts,
                DataGridSortDescription<T>[] descriptions)
            {
                _owner = owner;
                Version = version;
                Target = target;
                VisibleCounts = visibleCounts;
                Descriptions = descriptions;
            }

            internal bool TryApply()
            {
                var owner = _owner;
                _owner = null;
                return owner?.ApplyPreparedSort(this) == true;
            }
        }

        private sealed class ProjectionSnapshot
        {
            public List<ProjectionSnapshotNode> Nodes { get; } = new List<ProjectionSnapshotNode>();
        }

        private sealed class ProjectionSnapshotNode
        {
            public ProjectionSnapshotNode(Node node, int sourceIndex, ProjectionSnapshot children)
            {
                Node = node;
                SourceIndex = sourceIndex;
                Children = children;
            }

            public Node Node { get; }
            public int SourceIndex { get; }
            public ProjectionSnapshot Children { get; }
        }

        internal sealed class Branch
        {
            public Branch(Node parent, IEnumerable<T> source)
            {
                Parent = parent;
                Source = source ?? Array.Empty<T>();
            }

            public Node Parent { get; }
            public IEnumerable<T> Source { get; }
            public List<Node> Nodes { get; } = new List<Node>();
            public NotifyCollectionChangedEventHandler Handler { get; set; }
            public int NextOccurrenceId { get; set; }
        }

        internal sealed class Node
        {
            public Node(T item, IndexPath path, Node parent)
            {
                Item = item;
                Path = path;
                Parent = parent;
            }

            public T Item { get; }
            public IndexPath Path { get; }
            public Node Parent { get; }
            public Branch Children { get; set; }
            public bool Expanded { get; set; }
            public int VisibleSubtreeCount { get; set; } = 1;
        }

        private readonly Func<T, IEnumerable<T>> _children;
        private readonly Func<T, bool> _hasChildren;
        private readonly Func<T, bool> _getExpanded;
        private readonly Action<T, bool> _setExpanded;
        private readonly Dictionary<IndexPath, Node> _nodesByPath = new Dictionary<IndexPath, Node>();
        private readonly Branch _roots;
        private Predicate<T> _filter;
        private DataGridFilterMode _filterMode;
        private List<Node> _visibleNodes = new List<Node>();
        private int _projectionVersion;
        private bool _disposed;

        public DataGridSource(
            IEnumerable<T> roots,
            Func<T, IEnumerable<T>> children,
            Func<T, bool> hasChildren = null,
            Func<T, bool> getExpanded = null,
            Action<T, bool> setExpanded = null)
        {
            _children = children ?? throw new ArgumentNullException(nameof(children));
            _hasChildren = hasChildren;
            _getExpanded = getExpanded;
            _setExpanded = setExpanded;
            SortDescriptions = new ObservableCollection<DataGridSortDescription<T>>();
            SortDescriptions.CollectionChanged += SortDescriptionsChanged;
            _roots = new Branch(null, roots ?? throw new ArgumentNullException(nameof(roots)));
            PopulateBranch(_roots);
            Subscribe(_roots);
            _visibleNodes = Flatten();
        }

        public int Count => _visibleNodes.Count;
        public T this[int index] => _visibleNodes[index].Item;
        object IList.this[int index] { get => this[index]; set => throw new NotSupportedException(); }
        public bool IsReadOnly => true;
        public bool IsFixedSize => true;
        public bool IsSynchronized => false;
        public object SyncRoot => this;
        public ObservableCollection<DataGridSortDescription<T>> SortDescriptions { get; }
        public long ProjectionVisitCount { get; private set; }
        public Predicate<T> Filter { get => _filter; set => _filter = value; }
        public DataGridFilterMode FilterMode
        {
            get => _filterMode;
            set
            {
                if (!Enum.IsDefined(typeof(DataGridFilterMode), value)) throw new ArgumentOutOfRangeException(nameof(value));
                _filterMode = value;
            }
        }

        public event NotifyCollectionChangedEventHandler CollectionChanged;
        public event EventHandler<DataGridExpansionChangingEventArgs<T>> Expanding;
        public event EventHandler<DataGridExpansionChangingEventArgs<T>> Collapsing;
        public event EventHandler<DataGridExpansionChangedEventArgs<T>> Expanded;
        public event EventHandler<DataGridExpansionChangedEventArgs<T>> Collapsed;

        public IndexPath GetPath(int visibleIndex)
        {
            if (visibleIndex < 0 || visibleIndex >= Count) throw new ArgumentOutOfRangeException(nameof(visibleIndex));
            return _visibleNodes[visibleIndex].Path;
        }

        public int IndexOfPath(IndexPath path)
        {
            for (var index = 0; index < _visibleNodes.Count; index++)
                if (_visibleNodes[index].Path == path) return index;
            return -1;
        }

        public bool IsExpanded(IndexPath path) => GetNode(path).Expanded;

        public bool HasChildren(IndexPath path)
        {
            var node = GetNode(path);
            if (_hasChildren != null) return _hasChildren(node.Item);
            EnsureChildren(node);
            return node.Children.Nodes.Count != 0;
        }

        public bool Expand(IndexPath path)
        {
            ThrowIfDisposed();
            var node = GetNode(path);
            if (node.Expanded || !HasChildren(path)) return false;
            var args = new DataGridExpansionChangingEventArgs<T>(path, node.Item);
            Expanding?.Invoke(this, args);
            if (args.Cancel) return false;
            _projectionVersion++;
            var local = CanApplyLocalProjection;
            var previous = local ? null : _visibleNodes.ToArray();
            EnsureChildren(node);
            var oldCount = node.VisibleSubtreeCount;
            node.Expanded = true;
            _setExpanded?.Invoke(node.Item, true);
            if (local) ExpandVisibleNode(node, oldCount);
            else PublishVisibleChange(previous);
            Expanded?.Invoke(this, new DataGridExpansionChangedEventArgs<T>(path, node.Item));
            return true;
        }

        public bool Collapse(IndexPath path)
        {
            ThrowIfDisposed();
            var node = GetNode(path);
            if (!node.Expanded) return false;
            var args = new DataGridExpansionChangingEventArgs<T>(path, node.Item);
            Collapsing?.Invoke(this, args);
            if (args.Cancel) return false;
            _projectionVersion++;
            var local = CanApplyLocalProjection;
            var previous = local ? null : _visibleNodes.ToArray();
            var oldCount = node.VisibleSubtreeCount;
            node.Expanded = false;
            _setExpanded?.Invoke(node.Item, false);
            if (local) CollapseVisibleNode(node, oldCount);
            else PublishVisibleChange(previous);
            Collapsed?.Invoke(this, new DataGridExpansionChangedEventArgs<T>(path, node.Item));
            return true;
        }

        public bool Toggle(IndexPath path) => IsExpanded(path) ? Collapse(path) : Expand(path);

        public int ExpandAll(Func<T, bool> predicate = null)
        {
            ThrowIfDisposed();
            var expanded = 0;
            foreach (var node in _roots.Nodes.ToArray()) expanded += ExpandRecursive(node, predicate);
            return expanded;
        }

        public int CollapseAll(Func<T, bool> predicate = null)
        {
            ThrowIfDisposed();
            var collapsed = 0;
            foreach (var node in _nodesByPath.Values.OrderByDescending(node => node.Path.Count).ToArray())
                if (node.Expanded && (predicate == null || predicate(node.Item)) && Collapse(node.Path)) collapsed++;
            return collapsed;
        }

        public void RefreshSort()
        {
            ThrowIfDisposed();
            _projectionVersion++;
            PublishVisibleChange(_visibleNodes.ToArray());
        }

        internal void ReplaceSortDescriptions(IEnumerable<DataGridSortDescription<T>> descriptions)
        {
            ThrowIfDisposed();
            if (descriptions == null) throw new ArgumentNullException(nameof(descriptions));
            SortDescriptions.CollectionChanged -= SortDescriptionsChanged;
            try
            {
                SortDescriptions.Clear();
                foreach (var description in descriptions) SortDescriptions.Add(description);
            }
            finally { SortDescriptions.CollectionChanged += SortDescriptionsChanged; }
            RefreshSort();
        }

        internal Task<PreparedSort> PrepareSortAsync(
            IEnumerable<DataGridSortDescription<T>> descriptions,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (descriptions == null) throw new ArgumentNullException(nameof(descriptions));
            var descriptionSnapshot = descriptions.ToArray();
            var version = _projectionVersion;
            var projectionSnapshot = CaptureProjection(_roots);
            var targetCapacity = _visibleNodes.Count;
            return Task.Run(() =>
            {
                var target = new List<Node>(targetCapacity);
                var visibleCounts = new Dictionary<Node, int>();
                FlattenProjection(projectionSnapshot, descriptionSnapshot, target, visibleCounts, cancellationToken);
                return new PreparedSort(this, version, target, visibleCounts, descriptionSnapshot);
            }, cancellationToken);
        }

        public void RefreshFilter()
        {
            ThrowIfDisposed();
            _projectionVersion++;
            PublishVisibleChange(_visibleNodes.ToArray());
        }

        public IEnumerator<T> GetEnumerator()
        {
            foreach (var node in _visibleNodes) yield return node.Item;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public bool Contains(object value) => IndexOf(value) >= 0;
        public int IndexOf(object value)
        {
            for (var index = 0; index < Count; index++) if (Equals(this[index], value)) return index;
            return -1;
        }
        public void CopyTo(Array array, int index)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));
            for (var itemIndex = 0; itemIndex < Count; itemIndex++) array.SetValue(this[itemIndex], index + itemIndex);
        }
        public int Add(object value) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public void Insert(int index, object value) => throw new NotSupportedException();
        public void Remove(object value) => throw new NotSupportedException();
        public void RemoveAt(int index) => throw new NotSupportedException();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            SortDescriptions.CollectionChanged -= SortDescriptionsChanged;
            DisposeBranch(_roots);
            _nodesByPath.Clear();
            _visibleNodes.Clear();
        }

        private int ExpandRecursive(Node node, Func<T, bool> predicate)
        {
            var expanded = 0;
            if ((predicate == null || predicate(node.Item)) && !node.Expanded && HasChildren(node.Path) && Expand(node.Path)) expanded++;
            if (!node.Expanded) return expanded;
            EnsureChildren(node);
            foreach (var child in node.Children.Nodes.ToArray()) expanded += ExpandRecursive(child, predicate);
            return expanded;
        }

        private Node GetNode(IndexPath path)
        {
            ThrowIfDisposed();
            return _nodesByPath.TryGetValue(path, out var node)
                ? node
                : throw new ArgumentOutOfRangeException(nameof(path), $"Data-grid path '{path}' does not exist.");
        }

        private void EnsureChildren(Node node)
        {
            if (node.Children != null) return;
            var source = _children(node.Item) ?? Array.Empty<T>();
            var branch = new Branch(node, source);
            node.Children = branch;
            PopulateBranch(branch);
            Subscribe(branch);
        }

        private void PopulateBranch(Branch branch)
        {
            foreach (var item in branch.Source) branch.Nodes.Add(CreateNode(branch, item));
        }

        private Node CreateNode(Branch branch, T item)
        {
            for (var ancestor = branch.Parent; ancestor != null; ancestor = ancestor.Parent)
                if (ReferenceEquals(ancestor.Item, item))
                    throw new InvalidOperationException($"DataGridSource detected a hierarchy cycle below '{ancestor.Path}'.");
            var occurrence = branch.NextOccurrenceId++;
            var path = branch.Parent == null ? new IndexPath(occurrence) : branch.Parent.Path.Append(occurrence);
            var node = new Node(item, path, branch.Parent)
            {
                Expanded = _getExpanded?.Invoke(item) == true,
            };
            _nodesByPath.Add(path, node);
            return node;
        }

        private void Subscribe(Branch branch)
        {
            if (branch.Source is not INotifyCollectionChanged observable) return;
            branch.Handler = (_, args) => ApplyCollectionChange(branch, args);
            observable.CollectionChanged += branch.Handler;
        }

        private void ApplyCollectionChange(Branch branch, NotifyCollectionChangedEventArgs args)
        {
            ThrowIfDisposed();
            _projectionVersion++;
            if (CanApplyLocalProjection && args.Action != NotifyCollectionChangedAction.Reset)
            {
                ApplyLocalCollectionChange(branch, args);
                return;
            }
            var previous = _visibleNodes.ToArray();
            switch (args.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    InsertNodes(branch, args.NewStartingIndex, args.NewItems);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    RemoveNodes(branch, args.OldStartingIndex, args.OldItems.Count);
                    break;
                case NotifyCollectionChangedAction.Move:
                {
                    var moved = branch.Nodes.GetRange(args.OldStartingIndex, args.OldItems.Count);
                    branch.Nodes.RemoveRange(args.OldStartingIndex, args.OldItems.Count);
                    branch.Nodes.InsertRange(args.NewStartingIndex, moved);
                    break;
                }
                case NotifyCollectionChangedAction.Replace:
                    RemoveNodes(branch, args.OldStartingIndex, args.OldItems.Count);
                    InsertNodes(branch, args.NewStartingIndex, args.NewItems);
                    break;
                case NotifyCollectionChangedAction.Reset:
                    foreach (var node in branch.Nodes) DisposeNode(node);
                    branch.Nodes.Clear();
                    PopulateBranch(branch);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(args));
            }
            PublishVisibleChange(previous);
        }

        private void ApplyLocalCollectionChange(Branch branch, NotifyCollectionChangedEventArgs args)
        {
            var branchVisible = IsBranchVisible(branch);
            switch (args.Action)
            {
                case NotifyCollectionChangedAction.Add:
                {
                    var sourceIndex = args.NewStartingIndex < 0 ? branch.Nodes.Count : args.NewStartingIndex;
                    InsertNodes(branch, sourceIndex, args.NewItems);
                    if (!branchVisible) return;
                    var added = BuildVisibleNodes(branch.Nodes.GetRange(sourceIndex, args.NewItems.Count));
                    InsertVisibleRange(branch, sourceIndex, added);
                    break;
                }
                case NotifyCollectionChangedAction.Remove:
                    RemoveLocalNodes(branch, args.OldStartingIndex, args.OldItems.Count, branchVisible);
                    break;
                case NotifyCollectionChangedAction.Move:
                    MoveLocalNodes(branch, args.OldStartingIndex, args.NewStartingIndex, args.OldItems.Count, branchVisible);
                    break;
                case NotifyCollectionChangedAction.Replace:
                    RemoveLocalNodes(branch, args.OldStartingIndex, args.OldItems.Count, branchVisible);
                    var replacementIndex = args.NewStartingIndex < 0 ? args.OldStartingIndex : args.NewStartingIndex;
                    InsertNodes(branch, replacementIndex, args.NewItems);
                    if (branchVisible)
                    {
                        var added = BuildVisibleNodes(branch.Nodes.GetRange(replacementIndex, args.NewItems.Count));
                        InsertVisibleRange(branch, replacementIndex, added);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(args));
            }
        }

        private void RemoveLocalNodes(Branch branch, int sourceIndex, int count, bool branchVisible)
        {
            if (branchVisible)
            {
                var visibleIndex = GetVisibleInsertionIndex(branch, sourceIndex);
                var visibleCount = 0;
                for (var index = sourceIndex; index < sourceIndex + count; index++)
                    visibleCount += branch.Nodes[index].VisibleSubtreeCount;
                RemoveVisibleRange(branch, visibleIndex, visibleCount);
            }
            RemoveNodes(branch, sourceIndex, count);
        }

        private void MoveLocalNodes(Branch branch, int oldIndex, int newIndex, int count, bool branchVisible)
        {
            var moved = branch.Nodes.GetRange(oldIndex, count);
            var oldVisibleIndex = branchVisible ? GetVisibleInsertionIndex(branch, oldIndex) : -1;
            var visibleCount = branchVisible ? moved.Sum(node => node.VisibleSubtreeCount) : 0;
            List<Node> movedVisible = null;
            if (branchVisible)
            {
                movedVisible = _visibleNodes.GetRange(oldVisibleIndex, visibleCount);
                _visibleNodes.RemoveRange(oldVisibleIndex, visibleCount);
            }
            branch.Nodes.RemoveRange(oldIndex, count);
            branch.Nodes.InsertRange(newIndex, moved);
            if (!branchVisible) return;
            var newVisibleIndex = GetVisibleInsertionIndex(branch, newIndex);
            _visibleNodes.InsertRange(newVisibleIndex, movedVisible);
            if (oldVisibleIndex == newVisibleIndex) return;
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Move,
                movedVisible.Select(node => node.Item).ToList(),
                newVisibleIndex,
                oldVisibleIndex));
        }

        private bool IsBranchVisible(Branch branch) => branch.Parent == null ||
            (branch.Parent.Expanded && _visibleNodes.Contains(branch.Parent));

        private List<Node> BuildVisibleNodes(IEnumerable<Node> nodes)
        {
            var result = new List<Node>();
            foreach (var node in nodes) BuildVisibleSubtree(node, result);
            return result;
        }

        private int GetVisibleInsertionIndex(Branch branch, int sourceIndex)
        {
            var index = branch.Parent == null ? 0 : _visibleNodes.IndexOf(branch.Parent) + 1;
            for (var sibling = 0; sibling < sourceIndex; sibling++)
                index += branch.Nodes[sibling].VisibleSubtreeCount;
            return index;
        }

        private void InsertVisibleRange(Branch branch, int sourceIndex, List<Node> added)
        {
            if (added.Count == 0) return;
            var visibleIndex = GetVisibleInsertionIndex(branch, sourceIndex);
            _visibleNodes.InsertRange(visibleIndex, added);
            AdjustBranchOwnerVisibleCount(branch, added.Count);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Add,
                added.Select(node => node.Item).ToList(),
                visibleIndex));
        }

        private void RemoveVisibleRange(Branch branch, int visibleIndex, int count)
        {
            if (count == 0) return;
            var removed = _visibleNodes.GetRange(visibleIndex, count);
            _visibleNodes.RemoveRange(visibleIndex, count);
            AdjustBranchOwnerVisibleCount(branch, -count);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Remove,
                removed.Select(node => node.Item).ToList(),
                visibleIndex));
        }

        private static void AdjustBranchOwnerVisibleCount(Branch branch, int delta)
        {
            if (branch.Parent == null) return;
            branch.Parent.VisibleSubtreeCount += delta;
            AdjustAncestorVisibleCounts(branch.Parent.Parent, delta);
        }

        private void InsertNodes(Branch branch, int index, IList items)
        {
            if (index < 0) index = branch.Nodes.Count;
            var nodes = new List<Node>(items.Count);
            foreach (T item in items) nodes.Add(CreateNode(branch, item));
            branch.Nodes.InsertRange(index, nodes);
        }

        private void RemoveNodes(Branch branch, int index, int count)
        {
            var removed = branch.Nodes.GetRange(index, count);
            branch.Nodes.RemoveRange(index, count);
            foreach (var node in removed) DisposeNode(node);
        }

        private List<Node> Flatten()
        {
            var result = new List<Node>();
            foreach (var node in Project(_roots)) Flatten(node, result);
            return result;
        }

        private void Flatten(Node node, List<Node> result)
        {
            ProjectionVisitCount++;
            var start = result.Count;
            result.Add(node);
            if (node.Expanded)
            {
                EnsureChildren(node);
                foreach (var child in Project(node.Children)) Flatten(child, result);
            }
            node.VisibleSubtreeCount = result.Count - start;
        }

        private bool CanApplyLocalProjection => _filter == null && SortDescriptions.Count == 0;

        private void ExpandVisibleNode(Node node, int oldCount)
        {
            var descendants = new List<Node>();
            foreach (var child in node.Children.Nodes) BuildVisibleSubtree(child, descendants);
            node.VisibleSubtreeCount = 1 + descendants.Count;
            var visibleIndex = _visibleNodes.IndexOf(node);
            if (visibleIndex < 0) return;
            AdjustAncestorVisibleCounts(node.Parent, node.VisibleSubtreeCount - oldCount);
            if (descendants.Count == 0) return;
            _visibleNodes.InsertRange(visibleIndex + 1, descendants);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Add,
                descendants.Select(descendant => descendant.Item).ToList(),
                visibleIndex + 1));
        }

        private void CollapseVisibleNode(Node node, int oldCount)
        {
            node.VisibleSubtreeCount = 1;
            var visibleIndex = _visibleNodes.IndexOf(node);
            if (visibleIndex < 0) return;
            AdjustAncestorVisibleCounts(node.Parent, 1 - oldCount);
            var removeCount = Math.Max(0, oldCount - 1);
            if (removeCount == 0) return;
            var removed = _visibleNodes.GetRange(visibleIndex + 1, removeCount);
            _visibleNodes.RemoveRange(visibleIndex + 1, removeCount);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Remove,
                removed.Select(descendant => descendant.Item).ToList(),
                visibleIndex + 1));
        }

        private void BuildVisibleSubtree(Node node, List<Node> result)
        {
            ProjectionVisitCount++;
            var start = result.Count;
            result.Add(node);
            if (node.Expanded)
            {
                EnsureChildren(node);
                foreach (var child in node.Children.Nodes) BuildVisibleSubtree(child, result);
            }
            node.VisibleSubtreeCount = result.Count - start;
        }

        private static void AdjustAncestorVisibleCounts(Node node, int delta)
        {
            while (node != null && node.Expanded)
            {
                node.VisibleSubtreeCount += delta;
                node = node.Parent;
            }
        }

        private IReadOnlyList<Node> Project(Branch branch)
        {
            IEnumerable<Node> nodes = branch.Nodes;
            if (_filter != null)
                nodes = FilterMode == DataGridFilterMode.IncludeAncestorsOfMatches
                    ? nodes.Where(MatchesFilterOrDescendant)
                    : nodes.Where(node => _filter(node.Item));
            var projected = nodes.Select((node, index) => (Node: node, Index: index)).ToList();
            if (SortDescriptions.Count != 0)
            {
                projected.Sort((left, right) =>
                {
                    foreach (var description in SortDescriptions)
                    {
                        var result = description.Compare(left.Node.Item, right.Node.Item);
                        if (result != 0) return result;
                    }
                    return left.Index.CompareTo(right.Index);
                });
            }
            return projected.Select(entry => entry.Node).ToArray();
        }

        private ProjectionSnapshot CaptureProjection(Branch branch)
        {
            IEnumerable<Node> nodes = branch.Nodes;
            if (_filter != null)
                nodes = FilterMode == DataGridFilterMode.IncludeAncestorsOfMatches
                    ? nodes.Where(MatchesFilterOrDescendant)
                    : nodes.Where(node => _filter(node.Item));
            var snapshot = new ProjectionSnapshot();
            foreach (var entry in nodes.Select((node, index) => (Node: node, Index: index)))
            {
                ProjectionSnapshot children = null;
                if (entry.Node.Expanded)
                {
                    EnsureChildren(entry.Node);
                    children = CaptureProjection(entry.Node.Children);
                }
                snapshot.Nodes.Add(new ProjectionSnapshotNode(entry.Node, entry.Index, children));
            }
            return snapshot;
        }

        private static void FlattenProjection(
            ProjectionSnapshot snapshot,
            IReadOnlyList<DataGridSortDescription<T>> descriptions,
            List<Node> target,
            Dictionary<Node, int> visibleCounts,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var nodes = snapshot.Nodes.ToArray();
            if (descriptions.Count != 0)
            {
                Array.Sort(nodes, (left, right) =>
                {
                    foreach (var description in descriptions)
                    {
                        var result = description.Compare(left.Node.Item, right.Node.Item);
                        if (result != 0) return result;
                    }
                    return left.SourceIndex.CompareTo(right.SourceIndex);
                });
            }
            foreach (var entry in nodes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var start = target.Count;
                target.Add(entry.Node);
                if (entry.Children != null)
                    FlattenProjection(entry.Children, descriptions, target, visibleCounts, cancellationToken);
                visibleCounts[entry.Node] = target.Count - start;
            }
        }

        private bool ApplyPreparedSort(PreparedSort prepared)
        {
            ThrowIfDisposed();
            if (prepared.Version != _projectionVersion) return false;
            SortDescriptions.CollectionChanged -= SortDescriptionsChanged;
            try
            {
                SortDescriptions.Clear();
                foreach (var description in prepared.Descriptions) SortDescriptions.Add(description);
            }
            finally { SortDescriptions.CollectionChanged += SortDescriptionsChanged; }
            foreach (var pair in prepared.VisibleCounts) pair.Key.VisibleSubtreeCount = pair.Value;
            var changed = !SameNodes(_visibleNodes, prepared.Target);
            _visibleNodes = prepared.Target;
            _projectionVersion++;
            if (changed) CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            return true;
        }

        private bool MatchesFilterOrDescendant(Node node)
        {
            if (_filter(node.Item)) return true;
            EnsureChildren(node);
            foreach (var child in node.Children.Nodes)
                if (MatchesFilterOrDescendant(child)) return true;
            return false;
        }

        private void PublishVisibleChange(IReadOnlyList<Node> previous)
        {
            var target = Flatten();
            if (SameNodes(previous, target)) return;
            _visibleNodes = target;
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        private void SortDescriptionsChanged(object sender, NotifyCollectionChangedEventArgs args) => RefreshSort();

        private static bool SameNodes(IReadOnlyList<Node> left, IReadOnlyList<Node> right)
        {
            if (left.Count != right.Count) return false;
            for (var index = 0; index < left.Count; index++)
                if (!ReferenceEquals(left[index], right[index])) return false;
            return true;
        }

        private void DisposeNode(Node node)
        {
            if (node.Children != null) DisposeBranch(node.Children);
            _nodesByPath.Remove(node.Path);
        }

        private void DisposeBranch(Branch branch)
        {
            if (branch.Handler != null && branch.Source is INotifyCollectionChanged observable)
                observable.CollectionChanged -= branch.Handler;
            foreach (var node in branch.Nodes) DisposeNode(node);
            branch.Nodes.Clear();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DataGridSource<T>));
        }
    }
}