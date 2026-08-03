#!/usr/bin/env bash
set -euo pipefail

# Purpose: Summarize Markdown checklist progress by section and optionally list checklist items or
# fail while work remains. Usage: `bash scripts/track-plan.sh [OPTIONS] [PLAN]`; run with `--help`
# for all modes. Without a plan path, the dynamic text rendering plan is summarized.

script_directory="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
default_plan="$(cd "$script_directory/.." && pwd)/docs/dynamic-text-rendering-plan.md"
plan="$default_plan"
mode="summary"
fail_if_incomplete=false

usage() {
  cat <<'EOF'
Usage: track-plan.sh [--summary|--remaining|--all] [--fail-if-incomplete] [PLAN]

  --summary             Show overall and per-section progress (default).
  --remaining           Also list unchecked items with file line references.
  --all                 Also list every checklist item with its state.
  --fail-if-incomplete  Exit with status 1 unless every checklist item is checked.
EOF
}

while (($#)); do
  case "$1" in
    --summary|--remaining|--all)
      mode="${1#--}"
      ;;
    --fail-if-incomplete)
      fail_if_incomplete=true
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    --*)
      printf 'Unknown option: %s\n' "$1" >&2
      usage >&2
      exit 2
      ;;
    *)
      plan="$1"
      ;;
  esac
  shift
done

if [[ ! -f "$plan" ]]; then
  printf 'Plan not found: %s\n' "$plan" >&2
  exit 2
fi

awk -v plan="$plan" -v mode="$mode" -v strict="$fail_if_incomplete" '
function percentage(checked, total) {
  return total == 0 ? "0.0" : sprintf("%.1f", checked * 100 / total)
}

function remember_section(name) {
  if (!(name in section_seen)) {
    section_seen[name] = 1
    section_order[++section_count] = name
  }
}

/^## / {
  section = substr($0, 4)
  remember_section(section)
  next
}

/^### Phase [0-9]+:/ {
  section = substr($0, 5)
  remember_section(section)
  next
}

/^- \[[ xX]\] / {
  item = substr($0, 7)
  is_checked = substr($0, 4, 1) ~ /[xX]/
  total++
  section_total[section]++
  if (is_checked) {
    checked++
    section_checked[section]++
    state = "x"
  } else {
    remaining++
    state = " "
  }

  item_line[total] = NR
  item_section[total] = section
  item_state[total] = state
  item_text[total] = item
}

END {
  printf "Plan: %s\n", plan
  printf "Overall: %d/%d checked (%s%%), %d remaining\n", checked, total, percentage(checked, total), remaining
  print "Sections:"
  for (section_index = 1; section_index <= section_count; section_index++) {
    name = section_order[section_index]
    if (section_total[name] > 0) {
      printf "  %s: %d/%d (%s%%)\n", name, section_checked[name], section_total[name], percentage(section_checked[name], section_total[name])
    }
  }

  if (mode == "remaining" || mode == "all") {
    item_label = "All"
    if (mode == "remaining") {
      item_label = "Remaining"
    }
    printf "\n%s items:\n", item_label
    for (item_index = 1; item_index <= total; item_index++) {
      if (mode == "all" || item_state[item_index] == " ") {
        printf "%s:%d [%s] [%s] %s\n", plan, item_line[item_index], item_section[item_index], item_state[item_index], item_text[item_index]
      }
    }
  }

  if (strict == "true" && remaining > 0) {
    exit 1
  }
}
' "$plan"