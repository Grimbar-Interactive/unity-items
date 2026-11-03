# Unity Items Documentation

A slot-based item and inventory system. Everything lives in `GI.UnityToolkit.Items`.

## Core types

| Type | Role |
| --- | --- |
| `IItemAttributes` | Read-only, edit-time description of a kind of item (`UniqueId`, `Name`, `Description`, `Icon`, `IsStackable`, `MaxStackSize`). |
| `ItemDefinition` | ScriptableObject base class implementing `IItemAttributes`. Subclass it for your game's item data. |
| `Item` / `Item<T>` | Runtime instance built from attributes. Carries identity (`InstanceId`) and per-instance state, **not** quantity. |
| `ItemStack<TItem>` | One slot: an item plus a quantity. Only the inventory mutates it. |
| `Inventory<TItem>` | The container. Owns quantity, stacking, events. |

`UniqueId` says what *kind* an item is; `InstanceId` says which *object* it is. `Item` deliberately does not override `Equals`.

## Two inventory modes

```csharp
var bag    = new Inventory<MyItem>();        // unbounded: grows on demand, never holds empty slots
var hotbar = new Inventory<MyItem>(9);       // fixed size: exactly 9 slots, empty slots keep their index
var unique = new Inventory<MyItem>(20, allowDuplicates: false); // at most one stack per UniqueId
```

- **Unbounded:** removing a stack removes its slot, so later indices shift down. `Compact()` is a no-op.
- **Fixed size:** slot objects are created once and keep their index, so UI can bind to `inventory[i]` once and refresh on `OnSlotChanged`.

## Adding and removing

| Method | Behaviour |
| --- | --- |
| `TryAdd` / `TryRemove` / `TryRemoveAt(index, qty)` | **All-or-nothing.** On failure nothing changes and no events fire. |
| `AddUpTo` / `RemoveUpTo` | Best effort. Returns how many units were actually moved. |
| `AddRange` | Adds one unit of each item, best effort, as a single batch. Reports what did not fit. |
| `CanAdd` / `CanRemove` / `GetFreeSpaceFor` | Ask first. `IsFull` only says no *slot* is free; a full inventory can still accept units that stack. |

A non-stackable item (`StackLimit == 1`) can only be added one unit at a time per instance; use `AddRange` with distinct instances.

## Organising slots

`Swap`, `Move` (drag-and-drop: move, merge, or swap), `SplitStack`, `MergeStacks`, `Consolidate`, `Sort`, `Compact`.

## Events

| Event | Raised |
| --- | --- |
| `OnItemAdded(item, qty)` / `OnItemRemoved(item, qty)` | Once per logical add/removal, even across several slots. `Clear()` raises `OnItemRemoved` once per stack, in ascending slot order. |
| `OnSlotChanged(index)` | Once per changed slot, ascending, coalesced within a batch. In unbounded mode, slots that disappeared are not reported; check `SlotCount`. |
| `OnInventoryUpdated` | Exactly once at the end of any operation that changed something. |

Order per operation: item events, then slot events, then `OnInventoryUpdated`.

Group several operations into one notification with a batch:

```csharp
using (inventory.BeginBatchUpdate())
{
    inventory.TryAdd(sword);
    inventory.TryAdd(potion, 5);
}   // events flush here, once. Batches nest; disposing a handle twice is harmless.
```

Handlers may safely mutate the inventory; each flush works from its own snapshot.

## Custom stacking rules

Override `Item.CanStackWith` when per-instance state matters (durability, enchantments). The override must be symmetric, return false for `null`, and never return true for different `UniqueId`s.

## Filtered inventories

Override `CanAcceptItem` to reject items regardless of space:

```csharp
public class DiscBag : Inventory<MyItem>
{
    public DiscBag(int slots) : base(slots) { }
    protected override bool CanAcceptItem(MyItem item) => item is DiscItem;
}
```

## Saving

`Inventory` and `ItemStack` are intentionally **not** Unity-serializable. Persist through your own DTO:

- Stackable items: save `UniqueId` + quantity per slot.
- Non-stackable items with per-instance state: also save `InstanceId`, and restore it with the `Item(attributes, instanceId)` constructor.
- Rebuild by looking up the `ItemDefinition` from the saved `UniqueId`, constructing the item, and calling `TryAdd` (or `Move`/`Swap` to restore exact slot positions in a fixed-size inventory).

## Authoring definitions

`ItemDefinition` needs a non-empty, unique, never-changing `UniqueId`. `MaxStackSize` defaults to 1 and is clamped to at least 1; it is ignored unless `IsStackable` is set. In the editor, a missing `UniqueId` logs a warning when the asset is validated.
