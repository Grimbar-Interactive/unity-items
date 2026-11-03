# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)

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
