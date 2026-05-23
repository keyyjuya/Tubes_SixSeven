## Description
This project is an implementation of a bot for the Robocode Tank Royale game as part of the Algorithm Strategies course assignment. The bot is developed using C# and applies greedy strategy to determine actions during battles. In Robocode Tank Royale, each bot fights in an arena until only one winner remains (battle royale). All bot actions are fully controlled by algorithms programmed by the player without any manual control during the match.

## Main Bot
SixSeven

## Alternative Bots
- Keyju

- TOP 67

- 67 alter

## Algorithms
1. SixSeven applies a greedy strategy by always selecting the nearest enemy as the main target based on minimum distance detected by the radar. Additionally, the bot uses a risk function calculation to determine movement points with the lowest risk relative to enemy positions. This allows the bot to move more safely while maintaining attack effectiveness using linear targeting and adaptive bullet power.
2. Keyju is a bot that moves in a circle around the enemy at a 90-degree perpendicular angle. It tries to maintain a distance of 250 units from the target. The orbit direction changes randomly to become unpredictable. Keyju does not select the nearest target, but rather the enemy with the lowest energy. If the enemy is very weak with energy below 5 and distance less than 200 units, Keyju will stop orbiting and ram directly to finish it off.
3. TOP 67 is an aggressive bot that moves along the arena edges in a wave pattern. When energy is high, it tries to maintain an ideal distance of 150 units from the enemy while moving perpendicularly to avoid shots. If energy drops below 30, TOP 67 takes cover to the arena edges and moves in a wave pattern. Its main advantage is high ram damage due to frequently colliding with enemies at close range.
4. 67 alter is a bot that focuses on staying in the center of the arena. It moves in a zigzag pattern at a 75-degree angle relative to the enemy. The zigzag direction changes randomly every 10 to 25 turns to make its movement unpredictable. When hit by a bullet or detecting enemy fire, it will immediately reverse direction and dash forward to evade. Its main weakness is a very weak firing system, proven by zero bullet damage in two out of three test sessions.

## How to Run the Program
1. Clone this repository to your local machine: `git clone https://github.com/keyyjuya/Tubes_SixSeven`

2. Navigate to the bot project folder: `cd src/main-bot/SixSeven`

3. Adjust the .NET version in the SixSeven.csproj file to match the .NET version installed on your device.

4. Remove the bin and obj folders if they exist: `rm -rf bin obj`

5. Build and run the bot:
   - Windows (Command Prompt or PowerShell):
   
   `dotnet build`
   `dotnet run --no-build`
   - Linux / Mac:
   
   `dotnet build
   `dotnet run --no-build
   ## Or use the provided scripts:
   
   - Windows: Double-click `SixSeven.cmd` or run `SixSeven.cmd` in CMD
   
   - Linux/Mac: Grant permission then run
   
   `chmod +x SixSeven.sh`
   `./SixSeven.sh`

6. Run the Robocode Tank Royale application, then add the bot build folder to the bot directory configuration in Robocode and place the bot into the battle arena.  


## Author
1. Kezia Adelina Tamba (124140046)

2. Nathania Calista Hutapea (124140101)

3. Sahal Alvin Zairy (124140167)
