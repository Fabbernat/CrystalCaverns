using System.Text;

Console.OutputEncoding = Encoding.UTF8;

if (args.Contains("--help"))
{
    Console.WriteLine("""
        Crystal Caverns

        Explore the caves, collect crystals, fight slimes, and find stairs.

        Controls:
          Arrow keys / WASD  Move or attack
          Space / .          Wait
          L                  Review lesson scrolls
          Q                  Quit

        Run with --demo to simulate a few turns without keyboard input.
        """);
    return;
}

var game = new Game(width: 48, height: 22, demoMode: args.Contains("--demo"));
game.Run();

enum Tile
{
    Wall,
    Floor,
    Stairs
}

enum ItemKind
{
    Crystal,
    Potion,
    Scroll
}

enum Direction
{
    Up,
    Down,
    Left,
    Right,
    None
}

readonly record struct Point(int X, int Y)
{
    public static Point operator +(Point point, Point delta) => new(point.X + delta.X, point.Y + delta.Y);

    public int ManhattanDistanceTo(Point other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
}

record Item(Point Position, ItemKind Kind, string? Lesson = null)
{
    public char Glyph => Kind switch
    {
        ItemKind.Crystal => '*',
        ItemKind.Potion => '!',
        _ => '?'
    };
}

class Actor
{
    public Actor(string name, Point position, int maxHealth, int attack, char glyph)
    {
        Name = name;
        Position = position;
        MaxHealth = maxHealth;
        Health = maxHealth;
        Attack = attack;
        Glyph = glyph;
    }

    public string Name { get; }
    public Point Position { get; set; }
    public int MaxHealth { get; }
    public int Health { get; set; }
    public int Attack { get; }
    public char Glyph { get; }
    public bool IsAlive => Health > 0;
}

class Enemy : Actor
{
    public Enemy(Point position, int level)
        : base("Cave slime", position, maxHealth: 6 + level * 2, attack: 2 + level, glyph: 's')
    {
    }
}

class Game
{
    private readonly int _width;
    private readonly int _height;
    private readonly bool _demoMode;
    private readonly Random _random = new();
    private readonly List<Enemy> _enemies = [];
    private readonly List<Item> _items = [];
    private readonly List<string> _lessonsLearned = [];
    private readonly Queue<Direction> _demoMoves = new();
    private readonly Queue<string> _lessonDeck = new();
    private Tile[,] _map = new Tile[1, 1];
    private Actor _player = new("Explorer", new Point(1, 1), maxHealth: 30, attack: 5, glyph: '@');
    private int _level = 1;
    private int _turn = 1;
    private int _crystals;
    private string _message = "Find the stairs. Crystals make the expedition worthwhile.";

    public Game(int width, int height, bool demoMode)
    {
        _width = width;
        _height = height;
        _demoMode = demoMode;
        QueueLessons();
        QueueDemoMoves();
        GenerateLevel();
    }

    public void Run()
    {
        TrySetCursorVisible(false);

        try
        {
            var running = true;
            while (running && _player.IsAlive)
            {
                Render();
                var direction = ReadDirection();

                if (direction is null)
                {
                    running = false;
                    continue;
                }

                PlayerTurn(direction.Value);
                if (_player.IsAlive)
                {
                    EnemyTurn();
                }

                _turn++;

                if (_demoMode && _turn > 80)
                {
                    running = false;
                }
            }

            Render();
            Console.WriteLine();
            Console.WriteLine(_player.IsAlive
                ? "Thanks for exploring Crystal Caverns."
                : "You collapse in the cave. The crystals will have to wait.");
        }
        finally
        {
            TrySetCursorVisible(true);
        }
    }

    private void GenerateLevel()
    {
        _map = new Tile[_width, _height];
        _enemies.Clear();
        _items.Clear();

        for (var y = 0; y < _height; y++)
        {
            for (var x = 0; x < _width; x++)
            {
                var isBorder = x == 0 || y == 0 || x == _width - 1 || y == _height - 1;
                _map[x, y] = isBorder || _random.NextDouble() < 0.14 ? Tile.Wall : Tile.Floor;
            }
        }

        _player.Position = FirstOpenTile();

        AddRandomItems(ItemKind.Crystal, 7 + _level);
        AddRandomItems(ItemKind.Potion, 2);
        AddLessonScrolls(count: 3);
        AddEnemies(5 + _level);

        var stairs = RandomOpenPoint(minDistanceFromPlayer: 18);
        _map[stairs.X, stairs.Y] = Tile.Stairs;

        _message = $"Depth {_level}: the air hums around the crystals.";
    }

    private Point FirstOpenTile()
    {
        for (var y = 1; y < _height - 1; y++)
        {
            for (var x = 1; x < _width - 1; x++)
            {
                if (_map[x, y] == Tile.Floor)
                {
                    return new Point(x, y);
                }
            }
        }

        return new Point(1, 1);
    }

    private void AddRandomItems(ItemKind kind, int count)
    {
        for (var i = 0; i < count; i++)
        {
            _items.Add(new Item(RandomOpenPoint(), kind));
        }
    }

    private void AddLessonScrolls(int count)
    {
        for (var i = 0; i < count; i++)
        {
            if (_lessonDeck.Count == 0)
            {
                QueueLessons();
            }

            _items.Add(new Item(RandomOpenPoint(), ItemKind.Scroll, _lessonDeck.Dequeue()));
        }
    }

    private void AddEnemies(int count)
    {
        for (var i = 0; i < count; i++)
        {
            _enemies.Add(new Enemy(RandomOpenPoint(minDistanceFromPlayer: 7), _level));
        }
    }

    private Point RandomOpenPoint(int minDistanceFromPlayer = 0)
    {
        for (var attempts = 0; attempts < 1_000; attempts++)
        {
            var point = new Point(_random.Next(1, _width - 1), _random.Next(1, _height - 1));
            if (IsWalkable(point)
                && point.ManhattanDistanceTo(_player.Position) >= minDistanceFromPlayer
                && !_items.Any(item => item.Position == point)
                && !_enemies.Any(enemy => enemy.Position == point))
            {
                return point;
            }
        }

        return FirstOpenTile();
    }

    private Direction? ReadDirection()
    {
        if (_demoMode)
        {
            if (_demoMoves.Count == 0)
            {
                QueueDemoMoves();
            }

            Thread.Sleep(35);
            return _demoMoves.Dequeue();
        }

        while (true)
        {
            var key = Console.ReadKey(intercept: true).Key;
            return key switch
            {
                ConsoleKey.UpArrow or ConsoleKey.W => Direction.Up,
                ConsoleKey.DownArrow or ConsoleKey.S => Direction.Down,
                ConsoleKey.LeftArrow or ConsoleKey.A => Direction.Left,
                ConsoleKey.RightArrow or ConsoleKey.D => Direction.Right,
                ConsoleKey.Spacebar or ConsoleKey.OemPeriod => Direction.None,
                ConsoleKey.L => ReviewLessons(),
                ConsoleKey.Q => null,
                _ => Direction.None
            };
        }
    }

    private void PlayerTurn(Direction direction)
    {
        var destination = _player.Position + ToDelta(direction);
        var enemy = _enemies.FirstOrDefault(enemy => enemy.Position == destination && enemy.IsAlive);

        if (enemy is not null)
        {
            Attack(_player, enemy);
            if (!enemy.IsAlive)
            {
                _message = $"You shatter the {enemy.Name.ToLowerInvariant()}.";
                _enemies.Remove(enemy);
            }

            return;
        }

        if (direction == Direction.None)
        {
            _message = "You listen to the cave drip around you.";
            return;
        }

        if (!IsWalkable(destination))
        {
            _message = "Stone blocks the way.";
            return;
        }

        _player.Position = destination;
        PickUpItemAt(destination);

        if (_map[destination.X, destination.Y] == Tile.Stairs)
        {
            _level++;
            _player.Health = Math.Min(_player.MaxHealth, _player.Health + 8);
            GenerateLevel();
        }
    }

    private void PickUpItemAt(Point point)
    {
        var item = _items.FirstOrDefault(item => item.Position == point);
        if (item is null)
        {
            _message = "You step carefully through the cave.";
            return;
        }

        _items.Remove(item);

        if (item.Kind == ItemKind.Crystal)
        {
            _crystals++;
            _message = "A crystal rings like glass in your pack.";
            return;
        }

        if (item.Kind == ItemKind.Scroll)
        {
            var lesson = item.Lesson ?? "C# lets you model ideas with types, then let the compiler help you.";
            _lessonsLearned.Add(lesson);
            _message = $"Lesson scroll: {lesson}";
            return;
        }

        var heal = _random.Next(6, 11);
        _player.Health = Math.Min(_player.MaxHealth, _player.Health + heal);
        _message = $"The potion warms you for {heal} health.";
    }

    private void EnemyTurn()
    {
        foreach (var enemy in _enemies.ToList())
        {
            if (!enemy.IsAlive)
            {
                continue;
            }

            if (enemy.Position.ManhattanDistanceTo(_player.Position) == 1)
            {
                Attack(enemy, _player);
                continue;
            }

            var destination = ChooseEnemyMove(enemy);
            if (destination != enemy.Position
                && IsWalkable(destination)
                && destination != _player.Position
                && !_enemies.Any(other => other != enemy && other.Position == destination))
            {
                enemy.Position = destination;
            }
        }
    }

    private Point ChooseEnemyMove(Enemy enemy)
    {
        var canSmellPlayer = enemy.Position.ManhattanDistanceTo(_player.Position) <= 8;
        if (!canSmellPlayer || _random.NextDouble() < 0.25)
        {
            return enemy.Position + ToDelta((Direction)_random.Next(0, 4));
        }

        var xStep = Math.Sign(_player.Position.X - enemy.Position.X);
        var yStep = Math.Sign(_player.Position.Y - enemy.Position.Y);
        var horizontal = enemy.Position + new Point(xStep, 0);
        var vertical = enemy.Position + new Point(0, yStep);

        return _random.NextDouble() < 0.5 ? horizontal : vertical;
    }

    private void Attack(Actor attacker, Actor defender)
    {
        var damage = Math.Max(1, attacker.Attack + _random.Next(-1, 3));
        defender.Health -= damage;
        _message = $"{attacker.Name} hits {defender.Name.ToLowerInvariant()} for {damage}.";
    }

    private bool IsWalkable(Point point)
    {
        return point.X >= 0
            && point.Y >= 0
            && point.X < _width
            && point.Y < _height
            && _map[point.X, point.Y] != Tile.Wall;
    }

    private void Render()
    {
        if (CanUseCursorControl())
        {
            Console.SetCursorPosition(0, 0);
        }

        for (var y = 0; y < _height; y++)
        {
            for (var x = 0; x < _width; x++)
            {
                var point = new Point(x, y);
                Console.ForegroundColor = ColorFor(point);
                Console.Write(GlyphFor(point));
            }

            Console.WriteLine();
        }

        Console.ResetColor();
        Console.WriteLine(FitLine($"Depth {_level} Turn {_turn} HP {_player.Health}/{_player.MaxHealth} Crystals {_crystals} Lessons {_lessonsLearned.Count}"));
        Console.WriteLine(FitLine(_message));
        Console.WriteLine(FitLine("Arrows/WASD move  Space wait  L lessons  Q quit"));
    }

    private static bool CanUseCursorControl() => !Console.IsOutputRedirected;

    private static void TrySetCursorVisible(bool visible)
    {
        if (!CanUseCursorControl())
        {
            return;
        }

        try
        {
            Console.CursorVisible = visible;
        }
        catch (IOException)
        {
            // Some hosts report themselves as interactive but still reject cursor APIs.
        }
    }

    private string FitLine(string text)
    {
        return text.Length <= _width
            ? text.PadRight(_width)
            : string.Concat(text.AsSpan(0, _width - 3), "...");
    }

    private char GlyphFor(Point point)
    {
        if (_player.Position == point)
        {
            return _player.Glyph;
        }

        var enemy = _enemies.FirstOrDefault(enemy => enemy.Position == point && enemy.IsAlive);
        if (enemy is not null)
        {
            return enemy.Glyph;
        }

        var item = _items.FirstOrDefault(item => item.Position == point);
        if (item is not null)
        {
            return item.Glyph;
        }

        return _map[point.X, point.Y] switch
        {
            Tile.Wall => '#',
            Tile.Stairs => '>',
            _ => '.'
        };
    }

    private ConsoleColor ColorFor(Point point)
    {
        if (_player.Position == point)
        {
            return ConsoleColor.Cyan;
        }

        if (_enemies.Any(enemy => enemy.Position == point && enemy.IsAlive))
        {
            return ConsoleColor.Green;
        }

        var item = _items.FirstOrDefault(item => item.Position == point);
        if (item?.Kind == ItemKind.Crystal)
        {
            return ConsoleColor.Magenta;
        }

        if (item?.Kind == ItemKind.Potion)
        {
            return ConsoleColor.Red;
        }

        if (item?.Kind == ItemKind.Scroll)
        {
            return ConsoleColor.White;
        }

        return _map[point.X, point.Y] switch
        {
            Tile.Wall => ConsoleColor.DarkGray,
            Tile.Stairs => ConsoleColor.Yellow,
            _ => ConsoleColor.Gray
        };
    }

    private static Point ToDelta(Direction direction)
    {
        return direction switch
        {
            Direction.Up => new Point(0, -1),
            Direction.Down => new Point(0, 1),
            Direction.Left => new Point(-1, 0),
            Direction.Right => new Point(1, 0),
            _ => new Point(0, 0)
        };
    }

    private void QueueDemoMoves()
    {
        foreach (var direction in new[]
                 {
                     Direction.Right, Direction.Right, Direction.Down, Direction.Down,
                     Direction.Left, Direction.Up, Direction.Right, Direction.Down,
                     Direction.Right, Direction.Right, Direction.None
                 })
        {
            _demoMoves.Enqueue(direction);
        }
    }

    private Direction ReviewLessons()
    {
        if (_demoMode)
        {
            return Direction.None;
        }

        Console.Clear();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("Lesson Scrolls");
        Console.WriteLine("--------------");
        Console.ResetColor();

        if (_lessonsLearned.Count == 0)
        {
            Console.WriteLine("You have not found a lesson scroll yet. Look for ? in the caverns.");
        }
        else
        {
            for (var i = 0; i < _lessonsLearned.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {_lessonsLearned[i]}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("Press any key to return to the cave.");
        Console.ReadKey(intercept: true);
        _message = "You tuck the lesson scrolls safely away.";
        return Direction.None;
    }

    private void QueueLessons()
    {
        foreach (var lesson in new[]
                 {
                     "enum types give names to a fixed set of choices, like Wall, Floor, and Stairs.",
                     "record structs are great for tiny value types. Point compares by X and Y automatically.",
                     "List<T> grows as you add enemies, items, and lessons. The type tells C# what it contains.",
                     "LINQ methods such as FirstOrDefault and Any let you ask collections clear questions.",
                     "switch expressions turn one value into another, which is perfect for glyphs and colors.",
                     "properties such as IsAlive can compute useful facts without storing extra state.",
                     "Console.ReadKey reads one key press, which keeps this game turn-based and responsive.",
                     "Random creates variety, but your rules still shape what kind of cave appears.",
                     "nullable references, like string?, remind you to handle missing data deliberately."
                 })
        {
            _lessonDeck.Enqueue(lesson);
        }
    }
}
