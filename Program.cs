using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;

enum EnemyState { Patrolling, Chasing, Retreating, Flanking, Diving, Climbing }
enum Weather { Clear, Cloudy, Storm }
enum AircraftType { F16_Falcon, F35_Lightning, Su27_Flanker, MiG29_Fulcrum, F22_Raptor }

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
    public int MaxHealth { get; protected set; }
    public abstract int ScoreValue { get; }
    public double Heading { get; set; } // Direction in degrees (0-359)
    public double Velocity { get; set; } // Current speed
    public double MaxVelocity { get; protected set; }
    public double TurnRate { get; protected set; } // How fast the jet can turn
    public int Altitude { get; set; } // 0-3 altitude levels
    public int PreferredAltitude { get; protected set; } // AI preferred altitude
    public int Fuel { get; set; }
    public int MaxFuel { get; protected set; }
    public bool IsDetected { get; set; }
    public AircraftType AircraftType { get; protected set; }
    public string AircraftName { get; protected set; }
    public EnemyState CurrentState { get; protected set; }
    public int TurnsInState { get; protected set; }
    public int CombatExperience { get; protected set; } // Affects accuracy and tactics
    public bool HasAdvancedRadar { get; protected set; }
    public int DetectionRange { get; protected set; }

    public EnemyJet(int x, int y)
    {
        X = x;
        Y = y;
        Symbol = 'E';
        Health = 1;
        MaxHealth = 1;
        Fuel = 100;
        MaxFuel = 100;
        Altitude = 1;
        PreferredAltitude = 1;
        Heading = 0;
        Velocity = 1;
        MaxVelocity = 2;
        TurnRate = 45;
        CurrentState = EnemyState.Patrolling;
        TurnsInState = 0;
        CombatExperience = 1;
        HasAdvancedRadar = false;
        DetectionRange = 5;
        AircraftName = "Unknown";
    }

    public abstract void Move(int playerX, int playerY, char[,] grid);

    protected void UpdateState(EnemyState newState)
    {
        if (CurrentState != newState)
        {
            CurrentState = newState;
            TurnsInState = 0;
        }
        else
        {
            TurnsInState++;
        }
    }

    protected void ManageAltitude(int playerAltitude)
    {
        // AI altitude management based on combat situation and aircraft type
        if (CurrentState == EnemyState.Chasing)
        {
            // Try to match or gain altitude advantage over player
            if (playerAltitude > Altitude && Altitude < 3)
            {
                Altitude++;
                ConsumeFuel("climb");
            }
        }
        else if (CurrentState == EnemyState.Retreating)
        {
            // Climb to escape if possible
            if (Altitude < 3)
            {
                Altitude++;
                ConsumeFuel("climb");
            }
        }
        else if (CurrentState == EnemyState.Patrolling)
        {
            // Return to preferred altitude
            if (Altitude < PreferredAltitude && Altitude < 3)
            {
                Altitude++;
                ConsumeFuel("climb");
            }
            else if (Altitude > PreferredAltitude && Altitude > 0)
            {
                Altitude--;
            }
        }
    }

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
            HandleOutOfFuel();
        }
    }

    protected void HandleOutOfFuel()
    {
        Health -= 1;
        
        if (Altitude > 0)
        {
            Altitude--;
        }
        else
        {
            Health = 0;
        }
    }

    public virtual bool CanDetectPlayer(int playerX, int playerY, int playerAltitude)
    {
        double distance = Math.Sqrt(Math.Pow(X - playerX, 2) + Math.Pow(Y - playerY, 2));
        int altitudeDifference = Math.Abs(Altitude - playerAltitude);
        
        // Advanced radar can detect at longer range
        int effectiveRange = HasAdvancedRadar ? DetectionRange + 3 : DetectionRange;
        
        // Altitude difference affects detection
        if (altitudeDifference > 1) effectiveRange -= 2;
        
        return distance <= effectiveRange;
    }
}

class F16Falcon : EnemyJet
{
    private readonly JetFighterGame game;
    private Random random = new Random();
    private int lastPlayerX = -1, lastPlayerY = -1;
    private int turnsWithoutContact = 0;

    public F16Falcon(int x, int y, JetFighterGame game) : base(x, y)
    {
        this.game = game;
        AircraftType = AircraftType.F16_Falcon;
        AircraftName = "F-16 Fighting Falcon";
        Health = 3;
        MaxHealth = 3;
        Symbol = 'F';
        MaxVelocity = 2.5;
        TurnRate = 50;
        PreferredAltitude = 2; // Medium altitude fighter
        CombatExperience = 3;
        HasAdvancedRadar = true;
        DetectionRange = 7;
    }

    public override int ScoreValue => 150;

    public override void Move(int playerX, int playerY, char[,] grid)
    {
        bool playerDetected = CanDetectPlayer(playerX, playerY, game.PlayerAltitude);
        
        if (playerDetected)
        {
            lastPlayerX = playerX;
            lastPlayerY = playerY;
            turnsWithoutContact = 0;
            
            double distance = Math.Sqrt(Math.Pow(X - playerX, 2) + Math.Pow(Y - playerY, 2));
            
            if (Health < MaxHealth * 0.3)
            {
                UpdateState(EnemyState.Retreating);
            }
            else if (distance <= 3)
            {
                UpdateState(EnemyState.Flanking);
            }
            else
            {
                UpdateState(EnemyState.Chasing);
            }
        }
        else
        {
            turnsWithoutContact++;
            if (turnsWithoutContact > 5)
            {
                UpdateState(EnemyState.Patrolling);
            }
        }

        ManageAltitude(game.PlayerAltitude);
        ExecuteManeuver(playerX, playerY, grid);
        ConsumeFuel("regular");
    }

    private void ExecuteManeuver(int playerX, int playerY, char[,] grid)
    {
        switch (CurrentState)
        {
            case EnemyState.Patrolling:
                AdvancedPatrol(grid);
                break;
            case EnemyState.Chasing:
                PursuitManeuver(playerX, playerY, grid);
                break;
            case EnemyState.Retreating:
                TacticalRetreat(playerX, playerY, grid);
                break;
            case EnemyState.Flanking:
                FlankingManeuver(playerX, playerY, grid);
                break;
        }
    }

