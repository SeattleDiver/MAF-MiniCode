# Slide Deck Rebuild — Instructions

These instructions replace a tutorial course's lesson slide decks (`.pptx`) with decks that present each lesson's ideas visually. Bad decks copy the lesson's prose onto slides. Good decks keep each slide to a headline and a visual, and move the full prose into the speaker notes.

They apply to any course repository built the same way:
- a **syllabus** file that is the single source of truth for lesson numbering, titles, topics and labs;
- one **lesson `.md` per lesson folder**, with the deck stored next to it;
- optional **Phase or unit Capstones** that consolidate a run of lessons.

Read the repository's own instructions file (`CLAUDE.md` or equivalent) first. Where it conflicts with this file, it wins.

---

## The job, per lesson

Work **one lesson at a time, in course order**, and stop for review after each batch. Work out the batches from the syllabus, ending each batch at a Phase boundary or about every 5–10 decks. Keep the batch list and its status in a short note at the top of the working copy of this file.

For each lesson:

### Step 1 — Align the lesson with the syllabus

Compare the lesson `.md` with its section in the syllabus, which wins on any conflict. Check:

- **Title:** the lesson's file name and H1 match the syllabus title exactly. For a lesson split into parts, use the part title.
- **Topics:** every topic in the syllabus section is taught somewhere. For a split lesson, check across all of its parts. A topic that no part teaches is a finding.
- **Delivered as an exercise:** topics the syllabus delivers as an exercise appear in the lesson's Exercise, not in its code.
- **Lab:** the lesson's Expected Output demonstrates the syllabus lab.
- **Structure:** the lesson follows the section order the repository's instructions require, and its prerequisites name the starting point.
- **Scope:** the lesson teaches nothing the syllabus does not name.
- **Internal consistency:** the lesson's claims agree with its own code and with the later lessons it cites. Check type names, the projects that own each type, counts, file paths, and labels for split lessons.

**Report the findings and let the author decide** before any lesson edit. A gap can be closed either by editing the lesson or by trimming the syllabus. If a lesson is edited, follow the repository's verification rules for lessons, such as code blocks matching their files, and line budgets.

### Step 2 — Build the deck

Build a new deck that mirrors the major concepts in the lesson **as it stands**. Overwrite the old deck if there is one.

### Step 3 — QA, then record it

Run the checks under **QA**. If the repository keeps a progress tracker for its videos or decks, mark the deck done there and change nothing else in that file.

---

## What every deck contains

| # | Slide | Source in the lesson |
|---|---|---|
| 1 | **Title — what we'll learn.** Lesson number and title, Phase, and 3–5 short "you'll learn" points. Also shows *Starts from* and *Finishes as*. | Overview, Prerequisites |
| 2 | **Setup**, only if this lesson changes setup (a new package, a new environment variable). Skip it otherwise. | Setup |
| 3… | **One slide per major concept.** Usually one per bolded lead-in in Core Concepts. The slide title is the lead-in; the body is a diagram or 2–3 short lines. | Core Concepts |
| … | **Exercise**: the task and its acceptance criteria, shortened. | Exercise |
| … | **Expected Output**: what success looks like, as a short console excerpt. | Expected Output |
| last | **Summary — what we learned and where it goes.** Each concept from this lesson, mapped to the later lesson that builds on it, plus the Capstone or milestone it ships in. | The whole syllabus |

**Merge repeated ideas instead of making a slide for each mention.** Lessons often restate an idea in Core Concepts, Walkthrough and Expected Output, or state two closely related ideas back to back.

- **Before laying out a deck, list its concepts and look for overlap.** When two candidate slides would say the same thing, or show the same diagram with the same element highlighted, merge them. Otherwise give the second one a really different angle and layout. For example, show a boundary as a flow, then show its consequence as a before/after.
- **Material the lesson repeats goes into the speaker notes** of the one slide that covers it.
- **A "Lesson N said X; now the code agrees" callback** belongs in the notes, not on a new slide.
- **When reviewing the rendered deck,** flip through it in order and check that no two consecutive concept slides look alike at a glance.

