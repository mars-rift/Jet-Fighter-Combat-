using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;

enum EnemyState { Patrolling, Chasing, Retreating }
enum Weather { Clear, Cloudy, Storm }

class Mission
{
    public string Objective { get; }
    public bool IsComplete { get; private set; }
    public int RewardPoints { get; }
    
    public Mission(string objective, int rewardPoints)
    {
        Objective = objective;
        RewardPoints = rewardPoints;
        IsComplete = false;
    }
    
    public void Complete()
    {
        IsComplete = true;
        Console.WriteLine($"Mission complete: {Objective}");
        Console.WriteLine($"Reward: {RewardPoints} points");
    }
}

abstract class EnemyJet
{
    public int X { get; set; }
    public int Y { get; set; }
    public char Symbol { get; protected set; }
    public int Health { get; set; }
    public abstract int ScoreValue { get; }
    public double Heading { get; set; } // Direction in degrees (0-359)
    public double Velocity { get; set; } // Current speed
    public double MaxVelocity { get; protected set; }
    public double TurnRate { get; protected set; } // How fast the jet can turn
    public int Altitude { get; set; } // 0-3 altitude levels
    public int Fuel { get; set; }
    public int MaxFuel { get; protected set; }
    public bool IsDetected { get; set; }

    public EnemyJet(int x, int y)
    {
        X = x;
        Y = y;
        Symbol = 'E';
        Health = 1;
        Fuel = 100;     // Initialize with fuel
        MaxFuel = 100;
        Altitude = 1;   // Set starting altitude
        Heading = 0;
        Velocity = 1;
        MaxVelocity = 2;
        TurnRate = 45;  // 45 degrees per turn
    }

    public abstract void Move(int playerX, int playerY, char[,] grid);

    protected void ConsumeFuel(string maneuver)
    {
        int consumption = maneuver switch {
            "afterburner" => 5,
            "climb" => 3,
            "turn" => 2,
            _ => 1 // Regular movement
        };
        
        Fuel -= consumption;
        if (Fuel <= 0)
        {
            Fuel = 0;
            // Handle out of fuel emergency
            HandleOutOfFuel();
        }
    }

    protected void HandleOutOfFuel()
    {
        // For enemy jets
        Health -= 1; // Damage from engine failure
        
        // Emergency descent - lose altitude each turn
        if (Altitude > 0)
        {
            Altitude--;
        }
        else
        {
            // Crashed
            Health = 0;
        }
    }
}

class BasicEnemyJet : EnemyJet
{
    private readonly JetFighterGame game;

    public BasicEnemyJet(int x, int y, JetFighterGame game) : base(x, y)
    {
        this.game = game;
        Health = 2;     // Increase from 1 to 2
        Symbol = 'E';  // Changed from 'B' to 'E' for Enemy
    }

    public override int ScoreValue => 100;

    public override void Move(int playerX, int playerY, char[,] grid)
    {
        var step = game.AStarStep(X, Y, playerX, playerY, grid);
        if (step != null)
        {
            X = step.Value.nextX;
            Y = step.Value.nextY;
        }
        ConsumeFuel("regular");
    }
}

class AdvancedEnemyJet : EnemyJet
{
    private readonly JetFighterGame gameInstance;
    private EnemyState state = EnemyState.Patrolling;

    public AdvancedEnemyJet(int x, int y, JetFighterGame game) : base(x, y)
    {
        gameInstance = game;
        Symbol = 'A';
        Health = 4;      // Increase from 2 to 4
        MaxVelocity = 3; // Faster than basic
        TurnRate = 60;   // More maneuverable
    }

    public override int ScoreValue => 150;

    public override void Move(int playerX, int playerY, char[,] grid)
    {
        switch (state)
        {
            case EnemyState.Patrolling:
                RandomMove(grid);
                if (IsPlayerClose(playerX, playerY))
                    state = EnemyState.Chasing;
                break;
            case EnemyState.Chasing:
                var step = gameInstance.AStarStep(X, Y, playerX, playerY, grid);
                if (step != null)
                {
                    X = step.Value.nextX;
                    Y = step.Value.nextY;
                }
                if (Health < 1)
                    state = EnemyState.Retreating;
                break;
            case EnemyState.Retreating:
                RetreatMove(playerX, playerY, grid);
                break;
        }
        ConsumeFuel("regular");
    }

    private bool IsPlayerClose(int playerX, int playerY) =>
        Math.Abs(X - playerX) <= 5 && Math.Abs(Y - playerY) <= 5;

