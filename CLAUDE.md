Act as an expert C# AI Developer and Technical Content Creator - use the /tutorial-instructor skill if available. I am creating a YouTube video tutorial series titled "Building a Mini Claude Code–Style Agent with Microsoft Agent Framework (MAF)" — a single-agent coding-agent course (capstone: MiniCode) built on Microsoft Agent Framework, the successor to Semantic Kernel and AutoGen.

`syllabus.md` in this project folder is the single source of truth for Module numbering, titles, topics, labs, and scope. Every Module I ask you to generate must match its corresponding Module section in syllabus.md exactly. If anything in this file ever conflicts with syllabus.md, syllabus.md wins — flag the conflict and ask before proceeding.

---

# THIS IS A VIDEO TUTORIAL, NOT A PRODUCT

Read this section before anything else. It overrides any instinct toward completeness.

Every rule below that says "include X" is bounded by the budgets in this section. A Module that is thorough but too long has failed, not succeeded.

**The budgets are hard limits, not targets:**

| | Limit |
|---|---|
| Lesson `.md` length | **under 300 lines** |
| C# shown in one lesson | **at most 125 lines** — what I can present at a reasonable pace on camera |
| Whole MiniCode solution, when finished | **under ~2,500 lines of C#** across **under ~50 files** |
| Test code shown to viewers | **none — see rule 25** |

**Scope is the syllabus's topic list and nothing more.** Cover the topics that Module's syllabus section names, then stop. Do not harden beyond what is named, do not add cases the syllabus does not mention, do not model a result type where a string or a small record will do. If a Module feels thin against the topic list, it is probably right.

**When code exceeds the 125-line budget, the surplus becomes an exercise** (see rule 11). Do not solve everything on camera. The viewer learning by implementing is the point, not a compromise.

**Every lesson demonstrates the happy path.** Expected Output shows the thing working. Failure modes and defensive cases are worth a sentence in Core Concepts where they explain a design choice, but the demonstration is the success case, not a tour of everything that can go wrong.

**Prefer the smallest thing that teaches the idea.** A tutorial's code is illustrative. Two types beat thirteen. A returned string beats a result hierarchy. An optional parameter beats an options class.

---

# REPOSITORY LAYOUT

**Every Module folder is a complete, standalone, runnable solution** — the state of MiniCode at the *end* of that lesson. There is no solution at the repository root.

```text
ModuleNN-TitleInPascalCase/
    ModuleNN - Title.md              the lesson
    MiniCode.slnx                    a complete solution, at end-of-lesson state
    src/MiniCode.Cli/                terminal interface
    src/MiniCode.Agent/              MAF configuration, agent loop, context, planning
    src/MiniCode.Tools/              tools exposed to the model
    src/MiniCode.Workspace/          repository boundaries and filesystem access
    src/MiniCode.Infrastructure/     shell, Git, config, approval, telemetry

PhaseN-Capstone-TitleInPascalCase/
    PhaseN Capstone - Title.md
    MiniCode.slnx, src/             same shape; the Phase's consolidated state

verification/                        OUR harness — never presented, never shipped
    MiniCode.Verify.slnx
    MiniCode.Tests/

docs/                                reference material, not lesson content
syllabus.md, CLAUDE.md               repository root
```

1. **Folders are cumulative snapshots, and each one stands alone.** Open `ModuleNN` in VS2026 with no other folder present and it restores, builds and runs. The viewer never retypes earlier work; I never delete anything to get to a starting point.
2. **The recording workflow this exists to serve.** To record Module N, open **Module N−1's** folder — already complete, nothing to strip out — and build up to what Module N's folder contains. Module N's lesson names that starting folder in its Prerequisites. Module 2 starts from an empty project.
3. **Generating Module N means: copy Module N−1's solution into `ModuleNN-Title/`, apply this Module's changes, then write the lesson.** Files this Module does not touch stay byte-for-byte identical to the previous folder, so a viewer diffing two folders sees only what the lesson explains.
4. **Module folders never contain a test project.** Neither do Phase Capstones. See rule 25. A Module folder may hold the slide deck for that lesson and one small fixture file if its lab needs one; those are lesson assets, not part of the solution.
5. Module folders are named `ModuleNN-TitleInPascalCase` (zero-padded, hyphen separator, no underbars). Phase Capstone folders are named `PhaseN-Capstone-TitleInPascalCase`.
6. Never leave `bin/`, `obj/`, `.vs/` or `TestResults/` anywhere.
7. **Duplication between folders is intended, not waste.** It is what makes each lesson standalone. It stays affordable only because the finished solution is under ~50 files — which is what the budgets above are protecting.

---

# STRICT RULES FOR ALL C# CODE

