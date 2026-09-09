# MiniCode — Video Plan

**27 videos: 23 lessons and 4 Phase Capstones**, across 5 Phases. Present them strictly top to bottom — each one starts from the folder the previous one finished in.

## How to record any video

1. **Open the folder in the "Start from" column.** It is a complete, runnable solution — nothing to delete, nothing to strip out.
2. Present **Core Concepts** away from the editor (that section is written to work as a slide).
3. Build up to what the **"Finishes as"** folder contains. That folder is the answer key.
4. **Expected Output** in each lesson is real captured output, not illustrative — you can run it.

Each lesson is under 300 lines and shows at most 125 lines of C#. Anything beyond that budget is an **Exercise** for the viewer.

---

## Phase 1 — Foundations & Read-Only Agent → **Phase 1 Capstone (v0.1)**

Videos 1–8. Everything here rolls up into **`Phase1-Capstone-RepositoryExplorer`**.

| # | Video | Start from | Finishes as | Lesson | Deck |
|---|---|---|---|---|---|
| 1 | Understanding Coding Agents | *(no code)* | `Module01-UnderstandingCodingAgents` | 176 | ✅ |
| 2 | Microsoft Agent Framework Fundamentals | *empty folder* | `Module02-MicrosoftAgentFrameworkFundamentals` | 198 | ✅ |
| 3 | Designing the MiniCode Solution | Module 2 | `Module03-DesigningTheMiniCodeSolution` | 299 | ✅ |
| 4 | Building the Workspace Sandbox | Module 3 | `Module04-BuildingTheWorkspaceSandbox` | 218 | ✅ |
| 5 | Giving the Agent Read Access — The Interception Seam | Module 4 | `Module05a-TheInterceptionSeam` | 231 | ✅ |
| 6 | Giving the Agent Read Access — Reading Files | Module 5a | `Module05b-ReadingFiles` | 183 | ✅ |
| 7 | Repository Discovery | Module 5b | `Module06-RepositoryDiscovery` | 245 | ✅ |
| **8** | **★ Phase 1 Capstone — Repository Explorer (v0.1)** | Module 6 | `Phase1-Capstone-RepositoryExplorer` | 165 | ✅ |

**What v0.1 does:** reads an unfamiliar .NET repository and explains it. Cannot change anything.

---

## Phase 2 — Context & Planning → **Phase 2 Capstone (v0.2)**

Videos 9–13. Everything here rolls up into **`Phase2-Capstone-PlanningAgent`**.

| # | Video | Start from | Finishes as | Lesson | Deck |
|---|---|---|---|---|---|
| 9 | Project Instructions with AGENTS.md | Phase 1 Capstone | `Module07-ProjectInstructionsWithAgentsMd` | 241 | ✅ |
| 10 | Context Management — Budget and Search Before Read | Module 7 | `Module08a-BudgetAndSearchBeforeRead` | 235 | ✅ |
| 11 | Context Management — Working State and Compaction | Module 8a | `Module08b-WorkingStateAndCompaction` | 245 | ✅ |
| 12 | Planning and Task State | Module 8b | `Module09-PlanningAndTaskState` | 219 | ✅ |
| **13** | **★ Phase 2 Capstone — Planning Agent (v0.2)** | Module 9 | `Phase2-Capstone-PlanningAgent` | 154 | ⬜ |

**What v0.2 adds:** reads the repository's own conventions, plans before acting, survives a long session. Still cannot change anything.

---

## Phase 3 — Taking Action → **Phase 3 Capstone (v0.3)**

Videos 14–19. Everything here rolls up into **`Phase3-Capstone-AutonomousFixer`**.

| # | Video | Start from | Finishes as | Lesson | Deck |
|---|---|---|---|---|---|
| 14 | Safe File Editing | Phase 2 Capstone | `Module10-SafeFileEditing` | 209 | ✅ |
| 15 | Shell Command Execution — Running a Process Safely | Module 10 | `Module11a-RunningAProcessSafely` | — | ⬜ |
| 16 | Shell Command Execution — The Command Allow List | Module 11a | `Module11b-TheCommandAllowList` | — | ⬜ |
| 17 | The Autonomous Coding Loop | Module 11b | `Module12-TheAutonomousCodingLoop` | — | ⬜ |
| 18 | Automated Testing and Self-Correction | Module 12 | `Module13-AutomatedTestingAndSelfCorrection` | — | ⬜ |
| **19** | **★ Phase 3 Capstone — Autonomous Fixer (v0.3)** | Module 13 | `Phase3-Capstone-AutonomousFixer` | — | ⬜ |