    private void AdvancedPatrol(char[,] grid)
    {
        if (lastPlayerX != -1 && lastPlayerY != -1)
        {
            // Search near last known position
            var step = game.AStarStep(X, Y, lastPlayerX, lastPlayerY, grid);
            if (step != null && random.NextDouble() < 0.7)
            {
                X = step.Value.nextX;
                Y = step.Value.nextY;
                return;
            }
        }
        
        // Random patrol with some intelligence
        RandomMove(grid);
    }

    private void PursuitManeuver(int playerX, int playerY, char[,] grid)
    {
        // Use afterburner occasionally for pursuit
        if (random.NextDouble() < 0.3)
        {
            ConsumeFuel("afterburner");
        }
        
        var step = game.AStarStep(X, Y, playerX, playerY, grid);
        if (step != null)
        {
            X = step.Value.nextX;
            Y = step.Value.nextY;
        }
    }

    private void TacticalRetreat(int playerX, int playerY, char[,] grid)
    {
        // Move away from player while trying to gain altitude advantage
        int dx = X - playerX;
        int dy = Y - playerY;
        if (dx != 0) dx = dx > 0 ? 1 : -1;
        if (dy != 0) dy = dy > 0 ? 1 : -1;
        
        int newX = Math.Max(0, Math.Min(grid.GetLength(0) - 1, X + dx));
        int newY = Math.Max(0, Math.Min(grid.GetLength(1) - 1, Y + dy));
        
        X = newX;
        Y = newY;
    }

    private void FlankingManeuver(int playerX, int playerY, char[,] grid)
    {
        // Try to move to player's flank
        int[] flankX = { playerX - 2, playerX + 2, playerX - 1, playerX + 1 };
        int[] flankY = { playerY - 1, playerY + 1, playerY - 2, playerY + 2 };
        
        for (int i = 0; i < flankX.Length; i++)
        {
            if (flankX[i] >= 0 && flankX[i] < grid.GetLength(0) &&
                flankY[i] >= 0 && flankY[i] < grid.GetLength(1))
            {
                var step = game.AStarStep(X, Y, flankX[i], flankY[i], grid);
                if (step != null)
                {
                    X = step.Value.nextX;
                    Y = step.Value.nextY;
                    return;
                }
            }
        }
        
        // Fallback to pursuit
        PursuitManeuver(playerX, playerY, grid);
    }

    private void RandomMove(char[,] grid)
    {
        int direction = random.Next(0, 8);
        int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
        int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };
        int newX = Math.Max(0, Math.Min(grid.GetLength(0) - 1, X + dx[direction]));
        int newY = Math.Max(0, Math.Min(grid.GetLength(1) - 1, Y + dy[direction]));
        X = newX;
        Y = newY;
    }
}

class Su27Flanker : EnemyJet
{
    private readonly JetFighterGame game;
    private Random random = new Random();
    private int aggressionLevel = 5; // 1-10 scale

    public Su27Flanker(int x, int y, JetFighterGame game) : base(x, y)
    {
        this.game = game;
        AircraftType = AircraftType.Su27_Flanker;
        AircraftName = "Su-27 Flanker";
        Health = 5;
        MaxHealth = 5;
        Symbol = 'S';
        MaxVelocity = 3.5;
        TurnRate = 55;
        PreferredAltitude = 3; // High altitude interceptor
        CombatExperience = 4;
        HasAdvancedRadar = true;
        DetectionRange = 9;
    }

    public override int ScoreValue => 250;

    public override void Move(int playerX, int playerY, char[,] grid)
    {
        bool playerDetected = CanDetectPlayer(playerX, playerY, game.PlayerAltitude);
        
        if (playerDetected)
        {
            double distance = Math.Sqrt(Math.Pow(X - playerX, 2) + Math.Pow(Y - playerY, 2));
            
            if (Health < MaxHealth * 0.4)
            {
                UpdateState(EnemyState.Retreating);
                aggressionLevel = Math.Max(1, aggressionLevel - 1);
            }
            else if (distance > 8)
            {
                UpdateState(EnemyState.Chasing);
            }
            else if (aggressionLevel > 6)
            {
                UpdateState(EnemyState.Diving); // Aggressive diving attack
            }
            else
            {
                UpdateState(EnemyState.Flanking);
            }
        }
        else
        {
            UpdateState(EnemyState.Patrolling);
        }

        ManageAltitude(game.PlayerAltitude);
        ExecuteAdvancedManeuver(playerX, playerY, grid);
        ConsumeFuel("regular");
    }

    private void ExecuteAdvancedManeuver(int playerX, int playerY, char[,] grid)
    {
        switch (CurrentState)
        {
            case EnemyState.Patrolling:
                HighAltitudePatrol(grid);
                break;
            case EnemyState.Chasing:
                InterceptorPursuit(playerX, playerY, grid);
                break;
            case EnemyState.Retreating:
                EvasiveRetreat(playerX, playerY, grid);
                break;
            case EnemyState.Diving:
                DivingAttack(playerX, playerY, grid);
                break;
            case EnemyState.Flanking:
                FlankingManeuver(playerX, playerY, grid);
                break;
        }
    }

    private void HighAltitudePatrol(char[,] grid)
    {
        // Maintain high altitude and wide patrol pattern
        if (random.NextDouble() < 0.6)
        {
            RandomMove(grid);
        }
    }

    private void InterceptorPursuit(int playerX, int playerY, char[,] grid)
    {
        // Use afterburner for high-speed pursuit
        if (random.NextDouble() < 0.4)
        {
            ConsumeFuel("afterburner");
        }
        
        var step = game.AStarStep(X, Y, playerX, playerY, grid);
        if (step != null)
        {
            X = step.Value.nextX;
            Y = step.Value.nextY;
        }
    }

    private void EvasiveRetreat(int playerX, int playerY, char[,] grid)
    {
        // Complex evasive maneuvers while retreating
        int dx = X - playerX;
        int dy = Y - playerY;
        
        // Add some randomness to make retreat less predictable
        dx += random.Next(-1, 2);
        dy += random.Next(-1, 2);
        
        if (dx != 0) dx = dx > 0 ? 1 : -1;
        if (dy != 0) dy = dy > 0 ? 1 : -1;
        
        int newX = Math.Max(0, Math.Min(grid.GetLength(0) - 1, X + dx));
        int newY = Math.Max(0, Math.Min(grid.GetLength(1) - 1, Y + dy));
        
        X = newX;
        Y = newY;
    }

