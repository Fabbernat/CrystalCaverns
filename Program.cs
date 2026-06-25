using System.Text;

Console.OutputEncoding = Encoding.UTF8;

if (args.Contains("--help"))
{
    Console.WriteLine("""
        Crystal Caverns

        Explore the caves, collect crystals, fight slimes, and find portals.

        Controls:
          Arrow keys / WASD  Move or attack
          Space / .          Wait
          L                  Review lesson scrolls
          Q                  Quit

        Run with --demo to simulate a few turns without keyboard input.
        """);
    return;
}

var game = new Game(width: 20, height: 12, demoMode: args.Contains("--demo"));
game.Run();

enum Tile
{
    Wall,
    Floor,
    Portal
}

enum ItemKind
{
    Crystal,
    Apple,
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
    public string Glyph => Kind switch
    {
        ItemKind.Crystal => "💎",
        ItemKind.Apple => "🍎",
        _ => "📜"
    };
}

class Actor
{
    public Actor(string name, Point position, int maxHealth, int attack, string glyph)
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
    public string Glyph { get; }
    public bool IsAlive => Health > 0;
}

class Enemy : Actor
{
    public Enemy(Point position, int level)
        : base("Cave slime", position, maxHealth: 6 + level * 2, attack: 2 + level, glyph: "🤖")
    {
    }
}

class Game
{
    private readonly int _width;
    private const int HudWidth = 200;
    private readonly int _height;
    private readonly bool _demoMode;
    private readonly Random _random = new();
    private readonly List<Enemy> _enemies = [];
    private readonly List<Item> _items = [];
    private readonly List<string> _lessonsLearned = [];
    private readonly Queue<Direction> _demoMoves = new();
    private readonly Queue<string> _lessonDeck = new();
    private Tile[,] _map = new Tile[1, 1];

    // SHOCKBLAST
    private const int ShockblastRadius = 4;
    private const int ShockblastUsesPerLevel = 2;

    private int _shockblastUsesRemaining = ShockblastUsesPerLevel;


    // Magic number-ek:
    private const int StartingHealth = 40;
    private const int StartingAttack = 5;
    private static readonly Point StartingPosition = new(10, 16); // A map közepén kezd

    // És a felhasználásuk:
    private Actor _player = new(
        name: "Explorer",
        position: default,
        maxHealth: StartingHealth,
        attack: StartingAttack,
        glyph: "🧙"
    );


    private int _level = 1;
    private int _turn = 1;
    private int _crystals;
    private string _message = "Find the portals. Crystals make the expedition worthwhile.";

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
        Console.Clear();
        Console.SetCursorPosition(0, 0);
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

        _shockblastUsesRemaining = ShockblastUsesPerLevel;

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
        AddRandomItems(ItemKind.Apple, 2);
        AddLessonScrolls(count: 3);
        AddEnemies(5 + _level);

        var portals = RandomOpenPoint(minDistanceFromPlayer: 6);
        _map[portals.X, portals.Y] = Tile.Portal;

