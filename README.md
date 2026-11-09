# Unity Items
Item and inventory management system for Unity.

- Slot-based `Inventory<TItem>` in unbounded or fixed-size mode
- Stacking, splitting, merging, sorting, drag-and-drop `Move`
- Atomic `Try*` operations, best-effort `*UpTo` / `AddRange`
- Transfer between inventories (`TryTransferTo` / `TransferUpTo`) and slot-exact placement for save restore (`TryAddAt`)
- Batched, ordered events for UI binding
- ScriptableObject-backed `ItemDefinition`, with no dependency on project code
- Items are non-stackable by default; derive from `StackableItemDefinition` to make a kind stack (a `MaxStackSize` of 1 keeps it non-stackable)
- Editor warning when two definitions share a `UniqueId`

See [Documentation/UnityItems.md](Documentation/UnityItems.md) for the full guide.

## Quick start
```csharp
var inventory = new Inventory<MyItem>(maxSlotCount: 20);
inventory.OnSlotChanged += index => RefreshSlot(index);

if (inventory.TryAdd(new MyItem(definition), 1)) { /* added */ }
```
