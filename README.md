Battleship Hybrid: Physical-to-Digital Game 
An interactive Battleship game that bridges the gap between physical props and digital UI. This project uses WPF (.NET) for the game dashboard and MQTT for real-time communication between hardware sensors and the game engine.

Status: 🚧 Work in Progress. Current focus is on tests between 2 teams for Game and App flow and changing and creating UI and Style .

🚀 The Concept
Unlike a standard digital game, this project is designed to work with physical "blocks" and "ship props."

Players place physical ships onto a sensor-equipped board.

Sensors send MQTT messages (e.g., y42) to the application.

The WPF application validates the placement in real-time, handling edge cases like disjointed ships or "over-placement" through software logic.

🛠 Tech Stack
Language: C# / .NET

UI Framework: WPF (Windows Presentation Foundation)

Communication: MQTT (via HiveMQ Broker for tests / MQTT local broker at the end)

Graphics: GDI+ & Sprite-based animations (custom cached frame-rendering)

🧠 Key Features & Logic
1. Advanced Placement Validation
To handle the limitations of physical sensors (which can't "see" which ship is being placed), the system uses a state-machine approach:

Strict Adjacency: Every block placed must be adjacent to the previous one to prevent gaps.

Alignment Locking: Once the second block is placed, the ship is locked into a horizontal or vertical axis.

Stability Delay: For specific ship sizes, the game waits 1.5–2 seconds after the last block is detected. If a "bonus" block is detected during this time, the ship is rejected (preventing players from accidentally merging ships).

2. High-Performance Animations
To keep the UI responsive, animations (like shield hits and misses) are handled via a SpriteAnimatorCached class. Instead of cropping images on the fly, frames are pre-cached into ImageBrush objects and frozen in memory to ensure 60FPS playback without CPU spikes.

3. Cross-Team Communication
The game uses a "Red Team vs. Blue Team" architecture.

Score exchange happens automatically via MQTT when the game timer hits zero.

The Blue Team client acts as the final judge, comparing scores and publishing the "Win/Lose" state to the PlayFlow topic.
