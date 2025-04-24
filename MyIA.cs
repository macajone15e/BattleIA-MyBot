/*
Software   : BattleIA Bot - MyIA.cs
Version    : V3
Developers :
    MACAJONE Enzo
    MAZARS Baptiste

Architecture:
    |_ Program.cs      => Entry point for the bot execution
    |_ MyIA.cs         => Core bot logic (decision making, scanning, movement, combat)
    |_ BattleIA.Common => Provided interfaces and enums by the framework

Objectives :
    Implement an autonomous bot capable of surviving indefinitely in the absence of enemies 
    and maximising energy collection. The bot must :
        - Adapt its movements to its energy level
        - Favour nearby energy sources
        - Use intelligent scanning to explore the environment
        - Avoid risky movements when energy is low
        - Switch to absolute survival mode when energy is critical (<10)
        - Prepare for combat if an enemy is detected within range

Execution :
    $ ./LAUNCH.bat (Windows)
    ou
    $ dotnet run (dans le répertoire MyBot)

Last Modified :
    2025-04-24 by Enzo Macajone

TODO :
    - Do an external logger to study the strategy 
    - Improve the strategy 
*/

using System;
using System.Collections.Generic;
using BattleIA;

namespace MyBot
{
    public class MyIA
    {
        Random rng = new Random();
        bool debug = false;

        bool isFirstTurn;
        UInt16 shieldLevel;
        UInt16 cloakLevel;
        bool wasHit;
        UInt16 currentEnergy;

        const int mapHeight = 100;
        const int mapWidth = 100;
        int[,] map = new int[mapWidth, mapHeight];
        int posX = mapWidth / 2;
        int posY = mapHeight / 2;
        int scanSize = 0;
        int lastDirection = 0;

        // Initializes internal bot state at the beginning of the game
        public void DoInit()
        {
            isFirstTurn = true;
            shieldLevel = 0;
            cloakLevel = 0;
            wasHit = false;
            lastDirection = 0;
        }

        // Updates energy, shield, cloak and damage status each turn
        public void StatusReport(UInt16 turn, UInt16 energy, UInt16 shield, UInt16 cloak)
        {
            currentEnergy = energy;
            cloakLevel = cloak;
            if (shieldLevel != shield)
            {
                shieldLevel = shield;
                wasHit = true;
            }
        }

        // Main decision logic to determine the bot's next action
        public byte[] GetAction()
        {
            if (debug) Console.WriteLine($"[ACTION] Energy = {currentEnergy}, Shield = {shieldLevel}, Cloak = {cloakLevel}");

            // Try to detect enemy in straight line directions (N, S, W, E)
            MoveDirection? targetDir = null;
            int tx = -1, ty = -1;

            // Enemy detection logic for each direction
            // North
            for (int y = posY - 1; y >= 0; y--)
            {
                if (map[posX, y] == 1) break; // Wall
                if (map[posX, y] == 2) { targetDir = MoveDirection.North; tx = posX; ty = y; break; }
            }
            // South
            if (targetDir == null)
                for (int y = posY + 1; y < mapHeight; y++)
                {
                    if (map[posX, y] == 1) break;
                    if (map[posX, y] == 2) { targetDir = MoveDirection.South; tx = posX; ty = y; break; }
                }
            // West
            if (targetDir == null)
                for (int x = posX - 1; x >= 0; x--)
                {
                    if (map[x, posY] == 1) break;
                    if (map[x, posY] == 2) { targetDir = MoveDirection.West; tx = x; ty = posY; break; }
                }
            // East
            if (targetDir == null)
                for (int x = posX + 1; x < mapWidth; x++)
                {
                    if (map[x, posY] == 1) break;
                    if (map[x, posY] == 2) { targetDir = MoveDirection.East; tx = x; ty = posY; break; }
                }

            // If enemy was spotted, shoot
            if (targetDir != null)
            {
                map[tx, ty] = 3;
                if (debug) Console.WriteLine($"[SHOOT] Target spotted at ({tx}, {ty}) direction {targetDir}");
                return BotHelper.ActionShoot(targetDir.Value);
            }

            // Manage shield usage based on energy reserves
            int optimalShield = (currentEnergy / 500) * 50;
            if (optimalShield < 1) optimalShield = 1;
            else if (optimalShield > 1000) optimalShield = 1000;

            if (shieldLevel < optimalShield)
            {
                shieldLevel = (UInt16)optimalShield;
                wasHit = false;
                if (debug) Console.WriteLine($"[SHIELD] Setting shield to {shieldLevel}");
                return BotHelper.ActionShield(shieldLevel);
            }

            // Activate cloak if energy is sufficient and not already cloaked at max level
            if (currentEnergy > 1000 && cloakLevel < 4)
            {
                cloakLevel = 4;
                if (debug) Console.WriteLine($"[CLOAK] Activating cloak level {cloakLevel}");
                return BotHelper.ActionCloak(cloakLevel);
            }

            // Choose best direction and move
            var moveDir = BestDirection();
            if (debug) Console.WriteLine($"[MOVE] Moving in direction {moveDir}, new position = ({posX}, {posY})");
            return BotHelper.ActionMove((MoveDirection)moveDir);
        }

        // Determines scan range (large on first turn, then normal)
        public byte GetScanSurface()
        {
            if (isFirstTurn)
            {
                isFirstTurn = false;
                scanSize = 15 * 2 + 1;
                return 15;
            }
            else
            {
                scanSize = 4 * 2 + 1;
                return 4;
            }
        }

