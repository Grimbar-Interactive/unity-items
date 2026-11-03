# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)

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
