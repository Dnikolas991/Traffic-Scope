# Cities: Skylines II Hover Building Effect Research

## Scope

- Goal: reverse engineer the vanilla building hover path precisely enough for a mod-safe implementation.
- Focus:
  - canonical target resolution
  - coverage expansion
  - helper definition structure
  - temp/helper preview generation
  - cleanup lifecycle

## Conclusion Summary

1. The default-tool building hover entry point is:
   - `Game.Tools.ToolSystem::ToolUpdate()`
   - `Game.Tools.DefaultToolSystem::OnUpdate()`
   - `Game.Tools.DefaultToolSystem::Update(JobHandle)`
2. `DefaultToolSystem.UpdateDefinitions(...)` refreshes building helper definitions, but does not directly render the final hover visual.
3. Vanilla building hover first canonicalizes upward, then expands downward.
4. `Highlighted + Updated` can only provide highlight/outline behavior on the original entity set; it does not reproduce the full vanilla building hover coverage.
5. Recreating only the relation graph is insufficient. A vanilla-like result also depends on:
   - the real canonicalization rules
   - the full definition structure written by `CreateDefinitionsJob::AddEntity(...)`
   - downstream `GenerateObjectsSystem` timing
   - downstream `ApplyObjectsSystem` replacement and cleanup timing
6. The safest future implementation route is still to follow the vanilla definition/temp preview lifecycle, not to keep extending manual `Highlighted` logic.

## Evidence Chain

### A. Default-tool building hover entry path

Confirmed:

- `Game.Tools.ToolSystem::ToolUpdate()`
  - reads `activeTool`
  - directly updates that tool
- `Game.Tools.DefaultToolSystem::Update(JobHandle)`
  - in `State.Default`, calls `ToolBaseSystem.GetRaycastResult(Entity&, RaycastHit&, bool&)`
  - when the hit changes, calls `UpdateDefinitions(handle, entity, hit.m_CellIndex.x, float3.zero, false)`

Conclusion:

- ordinary building hover is driven by `DefaultToolSystem`
- not by a separate building-specific hover system

### B. `UpdateDefinitions(...)` is definition refresh, not final rendering

Confirmed:

- `Game.Tools.DefaultToolSystem::UpdateDefinitions(...)`
  - begins with `ToolBaseSystem.DestroyDefinitions(...)`
  - builds and schedules `CreateDefinitionsJob`
- `ToolBaseSystem.GetDefinitionQuery()`
  - is `CreationDefinition + Exclude<Updated>`
- `ToolBaseSystem/DestroyDefinitionsJob::Execute(...)`
  - destroys stale definition entities only

Conclusion:

- `UpdateDefinitions(...)` controls definition lifetime
- it does not by itself guarantee final preview creation or cleanup

### C. Canonical target resolution

Strongest direct evidence:

- `Game.Tools.DefaultToolSystem/SelectEntityJob::Execute(...)`

Confirmed upward relations:

- `Game.Tools.Temp.m_Original`
- `Game.Common.Owner.m_Owner`
- `Game.Objects.Attachment.m_Attached`
- `Game.Common.Target.m_Target`

Meaning:

- raycast hits on helper entities, child fragments, icons, or attachments are normalized upward
- vanilla prefers a more canonical building-like target before building the hover preview

Note:

- `Game.Objects.Attached.m_Parent` exists in the object/building chain
- but the strongest current evidence for canonical root resolution is the set above, not `Attached.m_Parent`

### D. Downward coverage expansion

Confirmed in building/object hover flow:

- `Game.Objects.SubObject`
- `Game.Buildings.InstalledUpgrade`
- `Game.Objects.Attachment`
- `Game.Objects.Attached`

Evidence:

- `Game.Tools.DefaultToolSystem/CreateDefinitionsJob::Execute(...)`
  - enumerates related building/object entities
  - invokes `AddEntity(...)` for upgrades and attachment-related entities
- `Game.Tools.GenerateObjectsSystem/FillCreationListJob::CheckSubObjects(...)`
  - traverses `SubObject` buffers
  - continues checking related attachment / ownership links while assembling creation sets

Conclusion:

- vanilla does not stop at the canonical root
- it fans back out into upgrades, attached parts, and subobjects for hover coverage

