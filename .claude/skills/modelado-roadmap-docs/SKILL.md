---
name: modelado-roadmap-docs
description: How to work a task in this project's roadmap — the docs/ files (ROADMAP.md, CHANGELOG.md, IMPROVEMENTS.md, and DECISIONS.md / DEFERRED.md once roadkeep writes them) owned by the roadkeep CLI and never hand-edited, and above all the one-task-one-commit rule: every finished roadmap task ends in one local commit — `git add -- <its paths>` then `git commit -F <message file>`, no Claude attribution, never pushed — code plus doc sync in that single commit. Use whenever adding a task, picking the next MOD-number, marking a task shipped, retiring, deferring, linting, editing any of those files, executing a block or a list of MOD ids, or finishing any task that touches this repo.
---

# Roadmap tasks & committing

**No figure in this file is derived from the backlog.** Counts, the priority queue and
which ids are open are all things `roadkeep` answers, and a sentence here quoting one is a
sentence that was true on the day it was written. Where you would want a number, this file
names the read instead.

## ⛔ READ FIRST — one task, one commit (non-negotiable)

**A task is not finished until the commit landed.** Commit with plain `git`, staged by
path, from the repo root — not `run-commit.cmd`, whose `git add *` takes the whole tree:

```
cd D:\Git\alegauss\modelado
git add -- <this task's paths>
git commit -F <message file>
```

- **Stage by path, never everything** — no `git add -A`, `.` or `*`. Another session in
  this checkout would have its files committed under your title, so
  `git diff --cached --stat` must list this task's files alone.
- **The message comes from a scratchpad file**: a conventional-commits title in ASCII, then
  a few plain lines on what changed and why. `-F` keeps it off the command line, where an em
  dash or an accent may not arrive as the bytes typed.
- **No attribution, no push.** No `Co-Authored-By` trailer, no "Generated with Claude
  Code" line, no `--author`: the commit is the user's, and it stays local. Hooks run —
  never `--no-verify`.
- **The doc sync rides in the same commit as the code**, so the governed files never
  describe a state that did not ship.
- **You may NOT do more than one task before committing.** A multi-task request
  (a whole block, or a list of `MOD<n>`s) is _not_ permission to batch: it is a request
  to run them one at a time, committing after each.
- **A batch of ≥2 tasks runs under the `/loop` skill** (self-paced): exactly one task
  per iteration, committed at the end of that iteration, then advance. Do not
  hand-roll a loop that defers commits to the end.
- **Self-check before starting task N+1:** `git status` / `git log -1`. If the previous
  task's work is still in the working tree, stop and commit it first.
- **Declare your paths, then read them back at the moment of committing.**
  `roadkeep claim MOD<n> --path <p>` says what the task will touch; **`roadkeep claim
  MOD<n>` with no `--path` answers what you declared plus what the tree holds that another
  live claim says is its own — the analysis `git add -A` cannot make.** `roadkeep claims`
  lists held, expired and stale, and `[claims] held` in `roadkeep.toml` is how long one
  stands.

The same rule applies to any finished unit of work in this repo, roadmap task or not:
when the work is done and validated, commit it the same way rather than leaving it in the
tree.

## The gate before the commit

Validated means the project's own build and test were run, not that the edit looked right.

| Command                 | What it holds                                                                                                                                                                  | When                                                                        |
| ----------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | --------------------------------------------------------------------------- |
| `run-tests-vm.cmd`      | Carries the working tree to the VMware guest and runs `run-tests.cmd` there: `dotnet build Modelado.slnx` with warnings as errors, every `tests\*.Tests` app, the winwright UI cases in `tests\Modelado.Cases`, and the Python suite in `scripts\tests`. Green only if every suite ran and passed **on the guest**. Never run the suite, the cases or the app for checking on the host desk. | Every task that touches code under `src\`, `tests\`, `cases\`, `scripts\` or `tools\`. |
| `roadkeep lint`         | The governed files. Non-zero exit is the whole point of it.                                                                                                                    | Every task, without exception — it is the only gate a docs-only change has. |

The tests that use the real models run only when `.cache\models` exists
(`prepare-models.cmd`) and say they skipped otherwise — a skip is not a pass for a task
that changed model code. A UI task ships with a winwright case in `cases\*.cases.json` that
passed on the guest; `run-tests-vm.cmd -Screenshot` brings back a picture of the guest's desk
where one helps. `run.cmd` launches the app for a person to use; it is not a gate.

**Name EVERY task id an assertion holds, not just the one you are working.** A test
written under one id often ends up holding the task that finished the work, and a test
that names no id is one nobody can trace back to what it was for.

## ⛔ READ SECOND — the governed files are owned by `roadkeep`

[`roadkeep.toml`](../../../roadkeep.toml) declares this project's format — prefix `MOD`,
`ref_scheme = "id"`, the markers, the limits — and [`.mcp.json`](../../../.mcp.json) plus
[`.claude/settings.json`](../../settings.json) wire the rest through
`.claude/hooks/roadkeep-launch.py`: a hook that **denies a hand-edit** to any governed file
and names the command that does it, the `mcp__roadkeep__*` tools whose input schema _is_
this project's format, and the upstream `roadkeep` skill with the whole write path. **Reach
for the MCP tools first**; the shell fallback is that launcher, not a `roadkeep` on PATH.

Start a task with `brief`, not by reading the files; `lint` is the gate.

Each file has one job — never duplicate content between them:

| File                                                    | Single responsibility                                                                                                                                                                 |
| ------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [`docs/ROADMAP.md`](../../../docs/ROADMAP.md)           | **Task status** — active backlog only (📋 designed · 💭 idea · ⏳ partial · 🛠 in-progress), one line per task, plus the `## Priority` queue, the block headings, the criteria and non-goals. |
| [`docs/CHANGELOG.md`](../../../docs/CHANGELOG.md)       | What has **shipped** — the ledger, indexed by block. Authoritative for the highest block letter.                                                                                      |
| [`docs/IMPROVEMENTS.md`](../../../docs/IMPROVEMENTS.md) | **Design rationale** for _unshipped_ work only. Deleted per task by the ship that closes it.                                                                                          |
| `docs/DECISIONS.md`                                     | **Constraints that outlive the work** — written only by `ship --decides`, corrected by `revise`, replaced by `supersede`. Nothing deletes an entry.                                   |
| `docs/DEFERRED.md`                                      | Lines **set aside** by `defer`, keeping their id, deps, symptom and section. `resume` brings one back.                                                                                |

