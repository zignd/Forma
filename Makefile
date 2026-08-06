SHELL := /bin/bash
.DEFAULT_GOAL := help
.NOTPARALLEL:

DOTNET ?= dotnet
CONFIGURATION ?= Debug
CATALOG_ARGS ?=
MONOGAME_PROJECT ?= $(abspath ../MonoGame/MonoGame.Framework/MonoGame.Framework.DesktopGL.csproj)
FNA_PROJECT ?= $(abspath ../FNA/FNA.Core.csproj)
PLAN ?= plans/dynamic-text-rendering-plan.md
TRACK_ARGS ?= --summary
NATIVEAOT_RUNTIME ?=
NATIVEAOT_PROFILE ?=
NATIVEAOT_MODE ?=

MONOGAME_CATALOG := samples/Forma.Catalog.MonoGame/Forma.Catalog.MonoGame.csproj
FNA_CATALOG := samples/Forma.Catalog.FNA/Forma.Catalog.FNA.csproj
MONOGAME_XAML_GAME := samples/Forma.Xaml.Game.MonoGame/Forma.Xaml.Game.MonoGame.csproj
FNA_XAML_GAME := samples/Forma.Xaml.Game.FNA/Forma.Xaml.Game.FNA.csproj
UNIT_TESTS := tests/Forma.Tests/Forma.Tests.csproj
RENDER_TESTS := tests/Forma.RenderTests/Forma.RenderTests.csproj
DOTNET_ARGS := --configuration "$(CONFIGURATION)" --nologo

.PHONY: help setup tools restore restore-monogame restore-fna \
	build build-monogame build-fna \
	test test-unit test-unit-monogame test-unit-fna \
	test-xaml test-xaml-monogame test-xaml-fna xaml-build-fixtures \
	test-render test-render-monogame test-render-fna \
	performance performance-graphics \
	catalog-monogame catalog-monogame-local catalog-fna catalog-fna-local \
	xaml-game-monogame xaml-game-fna smoke smoke-monogame smoke-fna render-parity video-smoke \
	text-spike text-spike-local text-baseline xaml-spike \
	compliance backend-references parity packages aot-analyzers native-font-failures static-font-backend nativeaot check check-all \
	icons icons-import icons-verify unicode unicode-verify track clean

help: ## Show available targets and configuration variables.
	@awk 'BEGIN { FS = ":.*## "; printf "Forma development targets\n\n" } /^[a-zA-Z0-9_.-]+:.*## / { printf "  %-24s %s\n", $$1, $$2 } END { printf "\nVariables: CONFIGURATION, DOTNET, CATALOG_ARGS, MONOGAME_PROJECT, FNA_PROJECT, PLAN, TRACK_ARGS, NATIVEAOT_RUNTIME, NATIVEAOT_PROFILE, NATIVEAOT_MODE\n" }' $(MAKEFILE_LIST)

setup: tools restore ## Restore local tools and both runtime dependency graphs.

tools: ## Restore repository-local .NET tools.
	$(DOTNET) tool restore

restore: restore-monogame restore-fna ## Restore both runtime dependency graphs.

restore-monogame: ## Restore the MonoGame catalog graph.
	$(DOTNET) restore $(MONOGAME_CATALOG) -p:FormaRuntime=MonoGame --nologo

restore-fna: ## Restore the FNA catalog graph.
	$(DOTNET) restore $(FNA_CATALOG) -p:FormaRuntime=FNA --nologo

build: build-monogame build-fna ## Build both complete runtime graphs.

build-monogame: ## Build the complete MonoGame graph.
	$(DOTNET) build $(MONOGAME_CATALOG) $(DOTNET_ARGS) -p:FormaRuntime=MonoGame
	$(DOTNET) build $(MONOGAME_XAML_GAME) $(DOTNET_ARGS) -p:FormaRuntime=MonoGame

build-fna: ## Build the complete FNA graph.
	$(DOTNET) build $(FNA_CATALOG) $(DOTNET_ARGS) -p:FormaRuntime=FNA
	$(DOTNET) build $(FNA_XAML_GAME) $(DOTNET_ARGS) -p:FormaRuntime=FNA

test: test-unit test-render ## Run unit and render tests for both runtimes.

test-unit: test-unit-monogame test-unit-fna ## Run unit tests for both runtimes.

test-unit-monogame: ## Run MonoGame unit and catalog inventory tests.
	$(DOTNET) test $(UNIT_TESTS) $(DOTNET_ARGS) -p:FormaRuntime=MonoGame

test-unit-fna: ## Run FNA unit and catalog inventory tests.
	$(DOTNET) test $(UNIT_TESTS) $(DOTNET_ARGS) -p:FormaRuntime=FNA

