# ⚓ Battleship Hybrid
### A Physical-to-Digital Interactive Game

An interactive **Battleship experience** that bridges the gap between **physical gameplay** and a **digital game engine**.

This project combines a **sensor-equipped board** with a **WPF desktop application**, using **MQTT** for real-time communication between hardware and software.

---

## 🚧 Project Status

**Work in Progress**

Current focus:
- Cross-team gameplay flow testing  
- UI/UX redesign and visual styling  
- Hardware ↔ Application communication improvements  

---

## 🚀 The Concept

Unlike a standard digital Battleship game, this system uses **real physical ship pieces**.

### 🔄 How It Works

1. Players place **physical ship blocks** on a sensor-enabled board  
2. Sensors detect placements and send MQTT messages (example: `y42`)  
3. The WPF application:
   - Interprets coordinates  
   - Validates ship placement in real time  
   - Enforces game rules and handles edge cases  

> 💡 Hardware provides **raw position data** — the software provides **game intelligence**

---

## 🛠 Tech Stack

| Layer | Technology |
|------|------------|
| **Language** | C# / .NET |
| **UI Framework** | WPF (Windows Presentation Foundation) |
| **Communication** | MQTT |
| **Broker (Testing)** | HiveMQ |
| **Broker (Final System)** | Local MQTT Broker |
| **Graphics** | GDI+ with Sprite-Based Animations |

---

## 🧠 Core Features & Game Logic

### 🎯 Advanced Placement Validation

Because sensors only detect **positions** (not ship identity), the game uses a **state-machine-based validation system**.

#### 🔗 Strict Adjacency
Each newly placed block must be directly adjacent to the previous one.

Prevents:
- Gaps inside ships  
- Illegal floating segments  

---

#### 📏 Alignment Locking
After the **second block** is placed:
- The ship is locked into **horizontal** *or* **vertical** alignment  
- Further blocks must follow the same axis  

---

#### ⏳ Stability Delay (Anti-Merge Protection)
the system waits **20 seconds** for each ship to be placed and then checks her placement.

If an extra block appears during this delay:
- The placement is rejected  
- Prevents accidental merging of two ships  

---

## 🎞 High-Performance Animation System

Animations are handled by a custom caching system:

### `SpriteAnimatorCached`

Instead of cropping images in real time:
- Frames are **pre-cached**
- Stored as **frozen `ImageBrush` objects**
- Reused during playback  

**Result:**
- Smooth 60 FPS animations  
- Minimal CPU usage  
- Responsive gameplay visuals  

---

## 🔴🔵 Cross-Team Game Architecture

The system supports **Red Team vs. Blue Team** competitive gameplay.

### 🧩 Result Flow

1. Each team runs its own game client  
2. Game state is exchanged via **MQTT topics**  
3. When the match timer hits zero:
   - Scores are auto
