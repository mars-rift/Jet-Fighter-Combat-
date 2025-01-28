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
        // Basic enemy moves randomly
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
        // Advanced enemy moves towards the player
        if (X < playerX) X++;
        else if (X > playerX) X--;

        if (Y < playerY) Y++;
        else if (Y > playerY) Y--;
    }
}

class JetFighterGame
{
    const int gridSize = 20;
    char[,] grid = new char[gridSize, gridSize];
    int playerX = gridSize / 2, playerY = gridSize / 2; // Start player in the center
    char playerJet = 'F'; // Player jet is an F-35
    int playerHealth = 5; // Player health
    int score = 0; // Player score
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
        // Place enemy jets
        enemyJets.Add(new BasicEnemyJet(9, 9));
        enemyJets.Add(new AdvancedEnemyJet(5, 5));

        foreach (var jet in enemyJets)
        {
            grid[jet.X, jet.Y] = jet.Symbol;
        }

        // Place player's jet
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
        // Remove player from current position
        grid[playerX, playerY] = '.';

        switch (direction)
        {
            case "w": // Up
                if (playerX > 0) playerX--;
                break;
            case "s": // Down
                if (playerX < gridSize - 1) playerX++;
                break;
            case "a": // Left
                if (playerY > 0) playerY--;
                break;
            case "d": // Right
                if (playerY < gridSize - 1) playerY++;
                break;
            case "q": // Up-Left
                if (playerX > 0 && playerY > 0) { playerX--; playerY--; }
                break;
            case "e": // Up-Right
                if (playerX > 0 && playerY < gridSize - 1) { playerX--; playerY++; }
                break;
            case "z": // Down-Left
                if (playerX < gridSize - 1 && playerY > 0) { playerX++; playerY--; }
                break;
            case "c": // Down-Right
                if (playerX < gridSize - 1 && playerY < gridSize - 1) { playerX++; playerY++; }
                break;
            default:
                Console.WriteLine("Invalid move. Use 'w', 'a', 's', 'd', 'q', 'e', 'z', 'c'.");
                break;
        }

        // Check for combat
        if (grid[playerX, playerY] == 'E' || grid[playerX, playerY] == 'A')
        {
            Console.WriteLine("Combat engaged!");
            int damage = random.Next(1, 4); // Random damage between 1 and 3
            playerHealth -= damage;
            Console.WriteLine($"You took {damage} damage. Health remaining: {playerHealth}");

            if (playerHealth <= 0)
            {
                Console.WriteLine("You have been defeated!");
                Environment.Exit(0); // End the game
            }
            else
            {
                Console.WriteLine("Enemy jet destroyed.");
                grid[playerX, playerY] = playerJet;
                enemyJets.RemoveAll(e => e.X == playerX && e.Y == playerY);
                score += 100; // Increase score
            }
        }
        else
        {
            // Place player at new position
            grid[playerX, playerY] = playerJet;
        }

        // Check for victory condition
        if (enemyJets.Count == 0)
        {
            Console.Clear();
            DisplayGrid();
            Console.WriteLine("Victory! All enemy jets are destroyed.");
            Environment.Exit(0); // End the game
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

        // Check for victory condition
        if (enemyJets.Count == 0)
        {
            Console.Clear();
            DisplayGrid();
            Console.WriteLine("Victory! All enemy jets are destroyed.");
            Environment.Exit(0); // End the game
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
            string move = Console.ReadLine();
            game.MovePlayer(move);
            game.MoveEnemies();
        }
    }
}