    private void RandomMove(char[,] grid)
    {
        Random rnd = new Random();
        int direction = rnd.Next(0, 8);
        int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
        int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };
        int newX = X + dx[direction];
        int newY = Y + dy[direction];
        if (newX >= 0 && newX < grid.GetLength(0) &&
            newY >= 0 && newY < grid.GetLength(1))
        {
            X = newX;
            Y = newY;
        }
    }

    private void RetreatMove(int playerX, int playerY, char[,] grid)
    {
        int dx = X - playerX;
        int dy = Y - playerY;
        if (dx != 0) dx /= Math.Abs(dx);
        if (dy != 0) dy /= Math.Abs(dy);
        int newX = X + dx;
        int newY = Y + dy;
        if (newX >= 0 && newX < grid.GetLength(0) &&
            newY >= 0 && newY < grid.GetLength(1))
        {
            X = newX;
            Y = newY;
        }
    }
}

class StealthEnemyJet : EnemyJet
{
    private readonly JetFighterGame game;

    public StealthEnemyJet(int x, int y, JetFighterGame game) : base(x, y)
    {
        this.game = game;
        Symbol = 'S';
        Health = 3;      // Increase from 1 to 3
        MaxVelocity = 2.5;
        TurnRate = 50;
    }

    public override int ScoreValue => 200;

    public override void Move(int playerX, int playerY, char[,] grid)
    {
        var step = game.AStarStep(X, Y, playerX, playerY, grid);
        if (step != null)
        {
            X = step.Value.nextX;
            Y = step.Value.nextY;
        }
        ConsumeFuel("regular");
    }
}

public class Node
{
    public int X { get; set; }
    public int Y { get; set; }
    public Node? Parent { get; set; }
    public int G { get; set; }
    public int H { get; set; }
    public int F => G + H;

    public Node(int x, int y, Node? parent, int g, int h)
    {
        X = x;
        Y = y;
        Parent = parent;
        G = g;
        H = h;
    }
}

class JetFighterGame
{
    private enum GameState { Playing, Victory, Defeat }

    private GameState currentState = GameState.Playing;
    private const int gridSize = 20;
    private char[,] grid;
    private int playerX, playerY;
    private char playerJet = 'F';
    private int playerHealth = 5;
    private int score = 0;
    private List<Mission> activeMissions = new List<Mission>();
    private List<Mission> completedMissions = new List<Mission>();
    private int missionCounter = 0;

    private int _playerDamage = 1;
    public int PlayerDamage 
    { 
        get => _playerDamage; 
        set => _playerDamage = value; 
    }
    private List<EnemyJet> enemyJets;
    private Random random;
    private Weather currentWeather = Weather.Clear;
    private double detectionRangeModifier = 1.0;
    private double accuracyModifier = 1.0;
    private int playerAltitude = 1;
    private int missileAmmo = 10;
    private int gunAmmo = 500;

    public double Heading { get; set; } // Direction in degrees (0-359)
    public double Velocity { get; set; } // Current speed
    public double MaxVelocity { get; protected set; }
    public double TurnRate { get; protected set; } // How fast the jet can turn
    public int Altitude { get; set; } // 0-3 altitude levels
    public int Fuel { get; set; }
    public int MaxFuel { get; protected set; }

    private bool afterburnerEnabled = false;

    private List<(int, int)> basePositions = new List<(int, int)>();

    public JetFighterGame()
    {
        grid = new char[gridSize, gridSize];
        enemyJets = new List<EnemyJet>();
        random = new Random();
        // Initialize flight parameters
        Fuel = 100;
        MaxFuel = 100;
        Heading = 0;
        Velocity = 1;
        MaxVelocity = 2;
        TurnRate = 45;
        InitializeGrid();
        PlaceJetFighters();
    }

    public (int nextX, int nextY)? AStarStep(int startX, int startY, int goalX, int goalY, char[,] grid)
    {
        var openSet = new List<Node>();
        var closedSet = new HashSet<(int, int)>();
        Node startNode = new Node(startX, startY, null, 0, GetHeuristic(startX, startY, goalX, goalY));
        openSet.Add(startNode);

        while (openSet.Count > 0)
        {
            openSet.Sort((a, b) => a.F.CompareTo(b.F));
            Node current = openSet[0];
            if (current.X == goalX && current.Y == goalY)
            {
                Node step = current;
                while (step.Parent != null && (step.Parent.X != startX || step.Parent.Y != startY))
                    step = step.Parent;
                return (step.X, step.Y);
            }
            openSet.Remove(current);
            closedSet.Add((current.X, current.Y));

            foreach (var neighbor in GetNeighbors(current, grid, grid.GetLength(0), goalX, goalY))
            {
                if (closedSet.Contains((neighbor.X, neighbor.Y)))
                    continue;
                var existing = openSet.Find(n => n.X == neighbor.X && n.Y == neighbor.Y);
                if (existing == null)
                    openSet.Add(neighbor);
                else if (neighbor.G < existing.G)
                {
                    existing.G = neighbor.G;
                    existing.Parent = current;
                }
            }
        }
        return null;
    }

