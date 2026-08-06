// Copyright (c) 2026 Igor Hipolito Vieira
// SPDX-License-Identifier: MIT

using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Forma.Tests
{
    public sealed class DataGridTest
    {
        [Test]
        public void DataGridHeaderIgnoresSortingWhenColumnHasNoSortBinding()
        {
            var grid = new DataGrid { ItemsSource = new[] { new RowModel("First", true) }, Size = new Vector2(240, 100) };
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Name",
                Binding = DataGridBinding<string>.Create<RowModel>(row => row.Name),
            });
            using var context = new UIContext { ViewportSize = grid.Size };
            context.Add(grid);
            context.Layout();
            var header = grid.GetColumnHeader(0);

            Assert.Multiple(() =>
            {
                Assert.That(() => header.PointerReleased(header.Bounds.Center, true), Throws.Nothing);
                Assert.That(grid.SortDescriptions, Is.Empty);
                Assert.That(() => grid.ActivateColumnHeader(0), Throws.InvalidOperationException);
            });
        }

        [Test]
        public void DataGrid_GeneratesExplicitTypedHeadersRowsAndCells()
        {
            var first = new RowModel("First", true);
            var second = new RowModel("Second", false);
            var nameColumn = new DataGridTextColumn
            {
                Header = "Name",
                Binding = DataGridBinding<string>.Create<RowModel>(row => row.Name, nameof(RowModel.Name)),
                Width = GridTrackSize.Star(2),
                MinimumWidth = 40,
            };
            var enabledColumn = new DataGridCheckBoxColumn
            {
                Header = "Enabled",
                Binding = DataGridBinding<bool>.Create<RowModel>(row => row.Enabled, nameof(RowModel.Enabled), (row, value) => row.Enabled = value),
                Width = GridTrackSize.Pixels(80),
                MinimumWidth = 60,
                MaximumWidth = 100,
            };
            var grid = new DataGrid
            {
                ItemsSource = new[] { first, second },
                Size = new Vector2(240, 100),
            };
            grid.Columns.Add(nameColumn);
            grid.Columns.Add(enabledColumn);
            using var context = new UIContext();
            context.Add(grid);
            context.Layout();

            Assert.Multiple(() =>
            {
                Assert.That(grid.GetColumnHeader(0).Content, Is.EqualTo("Name"));
                Assert.That(grid.GetColumnHeader(1).Content, Is.EqualTo("Enabled"));
                Assert.That(grid.GetRealizedContainer(0), Is.TypeOf<DataGridRow>());
                Assert.That(grid.GetCell(0, 0).Content, Is.TypeOf<TextBlock>());
                Assert.That(((TextBlock)grid.GetCell(0, 0).Content).Text, Is.EqualTo("First"));
                Assert.That(((CheckBox)grid.GetCell(0, 1).Content).Checked, Is.True);
            });

            first.Name = "Updated";
            Assert.That(((TextBlock)grid.GetCell(0, 0).Content).Text, Is.EqualTo("Updated"));
            ((CheckBox)grid.GetCell(0, 1).Content).Checked = false;
            Assert.That(first.Enabled, Is.False);

            grid.ResizeColumn(1, 200);
            Assert.Multiple(() =>
            {
                Assert.That(enabledColumn.Width.Unit, Is.EqualTo(GridTrackUnit.Pixel));
                Assert.That(enabledColumn.Width.Value, Is.EqualTo(100));
                Assert.That(((DataGridRow)grid.GetRealizedContainer(1)).Cells, Has.Count.EqualTo(2));
            });
            grid.Dispose();
        }

        [Test]
        public void DataGrid_DefaultTemplateProvidesProfessionalDensityAndCellGutters()
        {
            var grid = new DataGrid
            {
                ItemsSource = new[] { new RowModel("First", true), new RowModel("Second", false) },
                Size = new Vector2(320, 140),
            };
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Contributor",
                Binding = DataGridBinding<string>.Create<RowModel>(row => row.Name),
            });
            grid.Columns.Add(new DataGridCheckBoxColumn
            {
                Header = "Enabled",
                Binding = DataGridBinding<bool>.Create<RowModel>(row => row.Enabled),
                Width = GridTrackSize.Pixels(100),
            });
            using var context = new UIContext { ViewportSize = grid.Size };
            context.Add(grid);
            context.Layout();

            var header = grid.GetColumnHeader(0);
            var row = (DataGridRow)grid.GetRealizedContainer(0);
            var cell = grid.GetCell(0, 0);
            var cellContent = (ContentPresenter)cell.GetTemplateChild(ContentControl.ContentPresenterPartName);
            var headers = (GridPanel)grid.GetTemplateChild(DataGrid.ColumnHeadersPartName);

            Assert.Multiple(() =>
            {
                Assert.That(grid.AlternatingRowBackground, Is.True);
                Assert.That(grid.ShowHorizontalGridLines, Is.True);
                Assert.That(grid.ShowVerticalGridLines, Is.True);
                Assert.That(header.Size.Y, Is.GreaterThanOrEqualTo(DataGrid.DefaultColumnHeaderHeight));
                Assert.That(row.Size.Y, Is.GreaterThanOrEqualTo(DataGrid.DefaultEstimatedRowExtent));
                Assert.That(cellContent.VisualBounds.Left, Is.GreaterThanOrEqualTo(cell.VisualBounds.Left + 10));
                Assert.That(cellContent.VisualBounds.Right, Is.LessThanOrEqualTo(cell.VisualBounds.Right - 10));
                Assert.That(headers.Size.X, Is.EqualTo(grid.Size.X));
                Assert.That(row.Cells[^1].VisualBounds.Right, Is.EqualTo(grid.VisualBounds.Right));
            });
        }

        [Test]
        public void DataGrid_LargeProjectionShowsAFunctionalVerticalScrollBar()
        {
            var rows = Enumerable.Range(1, 5_000)
                .Select(index => new RowModel($"Row {index}", index % 2 == 0))
                .ToArray();
            var grid = new DataGrid
            {
                ItemsSource = rows,
                Size = new Vector2(320, 160),
            };
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Name",
                Binding = DataGridBinding<string>.Create<RowModel>(row => row.Name),
            });
            using var context = new UIContext { ViewportSize = grid.Size };
            context.Add(grid);
            context.Layout();
            context.Layout();

            Assert.Multiple(() =>
            {
                Assert.That(grid.VerticalScrollBar.Visible, Is.True);
                Assert.That(grid.VerticalScrollBar.Size.X, Is.GreaterThan(0));
                Assert.That(grid.VerticalScrollBar.Size.Y, Is.GreaterThan(0));
                Assert.That(grid.VerticalScrollBar.Page, Is.GreaterThan(0));
                Assert.That(grid.VerticalScrollBar.MaxValue, Is.GreaterThan(grid.VerticalScrollBar.Page));
                Assert.That(grid.HorizontalScrollBar.Visible, Is.False);
            });

            grid.VerticalScrollBar.Value = 300;
            context.Layout();
            Assert.That(grid.ScrollOffset.Y, Is.EqualTo(300).Within(.001f));
        }

        [Test]
        public void DataGrid_LargeHeaderSortRunsOffThreadAndAppliesAtAFrameBoundary()
        {
            var rows = Enumerable.Range(1, 5_000)
                .Reverse()
                .Select(index => new SortRow(index.ToString("D5"), index))
                .ToArray();
            var column = new DataGridTextColumn
            {
                Header = "Name",
                Binding = DataGridBinding<string>.Create<SortRow>(row => row.Name),
                SortBinding = DataGridSortBinding.Create<SortRow, string>(row => row.Name),
            };
            var grid = new DataGrid
            {
                ItemsSource = rows,
                AsynchronousSortThreshold = 0,
                Size = new Vector2(320, 160),
            };
            grid.Columns.Add(column);
            using var context = new UIContext { ViewportSize = grid.Size };
            context.Add(grid);
            context.Layout();

            grid.ActivateColumnHeader(0);

            Assert.Multiple(() =>
            {
                Assert.That(grid.IsSorting, Is.True);
                Assert.That(grid.GetRealizedContainer(0).DataContext, Is.SameAs(rows[0]));
            });

            var completed = System.Threading.SpinWait.SpinUntil(() =>
            {
                context.Update(new GameTime(), new MouseState(), new KeyboardState());
                System.Threading.Thread.Yield();
                return !grid.IsSorting;
            }, TimeSpan.FromSeconds(5));
            context.Layout();

            Assert.Multiple(() =>
            {
                Assert.That(completed, Is.True, "The worker sort should complete and be applied by DataGrid.Process.");
                Assert.That(grid.GetRealizedContainer(0).DataContext, Is.SameAs(rows[^1]));
                Assert.That(grid.GetColumnHeader(0).SortDirection, Is.EqualTo(DataGridSortDirection.Ascending));
            });
        }

        [Test]
        public void DataGrid_OrdersAndRefreshesTemplateAndExpanderColumns()
        {
            var text = new DataGridTextColumn
            {
                Header = "Text",
                DisplayIndex = 2,
                Binding = DataGridBinding<string>.Create<RowModel>(row => row.Name),
            };
            var template = new DataGridTemplateColumn
            {
                Header = "Template",
                HeaderTemplate = DataTemplate.Create<string>((_, value) => new TextBlock { Text = $"H:{value}" }),
                DisplayIndex = 0,
                CellTemplate = DataTemplate.Create<RowModel>((_, row) => new TextBlock { Text = $"T:{row.Name}" }),
            };
            var expander = new DataGridExpanderColumn
            {
                Header = "Node",
                DisplayIndex = 1,
                Children = DataGridBinding<System.Collections.IEnumerable>.Create<RowModel>(_ => Array.Empty<RowModel>()),
                Column = new DataGridTextColumn
                {
                    Binding = DataGridBinding<string>.Create<RowModel>(row => row.Name),
                },
            };
            var grid = new DataGrid
            {
                Mode = DataGridMode.Hierarchical,
                ItemsSource = new[] { new RowModel("Root", true) },
                Size = new Vector2(240, 80),
            };
            grid.Columns.Add(text);
            grid.Columns.Add(template);
            grid.Columns.Add(expander);
            using var context = new UIContext();
            context.Add(grid);
            context.Layout();

            Assert.Multiple(() =>
            {
                Assert.That(grid.GetColumnHeader(0).Content, Is.EqualTo("Template"));
                Assert.That(((TextBlock)((ContentPresenter)grid.GetColumnHeader(0).GetTemplateChild(ContentControl.ContentPresenterPartName)).PresentedControl).Text, Is.EqualTo("H:Template"));
                Assert.That(grid.GetColumnHeader(1).Content, Is.EqualTo("Node"));
                Assert.That(grid.GetColumnHeader(2).Content, Is.EqualTo("Text"));
                Assert.That(grid.GetCell(0, 0).ContentTemplate, Is.SameAs(template.CellTemplate));
                Assert.That(grid.GetCell(0, 1).Column, Is.SameAs(expander));
                Assert.That(((TextBlock)((StackPanel)grid.GetCell(0, 1).Content).Children[1]).Text, Is.EqualTo("Root"));
            });

            expander.IsVisible = false;
            context.Layout();
            Assert.Multiple(() =>
            {
                Assert.That(grid.GetDisplayColumns(), Is.EqualTo(new DataGridColumn[] { template, text }));
                Assert.That(((DataGridRow)grid.GetRealizedContainer(0)).Cells, Has.Count.EqualTo(2));
                Assert.That(grid.GetColumnHeader(1).Content, Is.EqualTo("Text"));
            });

            grid.CanUserResizeColumns = false;
            var previousWidth = text.Width;
            grid.ResizeColumn(1, 120);
            Assert.That(text.Width, Is.EqualTo(previousWidth));
            grid.Dispose();
        }

        [Test]
        public void DataGridSource_TracksOccurrencePathsExpansionAndNestedDeltas()
        {
            var duplicate = new TreeNode("Duplicate");
            var root = new TreeNode("Root", duplicate, duplicate);
            var other = new TreeNode("Other");
            var roots = new ObservableCollection<TreeNode> { root, other };
            using var source = new DataGridSource<TreeNode>(
                roots,
                node => node.Children,
                node => node.Children.Count != 0,
                node => node.IsExpanded,
                (node, expanded) => node.IsExpanded = expanded);
            var rootPath = source.GetPath(0);
            var expanded = 0;
            var cancelExpansion = true;
            source.Expanded += (_, _) => expanded++;
            source.Expanding += (_, args) => args.Cancel = cancelExpansion;

            Assert.That(source.Expand(rootPath), Is.False);
            cancelExpansion = false;
            Assert.That(source.Expand(rootPath), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(source.Count, Is.EqualTo(4));
                Assert.That(source[1], Is.SameAs(duplicate));
                Assert.That(source[2], Is.SameAs(duplicate));
                Assert.That(source.GetPath(1), Is.Not.EqualTo(source.GetPath(2)));
                Assert.That(root.IsExpanded, Is.True);
                Assert.That(expanded, Is.EqualTo(1));
            });

            var child = new TreeNode("Child");
            root.Children.Insert(1, child);
            Assert.Multiple(() =>
            {
                Assert.That(source.Count, Is.EqualTo(5));
                Assert.That(source[2], Is.SameAs(child));
                Assert.That(source.GetPath(0), Is.EqualTo(rootPath));
            });

            roots.Move(0, 1);
            Assert.Multiple(() =>
            {
                Assert.That(source.IndexOfPath(rootPath), Is.EqualTo(1));
                Assert.That(source[1], Is.SameAs(root));
            });

            var childPath = source.GetPath(3);
            child.Children.Add(root);
            Assert.Throws<InvalidOperationException>(() => source.Expand(childPath));
            child.Children.Clear();
            var cancelCollapse = true;
            source.Collapsing += (_, args) => args.Cancel = cancelCollapse;
            Assert.That(source.Collapse(rootPath), Is.False);
            cancelCollapse = false;
            Assert.That(source.Collapse(rootPath), Is.True);
            Assert.That(source.Count, Is.EqualTo(2));
        }

        [Test]
        public void DataGridSource_RecursiveOperationsHonorPredicates()
        {
            var leaf = new TreeNode("Leaf");
            var child = new TreeNode("Child", leaf);
            var root = new TreeNode("Root", child);
            using var source = new DataGridSource<TreeNode>(
                new[] { root },
                node => node.Children,
                node => node.Children.Count != 0);

            Assert.That(source.ExpandAll(node => node.Name != "Leaf"), Is.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(source.Count, Is.EqualTo(3));
                Assert.That(source.IsExpanded(source.GetPath(0)), Is.True);
                Assert.That(source.IsExpanded(source.GetPath(1)), Is.True);
            });
            Assert.That(source.CollapseAll(node => node.Name == "Root"), Is.EqualTo(1));
            Assert.That(source.Count, Is.EqualTo(1));
        }

        [Test]
        public void DataGridSource_SortsAndFiltersWithoutReplacingOccurrencePaths()
        {
            var first = new SortRow("b", 1);
            var second = new SortRow("a", 2);
            var third = new SortRow("a", 1);
            using var source = new DataGridSource<SortRow>(
                new[] { first, second, third },
                _ => Array.Empty<SortRow>());
            var paths = new Dictionary<SortRow, IndexPath>
            {
                [first] = source.GetPath(0),
                [second] = source.GetPath(1),
                [third] = source.GetPath(2),
            };

            source.SortDescriptions.Add(DataGridSortDescription<SortRow>.Create(row => row.Name));
            source.SortDescriptions.Add(DataGridSortDescription<SortRow>.Create(row => row.Order));
            Assert.That(source.ToArray(), Is.EqualTo(new[] { third, second, first }));
            Assert.Multiple(() =>
            {
                Assert.That(source.GetPath(source.IndexOf(third)), Is.EqualTo(paths[third]));
                Assert.That(source.GetPath(source.IndexOf(second)), Is.EqualTo(paths[second]));
                Assert.That(source.GetPath(source.IndexOf(first)), Is.EqualTo(paths[first]));
            });

            source.Filter = row => row.Order == 1;
            Assert.That(source.Count, Is.EqualTo(3));
            source.RefreshFilter();
            Assert.That(source.ToArray(), Is.EqualTo(new[] { third, first }));
            source.Filter = null;
            source.RefreshFilter();
            Assert.That(source.ToArray(), Is.EqualTo(new[] { third, second, first }));
        }

        [Test]
        public void DataGridSource_FilterCanRetainAncestorsOfMatches()
        {
            var match = new TreeNode("match");
            var parent = new TreeNode("parent", match) { IsExpanded = true };
            using var source = new DataGridSource<TreeNode>(
                new[] { parent },
                node => node.Children,
                getExpanded: node => node.IsExpanded);
            source.Filter = node => node.Name == "match";
            source.FilterMode = DataGridFilterMode.IncludeAncestorsOfMatches;
            source.RefreshFilter();

            Assert.That(source.ToArray(), Is.EqualTo(new[] { parent, match }));
            source.FilterMode = DataGridFilterMode.IndependentRows;
            source.RefreshFilter();
            Assert.That(source, Is.Empty);
        }

        [Test]
        public void DataGrid_HierarchicalModeProjectsRowsAndExpanderBindings()
        {
            var child = new TreeNode("Child");
            var root = new TreeNode("Root", child);
            var roots = new ObservableCollection<TreeNode> { root };
            var expander = new DataGridExpanderColumn
            {
                Header = "Node",
                Children = DataGridBinding<System.Collections.IEnumerable>.Create<TreeNode>(node => node.Children),
                HasChildren = DataGridBinding<bool>.Create<TreeNode>(node => node.Children.Count != 0),
                IsExpanded = DataGridBinding<bool>.Create<TreeNode>(node => node.IsExpanded, write: (node, value) => node.IsExpanded = value),
                Column = new DataGridTextColumn
                {
                    Binding = DataGridBinding<string>.Create<TreeNode>(node => node.Name),
                },
            };
            var grid = new DataGrid
            {
                Mode = DataGridMode.Hierarchical,
                Size = new Vector2(200, 100),
            };
            grid.Columns.Add(expander);
            grid.ItemsSource = roots;
            using var context = new UIContext();
            context.Add(grid);
            context.Layout();
            var rootPath = grid.GetRowPath(0);

            Assert.That(grid.Expand(rootPath), Is.True);
            context.Layout();
            Assert.Multiple(() =>
            {
                Assert.That(root.IsExpanded, Is.True);
                Assert.That(grid.RealizedCount, Is.EqualTo(2));
                Assert.That(((DataGridRow)grid.GetRealizedContainer(0)).IndexPath, Is.EqualTo(rootPath));
                Assert.That(((DataGridRow)grid.GetRealizedContainer(0)).IsExpanded, Is.True);
                Assert.That(((DataGridRow)grid.GetRealizedContainer(0)).IsCollapsed, Is.False);
                Assert.That(((DataGridRow)grid.GetRealizedContainer(1)).IndexPath.Count, Is.EqualTo(2));
                Assert.That(((TextBlock)((StackPanel)grid.GetCell(1, 0).Content).Children[1]).Text, Is.EqualTo("Child"));
            });

            root.Children.Add(new TreeNode("Second"));
            context.Layout();
            Assert.That(grid.RealizedCount, Is.EqualTo(3));
            Assert.That(grid.Collapse(rootPath), Is.True);
            context.Layout();
            Assert.Multiple(() =>
            {
                Assert.That(grid.RealizedCount, Is.EqualTo(1));
                Assert.That(((DataGridRow)grid.GetRealizedContainer(0)).IsExpanded, Is.False);
                Assert.That(((DataGridRow)grid.GetRealizedContainer(0)).IsCollapsed, Is.True);
            });
            grid.Dispose();
        }

        [Test]
        public void DataGrid_HierarchicalInsertRefreshesShiftedRealizedRowPositions()
        {
            var originalChild = new TreeNode("Original child");
            var root = new TreeNode("Root", originalChild) { IsExpanded = true };
            var grid = new DataGrid
            {
                Mode = DataGridMode.Hierarchical,
                ItemsSource = new[] { root },
                Size = new Vector2(240, 120),
            };
            grid.Columns.Add(new DataGridExpanderColumn
            {
                Children = DataGridBinding<System.Collections.IEnumerable>.Create<TreeNode>(node => node.Children),
                HasChildren = DataGridBinding<bool>.Create<TreeNode>(node => node.Children.Count != 0),
                IsExpanded = DataGridBinding<bool>.Create<TreeNode>(node => node.IsExpanded, write: (node, value) => node.IsExpanded = value),
                Column = new DataGridTextColumn { Binding = DataGridBinding<string>.Create<TreeNode>(node => node.Name) },
            });
            using var context = new UIContext();
            context.Add(grid);
            context.Layout();

            root.Children.Insert(0, new TreeNode("Inserted child"));
            context.Layout();

            var shiftedRow = (DataGridRow)grid.GetRealizedContainer(2);
            Assert.Multiple(() =>
            {
                Assert.That(shiftedRow.DataContext, Is.SameAs(originalChild));
                Assert.That(shiftedRow.RowIndex, Is.EqualTo(2));
                Assert.That(shiftedRow.IndexPath, Is.EqualTo(grid.GetRowPath(2)));
                Assert.That(shiftedRow.Cells, Has.All.Matches<DataGridCell>(cell => cell.RowPath == shiftedRow.IndexPath));
            });
        }

        [Test]
        public void DataGrid_DefaultHierarchyExpanderUsesTreeThemeIcons()
        {
            var collapsed = new ThemeIcon(new DrawingImage(), new Point(8, 8));
            var collapsedMirrored = new ThemeIcon(new DrawingImage(), new Point(9, 8));
            var expanded = new ThemeIcon(new DrawingImage(), new Point(10, 8));
            var root = new TreeNode("Root", new TreeNode("Child"));
            var grid = new DataGrid
            {
                Mode = DataGridMode.Hierarchical,
                ItemsSource = new[] { root },
                Size = new Vector2(240, 120),
            };
            grid.Columns.Add(new DataGridExpanderColumn
            {
                Children = DataGridBinding<System.Collections.IEnumerable>.Create<TreeNode>(node => node.Children),
                HasChildren = DataGridBinding<bool>.Create<TreeNode>(node => node.Children.Count != 0),
                IsExpanded = DataGridBinding<bool>.Create<TreeNode>(node => node.IsExpanded, write: (node, value) => node.IsExpanded = value),
                Column = new DataGridTextColumn { Binding = DataGridBinding<string>.Create<TreeNode>(node => node.Name) },
            });
            using var context = new UIContext();
            context.Theme.SetIcon("arrow_collapsed", collapsed, nameof(Tree));
            context.Theme.SetIcon("arrow_collapsed_mirrored", collapsedMirrored, nameof(Tree));
            context.Theme.SetIcon("arrow", expanded, nameof(Tree));
            context.Add(grid);
            context.Layout();

            var button = (DataGridExpanderButton)((StackPanel)grid.GetCell(0, 0).Content).Children[0];
            Assert.Multiple(() =>
            {
                Assert.That(button.ResolveExpanderIcon(), Is.EqualTo(collapsed));
                Assert.That(button.HideTextWhenDecorativeIconAvailable, Is.True);
                Assert.That(button.Text, Is.EqualTo("+"), "The text remains as a fallback for themes without hierarchy icons.");
                Assert.That(button.AccessibilityName, Is.EqualTo("Expand row"));
                Assert.That(button.TooltipText, Is.EqualTo("Expand row"));
                Assert.That(button.GetMinimumSize().X, Is.LessThanOrEqualTo(18));
            });

            grid.LayoutDirection = LayoutDirection.RightToLeft;
            Assert.That(button.ResolveExpanderIcon(), Is.EqualTo(collapsedMirrored));

            grid.LayoutDirection = LayoutDirection.LeftToRight;
            button.KeyPressed(Keys.Enter);
            button.KeyReleased(Keys.Enter);
            context.Layout();
            Assert.Multiple(() =>
            {
                Assert.That(root.IsExpanded, Is.True);
                Assert.That(button.ResolveExpanderIcon(), Is.EqualTo(expanded));
                Assert.That(button.Text, Is.EqualTo("-"));
                Assert.That(button.AccessibilityName, Is.EqualTo("Collapse row"));
                Assert.That(button.TooltipText, Is.EqualTo("Collapse row"));
            });
        }

        [Test]
        public void DataGrid_HeaderSortingAndFilteringPreserveSelectedOccurrences()
        {
            var first = new SortRow("b", 1);
            var selected = new SortRow("a", 2);
            var third = new SortRow("a", 1);
            var column = new DataGridTextColumn
            {
                Header = "Name",
                Binding = DataGridBinding<string>.Create<SortRow>(row => row.Name),
                SortBinding = DataGridSortBinding.Create<SortRow, string>(row => row.Name),
            };
            var orderColumn = new DataGridTextColumn
            {
                Header = "Order",
                Binding = DataGridBinding<string>.Create<SortRow>(row => row.Order.ToString()),
                SortBinding = DataGridSortBinding.Create<SortRow, int>(row => row.Order),
            };
            var grid = new DataGrid
            {
                ItemsSource = new[] { first, selected, third },
                Size = new Vector2(200, 100),
            };
            grid.Columns.Add(column);
            grid.Columns.Add(orderColumn);
            using var context = new UIContext();
            context.Add(grid);
            context.Layout();
            grid.SelectedItem = selected;

            grid.ActivateColumnHeader(0);
            grid.SortDescriptions.Add(new DataGridSortDescription(orderColumn, DataGridSortDirection.Ascending));
            context.Layout();
            Assert.Multiple(() =>
            {
                Assert.That(grid.GetRealizedContainer(0).DataContext, Is.SameAs(third));
                Assert.That(grid.GetRealizedContainer(1).DataContext, Is.SameAs(selected));
                Assert.That(grid.SelectedItem, Is.SameAs(selected));
                Assert.That(grid.SelectedIndex, Is.EqualTo(1));
                Assert.That(grid.GetColumnHeader(0).SortDirection, Is.EqualTo(DataGridSortDirection.Ascending));
                Assert.That(grid.GetColumnHeader(1).SortDirection, Is.EqualTo(DataGridSortDirection.Ascending));
            });

            grid.Filter = item => ((SortRow)item).Order == 2;
            Assert.That(grid.RealizedCount, Is.EqualTo(3));
            grid.RefreshFilter();
            context.Layout();
            Assert.Multiple(() =>
            {
                Assert.That(grid.RealizedCount, Is.EqualTo(1));
                Assert.That(grid.SelectedItem, Is.SameAs(selected));
                Assert.That(grid.SelectedIndex, Is.Zero);
            });

            grid.Filter = null;
            grid.RefreshFilter();
            grid.ActivateColumnHeader(0);
            Assert.Multiple(() =>
            {
                Assert.That(grid.SortDescriptions[0].Direction, Is.EqualTo(DataGridSortDirection.Descending));
                Assert.That(grid.GetColumnHeader(0).SortDirection, Is.EqualTo(DataGridSortDirection.Descending));
                Assert.That(grid.GetColumnHeader(1).SortDirection, Is.Null);
            });
            grid.ActivateColumnHeader(0);
            Assert.Multiple(() =>
            {
                Assert.That(grid.SortDescriptions, Is.Empty);
                Assert.That(grid.GetColumnHeader(0).SortDirection, Is.Null);
            });
            grid.Dispose();
        }

        [Test]
        public void DataGrid_CellSelectionSupportsRangesMutationAndRtlNavigation()
        {
            var rows = new ObservableCollection<SortRow>
            {
                new SortRow("a", 1),
                new SortRow("b", 2),
                new SortRow("c", 3),
            };
            var grid = new DataGrid
            {
                ItemsSource = rows,
                SelectionUnit = DataGridSelectionUnit.Cell,
                SelectionMode = ItemListSelectionMode.Multi,
                LayoutDirection = LayoutDirection.RightToLeft,
                Size = new Vector2(200, 100),
            };
            grid.Columns.Add(new DataGridTextColumn { Binding = DataGridBinding<string>.Create<SortRow>(row => row.Name) });
            grid.Columns.Add(new DataGridTextColumn { Binding = DataGridBinding<string>.Create<SortRow>(row => row.Order.ToString()) });
            using var context = new UIContext { ViewportSize = new Vector2(200, 100) };
            context.Add(grid);
            context.Layout();
            var changes = new List<DataGridCellSelectionChangedEventArgs>();
            grid.CellSelectionChanged += (_, args) => changes.Add(args);
            var first = new CellIndex(grid.GetRowPath(0), 0);
            var last = new CellIndex(grid.GetRowPath(1), 1);

            grid.SelectCellRange(first, last);
            Assert.Multiple(() =>
            {
                Assert.That(grid.SelectedCells, Has.Count.EqualTo(4));
                Assert.That(grid.CurrentCell, Is.EqualTo(last));
                Assert.That(grid.GetCell(0, 0).IsSelected, Is.True);
                Assert.That(grid.GetCell(1, 1).IsCurrent, Is.True);
                Assert.That(changes, Has.Count.EqualTo(1));
                Assert.That(changes[0].OldCells, Is.Empty);
                Assert.That(changes[0].NewCells, Has.Count.EqualTo(4));
            });

            grid.CurrentCell = new CellIndex(grid.GetRowPath(0), 0);
            grid.GetCell(0, 1).IsSelectable = false;
            grid.KeyPressed(Keys.Left);
            Assert.That(grid.CurrentCell.Value.ColumnIndex, Is.Zero);
            grid.GetCell(0, 1).IsSelectable = true;
            grid.KeyPressed(Keys.Left);
            Assert.That(grid.CurrentCell.Value.ColumnIndex, Is.EqualTo(1));
            grid.KeyPressed(Keys.Down);
            Assert.That(grid.CurrentCell.Value.RowPath, Is.EqualTo(grid.GetRowPath(1)));
            grid.CurrentCell = new CellIndex(grid.GetRowPath(0), 0);
            grid.KeyPressed(Keys.PageDown);
            Assert.That(grid.CurrentCell.Value.RowPath, Is.EqualTo(grid.GetRowPath(2)));

            grid.CurrentCell = new CellIndex(grid.GetRowPath(1), 1);
            var retained = grid.CurrentCell.Value;
            rows.Move(1, 0);
            Assert.That(grid.CurrentCell, Is.EqualTo(retained));
            rows.RemoveAt(0);
            Assert.Multiple(() =>
            {
                Assert.That(grid.CurrentCell, Is.Null);
                Assert.That(grid.SelectedCells.Any(cell => cell.RowPath == retained.RowPath), Is.False);
            });

            grid.SelectionUnit = DataGridSelectionUnit.Row;
            var rowPath = grid.GetRowPath(0);
            grid.SelectRowHeader(rowPath);
            Assert.Multiple(() =>
            {
                Assert.That(grid.SelectedRowPaths, Is.EqualTo(new[] { rowPath }));
                Assert.That(grid.CurrentRowPath, Is.EqualTo(rowPath));
            });
            grid.Dispose();
        }

        [Test]
        public void DataGrid_CellSelectionTracksSortFilterAndCollapse()
        {
            var first = new SortRow("b", 1);
            var selected = new SortRow("a", 2);
            var column = new DataGridTextColumn
            {
                Binding = DataGridBinding<string>.Create<SortRow>(row => row.Name),
                SortBinding = DataGridSortBinding.Create<SortRow, string>(row => row.Name),
            };
            var grid = new DataGrid
            {
                ItemsSource = new[] { first, selected },
                SelectionUnit = DataGridSelectionUnit.Cell,
                Size = new Vector2(200, 100),
            };
            grid.Columns.Add(column);
            using var context = new UIContext { ViewportSize = new Vector2(200, 100) };
            context.Add(grid);
            context.Layout();
            var selectedCell = new CellIndex(grid.GetRowPath(1), 0);
            grid.SelectCell(selectedCell);

            grid.ActivateColumnHeader(0);
            context.Layout();
            Assert.Multiple(() =>
            {
                Assert.That(grid.CurrentCell, Is.EqualTo(selectedCell));
                Assert.That(grid.SelectedCells, Is.EqualTo(new[] { selectedCell }));
                Assert.That(grid.GetCell(0, 0).IsSelected, Is.True);
            });
            grid.Filter = item => ReferenceEquals(item, selected);
            grid.RefreshFilter();
            Assert.That(grid.SelectedCells, Is.EqualTo(new[] { selectedCell }));
            grid.Filter = item => ReferenceEquals(item, first);
            grid.RefreshFilter();
            Assert.Multiple(() =>
            {
                Assert.That(grid.SelectedCells, Is.Empty);
                Assert.That(grid.CurrentCell, Is.Null);
            });
            grid.Dispose();

            var child = new TreeNode("Child");
            var root = new TreeNode("Root", child);
            var hierarchy = new DataGrid
            {
                Mode = DataGridMode.Hierarchical,
                SelectionUnit = DataGridSelectionUnit.Cell,
            };
            hierarchy.Columns.Add(new DataGridExpanderColumn
            {
                Children = DataGridBinding<System.Collections.IEnumerable>.Create<TreeNode>(node => node.Children),
                HasChildren = DataGridBinding<bool>.Create<TreeNode>(node => node.Children.Count != 0),
                Column = new DataGridTextColumn { Binding = DataGridBinding<string>.Create<TreeNode>(node => node.Name) },
            });
            hierarchy.ItemsSource = new[] { root };
            var rootPath = hierarchy.GetRowPath(0);
            Assert.That(hierarchy.Expand(rootPath), Is.True);
            hierarchy.SelectCell(new CellIndex(hierarchy.GetRowPath(1), 0));

            Assert.That(hierarchy.Collapse(rootPath), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(hierarchy.SelectedCells, Is.Empty);
                Assert.That(hierarchy.CurrentCell, Is.Null);
            });
            hierarchy.Dispose();
        }

        [Test]
        public void DataGrid_LargeFlatSourceBoundsRowsCellsAndColumnCount()
        {
            var grid = new DataGrid
            {
                ItemsSource = Enumerable.Range(0, 100_000).ToArray(),
                Size = new Vector2(160, 72),
            };
            grid.Columns.Add(new DataGridTextColumn
            {
                Binding = DataGridBinding<string>.Create<int>(value => value.ToString()),
            });
            using var context = new UIContext { ViewportSize = new Vector2(160, 72) };
            context.Add(grid);
            context.Layout();

            Assert.Multiple(() =>
            {
                Assert.That(grid.RealizedCount, Is.LessThanOrEqualTo(5));
                Assert.That(grid.RealizedCellCount, Is.EqualTo(grid.RealizedCount));
                Assert.Throws<InvalidOperationException>(() => grid.GetRealizedContainer(50_000));
            });

            for (var index = 1; index <= DataGrid.MaximumSupportedVisibleColumns; index++)
                grid.Columns.Add(new DataGridTextColumn { IsVisible = index < DataGrid.MaximumSupportedVisibleColumns });
            Assert.That(grid.GetDisplayColumns(), Has.Count.EqualTo(DataGrid.MaximumSupportedVisibleColumns));
            Assert.Throws<InvalidOperationException>(() => grid.Columns[^1].IsVisible = true);
            Assert.Multiple(() =>
            {
                Assert.That(grid.Columns[^1].IsVisible, Is.False);
                Assert.That(grid.GetDisplayColumns(), Has.Count.EqualTo(DataGrid.MaximumSupportedVisibleColumns));
            });
            grid.Dispose();
        }

        [Test]
        public void DataGridSource_LocalBranchChangesDoNotRevisitUnrelatedSubtrees()
        {
            var local = new TreeNode("Local", new TreeNode("One"), new TreeNode("Two"));
            var wide = new TreeNode("Wide", Enumerable.Range(0, 1_000)
                .Select(index => new TreeNode(index.ToString()))
                .ToArray()) { IsExpanded = true };
            using var source = new DataGridSource<TreeNode>(
                new[] { local, wide },
                node => node.Children,
                node => node.Children.Count != 0,
                node => node.IsExpanded);
            var localPath = source.GetPath(0);
            var beforeExpand = source.ProjectionVisitCount;

            Assert.That(source.Expand(localPath), Is.True);
            Assert.That(source.ProjectionVisitCount - beforeExpand, Is.EqualTo(2));
            var beforeAdd = source.ProjectionVisitCount;
            local.Children.Add(new TreeNode("Three"));
            Assert.Multiple(() =>
            {
                Assert.That(source.ProjectionVisitCount - beforeAdd, Is.EqualTo(1));
                Assert.That(source.Count, Is.EqualTo(1_005));
            });

            var beforeMove = source.ProjectionVisitCount;
            local.Children.Move(2, 0);
            local.Children.RemoveAt(0);
            Assert.That(source.ProjectionVisitCount, Is.EqualTo(beforeMove));
            Assert.That(source.Collapse(localPath), Is.True);
            Assert.That(source.ProjectionVisitCount, Is.EqualTo(beforeMove));
            var warmFrame = source.ProjectionVisitCount;
            _ = source.Count;
            _ = source.GetPath(source.Count - 1);
            Assert.That(source.ProjectionVisitCount, Is.EqualTo(warmFrame));
        }

        [Test]
        public void DataGridSource_DeepAndDuplicateNullOccurrencesKeepDeterministicPaths()
        {
            var leaf = new TreeNode("128");
            for (var depth = 127; depth >= 0; depth--)
                leaf = new TreeNode(depth.ToString(), leaf) { IsExpanded = true };
            using (var deep = new DataGridSource<TreeNode>(
                new[] { leaf },
                node => node.Children,
                getExpanded: node => node.IsExpanded))
            {
                Assert.That(deep.Count, Is.EqualTo(129));
                var middle = deep.GetPath(64);
                var beforeCollapse = deep.ProjectionVisitCount;
                Assert.That(deep.Collapse(middle), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(deep.Count, Is.EqualTo(65));
                    Assert.That(deep.ProjectionVisitCount, Is.EqualTo(beforeCollapse));
                });
            }

            const string duplicate = "same";
            var values = new ObservableCollection<string> { null, duplicate, duplicate };
            using var occurrences = new DataGridSource<string>(values, _ => Array.Empty<string>());
            var nullPath = occurrences.GetPath(0);
            var firstDuplicatePath = occurrences.GetPath(1);
            var secondDuplicatePath = occurrences.GetPath(2);
            Assert.That(firstDuplicatePath, Is.Not.EqualTo(secondDuplicatePath));

            values.Move(2, 0);
            Assert.Multiple(() =>
            {
                Assert.That(occurrences.GetPath(0), Is.EqualTo(secondDuplicatePath));
                Assert.That(occurrences.IndexOfPath(nullPath), Is.EqualTo(1));
                Assert.That(occurrences.IndexOfPath(firstDuplicatePath), Is.EqualTo(2));
            });
            values[1] = null;
            Assert.Multiple(() =>
            {
                Assert.That(occurrences[1], Is.Null);
                Assert.That(occurrences.GetPath(1), Is.Not.EqualTo(nullPath));
                Assert.That(occurrences.IndexOfPath(nullPath), Is.EqualTo(-1));
            });
        }

        [Test]
        public void DataGrid_LargeExpandedHierarchyKeepsRealizationAndSelectionBounded()
        {
            var children = Enumerable.Range(0, 10_000)
                .Select(index => new TreeNode($"Child {index}"))
                .ToArray();
            var root = new TreeNode("Root", children) { IsExpanded = true };
            var grid = new DataGrid
            {
                Mode = DataGridMode.Hierarchical,
                SelectionUnit = DataGridSelectionUnit.Cell,
                Size = new Vector2(180, 72),
            };
            grid.Columns.Add(new DataGridExpanderColumn
            {
                Children = DataGridBinding<System.Collections.IEnumerable>.Create<TreeNode>(node => node.Children),
                HasChildren = DataGridBinding<bool>.Create<TreeNode>(node => node.Children.Count != 0),
                IsExpanded = DataGridBinding<bool>.Create<TreeNode>(node => node.IsExpanded),
                Column = new DataGridTextColumn { Binding = DataGridBinding<string>.Create<TreeNode>(node => node.Name) },
            });
            grid.Columns.Add(new DataGridTextColumn { Binding = DataGridBinding<string>.Create<TreeNode>(node => node.Name) });
            grid.ItemsSource = new[] { root };
            using var context = new UIContext { ViewportSize = new Vector2(180, 72) };
            context.Add(grid);
            context.Layout();
            var selected = new CellIndex(grid.GetRowPath(0), 1);
            grid.SelectCell(selected);

            Assert.Multiple(() =>
            {
                Assert.That(grid.HierarchySource.IsExpanded(grid.GetRowPath(0)), Is.True);
                Assert.That(grid.RealizedCount, Is.LessThanOrEqualTo(10));
                Assert.That(grid.RealizedCellCount, Is.EqualTo(grid.RealizedCount * 2));
            });
            grid.ScrollOffset = new Vector2(0, DataGrid.DefaultEstimatedRowExtent * 5_000);
            context.Layout();
            Assert.Multiple(() =>
            {
                Assert.That(grid.RealizedCount, Is.LessThanOrEqualTo(10));
                Assert.That(grid.RealizedCellCount, Is.EqualTo(grid.RealizedCount * 2));
                Assert.That(grid.SelectedCells, Is.EqualTo(new[] { selected }));
                Assert.That(grid.CurrentCell, Is.EqualTo(selected));
                Assert.That(grid.HierarchySource.IsExpanded(grid.GetRowPath(0)), Is.True);
                Assert.That(grid.RecycledCount, Is.LessThanOrEqualTo(grid.RecyclePoolCapacity));
            });
            grid.Dispose();
        }

        private sealed class RowModel : INotifyPropertyChanged
        {
            private string _name;
            private bool _enabled;

            public RowModel(string name, bool enabled)
            {
                _name = name;
                _enabled = enabled;
            }

            public string Name
            {
                get => _name;
                set
                {
                    if (_name == value) return;
                    _name = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                }
            }

            public bool Enabled
            {
                get => _enabled;
                set
                {
                    if (_enabled == value) return;
                    _enabled = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Enabled)));
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        private sealed class TreeNode
        {
            public TreeNode(string name, params TreeNode[] children)
            {
                Name = name;
                Children = new ObservableCollection<TreeNode>(children);
            }

            public string Name { get; }
            public ObservableCollection<TreeNode> Children { get; }
            public bool IsExpanded { get; set; }
        }

        private sealed record SortRow(string Name, int Order);
    }
}