## The loop

- **`brief --claim`** picks the next line and briefs it in one read, taking it in the same
  transaction. **Reach for `--designed`** when much of the backlog is 💭 — `list --marker
  💭` says how much — so an unscoped pick does not hand you a `section add` instead of a
  commit.
- **`--have <word>`** where the machine has one of the requirements `[requirements]
  declared` names. Lines naming one are set aside and named, never silently dropped.
- **Order is the `## Priority` section of `docs/ROADMAP.md`**, not opinion. `priority
  list` is the read; an empty queue means `pick` falls through to the lowest ready id.
  `priority add` / `priority drop` are the doors.
- **The read BEFORE an add is `delivered <block> --near "<the sentence you would file>"`.**
  It ranks that block's nearest deliveries against what you are about to propose, which is
  the duplicate question asked _before_ an id is spent.
- **Adding is `add --block <x> --symptom "…" --why "…" --section "…"`.** The `§MOD<n>`
  pointer is derived. A line whose section is missing is a `ref.unresolved` finding, so
  file both halves in the one transaction. `budget` prices every field first.
- **The next id is `roadkeep next-id`** — it scans every governed file and never fills a
  gap. Retired ids are never reused.
- **Keep a task line terse** — symptom (what does not work, never a fix name) + one
  sentence of why + the pointer. The reasoning belongs in the section.

## The two lists that bind a proposal

- **`non-goal list`** — every entry binds. A proposal a non-goal forbids is not filed.
  Where one merely _bounds_ a line without forbidding it, the gate says so
  (`non-goal.reaches`) and the answer is recorded by quoting that lead in the line's own
  design section.
- **`criterion list`** — what would finish each block. **A constraint the project must
  keep obeying usually belongs here, not in `DECISIONS.md`**: a criterion survives every
  ship, is never deleted, and is what `ship --checked <lead>` verifies.

## Shipping, and the section it deletes

`ship <id> --why "<what now works>"` writes the ledger entry, clears the roadmap line and
**deletes the design section**, in one transaction or none. That deletion is correct, but
it is the last moment anything in that section can be saved.

**Read the section before you ship it**, and ask of each paragraph: _does this stop being
true when the code moves?_

| Answer                                                                      | Door                                                                                                               |
| --------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------ |
| It explains **this module**                                                 | `--recorded-in <path>` — move the prose into that file's header comment. **This is the common case.**              |
| It is a rule the project must keep obeying                                  | usually not a ship flag at all — `criterion add` or `non-goal add`, which are never deleted                        |
| It is a constraint with a rejected alternative, belonging to no single file | `--decides "<the constraint>"`, plus `section add <id> --role decisions` for what was weighed — `budget` prices it |
| You read it and it had gone stale                                           | `--superseded-design "<what it was wrong about>"`                                                                  |
| Nothing survives                                                            | ship plainly — most ships are this                                                                                 |

**Never copy a section into a second file.** **A design section may carry its own
instruction** — `On ship: --decides "…"` or `On ship: --recorded-in <path>`. Honour it,
revised against what was actually built.

## Everything else

- **Status lives in exactly one file.** If a marker anywhere disagrees with the roadmap, the
  roadmap wins.
- **A pause is `defer <id> --reason "…"`, not `retire`.** Retiring is terminal: the id cannot
  come back, the resolver reads the dep as never, and the section is deleted.
- **A false claim is `restate <id> --symptom "…"`**, which keeps the id, the deps, the marker
  and the design — not `retire` plus `add`.
- **`lint` reports and `repair` spends the report** — every finding names the command that
  closes it, and `explain <code>` says what a code means before you guess.