**What v0.3 adds:** edits files, runs commands, loops until the build and tests pass. **Nobody is asked permission yet** — that is deliberate, and Module 15 is the answer to it.

---

## Phase 4 — Governance, CLI & Delivery → **Phase 4 Capstone (v0.4)**

Videos 20–26. Everything here rolls up into **`Phase4-Capstone-DeveloperCli`**.

| # | Video | Start from | Finishes as | Lesson | Deck |
|---|---|---|---|---|---|
| 20 | Git Integration | Phase 3 Capstone | `Module14-GitIntegration` | — | ⬜ |
| 21 | Human Approval and Safety | Module 14 | `Module15-HumanApprovalAndSafety` | — | ⬜ |
| 22 | Developer CLI — Dispatch and Informational Commands | Module 15 | `Module16a-DispatchAndInformationalCommands` | — | ⬜ |
| 23 | Developer CLI — Plan and Review Modes | Module 16a | `Module16b-PlanAndReviewModes` | — | ⬜ |
| 24 | Observability | Module 16b | `Module17-Observability` | — | ⬜ |
| 25 | Packaging MiniCode | Module 17 | `Module18-PackagingMiniCode` | — | ⬜ |
| **26** | **★ Phase 4 Capstone — Developer CLI (v0.4)** | Module 18 | `Phase4-Capstone-DeveloperCli` | — | ⬜ |

**What v0.4 adds:** Git awareness, human approval on dangerous operations, slash-command operating modes, tracing, and `dotnet tool install -g MiniCode`.

---

## Phase 5 — Capstone

| # | Video | Start from | Finishes as | Lesson | Deck |
|---|---|---|---|---|---|
| **27** | **Capstone Project (v1.0)** | Phase 4 Capstone | `Module19-CapstoneProject` | — | ⬜ |

No new code — MiniCode completes a real development task end to end, autonomously.

---

## Which Modules belong to which Capstone

| Capstone | Version | Modules it consolidates | Videos |
|---|---|---|---|
| Phase 1 — Repository Explorer | v0.1 | 1, 2, 3, 4, 5a, 5b, 6 | 1–7 → 8 |
| Phase 2 — Planning Agent | v0.2 | 7, 8a, 8b, 9 | 9–12 → 13 |
| Phase 3 — Autonomous Fixer | v0.3 | 10, 11a, 11b, 12, 13 | 14–18 → 19 |
| Phase 4 — Developer CLI | v0.4 | 14, 15, 16a, 16b, 17, 18 | 20–25 → 26 |
| *(none — Module 19 is the course capstone)* | v1.0 | 19 | 27 |

A Capstone is **not** a new topic. It consolidates the Phase, tightens the seams between its Modules, demonstrates the tool end to end, and is honest about what it still cannot do. It is also the only place the version number moves.

**Four Modules are split across two videos each** because their syllabus topic list will not fit in one: **5, 8, 11, 16**. Both parts share the Module's number, scope and Phase — the 19-Module structure is unchanged.

---

## Where things stand

**Built and verified: videos 1–14** (through Module 10). Every folder builds at 0 warnings / 0 errors; every lesson's code blocks match its own folder byte-for-byte.

**Remaining: videos 15–27** (13 units).

**Decks:** 13 of 14 built units have one. Missing: **Phase 2 Capstone (video 13)**.

---

## Notes for recording

- **The `verification/` folder is not part of the course.** It is a private test harness so the presented code can be trusted. No test code appears in any lesson, and no Module folder contains a test project.
- **`AGENTS.md` at a Module folder root is a lab fixture**, from Module 7 onward — it is what MiniCode discovers when pointed at its own folder.
- **The console banner is fixed for the whole series** (`MiniCode. Type 'exit' to quit.`) so it never needs re-recording. The version line above it changes only at a Capstone.
- **`OPENAI_API_KEY` must be set** for any video from 2 onward. Module 4 is the one lesson whose demonstration needs no key.
