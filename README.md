# # Jet Fighter Combat Simulator

A terminal-based jet combat simulation game where you pilot a fighter jet against enemy aircraft in a dynamic battlefield.

![Jet Combat](https://img.shields.io/badge/Jet-Combat-blue) ![C# Console](https://img.shields.io/badge/C%23-Console-brightgreen)

## Description

Jet Fighter Combat is a strategic air combat simulation game built with C#. Navigate your fighter through a dangerous airspace, engage enemy jets in tactical combat, and manage critical resources like fuel and ammunition while battling various types of enemy aircraft.

## Features

- **Dynamic Combat System**: Engage in dogfights with different types of enemy aircraft
- **Resource Management**: Monitor and manage fuel, ammunition, and jet health
- **Multiple Enemy Types**: Face basic, advanced, and stealth enemy jets, each with unique behaviors
- **Flight Mechanics**: Control altitude, speed, and use afterburners for tactical advantages
- **Realistic Fuel System**: Use refueling bases and mid-air tankers to extend mission time
- **Weather Effects**: Changing weather conditions affect detection and combat accuracy

## Installation

1. Clone the repository:
   ```
   git clone https://github.com/mars-rift/Jet-Fighter-Combat-.git
   ```

2. Navigate to the project directory:
   ```
   cd Jet-Fighter-Combat-
   ```

3. Build the project:
   ```
   dotnet build
   ```

4. Run the game:
   ```
   dotnet run
   ```

## How to Play

Your jet is represented by the 'F' symbol on the grid. Navigate through the airspace, avoid or engage enemy jets (marked as 'B', 'A', or 'S'), and manage your resources carefully.

### Controls

- **Movement**:
  - `w` - Move up
  - `a` - Move left
  - `s` - Move down
  - `d` - Move right
  - `q` - Move diagonally up-left
  - `e` - Move diagonally up-right
  - `z` - Move diagonally down-left
  - `c` - Move diagonally down-right

- **Actions**:
  - `r` - Refuel (when at a base or near a tanker)
  - `b` - Toggle afterburner
  - `u` - Climb (increase altitude)
  - `j` - Descend (decrease altitude)

### Map Legend

- `F` - Your fighter jet
- `B` - Base/airfield (refuel here)
- `T` - Tanker aircraft (refuel when adjacent)
- `E/A/S` - Enemy jets (Basic/Advanced/Stealth)

## Game Mechanics

### Fuel System

- Movement consumes fuel
- Higher altitudes are more fuel-efficient for cruising
- Afterburners increase speed but double fuel consumption
- Refuel at bases (full tank) or tankers (half tank)
- Running out of fuel forces emergency descent and potential crash damage

### Combat

- Engage enemies by moving onto their position
- Different enemy types have varying health, damage output, and behaviors
- Weather affects combat accuracy
- Altitude provides tactical advantages in certain situations

### Enemy Types

1. **Basic Enemy Jet (B)**
   - Medium health
   - Basic pathfinding
   - Low damage

2. **Advanced Enemy Jet (A)**
   - High health
   - Complex behavior patterns (patrolling, chasing, retreating)
   - Medium damage
   - More maneuverable

3. **Stealth Enemy Jet (S)**
   - Medium-high health
   - Advanced pathfinding
   - High damage
   - Harder to detect

## Future Plans

- Weapon loadout customization
- Mission-based campaign mode
- Upgradable player jet
- Additional enemy types and behaviors
- Enhanced graphics with ASCII art
- Multiplayer capability

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Acknowledgments

- Inspired by classic aerial combat games
- Built using C# and .NET Core

---

*"Aim high, fly fast, hit hard!" - Jet Fighter Combat*
