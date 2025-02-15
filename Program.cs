using System;
using System.Collections.Generic;
using System.Linq;

enum EnemyState { Patrolling, Chasing, Retreating }

abstract class EnemyJet
{
    public int X { get; set; }
    public int Y { get; set; }
    public char Symbol { get; protected set; }
    public int Health { get; set; }
    public abstract int ScoreValue { get; }

    public EnemyJet(int x, int y)
    {
        X = x;
        Y = y;
        Symbol = 'E';
        Health = 1;
    }

    public abstract void Move(int playerX, int playerY, char[,] grid);
}

class BasicEnemyJet : EnemyJet
{
    private readonly JetFighterGame game;

    public BasicEnemyJet(int x, int y, JetFighterGame game) : base(x, y)
    {
        this.game = game;
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
        Health = 2;
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
        Health = 1;
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
    private int playerDamage = 1;
    private List<EnemyJet> enemyJets;
    private Random random;

    public JetFighterGame()
    {
        grid = new char[gridSize, gridSize];
        enemyJets = new List<EnemyJet>();
        random = new Random();
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
        for (int i = 0; i < gridSize; i++)
        {
            for (int j = 0; j < gridSize; j++)
                Console.Write(grid[i, j] + " ");
            Console.WriteLine();
        }
        Console.WriteLine($"Player Health: {playerHealth}  Score: {score}");
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

        // Player attacks enemy.
        enemy.Health -= playerDamage;
        Console.WriteLine($"You hit the enemy for {playerDamage} damage.");

        if (enemy.Health > 0)
        {
            // Enemy counterattacks with higher damage and added critical chance.
            int baseDamage = random.Next(1, 6); // Damage between 1 and 5.
            bool isCritical = random.NextDouble() < 0.3; // 30% chance.
            int damageTaken = isCritical ? baseDamage * 2 : baseDamage;
            Console.WriteLine($"{enemy.GetType().Name} counterattacks{(isCritical ? " with a critical hit" : "")}! You took {damageTaken} damage.");
            playerHealth -= damageTaken;
            if (playerHealth <= 0)
                currentState = GameState.Defeat;
        }
        else
        {
            Console.WriteLine($"{enemy.GetType().Name} destroyed!");
            enemyJets.Remove(enemy);
            score += enemy.ScoreValue;
            if (enemyJets.Count == 0)
            {
                currentState = GameState.Victory;
                return;
            }
        }
    }

    public void MovePlayer(string direction)
    {
        if (currentState != GameState.Playing) return;

        grid[playerX, playerY] = '.';

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
                break;
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

        // Coordinated enemy attack now deals more damage with a chance for critical hits.
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
}

class Program
{
    static void Main(string[] args)
    {
        JetFighterGame game = new JetFighterGame();
        game.DisplayGrid();

        while (true)
        {
            Console.Write("\nEnter move (w/a/s/d/q/e/z/c): ");
            string? input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
                continue;

            game.MovePlayer(input);
            game.MoveEnemies();
            game.DisplayGrid();
        }
    }
}