    private void DivingAttack(int playerX, int playerY, char[,] grid)
    {
        // Aggressive diving attack - move directly toward player
        var step = game.AStarStep(X, Y, playerX, playerY, grid);
        if (step != null)
        {
            X = step.Value.nextX;
            Y = step.Value.nextY;
        }
        
        // Lose altitude during dive
        if (Altitude > 0 && random.NextDouble() < 0.3)
        {
            Altitude--;
        }
    }

    private void FlankingManeuver(int playerX, int playerY, char[,] grid)
    {
        // Similar to F16 flanking but more aggressive
        int[] flankX = { playerX - 3, playerX + 3, playerX - 2, playerX + 2 };
        int[] flankY = { playerY - 2, playerY + 2, playerY - 3, playerY + 3 };
        
        for (int i = 0; i < flankX.Length; i++)
        {
            if (flankX[i] >= 0 && flankX[i] < grid.GetLength(0) &&
                flankY[i] >= 0 && flankY[i] < grid.GetLength(1))
            {
                var step = game.AStarStep(X, Y, flankX[i], flankY[i], grid);
                if (step != null)
                {
                    X = step.Value.nextX;
                    Y = step.Value.nextY;
                    return;
                }
            }
        }
        
        InterceptorPursuit(playerX, playerY, grid);
    }

    private void RandomMove(char[,] grid)
    {
        int direction = random.Next(0, 8);
        int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
        int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };
        int newX = Math.Max(0, Math.Min(grid.GetLength(0) - 1, X + dx[direction]));
        int newY = Math.Max(0, Math.Min(grid.GetLength(1) - 1, Y + dy[direction]));
        X = newX;
        Y = newY;
    }
}

class F22Raptor : EnemyJet
{
    private readonly JetFighterGame game;
    private Random random = new Random();
    private bool stealthMode = true;
    private int stealthCooldown = 0;

    public F22Raptor(int x, int y, JetFighterGame game) : base(x, y)
    {
        this.game = game;
        AircraftType = AircraftType.F22_Raptor;
        AircraftName = "F-22 Raptor";
        Health = 4;
        MaxHealth = 4;
        Symbol = 'R';
        MaxVelocity = 3.2;
        TurnRate = 65;
        PreferredAltitude = 3; // High altitude stealth fighter
        CombatExperience = 5;
        HasAdvancedRadar = true;
        DetectionRange = 8;
    }

    public override int ScoreValue => 300;

    public override void Move(int playerX, int playerY, char[,] grid)
    {
        ManageStealthMode();
        
        bool playerDetected = CanDetectPlayer(playerX, playerY, game.PlayerAltitude);
        
        if (playerDetected)
        {
            double distance = Math.Sqrt(Math.Pow(X - playerX, 2) + Math.Pow(Y - playerY, 2));
            
            if (Health < MaxHealth * 0.5)
            {
                UpdateState(EnemyState.Retreating);
                stealthMode = true; // Activate stealth when retreating
            }
            else if (distance <= 4 && stealthMode)
            {
                UpdateState(EnemyState.Diving); // Stealth attack
            }
            else
            {
                UpdateState(EnemyState.Chasing);
            }
        }
        else
        {
            UpdateState(EnemyState.Patrolling);
        }

        ManageAltitude(game.PlayerAltitude);
        ExecuteStealthManeuver(playerX, playerY, grid);
        ConsumeFuel("regular");
    }

    private void ManageStealthMode()
    {
        if (stealthCooldown > 0)
        {
            stealthCooldown--;
            return;
        }

        // Toggle stealth based on situation
        if (CurrentState == EnemyState.Retreating || 
            (CurrentState == EnemyState.Patrolling && random.NextDouble() < 0.3))
        {
            stealthMode = true;
        }
        else if (CurrentState == EnemyState.Chasing && random.NextDouble() < 0.2)
        {
            stealthMode = false; // Sometimes turn off stealth for attack
            stealthCooldown = 3; // Cooldown before next stealth activation
        }
    }

    public override bool CanDetectPlayer(int playerX, int playerY, int playerAltitude)
    {
        // F-22 has reduced detection range when in stealth mode
        double distance = Math.Sqrt(Math.Pow(X - playerX, 2) + Math.Pow(Y - playerY, 2));
        int altitudeDifference = Math.Abs(Altitude - playerAltitude);
        
        int effectiveRange = stealthMode ? DetectionRange - 2 : DetectionRange + 3;
        
        if (altitudeDifference > 1) effectiveRange -= 2;
        
        return distance <= effectiveRange;
    }

    private void ExecuteStealthManeuver(int playerX, int playerY, char[,] grid)
    {
        switch (CurrentState)
        {
            case EnemyState.Patrolling:
                StealthPatrol(grid);
                break;
            case EnemyState.Chasing:
                StealthPursuit(playerX, playerY, grid);
                break;
            case EnemyState.Retreating:
                StealthRetreat(playerX, playerY, grid);
                break;
            case EnemyState.Diving:
                StealthAttack(playerX, playerY, grid);
                break;
        }
    }

    private void StealthPatrol(char[,] grid)
    {
        // Unpredictable movement pattern
        if (random.NextDouble() < 0.4)
        {
            RandomMove(grid);
        }
    }

    private void StealthPursuit(int playerX, int playerY, char[,] grid)
    {
        // Approach from unexpected angles
        if (random.NextDouble() < 0.3)
        {
            // Indirect approach
            int offsetX = random.Next(-2, 3);
            int offsetY = random.Next(-2, 3);
            int targetX = Math.Max(0, Math.Min(grid.GetLength(0) - 1, playerX + offsetX));
            int targetY = Math.Max(0, Math.Min(grid.GetLength(1) - 1, playerY + offsetY));
            
            var step = game.AStarStep(X, Y, targetX, targetY, grid);
            if (step != null)
            {
                X = step.Value.nextX;
                Y = step.Value.nextY;
            }
        }
        else
        {
            // Direct approach
            var step = game.AStarStep(X, Y, playerX, playerY, grid);
            if (step != null)
            {
                X = step.Value.nextX;
                Y = step.Value.nextY;
            }
        }
    }

    private void StealthRetreat(int playerX, int playerY, char[,] grid)
    {
        // Evasive retreat with stealth
        int dx = X - playerX;
        int dy = Y - playerY;
        
        // Add stealth evasion
        dx += random.Next(-2, 3);
        dy += random.Next(-2, 3);
        
        if (dx != 0) dx = dx > 0 ? 1 : -1;
        if (dy != 0) dy = dy > 0 ? 1 : -1;
        
        int newX = Math.Max(0, Math.Min(grid.GetLength(0) - 1, X + dx));
        int newY = Math.Max(0, Math.Min(grid.GetLength(1) - 1, Y + dy));
        
        X = newX;
        Y = newY;
    }