    private int GetHeuristic(int x, int y, int goalX, int goalY) =>
        Math.Abs(x - goalX) + Math.Abs(y - goalY);

    private List<Node> GetNeighbors(Node current, char[,] grid, int gridSize, int goalX, int goalY)
    {
        var neighbors = new List<Node>();
        int[] dx = { -1, -1, -1, 0, 0, 1, 1, 1 };
        int[] dy = { -1, 0, 1, -1, 1, -1, 0, 1 };

        for (int i = 0; i < dx.Length; i++)
        {
            int newX = current.X + dx[i];
            int newY = current.Y + dy[i];
            if (newX >= 0 && newX < gridSize && newY >= 0 && newY < gridSize)
            {
                int newG = current.G + 1;
                int newH = GetHeuristic(newX, newY, goalX, goalY);
                neighbors.Add(new Node(newX, newY, current, newG, newH));
            }
        }
        return neighbors;
    }

    private void InitializeGrid()
    {
        for (int i = 0; i < gridSize; i++)
            for (int j = 0; j < gridSize; j++)
                grid[i, j] = '.';
        
        // Add fuel bases/airfields (3 of them) at random positions
        for (int i = 0; i < 3; i++)
        {
            var (x, y) = GenerateRandomPosition();
            grid[x, y] = 'B'; // B for Base/airfield
            basePositions.Add((x, y));
        }

        // Add mid-air refueling tankers (1 of them) that move slowly
        var (tankerX, tankerY) = GenerateRandomPosition();
        grid[tankerX, tankerY] = 'T'; // T for Tanker
    }

    private void PlaceJetFighters()
    {
        (playerX, playerY) = GenerateRandomPosition();
        grid[playerX, playerY] = playerJet;

        (int x, int y) = GenerateRandomPosition();
        enemyJets.Add(new BasicEnemyJet(x, y, this));
        grid[x, y] = 'E';

        (x, y) = GenerateRandomPosition();
        enemyJets.Add(new AdvancedEnemyJet(x, y, this));
        grid[x, y] = 'A';

        (x, y) = GenerateRandomPosition();
        enemyJets.Add(new StealthEnemyJet(x, y, this));
        grid[x, y] = 'S';
    }

    private (int, int) GenerateRandomPosition()
    {
        int x, y;
        do
        {
            x = random.Next(gridSize);
            y = random.Next(gridSize);
        } while (grid[x, y] != '.');
        return (x, y);
    }

    public void DisplayGrid()
    {
        Console.Clear();
        
        // Show radar information
        Console.WriteLine("RADAR STATUS:");
        foreach (var enemy in enemyJets.Where(e => e.IsDetected))
        {
            Console.WriteLine($"{enemy.GetType().Name} at ({enemy.X},{enemy.Y}) | " +
                             $"Alt: {enemy.Altitude} | Health: {enemy.Health}");
        }
        
        // Show grid with altitude indicators
        for (int i = 0; i < gridSize; i++)
        {
            for (int j = 0; j < gridSize; j++)
            {
                // Display different colors based on altitude
                if (i == playerX && j == playerY)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write(playerJet + " ");
                    Console.ResetColor();
                }
                else
                {
                    // Find if there's an enemy at this position
                    var enemy = enemyJets.FirstOrDefault(e => e.X == i && e.Y == j);
                    if (enemy != null && enemy.IsDetected)
                    {
                        // Color based on enemy type
                        Console.ForegroundColor = enemy is StealthEnemyJet ? 
                            ConsoleColor.DarkGray : ConsoleColor.Red;
                        Console.Write(enemy.Symbol + " ");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.Write(grid[i, j] + " ");
                    }
                }
            }
            Console.WriteLine();
        }
        
        Console.WriteLine($"Player Health: {playerHealth}  Score: {score}");
        
        // Show fuel status with color coding
        Console.Write("Fuel: ");
        if (Fuel < MaxFuel * 0.2) // Less than 20%
            Console.ForegroundColor = ConsoleColor.Red;
        else if (Fuel < MaxFuel * 0.5) // Less than 50%
            Console.ForegroundColor = ConsoleColor.Yellow;
        else
            Console.ForegroundColor = ConsoleColor.Green;
            
        Console.Write($"{Fuel}/{MaxFuel}");
        Console.ResetColor();
        
        // Show afterburner status
        Console.Write($"  Afterburner: {(afterburnerEnabled ? "ON" : "OFF")}");
        