8. Target Microsoft Agent Framework (`Microsoft.Agents.AI` and related packages) on .NET 10. Before writing code for any Module, verify current package names, namespaces, and API signatures — MAF is under active development. Flag breaking changes you find versus older samples.
9. Use OpenAI exclusively, via the first-party `Microsoft.Extensions.AI.OpenAI` package's `IChatClient` implementation. Verify that package's current version and API surface before writing code.
10. The chat model is always `gpt-4.1-mini`. This course does not use embeddings, RAG, or vector memory — Module 8's context work is search-and-summarize based.
11. API keys always come from environment variables (e.g. `OPENAI_API_KEY`). Never hardcode a key. Exactly one type reads the environment; nothing else calls `Environment.GetEnvironmentVariable`.
12. Skip presenter dialog and pleasantries — give me material I can present directly.
13. **Every Module lesson follows this exact structure, and the whole file stays under 300 lines:**
    - **Project Overview** — 2–3 sentences: what we build and why it matters.
    - **Prerequisites** — which prior Modules this assumes, **which folder to open as the starting point** (`ModuleNN-Title/`, or "an empty project" for Module 2), and which Phase Capstone it feeds.
    - **Setup** — only what *changes* this Module (a new package, a new env var). If nothing changes, say so in one line.
    - **Core Concepts** — the idea in plain language, before any code. **Write this so it works as a slide**: short paragraphs or bullets, one idea each, no code. This is the part I present away from the editor.
    - **The Code** — at most 125 lines of C#, being the heart of the Module. Show complete, compiling files where they fit the budget; where a file is larger than the budget, show the method or two that matter and name the file the rest lives in. No ellipses inside anything you do show.
    - **Walkthrough** — step through what was just shown. Brief.
    - **Exercise** — what the viewer implements themselves, with concrete acceptance criteria (stated in prose — never a test file, see rule 25). This is where everything over the 125-line budget goes. Name the file the work belongs in. Where the series itself needs that code later, say which Module supplies it; where it is a stretch beyond what MiniCode requires, the acceptance criteria *are* the specification and no reference implementation is promised — do not grow the solution just to answer an exercise.
    - **Expected Output** — what success looks like, concretely.
14. One public type per file, in its own `.cs` file; enums and interfaces get their own files. **XML doc comments go on every public type, and on members whose purpose is not obvious from the signature — not on every member.** The lesson prose is where explanation belongs; duplicating it in doc comments on trivial members is what turned 9,000 lines of code into 23,000 lines of file.
15. Never use top-level statements. Every type lives in the project namespace.
16. Projects are always named `MiniCode.*`. Never put `ModuleN` or `ModuleN_` in a project, file, class, or namespace name. A Module number may appear in a comment as a stable cross-reference to syllabus.md (`// Module 11 hardens this`) but never in program output, an identifier, or a file-header banner — a banner naming the current Module goes stale the moment a later Module carries the file forward untouched. Where a Module is split, cite the exact part (`Module 5b`), never the bare number. Never promise in a comment that a shape will never change; say what a later Module will do to it.
17. No underscores in any filename or folder name.
18. **Never depend on the future.** A Module's code must not reference, stub, `TODO`, or anticipate anything a later Module introduces. Read ahead only far enough to avoid designing something a later Module must tear down.
19. **Consistency across the series.** The same package is pinned to the same exact version everywhere; `.csproj` property order and `ProjectReference` separators stay uniform; naming and doc style stay uniform; each lesson reads as the next episode of the same series. A viewer diffing two Module folders should see only differences the lessons explain.
20. Consult `docs/MiniCode Build Plan.md` before writing a Module. It records which project owns each capability, what shape it takes, and which later Modules consume it, so a type's shape is chosen once. Keep it updated.

---

# PHASES AND CAPSTONES

21. The course is delivered in five Phases. Each of the first four ends with a Phase Capstone: a full runnable snapshot of the solution, plus a lesson that exercises the Phase's work end to end and states honestly what the tool still cannot do. Capstones introduce no concept absent from syllabus.md.
    - Phase 1 (Modules 1–6) → `Phase1-Capstone-RepositoryExplorer` (v0.1)
    - Phase 2 (Modules 7–9) → `Phase2-Capstone-PlanningAgent` (v0.2)
    - Phase 3 (Modules 10–13) → `Phase3-Capstone-AutonomousFixer` (v0.3)
    - Phase 4 (Modules 14–18) → `Phase4-Capstone-DeveloperCli` (v0.4)
    - Phase 5 (Module 19) is the course capstone itself (v1.0) and needs no separate folder.
    A Capstone lesson may run to 400 lines, since it summarises a Phase. `MiniCodeVersion` moves at a Capstone and nowhere else.
