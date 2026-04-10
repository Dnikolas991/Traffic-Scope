# Cities: Skylines II Hover Building Effect Research

## Scope

- Goal: reverse engineer the vanilla building hover highlight path for mod-compatible reproduction.
- Focus:
  - hover target detection
  - hover state propagation
  - rendering / outline path
  - coverage range: whether highlight resolution expands upward or downward across related entities

## Current Conclusions

### Confirmed

- `Game.Tools.Highlighted` exists as a shared ECS tag for world highlight state.
- `Game.Rendering.OutlinesWorldUIPass` is the vanilla outline rendering path.
- `ToolBaseSystem.GetRaycastResult(...)` reads raycast results but does not itself write hover state.
- `DefaultToolSystem.Update(...)` calls `UpdateDefinitions(...)` during the normal hover/update flow.
- `CreateDefinitionsJob::AddEntity(...)` creates helper definition entities with:
  - `CreationDefinition`
  - `OwnerDefinition`
  - `ObjectDefinition`
  - `IconDefinition`
  - sometimes `NetCourse`
- `GenerateObjectsSystem/CreateObjectsJob::CreateObject(...)` creates helper render objects with `Game.Tools.Temp`.
- `Temp.m_Original` points back to the original source entity.
- `SelectTempEntity(...)` resolves from helper/temp entities back to the real selected entity, so selection resolution is separate from hover preview generation.

### High Confidence Inference

- Vanilla building hover is likely not a direct material swap on the original building entity.
- The default tool path appears to normalize the hovered target, create helper definition entities, generate temp/helper render entities, and let rendering systems drive the final outline effect.

### Open Questions

- Whether the helper/temp render entity also receives `Game.Tools.Highlighted` during ordinary building hover.
- The exact runtime consumer of `RenderingSettingsData::m_HoveredColor`.
- The exact meaning of some internal bit flags observed in batch/instance update code.

## Coverage Range Analysis

### Upward Resolution Is Confirmed

Selection resolution climbs upward from helper or child-like entities to the canonical target:

- `Game.Common.Owner.m_Owner`
- `Game.Objects.Attachment.m_Attached`
- `Game.Common.Target.m_Target`

In `DefaultToolSystem/SelectEntityJob::Execute(...)`, the logic resolves through ownership and attachment relations and then continues climbing through `Owner` multiple times when the current entity is not yet a stable top-level target such as a building or vehicle.

Implication:

- If the raycast or helper path lands on a child piece, icon, attachment, or other intermediate entity, vanilla prefers to resolve upward to a more canonical parent/original entity.

### Downward Expansion Is Confirmed

Hover/helper coverage also expands downward from the canonical target into related child content:

- `Game.Buildings.InstalledUpgrade.m_Upgrade`
- `Game.Objects.Attachment.m_Attached`
- `Game.Objects.Attached.m_Parent`
- `Game.Objects.SubObject.m_SubObject`

In `DefaultToolSystem/CreateDefinitionsJob::Execute(...)`, the system enumerates related entities and calls `AddEntity(...)` for them, including installed upgrades and attachment-related entities.

In `GenerateObjectsSystem/FillCreationListJob`, `CheckSubObjects(...)` traverses `SubObject` buffers and also checks attachment / ownership-related links while assembling the helper creation set.

Implication:

- After the system identifies the canonical hovered building, it fans back downward to cover upgrade pieces, attached parts, and subobjects for hover preview generation.

## Working Model

For ordinary building hover, the most likely coverage model is:

1. Raycast hits some entity belonging to the building composition.
2. The tool path resolves upward to the canonical building/original entity.
3. The tool path expands downward into related upgrades / attachments / subobjects.
4. Helper/temp render entities are generated.
5. Rendering systems produce the final outline/highlight visual.

This means the coverage is not purely upward or purely downward.

It is best described as:

- upward for canonical target resolution
- downward for visual coverage expansion

## Mod Reproduction Guidance

### Minimal Route

- Perform your own hover raycast.
- Resolve to the canonical building entity.
- Add and remove `Game.Tools.Highlighted` on hover enter/leave.
- Verify whether the building directly participates in the vanilla outline path.

### Vanilla-Like Route

- Resolve the hovered entity upward to the canonical building.
- Expand the coverage set downward across related upgrades / attachments / subobjects.
- Create helper definition entities.
- Let vanilla-style temp/helper object generation feed the render path.

### Practical Takeaway

- If you only highlight the exact hit entity, coverage will likely be too narrow.
- If you only walk downward from the hit entity without first canonicalizing upward, you risk highlighting the wrong fragment set.
- A robust mod implementation should first normalize upward, then expand downward.