**Summary relationships must come from sources, not invention.** Use the lessons' own forward references and the later lessons' syllabus topic lists. Name a later lesson only when its syllabus section or lesson actually depends on the concept. Verify each one by searching the later lesson, and note where it was found.

### Content rules

- **Labels:**
  - Title-slide kicker: `Lesson 5a  ·  Phase 1`, or for a Capstone `Phase 1 Capstone  ·  v0.1`.
  - Footer: `<Course name>  ·  Lesson 5a — <Title>`.
  - For a split lesson, always cite the exact part (`5a`), never the bare number.
- **Setup slide placement:**
  - When Setup changes nothing, there is no Setup slide. Any version or currency note goes in the title slide's notes.
  - Setup may follow the first concept slide when the "why" has to come first.
- **Several exercises, one slide:** condense two or three exercises onto one Exercise slide, with one or two criteria each. The full wording of every exercise goes in the notes.
- **A lesson without code or exercises** has no Setup, Code or Exercise slides. Mirror the sections the lesson actually has.
- **Captured output:**
  - Console excerpts may be rewrapped to fit, at about 62 characters per line at size 10 when the panel has side items.
  - They may be trimmed with "...", and stale absolute paths shortened to `...\FolderName`.
  - Never change the words. Say in the notes what was cut.
- **Illustrative output:** when a lesson has no output to show but a console panel is still the right visual, make its first line `# An illustration, not captured output`. Flag it in the batch report so the author can decide whether it stays.
- **Lesson slips are not copied:** where the lesson has a wrong label, type name or project, the slide and notes use the correct form. The slip is reported as an alignment finding.
- **Summary slide:**
  - At most 6 rows, and only rows with a verified downstream lesson. Fewer rows is fine.
  - Never give two rows the same lesson and the same idea.
  - "Ships in" names the Capstone or milestone. A Capstone deck names its own version.
- **Capstone decks:**
  - Open with a "where we are" grid of the Phase's lessons.
  - The summary maps each thing the version cannot do to the later lesson that adds it.
  - A gap that no later lesson closes goes in the notes, not on the summary.
  - The deck may run to about 14 slides.

---

## Design rules (course-wide, so every deck looks like the same series)

- **One theme for every deck in the course.** Use the same palette, fonts, title-slide layout and summary-slide layout throughout. A shared generator enforces this (see **Tooling**).
- **Palette:** one dominant colour taken from the course's subject, such as the framework's brand colour, plus a sharp accent used sparingly. Title and summary slides are dark; content slides are white.
- **One visual motif:** for example, an icon in a filled circle on every card.
- **No prose paragraphs on slides.** A slide holds a headline, a diagram, or at most about 3 short lines. The lesson's full paragraph for that concept goes in the **speaker notes**.
- **No code on slides.** Code is presented in the editor; the deck is the away-from-the-editor part of the video. A console excerpt on the Expected Output slide is fine, and so are type and member names in body text.
- **Every slide has a visual:** a flow diagram, comparison columns, an icon grid or a console panel. No title-plus-bullets slides.
- **Avoid AI-slide tells:** no accent lines under titles, no colour bars or edge stripes, no centred body text, no cream backgrounds.
- **Fonts:** Cambria for headings, Calibri for body text and Courier New for console text. These ship with Office and render true to width.
- **Size:** about 8–12 slides per lesson. A Capstone deck may run longer.

### Builders and text budgets

The generator provides a fixed set of slide builders. Use only these:

| Builder | What it is |
|---|---|
| `title` | Title slide |
| `flow` | Chain of 3–5 cards joined by arrows. Use it for a sequence or a cause and effect, never a set of alternatives. |
| `compare` | Two cards side by side: before/after, with/without, do/don't. Either card may hold a console snippet. |
| `hub` | One centre card with 3–4 spokes |
| `grid` | 3–6 cards |
| `console` | A console panel, optionally with up to 3 side items |
| `exercise` | The task card plus its criteria |
| `summary` | The closing slide |

Text budgets on a 10" × 5.625" slide:

- A card about 2.9" wide holds roughly 3 lines of 12pt.
- A flow node label holds about 20 characters at 15pt.
- A hub spoke label holds about 16 characters at 15pt.
- Labels inside one slide must all differ.

