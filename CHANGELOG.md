# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)

## [1.2.1] - 2026-11-04
### Fixed
- A `LinkedInventory` could be left claiming units its source no longer held when an event handler on the source threw: the source's `OnInventoryUpdated` never fired, so the loadout never reconciled. The loadout now follows the source through an internal notification that is raised after the public events even if a handler throws. As a result it reconciles after all public source handlers, not in subscription order. A loadout whose own handler throws no longer stops other loadouts on the same source from reconciling: every loadout is notified, then the first exception is rethrown.
- Whether an `Item` is stackable is now decided when it is constructed. Previously a stack limit that changed across 1 at runtime (for example editing a definition in play mode) changed `IsStackable` and `InstanceId` on live items, so one stackable reference spread over several slots suddenly had the same `InstanceId` in each, breaking instance lookup, removal and saves. A stackable item whose limit later shrinks to 1 now stays fungible with no room left; a non-stackable item stays at 1 if its limit grows.

## [1.2.0] - 2026-11-04
### Changed
- Stackability now comes from one place: an item is stackable exactly when `MaxStackSize` is above 1. `IsStackable` is removed from `IItemAttributes` and from `ItemDefinition` (where it was a serialized field); `Item.IsStackable` is derived and cannot be overridden. `ItemDefinition.MaxStackSize` is clamped to at least 1.
- `Item.InstanceId` is now `Guid?` and is null for stackable items. Stackable items are fungible: stacking merges units without keeping the incoming item, and one reference may fill several slots, so they carry no identity. Compare stackables by `UniqueId`, non-stackables by `InstanceId`.
- `Inventory.IndexOf(TItem)` is renamed `IndexOfInstance(TItem)` and `Contains(TItem)` is renamed `ContainsInstance(TItem)`. They match by `InstanceId` and never match a stackable item. `TryRemoveInstance` and `TryFindByInstanceId` follow the same rule.

### Fixed
- A non-stackable item could be added twice when a distinct object carried the same `InstanceId` (for example a save restored twice); duplicates were detected by object reference. They are now detected by `InstanceId` in `TryAdd`, `AddUpTo`, `AddRange`, `TryAddAt` and transfers.

### Migration
- Remove `isStackable` / `IsStackable` implementations from your `IItemAttributes` types.
- `ItemDefinition` assets that had `IsStackable` off but `MaxStackSize` above 1 now stack. Set `MaxStackSize` to 1 on those assets.
- Replace `inventory.IndexOf(item)` / `inventory.Contains(item)` with `IndexOfInstance(item)` / `ContainsInstance(item)`, and handle `InstanceId` being null.

## [1.1.1] - 2026-11-03
### Fixed
- `Move`, `Compact`, `Sort` and `SplitStack` silently destroyed units from a stack that was over-full because its item's stack limit had shrunk; `Sort` did so for every over-full stack even if nothing moved. `ItemStack.Set` no longer clamps, and `SplitStack` now refuses a quantity above the stack limit.
- `TryTransferTo` / `TransferUpTo` raised the target's events before the source was updated, so a target handler saw the units in both inventories, and one that mutated the source could throw or corrupt the transfer. Both inventories are now updated before either raises events.
- `OnSlotChanged` could report an index that no longer existed when a handler shrank an unbounded inventory during the flush.

### Changed
- The editor duplicate-id postprocessor no longer loads every imported `.asset`; it checks the asset type only.
- `ItemDefinition.OnValidate` warns when `MaxStackSize` is above 1 on an item that is not stackable.

## [1.1.0] - 2026-11-03
### Added
- `Inventory.TryTransferTo` / `TransferUpTo`: move a stack, an exact quantity, or as much as fits into another inventory, honouring the target's stacking, capacity, `allowDuplicates` and `CanAcceptItem`.
- `Inventory.TryAddAt(index, item, quantity)`: atomic placement into a specific slot, for restoring saved fixed-size inventories.
- Editor assembly `GrimbarInteractive.UnityToolkit.Items.Editor`: warns when `ItemDefinition` assets share a `UniqueId` (automatically on import, and via **Grimbar Interactive > Items > Validate Unique Ids**), plus `ItemDefinitionValidator.FindDuplicateUniqueIds`.
- `Inventory.TryRemove(string uniqueId, int quantity = 1)` and `CanRemove(string uniqueId, int quantity = 1)`.

### Changed
- Enumerating an inventory (`foreach`, `Items`, `FindAll`, `TryFind`) now throws `InvalidOperationException` if the inventory is modified mid-enumeration, instead of silently skipping items.
- Mutating operations no longer allocate for event batching in the steady state (struct batch scope, pooled event buffers).
- `Inventory.Move` documentation now states its actual return value.

### Fixed
- The same non-stackable instance could be added to several slots, breaking lookup, removal and saves by `InstanceId`. It is now rejected.
- `ItemDefinition.OnValidate` was only declared in the editor, so overriding it without `#if UNITY_EDITOR` failed player builds. It is now always declared.
- `ItemStack.RemainingSpace` could go negative when an item's stack limit shrank below the stored quantity, understating free space for later adds.

## [1.0.1] - 2026-11-03
### Added
- `Item` / `Item<T>` constructors that accept an existing `InstanceId`, so saved items can be restored.
- Package documentation and README.
- `ItemDefinition` editor validation: trims `UniqueId` and warns when it is empty.

### Changed
- `ItemDefinition.MaxStackSize` now defaults to 1 and is clamped to a minimum of 1 in the inspector.
- `Inventory.Slots` returns a read-only wrapper instead of the backing list.
- `Inventory.Clear()` raises `OnItemRemoved` in ascending slot order.
- `TotalQuantity` and `GetQuantity` clamp to `int.MaxValue` instead of overflowing.
- `ApplyAdd` also requires matching `UniqueId` before stacking, consistent with `GetFreeSpaceFor`.

### Fixed
- `Move` left an empty slot behind in unbounded inventories when it fully merged a stack.
- `OnSlotChanged` could report an index that no longer existed after an unbounded removal inside a batch.
- Disposing a `BeginBatchUpdate` handle twice permanently suppressed all events.
- Event flushing is now safe against handlers that mutate the inventory or throw.

### Removed
- Unused internal `ItemStack(TItem, int)` constructor.

## [1.0.0] - 2026-08-13
### Added
- Added initial item/inventory system.