### E. Building/object definition richness

Confirmed core writes in `CreateDefinitionsJob::AddEntity(...)` for object/building-like branches:

- `Game.Common.Updated`
- `Game.Tools.CreationDefinition`
- conditional `Game.Tools.OwnerDefinition`
- `Game.Tools.ObjectDefinition`
- conditional `Game.Notifications.IconDefinition`

Important `CreationDefinition` fields confirmed relevant:

- `m_Original`
- `m_Flags`
- `m_SubPrefab`
- `m_Attached`

Important `ObjectDefinition` fields confirmed relevant:

- `m_Position`
- `m_LocalPosition`
- `m_Scale`
- `m_Rotation`
- `m_LocalRotation`
- `m_Elevation`
- `m_Intensity`
- `m_ParentMesh`
- `m_GroupIndex`
- `m_Probability`
- `m_PrefabSubIndex`

Supporting data sources confirmed relevant:

- `Game.Tools.LocalTransformCache`
- `Game.Tools.EditorContainer`
- `Game.Objects.Transform`
- `Game.Objects.Elevation`
- `Game.Prefabs.PrefabRef`

Meaning:

- building preview correctness depends on more than “root + relations”
- local transform, parent mesh, subprefab, group index, probability, scale, and intensity all matter

### F. Downstream preview generation and cleanup

Confirmed downstream systems:

- `Game.Tools.GenerateObjectsSystem`
- `Game.Tools.ApplyObjectsSystem`

Confirmed high-level behavior:

- `GenerateObjectsSystem` consumes `CreationDefinition + Updated + Any(ObjectDefinition | NetCourse)`
- object/building branches become temp/helper objects
- cleanup is not handled by `DestroyDefinitions(...)` alone
- stale definitions must fall out of the refreshed `Updated` set and then downstream systems must stop rebuilding / continue cleanup

Conclusion:

- residual previews, wrong coverage, or freezes can happen if only the definition stage is simulated
- the full stale -> generate -> apply/cleanup lifecycle matters

## Why Previous Mod Attempts Were Incomplete

### 1. Highlighting only the building root

What it solves:

- only the narrowest original-entity highlight case

What it misses:

- canonicalization
- downward expansion
- helper definition richness
- temp/helper object generation
- cleanup lifecycle

### 2. Walking `Owner / Attached / Target`

What it solves:

- partial canonical target normalization

What it misses:

- downstream coverage expansion
- definition structure
- generated preview objects

### 3. Walking `SubObject / InstalledUpgrade / Attachment`

What it solves:

- a broader relation set

What it misses:

- actual vanilla `AddEntity(...)` data population
- `LocalTransformCache`-derived fields
- `CreationDefinition.m_SubPrefab`
- `CreationDefinition.m_Attached`
- lifecycle timing

### 4. Highlighting the whole manually collected entity group

What it solves:

- more outline coverage on original entities

What it misses:

- helper/temp preview generation
- original vanilla object-definition data
- replacement and cleanup cadence

### 5. Creating simplified helper definitions

What it solves:

- only the appearance of a vanilla-like entry point

What it misses:

- full `CreationDefinition`
- full `ObjectDefinition`
- conditional `IconDefinition`
- downstream assumptions about correctly populated data

This is why simplified helper-definition attempts can produce broken visuals or freeze the tool path.

## Practical Guidance For Transit Scope

### Stable current baseline

Current stable project behavior is still the custom route:

- the mod owns selection mode
- the mod performs its own hit resolution
- the mod expands building highlight entities upward/downward
- the mod applies `Highlighted + Updated` to the original entity group

This is stable enough to use, but it is not vanilla-equivalent.

### Recommended next step

If building hover is revisited later, the next implementation attempt should follow this rule:

- do not continue extending only the relation-walking logic
- do not retry simplified helper definitions
- either:
  - let the real vanilla tool path run end-to-end
  - or reconstruct the full building/object definition input that downstream vanilla systems expect

### Must-avoid routes

- only highlighting the root building
- only extending `Owner / Target / SubObject` relations
- manually assuming `Highlighted` is enough
- creating partial helper definition entities
- bypassing cleanup timing
