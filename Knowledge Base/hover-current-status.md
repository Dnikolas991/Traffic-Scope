# Hover Current Status

This note records the current stable hover baseline after the recent road/building hover experiments.

## Current Priority

The current implementation prioritizes:

- tool stability
- predictable selection behavior
- avoiding freezes

It does not currently prioritize full vanilla visual parity.

## Road Hover

Current stable behavior:

- Transit Scope still owns the selection-mode tool flow.
- The hovered road entity receives `Highlighted + Updated`.
- `OverlaySystem` still draws the mod's custom road hover overlay.

Result:

- road hover remains visible and stable
- the vanilla light-blue road mask is still not reproduced

## Building Hover

Current stable behavior:

- Transit Scope still owns hit resolution for buildings
- building-related entities are expanded by the mod
- the collected original-entity set receives `Highlighted + Updated`

Result:

- the main building and part of its composition can be covered
- full vanilla building hover coverage is still not reproduced

## Confirmed Failed Routes

The following routes are already known to be insufficient or risky:

- switching the whole tool flow to `DefaultToolSystem`
- simplified helper-definition creation
- isolated reflection calls to partial vanilla hover methods
- extending only relation walking and expecting vanilla-equivalent coverage

Observed failure modes:

- the Transit Scope tool failed to open correctly
- visuals became incorrect or incomplete
- old hover state was not cleaned correctly
- the game could freeze

## Implementation Constraint

If hover work is resumed later, it must start from the reverse-engineering notes:

- `Hover_Highlight.md`
- `hover-building-effect-research.md`

Future implementation should avoid further partial simulation attempts.
