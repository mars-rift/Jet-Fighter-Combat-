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

    // ---- Assignment snapshot ----
    // Captures the relevant world state at the moment the mission is assigned.
    // Mission completion is then evaluated as a DELTA against this snapshot, so a
    // mission cannot be "completed" by state that already existed when it was given.
    public int AssignedTurn { get; set; }
    public int KillsAtAssignment { get; set; }
    public int StealthKillsAtAssignment { get; set; }
    public int Alt3KillsAtAssignment { get; set; }
    public int MidAirRefuelsAtAssignment { get; set; }
    public int StormTurnsAtAssignment { get; set; }   // running counter of turns spent in storm
    public bool StartedAtMaxAltitude { get; set; }    // for "Reach maximum altitude"
    public bool StartedAtBase { get; set; }           // for "Visit any base"
    public bool BaseVisitedSinceAssignment { get; set; }
    public bool MaxAltitudeReachedSinceAssignment { get; set; }

    public Mission(string objective, int rewardPoints)
    {
        Objective = objective;
        RewardPoints = rewardPoints;
        IsComplete = false;
    }

    public void Complete()
    {
        if (IsComplete)
            return;

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

    protected void ManageAltitude(int playerAltitude, Weather weather)
    {
        int targetAltitude = PreferredAltitude;

        if (CurrentState == EnemyState.Chasing || CurrentState == EnemyState.Flanking || CurrentState == EnemyState.Diving)
        {
            if (weather == Weather.Storm)
            {
                // Storms make extreme high-altitude maneuvers risky; favor medium altitude when engaging.
                targetAltitude = Math.Min(2, Math.Max(1, playerAltitude));
            }
            else if (weather == Weather.Cloudy)
            {
                // Clouds reduce visibility, so close the gap and engage from a stable altitude.
                targetAltitude = Math.Max(1, Math.Min(3, playerAltitude));
            }
            else
            {
                // Clear weather allows altitude advantage seeking.
                targetAltitude = Math.Min(3, Math.Max(playerAltitude, PreferredAltitude));
            }
        }
        else if (CurrentState == EnemyState.Retreating)
        {
            if (weather == Weather.Storm)
            {
                targetAltitude = Math.Max(1, Altitude - 1);
            }
            else
            {
                targetAltitude = Math.Min(3, Altitude + 1);
            }
        }
        else if (CurrentState == EnemyState.Patrolling)
        {
            if (weather == Weather.Storm)
            {
                targetAltitude = Math.Min(2, PreferredAltitude);
            }
            else
            {
                targetAltitude = PreferredAltitude;
            }
        }

        if (Altitude < targetAltitude && Altitude < 3)
        {
            Altitude++;
            ConsumeFuel("climb");
        }
        else if (Altitude > targetAltitude && Altitude > 0)
        {
            Altitude--;
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
        double distance = Math.Sqrt(Math.Pow(X - playerX, 2) + Math.Pow(Y - playerY, 2));

        if (playerDetected)
        {
            lastPlayerX = playerX;
            lastPlayerY = playerY;
            turnsWithoutContact = 0;

            if (Health < MaxHealth * 0.35)
            {
                UpdateState(EnemyState.Retreating);
            }
            else if (distance <= 2)
            {
                UpdateState(EnemyState.Flanking);
            }
            else if (distance <= 6)
            {
                UpdateState(EnemyState.Chasing);
            }
            else
            {
                UpdateState(EnemyState.Flanking);
            }
        }
        else
        {
            turnsWithoutContact++;
            if (turnsWithoutContact > 4)
            {
                UpdateState(EnemyState.Patrolling);
            }
        }

        if (CurrentState == EnemyState.Flanking && distance > 5)
            UpdateState(EnemyState.Chasing);
        else if (CurrentState == EnemyState.Chasing && distance <= 2)
            UpdateState(EnemyState.Flanking);

        ManageAltitude(game.PlayerAltitude, game.CurrentWeather);
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
        double distance = Math.Sqrt(Math.Pow(X - playerX, 2) + Math.Pow(Y - playerY, 2));

        if (playerDetected)
        {
            if (Health < MaxHealth * 0.4)
            {
                UpdateState(EnemyState.Retreating);
                aggressionLevel = Math.Max(1, aggressionLevel - 1);
            }
            else if (distance <= 4)
            {
                if (aggressionLevel > 6)
                    UpdateState(EnemyState.Diving); // Aggressive diving attack
                else
                    UpdateState(EnemyState.Flanking);
            }
            else if (distance <= 7)
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
            UpdateState(EnemyState.Patrolling);
        }

        if (CurrentState == EnemyState.Flanking && distance > 7)
            UpdateState(EnemyState.Chasing);
        else if (CurrentState == EnemyState.Diving && distance > 5)
            UpdateState(EnemyState.Chasing);

        ManageAltitude(game.PlayerAltitude, game.CurrentWeather);
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
        double distance = Math.Sqrt(Math.Pow(X - playerX, 2) + Math.Pow(Y - playerY, 2));
        
        if (playerDetected)
        {
            if (Health < MaxHealth * 0.5)
            {
                UpdateState(EnemyState.Retreating);
                stealthMode = true; // Activate stealth when retreating
            }
            else if (distance <= 3 && stealthMode)
            {
                UpdateState(EnemyState.Diving); // Stealth attack
            }
            else if (distance <= 4)
            {
                UpdateState(EnemyState.Flanking);
            }
            else if (distance <= 7)
            {
                UpdateState(EnemyState.Chasing);
            }
            else
            {
                UpdateState(EnemyState.Patrolling);
            }
        }
        else
        {
            UpdateState(EnemyState.Patrolling);
        }

        if (CurrentState == EnemyState.Chasing && distance <= 4)
            UpdateState(EnemyState.Flanking);
        else if (CurrentState == EnemyState.Diving && distance > 4)
            UpdateState(EnemyState.Chasing);

        ManageAltitude(game.PlayerAltitude, game.CurrentWeather);
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
    private bool gameOverHandled = false; // Track if game over has been properly handled
    public bool IsGameActive => currentState == GameState.Playing;
    
    // Method to check if game ended and handle cleanup if needed
    public void EnsureGameEndHandled()
    {
        if (!IsGameActive && !gameOverHandled)
        {
            // If game is not active but we haven't properly handled game over, do it now
            HandleGameOver();
        }
    }
    private const int gridSize = 20;
    private char[,] grid;
    private int playerX, playerY;
    private char playerJet = 'P';
    private int maxPlayerHealth = 10;
    private int playerHealth = 10;
    private int score = 0;
    private List<Mission> activeMissions = new List<Mission>();
    private List<Mission> completedMissions = new List<Mission>();
    private int missionCounter = 0;

    // ---- Mission-progress counters ----
    // These are monotonically increasing tallies of player-attributable events. Mission
    // completion compares the current value to a snapshot taken when the mission was
    // assigned, so missions can only be cleared by NEW work the player performs after
    // accepting them.
    private int turnCounter = 0;             // total full game turns elapsed
    private int totalEnemyKills = 0;         // enemies destroyed by the player
    private int stealthKills = 0;            // F-22 Raptor kills
    private int alt3Kills = 0;               // kills scored at altitude 3
    private int midAirRefuels = 0;           // successful tanker refuels via 'r'
    private int stormTurnsLifetime = 0;      // running total of turns spent in storms

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
    public Weather CurrentWeather => currentWeather;
    private int weatherTurns = 0; // Track how long current weather has persisted
    private double detectionRangeModifier = 1.0;
    private double accuracyModifier = 1.0;
    private int playerAltitude = 1;
    private int missileAmmo = 10;
    private int gunAmmo = 500;
    private int countermeasures = 4;       // Chaff/flare bundles available
    private int maxCountermeasures = 4;

    // Combat flow state -- ensures correct battle order and prevents double-resolution
    private int playerDx = -1;             // Last movement direction (player "facing")
    private int playerDy = 0;
    private bool enemyCombatResolvedThisTurn = false; // True if an enemy already fired at the player this turn
    private bool playerEvadingNextTurn = false;       // Carry-over evasion benefit from a successful evasive maneuver

    // Combat-encounter control
    private enum EngagementAspect { HeadOn, Beam, Rear }

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
    private List<(int, int)> tankerPositions = new List<(int, int)>(); // Track tanker positions

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
        tankerPositions.Add((tankerX, tankerY)); // Track tanker position
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
        try
        {
            // Clear any previous output and ensure clean display
            Console.Clear();
            
            // Display final grid state
            Console.WriteLine("═══════════════════════════════════════════════════════════════════");
            Console.WriteLine("                          FINAL MISSION STATUS                       ");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════");
            Console.WriteLine();
            
            DisplayGrid();
            
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════════════");
            Console.ForegroundColor = currentState == GameState.Victory ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine($"                          {message.ToUpper()}                          ");
            Console.ResetColor();
            Console.WriteLine("═══════════════════════════════════════════════════════════════════");
            Console.WriteLine();
            
            Console.WriteLine("[STATS] FINAL COMBAT STATISTICS:");
            Console.WriteLine($"   [HP]  Final Health: {playerHealth}/{maxPlayerHealth}");
            Console.WriteLine($"   [PTS] Final Score: {score}");
            Console.WriteLine($"   [ALT] Final Altitude: {playerAltitude}");
            Console.WriteLine($"   [FUEL] Remaining Fuel: {Fuel}/{MaxFuel}");
            Console.WriteLine($"   [MISS] Completed Missions: {completedMissions.Count}");
            Console.WriteLine($"   [ENMY] Enemies Remaining: {enemyJets.Count}");
            
            if (completedMissions.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("[COMPLETE] COMPLETED MISSIONS:");
                foreach (var mission in completedMissions)
                {
                    Console.WriteLine($"   [OK] {mission.Objective} (+{mission.RewardPoints} pts)");
                }
            }
            
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════════════");
            Console.WriteLine("                    THANK YOU FOR PLAYING!                         ");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════");
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            
            // Ensure we wait for user input before exiting
            Console.ReadKey(true);
        }
        catch (Exception ex)
        {
            // Emergency fallback in case of display issues
            Console.WriteLine("\n" + message);
            Console.WriteLine($"Final Score: {score}");
            Console.WriteLine($"Final Health: {playerHealth}");
            Console.WriteLine($"Game ended due to display error: {ex.Message}");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey(true);
        }
        
        Environment.Exit(0);
    }

    /// <summary>
    /// Resolves a 1-v-1 air-to-air engagement between the player and a single enemy.
    /// Battle order is determined by INITIATIVE:
    ///   1. SITREP (range / aspect / lock probability) is presented.
    ///   2. Player chooses an action (fire missile / guns / evade / countermeasures).
    ///   3. If the enemy has initiative, ENEMY FIRES FIRST (modified by player's defensive choice).
    ///      Otherwise the player's shot resolves first.
    ///   4. The other party returns fire (if still alive and not already resolved).
    /// This ordering prevents the previous bug where the enemy always counterattacked
    /// AFTER the player, even when the enemy was the one who ambushed the player.
    /// </summary>
    private void ProcessCombat(EnemyJet enemy, bool enemyInitiated = false)
    {
        Console.Clear();
        if (enemyInitiated)
            DisplayTypingMessage($"[BREAK] {enemy.AircraftName} bouncing you from {enemy.X},{enemy.Y}!", ConsoleColor.Red, 30);
        else
            DisplayTypingMessage($"[ALERT] ENEMY CONTACT! {enemy.AircraftName} detected at {enemy.X},{enemy.Y}!", ConsoleColor.Red);

        double distance = Math.Sqrt(Math.Pow(enemy.X - playerX, 2) + Math.Pow(enemy.Y - playerY, 2));
        int chebDistance = ChebyshevDistance(playerX, playerY, enemy.X, enemy.Y);
        string distanceDesc = distance switch
        {
            <= 1 => "POINT BLANK RANGE",
            <= 2 => "Close range",
            <= 3 => "Medium-close range",
            <= 5 => "Medium range",
            _ => "Long range"
        };

        EngagementAspect aspect = ComputeAspect(enemy);
        int altDelta = enemy.Altitude - playerAltitude;
        bool enemyHasInitiative = ComputeEnemyInitiative(enemy, enemyInitiated, aspect, altDelta);

        // SITREP
        DisplayTypingMessage($"Distance: {distance:F1} units ({distanceDesc})", ConsoleColor.Yellow);
        DisplayTypingMessage($"Aspect: {AspectLabel(aspect)} | Altitude delta: {altDelta:+#;-#;0}", ConsoleColor.Cyan);
        DisplayTypingMessage($"Enemy Status: {enemy.Health}/{enemy.MaxHealth} HP | State: {enemy.CurrentState}", ConsoleColor.Cyan);
        if (enemyHasInitiative)
            DisplayTypingMessage("[INITIATIVE] Enemy has the bounce — they will fire first!", ConsoleColor.Red);
        else
            DisplayTypingMessage("[INITIATIVE] You hold the advantage — fire first!", ConsoleColor.Green);

        if (enemy.CurrentState == EnemyState.Chasing)
            DisplayTypingMessage("WARNING: Enemy is actively pursuing!", ConsoleColor.Red);
        else if (enemy.CurrentState == EnemyState.Flanking)
            DisplayTypingMessage("CAUTION: Enemy attempting flanking maneuver!", ConsoleColor.Yellow);

        DisplayCombatOptions(enemy);

        string? action = Console.ReadLine();
        bool evaded = false;
        bool deployedCm = false;
        bool playerCommittedAttack = false;
        bool fireMissile = false;
        bool fireGun = false;

        switch (action?.ToLower())
        {
            case "1": // Fire missile
                fireMissile = true;
                playerCommittedAttack = true;
                break;
            case "2": // Gun attack
                fireGun = true;
                playerCommittedAttack = true;
                break;
            case "3": // Evasive maneuver
                DisplayTypingMessage("[EVADE] Executing evasive maneuvers!", ConsoleColor.Green, 40);
                evaded = PerformEvasiveManeuver();
                break;
            case "4": // Countermeasures (chaff/flares)
                deployedCm = DeployCountermeasures();
                break;
            default:
                DisplayTypingMessage("[MISS] Combat opportunity missed! No action taken.", ConsoleColor.DarkRed);
                break;
        }

        // ----- PHASE 1: ENEMY FIRES FIRST IF THEY HAVE INITIATIVE -----
        if (enemyHasInitiative && enemy.Health > 0)
        {
            double accMod = 1.0;
            double dmgMod = 1.0;
            if (evaded) { accMod *= 0.45; dmgMod *= 0.6; }
            if (deployedCm) { accMod *= 0.35; }
            // Aspect: head-on snap shots are harder for the enemy than rear-aspect shots.
            if (aspect == EngagementAspect.HeadOn) accMod *= 0.85;
            else if (aspect == EngagementAspect.Rear) accMod *= 1.15;

            System.Threading.Thread.Sleep(400);
            DisplayTypingMessage("[INCOMING] Enemy fox-2/guns -- defensive!", ConsoleColor.Red);
            EnemyAttack(enemy, accMod, dmgMod);
            enemyCombatResolvedThisTurn = true;

            if (currentState != GameState.Playing) return;
        }

        // ----- PHASE 2: PLAYER ATTACK RESOLVES -----
        if (fireMissile && enemy.Health > 0)
        {
            if (TryFireWeapon("Missile", enemy, 7, aspect))
            {
                DisplayTypingMessage("[MISSILE] MISSILE AWAY! Tracking target...", ConsoleColor.Yellow, 50);
                System.Threading.Thread.Sleep(800);
            }
        }
        else if (fireGun && enemy.Health > 0)
        {
            if (TryFireWeapon("Gun", enemy, 3, aspect))
            {
                DisplayTypingMessage("[GUNS] GUNS GUNS GUNS! Engaging with cannon!", ConsoleColor.Red, 40);
                System.Threading.Thread.Sleep(600);
            }
        }

        // ----- PHASE 3: ENEMY RETURN FIRE (only if they haven't already shot) -----
        if (enemy.Health > 0 && !enemyCombatResolvedThisTurn)
        {
            if (evaded)
            {
                DisplayTypingMessage("[EVADE] Enemy can't get a firing solution -- maneuver paid off.", ConsoleColor.Green);
            }
            else if (deployedCm)
            {
                DisplayTypingMessage("[DEFEND] Countermeasures spoiled the lock -- enemy holds fire.", ConsoleColor.Green);
            }
            else
            {
                System.Threading.Thread.Sleep(500);
                DisplayTypingMessage("[WARNING] Enemy returning fire!", ConsoleColor.Red);
                // Return fire is slightly less accurate -- the enemy is reactive, not proactive.
                double accMod = playerCommittedAttack ? 0.85 : 1.0;
                if (aspect == EngagementAspect.Rear) accMod *= 1.15;
                EnemyAttack(enemy, accMod, 1.0);
                enemyCombatResolvedThisTurn = true;
            }
        }

        // ----- PHASE 4: RESOLUTION -----
        if (enemy.Health <= 0)
        {
            DisplayTypingMessage($"[KILL] {enemy.AircraftName} DESTROYED! Excellent shooting, pilot!", ConsoleColor.Green, 40);
            DisplayTypingMessage($"[SCORE] +{enemy.ScoreValue} points earned", ConsoleColor.Yellow);

            // Clear enemy from grid before removing from list
            grid[enemy.X, enemy.Y] = '.';
            enemyJets.Remove(enemy);
            score += enemy.ScoreValue;

            // Update kill counters BEFORE the victory check so that any final-kill
            // missions ("Defeat a stealth aircraft", "High altitude combat", etc.) can be
            // properly recognized.
            totalEnemyKills++;
            if (enemy is F22Raptor) stealthKills++;
            if (playerAltitude == 3) alt3Kills++;

            // Random chance for power-up when defeating enemy
            if (random.NextDouble() < 0.25) // 25% chance
            {
                System.Threading.Thread.Sleep(500);
                PowerUp();
            }

            if (enemyJets.Count == 0)
            {
                currentState = GameState.Victory;
                DisplayTypingMessage("[VICTORY] ALL ENEMIES NEUTRALIZED! MISSION ACCOMPLISHED!", ConsoleColor.Green, 50);
                System.Threading.Thread.Sleep(1500);
                HandleGameOver();
                return;
            }
        }

        WaitForInput();
    }

    // ---------- Combat helpers (geometry, aspect, initiative) ----------

    private static int ChebyshevDistance(int ax, int ay, int bx, int by)
    {
        return Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));
    }

    /// <summary>
    /// Determine engagement aspect from the player's last heading toward the enemy:
    ///   HeadOn: enemy is roughly in front of the player (player's facing dotted with
    ///           the bearing-to-enemy is positive)
    ///   Rear:   enemy is behind the player (negative dot product)
    ///   Beam:   roughly perpendicular
    /// </summary>
    private EngagementAspect ComputeAspect(EnemyJet enemy)
    {
        int relX = enemy.X - playerX;
        int relY = enemy.Y - playerY;
        if (relX == 0 && relY == 0) return EngagementAspect.HeadOn;
        // Normalize -- only directional sign matters here.
        double mag = Math.Sqrt(relX * relX + relY * relY);
        double rx = relX / mag;
        double ry = relY / mag;
        double fmag = Math.Sqrt(playerDx * playerDx + playerDy * playerDy);
        if (fmag == 0) return EngagementAspect.Beam;
        double fx = playerDx / fmag;
        double fy = playerDy / fmag;
        double dot = fx * rx + fy * ry;
        if (dot > 0.5) return EngagementAspect.HeadOn;
        if (dot < -0.5) return EngagementAspect.Rear;
        return EngagementAspect.Beam;
    }

    private static string AspectLabel(EngagementAspect a) => a switch
    {
        EngagementAspect.HeadOn => "Head-on",
        EngagementAspect.Beam => "Beam",
        EngagementAspect.Rear => "Rear (six o'clock)",
        _ => "Unknown"
    };

    /// <summary>
    /// Determine which side has firing initiative this turn. Considers who
    /// initiated, stealth status, aspect, altitude advantage, experience, and
    /// a small random factor.
    /// </summary>
    private bool ComputeEnemyInitiative(EnemyJet enemy, bool enemyInitiated, EngagementAspect aspect, int altDelta)
    {
        double enemyScore = 0.0;
        double playerScore = 0.0;

        // Who started this engagement gets a strong initiative bias.
        if (enemyInitiated) enemyScore += 0.45; else playerScore += 0.40;

        // Aspect: rear-aspect attacker has the advantage; head-on neutralizes.
        switch (aspect)
        {
            case EngagementAspect.Rear: playerScore += 0.20; break;       // enemy is in front -- player rear-aspect
            case EngagementAspect.HeadOn: /* neutral */ break;
            case EngagementAspect.Beam: enemyScore += 0.05; break;
        }

        // Stealth fighters get an initiative bonus when undetected/diving.
        if (enemy is F22Raptor raptor && raptor.IsInStealthMode()) enemyScore += 0.25;
        if (!enemy.IsDetected) enemyScore += 0.20;

        // Altitude advantage (higher = energy advantage).
        if (altDelta > 0) enemyScore += 0.08 * altDelta;
        else if (altDelta < 0) playerScore += 0.08 * -altDelta;

        // Combat experience.
        enemyScore += 0.04 * enemy.CombatExperience;

        // Aggressive states imply commitment to attack.
        if (enemy.CurrentState == EnemyState.Diving) enemyScore += 0.15;
        else if (enemy.CurrentState == EnemyState.Chasing) enemyScore += 0.05;
        else if (enemy.CurrentState == EnemyState.Retreating) playerScore += 0.20;

        // Weather slightly randomizes who reacts first.
        if (currentWeather == Weather.Storm) { enemyScore += random.NextDouble() * 0.1; playerScore += random.NextDouble() * 0.1; }

        // Carry-over evasion from a successful previous evasive maneuver.
        if (playerEvadingNextTurn) playerScore += 0.15;

        return enemyScore > playerScore;
    }

    private bool DeployCountermeasures()
    {
        if (countermeasures <= 0)
        {
            DisplayTypingMessage("[NO CM] Countermeasures depleted!", ConsoleColor.Red);
            return false;
        }
        countermeasures--;
        DisplayTypingMessage("[CHAFF/FLARE] Dispensing chaff and flares!", ConsoleColor.Yellow, 30);
        // Countermeasures are mostly effective vs. radar/IR locks.
        bool worked = random.NextDouble() < 0.85;
        if (worked)
            DisplayTypingMessage("[DEFEND] Decoys defeating enemy lock!", ConsoleColor.Green);
        else
            DisplayTypingMessage("[DEFEND] Decoys deployed but lock retained!", ConsoleColor.DarkYellow);
        return worked;
    }

    private void CheckForCombatEncounters()
    {
        // Check for enemies within different combat ranges - more balanced for gameplay
        foreach (var enemy in enemyJets.ToList())
        {
            double distance = Math.Sqrt(Math.Pow(enemy.X - playerX, 2) + Math.Pow(enemy.Y - playerY, 2));
            
            // More balanced combat ranges allowing for tactical movement
            bool shouldEngage = false;
            
            if (distance == 0)
            {
                shouldEngage = true;
            }
            else if (distance <= 1) // Point blank range - very high chance but not automatic
            {
                shouldEngage = random.NextDouble() < 0.85;
            }
            else if (distance <= 2) // Close range - moderate chance
            {
                shouldEngage = random.NextDouble() < 0.35;
            }
            else if (distance <= 3) // Medium-close range - lower chance
            {
                shouldEngage = random.NextDouble() < 0.15;
            }
            else if (distance <= 5) // Medium range - very low chance, only if enemy is aggressive
            {
                shouldEngage = enemy.IsDetected && 
                              (enemy.CurrentState == EnemyState.Chasing || enemy.CurrentState == EnemyState.Diving) && 
                              random.NextDouble() < 0.08;
            }
            // Removed long range combat to allow more free movement
            
            if (shouldEngage)
            {
                // Player initiated this engagement (entered the enemy's range during own move).
                ProcessCombat(enemy, enemyInitiated: false);
                if (currentState != GameState.Playing)
                    return;
                break; // Only one combat encounter per turn
            }
        }
    }

    public void MovePlayer(string direction)
    {
        if (currentState != GameState.Playing) return;

        // Reset per-turn combat flag at the start of the player's turn so a single
        // turn cannot resolve enemy fire twice (player-initiated + enemy-initiated).
        enemyCombatResolvedThisTurn = false;

        grid[playerX, playerY] = '.';

        int originalX = playerX;
        int originalY = playerY;
        int moveCount = afterburnerEnabled ? 2 : 1;

        int dx = 0;
        int dy = 0;

        switch (direction)
        {
            case "w": dx = -1; break;
            case "s": dx = 1; break;
            case "a": dy = -1; break;
            case "d": dy = 1; break;
            case "q": dx = -1; dy = -1; break;
            case "e": dx = -1; dy = 1; break;
            case "z": dx = 1; dy = -1; break;
            case "c": dx = 1; dy = 1; break;
            default:
                Console.WriteLine("Invalid move. Use w, a, s, d, q, e, z, or c.");
                grid[originalX, originalY] = playerJet;
                return; // Don't consume fuel for invalid moves
        }

        // Track player's facing for aspect calculations (head-on / beam / rear).
        playerDx = dx;
        playerDy = dy;

        bool moved = false;
        for (int step = 0; step < moveCount; step++)
        {
            int newX = Math.Max(0, Math.Min(gridSize - 1, playerX + dx));
            int newY = Math.Max(0, Math.Min(gridSize - 1, playerY + dy));

            if (newX == playerX && newY == playerY)
                break;

            playerX = newX;
            playerY = newY;
            moved = true;

            ConsumeFuel(afterburnerEnabled ? "afterburner" : "regular");
            if (Fuel <= 0)
            {
                afterburnerEnabled = false;
                break;
            }
        }

        if (!moved)
        {
            grid[originalX, originalY] = playerJet;
            return;
        }

        // Mark the "Visit any base" objective the moment the player MOVES onto a base
        // tile (so missions assigned while standing on a base aren't auto-completed).
        if (basePositions.Contains((playerX, playerY)))
        {
            foreach (var m in activeMissions)
            {
                if (!m.IsComplete && m.Objective.Contains("Visit any base"))
                    m.BaseVisitedSinceAssignment = true;
            }
        }

        // Check for enemy encounters at various combat ranges
        CheckForCombatEncounters();

        if (currentState == GameState.Playing)
            grid[playerX, playerY] = playerJet;
        else
            HandleGameOver();
    }

    public void MoveEnemies()
    {
        if (currentState != GameState.Playing)
            return;

        // Advance the global turn counter once per turn.
        turnCounter++;

        // Fresh enemy phase: enemies have not yet resolved their fire this turn.
        enemyCombatResolvedThisTurn = false;

        UpdateWeather();
        UpdateRadar();

        // Track time spent in storm conditions for the "Survive a storm" objective.
        if (currentWeather == Weather.Storm)
            stormTurnsLifetime++;

        // Clear enemy positions from grid before moving
        foreach (var jet in enemyJets)
        {
            if (grid[jet.X, jet.Y] == jet.Symbol)
                grid[jet.X, jet.Y] = '.';
        }

        // Move each enemy and check for combat encounters at various ranges
        foreach (var jet in enemyJets.ToList())
        {
            int oldX = jet.X;
            int oldY = jet.Y;
            
            jet.Move(playerX, playerY, grid);
            
            // Update grid with new enemy position (only if enemy is still alive)
            if (enemyJets.Contains(jet))
            {
                // Make sure we don't overwrite the player position or fixed infrastructure
                if (!(jet.X == playerX && jet.Y == playerY) && 
                    !basePositions.Contains((jet.X, jet.Y)) && 
                    !tankerPositions.Contains((jet.X, jet.Y)))
                    grid[jet.X, jet.Y] = jet.Symbol;
            }
        }

        // Restore infrastructure positions that may have been overwritten
        foreach (var (x, y) in basePositions)
        {
            if (!(x == playerX && y == playerY))
                grid[x, y] = 'B';
        }
        foreach (var (x, y) in tankerPositions)
        {
            if (!(x == playerX && y == playerY))
                grid[x, y] = 'T';
        }

        // Check for combat encounters after all enemies have moved
        CheckForEnemyCombatInitiation();

        // Check for coordinated attacks from adjacent enemies, but ONLY if a 1-v-1
        // engagement has not already resolved this turn. This prevents the player
        // from being shot at twice in the same enemy phase.
        if (!enemyCombatResolvedThisTurn)
        {
            var adjacentEnemies = enemyJets.FindAll(e => Math.Abs(e.X - playerX) <= 1 && Math.Abs(e.Y - playerY) <= 1);

            if (adjacentEnemies.Count >= 2)
            {
                // Wingmen contribute reduced fire because the lead has already engaged.
                double accMult = playerEvadingNextTurn ? 0.55 : 1.0;
                int totalDamage = adjacentEnemies.Sum(e =>
                {
                    if (random.NextDouble() > 0.6 * accuracyModifier * accMult) return 0; // some shots miss
                    int baseDamage = random.Next(1, 4);
                    bool isCrit = random.NextDouble() < 0.10;
                    return isCrit ? baseDamage * 2 : baseDamage;
                });

                if (totalDamage > 0)
                {
                    DisplayTypingMessage("[MULTI] MULTIPLE BOGEYS! Wingmen pressing the attack!", ConsoleColor.Red, 50);
                    DisplayTypingMessage($"[DAMAGE] Combined fire deals {totalDamage} damage!", ConsoleColor.Red);

                    playerHealth -= totalDamage;
                    enemyCombatResolvedThisTurn = true;

                    if (playerHealth <= 0)
                    {
                        currentState = GameState.Defeat;
                        HandleGameOver();
                        return;
                    }

                    WaitForInput("Multiple enemies engaged! Press any key to continue...");
                }
                else
                {
                    DisplayTypingMessage("[EVADE] Wingmen open fire but miss in the merge!", ConsoleColor.Green);
                }
            }
        }

        // Evasion bonus only carries over a single turn.
        playerEvadingNextTurn = false;
        
        // Display any queued messages
        if (messageQueue.Count > 0)
        {
            DisplayMessages();
        }
        
        // Ensure player position is correctly marked
        if (grid[playerX, playerY] != playerJet)
            grid[playerX, playerY] = playerJet;
    }

    private void CheckForEnemyCombatInitiation()
    {
        // Allow enemies to initiate combat when they get close, but less aggressively
        foreach (var enemy in enemyJets.ToList())
        {
            double distance = Math.Sqrt(Math.Pow(enemy.X - playerX, 2) + Math.Pow(enemy.Y - playerY, 2));
            
            // Enemies can initiate combat based on their state and distance - more tactical
            bool shouldInitiate = false;
            
            if (distance == 0)
            {
                shouldInitiate = true;
            }
            else if (distance <= 1 && enemy.CurrentState == EnemyState.Chasing) // Very close aggressive combat
            {
                shouldInitiate = random.NextDouble() < 0.6; // Reduced from 0.9
            }
            else if (distance <= 1 && enemy.CurrentState == EnemyState.Diving) // Diving attack at close range
            {
                shouldInitiate = random.NextDouble() < 0.7; // Only when very close
            }
            else if (distance <= 2 && enemy.CurrentState == EnemyState.Flanking) // Flanking maneuver
            {
                shouldInitiate = random.NextDouble() < 0.25; // Reduced from 0.5
            }
            // Removed longer range enemy attacks to give player more breathing room
            
            if (shouldInitiate)
            {
                DisplayTypingMessage($"[ATTACK] {enemy.AircraftName} initiating attack run!", ConsoleColor.Red);
                // Enemy initiated -- they get initiative bias and may fire first.
                ProcessCombat(enemy, enemyInitiated: true);
                if (currentState != GameState.Playing)
                    return;
                break; // Only one enemy-initiated combat per turn
            }
        }
    }

    private void HandleGameOver()
    {
        if (gameOverHandled) return; // Prevent double-handling
        
        gameOverHandled = true;
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

        if (enemy is F22Raptor raptor && raptor.IsInStealthMode())
        {
            detectionRange = Math.Max(2, detectionRange - 4);
        }

        // Higher altitude gives a small radar advantage
        detectionRange += playerAltitude / 2;

        // Altitude difference reduces detection if the enemy is above or below the player
        int altitudeDifference = Math.Abs(enemy.Altitude - playerAltitude);
        if (altitudeDifference > 1)
            detectionRange = Math.Max(1, detectionRange - 2);

        // Faster or more experienced enemies are slightly harder to spot
        if (enemy.CombatExperience >= 4)
            detectionRange = Math.Max(1, detectionRange - 1);

        // Calculate distance
        double distance = Math.Sqrt(Math.Pow(enemy.X - playerX, 2) + Math.Pow(enemy.Y - playerY, 2));

        return distance <= detectionRange * detectionRangeModifier;
    }

    private void DisplayCombatOptions(EnemyJet enemy)
    {
        Console.WriteLine("Combat Options:");
        Console.WriteLine($"1. Fire missile  [Missiles: {missileAmmo}]   (effective range <=8, best head-on)");
        Console.WriteLine($"2. Gun attack    [Rounds:   {gunAmmo}]   (effective range <=2, best rear-aspect)");
        Console.WriteLine("3. Evasive maneuver  (reposition + reduce incoming hit)");
        Console.WriteLine($"4. Countermeasures   [Chaff/Flare: {countermeasures}/{maxCountermeasures}] (spoil missile lock)");
        Console.WriteLine("Choose your action (1-4):");
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
                    Weather.Storm => "[STORM] STORM FRONT MOVING IN! Visibility severely reduced!",
                    Weather.Cloudy => "[CLOUD] Cloud cover increasing. Reduced visibility conditions.",
                    Weather.Clear => "[CLEAR] Weather clearing up. Optimal visibility restored.",
                    _ => "[WEATHER] Weather conditions changing..."
                };
                
                AddMessage(weatherMessage, ConsoleColor.Yellow, true);
                
                // Additional weather effects messaging
                if (currentWeather == Weather.Storm)
                {
                    AddMessage("[EFFECT] Storm conditions: -50% detection range, -30% accuracy!", ConsoleColor.Red);
                }
                else if (currentWeather == Weather.Cloudy)
                {
                    AddMessage("[EFFECT] Cloudy conditions: -20% detection range, -10% accuracy.", ConsoleColor.Yellow);
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

            // Mark the "Reach maximum altitude" objective only when the player ACTIVELY
            // climbs to alt 3 after the mission was assigned.
            if (playerAltitude == 3)
            {
                foreach (var m in activeMissions)
                {
                    if (!m.IsComplete && m.Objective.Contains("Reach maximum altitude"))
                        m.MaxAltitudeReachedSinceAssignment = true;
                }
            }

            string altitudeDesc = playerAltitude switch
            {
                1 => "Low altitude (1000ft)",
                2 => "Medium altitude (5000ft)", 
                3 => "High altitude (10000ft)",
                _ => $"Altitude level {playerAltitude}"
            };
            
            DisplayTypingMessage($"[CLIMB] Climbing to {altitudeDesc}", ConsoleColor.Cyan);
            DisplayTypingMessage("[ENGINE] Engine power increased - fuel consumption higher during climb.", ConsoleColor.Yellow);
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
            
            DisplayTypingMessage($"[DESCEND] Descending from {currentDesc} to {newDesc}", ConsoleColor.Green);
            if (playerAltitude == 0)
                DisplayTypingMessage("[WARNING] WARNING: At ground level - limited maneuverability!", ConsoleColor.Red);
        }
        else
        {
            if (increase)
                DisplayTypingMessage("[LIMIT] Maximum service ceiling reached! Cannot climb higher.", ConsoleColor.Yellow);
            else
                DisplayTypingMessage("[LIMIT] Already at minimum safe altitude!", ConsoleColor.Yellow);
        }
    }

    private void GenerateNewMission()
    {
        string[] missionTypes = {
            "Destroy an enemy jet",
            "Visit any base", 
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

        // Snapshot all relevant world state at the moment of assignment. Mission
        // completion is then evaluated as the DELTA between the current state and the
        // snapshot, preventing pre-existing conditions from instantly closing the mission.
        newMission.AssignedTurn = turnCounter;
        newMission.KillsAtAssignment = totalEnemyKills;
        newMission.StealthKillsAtAssignment = stealthKills;
        newMission.Alt3KillsAtAssignment = alt3Kills;
        newMission.MidAirRefuelsAtAssignment = midAirRefuels;
        newMission.StormTurnsAtAssignment = stormTurnsLifetime;
        newMission.StartedAtMaxAltitude = (playerAltitude == 3);
        newMission.StartedAtBase = basePositions.Contains((playerX, playerY));
        newMission.BaseVisitedSinceAssignment = false;
        newMission.MaxAltitudeReachedSinceAssignment = false;

        activeMissions.Add(newMission);

        DisplayTypingMessage($"[RADIO] NEW MISSION BRIEFING RECEIVED!", ConsoleColor.Cyan, 40);
        DisplayTypingMessage($"[TARGET] {missionName}", ConsoleColor.White);
        DisplayTypingMessage($"[REWARD] Mission reward: {rewardPoints} points", ConsoleColor.Yellow);
        DisplayTypingMessage("[INFO] Check mission status with 'm' command", ConsoleColor.Green);
    }

    public void DisplayMissions()
    {
        Console.Clear();
        DisplayTypingMessage("[MISSIONS] MISSION BRIEFING SYSTEM", ConsoleColor.Cyan, 40);
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
                Console.WriteLine($"│ [ACTIVE] {mission.Objective,-50} Reward: {mission.RewardPoints,4} pts │");
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
                Console.WriteLine($"│ [DONE] {mission.Objective,-50} Reward: {mission.RewardPoints,4} pts │");
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
        // Mission completion is evaluated as a DELTA against the snapshot taken when
        // the mission was assigned. This guarantees rewards are only paid out for
        // actions the player actually performed AFTER accepting the mission.
        foreach (var mission in activeMissions.ToList())
        {
            if (mission.IsComplete)
                continue;

            bool completed = false;

            if (mission.Objective.Contains("Destroy an enemy jet"))
            {
                // Need at least one new kill since the mission was assigned.
                completed = (totalEnemyKills - mission.KillsAtAssignment) >= 1;
            }
            else if (mission.Objective.Contains("Visit any base"))
            {
                // The player must MOVE onto a base after the mission was assigned. We do
                // not credit the player for already being on a base at assignment time.
                completed = mission.BaseVisitedSinceAssignment;
            }
            else if (mission.Objective.Contains("Reach maximum altitude"))
            {
                // If the player was already at altitude 3 when assigned, they must
                // descend and climb again -- the flag is only set on a fresh climb.
                completed = mission.MaxAltitudeReachedSinceAssignment;
            }
            else if (mission.Objective.Contains("Perform mid-air refueling"))
            {
                // Counter increments only inside the tanker branch of TryRefuel().
                completed = (midAirRefuels - mission.MidAirRefuelsAtAssignment) >= 1;
            }
            else if (mission.Objective.Contains("Survive a storm"))
            {
                // Must spend at least 5 turns in a storm AFTER the mission was assigned.
                completed = currentWeather == Weather.Storm
                            && (stormTurnsLifetime - mission.StormTurnsAtAssignment) >= 5;
            }
            else if (mission.Objective.Contains("Defeat a stealth aircraft"))
            {
                // Need a fresh stealth (F-22) kill since the mission was assigned.
                completed = (stealthKills - mission.StealthKillsAtAssignment) >= 1;
            }
            else if (mission.Objective.Contains("High altitude combat"))
            {
                // Need a kill scored while at altitude 3 since the mission was assigned.
                completed = (alt3Kills - mission.Alt3KillsAtAssignment) >= 1;
            }
            else if (mission.Objective.Contains("Maintain air superiority"))
            {
                // Requires actively engaging the enemy: complete only when at least one
                // kill has happened since assignment AND every remaining enemy is at low
                // health (or none remain). The empty-list case still requires a kill,
                // which removes the freebie when the final enemy dies via combat.
                bool killSinceAssign = (totalEnemyKills - mission.KillsAtAssignment) >= 1;
                bool allLow = !enemyJets.Any() || enemyJets.All(e => e.Health <= e.MaxHealth * 0.3);
                completed = killSinceAssign && allLow;
            }
            else if (mission.Objective.Contains("Reconnaissance mission"))
            {
                // Requires at least one enemy to actually exist and all of them to be
                // detected. Empty enemy list does NOT auto-complete (was a major bug).
                completed = enemyJets.Any() && enemyJets.All(e => e.IsDetected);
            }

            if (completed)
            {
                CompleteActiveMission(mission);
            }
        }
    }

    private void CompleteActiveMission(Mission mission)
    {
        if (mission.IsComplete)
            return;

        mission.Complete(); // Mark mission as complete
        activeMissions.Remove(mission);
        completedMissions.Add(mission);
        
        DisplayTypingMessage("[SUCCESS] MISSION ACCOMPLISHED!", ConsoleColor.Green, 50);
        DisplayTypingMessage($"[COMPLETE] {mission.Objective}", ConsoleColor.White);
        DisplayTypingMessage($"[REWARD] Mission reward: {mission.RewardPoints} points", ConsoleColor.Yellow);
        
        score += mission.RewardPoints;
        
        // Maybe generate a new mission
        if (activeMissions.Count < 3 && random.NextDouble() < 0.7) // 70% chance
        {
            System.Threading.Thread.Sleep(1000);
            GenerateNewMission();
        }
        
        WaitForInput();
    }

    private bool TryFireWeapon(string weaponType, EnemyJet enemy, int baseDamage, EngagementAspect aspect = EngagementAspect.Beam)
    {
        int ammo = weaponType == "Missile" ? missileAmmo : gunAmmo;
        if (ammo <= 0)
        {
            DisplayTypingMessage($"[NO AMMO] OUT OF {weaponType.ToUpper()} AMMO! Weapon dry!", ConsoleColor.Red);
            return false;
        }

        double distance = Math.Sqrt(Math.Pow(enemy.X - playerX, 2) + Math.Pow(enemy.Y - playerY, 2));

        // Weapon-specific effective ranges and base accuracy.
        double maxRange = weaponType == "Missile" ? 8.0 : 2.0;
        double minRange = weaponType == "Missile" ? 1.0 : 0.0;
        double hitChance = weaponType == "Missile" ? 0.70 : 0.55;
        hitChance *= accuracyModifier;

        // Out of effective envelope -- the player can take the shot but it is poor.
        if (distance > maxRange)
        {
            double over = distance - maxRange;
            hitChance -= 0.15 * over;
            DisplayTypingMessage($"[ENVELOPE] Target outside effective {weaponType.ToLower()} range!", ConsoleColor.DarkYellow);
        }
        else if (distance < minRange)
        {
            // Missile minimum-range -- needs time to arm.
            hitChance -= 0.25;
            DisplayTypingMessage("[ENVELOPE] Inside missile minimum range -- weapon may not arm!", ConsoleColor.DarkYellow);
        }

        // Aspect modifiers: missiles prefer head-on (BVR), guns prefer rear-aspect (tracking).
        if (weaponType == "Missile")
        {
            if (aspect == EngagementAspect.HeadOn) hitChance += 0.10;
            else if (aspect == EngagementAspect.Rear) hitChance += 0.05;
            else hitChance -= 0.05; // beam is tough for missiles (look-down, doppler notch)
        }
        else // Gun
        {
            if (aspect == EngagementAspect.Rear) hitChance += 0.15;
            else if (aspect == EngagementAspect.HeadOn) hitChance -= 0.10; // closure too high
        }

        // Altitude differential -- shooting up is harder.
        int altDelta = enemy.Altitude - playerAltitude;
        if (altDelta > 0) hitChance -= 0.05 * altDelta;

        // Lock-on phase for missiles vs. stealth/experienced targets.
        if (weaponType == "Missile")
        {
            double lockChance = 0.85;
            if (enemy is F22Raptor raptor && raptor.IsInStealthMode()) lockChance -= 0.45;
            else if (enemy is F22Raptor) lockChance -= 0.20;
            if (!enemy.IsDetected) lockChance -= 0.30;
            if (currentWeather == Weather.Storm) lockChance -= 0.15;
            else if (currentWeather == Weather.Cloudy) lockChance -= 0.05;
            lockChance = Math.Max(0.10, Math.Min(0.98, lockChance));

            DisplayTypingMessage($"[RADAR] Acquiring lock on {enemy.AircraftName}... ({lockChance * 100:F0}%)", ConsoleColor.Cyan, 20);
            if (random.NextDouble() > lockChance)
            {
                DisplayTypingMessage("[NO LOCK] Failed to acquire missile lock! Weapon not released.", ConsoleColor.Red);
                // Don't burn a missile on a no-lock shot.
                return false;
            }
        }
        else
        {
            // Stealth/experienced enemies are harder to track with guns too.
            if (enemy is F22Raptor) hitChance -= 0.20;
            else if (enemy.CombatExperience > 3) hitChance -= 0.08;
        }

        // Clamp.
        hitChance = Math.Max(0.05, Math.Min(0.95, hitChance));

        DisplayTypingMessage($"[TARGET] Targeting {enemy.AircraftName} ({AspectLabel(aspect)})...", ConsoleColor.Cyan, 20);
        DisplayTypingMessage($"Hit probability: {hitChance * 100:F0}%", ConsoleColor.Yellow);

        if (random.NextDouble() <= hitChance)
        {
            int baseDmg = weaponType == "Missile" ? random.Next(4, 7) : random.Next(1, 3);
            // Rear-aspect gun hits get a damage bump (tracking shot vs. fleeting).
            if (weaponType == "Gun" && aspect == EngagementAspect.Rear) baseDmg += 1;
            int damage = baseDmg * PlayerDamage;
            enemy.Health -= damage;

            string hitMessage = weaponType == "Missile" ?
                $"[IMPACT] DIRECT HIT! Missile impact confirmed! {damage} damage dealt!" :
                $"[HIT] TARGET HIT! Cannon rounds on target! {damage} damage dealt!";
            DisplayTypingMessage(hitMessage, ConsoleColor.Green, 30);

            if (damage > baseDmg * 1.5)
                DisplayTypingMessage("[CRITICAL] CRITICAL DAMAGE! Excellent marksmanship!", ConsoleColor.Yellow);

            if (weaponType == "Missile") missileAmmo--;
            else gunAmmo -= 10;

            return true;
        }
        else
        {
            string missMessage = weaponType == "Missile" ?
                "[MISS] MISSILE MISS! Target evaded -- no impact!" :
                "[MISS] SHOTS WIDE! Cannon fire ineffective!";
            DisplayTypingMessage(missMessage, ConsoleColor.Red);

            if (currentWeather == Weather.Storm)
                DisplayTypingMessage("[STORM] Storm conditions affecting targeting accuracy!", ConsoleColor.DarkYellow);

            if (weaponType == "Missile") missileAmmo--;
            else gunAmmo -= 5;

            return false;
        }
    }

    private void EnemyAttack(EnemyJet enemy, double accMod = 1.0, double dmgMod = 1.0)
    {
        // Base attack chance varies by aircraft type and experience
        double attackChance = 0.7 + (enemy.CombatExperience * 0.05);

        // F-22 Raptor has higher accuracy
        if (enemy is F22Raptor)
        {
            attackChance = 0.85;
            DisplayTypingMessage("[WARNING] F-22 Raptor locking weapons systems!", ConsoleColor.Red);
        }
        // Su-27 Flanker is also highly accurate
        else if (enemy is Su27Flanker)
        {
            attackChance = 0.82;
            DisplayTypingMessage("[WARNING] Su-27 Flanker engaging with missiles!", ConsoleColor.Red);
        }
        // F-16 Falcon has good accuracy
        else if (enemy is F16Falcon)
        {
            attackChance = 0.78;
            DisplayTypingMessage("[WARNING] F-16 Fighting Falcon opening fire!", ConsoleColor.Red);
        }

        // Apply weather and per-engagement modifiers (defensive maneuvers, countermeasures, aspect, etc.)
        attackChance *= accuracyModifier * accMod;
        attackChance = Math.Max(0.05, Math.Min(0.95, attackChance));

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
            damage = Math.Max(1, (int)Math.Round(damage * dmgMod));
            
            if (isCrit)
            {
                DisplayTypingMessage($"[CRITICAL] CRITICAL HIT! {enemy.AircraftName} scores devastating hit for {damage} damage!", ConsoleColor.Red, 40);
                DisplayTypingMessage("[BREACH] HULL BREACH! Critical systems damaged!", ConsoleColor.Red);
            }
            else
            {
                DisplayTypingMessage($"[DAMAGE] ENEMY HIT! {enemy.AircraftName} deals {damage} damage!", ConsoleColor.Red, 30);
            }
            
            playerHealth -= damage;
            
            // Health status warnings
            if (playerHealth <= 1)
                DisplayTypingMessage("[CRITICAL] CRITICAL DAMAGE! Aircraft systems critically damaged!", ConsoleColor.Red, 50);
            else if (playerHealth <= 2)
                DisplayTypingMessage("[WARNING] SEVERE DAMAGE! Hull integrity compromised!", ConsoleColor.Yellow);
            else if (playerHealth <= 3)
                DisplayTypingMessage("[WARNING] MODERATE DAMAGE! Systems showing stress!", ConsoleColor.Yellow);
            
            if (playerHealth <= 0)
            {
                currentState = GameState.Defeat;
                DisplayTypingMessage("[DESTROYED] AIRCRAFT DESTROYED! Pilot must eject from the jet!", ConsoleColor.Red, 50);
                System.Threading.Thread.Sleep(1500); // Give time to read the final message
                HandleGameOver();
                return;
            }
        }
        else
        {
            string[] missMessages = {
                $"[MISS] {enemy.AircraftName} misses! Shots wide of target!",
                $"[EVADE] Evasive maneuvers successful! {enemy.AircraftName} attack ineffective!",
                $"[LUCK] Lucky break! {enemy.AircraftName} weapons malfunction!",
                $"[DEFENSE] Defensive systems work! {enemy.AircraftName} can't get a lock!"
            };
            DisplayTypingMessage(missMessages[random.Next(missMessages.Length)], ConsoleColor.Green);
        }
    }

    private bool PerformEvasiveManeuver()
    {
        DisplayTypingMessage("[MANEUVER] Executing barrel roll maneuver!", ConsoleColor.Green, 40);
        
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
        
        bool success = random.NextDouble() < 0.75;
        if (validMoves.Count > 0 && success)
        {
            var move = validMoves[random.Next(validMoves.Count)];
            playerX = move.Item1;
            playerY = move.Item2;
            DisplayTypingMessage("[SUCCESS] Evasive maneuver successful! New position acquired.", ConsoleColor.Green);
            // Carry the evasion benefit into next turn (helps vs. coordinated attacks).
            playerEvadingNextTurn = true;
            return true;
        }
        
        if (validMoves.Count > 0)
        {
            DisplayTypingMessage("[WARNING] Evasive maneuver partially succeeded, but enemy remains in range.", ConsoleColor.Yellow);
            var move = validMoves[random.Next(validMoves.Count)];
            playerX = move.Item1;
            playerY = move.Item2;
        }
        else
        {
            DisplayTypingMessage("[WARNING] Limited maneuvering space! Maintaining current position.", ConsoleColor.Yellow);
        }
        return false;
    }

    public void PowerUp()
    {
        PlayerDamage++;
        DisplayTypingMessage("[POWERUP] POWER UP ACQUIRED!", ConsoleColor.Yellow, 50);
        DisplayTypingMessage($"[UPGRADE] Weapons systems upgraded! Damage multiplier: x{PlayerDamage}", ConsoleColor.Green, 30);
        DisplayTypingMessage("[UPGRADE] All weapon systems now operating at enhanced capacity!", ConsoleColor.Cyan);
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
        afterburnerEnabled = false;
        DisplayTypingMessage("[EMERGENCY] WARNING: OUT OF FUEL! EMERGENCY LANDING REQUIRED!", ConsoleColor.Red, 50);
        
        // Forced descent
        if (playerAltitude > 0)
        {
            playerAltitude--;
            DisplayTypingMessage($"[DESCENT] Altitude dropping! Current altitude: {playerAltitude}", ConsoleColor.Yellow);
        }
        else
        {
            // Check if player is over a landing zone/base
            if (IsOverLandingZone(playerX, playerY))
            {
                DisplayTypingMessage("[EMERGENCY] Emergency landing successful!", ConsoleColor.Green);
                Refuel(50); // Partial refuel after emergency landing
            }
            else
            {
                // Crashed
                playerHealth -= 2;
                DisplayTypingMessage("[CRASH] CRASH! Emergency landing on hostile terrain!", ConsoleColor.Red);
                
                if (playerHealth <= 0)
                {
                    currentState = GameState.Defeat;
                    HandleGameOver();
                    return;
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
        DisplayTypingMessage($"[FUEL] Refueled! Current fuel: {Fuel}/{MaxFuel}", ConsoleColor.Green);
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
            DisplayTypingMessage("[REFUEL] Airbase refueling sequence initiated...", ConsoleColor.Cyan);
            System.Threading.Thread.Sleep(1000);
            DisplayTypingMessage("[REFUEL] Fuel pumps engaged - full tank refuel in progress...", ConsoleColor.Yellow);
            Refuel(MaxFuel); // Full refuel
            DisplayTypingMessage("[REFUEL] Refueling complete! All systems ready for combat.", ConsoleColor.Green);
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
                    DisplayTypingMessage("[TANKER] Mid-air refueling tanker detected!", ConsoleColor.Cyan);
                    DisplayTypingMessage("[CONNECT] Establishing fuel line connection...", ConsoleColor.Yellow);
                    System.Threading.Thread.Sleep(1500);
                    DisplayTypingMessage("[REFUEL] Mid-air refueling in progress...", ConsoleColor.Yellow);
                    Refuel(MaxFuel / 2); // Half tank from mid-air refueling
                    DisplayTypingMessage("[REFUEL] Mid-air refueling complete! Disconnecting fuel line.", ConsoleColor.Green);
                    // Credit the player with a successful mid-air refuel for mission tracking.
                    midAirRefuels++;
                    return;
                }
            }
        }
        
        DisplayTypingMessage("[ERROR] No refueling sources detected in area!", ConsoleColor.Red);
        DisplayTypingMessage("[SEARCH] Search for airbase (B) or tanker aircraft (T) nearby.", ConsoleColor.Yellow);
    }

    private void ToggleAfterburner()
    {
        if (!afterburnerEnabled && Fuel <= 5)
        {
            DisplayTypingMessage("[WARNING] Not enough fuel to sustain afterburner!", ConsoleColor.Yellow);
            return;
        }

        afterburnerEnabled = !afterburnerEnabled;
        if (afterburnerEnabled)
        {
            DisplayTypingMessage("[AFTERBURNER] AFTERBURNER ENGAGED! Maximum thrust activated!", ConsoleColor.Red, 40);
            DisplayTypingMessage("[WARNING] WARNING: Increased fuel consumption rate and faster movement!", ConsoleColor.Yellow);
        }
        else
        {
            DisplayTypingMessage("[AFTERBURNER] Afterburner disengaged. Normal cruise thrust restored.", ConsoleColor.Green);
            DisplayTypingMessage("[SYSTEM] Fuel consumption returned to normal levels.", ConsoleColor.White);
        }
    }

    public void SaveGame()
    {
        try
        {
            DisplayTypingMessage("[SAVE] Initiating save sequence...", ConsoleColor.Cyan);

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
            
            DisplayTypingMessage("[SAVE] Game saved successfully to flight recorder!", ConsoleColor.Green);
            DisplayTypingMessage($"[SAVE] Save file: {savePath}", ConsoleColor.White);
        }
        catch (Exception ex)
        {
            DisplayTypingMessage($"[ERROR] Save failed! Error: {ex.Message}", ConsoleColor.Red);
        }
        
        WaitForInput();
    }

    public bool LoadGame()
    {
        string savePath = "save.json";
        if (!File.Exists(savePath))
        {
            DisplayTypingMessage("[ERROR] No saved mission found in flight recorder!", ConsoleColor.Red);
            WaitForInput();
            return false;
        }

        try
        {
            DisplayTypingMessage("[LOAD] Loading saved mission from flight recorder...", ConsoleColor.Cyan);

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
                
                DisplayTypingMessage("[LOAD] Mission data loaded successfully!", ConsoleColor.Green);
                DisplayTypingMessage($"[STATUS] Health: {playerHealth} | Score: {score} | Fuel: {Fuel}", ConsoleColor.White);
                DisplayTypingMessage("[RESUME] Resuming combat operations...", ConsoleColor.Yellow);
            }
            else
            {
                DisplayTypingMessage("[ERROR] Corrupted save data! Unable to load mission.", ConsoleColor.Red);
                WaitForInput();
                return false;
            }
        }
        catch (Exception ex)
        {
            DisplayTypingMessage($"[ERROR] Load failed! Error: {ex.Message}", ConsoleColor.Red);
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

        while (game.IsGameActive)
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
            
            // Only continue with game logic if game is still active
            if (game.IsGameActive)
            {
                game.MoveEnemies();
                game.CheckMissionProgress();
                game.DisplayGrid();
            }
        }
        
        // Ensure game over is handled if we exit the loop but haven't called HandleGameOver
        if (!game.IsGameActive)
        {
            // Safety net to ensure proper game over handling
            game.EnsureGameEndHandled();
        }
    }
}
