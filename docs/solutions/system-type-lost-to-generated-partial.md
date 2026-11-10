---
date: 2026-11-10
area: DoIt/Systems
symptoms:
  - "error CS0311: The type 'ActionsUISystem' cannot be used as type parameter 'SystemType' in the generic type or method 'UpdateSystem.UpdateAt<SystemType>(SystemUpdatePhase)'. There is no implicit reference conversion from 'ActionsUISystem' to 'Unity.Entities.ComponentSystemBase'."
  - "'ActionsUISystem' does not contain a definition for 'Results'"
tags: [csharp, entities, source-generator, namespace, systembase]
---

# A system stops being a system once the Entities generator wakes on it

## Problem

Adding `UnityEngine.Time.unscaledTime` to a UI system's `OnUpdate` broke every caller of the system: `UpdateAt<T>` and `GetExistingSystemManaged<T>` refused it, and its members went "missing".

## Root cause

The Entities source generators emit their partial of a system into the global namespace when the file uses a file-scoped namespace, which the `cs2-mod-project` skill documents. The callers then resolve the generated, empty `ActionsUISystem`. Referencing `Time` in the system's body was enough to wake the generator here, with no `SystemAPI` call or schedule site; every system file in this repo uses a file-scoped namespace.

## Fix

Keep the trigger out of the system: the tick reads a `Stopwatch` (`DoIt/Systems/ActionsUISystem.cs:57-59`). Giving the file a block namespace is the other fix.

## Prevention

Dump the generated files with `-p:EmitCompilerGeneratedFiles=true -p:CompilerGeneratedFilesOutputPath=<dir>` when a system's type goes missing, and look for a `<System>__System_*.g.cs` under `SystemGenerator`.
