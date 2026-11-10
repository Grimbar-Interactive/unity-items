# Unity Items Documentation

A slot-based item and inventory system. Everything lives in `GI.UnityToolkit.Items`.

## Core types

| Type | Role |
| --- | --- |
| `IItemAttributes` | Read-only, edit-time description of a kind of item (`UniqueId`, `Name`, `Description`, `Icon`, `MaxStackSize`). |
| `ItemDefinition` | ScriptableObject base class implementing `IItemAttributes`. Subclass it for your game's item data. Items built from it never stack. |
| `StackableItemDefinition` | `ItemDefinition` with a serialized `MaxStackSize` (default 64, minimum 1). Subclass it for items that stack; a value of 1 opts an item out of stacking. |
| `Item` / `Item<T>` | Runtime instance built from attributes. Carries per-instance state, **not** quantity. Non-stackable items also carry an identity (`InstanceId`). |
| `ItemStack<TItem>` | One slot: an item plus a quantity. Only the inventory mutates it. |
| `Inventory<TItem>` | The container. Owns quantity, stacking, events. |

`UniqueId` says what *kind* an item is; `InstanceId` says which *object* it is. `Item` deliberately does not override `Equals`.

**Stackable** means `MaxStackSize > 1` (`Item.IsStackable`, derived and not overridable). The two kinds of item are compared differently:

| | Compared by | `InstanceId` |
| --- | --- | --- |
| Stackable | `UniqueId` (and `CanStackWith`) - units are fungible | `null` |
| Non-stackable | `InstanceId` - at most one slot per id | a `Guid` |

Stackables carry no identity because stacking merges units without keeping the incoming item, and one stackable reference may legitimately fill several slots (overflow, `SplitStack`, partial transfers). `IndexOfInstance`, `ContainsInstance`, `TryRemoveInstance` and `TryFindByInstanceId` therefore never match a stackable; use the `UniqueId` overloads (`Contains(string)`, `TryRemove`, `GetQuantity`) for those.

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
| `TryAdd` / `TryRemove` / `TryRemoveAt(index, qty)` | **All-or-nothing.** On failure nothing changes and no events fire. `TryRemove` takes an item or a `UniqueId` string. |
| `TryAddAt(index, item, qty)` | All-or-nothing placement into a specific slot. Used to restore saves. |
| `AddUpTo` / `RemoveUpTo` | Best effort. Returns how many units were actually moved. |
| `AddRange` | Adds one unit of each item, best effort, as a single batch. Reports what did not fit. |
| `CanAdd` / `CanRemove` / `GetFreeSpaceFor` | Ask first. `IsFull` only says no *slot* is free; a full inventory can still accept units that stack. `CanRemove` takes an item or a `UniqueId`. |

A non-stackable item (`MaxStackSize == 1`) can only be added one unit at a time per instance; use `AddRange` with distinct instances. One `InstanceId` can occupy only one slot, so adding a non-stackable item whose `InstanceId` is already held is rejected, even if it is a different object (for example a save restored twice).

## Organising slots

`Swap`, `Move` (drag-and-drop: move, merge, or swap), `SplitStack`, `MergeStacks`, `Consolidate`, `Sort`, `Compact`.

`SplitStack` refuses a quantity above the item's stack limit, so a split never creates an over-full stack.

## Moving between inventories

```csharp
bag.TryTransferTo(chest, index);          // whole stack, all or nothing
bag.TryTransferTo(chest, index, 3);       // exactly 3 units, all or nothing
var moved = bag.TransferUpTo(chest, index, 10); // as many as the chest will take
```

The target's own rules decide: stacking, free slots, `allowDuplicates`, and any `CanAcceptItem` override. A failed `TryTransferTo` changes neither inventory and raises no events. On success the target raises `OnItemAdded` and the source raises `OnItemRemoved`, each followed by its own slot and update events (target first), but both inventories are already fully updated by the time any handler runs. Transferring to the same inventory throws; use `Move`.

Enumerating an inventory (`foreach`, `Items`, `FindAll`) throws `InvalidOperationException` if the inventory changes mid-loop, like `List<T>`. Copy what you need first, then mutate:

```csharp
foreach (var item in inventory.FindAll(IsExpired).ToList()) inventory.TryRemoveInstance(item);
```

## Inventory collections