        Console.WriteLine($"\nAltitude: {playerAltitude}  Weapons: Missiles ({missileAmmo}) Guns ({gunAmmo})");
        Console.WriteLine($"Weather: {currentWeather}  Damage Multiplier: x{PlayerDamage}");
          // Always display controls
        Console.WriteLine("\n\nCONTROLS:");
        Console.WriteLine("Movement: w/a/s/d/q/e/z/c");
        Console.WriteLine("Actions: r (refuel), b (afterburner), u (climb), j (descend)");
        Console.WriteLine("Game: v (save game), l (load game), m (show missions)");
        
        // Display map legend
        Console.WriteLine("\nMAP LEGEND:");
        Console.WriteLine("F - Your fighter jet");
        Console.WriteLine("B - Base/airfield (refuel here)");
        Console.WriteLine("T - Tanker aircraft (refuel when adjacent)");
        Console.WriteLine("E/A/S - Enemy jets");
    }

    private void EndGame(string message)
    {
        DisplayGrid();
        Console.WriteLine(message);
        Console.WriteLine("\nGame Over. Combat Results:");
        Console.WriteLine($"Final Health: {playerHealth}");
        Console.WriteLine($"Final Score: {score}");
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
        Environment.Exit(0);
    }

    private void ProcessCombat(EnemyJet enemy)
    {
        Console.WriteLine($"\nCombat engaged with {enemy.GetType().Name}!");
        DisplayCombatOptions(enemy);
        
        string? action = Console.ReadLine();
        switch (action?.ToLower())
        {
            case "1": // Fire missile
                if (TryFireWeapon("Missile", enemy, 7))
                    Console.WriteLine("Missile away!");
                break;
            case "2": // Gun attack
                if (TryFireWeapon("Gun", enemy, 3))
                    Console.WriteLine("Guns firing!");
                break;
            case "3": // Evasive maneuver
                PerformEvasiveManeuver();
                break;
            default:
                Console.WriteLine("Combat opportunity missed!");
                break;
        }
        
        // Enemy counterattack with realistic factors
        if (enemy.Health > 0)
        {
            EnemyAttack(enemy);
        }
        else
        {
            Console.WriteLine($"{enemy.GetType().Name} destroyed!");
            enemyJets.Remove(enemy);
            score += enemy.ScoreValue;
            
            // Random chance for power-up when defeating enemy
            if (random.NextDouble() < 0.25) // 25% chance
            {
                PowerUp();
            }
            
            if (enemyJets.Count == 0)
            {
                currentState = GameState.Victory;
            }
        }
    }

    public void MovePlayer(string direction)
    {
        if (currentState != GameState.Playing) return;

        grid[playerX, playerY] = '.';

        // Save original position in case we need to roll back
        int originalX = playerX;
        int originalY = playerY;

        switch (direction)
        {
            case "w": if (playerX > 0) playerX--; break;
            case "s": if (playerX < gridSize - 1) playerX++; break;
            case "a": if (playerY > 0) playerY--; break;
            case "d": if (playerY < gridSize - 1) playerY++; break;
            case "q": if (playerX > 0 && playerY > 0) { playerX--; playerY--; } break;
            case "e": if (playerX > 0 && playerY < gridSize - 1) { playerX--; playerY++; } break;
            case "z": if (playerX < gridSize - 1 && playerY > 0) { playerX++; playerY--; } break;
            case "c": if (playerX < gridSize - 1 && playerY < gridSize - 1) { playerX++; playerY++; } break;
            default:
                Console.WriteLine("Invalid move. Use w, a, s, d, q, e, z, or c.");
                return; // Don't consume fuel for invalid moves
        }

        // Only consume fuel if the player actually moved
        if (originalX != playerX || originalY != playerY)
        {
            ConsumeFuel("regular");
        }

        // Check if player has moved onto an enemy.
        EnemyJet? encountered = enemyJets.Find(e => e.X == playerX && e.Y == playerY);
        if (encountered != null)
            ProcessCombat(encountered);

        if (currentState == GameState.Playing)
            grid[playerX, playerY] = playerJet;
        else
            HandleGameOver();
    }

    public void MoveEnemies()
    {
        if (currentState != GameState.Playing)
            return;

        UpdateWeather();
        UpdateRadar();

        foreach (var jet in enemyJets.ToList())
        {
            grid[jet.X, jet.Y] = '.';
            jet.Move(playerX, playerY, grid);

            // Individual enemy collision with player triggers combat.
            if (jet.X == playerX && jet.Y == playerY)
            {
                ProcessCombat(jet);
                if (currentState != GameState.Playing)
                {
                    HandleGameOver();
                    return;
                }
            }
            grid[jet.X, jet.Y] = jet.Symbol;
        }

        // Define adjacentEnemies before using it
        var adjacentEnemies = enemyJets.FindAll(e => Math.Abs(e.X - playerX) <= 1 && Math.Abs(e.Y - playerY) <= 1);
        
        if (adjacentEnemies.Count >= 2)
        {
            int totalDamage = adjacentEnemies.Sum(e =>
            {
                int baseDamage = random.Next(1, 6);
                bool isCrit = random.NextDouble() < 0.25;
                return isCrit ? baseDamage * 2 : baseDamage;
            });
            Console.WriteLine($"\nEnemy coordinated attack! You took {totalDamage} damage.");
            playerHealth -= totalDamage;
            if (playerHealth <= 0)
            {
                currentState = GameState.Defeat;
                HandleGameOver();
                return;
            }
        }
        grid[playerX, playerY] = playerJet;
    }

    private void HandleGameOver()
    {
        string message = currentState switch
        {
            GameState.Victory => "Victory! All enemy jets destroyed.",
            GameState.Defeat => "Defeat! Your jet has been destroyed.",
            _ => "Game Over!"
        };
        EndGame(message);
    }

    public void UpdateRadar()
    {
        foreach (var enemy in enemyJets)
        {
            // Check if enemy is in detection range based on altitude and stealth
            bool isDetected = CalculateDetection(enemy);
            enemy.IsDetected = isDetected;
        }
    }

    private bool CalculateDetection(EnemyJet enemy)
    {
        int detectionRange = 8; // Base detection range

        // StealthEnemyJet is harder to detect
        if (enemy is StealthEnemyJet)
            detectionRange = 4;

        // Higher altitude increases detection range
        detectionRange += playerAltitude;

        // Calculate distance
        double distance = Math.Sqrt(Math.Pow(enemy.X - playerX, 2) + Math.Pow(enemy.Y - playerY, 2));

        return distance <= detectionRange * detectionRangeModifier;
    }

    private void DisplayCombatOptions(EnemyJet enemy)
    {
        Console.WriteLine("Combat Options:");
        Console.WriteLine("1. Fire missile");
        Console.WriteLine("2. Gun attack");
        Console.WriteLine("3. Evasive maneuver");
        Console.WriteLine("Choose your action (1-3):");
    }

    private void UpdateWeather()
    {
        if (random.NextDouble() < 0.05) // 5% chance to change weather each turn
        {
            currentWeather = (Weather)random.Next(0, 3);
            Console.WriteLine($"Weather changing to: {currentWeather}");
        }
        
        // Apply weather effects
        switch (currentWeather)
        {
            case Weather.Storm:
                // Reduce visibility and accuracy
                detectionRangeModifier = 0.5;
                accuracyModifier = 0.7;
                break;
            case Weather.Cloudy:
                detectionRangeModifier = 0.8;
                accuracyModifier = 0.9;
                break;
            default:
                detectionRangeModifier = 1.0;
                accuracyModifier = 1.0;
                break;
        }
    }
    
    private bool TryFireWeapon(string weaponType, EnemyJet enemy, int baseDamage)
    {
        int ammo = weaponType == "Missile" ? missileAmmo : gunAmmo;
        if (ammo <= 0)
        {
            Console.WriteLine($"Out of {weaponType.ToLower()} ammo!");
            return false;
        }
        
        // Calculate hit chance based on weather, altitude and distance
        double hitChance = 0.65 * accuracyModifier;
        double distance = Math.Sqrt(Math.Pow(enemy.X - playerX, 2) + Math.Pow(enemy.Y - playerY, 2));
        
        // Adjust hit chance based on distance
        if (distance > 5) hitChance -= 0.15;
        if (distance > 8) hitChance -= 0.15;
        
        // Stealth enemies are harder to hit
        if (enemy is StealthEnemyJet) hitChance -= 0.2;
        
        // Roll for hit
        if (random.NextDouble() <= hitChance)
        {
            // Use playerDamage as a multiplier for weapon damage
            int baseDmg = weaponType == "Missile" ? random.Next(4, 7) : random.Next(1, 3);
            int damage = baseDmg * PlayerDamage; // Using playerDamage here
            enemy.Health -= damage;
            Console.WriteLine($"Hit! Enemy {enemy.GetType().Name} took {damage} damage.");
            
            // Reduce ammo
            if (weaponType == "Missile")
                missileAmmo--;
            else
                gunAmmo -= 10;
            
            return true;
        }
        else
        {
            Console.WriteLine($"{weaponType} missed!");
            
            if (weaponType == "Missile")
                missileAmmo--;
            else
                gunAmmo -= 5;
                
            return false;
        }
    }
    
    private void PerformEvasiveManeuver()
    {
        // Implement evasive maneuver logic
        Console.WriteLine("Performing evasive maneuver!");
        
        // Chance to avoid enemy attacks in the next turn
        ConsumeFuel("turn");
        
        // Move to a random adjacent cell if possible
        int[] dx = { -1, -1, -1, 0, 0, 1, 1, 1 };
        int[] dy = { -1, 0, 1, -1, 1, -1, 0, 1 };
        
        List<(int, int)> validMoves = new List<(int, int)>();
        for (int i = 0; i < dx.Length; i++)
        {
            int newX = playerX + dx[i];
            int newY = playerY + dy[i];
            if (newX >= 0 && newX < gridSize && newY >= 0 && newY < gridSize)
            {
                validMoves.Add((newX, newY));
            }
        }
        
        if (validMoves.Count > 0)
        {
            var move = validMoves[random.Next(validMoves.Count)];
            playerX = move.Item1;
            playerY = move.Item2;
            Console.WriteLine("Moved to a new position!");
        }
    }
    
    private void EnemyAttack(EnemyJet enemy)
    {
        // Increase base attack chance
        double attackChance = 0.8; // From 0.75
        
        // Stealth enemies harder to detect but more deadly when they attack
        if (enemy is StealthEnemyJet) 
        {
            attackChance = 0.7; // From 0.6
        }
        
        // Advanced enemies are more accurate
        if (enemy is AdvancedEnemyJet)
        {
            attackChance = 0.9; // From 0.85
        }
        
        // Apply weather effects to attack chance
        attackChance *= accuracyModifier;
        
        if (random.NextDouble() <= attackChance)
        {
            // Increase damage range for more challenge
            int baseDamage = enemy is BasicEnemyJet ? random.Next(1, 4) : // Increased upper bound
                             enemy is AdvancedEnemyJet ? random.Next(2, 5) : // Increased upper bound
                             random.Next(3, 6); // Stealth jets do most damage, increased upper bound
            
            // Critical hit chance increased
            bool isCrit = random.NextDouble() < 0.25; // From 0.2
            int damage = isCrit ? baseDamage * 2 : baseDamage;
            
            Console.WriteLine($"Enemy {enemy.GetType().Name} hits you for {damage} damage!" + 
                             (isCrit ? " CRITICAL HIT!" : ""));
            playerHealth -= damage;
            
            if (playerHealth <= 0)
            {
                currentState = GameState.Defeat;
            }
        }
        else
        {
            Console.WriteLine($"Enemy {enemy.GetType().Name} missed!");
        }
    }
    
    private void ConsumeFuel(string maneuver)
    {
        // Base consumption rate
        int consumption = 1;
        
        // Add consumption based on maneuver
        consumption += maneuver switch {
            "afterburner" => 4,
            "climb" => 2,
            "turn" => 1,
            _ => 0
        };
        
        // Add consumption based on speed
        if (Velocity > MaxVelocity * 0.8) consumption += 1;
        
        // Add consumption based on altitude - higher is more efficient for cruising
        if (playerAltitude == 0) consumption += 1; // Low altitude is inefficient
        else if (playerAltitude == 3) consumption = Math.Max(1, consumption - 1); // High altitude is efficient for cruising, but always consume at least 1 fuel
        
        // If afterburner is on, double the consumption but also increase speed
        if (afterburnerEnabled)
        {
            consumption *= 2;
            // Speed boost logic would be here
        }
        
        Fuel -= consumption;
        if (Fuel <= 0)
        {
            Fuel = 0;
            HandlePlayerOutOfFuel();
        }
    }

    private void HandlePlayerOutOfFuel()
    {
        Console.WriteLine("\n*** WARNING: OUT OF FUEL! EMERGENCY LANDING REQUIRED! ***");
        
        // Forced descent
        if (playerAltitude > 0)
        {
            playerAltitude--;
            Console.WriteLine($"Altitude dropping! Current altitude: {playerAltitude}");
        }
        else
        {
            // Check if player is over a landing zone/base
            if (IsOverLandingZone(playerX, playerY))
            {
                Console.WriteLine("Emergency landing successful!");
                Refuel(50); // Partial refuel after emergency landing
            }
            else
            {
                // Crashed
                playerHealth -= 2;
                Console.WriteLine("CRASH! You've taken damage from emergency landing on hostile terrain!");
                
                if (playerHealth <= 0)
                {
                    currentState = GameState.Defeat;
                    HandleGameOver();
                }
            }
        }
    }

    private bool IsOverLandingZone(int x, int y)
    {
        // Add landing zones as needed - for now just check if position has 'B' (base)
        return grid[x, y] == 'B';
    }

    private void Refuel(int amount)
    {
        Fuel += amount;
        if (Fuel > MaxFuel)
            Fuel = MaxFuel;
        Console.WriteLine($"Refueled! Current fuel: {Fuel}/{MaxFuel}");
    }

    public void PowerUp()
    {
        PlayerDamage++;
        Console.WriteLine($"POWER UP! Weapon damage increased to x{PlayerDamage}");
    }

    public void ProcessPlayerAction(string action)
    {
        switch (action.ToLower())
        {
            case "r": // Refuel if over base or near tanker
                TryRefuel();
                break;
            case "b": // Changed from "a" to "b" for afterburner
                ToggleAfterburner();
                break;            case "v": // Save game (changed from 's' to 'v' to avoid conflict with move south)
                SaveGame();
                break;
            case "l": // Load game
                LoadGame();
                break;
            case "m": // Show missions
                DisplayMissions();
                break;
            // Add other commands as needed
        }
    }

    private void TryRefuel()
    {
        // Check if player is on an airbase
        if (basePositions.Contains((playerX, playerY)))
        {
            Console.WriteLine("Refueling at airbase...");
            Refuel(MaxFuel); // Full refuel
            return;
        }
        
        // Check if player is adjacent to a tanker
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                int nx = playerX + dx;
                int ny = playerY + dy;
                
                if (nx >= 0 && nx < gridSize && ny >= 0 && ny < gridSize && grid[nx, ny] == 'T')
                {
                    Console.WriteLine("Mid-air refueling from tanker...");
                    Refuel(MaxFuel / 2); // Half tank from mid-air refueling
                    return;
                }
            }
        }
        
        Console.WriteLine("No refueling source nearby.");
    }

    private void ToggleAfterburner()
    {
        afterburnerEnabled = !afterburnerEnabled;
        Console.WriteLine(afterburnerEnabled ? 
            "Afterburner ENGAGED! Speed increased but fuel consumption is higher." : 
            "Afterburner DISENGAGED. Normal fuel consumption resumed.");
    }

    public void ChangeAltitude(bool increase)
    {
        if (increase && playerAltitude < 3)
        {
            playerAltitude++;
            ConsumeFuel("climb");
            Console.WriteLine($"Climbing to altitude {playerAltitude}");
        }
        else if (!increase && playerAltitude > 0)
        {
            playerAltitude--;
            Console.WriteLine($"Descending to altitude {playerAltitude}");
            // Descending uses less fuel, no consumption
        }
        else
        {
            Console.WriteLine("Cannot change altitude further in that direction.");
        }
    }

    public void SaveGame()
    {
        var saveData = new Dictionary<string, object>
        {
            { "playerX", playerX },
            { "playerY", playerY },
            { "playerHealth", playerHealth },
            { "score", score },
            { "playerAltitude", playerAltitude },
            { "missileAmmo", missileAmmo },
            { "gunAmmo", gunAmmo },
            { "fuel", Fuel },
            { "playerDamage", PlayerDamage },
            { "currentWeather", (int)currentWeather }
        };

        string json = JsonSerializer.Serialize(saveData);
        string savePath = "save.json";
        File.WriteAllText(savePath, json);
        Console.WriteLine($"Game saved to {savePath}");
    }

    public bool LoadGame()
    {
        string savePath = "save.json";
        if (!File.Exists(savePath))
        {
            Console.WriteLine("No save file found!");
            return false;
        }

        try
        {
            string json = File.ReadAllText(savePath);
            var saveData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

            if (saveData != null)
            {
                playerX = saveData["playerX"].GetInt32();
                playerY = saveData["playerY"].GetInt32();
                playerHealth = saveData["playerHealth"].GetInt32();
                score = saveData["score"].GetInt32();
                playerAltitude = saveData["playerAltitude"].GetInt32();
                missileAmmo = saveData["missileAmmo"].GetInt32();
                gunAmmo = saveData["gunAmmo"].GetInt32();
                Fuel = saveData["fuel"].GetInt32();
                PlayerDamage = saveData["playerDamage"].GetInt32();
                currentWeather = (Weather)saveData["currentWeather"].GetInt32();
            }
            else
            {
                Console.WriteLine("Invalid save data!");
                return false;
            }

            Console.WriteLine("Game loaded successfully!");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading game: {ex.Message}");
            return false;
        }
    }

    private void GenerateNewMission()
    {
        string[] missionTypes = {
            "Destroy an enemy jet",
            "Visit all bases",
            "Reach maximum altitude",
            "Perform mid-air refueling",
            "Survive a storm"
        };
        
        int rewardPoints = random.Next(50, 201); // 50-200 points
        string objective = missionTypes[random.Next(missionTypes.Length)];
        string missionName = $"Mission #{++missionCounter}: {objective}";
        
        Mission newMission = new Mission(missionName, rewardPoints);
        activeMissions.Add(newMission);
        
        Console.WriteLine($"\nNew mission available: {missionName}");
        Console.WriteLine($"Reward: {rewardPoints} points");
    }

    public void CheckMissionProgress()
    {
        foreach (var mission in activeMissions.ToList())
        {
            // Check mission completion based on objective
            bool completed = false;
            
            if (mission.Objective.Contains("Destroy an enemy jet") && 
                enemyJets.Count < 3) // Started with 3 enemies
            {
                completed = true;
            }
            else if (mission.Objective.Contains("Visit all bases"))
            {
                bool visitedAll = true;
                foreach (var basePos in basePositions)
                {
                    if (playerX != basePos.Item1 || playerY != basePos.Item2)
                    {
                        visitedAll = false;
                        break;
                    }
                }
                completed = visitedAll;
            }
            else if (mission.Objective.Contains("Reach maximum altitude") &&
                    playerAltitude == 3)
            {
                completed = true;
            }
            else if (mission.Objective.Contains("Perform mid-air refueling"))
            {
                // Check if adjacent to a tanker (T)
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int nx = playerX + dx;
                        int ny = playerY + dy;
                        
                        if (nx >= 0 && nx < gridSize && 
                            ny >= 0 && ny < gridSize && 
                            grid[nx, ny] == 'T')
                        {
                            completed = true;
                            break;
                        }
                    }
                    if (completed) break;
                }
            }
            else if (mission.Objective.Contains("Survive a storm") &&
                    currentWeather == Weather.Storm)
            {
                completed = true;
            }
            
            if (completed)
            {
                CompleteActiveMission(mission);
            }
        }
    }

    private void CompleteActiveMission(Mission mission)
    {
        activeMissions.Remove(mission);
        completedMissions.Add(mission);
        mission.Complete();
        score += mission.RewardPoints;
        
        // Maybe generate a new mission
        if (activeMissions.Count < 3 && random.NextDouble() < 0.7) // 70% chance
        {
            GenerateNewMission();
        }
    }

    public void DisplayMissions()
    {
        Console.WriteLine("\n--- ACTIVE MISSIONS ---");
        if (activeMissions.Count == 0)
        {
            Console.WriteLine("No active missions.");
        }
        else
        {
            foreach (var mission in activeMissions)
            {
                Console.WriteLine($"* {mission.Objective} (Reward: {mission.RewardPoints} points)");
            }
        }
        
        Console.WriteLine("\n--- COMPLETED MISSIONS ---");
        if (completedMissions.Count == 0)
        {
            Console.WriteLine("No completed missions.");
        }
        else
        {
            foreach (var mission in completedMissions)
            {
                Console.WriteLine($"* {mission.Objective} (Reward: {mission.RewardPoints} points)");
            }
        }
        
        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey();
    }
    
    public void InitializeMissions()
    {
        // Start with 1-2 missions
        int initialMissions = random.Next(1, 3);
        for (int i = 0; i < initialMissions; i++)
        {
            GenerateNewMission();
        }
    }
}