        _message = $"Depth {_level}: the air hums around the crystals.";
    }

    // SHOCKBLAST METHOD
    private void UseShockblast()
    {
        if (_shockblastUsesRemaining <= 0)
        {
            _message = "No shockblast charges left on this level.";
            return;
        }

        var enemiesKilled = _enemies
            .Where(enemy =>
                enemy.IsAlive &&
                enemy.Position.ManhattanDistanceTo(_player.Position) <= ShockblastRadius)
            .ToList();

        if (enemiesKilled.Count == 0)
        {
            _message = "Your shockblast pulses through the cave but hits nothing.";
            return;
        }

        foreach (var enemy in enemiesKilled)
        {
            enemy.Health = 0;
            _enemies.Remove(enemy);
        }

        _shockblastUsesRemaining--;

        _message =
            $"Arcane shockblast destroyed {enemiesKilled.Count} enemies! " +
            $"Charges left: {_shockblastUsesRemaining}";
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

        return new Point(10, 16);
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

        ConsoleKey.E => HandleShockblast(),

        ConsoleKey.Q => null,
        _ => Direction.None
    };
        }
    }

    private Direction HandleShockblast()
    {
        UseShockblast();
        return Direction.None;
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

        if (_map[destination.X, destination.Y] == Tile.Portal)
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

        var heal = _random.Next(10, 16);
        _player.Health = Math.Min(_player.MaxHealth, _player.Health + heal);
        _message = $"The apple heals you for {heal} health.";
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
        var canSmellPlayer = enemy.Position.ManhattanDistanceTo(_player.Position) <= (3 + _level / 2);
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
        Console.WriteLine(FitHud(
            $"Depth {_level} Turn {_turn} " +
            $"HP {_player.Health}/{_player.MaxHealth} " +
            $"Crystals {_crystals} " +
            $"Lessons {_lessonsLearned.Count} " +
            $"Shock {_shockblastUsesRemaining}/{ShockblastUsesPerLevel}"
        ));
        Console.WriteLine(FitHud(_message));
        Console.WriteLine(FitHud(
            "Arrows/WASD move  Space wait  E shockblast(4 tiles)  L lessons  Q quit"
        ));
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



    private string FitHud(string text)
    {
        return text.Length <= HudWidth
            ? text
            : text[..(HudWidth - 3)] + "...";
    }

    private string GlyphFor(Point point)
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
            Tile.Wall => "🧱",
            Tile.Portal => "🌀",
            _ => "  "
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

        if (item?.Kind == ItemKind.Apple)
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
            Tile.Portal => ConsoleColor.Yellow,
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
        // ===== C# BASICS (25) =====
        "Variables store data so your program can remember values.",
        "Choose meaningful variable names so code explains itself.",
        "int stores whole numbers while double stores decimal numbers.",
        "string stores text such as names and messages.",
        "bool stores only true or false values.",
        "if statements let your program make decisions.",
        "else handles situations when a condition is false.",
        "for loops repeat code a specific number of times.",
        "foreach loops visit every item in a collection.",
        "while loops continue until a condition becomes false.",
        "Methods let you group reusable logic into one place.",
        "Parameters send data into methods.",
        "return sends a result back from a method.",
        "Classes act like blueprints for creating objects.",
        "Objects are instances created from classes.",
        "Properties provide controlled access to data.",
        "Constructors run when an object is created.",
        "Lists store collections of items that can grow.",
        "Arrays have a fixed size once created.",
        "Enums give names to fixed choices.",
        "Comments help explain why code exists.",
        "Exceptions represent errors that happen while running.",
        "Try-catch blocks let you handle errors safely.",
        "Null means a variable points to nothing.",
        "Practice reading code is just as important as writing code.",

        // ===== VISUAL STUDIO (25) =====
        "Solution Explorer shows your projects and files.",
        "Press F5 to run and debug your application.",
        "Press Ctrl+S often to save your work.",
        "Breakpoints pause execution at specific lines.",
        "The debugger helps you inspect variable values.",
        "Hover over variables during debugging to see values.",
        "Step Into enters method execution line by line.",
        "Step Over runs a method without entering it.",
        "Error List helps you find compilation problems.",
        "Warnings do not always stop your program.",
        "Red underlines usually indicate code problems.",
        "IntelliSense suggests methods and properties automatically.",
        "Press Ctrl+Space to trigger IntelliSense manually.",
        "Rename symbols safely using Refactor Rename.",
        "Use Ctrl+Dot to see quick code fixes.",
        "Build compiles your project into executable code.",
        "Clean removes generated build files.",
        "Rebuild cleans and compiles everything again.",
        "The Output window shows build messages.",
        "NuGet installs useful libraries into projects.",
        "Projects can belong to one solution.",
        "Use folders to organize related files.",
        "Git integration helps track code changes.",
        "Watch windows let you monitor variables.",
        "Debugging saves more time than guessing.",

        // ===== .NET + WEB API (25) =====
        "ASP.NET helps build web applications with C#.",
        "A Web API lets applications communicate with each other.",
        "Endpoints are URLs that perform actions.",
        "GET requests usually retrieve data.",
        "POST requests usually create new data.",
        "PUT requests usually update data.",
        "DELETE requests usually remove data.",
        "JSON is commonly used to exchange data.",
        "Controllers organize API endpoints.",
        "Routes decide which URL calls which code.",
        "Dependency Injection supplies objects automatically.",
        "Services contain business logic.",
        "Models describe application data.",
        "DTOs transfer data between systems.",
        "Middleware runs during every request.",
        "Status code 200 means success.",
        "Status code 404 means not found.",
        "Status code 500 means server error.",
        "Swagger helps test APIs visually.",
        "Entity Framework simplifies database work.",
        "DbContext manages database communication.",
        "Async methods improve responsiveness.",
        "await pauses without blocking execution.",
        "APIs should return useful error messages.",
        "Clean architecture keeps code easier to maintain.",

        // ===== REACT + FULL STACK (25) =====
        "React builds interfaces from reusable components.",
        "Components help divide applications into small pieces.",
        "Props send data from parent to child.",
        "State stores changing information.",
        "Changing state updates the user interface.",
        "useState creates component state.",
        "useEffect runs code after rendering.",
        "JSX lets HTML-like syntax live inside JavaScript.",
        "Events respond to user actions.",
        "Forms collect user input.",
        "Controlled inputs connect form values to state.",
        "Keys help React track list items.",
        "Conditional rendering shows content only when needed.",
        "Fetch requests retrieve data from APIs.",
        "Frontend runs in the browser.",
        "Backend runs on the server.",
        "Full-stack developers work with frontend and backend.",
        "REST APIs connect applications together.",
        "JSON often moves data between frontend and backend.",
        "Browsers send HTTP requests to servers.",
        "CSS controls application appearance.",
        "Responsive design adapts layouts to screens.",
        "Developer tools help inspect web pages.",
        "Console logs help track application behavior.",
        "Small reusable components simplify large projects."
    })
        {
            _lessonDeck.Enqueue(lesson);
        }
    }
}
