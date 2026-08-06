# ADR 0005: Template-First Compatibility and Lifetime Rules

- Status: Accepted
- Date: 2026-08-04
- Owners: Forma maintainers
- Related plan: `plans/xaml-templates-items-and-virtualization-plan.md`, Phase 0 task 4
- Related manifest: `docs/control-template-migration-manifest.md`

## Context

Forma currently has one `Control.Children`/`Parent` tree for application ownership, layout,
drawing, hit testing, inherited data, and context attachment. Compiled XAML roots own one-shot
attachment scopes: leaving an active `UIContext` disposes their bindings, resources, styles,
events, triggers, and clocks. Template projection and virtualized recycling need visual placement
without changing logical ownership and need reversible deactivation without weakening ordinary
XAML-root disposal.

This release is intentionally source/API breaking where the migration manifest requires it, but
existing semantic behavior, first-party applications, core-only packaging, and deterministic
lifetime must remain explicit.

## Decision

### Architectural boundary

- `Control` remains the universal styleable visual/layout node. It supplies geometry, input,
  resources, classes, inherited values, composition, and direct visual children.
- `TemplatedControl : Control` alone exposes `Template`, `ApplyTemplate`, `TemplateRoot`,
  `GetTemplateChild`, template invalidation, and templated-parent state.
- Foundational primitives, panels, presenters, and specialized parts never resolve a default
  `ControlTemplate`. Their drawing/layout/projection behavior remains direct.
- Semantic widgets derive from `TemplatedControl`, own behavior and state, and draw no unconditional
  replaceable outer chrome. Packaged defaults and author templates use the same typed
  `TemplateInstance` contract.
- A template root must be foundational or an intentional nested semantic widget. Template lookup
  stops at foundational roots and self-recursive template graphs are rejected.

### Ownership trees

- `Children`/`Parent` remains the public logical ownership tree for application content and
  disposal ownership.
- Internal `VisualParent`/visual children drive layout, drawing, hit testing, focus bounds,
  accessibility bounds, selectors, clipping, and context attachment.
- One explicit `InheritanceParent` drives data context, resources, theme/tokens, enabled state,
  language, direction, and inherited text values.
- An ordinary child uses its logical parent for visual placement and inheritance. A template root
  is visually owned by its `TemplatedControl` and inherits through that owner. Projected content
  retains its logical owner/inheritance even when a presenter places it elsewhere. Generated item
  containers inherit from the items owner; their data-template roots inherit from the container.
- No mutable control, template instance, panel instance, or presenter instance may have two owners.
  Cycles and multiple visual/inheritance parents are errors.

### Template and item selection

- Items use an explicit `ItemTemplate`: inline, directly assigned, or a keyed `DataTemplate`
  resource. There is no implicit runtime-type matching, assembly scanning, reflection lookup, or
  `DataTemplateSelector` in this release.
- A `DataTemplate` produces one fresh root and local namescope per realized source occurrence. A
  `ControlTemplate` produces one fresh root and local namescope per templated owner.
  `ItemsPanelTemplate` produces one fresh compatible panel per items presenter.
- Event attributes are illegal directly inside `DataTemplate`. Eventful rows are ordinary separate
  compiled `x:Class` controls referenced by the data template; handlers resolve only on that row.
- Generated factories, bindings, relative sources, selectors, parts, and state accessors are closed
  and statically typed. No release path uses a runtime XAML reader or reflection fallback.

### Collection and thread behavior

- Before attachment, an items source may be prepared on any thread only while it is not observed by
  an attached owner.
- Once an items owner is attached to a `UIContext`, every collection notification and selection
  mutation must occur on that context's UI thread. A cross-thread notification throws a stable
  deterministic exception; Forma does not silently dispatch, queue, or reorder it.
- Collection deltas preserve one logical slot per source occurrence. Duplicate references are not
  coalesced. Add/remove preserve unaffected slots, move preserves slot identity, replace creates a
  new slot, and reset rebuilds.

### Lifetime states

Ordinary controls are not implicitly disposed merely because `RemoveChild` clears their parent and
context. The ownership rule concerns framework-created attachment state:

- An ordinary compiled view/XAML root owns a one-shot `XamlAttachmentScope`. Its first transition
  from a non-null `UIContext` to another context or no context disposes bindings, resource
  subscriptions, styles, event subscriptions, triggers, transitions, and clocks. Reattaching the
  same disposed compiled root does not reactivate that scope; callers create/load a new view.
- A `TemplateInstance` owns its root, local namescope, bindings, styles, resources, triggers,
  transitions, and clocks. `Dispose` is final and recursive for this owned state.
- Template reapplication disposes the prior instance before exposing the replacement. It never
  reuses a mutable root from another owner.
- Virtualized recycling uses explicit `Deactivate` and `Activate` states, not ordinary-tree detach
  semantics. Deactivation removes active visual/context/inheritance participation, clears transient
  input/focus/capture state, suspends subscriptions/clocks, and retains only pool-approved state.
  Activation rebinds the same compatible instance to one new logical slot before attachment.
- A stale theme/template/hot-reload version, a non-poolable event/code-behind lifetime, failed
  rebind, pool eviction, or owner disposal calls final `Dispose`; such an instance is never
  reactivated.
- Controls and templates never own shared fonts, textures, brushes, immutable geometries, or text
  layouts unless an API explicitly transfers ownership. Device-scoped caches are owned by the UI
  rendering service and obey ADR 0004 budgets.

### Compatibility policy

- Behavioral APIs listed in the migration manifest remain on semantic owners even when their
  rendering moves into presenters/templates.
- Compatibility aliases for existing foundation types may remain template-free, but may not
  preserve a second legacy rendering path for semantic widgets.
- Public owner-coupled helper types first move behind public base/interface return types and become
  invalid standalone XAML roots; internalization then follows the breaking-release API noted in the
  manifest.
- Catalog and Signal Run migrate in the same phase as every affected runtime type. No phase may
  leave either application on temporary legacy chrome. Signal Run remains core-only in Release.

## Rejected Alternatives

- Making all `Control` types templated: rejected because it permits recursive primitive templates
  and obscures direct-render/layout ownership.
- Treating visual projection as logical reparenting: rejected because it changes data/resource
  inheritance and disposal ownership when templates change.
- Reusing ordinary detach for pooled items: rejected because current XAML attachment scopes are
  intentionally one-shot and pooled instances require reversible suspension.
- Implicit item-type template lookup: rejected because it requires runtime discovery or ambiguous
  precedence and makes NativeAOT closure harder to prove.
- Silently dispatching collection notifications: rejected because it changes event order and hides
  ownership bugs.
- Retaining legacy semantic chrome behind a fallback switch: rejected because template replacement
  would not control the complete visual surface.

## Consequences

Phase 1 must add distinct logical, visual, and inheritance relationships plus an explicit template
lifecycle without changing ordinary compiled-root disposal. Virtualization must use its own bounded
activation protocol. The migration is deliberately breaking at declared helper APIs and visual
structure, but behavior ownership, application continuity, AOT closure, and resource ownership are
now testable contracts rather than implementation convention.