class Weapon
{
    public string Name { get; }
    public int Range { get; }
    public int Damage { get; }
    public int Ammo { get; set; }
    
    public Weapon(string name, int range, int damage, int ammo)
    {
        Name = name;
        Range = range;
        Damage = damage;
        Ammo = ammo;
    }
    
    public bool Fire(out int damageDealt)
    {
        damageDealt = 0;
        if (Ammo <= 0) return false;
        
        Ammo--;
        damageDealt = Damage;
        return true;
    }
}

class Program
{
    static void Main(string[] args)
    {
        JetFighterGame game = new JetFighterGame();
        game.UpdateRadar(); // Add initial radar update
        game.InitializeMissions(); // Initialize missions
        game.DisplayGrid();

        while (true)
        {
            Console.Write("\nEnter move or action: ");
            string? input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
                continue;            // Process action commands
            if (input == "r" || input == "b" || input == "v" || input == "l" || input == "m")
            {
                game.ProcessPlayerAction(input);
            }
            else if (input == "u") // Climb
            {
                game.ChangeAltitude(true);
            }
            else if (input == "j") // Descend
            {
                game.ChangeAltitude(false);
            }
            else // Movement commands
            {
                game.MovePlayer(input);
            }
            
            game.MoveEnemies();
            game.CheckMissionProgress();
            game.DisplayGrid();
        }
    }
}
