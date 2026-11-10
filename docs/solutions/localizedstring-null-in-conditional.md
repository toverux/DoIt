---
date: 2026-11-10
area: DoIt/Actions
symptoms:
  - 'An action row shows an empty category instead of falling back to its source name'
tags: [csharp, localizedstring, implicit-conversion, nullable]
---

# A null branch in a LocalizedString conditional becomes an empty string

## Problem

`cond ? LocalizedString.Id(x) : null` assigned to a `LocalizedString?` property never yields null, so a `??` fallback on it never fires and the row shows empty text. No warning, no error.

## Root cause

`LocalizedString` converts implicitly from `string` (decompile `Game.UI.Localization/LocalizedString.cs:124`, `implicit operator LocalizedString(string id) => Id(id)`). The bare `null` converts through it, so the conditional's type is `LocalizedString`, not `LocalizedString?`, and its false branch is `LocalizedString.Id(null)`: a value with neither id nor value, which resolves to `""`.

## Fix

Type the null branch: `cond ? LocalizedString.Id(x) : (LocalizedString?) null` (`DoIt/Actions/RegistrationActionSource.cs:47-50`).

## Prevention

Cast the null branch of any conditional producing a `LocalizedString?`; the same holds for any struct with an implicit conversion from a reference type.
