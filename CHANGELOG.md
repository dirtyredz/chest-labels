# Changelog

Versions here are **released** versions - the ones players see on Nexus. Build iterations
during development are not releases and do not get an entry. See RELEASING.md.

Plain ASCII punctuation throughout - em-dashes did not survive a PowerShell round-trip and
came back double-encoded.

## 1.0.0 - 2026-08-10

**Screen reader support.** Named chests are now spoken by screen readers such as
MoonlightAccess. Facing a chest in the world announced only its type ("Storage Crate"),
because the reader takes the name from the game's own name field and the game leaves that
field empty for chests. The mod now supplies the label through that field when it is read,
so the reader speaks the name the player gave it.

A new `ScreenReaderName` setting chooses the wording:

- **Type and label** (default) - "Storage Crate named Pantry".
- **Label only** - "Pantry".
- **Off** - leaves the field untouched.

Read-only, as ever: the label is supplied only at the moment the field is read and is never
written into the save, so item stacking and identity - which compare the underlying stored
value, not this field - are untouched. See research/02-save-format.md for why the field
itself is deliberately left unwritten.

## 0.7.1 - 2026-08-03

**Fixed: clicking the pencil changed the text's appearance.** The rename field resolved
Gelica-Bold at a fixed 20pt while the title had moved to Gelica-Black at 33, so weight, size
and colour all shifted the moment editing began. The field now matches the title exactly and
follows it if the title's size is changed in settings.

Nothing links the two objects, so restyling the title in 0.7.0 quietly left the field behind.

## 0.7.0 - 2026-08-03

Visual integration. The mod should no longer look bolted on.

**Hover label**

- Uses the game's own speech bubble rather than a hand-drawn plate, so the shape, font and
  animation are the game's.
- New `NameplateTint` recolours the bubble and its pointer. The original colour of every
  image touched is cached and restored the moment the label hides, so the game's own
  tooltips are unaffected.
- Detection reads the game's current interaction target instead of a private raycast. Two
  independent detections disagreed near the edge of range: the interaction arrow appeared
  before the label, then vanished as the label caught up.
- The interaction arrow stands down while a named chest's label is showing, derived from the
  game's interaction target in the same frame rather than from whether the label has
  rendered yet.

**Chest window title**

- Gelica-Black at a larger size, in the yellow the game uses for item counts.
- More room above and below: the decorative flourish is raised and the divider line pushed
  down, with the window growing so the item grid keeps its size.

**Fixed**

- The hover label rendered in Unity's stock font while the in-panel title correctly used the
  game's Gelica. The title borrowed its font from a neighbouring text element; the hover
  label lives on the mod's own canvas with no neighbour, so it fell back to a default and
  shipped that way in 0.6.0. Fonts are now resolved explicitly everywhere, including the
  rename field.

**Tried and reverted during development**

- Feeding the name into the game's interaction banner via `Interactable.InteractionText`.
  Matched the game exactly but moved the name to the bottom of the screen with a right-click
  icon. Kept behind `UseGameInteractionLabel`, off by default.
- Assuming the game's nameplate was the purple character-name banner. It is the orange
  speech bubble; the banner is the interaction label.

## 0.6.0 - 2026-08-03

First public release.

- Name any storage chest from inside the chest window, with a pencil button. Enter saves,
  Escape cancels.
- The name shows as a title in the chest window's own header, under the game's decoration.
- Mouse over a chest in the world to see its name.
- Labels stored per save in a JSON sidecar under `BepInEx/config/ChestLabels/`, keyed by the
  chest's persistent GUID. **Never writes to the game save.**
- Labels survive a chest being picked up and re-placed; they are pruned when a chest is
  destroyed.
- Chest-screen hotkeys are suppressed while the rename field has focus. The screen reads
  input through Rewired, which ignores Unity's keyboard focus, so typing a name containing
  "x" would otherwise also reorder the chest.
- Corrupt sidecar files are quarantined with a `.corrupt-<timestamp>` suffix rather than
  deleted.
