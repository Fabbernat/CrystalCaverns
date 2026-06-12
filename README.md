# Crystal Caverns

A small turn-based console roguelite written in C# and .NET.

## Run it

```powershell
dotnet run
```

Use the arrow keys or WASD to move. Moving into a slime attacks it. Find crystals, drink potions, collect lesson scrolls marked `?`, and step onto `>` to descend. Press `L` during the game to review the .NET lessons you have found.

For a quick automated smoke test:

```powershell
dotnet run -- --demo
```

## What this teaches

- Top-level programs and command-line arguments
- Enums for finite sets of choices, such as tiles and directions
- Records for lightweight immutable values, such as grid positions
- Classes for entities with behavior and mutable state
- Lists, queues, LINQ, and random generation
- Nullable references and safe fallback values
- Switch expressions for display logic
- Game loops: input, update, enemy AI, render
- Console rendering, colors, keyboard input, and host portability

## Good next changes

- Add weapons and armor with different stats
- Add a save file using `System.Text.Json`
- Split `Program.cs` into multiple files and namespaces
- Add unit tests for movement, combat, and item pickup
- Replace random cave generation with rooms and corridors