The generator should protect layouts automatically:

- slide titles, title-slide folder names and exercise file paths shrink to fit;
- flow nodes grow to fit their text;
- long hub labels wrap to two lines.

PowerPoint ignores a zero-width space as a break point, so force a break inside a long identifier with `\n`.

If a lesson needs a layout no builder provides, raise it before adding a builder.

---

## Naming and location

- **Location:** the deck lives in its lesson folder, next to the lesson `.md`, as a lesson asset.
- **File name:** the lesson's file name with `.pptx` in place of `.md`.
- **Edge case:** when a lesson title itself ends in `.md` (for example "…with AGENTS.md"), that `.md` is the extension. Swap it for `.pptx` and keep whatever name an existing deck already uses.
- **Generator source:** keep it in the repository's untracked working folder (typically `docs/slides/`), and never commit it unless the repository says otherwise.

---

## Tooling

- **Generator:**
  - A Node script using `pptxgenjs`: a shared `theme.js` that holds the palette, fonts and builders, plus one content file per deck that holds the slides and their speaker notes.
  - Build with `node build.js <deck>`.
  - Reuse an existing course's generator when one is available, and keep its theme so the courses match.
- **Packages:**
  - Install `pptxgenjs react-icons react react-dom sharp` in a scratch folder, and point `NODE_PATH` at its `node_modules`.
  - Icons come from `react-icons/fa6`. Confirm an icon exists before using it.
- **Python** (Windows): use the full path to the installed `python.exe`, because bare `python` may be the Microsoft Store stub. Install `markitdown[pptx]` and `defusedxml`.
- **Validator:** `validate.py` in the pptx skill's `scripts/office/` folder.
- **Rendering for visual QA:**
  - Where LibreOffice is not installed, render through PowerPoint COM:
    ```powershell
    param([string[]]$Decks, [string]$OutRoot)
    $app = New-Object -ComObject PowerPoint.Application
    foreach ($d in $Decks) {
      $out = Join-Path $OutRoot ([IO.Path]::GetFileNameWithoutExtension($d))
      New-Item -ItemType Directory -Force $out | Out-Null
      $p = $app.Presentations.Open($d, $true, $false, $false)
      $p.Export($out, "PNG", 1600, 900); $p.Close()
    }
    $app.Quit()
    ```
  - Pass several decks as `-Decks a.pptx,b.pptx`. A space-separated list binds only the first deck.
- **Contact sheets:** for a fast first pass, tile six rendered slides per image with Pillow, at 790×444 each in a 2×3 grid. Re-read single slides at full size only where something looks wrong.

## QA

- `validate.py <deck>.pptx` passes.
- `markitdown <deck>.pptx`: the content is all present, in order, with no typos and no placeholder text.
- **Render and inspect every slide.** Check for:
  - text overflow;
  - overlapping shapes;
  - low contrast;
  - uneven gaps;
  - margins under 0.5".

  Fix the generator or the content file, then re-render.
- No two concept slides repeat the same idea or look the same: same layout, same highlighted element, same message.
- Every claim on a slide is backed by the lesson or the syllabus. Every later-lesson reference on the summary slide names a lesson that actually builds on the concept.
- If visual QA could not run, say so. Do not imply it did.

## Batch workflow

- **Parallel drafting:** forks or sub-agents may draft content files in parallel, a pair of lessons each. Each one:
  - writes only its own deck content files;
  - checks that its deck builds;
  - reports alignment findings with lesson line numbers, and where each summary reference was verified.
- **What drafting agents never do:** edit the shared theme, edit another deck's file, touch the progress tracker, or do visual QA.
- **One owner** renders every deck, reviews the contact sheets, fixes layout in the theme and content in the deck files, and writes the batch report.
- **Any change to the shared theme** means rebuilding and re-rendering every existing deck, not just the current batch.
- **The batch report** gives, for each lesson:
  - alignment findings that need the author's decision, with line numbers;
  - slide counts;
  - places where the deck departs from the lesson: trimmed output, condensed exercises, illustrative content;
  - out-of-scope findings, in one line each.