test-xaml: test-xaml-monogame test-xaml-fna xaml-build-fixtures ## Run Forma XAML runtime, compiler, tooling, sample, and build tests.

test-xaml-monogame: ## Run focused Forma XAML tests against MonoGame.
	$(DOTNET) test tests/Forma.Xaml.Tests/Forma.Xaml.Tests.csproj $(DOTNET_ARGS) -p:FormaRuntime=MonoGame
	$(DOTNET) test tests/Forma.Xaml.Tool.Tests/Forma.Xaml.Tool.Tests.csproj $(DOTNET_ARGS) -p:FormaRuntime=MonoGame
	$(DOTNET) test tests/Forma.Xaml.Game.Tests/Forma.Xaml.Game.Tests.csproj $(DOTNET_ARGS) -p:FormaRuntime=MonoGame

test-xaml-fna: ## Run focused Forma XAML tests against FNA.
	$(DOTNET) test tests/Forma.Xaml.Tests/Forma.Xaml.Tests.csproj $(DOTNET_ARGS) -p:FormaRuntime=FNA
	$(DOTNET) test tests/Forma.Xaml.Tool.Tests/Forma.Xaml.Tool.Tests.csproj $(DOTNET_ARGS) -p:FormaRuntime=FNA
	$(DOTNET) test tests/Forma.Xaml.Game.Tests/Forma.Xaml.Game.Tests.csproj $(DOTNET_ARGS) -p:FormaRuntime=FNA

xaml-build-fixtures: ## Validate compiled, invalid, incremental, PDB, and deterministic XAML builds.
	bash scripts/test-xaml-build-fixtures.sh

test-render: test-render-monogame test-render-fna ## Run render tests for both runtimes.

test-render-monogame: ## Run MonoGame render tests.
	$(DOTNET) test $(RENDER_TESTS) $(DOTNET_ARGS) -p:FormaRuntime=MonoGame

test-render-fna: ## Run FNA render tests.
	$(DOTNET) test $(RENDER_TESTS) $(DOTNET_ARGS) -p:FormaRuntime=FNA

performance: ## Run deterministic template, collection, selector, and virtualization invariants.
	bash scripts/check-xaml-performance-invariants.sh

performance-graphics: ## Run bounded compositor/effect and warm graphics-cache invariants.
	bash scripts/test-dynamic-render-smoke.sh

catalog-monogame: ## Launch the interactive MonoGame catalog.
	$(DOTNET) run --project $(MONOGAME_CATALOG) --configuration "$(CONFIGURATION)" -p:FormaRuntime=MonoGame $(CATALOG_ARGS)

catalog-monogame-local: ## Launch the catalog against a local MonoGame fork.
	@test -f "$(MONOGAME_PROJECT)" || { echo "MONOGAME_PROJECT does not exist: $(MONOGAME_PROJECT)" >&2; exit 2; }
	DOTNET="$(DOTNET)" bash scripts/run-catalog-local.sh MonoGame "$(MONOGAME_CATALOG)" "$(MONOGAME_PROJECT)" "$(CONFIGURATION)" -- $(CATALOG_ARGS)

catalog-fna: ## Launch the interactive FNA catalog.
	$(DOTNET) run --project $(FNA_CATALOG) --configuration "$(CONFIGURATION)" -p:FormaRuntime=FNA $(CATALOG_ARGS)

catalog-fna-local: ## Launch the catalog against a local FNA fork.
	@test -f "$(FNA_PROJECT)" || { echo "FNA_PROJECT does not exist: $(FNA_PROJECT)" >&2; exit 2; }
	DOTNET="$(DOTNET)" bash scripts/run-catalog-local.sh FNA "$(FNA_CATALOG)" "$(FNA_PROJECT)" "$(CONFIGURATION)" -- $(CATALOG_ARGS)

xaml-game-monogame: ## Launch the shared XAML sample with MonoGame.
	$(DOTNET) run --project $(MONOGAME_XAML_GAME) --configuration "$(CONFIGURATION)" -p:FormaRuntime=MonoGame

xaml-game-fna: ## Launch the shared XAML sample with FNA.
	$(DOTNET) run --project $(FNA_XAML_GAME) --configuration "$(CONFIGURATION)" -p:FormaRuntime=FNA

smoke: smoke-monogame smoke-fna ## Run both bounded catalog smoke checks.

smoke-monogame: ## Run the bounded MonoGame catalog smoke check.
	bash scripts/check-catalog-smoke.sh
	$(DOTNET) test tests/Forma.Xaml.Game.Tests/Forma.Xaml.Game.Tests.csproj $(DOTNET_ARGS) -p:FormaRuntime=MonoGame

