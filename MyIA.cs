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

Objectif :
    Implémenter un bot autonome capable de survivre indéfiniment en l'absence d'ennemi 
    et de maximiser la collecte d'énergie. Le bot doit :
        - Adapter ses déplacements à son niveau d'énergie
        - Privilégier les sources d’énergie proches
        - Utiliser intelligemment le scan pour explorer l’environnement
        - Éviter tout déplacement risqué lorsque l'énergie est basse
        - Passer en mode survie absolue lorsque l'énergie est critique (<10)
        - Se préparer au combat si un ennemi est détecté à portée

Execution :
    $ ./LAUNCH.bat (Windows)
    ou
    $ dotnet run (dans le répertoire MyBot)
*/
Tu peux aussi ajouter une ligne avec la date de dernière modification ou une section TODO si vous avez des améliorations prévues. Tu veux que je t’aide à faire un template de header propre et réutilisable ?







Vous avez dit :
en anglais et je veux bien une section dernière modification et todo


ChatGPT a dit :
Parfait ! Voici un template de header propre, clair et réutilisable pour tous tes fichiers sources du projet, entièrement en anglais, avec les sections que tu voulais :

csharp
Copier
Modifier
/*
File        : MyIA.cs
Project     : BattleIA 2025 - Autonomous Bot
Version     : V3
Developers  :
    - Enzo Macajone
    - Baptiste Mazars

Architecture :
    |_ Program.cs      => Entry point for the bot execution
    |_ MyIA.cs         => Core bot logic (decision making, scanning, movement, combat)
    |_ BattleIA.Common => Provided interfaces and enums by the framework

Purpose :
    Develop a fully autonomous and energy-efficient bot for the BattleIA 2025 competition.
    The bot's main goals are to:
        - Survive indefinitely in the absence of enemies
        - Maximize energy collection with safe and efficient pathfinding
        - Adapt movement strategy based on current energy levels
        - Use scanning wisely to discover surroundings and detect enemies
        - Enter a survival mode when energy is critically low (<10)
        - Prepare for combat when an enemy is detected within range

Run Instructions :
    Windows :   ./LAUNCH.bat
    Linux/macOS: dotnet run (from the MyBot directory)

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
        bool debug = true;

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

        public void DoInit()
        {
            isFirstTurn = true;
            shieldLevel = 0;
            cloakLevel = 0;
            wasHit = false;
            lastDirection = 0;
        }

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


        public byte[] GetAction()
        {
            if (debug) Console.WriteLine($"[ACTION] Energy = {currentEnergy}, Shield = {shieldLevel}, Cloak = {cloakLevel}");

            // Scan for visible enemy in line of sight (N, S, E, W)
            MoveDirection? targetDir = null;
            int tx = -1, ty = -1;

            // North
            for (int y = posY - 1; y >= 0; y--)
            {
                if (map[posX, y] == 1) break;
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

            if (targetDir != null)
            {
                map[tx, ty] = 3;
                if (debug) Console.WriteLine($"[SHOOT] Target spotted at ({tx}, {ty}) direction {targetDir}");
                return BotHelper.ActionShoot(targetDir.Value);
            }


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

            if (currentEnergy > 1000 && cloakLevel < 4)
            {
                cloakLevel = 4;
                if (debug) Console.WriteLine($"[CLOAK] Activating cloak level {cloakLevel}");
                return BotHelper.ActionCloak(cloakLevel);
            }

            var moveDir = BestDirection();
            if (debug) Console.WriteLine($"[MOVE] Moving in direction {moveDir}, new position = ({posX}, {posY})");
            return BotHelper.ActionMove((MoveDirection)moveDir);
        }

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
        private List<(int, int)>? FindPathToEnergy()
        {
            int d = (scanSize - 1) / 2;
            int minX = Math.Max(0, posX - d), maxX = Math.Min(mapWidth - 1, posX + d);
            int minY = Math.Max(0, posY - d), maxY = Math.Min(mapHeight - 1, posY + d);
            bool[,] visited = new bool[mapWidth, mapHeight];
            int[,] parentX = new int[mapWidth, mapHeight];
            int[,] parentY = new int[mapWidth, mapHeight];

            for (int i = 0; i < mapWidth; i++)
                for (int j = 0; j < mapHeight; j++)
                    parentX[i, j] = parentY[i, j] = -1;

            var queue = new Queue<(int, int)>();
            queue.Enqueue((posX, posY));
            visited[posX, posY] = true;

            bool found = false;
            int targetX = -1, targetY = -1;
            int[] dx = { 0, -1, 0, 1 };
            int[] dy = { -1, 0, 1, 0 };

            while (queue.Count > 0 && !found)
            {
                var (cx, cy) = queue.Dequeue();
                if (map[cx, cy] == 4)
                {
                    found = true; targetX = cx; targetY = cy;
                    break;
                }
                for (int i = 0; i < 4; i++)
                {
                    int nx = cx + dx[i], ny = cy + dy[i];
                    if (nx < minX || nx > maxX || ny < minY || ny > maxY) continue;
                    if (map[nx, ny] <= 2) continue;
                    if (!visited[nx, ny])
                    {
                        visited[nx, ny] = true;
                        parentX[nx, ny] = cx;
                        parentY[nx, ny] = cy;
                        queue.Enqueue((nx, ny));
                    }
                }
            }

            if (!found) return null;

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

        public int BestDirection()
        {
            if (posY - 1 >= 0 && map[posX, posY - 1] == 4) { posY--; return lastDirection = 1; }
            if (posX + 1 < mapWidth && map[posX + 1, posY] == 4) { posX++; return lastDirection = 4; }
            if (posX - 1 >= 0 && map[posX - 1, posY] == 4) { posX--; return lastDirection = 2; }
            if (posY + 1 < mapHeight && map[posX, posY + 1] == 4) { posY++; return lastDirection = 3; }

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
            else
            {
                var options = new List<int>();
                if (posY - 1 >= 0 && map[posX, posY - 1] > 2) options.Add(1);
                if (posX + 1 < mapWidth && map[posX + 1, posY] > 2) options.Add(4);
                if (posX - 1 >= 0 && map[posX - 1, posY] > 2) options.Add(2);
                if (posY + 1 < mapHeight && map[posX, posY + 1] > 2) options.Add(3);
                if (options.Count > 0) dir = options[rng.Next(options.Count)];
            }

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
