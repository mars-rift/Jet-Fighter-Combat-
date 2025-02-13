using System;
using System.Collections.Generic;

abstract class EnemyJet
{
    public int X { get; set; }
    public int Y { get; set; }
    public char Symbol { get; protected set; }

    public EnemyJet(int x, int y)
    {
        X = x;
        Y = y;
        Symbol = 'E';
    }

    public abstract void Move(int playerX, int playerY, char[,] grid);
}

class BasicEnemyJet : EnemyJet
{
    public BasicEnemyJet(int x, int y) : base(x, y) { }

    public override void Move(int playerX, int playerY, char[,] grid)
    {
        Random random = new Random();
        int moveDirection = random.Next(4);
        switch (moveDirection)
        {
            case 0: if (X > 0) X--; break; // Up
            case 1: if (X < grid.GetLength(0) - 1) X++; break; // Down
            case 2: if (Y > 0) Y--; break; // Left
            case 3: if (Y < grid.GetLength(1) - 1) Y++; break; // Right
        }
    }
}

class AdvancedEnemyJet : EnemyJet
{
    public AdvancedEnemyJet(int x, int y) : base(x, y)
    {
        Symbol = 'A';
    }

    public override void Move(int playerX, int playerY, char[,] grid)
    {
        if (X < playerX) X++;
        else if (X > playerX) X--;

        if (Y < playerY) Y++;
        else if (Y > playerY) Y--;
    }
}

class StealthEnemyJet : EnemyJet
{
    public StealthEnemyJet(int x, int y) : base(x, y)
    {
        Symbol = 'S';
    }

    public override void Move(int playerX, int playerY, char[,] grid)
    {
        Random random = new Random();
        int moveDirection = random.Next(4);
        switch (moveDirection)
        {
            case 0: if (X > 0) X--; break; // Up
            case 1: if (X < grid.GetLength(0) - 1) X++; break; // Down
            case 2: if (Y > 0) Y--; break; // Left
            case 3: if (Y < grid.GetLength(1) - 1) Y++; break; // Right
        }
    }
}

class JetFighterGame
{
    const int gridSize = 20;
    char[,] grid = new char[gridSize, gridSize];
    int playerX = gridSize / 2, playerY = gridSize / 2;
    char playerJet = 'F';
    int playerHealth = 5;
    int score = 0;
    List<EnemyJet> enemyJets = new List<EnemyJet>();
    Random random = new Random();

    public JetFighterGame()
    {
        InitializeGrid();
        PlaceJetFighters();
    }

    void InitializeGrid()
    {
        for (int i = 0; i < gridSize; i++)
        {
            for (int j = 0; j < gridSize; j++)
            {
                grid[i, j] = '.';
            }
        }
    }

    void PlaceJetFighters()
    {
        enemyJets.Add(new BasicEnemyJet(9, 9));
        enemyJets.Add(new AdvancedEnemyJet(5, 5));
        enemyJets.Add(new StealthEnemyJet(15, 15));

        foreach (var jet in enemyJets)
        {
            grid[jet.X, jet.Y] = jet.Symbol;
        }

        grid[playerX, playerY] = playerJet;
    }

    public void DisplayGrid()
    {
        for (int i = 0; i < gridSize; i++)
        {
            for (int j = 0; j < gridSize; j++)
            {
                Console.Write(grid[i, j] + " ");
            }
            Console.WriteLine();
        }
        Console.WriteLine($"Health: {playerHealth}  Score: {score}");
    }

    public void MovePlayer(string direction)
    {
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
            default: Console.WriteLine("Invalid move. Use 'w', 'a', 's', 'd', 'q', 'e', 'z', 'c'."); break;
        }

        if (grid[playerX, playerY] == 'E' || grid[playerX, playerY] == 'A' || grid[playerX, playerY] == 'S')
        {
            Console.WriteLine("Combat engaged!");
            int damage = random.Next(1, 4);
            playerHealth -= damage;
            Console.WriteLine($"You took {damage} damage. Health remaining: {playerHealth}");

            if (playerHealth <= 0)
            {
                Console.WriteLine("You have been defeated!");
                Environment.Exit(0);
            }
            else
            {
                Console.WriteLine("Enemy jet destroyed.");
                enemyJets.RemoveAll(e => e.X == playerX && e.Y == playerY);
                score += 100;
            }
        }

        // Ensure player icon remains 'F'
        grid[playerX, playerY] = playerJet;

        // Check for victory condition
        if (enemyJets.Count == 0)
        {
            Console.Clear();
            DisplayGrid();
            Console.WriteLine("Victory! All enemy jets are destroyed.");
            Environment.Exit(0);
        }
    }

    public void MoveEnemies()
    {
        foreach (var jet in enemyJets)
        {
            grid[jet.X, jet.Y] = '.';
            jet.Move(playerX, playerY, grid);
            grid[jet.X, jet.Y] = jet.Symbol;
        }

        // Ensure player icon remains 'F'
        grid[playerX, playerY] = playerJet;

        // Check for victory condition
        if (enemyJets.Count == 0)
        {
            Console.Clear();
            DisplayGrid();
            Console.WriteLine("Victory! All enemy jets are destroyed.");
            Environment.Exit(0);
        }
    }

    static void Main(string[] args)
    {
        JetFighterGame game = new JetFighterGame();

        while (true)
        {
            Console.Clear();
            game.DisplayGrid();
            Console.WriteLine("Move your jet (w = up, s = down, a = left, d = right, q = up-left, e = up-right, z = down-left, c = down-right): ");
            string? move = Console.ReadLine();
            if (move != null)
            {
                game.MovePlayer(move);
                game.MoveEnemies();
            }
            else
            {
                Console.WriteLine("Invalid input. Please enter a valid move.");
            }
        }
    }
}
