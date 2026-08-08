// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using Forma.Xaml;

namespace Forma
{
    /// <summary>Presents an object through a content template with configurable horizontal and vertical alignment.</summary>
    [TemplatePart(ContentPresenterPartName, typeof(ContentPresenter), false)]
    public class ContentControl : TemplatedControl
    {
        public const string ContentPresenterPartName = "PART_ContentPresenter";
        private object _content;
        private DataTemplate _contentTemplate;
        private HorizontalAlignment _horizontalContentAlignment = HorizontalAlignment.Fill;
        private VerticalAlignment _verticalContentAlignment = VerticalAlignment.Fill;

        public object Content
        {
            get => _content;
            set => SetContent(value, false);
        }

        internal void SetGeneratedContent(object value)
        {
            var presenter = ResolveContentPresenter();
            if (presenter != null && presenter.CanRebindGeneratedContent(_contentTemplate))
            {
                _content = value;
                ActivateTemplateAfterRecycle();
                try { presenter.RebindGeneratedContentAfterRecycle(value); }
                catch
                {
                    _content = null;
                    throw;
                }
                OnPropertyChanged(nameof(Content));
                return;
            }
            SetContent(value, true);
        }

        internal bool TryDeactivateGeneratedContentForRecycle()
        {
            var presenter = ResolveContentPresenter();
            if (presenter == null || !presenter.TryDeactivateGeneratedContentForRecycle()) return false;
            DeactivateTemplateForRecycle();
            _content = null;
            OnPropertyChanged(nameof(Content));
            return true;
        }

        private void SetContent(object value, bool forceNullTemplate)
        {
            if (!forceNullTemplate && ReferenceEquals(_content, value)) return;
            var previous = _content;
            var previousControl = previous as Control;
            var previousOwned = previousControl?.Parent == this;
            var candidate = _contentTemplate == null ? value as Control : null;
            if (candidate != null) base.AddChild(candidate);
            try
            {
                var presenter = ResolveContentPresenter();
                if (presenter != null)
                {
                    if (forceNullTemplate) presenter.SetGeneratedContent(value);
                    else presenter.Content = value;
                }
                _content = value;
                if (previousOwned && !ReferenceEquals(previousControl, candidate))
                    base.RemoveChild(previousControl);
                OnPropertyChanged(nameof(Content));
                EnsureContentPresenter();
                var appliedPresenter = ResolveContentPresenter();
                if (forceNullTemplate && appliedPresenter != null && appliedPresenter.PresentedControl == null)
                    appliedPresenter.SetGeneratedContent(value);
            }
            catch
            {
                _content = previous;
                if (candidate?.Parent == this) base.RemoveChild(candidate);
                if (previousOwned && previousControl.Parent != this) base.AddChild(previousControl);
                ResolveContentPresenter()?.SetContentAfterFailedReplacement(previous);
                throw;
            }
        }

        public DataTemplate ContentTemplate
        {
            get => _contentTemplate;
            set
            {
                if (ReferenceEquals(_contentTemplate, value)) return;
                var previous = _contentTemplate;
                var contentControl = _content as Control;
                var wasOwned = contentControl?.Parent == this;
                var shouldOwn = value == null && contentControl != null;
                if (shouldOwn && !wasOwned) base.AddChild(contentControl);
                try
                {
                    var presenter = ResolveContentPresenter();
                    if (presenter != null) presenter.ContentTemplate = value;
                    _contentTemplate = value;
                    if (!shouldOwn && wasOwned) base.RemoveChild(contentControl);
                    OnPropertyChanged(nameof(ContentTemplate));
                    EnsureContentPresenter();
                }
                catch
                {
                    _contentTemplate = previous;
                    if (shouldOwn && !wasOwned && contentControl.Parent == this) base.RemoveChild(contentControl);
                    if (wasOwned && contentControl.Parent != this) base.AddChild(contentControl);
                    var presenter = ResolveContentPresenter();
                    if (presenter != null) presenter.ContentTemplate = previous;
                    throw;
                }
            }
        }

        public HorizontalAlignment HorizontalContentAlignment
        {
            get => _horizontalContentAlignment;
            set
            {
                if (_horizontalContentAlignment == value) return;
                _horizontalContentAlignment = value;
                var presenter = ResolveContentPresenter();
                if (presenter != null) presenter.HorizontalContentAlignment = value;
                OnPropertyChanged(nameof(HorizontalContentAlignment));
                QueueLayout();
            }
        }

        public VerticalAlignment VerticalContentAlignment
        {
            get => _verticalContentAlignment;
            set
            {
                if (_verticalContentAlignment == value) return;
                _verticalContentAlignment = value;
                var presenter = ResolveContentPresenter();
                if (presenter != null) presenter.VerticalContentAlignment = value;
                OnPropertyChanged(nameof(VerticalContentAlignment));
                QueueLayout();
            }
        }

        public override void AddChild(Control child)
        {
            if (child == null) throw new ArgumentNullException(nameof(child));
            if (_content != null && !ReferenceEquals(_content, child))
                throw new InvalidOperationException("ContentControl accepts one content value.");
            Content = child;
        }

        private void EnsureContentPresenter()
        {
            if (TemplateRoot == null && (_content != null || _contentTemplate != null)) ApplyTemplate();
        }

        protected override void OnTemplateApplied()
        {
            base.OnTemplateApplied();
            var presenter = ResolveContentPresenter();
            if (presenter == null) return;
            presenter.ContentTemplate = _contentTemplate;
            presenter.Content = _content;
            presenter.HorizontalContentAlignment = _horizontalContentAlignment;
            presenter.VerticalContentAlignment = _verticalContentAlignment;
        }

        private ContentPresenter ResolveContentPresenter() =>
            TemplateRoot as ContentPresenter ?? GetTemplateChild(ContentPresenterPartName) as ContentPresenter;
    }
}