        // Updates bot's map knowledge using scanned area information
        public void AreaInformation(byte distance, byte[] info)
        {
            scanSize = distance;
            int idx = 0;
            for (int j = posY - (distance - 1) / 2; j <= posY + (distance - 1) / 2; j++)
            {
                for (int i = posX - (distance - 1) / 2; i <= posX + (distance - 1) / 2; i++)
                {
                    if (j >= 0 && i >= 0 && j < mapHeight && i < mapWidth)
                    {
                        if (i == posX && j == posY)
                        {
                            map[i, j] = 3;
                        }
                        else
                        {
                            // Convert scanned value to map state
                            switch ((CaseState)info[idx])
                            {
                                case CaseState.Wall: map[i, j] = 1; break;
                                case CaseState.Ennemy: map[i, j] = 2; break;
                                case CaseState.Empty: map[i, j] = 3; break;
                                case CaseState.Energy: map[i, j] = 4; break;
                            }
                        }
                    }
                    idx++;
                }
            }
            if (debug) Console.WriteLine($"[SCAN] Updated area around ({posX}, {posY}) with radius {distance}");
        }
        // Breadth-First Search to find the shortest path to the nearest energy cell
        private List<(int, int)>? FindPathToEnergy()
        {
            int d = (scanSize - 1) / 2;
            int minX = Math.Max(0, posX - d), maxX = Math.Min(mapWidth - 1, posX + d);
            int minY = Math.Max(0, posY - d), maxY = Math.Min(mapHeight - 1, posY + d);
            bool[,] visited = new bool[mapWidth, mapHeight];
            int[,] parentX = new int[mapWidth, mapHeight];
            int[,] parentY = new int[mapWidth, mapHeight];

            // Initialize parents to -1 (no parent)
            for (int i = 0; i < mapWidth; i++)
                for (int j = 0; j < mapHeight; j++)
                    parentX[i, j] = parentY[i, j] = -1;

            var queue = new Queue<(int, int)>();
            queue.Enqueue((posX, posY));
            visited[posX, posY] = true;

            bool found = false;
            int targetX = -1, targetY = -1;
            int[] dx = { 0, -1, 0, 1 }; // directions: up, left, down, right
            int[] dy = { -1, 0, 1, 0 };

            // Standard BFS to find closest energy
            while (queue.Count > 0 && !found)
            {
                var (cx, cy) = queue.Dequeue();
                if (map[cx, cy] == 4) // energy found
                {
                    found = true; targetX = cx; targetY = cy;
                    break;
                }
                for (int i = 0; i < 4; i++)
                {
                    int nx = cx + dx[i], ny = cy + dy[i];
                    if (nx < minX || nx > maxX || ny < minY || ny > maxY) continue;
                    if (map[nx, ny] <= 2) continue; // ignore walls and enemies
                    if (!visited[nx, ny])
                    {
                        visited[nx, ny] = true;
                        parentX[nx, ny] = cx;
                        parentY[nx, ny] = cy;
                        queue.Enqueue((nx, ny));
                    }
                }
            }

            // No path to energy found
            if (!found) return null;

            // Reconstruct the path from the target back to the current position
            var path = new List<(int, int)>();
            int x = targetX, y = targetY;
            while (x != posX || y != posY)
            {
                path.Add((x, y));
                int px = parentX[x, y], py = parentY[x, y];
                x = px; y = py;
            }
            path.Add((posX, posY));
            path.Reverse();
            return path;
        }

        // Determines the best move direction based on nearby energy or explored map
        public int BestDirection()
        {
            // Priority 1: Adjacent energy
            if (posY - 1 >= 0 && map[posX, posY - 1] == 4) { posY--; return lastDirection = 1; }
            if (posX + 1 < mapWidth && map[posX + 1, posY] == 4) { posX++; return lastDirection = 4; }
            if (posX - 1 >= 0 && map[posX - 1, posY] == 4) { posX--; return lastDirection = 2; }
            if (posY + 1 < mapHeight && map[posX, posY + 1] == 4) { posY++; return lastDirection = 3; }

            // Priority 2: Follow the shortest path to nearest energy if known
            var path = FindPathToEnergy();
            int dir = 0;
            if (path != null && path.Count > 1)
            {
                var (nx, ny) = path[1];
                int dx_ = nx - posX, dy_ = ny - posY;
                if (dx_ == 1) dir = 4;
                else if (dx_ == -1) dir = 2;
                else if (dy_ == 1) dir = 3;
                else if (dy_ == -1) dir = 1;
            }
            else // Priority 3: Random safe exploration
            {
                var options = new List<int>();
                if (posY - 1 >= 0 && map[posX, posY - 1] > 2) options.Add(1);
                if (posX + 1 < mapWidth && map[posX + 1, posY] > 2) options.Add(4);
                if (posX - 1 >= 0 && map[posX - 1, posY] > 2) options.Add(2);
                if (posY + 1 < mapHeight && map[posX, posY + 1] > 2) options.Add(3);
                if (options.Count > 0) dir = options[rng.Next(options.Count)];
            }

            // Avoid backtracking if possible (prefer last direction over its opposite)
            int opposite = lastDirection switch { 1 => 3, 2 => 4, 3 => 1, 4 => 2, _ => 0 };
            bool accessible = lastDirection switch
            {
                1 => posY - 1 >= 0 && map[posX, posY - 1] > 2,
                2 => posX - 1 >= 0 && map[posX - 1, posY] > 2,
                3 => posY + 1 < mapHeight && map[posX, posY + 1] > 2,
                4 => posX + 1 < mapWidth && map[posX + 1, posY] > 2,
                _ => false
            };
            if (lastDirection != 0 && dir == opposite && accessible)
            {
                dir = lastDirection;
            }

            // Apply chosen movement direction and update position
            switch (dir)
            {
                case 1: posY--; break;
                case 2: posX--; break;
                case 3: posY++; break;
                case 4: posX++; break;
            }

            return lastDirection = dir;
        }
    }
}

