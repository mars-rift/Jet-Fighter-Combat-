class JetFighterGame
{
    const int gridSize = 10;
    char[,] grid = new char[gridSize, gridSize];
    int playerX = 0, playerY = 0;
    char playerJet;
    List<(int, int)> enemyPositions = new List<(int, int)>();

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
        // Place player's jet
        grid[playerX, playerY] = playerJet;

        // Place enemy jets
        enemyPositions.Add((9, 9));
        enemyPositions.Add((5, 5));

        foreach (var pos in enemyPositions)
        {
            grid[pos.Item1, pos.Item2] = 'E';
        }
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
            default:
                Console.WriteLine("Invalid move. Use 'w', 'a', 's', 'd'.");
                break;
        }

        // Check for combat
        if (grid[playerX, playerY] == 'E')
        {
            Console.WriteLine("Combat engaged! Enemy jet destroyed.");
            grid[playerX, playerY] = playerJet;
            enemyPositions.RemoveAll(e => e.Item1 == playerX && e.Item2 == playerY);
        }
        else
        {
            // Place player at new position
            grid[playerX, playerY] = playerJet;
        }

        // Check for victory condition
        if (enemyPositions.Count == 0)
        {
            Console.Clear();
            DisplayGrid();
            Console.WriteLine("Victory! All enemy jets are destroyed.");
            Environment.Exit(0); // End the game
        }
    }

    public void MoveEnemies()
    {
        List<(int, int)> newEnemyPositions = new List<(int, int)>();

        foreach (var pos in enemyPositions)
        {
            grid[pos.Item1, pos.Item2] = '.';

            int newX = pos.Item1;
            int newY = pos.Item2;

            if (pos.Item1 < playerX) newX++;
            else if (pos.Item1 > playerX) newX--;

            if (pos.Item2 < playerY) newY++;
            else if (pos.Item2 > playerY) newY--;

            if (grid[newX, newY] == playerJet)
            {
                Console.WriteLine("Enemy jet collided with player! Player destroyed.");
                Environment.Exit(0); // End the game
            }

            newEnemyPositions.Add((newX, newY));
        }

        foreach (var newPos in newEnemyPositions)
        {
            grid[newPos.Item1, newPos.Item2] = 'E';
        }

        enemyPositions = newEnemyPositions;
    }

    static void Main(string[] args)
    {
        JetFighterGame game = new JetFighterGame();

        // Player jet selection
        Console.WriteLine("Select your jet: F-22 (F) or Su-57 (S)");
        while (true)
        {
            string choice = Console.ReadLine().ToUpper();
            if (choice == "F")
            {
                game.playerJet = 'F';
                break;
            }
            else if (choice == "S")
            {
                game.playerJet = 'S';
                break;
            }
            else
            {
                Console.WriteLine("Invalid choice. Please select F-22 (F) or Su-57 (S)");
            }
        }

        while (true)
        {
            Console.Clear();
            game.DisplayGrid();
            Console.WriteLine("Move your jet (w = up, s = down, a = left, d = right): ");
            string move = Console.ReadLine();
            game.MovePlayer(move);
            game.MoveEnemies();
        }
    }
}