    private void StealthAttack(int playerX, int playerY, char[,] grid)
    {
        // Sudden stealth attack - direct approach
        var step = game.AStarStep(X, Y, playerX, playerY, grid);
        if (step != null)
        {
            X = step.Value.nextX;
            Y = step.Value.nextY;
        }
    }

    private void RandomMove(char[,] grid)
    {
        int direction = random.Next(0, 8);
        int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
        int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };
        int newX = Math.Max(0, Math.Min(grid.GetLength(0) - 1, X + dx[direction]));
        int newY = Math.Max(0, Math.Min(grid.GetLength(1) - 1, Y + dy[direction]));
        X = newX;
        Y = newY;
    }

    public bool IsInStealthMode() => stealthMode;
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
    private char playerJet = 'P';
    private int playerHealth = 5;
    private int score = 0;
    private List<Mission> activeMissions = new List<Mission>();
    private List<Mission> completedMissions = new List<Mission>();
    private int missionCounter = 0;
    private Queue<string> messageQueue = new Queue<string>(); // Message queue for delayed display

    private int _playerDamage = 1;
    public int PlayerDamage 
    { 
        get => _playerDamage; 
        set => _playerDamage = value; 
    }
    private List<EnemyJet> enemyJets;
    private Random random;
    private Weather currentWeather = Weather.Clear;
    private int weatherTurns = 0; // Track how long current weather has persisted
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
    public int PlayerAltitude => playerAltitude; // Public property to access player altitude
    public int Fuel { get; set; }
    public int MaxFuel { get; protected set; }

    private bool afterburnerEnabled = false;
    private List<(int, int)> basePositions = new List<(int, int)>();

    // Message system methods
    private void AddMessage(string message, ConsoleColor color = ConsoleColor.White, bool urgent = false)
    {
        messageQueue.Enqueue($"{color}|{message}");
        if (urgent)
        {
            DisplayMessages();
        }
    }

    private void DisplayMessages()
    {
        while (messageQueue.Count > 0)
        {
            var message = messageQueue.Dequeue();
            var parts = message.Split('|');
            if (parts.Length == 2 && Enum.TryParse<ConsoleColor>(parts[0], out var color))
            {
                Console.ForegroundColor = color;
                Console.WriteLine($">>> {parts[1]}");
                Console.ResetColor();
                System.Threading.Thread.Sleep(800); // Pause for dramatic effect
            }
        }
    }

    private void DisplayTypingMessage(string message, ConsoleColor color = ConsoleColor.White, int delayMs = 30)
    {
        Console.ForegroundColor = color;
        Console.Write(">>> ");
        foreach (char c in message)
        {
            Console.Write(c);
            System.Threading.Thread.Sleep(delayMs);
        }
        Console.WriteLine();
        Console.ResetColor();
        System.Threading.Thread.Sleep(500);
    }

    private void WaitForInput(string prompt = "Press any key to continue...")
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"\n{prompt}");
        Console.ResetColor();
        Console.ReadKey(true);
    }

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

        // Place F-16 Falcon
        (int x, int y) = GenerateRandomPosition();
        enemyJets.Add(new F16Falcon(x, y, this));
        grid[x, y] = 'F';

        // Place Su-27 Flanker
        (x, y) = GenerateRandomPosition();
        enemyJets.Add(new Su27Flanker(x, y, this));
        grid[x, y] = 'S';

        // Place F-22 Raptor
        (x, y) = GenerateRandomPosition();
        enemyJets.Add(new F22Raptor(x, y, this));
        grid[x, y] = 'R';
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
        
        // Show compact radar information with better formatting
        Console.WriteLine("┌─ RADAR STATUS ─────────────────────────────────────────────────────┐");
        var detectedEnemies = enemyJets.Where(e => e.IsDetected).ToList();
        if (detectedEnemies.Any())
        {
            foreach (var enemy in detectedEnemies)
            {
                string stealthInfo = "";
                if (enemy is F22Raptor raptor && raptor.IsInStealthMode())
                    stealthInfo = " [STEALTH]";
                    
                Console.WriteLine($"│ {enemy.AircraftName} at ({enemy.X:D2},{enemy.Y:D2}) Alt:{enemy.Altitude} HP:{enemy.Health}/{enemy.MaxHealth} {enemy.CurrentState}{stealthInfo}");
            }
        }
        else
        {
            Console.WriteLine("│ No enemy contacts detected");
        }
        Console.WriteLine("└────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
        
        // Show grid with better formatting
        Console.WriteLine("    " + string.Join("", Enumerable.Range(0, gridSize).Select(i => (i % 10).ToString() + " ")));
        for (int i = 0; i < gridSize; i++)
        {
            Console.Write($"{i:D2}: ");
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
                        // Color based on enemy type and stealth status
                        if (enemy is F22Raptor raptor && raptor.IsInStealthMode())
                        {
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                        }
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
        Console.WriteLine();
        
        // Compact status display
        Console.WriteLine($"Health: {playerHealth}  Score: {score}  Alt: {playerAltitude}  Weather: {currentWeather}");
        
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
        
        Console.WriteLine($"  Afterburner: {(afterburnerEnabled ? "ON" : "OFF")}  Weapons: M{missileAmmo} G{gunAmmo}  DMG: x{PlayerDamage}");
        
        // Compact controls display
        Console.WriteLine("\nControls: w/a/s/d/q/e/z/c=move | r=refuel | b=afterburner | u/j=altitude | v/l=save/load | m=missions");
        
        // Compact legend
        Console.WriteLine("Legend: P=You | F=F-16 | S=Su-27 | R=F-22 | B=Base | T=Tanker");
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
        Console.Clear();
        DisplayTypingMessage($"🚨 ENEMY CONTACT! {enemy.AircraftName} detected at {enemy.X},{enemy.Y}!", ConsoleColor.Red);
        
        double distance = Math.Sqrt(Math.Pow(enemy.X - playerX, 2) + Math.Pow(enemy.Y - playerY, 2));
        string distanceDesc = distance switch
        {
            <= 2 => "POINT BLANK RANGE",
            <= 4 => "Close range",
            <= 8 => "Medium range",
            _ => "Long range"
        };
        
        DisplayTypingMessage($"Distance: {distance:F1} units ({distanceDesc})", ConsoleColor.Yellow);
        DisplayTypingMessage($"Enemy Status: {enemy.Health}/{enemy.MaxHealth} HP | State: {enemy.CurrentState}", ConsoleColor.Cyan);
        
        if (enemy.CurrentState == EnemyState.Chasing)
            DisplayTypingMessage("WARNING: Enemy is actively pursuing!", ConsoleColor.Red);
        else if (enemy.CurrentState == EnemyState.Flanking)
            DisplayTypingMessage("CAUTION: Enemy attempting flanking maneuver!", ConsoleColor.Yellow);
        
        DisplayCombatOptions(enemy);
        
        string? action = Console.ReadLine();
        switch (action?.ToLower())
        {
            case "1": // Fire missile
                if (TryFireWeapon("Missile", enemy, 7))
                {
                    DisplayTypingMessage("🚀 MISSILE AWAY! Tracking target...", ConsoleColor.Yellow, 50);
                    System.Threading.Thread.Sleep(1000);
                }
                break;
            case "2": // Gun attack
                if (TryFireWeapon("Gun", enemy, 3))
                {
                    DisplayTypingMessage("💥 GUNS GUNS GUNS! Engaging with cannon!", ConsoleColor.Red, 40);
                    System.Threading.Thread.Sleep(800);
                }
                break;
            case "3": // Evasive maneuver
                DisplayTypingMessage("🌪️ Executing evasive maneuvers!", ConsoleColor.Green, 40);
                PerformEvasiveManeuver();
                break;
            default:
                DisplayTypingMessage("❌ Combat opportunity missed! No action taken.", ConsoleColor.DarkRed);
                break;
        }
        
        // Enemy counterattack with realistic factors
        if (enemy.Health > 0)
        {
            System.Threading.Thread.Sleep(500);
            DisplayTypingMessage("⚠️ Enemy counterattack incoming!", ConsoleColor.Red);
            EnemyAttack(enemy);
        }
        else
        {
            DisplayTypingMessage($"💀 {enemy.AircraftName} DESTROYED! Excellent shooting, pilot!", ConsoleColor.Green, 40);
            DisplayTypingMessage($"🏆 +{enemy.ScoreValue} points earned", ConsoleColor.Yellow);
            
            enemyJets.Remove(enemy);
            score += enemy.ScoreValue;
            
            // Random chance for power-up when defeating enemy
            if (random.NextDouble() < 0.25) // 25% chance
            {
                System.Threading.Thread.Sleep(500);
                PowerUp();
            }
            
            if (enemyJets.Count == 0)
            {
                currentState = GameState.Victory;
                DisplayTypingMessage("🎉 ALL ENEMIES NEUTRALIZED! MISSION ACCOMPLISHED!", ConsoleColor.Green, 50);
            }
        }
        
        WaitForInput();
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

        // Clear enemy positions from grid before moving
        foreach (var jet in enemyJets)
        {
            if (grid[jet.X, jet.Y] == jet.Symbol)
                grid[jet.X, jet.Y] = '.';
        }

        // Move each enemy and check for collisions
        foreach (var jet in enemyJets.ToList())
        {
            int oldX = jet.X;
            int oldY = jet.Y;
            
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
            
            // Update grid with new enemy position (only if enemy is still alive)
            if (enemyJets.Contains(jet))
            {
                // Make sure we don't overwrite the player position
                if (!(jet.X == playerX && jet.Y == playerY))
                grid[jet.X, jet.Y] = jet.Symbol;
            }
        }

        // Check for coordinated attacks from adjacent enemies
        var adjacentEnemies = enemyJets.FindAll(e => Math.Abs(e.X - playerX) <= 1 && Math.Abs(e.Y - playerY) <= 1);
        
        if (adjacentEnemies.Count >= 2)
        {
            int totalDamage = adjacentEnemies.Sum(e =>
            {
                int baseDamage = random.Next(1, 4); // Reduced from 1-6 to be less punishing
                bool isCrit = random.NextDouble() < 0.15; // Reduced crit chance
                return isCrit ? baseDamage * 2 : baseDamage;
            });
            
            DisplayTypingMessage("🚨 MULTIPLE BOGEYS! Coordinated enemy attack detected!", ConsoleColor.Red, 50);
            DisplayTypingMessage($"💥 Combined assault deals {totalDamage} damage!", ConsoleColor.Red);
            
            playerHealth -= totalDamage;
            if (playerHealth <= 0)
            {
                currentState = GameState.Defeat;
                HandleGameOver();
                return;
            }
            
            WaitForInput("Multiple enemies engaged! Press any key to continue...");
        }
        
        // Display any queued messages
        if (messageQueue.Count > 0)
        {
            DisplayMessages();
        }
        
        // Ensure player position is correctly marked
        if (grid[playerX, playerY] != playerJet)
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

        // F-22 Raptor in stealth mode is harder to detect
        if (enemy is F22Raptor raptor && raptor.IsInStealthMode())
            detectionRange = 3;
        
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
            Weather newWeather = (Weather)random.Next(0, 3);
            if (newWeather != currentWeather)
            {
                currentWeather = newWeather;
                weatherTurns = 0;
                
                string weatherMessage = currentWeather switch
                {
                    Weather.Storm => "⛈️ STORM FRONT MOVING IN! Visibility severely reduced!",
                    Weather.Cloudy => "☁️ Cloud cover increasing. Reduced visibility conditions.",
                    Weather.Clear => "☀️ Weather clearing up. Optimal visibility restored."
                };
                
                AddMessage(weatherMessage, ConsoleColor.Yellow, true);
                
                // Additional weather effects messaging
                if (currentWeather == Weather.Storm)
                {
                    AddMessage("⚠️ Storm conditions: -50% detection range, -30% accuracy!", ConsoleColor.Red);
                }
                else if (currentWeather == Weather.Cloudy)
                {
                    AddMessage("⚠️ Cloudy conditions: -20% detection range, -10% accuracy.", ConsoleColor.Yellow);
                }
            }
        }
        
        weatherTurns++; // Increment weather persistence counter
        
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

    public void ChangeAltitude(bool increase)
    {
        if (increase && playerAltitude < 3)
        {
            playerAltitude++;
            ConsumeFuel("climb");
            
            string altitudeDesc = playerAltitude switch
            {
                1 => "Low altitude (1000ft)",
                2 => "Medium altitude (5000ft)", 
                3 => "High altitude (10000ft)",
                _ => $"Altitude level {playerAltitude}"
            };
            
            DisplayTypingMessage($"⬆️ Climbing to {altitudeDesc}", ConsoleColor.Cyan);
            DisplayTypingMessage("✈️ Engine power increased - fuel consumption higher during climb.", ConsoleColor.Yellow);
        }
        else if (!increase && playerAltitude > 0)
        {
            string currentDesc = playerAltitude switch
            {
                3 => "High altitude (10000ft)",
                2 => "Medium altitude (5000ft)",
                1 => "Low altitude (1000ft)",
                _ => $"Altitude level {playerAltitude}"
            };
            
            playerAltitude--;
            
            string newDesc = playerAltitude switch
            {
                2 => "Medium altitude (5000ft)",
                1 => "Low altitude (1000ft)", 
                0 => "Ground level",
                _ => $"Altitude level {playerAltitude}"
            };
            
            DisplayTypingMessage($"⬇️ Descending from {currentDesc} to {newDesc}", ConsoleColor.Green);
            if (playerAltitude == 0)
                DisplayTypingMessage("⚠️ WARNING: At ground level - limited maneuverability!", ConsoleColor.Red);
        }
        else
        {
            if (increase)
                DisplayTypingMessage("⚠️ Maximum service ceiling reached! Cannot climb higher.", ConsoleColor.Yellow);
            else
                DisplayTypingMessage("⚠️ Already at minimum safe altitude!", ConsoleColor.Yellow);
        }
    }

    private void GenerateNewMission()
    {
        string[] missionTypes = {
            "Destroy an enemy jet",
            "Visit all bases", 
            "Reach maximum altitude",
            "Perform mid-air refueling",
            "Survive a storm",
            "Defeat a stealth aircraft",
            "High altitude combat",
            "Maintain air superiority",
            "Reconnaissance mission"
        };
        
        int rewardPoints = random.Next(100, 301); // 100-300 points
        string objective = missionTypes[random.Next(missionTypes.Length)];
        string missionName = $"Mission #{++missionCounter}: {objective}";
        
        Mission newMission = new Mission(missionName, rewardPoints);
        activeMissions.Add(newMission);
        
        DisplayTypingMessage($"📡 NEW MISSION BRIEFING RECEIVED!", ConsoleColor.Cyan, 40);
        DisplayTypingMessage($"🎯 {missionName}", ConsoleColor.White);
        DisplayTypingMessage($"💰 Mission reward: {rewardPoints} points", ConsoleColor.Yellow);
        DisplayTypingMessage("📋 Check mission status with 'm' command", ConsoleColor.Green);
    }

    public void DisplayMissions()
    {
        Console.Clear();
        DisplayTypingMessage("📋 MISSION BRIEFING SYSTEM", ConsoleColor.Cyan, 40);
        Console.WriteLine();
        
        Console.WriteLine("┌─ ACTIVE MISSIONS ──────────────────────────────────────────────────┐");
        if (activeMissions.Count == 0)
        {
            Console.WriteLine("│ No active missions assigned.                                       │");
        }
        else
        {
            foreach (var mission in activeMissions)
            {
                Console.WriteLine($"│ 🎯 {mission.Objective,-50} Reward: {mission.RewardPoints,4} pts │");
            }
        }
        Console.WriteLine("└─────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
        
        Console.WriteLine("┌─ COMPLETED MISSIONS ───────────────────────────────────────────────┐");
        if (completedMissions.Count == 0)
        {
            Console.WriteLine("│ No completed missions.                                             │");
        }
        else
        {
            foreach (var mission in completedMissions)
            {
                Console.WriteLine($"│ ✅ {mission.Objective,-50} Reward: {mission.RewardPoints,4} pts │");
            }
        }
        Console.WriteLine("└─────────────────────────────────────────────────────────────────────┘");
        
        WaitForInput();
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

    public void CheckMissionProgress()
    {
        foreach (var mission in activeMissions.ToList())
        {
            // Check mission completion based on objective
            bool completed = false;
            
            if (mission.Objective.Contains("Destroy an enemy jet"))
            {
                // Check if any enemy has been destroyed (started with 3)
                completed = enemyJets.Count < 3;
            }
            else if (mission.Objective.Contains("Visit all bases"))
            {
                // Check if player is currently at any base
                completed = basePositions.Any(basePos => playerX == basePos.Item1 && playerY == basePos.Item2);
            }
            else if (mission.Objective.Contains("Reach maximum altitude"))
            {
                completed = playerAltitude == 3;
            }
            else if (mission.Objective.Contains("Perform mid-air refueling"))
            {
                // Check if adjacent to a tanker (T)
                for (int dx = -1; dx <= 1 && !completed; dx++)
                {
                    for (int dy = -1; dy <= 1 && !completed; dy++)
                    {
                        int nx = playerX + dx;
                        int ny = playerY + dy;
                        
                        if (nx >= 0 && nx < gridSize && 
                            ny >= 0 && ny < gridSize && 
                            grid[nx, ny] == 'T')
                        {
                            completed = true;
                        }
                    }
                }
            }
            else if (mission.Objective.Contains("Survive a storm"))
            {
                // Must survive for multiple turns in a storm
                completed = currentWeather == Weather.Storm && weatherTurns >= 5;
            }
            else if (mission.Objective.Contains("Defeat a stealth aircraft"))
            {
                // Check if F-22 Raptor has been destroyed
                completed = !enemyJets.Any(e => e is F22Raptor);
            }
            else if (mission.Objective.Contains("High altitude combat"))
            {
                // Complete combat at altitude 3
                completed = playerAltitude == 3 && 
                           enemyJets.Any(e => Math.Abs(e.X - playerX) <= 2 && Math.Abs(e.Y - playerY) <= 2);
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
        
        DisplayTypingMessage("🎯 MISSION ACCOMPLISHED!", ConsoleColor.Green, 50);
        DisplayTypingMessage($"✅ {mission.Objective}", ConsoleColor.White);
        DisplayTypingMessage($"💰 Mission reward: {mission.RewardPoints} points", ConsoleColor.Yellow);
        
        score += mission.RewardPoints;
        
        // Maybe generate a new mission
        if (activeMissions.Count < 3 && random.NextDouble() < 0.7) // 70% chance
        {
            System.Threading.Thread.Sleep(1000);
            GenerateNewMission();
        }
        
        WaitForInput();
    }

    private bool TryFireWeapon(string weaponType, EnemyJet enemy, int baseDamage)
    {
        int ammo = weaponType == "Missile" ? missileAmmo : gunAmmo;
        if (ammo <= 0)
        {
            DisplayTypingMessage($"❌ OUT OF {weaponType.ToUpper()} AMMO! Weapon dry!", ConsoleColor.Red);
            return false;
        }
        
        // Calculate hit chance based on weather, altitude and distance
        double hitChance = 0.65 * accuracyModifier;
        double distance = Math.Sqrt(Math.Pow(enemy.X - playerX, 2) + Math.Pow(enemy.Y - playerY, 2));
        
        // Adjust hit chance based on distance
        if (distance > 5) hitChance -= 0.15;
        if (distance > 8) hitChance -= 0.15;
        
        // Stealth/advanced enemies are harder to hit
        if (enemy is F22Raptor) hitChance -= 0.25;
        else if (enemy.CombatExperience > 3) hitChance -= 0.1;
        
        // Display targeting information
        DisplayTypingMessage($"🎯 Targeting {enemy.AircraftName}...", ConsoleColor.Cyan, 20);
        DisplayTypingMessage($"Hit probability: {hitChance * 100:F0}%", ConsoleColor.Yellow);
        
        // Roll for hit
        if (random.NextDouble() <= hitChance)
        {
            // Use playerDamage as a multiplier for weapon damage
            int baseDmg = weaponType == "Missile" ? random.Next(4, 7) : random.Next(1, 3);
            int damage = baseDmg * PlayerDamage;
            enemy.Health -= damage;
            
            string hitMessage = weaponType == "Missile" ? 
                $"💥 DIRECT HIT! Missile impact confirmed! {damage} damage dealt!" :
                $"💥 TARGET HIT! Cannon rounds on target! {damage} damage dealt!";
            
            DisplayTypingMessage(hitMessage, ConsoleColor.Green, 30);
            
            if (damage > baseDmg * 1.5) // High damage
                DisplayTypingMessage("🔥 CRITICAL DAMAGE! Excellent marksmanship!", ConsoleColor.Yellow);
            
            // Reduce ammo
            if (weaponType == "Missile")
                missileAmmo--;
            else
                gunAmmo -= 10;
            
            return true;
        }
        else
        {
            string missMessage = weaponType == "Missile" ? 
                "❌ MISSILE MISS! Target evaded - negative impact!" :
                "❌ SHOTS WIDE! Cannon fire ineffective!";
            
            DisplayTypingMessage(missMessage, ConsoleColor.Red);
            
            // Weather/conditions affecting accuracy
            if (currentWeather == Weather.Storm)
                DisplayTypingMessage("⛈️ Storm conditions affecting targeting accuracy!", ConsoleColor.DarkYellow);
            
            if (weaponType == "Missile")
                missileAmmo--;
            else
                gunAmmo -= 5;
                
            return false;
        }
    }

    private void EnemyAttack(EnemyJet enemy)
    {
        // Base attack chance varies by aircraft type and experience
        double attackChance = 0.7 + (enemy.CombatExperience * 0.05);
        
        // F-22 Raptor has higher accuracy
        if (enemy is F22Raptor)
        {
            attackChance = 0.85;
            DisplayTypingMessage("⚠️ F-22 Raptor locking weapons systems!", ConsoleColor.Red);
        }
        // Su-27 Flanker is also highly accurate
        else if (enemy is Su27Flanker)
        {
            attackChance = 0.82;
            DisplayTypingMessage("⚠️ Su-27 Flanker engaging with missiles!", ConsoleColor.Red);
        }
        // F-16 Falcon has good accuracy
        else if (enemy is F16Falcon)
        {
            attackChance = 0.78;
            DisplayTypingMessage("⚠️ F-16 Fighting Falcon opening fire!", ConsoleColor.Red);
        }
        
        // Apply weather effects to attack chance
        attackChance *= accuracyModifier;
        
        System.Threading.Thread.Sleep(800); // Build tension
        
        if (random.NextDouble() <= attackChance)
        {
            // Damage varies by aircraft type and experience
            int baseDamage = enemy switch
            {
                F16Falcon => random.Next(2, 5),
                Su27Flanker => random.Next(3, 6),
                F22Raptor => random.Next(3, 7),
                _ => random.Next(1, 4)
            };
            
            // Critical hit chance based on experience
            bool isCrit = random.NextDouble() < (0.15 + enemy.CombatExperience * 0.02);
            int damage = isCrit ? baseDamage * 2 : baseDamage;
            
            if (isCrit)
            {
                DisplayTypingMessage($"💥💥 CRITICAL HIT! {enemy.AircraftName} scores devastating hit for {damage} damage!", ConsoleColor.Red, 40);
                DisplayTypingMessage("🚨 HULL BREACH! Critical systems damaged!", ConsoleColor.Red);
            }
            else
            {
                DisplayTypingMessage($"💥 ENEMY HIT! {enemy.AircraftName} deals {damage} damage!", ConsoleColor.Red, 30);
            }
            
            playerHealth -= damage;
            
            // Health status warnings
            if (playerHealth <= 1)
                DisplayTypingMessage("🆘 CRITICAL CONDITION! Immediate medical attention required!", ConsoleColor.Red, 50);
            else if (playerHealth <= 2)
                DisplayTypingMessage("⚠️ SEVERE DAMAGE! Hull integrity compromised!", ConsoleColor.Yellow);
            else if (playerHealth <= 3)
                DisplayTypingMessage("⚠️ MODERATE DAMAGE! Systems showing stress!", ConsoleColor.Yellow);
            
            if (playerHealth <= 0)
            {
                currentState = GameState.Defeat;
                DisplayTypingMessage("💀 AIRCRAFT DESTROYED! PILOT DOWN!", ConsoleColor.Red, 50);
            }
        }
        else
        {
            string[] missMessages = {
                $"✅ {enemy.AircraftName} misses! Shots wide of target!",
                $"✅ Evasive maneuvers successful! {enemy.AircraftName} attack ineffective!",
                $"✅ Lucky break! {enemy.AircraftName} weapons malfunction!",
                $"✅ Defensive systems work! {enemy.AircraftName} can't get a lock!"
            };
            DisplayTypingMessage(missMessages[random.Next(missMessages.Length)], ConsoleColor.Green);
        }
    }

    private void PerformEvasiveManeuver()
    {
        DisplayTypingMessage("🌪️ Executing barrel roll maneuver!", ConsoleColor.Green, 40);
        
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
            DisplayTypingMessage("✅ Evasive maneuver successful! New position acquired.", ConsoleColor.Green);
        }
        else
        {
            DisplayTypingMessage("⚠️ Limited maneuvering space! Maintaining current position.", ConsoleColor.Yellow);
        }
    }

    public void PowerUp()
    {
        PlayerDamage++;
        DisplayTypingMessage("🚀 POWER UP ACQUIRED!", ConsoleColor.Yellow, 50);
        DisplayTypingMessage($"⚡ Weapons systems upgraded! Damage multiplier: x{PlayerDamage}", ConsoleColor.Green, 30);
        DisplayTypingMessage("🔧 All weapon systems now operating at enhanced capacity!", ConsoleColor.Cyan);
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
        DisplayTypingMessage("🚨 WARNING: OUT OF FUEL! EMERGENCY LANDING REQUIRED!", ConsoleColor.Red, 50);
        
        // Forced descent
        if (playerAltitude > 0)
        {
            playerAltitude--;
            DisplayTypingMessage($"⬇️ Altitude dropping! Current altitude: {playerAltitude}", ConsoleColor.Yellow);
        }
        else
        {
            // Check if player is over a landing zone/base
            if (IsOverLandingZone(playerX, playerY))
            {
                DisplayTypingMessage("✅ Emergency landing successful!", ConsoleColor.Green);
                Refuel(50); // Partial refuel after emergency landing
            }
            else
            {
                // Crashed
                playerHealth -= 2;
                DisplayTypingMessage("💥 CRASH! Emergency landing on hostile terrain!", ConsoleColor.Red);
                
                if (playerHealth <= 0)
                {
                    currentState = GameState.Defeat;
                    HandleGameOver();
                }
            }
        }
        
        WaitForInput();
    }

    private bool IsOverLandingZone(int x, int y)
    {
        return grid[x, y] == 'B';
    }

    private void Refuel(int amount)
    {
        Fuel += amount;
        if (Fuel > MaxFuel)
            Fuel = MaxFuel;
        DisplayTypingMessage($"⛽ Refueled! Current fuel: {Fuel}/{MaxFuel}", ConsoleColor.Green);
    }

    public void ProcessPlayerAction(string action)
    {
        switch (action.ToLower())
        {
            case "r": // Refuel if over base or near tanker
                TryRefuel();
                break;
            case "b": // Afterburner
                ToggleAfterburner();
                break;
            case "v": // Save game
                SaveGame();
                break;
            case "l": // Load game
                LoadGame();
                break;
            case "m": // Show missions
                DisplayMissions();
                break;
        }
    }

    private void TryRefuel()
    {
        // Check if player is on an airbase
        if (basePositions.Contains((playerX, playerY)))
        {
            DisplayTypingMessage("🏭 Airbase refueling sequence initiated...", ConsoleColor.Cyan);
            System.Threading.Thread.Sleep(1000);
            DisplayTypingMessage("⛽ Fuel pumps engaged - full tank refuel in progress...", ConsoleColor.Yellow);
            Refuel(MaxFuel); // Full refuel
            DisplayTypingMessage("✅ Refueling complete! All systems ready for combat.", ConsoleColor.Green);
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
                    DisplayTypingMessage("✈️ Mid-air refueling tanker detected!", ConsoleColor.Cyan);
                    DisplayTypingMessage("🔗 Establishing fuel line connection...", ConsoleColor.Yellow);
                    System.Threading.Thread.Sleep(1500);
                    DisplayTypingMessage("⛽ Mid-air refueling in progress...", ConsoleColor.Yellow);
                    Refuel(MaxFuel / 2); // Half tank from mid-air refueling
                    DisplayTypingMessage("✅ Mid-air refueling complete! Disconnecting fuel line.", ConsoleColor.Green);
                    return;
                }
            }
        }
        
        DisplayTypingMessage("❌ No refueling sources detected in area!", ConsoleColor.Red);
        DisplayTypingMessage("🔍 Search for airbase (B) or tanker aircraft (T) nearby.", ConsoleColor.Yellow);
    }

    private void ToggleAfterburner()
    {
        afterburnerEnabled = !afterburnerEnabled;
        if (afterburnerEnabled)
        {
            DisplayTypingMessage("🔥 AFTERBURNER ENGAGED! Maximum thrust activated!", ConsoleColor.Red, 40);
            DisplayTypingMessage("⚠️ WARNING: Increased fuel consumption rate!", ConsoleColor.Yellow);
        }
        else
        {
            DisplayTypingMessage("🔥 Afterburner disengaged. Normal cruise thrust restored.", ConsoleColor.Green);
            DisplayTypingMessage("✅ Fuel consumption returned to normal levels.", ConsoleColor.White);
        }
    }

    public void SaveGame()
    {
        try
        {
            DisplayTypingMessage("💾 Initiating save sequence...", ConsoleColor.Cyan);
            
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
            
            DisplayTypingMessage("✅ Game saved successfully to flight recorder!", ConsoleColor.Green);
            DisplayTypingMessage($"📁 Save file: {savePath}", ConsoleColor.White);
        }
        catch (Exception ex)
        {
            DisplayTypingMessage($"❌ Save failed! Error: {ex.Message}", ConsoleColor.Red);
        }
        
        WaitForInput();
    }

    public bool LoadGame()
    {
        string savePath = "save.json";
        if (!File.Exists(savePath))
        {
            DisplayTypingMessage("❌ No saved mission found in flight recorder!", ConsoleColor.Red);
            WaitForInput();
            return false;
        }

        try
        {
            DisplayTypingMessage("📁 Loading saved mission from flight recorder...", ConsoleColor.Cyan);
            
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
                
                DisplayTypingMessage("✅ Mission data loaded successfully!", ConsoleColor.Green);
                DisplayTypingMessage($"📊 Health: {playerHealth} | Score: {score} | Fuel: {Fuel}", ConsoleColor.White);
                DisplayTypingMessage("🎯 Resuming combat operations...", ConsoleColor.Yellow);
            }
            else
            {
                DisplayTypingMessage("❌ Corrupted save data! Unable to load mission.", ConsoleColor.Red);
                WaitForInput();
                return false;
            }
        }
        catch (Exception ex)
        {
            DisplayTypingMessage($"❌ Load failed! Error: {ex.Message}", ConsoleColor.Red);
            WaitForInput();
            return false;
        }
        
        WaitForInput();
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
                continue;
                
            // Process action commands
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