smoke-fna: ## Run the bounded FNA catalog smoke check.
	FormaRuntime=FNA bash scripts/check-catalog-smoke.sh
	$(DOTNET) test tests/Forma.Xaml.Game.Tests/Forma.Xaml.Game.Tests.csproj $(DOTNET_ARGS) -p:FormaRuntime=FNA

render-parity: ## Compare deterministic catalog rendering across runtimes.
	bash scripts/check-catalog-render-parity.sh

video-smoke: ## Validate FNA Theora playback through Forma.Media.
	bash scripts/check-fna-video-smoke.sh

text-spike: ## Validate dynamic text shaping, rasterization, and atlas upload against both packages.
	bash scripts/check-dynamic-text-spike.sh

text-spike-local: ## Validate the dynamic text spike against packaged peers and a local FNA fork.
	@test -f "$(FNA_PROJECT)" || { echo "FNA_PROJECT does not exist: $(FNA_PROJECT)" >&2; exit 2; }
	FNA_PROJECT="$(FNA_PROJECT)" bash scripts/check-dynamic-text-spike.sh

text-baseline: ## Capture the pre-dynamic-text catalog screenshots and metrics at 1x and 2x.
	bash scripts/capture-dynamic-text-baseline.sh

compliance: ## Validate licenses, notices, and SPDX identifiers.
	bash scripts/check-compliance.sh

backend-references: ## Compile Forma.Media against supported MonoGame backends.
	bash scripts/check-backend-references.sh

parity: ## Build/test both runtimes and compare references and public APIs.
	bash scripts/check-runtime-parity.sh

packages: ## Build and validate all peer packages and isolated consumers.
	bash scripts/test-package-consumer.sh

aot-analyzers: ## Build fast source-linked trim/AOT analyzer consumers for both runtimes.
	bash scripts/check-aot-analyzers.sh

native-font-failures: ## Verify bounded missing, incompatible, and rejected native font failures.
	bash scripts/check-native-font-failures.sh

static-font-backend: ## Validate source-injected platform font backends without FreeType/HarfBuzz sidecars.
	FORMA_RUNTIME="$(NATIVEAOT_RUNTIME)" bash scripts/test-static-font-backend-spike.sh

nativeaot: static-font-backend ## Validate trim-only and NativeAOT package consumers on macOS arm64.
	NATIVEAOT_RUNTIME="$(NATIVEAOT_RUNTIME)" NATIVEAOT_PROFILE="$(NATIVEAOT_PROFILE)" NATIVEAOT_MODE="$(NATIVEAOT_MODE)" bash scripts/test-nativeaot-package-consumer.sh

xaml-spike: ## Validate XAML compiler feasibility and a compiler-free NativeAOT view.
	bash scripts/test-xaml-spike.sh

icons: ## Regenerate canonical 1x/2x default theme icon atlases.
	$(DOTNET) run --project tools/Forma.IconPipeline/Forma.IconPipeline.csproj -- generate

icons-import: ## Import mapped icons from GODOT_ROOT at the pinned revision.
	@test -n "$(GODOT_ROOT)" || { echo "GODOT_ROOT is required." >&2; exit 2; }
	$(DOTNET) run --project tools/Forma.IconPipeline/Forma.IconPipeline.csproj -- import "$(GODOT_ROOT)"

icons-verify: ## Regenerate and byte-compare committed icon outputs.
	$(DOTNET) run --project tools/Forma.IconPipeline/Forma.IconPipeline.csproj -- verify

unicode: ## Regenerate canonical Unicode 17 managed tables and conformance cases.
	$(DOTNET) run --project tools/Forma.UnicodePipeline/Forma.UnicodePipeline.csproj -- generate

unicode-verify: ## Download pinned Unicode sources and byte-compare generated outputs.
	$(DOTNET) run --project tools/Forma.UnicodePipeline/Forma.UnicodePipeline.csproj -- verify

check: compliance icons-verify unicode-verify parity test-xaml performance aot-analyzers native-font-failures ## Run the portable CI validation gates.

check-all: check backend-references smoke performance-graphics render-parity video-smoke packages nativeaot ## Run every validation, including graphical, package, and NativeAOT checks.

track: ## Show plan progress; override PLAN and TRACK_ARGS as needed.
	bash scripts/track-plan.sh $(TRACK_ARGS) "$(PLAN)"

clean: ## Clean both runtime variants across the solution.
	$(DOTNET) clean Forma.slnx $(DOTNET_ARGS) -p:FormaRuntime=MonoGame
	$(DOTNET) clean Forma.slnx $(DOTNET_ARGS) -p:FormaRuntime=FNA