An `InventoryCollection` holds several typed inventories and routes items to the right one. At most one `Inventory<T>` may be registered per item type, but a type and its base types may each have one:

```csharp
var collection = new InventoryCollection();
collection.Register(new Inventory<BasicItem>());
collection.Register(new Inventory<UseItem>(maxSlotCount: 10));   // UseItem : BasicItem

collection.TryAdd(potion);   // a UseItem: goes to Inventory<UseItem>
collection.TryAdd(rock);     // a BasicItem: goes to Inventory<BasicItem>
```

An item is added to the **most specific** registered inventory for its runtime type, regardless of registration order. An item with no matching inventory is rejected. Registration is by exact type, so a subclass such as `DiscBag : Inventory<DiscItem>` is registered as `Register(discBag)` and keyed `DiscItem`.

By default the add uses only that one inventory: if it is full, or its `CanAcceptItem` rejects the item, the add fails. Pass `allowFallback: true` to let units spill into less specific inventories (`UseItem` then `BasicItem`):

```csharp
var collection = new InventoryCollection(allowFallback: true);
```

`TryAdd` stays all-or-nothing across every inventory it may use; `AddUpTo` and `AddRange` are best-effort. A non-stackable item whose `InstanceId` is already held by any inventory it could be added to is rejected, so one instance never lives in two inventories.

Removal (`TryRemove`, `RemoveUpTo`, `TryRemoveInstance`) and queries (`Contains`, `GetQuantity`, `TryFind`, `FindAll`, `TryFindByInstanceId`, `Items`) look at the whole chain, most specific first, whatever `allowFallback` says. The `string uniqueId` overloads search every inventory in registration order. `TryFind<T>` and `FindAll<T>` filter to items of type `T`.

The collection forwards `OnItemAdded` and `OnItemRemoved` from its inventories and raises `OnCollectionUpdated` once at the end of each operation (also on register and unregister). `BeginBatchUpdate()` batches every registered inventory and coalesces the update. Unregistering an inventory stops the forwarding but leaves its contents alone.

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

- Save one record per occupied slot: slot index, `UniqueId`, quantity, and `InstanceId` (null for stackable items, which have none).
- Restore by looking up the `ItemDefinition` from the saved `UniqueId` and constructing the item. Use the `Item(attributes, instanceId)` constructor to bring back a saved `InstanceId` for non-stackable items.
- **Fixed-size inventories:** call `TryAddAt(slotIndex, item, quantity)` for each record. Items return to exactly their saved slots, and a failed call (bad data, a definition that changed) leaves the slot untouched and returns false.
- **Unbounded inventories:** there are no empty slots to address, so call `TryAdd` for each record in saved order. Slot order is rebuilt exactly.

```csharp
var restored = new Inventory<MyItem>(save.SlotCount);
foreach (var entry in save.Slots)
{
    var definition = definitions[entry.UniqueId];
    var item = entry.InstanceId is { } id ? new MyItem(definition, id) : new MyItem(definition);
    if (!restored.TryAddAt(entry.Index, item, entry.Quantity)) Debug.LogWarning($"Could not restore {entry.UniqueId}");
}
```

## Authoring definitions

`ItemDefinition` needs a non-empty, unique, never-changing `UniqueId`. A plain `ItemDefinition` always has a `MaxStackSize` of 1, so its items are not stackable. To make a kind stackable, derive from `StackableItemDefinition` instead: it serializes `MaxStackSize` (default 64, kept at 1 or above in the editor), and a value above 1 is what makes the kind stackable; a value of 1 lets an inheritor opt out of stacking. `ItemDefinition.IsStackable` reports this for editor and UI code that has no `Item` yet. In the editor, a missing `UniqueId` logs a warning when the asset is validated.

Two definitions sharing a `UniqueId` are treated as one kind everywhere: they stack together and collide in saves. The editor assembly (`GrimbarInteractive.UnityToolkit.Items.Editor`) checks for this automatically after any `ItemDefinition` asset is created, duplicated, saved or moved, and warns once per duplicated id listing every asset involved (click the warning to ping the first). Run it on demand from **Grimbar Interactive > Items > Validate Unique Ids**, or call `ItemDefinitionValidator.FindDuplicateUniqueIds` from your own tooling or tests.

`ItemDefinition.OnValidate` is `protected virtual` in every build, so a subclass can override it (calling the base) without wrapping the override in `#if UNITY_EDITOR`.
