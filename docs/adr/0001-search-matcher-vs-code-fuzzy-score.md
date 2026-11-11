---
date: 2026-11-11
status: accepted
area: action search
---

# The search scores terms with a port of VS Code's fuzzyScore

## Context

The registry's search ranks actions through one seam, a matcher scoring one folded term against one folded text, inside a pipeline the matcher does not own: folding, a split into terms, a typo pass, field groups, tiers read from the match ranges, then frecency. The substring matcher it shipped with found neither abbreviations (`ps` for Pause Simulation) nor a term with letters left out.

Three candidates were live: a C# port of fzf's V2 algorithm, a port of VS Code's `fuzzyScore`, and the substring matcher as the baseline. No NuGet package qualified: none returned match positions on a framework the game's Mono loads and was maintained. A bake-off test ran each through the real pipeline over the game's labels in the 12 locales, against the checked-in case file, and timed it against the baseline. Share of cases ranking an expected label first, on the 379 cases after the fluent-reader review:

| Candidate                 | First | Top 3 | Time vs substring |
| ------------------------- | ----- | ----- | ----------------- |
| Substring                 | 69.9% | 70.7% | 1.00×             |
| fzf V2                    | 86.8% | 87.9% | 1.41×             |
| VS Code, weak first match | 87.1% | 88.4% | 1.47×             |

The case file is also what the pipeline was tuned on, so the ranking rules were checked on a held-out set: queries generated mechanically from every label, scored by first-rank share. VS Code without a tie-break 64.5%; with the shorter title breaking a score tie 65.8%; with the shorter title placed before the score 64.9%; fzf with the tie-break 64.7%; VS Code without the description rule 61.6%. The generator is not in the repo.

## Decision

The registry's default matcher is `VsCodeActionMatcher`, VS Code's `fuzzyScore` run with its option that lets a term's first character land inside a word (`tor` finds `constructor`), as without it every run inside a word misses, worst on CJK. It led fzf on the case file and on the generated queries, abbreviations most, within the speed gate of five times the baseline.

Three ranking rules around it were settled on the same measurements:

- A description is hit only by a run of the term, which the matcher then scores: letters scattered across prose are chance, and such hits filled the row cap and kept the typo pass from running.
- At equal group, tier, field, frecency and score, the shorter title ranks first, so a whole label ranks above the longer ones opening with it.
- An initials tier sits between prefix and word starts: one character on the start of every word of the text, a word opening on a letter, a mark or a number. It ranks `ps` on Pause Simulation above Post Service Coverage, which VS Code's score preferred by one character of gap. An apostrophe joins an elided article to its word, so `pi` and `pdi` are both initials of Panneau d'informations: on the generated queries, Latin initials ranked first 67.2% of the time against 66.6% without the join, and no case moved. A lone letter after an apostrophe, such as a possessive's s, is not a word.

A Korean consonant typed alone matches a syllable that opens on it, while a syllable typed in the term matches only itself, so `ㅂㄷㅈ` finds 불도저 and the tiers rank a word's initial consonants above a run of them inside a word. The rule sits in the comparison of one character of the term with one of the text, which every matcher behind the seam applies, so a rematch candidate must apply it too. A cluster an IME composes from two consonants typed with no vowel, `ㄳ` from `ㄱ` then `ㅅ`, folds into those two consonants.

## Consequences

- The fzf port and the bake-off test are deleted; git history keeps both for a rematch. A rematch reruns the bake-off on the case file and on generated queries, since the case file alone rewards tuning to it.
- Two rules pass the case file and lose elsewhere, and each must stay out:
  - The shorter title placed before the score fixes initials like `ps` but loses on the generated queries, costing word prefixes and consonant skeletons.
  - An initials tier without the one-character rule reads `econ` on Electricity Consumption (`e` and `con`) as initials, above `econ` inside City Economy.
- A typo hit ranks below every clean hit of its group, a scattered one included. A term whose label only the typo pass reaches therefore loses to letters scattered by chance across another label: de `laerm` for Lärm, pl `metro` for metra, ru `стата` for статистика. Placing the typo tier above the scattered one fixed `laerm` alone and broke eight cases, as a letter left out (`buldozer`) is itself a clean scattered hit. No rule found separates a chance scatter from an intended one without tuning to the case file.
- `THIRD-PARTY-NOTICES.md` carries VS Code's MIT notice and ships beside the DLL.