22. **A Module may be split into video-sized parts** — `Module04a`, `Module04b` — when its syllabus topic list genuinely cannot be taught inside one lesson's budget. Parts share the Module's syllabus number, scope, and Phase, so the 19-Module spine and the Phase mapping are unchanged. Folder: `Module04a-TitleFragment`; lesson: `Module04a - Title Fragment.md`. Propose a split before generating it, and never split to evade the budget when trimming scope is the right answer instead.

---

# STRICT RULES FOR ALL DOCUMENTATION

23. Each Module folder contains exactly one lesson `.md`, named `ModuleNN - Title As Written In Syllabus.md`. Each Phase Capstone folder contains one `PhaseN Capstone - Title.md`. Where a Module title already ends in `.md`, that suffix serves as the extension — never a doubled `.md.md`.
24. Every code block that shows a complete file must be preceded by a heading containing that file's exact repo-relative path in backticks, e.g. ``### `src/MiniCode.Workspace/Workspace.cs` ``. Excerpts shorter than a whole file must say which file they come from.
25. Reference material that is not lesson content lives in `docs/`. `syllabus.md` and this file stay at the repository root.
26. No underscores in any filename or folder name.

---

# VERIFICATION

27. **Unit tests are ours, not the viewer's.** They exist so I can trust the code I present; they are never part of the course.
    - Tests live in `verification/MiniCode.Tests/`, referenced only by `verification/MiniCode.Verify.slnx`, which points at the most recent Module folder. A Module folder’s own `MiniCode.slnx` contains only that Module’s `src/` projects.
    - **Never show test code in a lesson.** No test file appears under a heading, in a code block, or in an inventory. Never cite a test as evidence to the viewer; if a claim matters, demonstrate it in Expected Output instead.
    - Module folders and Phase Capstones contain `src/` only. No `verification/`, no test project, ever.
    - Keep the harness small — it is a gate, not a deliverable. A handful of tests per Module covering what would be embarrassing to get wrong on camera is enough.
    - An **Exercise** may state acceptance criteria in prose ("a path containing `..` should be refused"). It must not hand the viewer a test file.
28. Before declaring a Module done:
    - Delete every `bin/`, `obj/`, `.vs/`, `TestResults/`, then build the Module folder’s **`MiniCode.slnx`** — must be **0 warnings, 0 errors**. An incremental build hides warnings, so the build must be clean. Analyzer warnings count; fix call sites rather than suppressing.
    - Build and run `verification/MiniCode.Verify.slnx` with plain `dotnet test` — all passing. Never `--nologo`, which reports "Zero tests ran" with exit code 5 on the .NET 10 SDK.
    - Confirm every complete-file code block in the lesson matches that file **in this Module's own folder**, byte-for-byte. Each folder is its own snapshot, so this holds permanently — a later Module changing a file does not affect an earlier lesson.
    - **Any time a Module's code is touched again — a fix, a refactor, a restructure, a rename — that Module's `.md` is updated in the same pass and re-verified before moving on.** A lesson is a view of its folder, not a record of what the folder once held. This applies to prose as much as code blocks: file paths, folder names, package versions, counts, the starting-point line, and anything the lesson claims about behaviour. Never leave a Module folder and its lesson out of step, even briefly.
    - Confirm every file this Module did **not** change is byte-for-byte identical to Module N−1's copy. Report the list of files that differ; each one must be something the lesson explains.
    - Confirm the lesson is **under 300 lines** and shows **at most 125 lines of C#**. Report both counts.
    - Confirm the lesson mentions no test file and no test project.
    - Delete build artifacts again.
    Report real numbers. Never claim a build or test result you did not observe.

---

# HOW I WILL PROMPT YOU

29. **"Generate Module N"** — copy Module N−1's solution into `ModuleNN-Title/`, apply this Module's changes, then write `ModuleNN-Title/ModuleNN - Title.md`. Do not generate multiple Modules unless I ask.
30. **"Generate Phase N Capstone"** — copy the preceding Module's solution into `PhaseN-Capstone-Title/`, consolidate it, and write its lesson.
31. **Generate strictly in series order, one unit at a time**: Modules 1–6 → Phase 1 Capstone → Modules 7–9 → Phase 2 Capstone → Modules 10–13 → Phase 3 Capstone → Modules 14–18 → Phase 4 Capstone → Module 19. Never start a unit before the previous one builds clean — each Module is copied from the previous one, so a fault in an unverified predecessor propagates forward through every folder after it.
32. Parallel agents are fine **within** one unit (design, code, lesson, tests) provided one agent owns the merge and the verification. Never run two agents on different units at once. Work that touches no unit in progress — a docs audit, a syllabus revision — may run in parallel if told exactly which files it may touch.
