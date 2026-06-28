# Road to Money — Game Design & Production Plan
**Engine:** Godot 4.3 Mono | **Language:** C# | **Platform:** Windows  
**Genre:** Casual · Exploration · Collection | **Perspective:** First Person 3D  
**Version:** 1.0 | **Last Updated:** 2024

---

## Table of Contents
1. [Project Architecture](#1-project-architecture)
2. [Godot Scene Hierarchy](#2-godot-scene-hierarchy)
3. [Resource System](#3-resource-system)
4. [AI Behavior System](#4-ai-behavior-system)
5. [Upgrade System](#5-upgrade-system)
6. [Save System](#6-save-system)
7. [Economy Balancing](#7-economy-balancing)
8. [Task Breakdown](#8-task-breakdown)
9. [Sprint Planning](#9-sprint-planning)
10. [Technical Risks](#10-technical-risks)
11. [Performance Optimization](#11-performance-optimization)
12. [Asset Pipeline](#12-asset-pipeline)
13. [C# Autoload Singletons — Full Code](#13-c-autoload-singletons--full-code)
14. [C# vs GDScript — Migration Notes](#14-c-vs-gdscript--migration-notes)

---

## 1. Project Architecture

### Engine Config
| Setting | Value |
|---|---|
| Engine | Godot 4.3 Mono (.NET 8) |
| Renderer | Forward+ |
| Language | C# (primary) |
| Target FPS | 60 |
| Minimum GPU | GTX 1060 |
| Resolution | 1920 × 1080 |

### Folder Structure
```
res://
├── scenes/
│   ├── world/          # Ocean, biomes, zones
│   ├── player/         # FPV controller
│   ├── creatures/      # Fish, crabs, etc.
│   ├── ui/             # HUD, menus, shop
│   └── props/          # Coral, seaweed, rocks
├── scripts/
│   ├── autoloads/      # Singleton C# classes
│   ├── player/
│   ├── creatures/
│   ├── ui/
│   └── systems/
├── resources/
│   ├── items/          # SeaResource .tres files
│   ├── upgrades/       # UpgradeData .tres files
│   └── creatures/      # CreatureData .tres files
├── assets/
│   ├── models/         # .glb meshes
│   ├── textures/
│   ├── sounds/
│   ├── particles/
│   └── shaders/
└── addons/
```

### Autoload Singletons (C#)
| File | Class | Responsibility |
|---|---|---|
| `EventBus.cs` | `EventBus` | Decoupled event relay |
| `GameManager.cs` | `GameManager` | Global state, scene transitions |
| `EconomyManager.cs` | `EconomyManager` | Inventory, currency, transactions |
| `UpgradeManager.cs` | `UpgradeManager` | Unlock logic, stat access |
| `SaveSystem.cs` | `SaveSystem` | JSON save/load, atomic write |
| `AudioManager.cs` | `AudioManager` | Spatial SFX, ambient, music |

### Core Design Patterns
- **Singleton** — All 6 Autoloads accessed via static `Instance`
- **State Machine** — Player swim states, Creature AI states (enum-driven)
- **Observer / EventBus** — C# `event Action<T>` relay for decoupled communication
- **Resource Composition** — `SeaResource`, `UpgradeData` as Godot Resource `.tres` files
- **Object Pool** — Creature instances pre-warmed, recycled via `CreaturePool`

---

## 2. Godot Scene Hierarchy

### Main World Scene
```
OceanWorld (Node3D)
├── WorldEnvironment
│   ├── DirectionalLight3D      # Sun
│   └── OceanFog
├── OceanSurface
│   ├── WaterPlane (MeshInstance3D)
│   └── SurfaceParticles (GpuParticles3D)
├── OceanZoneManager (Node3D)
│   ├── Zone_Shallow  (0–15m)
│   ├── Zone_Reef     (15–40m)
│   ├── Zone_Deep     (40–80m)
│   └── Zone_Abyss    (80m+)
├── CreatureSpawner (Node3D)
├── ResourceSpawner (Node3D)
├── Player (CharacterBody3D)
└── UI_Layer (CanvasLayer)
    ├── HUD
    └── PauseMenu
```

### Player Scene
```
Player (CharacterBody3D)        # SwimController.cs
├── CameraRig (Node3D)
│   ├── Camera3D
│   ├── UnderwaterShader
│   └── BubbleTrail (GpuParticles3D)
├── OxygenSystem (Node3D)       # OxygenSystem.cs
├── CollectionArea (Area3D)
│   └── CollisionShape3D
├── HandTool (Node3D)
├── DivingLight (SpotLight3D)
├── StorageBag (Node3D)         # StorageBag.cs
└── PlayerStats (Node3D)        # PlayerStats.cs
```

### Creature Scene Template
```
Fish (CharacterBody3D)          # CreatureAI.cs
├── MeshInstance3D
├── AnimationPlayer
├── NavigationAgent3D
├── DetectionArea (Area3D)
└── LootTable (Resource)
```

### Zone System
- Each zone is a **separate subscene** loaded additively via `ResourceLoader.LoadThreadedRequest()`
- Zones overlap by **5m** for seamless transitions
- Deeper zones gated behind `UpgradeManager.CanDiveTo(zone)`

---

## 3. Resource System

### SeaResource.cs (Custom Resource)
```csharp
using Godot;

[GlobalClass]
public partial class SeaResource : Resource
{
    public enum Rarity { Common, Uncommon, Rare, Epic }

    [Export] public string Id { get; set; }
    [Export] public string DisplayName { get; set; }
    [Export] public Texture2D Icon { get; set; }
    [Export] public Mesh Mesh { get; set; }
    [Export] public Rarity ResourceRarity { get; set; }
    [Export] public int BaseValue { get; set; }
    [Export] public float Weight { get; set; }
    [Export] public string[] SpawnZones { get; set; }
    [Export] public float MinDepth { get; set; }
    [Export] public float CatchDifficulty { get; set; }
    [Export] public float RespawnTime { get; set; }
}
```

### Resource Catalogue
| Resource | Zone | Depth | Rarity | Base Value | Weight |
|---|---|---|---|---|---|
| Shell | Shallow | 0–8m | Common | 5¢ | 0.1 kg |
| Clam | Shallow | 5–15m | Uncommon | 18¢ | 0.3 kg |
| Shrimp | Reef | 10–30m | Common | 22¢ | 0.2 kg |
| Crab | Reef | 15–40m | Uncommon | 45¢ | 0.8 kg |
| Squid | Deep | 35–60m | Rare | 80¢ | 0.6 kg |
| Octopus | Deep | 40–70m | Rare | 120¢ | 1.2 kg |
| Small Fish | All | 0–50m | Common | 30¢ | 0.3 kg |
| Large Fish | Abyss | 60–80m+ | Epic | 250¢ | 2.0 kg |

> Inventory uses a **weight-based system**. StorageBag L1 = 10 kg capacity.

---

## 4. AI Behavior System

### Finite State Machine
```csharp
public enum CreatureState
{
    Idle, Wander, Flee, School, Forage, Hiding, Stunned
}
```

### Boids Flocking Core
```csharp
private Vector3 CalculateFlocking(CreatureAI agent)
{
    Vector3 separation = Vector3.Zero;
    Vector3 alignment  = Vector3.Zero;
    Vector3 cohesion   = Vector3.Zero;
    int count = 0;

    foreach (var neighbor in GetNeighbors(agent, NeighborRadius))
    {
        float dist = agent.GlobalPosition.DistanceTo(neighbor.GlobalPosition);
        if (dist < SeparationRadius)
            separation -= (neighbor.GlobalPosition - agent.GlobalPosition);
        alignment += neighbor.Velocity;
        cohesion  += neighbor.GlobalPosition;
        count++;
    }

    if (count == 0) return Vector3.Zero;
    cohesion /= count;

    return separation * SeparationWeight
         + alignment  * AlignmentWeight
         + (cohesion - agent.GlobalPosition) * CohesionWeight;
}
```

### Creature Behavior Summary
| Creature | States | Special |
|---|---|---|
| Small Fish | School → Flee → Idle | Boids, propagating flee |
| Large Fish | Wander → Flee | Solitary, fast flee |
| Crab | Idle → Wander → Hiding | Hides in rock crevices |
| Squid | School → Flee | Ink burst VFX on flee |
| Octopus | Idle → Hiding → Flee | Camouflage shader toggle |
| Turtle | Wander (Path3D) | Ambient only, uncatchable |
| Jellyfish | Idle (Path3D) | Ambient only, uncatchable |

> AI updates throttled every 3 physics frames. Max 60 active agents; beyond that `SetPhysicsProcess(false)`.

---

## 5. Upgrade System

### UpgradeData.cs (Custom Resource)
```csharp
[GlobalClass]
public partial class UpgradeData : Resource
{
    public enum UpgradeCategory { Bag, Tool, Oxygen, Light }

    [Export] public string Id { get; set; }
    [Export] public UpgradeCategory Category { get; set; }
    [Export] public int Level { get; set; }
    [Export] public string DisplayName { get; set; }
    [Export] public string Description { get; set; }
    [Export] public int Cost { get; set; }
    [Export] public string PrerequisiteUpgradeId { get; set; }
    [Export] public Texture2D Icon { get; set; }
}
```

### Upgrade Tiers
| Category | Tiers | Progression | Costs (¢) |
|---|---|---|---|
| Storage Bag | 5 | 10 kg → 60 kg | 100 / 300 / 700 / 1500 / 4000 |
| Catching Tool | 4 | Hand → Net → Spear → Vacuum | 200 / 600 / 1800 / 5000 |
| Oxygen Tank | 5 | 60s → 300s | 150 / 400 / 900 / 2500 / 6000 |
| Diving Light | 3 | None → 8m → 16m → 28m | 250 / 800 / 3000 |

### Zone Gating
| Zone | Requirements |
|---|---|
| Zone_Shallow | Always unlocked |
| Zone_Reef | Oxygen >= L1 |
| Zone_Deep | Oxygen >= L3 + Light >= L1 |
| Zone_Abyss | Oxygen >= L5 + Light >= L3 + Bag >= L4 |

---

## 6. Save System

### Save Schema (JSON)
```json
{
  "version": "1.0",
  "timestamp": 1712345678,
  "player": {
    "coins": 4500,
    "totalEarned": 18200,
    "totalDives": 47
  },
  "upgrades": { "bag": 2, "tool": 1, "oxygen": 3, "light": 1 },
  "inventory": { "clam": 12, "crab": 3, "shell": 7 }
}
```

### Atomic Write Pattern
```csharp
public void SaveGame()
{
    var data = CollectAllData();
    string tmpPath = SavePath + ".tmp";

    using var file = FileAccess.Open(tmpPath, FileAccess.ModeFlags.Write);
    file.StoreString(Json.Stringify(data, "\t"));
    file.Close();

    if (FileAccess.FileExists(SavePath))
        DirAccess.CopyAbsolute(SavePath, BackupPath);

    DirAccess.RenameAbsolute(tmpPath, SavePath);
    EventBus.Instance.EmitGameSaved();
}
```

Auto-save triggers: surface return, shop purchase, every 5 minutes.

---

## 7. Economy Balancing

### Session Targets
| Metric | Target |
|---|---|
| Session length | 15–20 minutes |
| Dives to first upgrade | 3–5 dives |
| Full progression | 8–12 hours |
| Max bag value per dive | ~3,200¢ |

### Earnings Curve
| Stage | Earnings per Dive |
|---|---|
| Early game (L1 gear) | 60–120¢ |
| Mid game (L2–3) | 300–700¢ |
| Late game (L4–5) | 1,200–3,200¢ |

### Sell Price Formula
```
finalPrice = baseValue
  * rarityMultiplier    // 1.0 / 1.5 / 2.5 / 4.0
  * depthBonus          // 1.0 + (depth / 100)
  * freshnessFactor     // 1.0 -> 0.8 decay if surfacing late
  * marketVariance      // +-15% random, resets daily
```

---

## 8. Task Breakdown

| Task | Priority | Est. Hours | Notes |
|---|---|---|---|
| FPV swim controller + buoyancy | Critical | 24h | Core feel |
| Ocean zone world + seabed mesh | Critical | 32h | 4 zones, NavMesh bake |
| Underwater visual shader | Critical | 20h | Caustics, fog, color shift |
| Resource spawner + respawn | Critical | 12h | Zone-aware, rarity-weighted |
| Collection mechanic + HUD | Critical | 16h | Area3D catch, bag weight |
| Oxygen system + danger state | Critical | 10h | Timer, UI bar, surface detect |
| Fish AI (Boids + flee) | High | 28h | Boids, NavigationAgent3D |
| Crab / Octopus / Squid AI | High | 20h | Unique flee behaviors |
| Coral reef props + seaweed | High | 16h | MultiMesh, flow shader |
| Bubble particle system | High | 6h | Player trail + ambient |
| Shop + upgrade UI | High | 18h | Sell flow, upgrade tree |
| Save / load system | High | 10h | Atomic JSON, backup |
| Turtle & Jellyfish ambient AI | Medium | 12h | Path3D patrol |
| Upgrade stat integration | Medium | 14h | All 4 categories |
| Audio: ambient + spatial SFX | Medium | 16h | Underwater EQ filter |
| Main menu + scene transitions | Medium | 8h | Splash screen |
| Performance profiling + LOD | Medium | 12h | Target 60fps GTX 1060 |
| Collectible logbook | Low | 10h | Discovery UI |
| Achievement system | Low | 8h | Steam API optional |

**Total estimated: ~292 developer hours**

---

## 9. Sprint Planning

| Sprint | Theme | Deliverables | Milestone |
|---|---|---|---|
| S1 Wk 1–2 | Core Feel | FPV swim, basic ocean mesh, oxygen timer, surface detect, resource pickup | Playable dive loop |
| S2 Wk 3–4 | World & Visuals | 4 zones, underwater shader, coral MultiMesh, bubbles, LOD | Visual vertical slice |
| S3 Wk 5–6 | Creatures & AI | Fish Boids, crab/squid/octopus FSM, turtle/jellyfish, catch | Living ocean |
| S4 Wk 7–8 | Economy & UI | Shop, sell flow, upgrades, zone gating, HUD, save/load | Full game loop |
| S5 Wk 9–10 | Content & Polish | Audio, main menu, all .tres files, balance pass, settings | Content complete |
| S6 Wk 11–12 | QA & Release | Profiling, bug fixes, Steam integration, build pipeline | Gold master |

---

## 10. Technical Risks

| Risk | Level | Mitigation |
|---|---|---|
| Underwater rendering performance | High | Shader LOD levels, caustics as projected texture, settings toggle |
| NavigationAgent3D in 3D water | High | Custom steering + Boids, waypoint graph for long paths |
| C# GC pauses causing frame stutter | High | Avoid per-frame allocations, struct pools, pre-warm creatures |
| Concurrent AI agent performance | Medium | Object pooling, per-frame throttling, LOD deactivation |
| FPV swim feel tuning time | Medium | All constants as [Export], dedicated tuning scene in Sprint 1 |
| Save file corruption on crash | Medium | Atomic write, backup slot, schema version migration |
| C# hot reload limitations | Medium | Expect full recompile on change; keep modules small |
| Asset pipeline bottleneck | Low | Import presets in repo, CI asset validation |

---

## 11. Performance Optimization

### Rendering
| Setting | Value |
|---|---|
| LOD levels per asset | 3 (8m / 20m / 40m) |
| Occlusion | OccluderInstance3D behind rocks |
| Coral forest | MultiMeshInstance3D |
| Shadow cast distance | 15m max |
| Particle budget | 512 concurrent max |

### AI & Gameplay
| Setting | Value |
|---|---|
| AI update rate | Every 3 physics frames |
| Max active agents | 60 |
| Creature pooling | Pre-warm 20 per species |
| Off-screen culling | VisibleOnScreenNotifier3D |

### C#-Specific
- Cache `GetNode<T>()` in `_Ready()` — never call in `_Process()`
- No LINQ in `_PhysicsProcess()` or AI tick — use `for` loops
- Use `struct` for frequently-created per-frame data
- Use `Span<T>` / `ArrayPool<T>` for per-frame collections

---

## 12. Asset Pipeline

### 3D Models
| Spec | Value |
|---|---|
| Format | GLB (GLTF 2.0) |
| Authoring | Blender 4.x |
| Creature poly budget | 800–2,500 tris |
| Prop poly budget | 200–800 tris |
| LOD generation | Blender Decimate — 3 meshes per asset |
| Rig | Max 40 bones per creature |

### Textures
| Type | Spec |
|---|---|
| Creatures | 512x512 Albedo + Normal + ORM |
| Environment | 1024x1024 tileable |
| Compression | BC7 (colour), BC5 (normals) |
| Atlasing | Props — 1 atlas per zone |

### Shaders & VFX
| Effect | Technique |
|---|---|
| Water surface | Custom ShaderMaterial (FFT waves) |
| Underwater overlay | CanvasItem shader on fullscreen quad |
| Seaweed sway | Vertex shader (sine + noise) |
| Caustics | Projected texture on WorldEnvironment |
| Particles | GpuParticles3D |

### Version Control
- Git + Git LFS for binary assets
- Branches: `main` -> `staging` -> `feature/*`
- All `.tres` files in text format for diffability

---

## 13. C# Autoload Singletons — Full Code

### EventBus.cs
```csharp
using Godot;
using System;

public partial class EventBus : Node
{
    public static EventBus Instance { get; private set; }

    public event Action<string, int> ResourceCollected;
    public event Action              InventoryChanged;
    public event Action<int>         CoinsChanged;
    public event Action<string>      UpgradePurchased;
    public event Action              DiveStarted;
    public event Action              DiveEnded;
    public event Action<float,float> OxygenChanged;
    public event Action              OxygenCritical;
    public event Action              PlayerSurfaced;
    public event Action              GameSaved;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
    }

    public void EmitResourceCollected(string id, int qty) => ResourceCollected?.Invoke(id, qty);
    public void EmitInventoryChanged()                    => InventoryChanged?.Invoke();
    public void EmitCoinsChanged(int amount)              => CoinsChanged?.Invoke(amount);
    public void EmitUpgradePurchased(string id)           => UpgradePurchased?.Invoke(id);
    public void EmitDiveStarted()                         => DiveStarted?.Invoke();
    public void EmitDiveEnded()                           => DiveEnded?.Invoke();
    public void EmitOxygenChanged(float cur, float max)   => OxygenChanged?.Invoke(cur, max);
    public void EmitOxygenCritical()                      => OxygenCritical?.Invoke();
    public void EmitPlayerSurfaced()                      => PlayerSurfaced?.Invoke();
    public void EmitGameSaved()                           => GameSaved?.Invoke();
}
```

### GameManager.cs
```csharp
using Godot;

public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; }

    public enum GameState { MainMenu, Surface, Diving, Shop, Paused }

    public GameState CurrentState { get; private set; } = GameState.MainMenu;
    public int   TotalDives   { get; set; } = 0;
    public float SessionStart { get; private set; }

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
    }

    public void ChangeState(GameState newState)
    {
        CurrentState = newState;
        switch (newState)
        {
            case GameState.Diving:
                SessionStart = Time.GetTicksMsec() / 1000f;
                TotalDives++;
                EventBus.Instance.EmitDiveStarted();
                break;
            case GameState.Surface:
                EventBus.Instance.EmitDiveEnded();
                break;
        }
    }

    public bool IsDiving() => CurrentState == GameState.Diving;
}
```

### EconomyManager.cs
```csharp
using Godot;
using System.Collections.Generic;

public partial class EconomyManager : Node
{
    public static EconomyManager Instance { get; private set; }

    public int   Coins            { get; private set; } = 0;
    public int   TotalEarned      { get; private set; } = 0;
    public float BagWeightCurrent { get; private set; } = 0f;

    private Dictionary<string, int> _inventory = new();
    public IReadOnlyDictionary<string, int> Inventory => _inventory;

    public override void _Ready()
    {
        Instance = this;
        EventBus.Instance.ResourceCollected += OnResourceCollected;
    }

    public void AddCoins(int amount)
    {
        Coins       += amount;
        TotalEarned += amount;
        EventBus.Instance.EmitCoinsChanged(Coins);
    }

    public bool SpendCoins(int amount)
    {
        if (Coins < amount) return false;
        Coins -= amount;
        EventBus.Instance.EmitCoinsChanged(Coins);
        return true;
    }

    public void AddToInventory(string resourceId, int quantity = 1)
    {
        _inventory.TryGetValue(resourceId, out int current);
        _inventory[resourceId] = current + quantity;
        EventBus.Instance.EmitInventoryChanged();
    }

    public void ClearInventory()
    {
        _inventory.Clear();
        BagWeightCurrent = 0f;
        EventBus.Instance.EmitInventoryChanged();
    }

    private void OnResourceCollected(string id, int qty) => AddToInventory(id, qty);
}
```

### UpgradeManager.cs
```csharp
using Godot;
using System.Collections.Generic;

public partial class UpgradeManager : Node
{
    public static UpgradeManager Instance { get; private set; }

    public Dictionary<string, int> Upgrades { get; set; } = new()
    {
        { "bag",    0 },
        { "tool",   0 },
        { "oxygen", 0 },
        { "light",  0 }
    };

    private static readonly float[] BagCapacity    = { 10f, 20f, 30f, 45f, 60f };
    private static readonly float[] OxygenDuration = { 60f, 90f, 130f, 200f, 300f };
    private static readonly float[] LightRange     = { 0f, 8f, 16f, 28f };

    public override void _Ready() => Instance = this;

    public float GetBagCapacity()    => BagCapacity   [Upgrades["bag"]];
    public float GetOxygenDuration() => OxygenDuration[Upgrades["oxygen"]];
    public float GetLightRange()     => LightRange    [Upgrades["light"]];

    public bool CanDiveTo(string zone) => zone switch
    {
        "Zone_Shallow" => true,
        "Zone_Reef"    => Upgrades["oxygen"] >= 1,
        "Zone_Deep"    => Upgrades["oxygen"] >= 3 && Upgrades["light"] >= 1,
        "Zone_Abyss"   => Upgrades["oxygen"] >= 5 && Upgrades["light"] >= 3 && Upgrades["bag"] >= 4,
        _              => false
    };

    public void ApplyUpgrade(string category)
    {
        Upgrades[category]++;
        EventBus.Instance.EmitUpgradePurchased(category);
    }
}
```

### SaveSystem.cs
```csharp
using Godot;
using Godot.Collections;

public partial class SaveSystem : Node
{
    public static SaveSystem Instance { get; private set; }

    private const string SavePath   = "user://save_data.json";
    private const string BackupPath = "user://save_data.bak";
    private const string Version    = "1.0";

    public override void _Ready() => Instance = this;

    public void SaveGame()
    {
        var data = new Dictionary
        {
            ["version"]   = Version,
            ["timestamp"] = Time.GetUnixTimeFromSystem(),
            ["player"] = new Dictionary
            {
                ["coins"]       = EconomyManager.Instance.Coins,
                ["totalEarned"] = EconomyManager.Instance.TotalEarned,
                ["totalDives"]  = GameManager.Instance.TotalDives
            },
            ["upgrades"]  = new Dictionary(UpgradeManager.Instance.Upgrades),
            ["inventory"] = new Dictionary(EconomyManager.Instance.Inventory)
        };

        string tmpPath = SavePath + ".tmp";
        using var file = FileAccess.Open(tmpPath, FileAccess.ModeFlags.Write);
        file.StoreString(Json.Stringify(data, "\t"));
        file.Close();

        if (FileAccess.FileExists(SavePath))
            DirAccess.CopyAbsolute(SavePath, BackupPath);

        DirAccess.RenameAbsolute(tmpPath, SavePath);
        EventBus.Instance.EmitGameSaved();
    }

    public bool LoadGame()
    {
        if (!FileAccess.FileExists(SavePath)) return false;
        var data = ReadJson(SavePath);
        if (!Validate(data)) return TryRestoreBackup();
        Apply(data);
        return true;
    }

    private bool Validate(Dictionary data) =>
        data.ContainsKey("version") &&
        data.ContainsKey("player") &&
        data.ContainsKey("upgrades");

    private void Apply(Dictionary data)
    {
        var player  = data["player"].AsGodotDictionary();
        var upgDict = data["upgrades"].AsGodotDictionary();
        var invDict = data["inventory"].AsGodotDictionary();

        EconomyManager.Instance.AddCoins((int)player["coins"]);
        GameManager.Instance.TotalDives = (int)player["totalDives"];

        foreach (var key in upgDict.Keys)
            UpgradeManager.Instance.Upgrades[key.AsString()] = upgDict[key].AsInt32();

        foreach (var key in invDict.Keys)
            EconomyManager.Instance.AddToInventory(key.AsString(), invDict[key].AsInt32());
    }

    private bool TryRestoreBackup()
    {
        if (!FileAccess.FileExists(BackupPath)) return false;
        var data = ReadJson(BackupPath);
        if (!Validate(data)) return false;
        Apply(data);
        return true;
    }

    private static Dictionary ReadJson(string path)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        return Json.ParseString(file.GetAsText()).AsGodotDictionary();
    }
}
```

### AudioManager.cs
```csharp
using Godot;

public partial class AudioManager : Node
{
    public static AudioManager Instance { get; private set; }

    private AudioStreamPlayer _musicPlayer;
    private const string UnderwaterBus = "Underwater";
    private const string MasterBus     = "Master";

    public override void _Ready()
    {
        Instance = this;
        _musicPlayer     = new AudioStreamPlayer();
        _musicPlayer.Bus = MasterBus;
        AddChild(_musicPlayer);
    }

    public void PlayMusic(AudioStream stream)
    {
        if (_musicPlayer.Stream == stream) return;
        _musicPlayer.Stream = stream;
        _musicPlayer.Play();
    }

    public async void PlaySfx(AudioStream stream, Vector3 position = default)
    {
        var player    = new AudioStreamPlayer3D();
        player.Stream   = stream;
        player.Position = position;
        GetTree().Root.AddChild(player);
        player.Play();
        await ToSignal(player, AudioStreamPlayer3D.SignalName.Finished);
        player.QueueFree();
    }

    public void SetUnderwater(bool enabled)
    {
        int idx = AudioServer.GetBusIndex(UnderwaterBus);
        if (idx >= 0)
            AudioServer.SetBusEffectEnabled(idx, 0, enabled);
    }
}
```

---

## 14. C# vs GDScript — Migration Notes

### Key Syntax Differences
| Topic | GDScript | C# |
|---|---|---|
| Signal definition | `signal my_signal(value)` | `event Action<T> MySignal` |
| Signal emit | `emit_signal("name", val)` | `MySignal?.Invoke(val)` |
| Signal connect | `node.signal.connect(func)` | `node.Signal += OnMethod` |
| Autoload access | `GameManager.method()` | `GameManager.Instance.Method()` |
| Export variable | `@export var speed: float` | `[Export] public float Speed { get; set; }` |
| Custom Resource | `class_name X extends Resource` | `[GlobalClass] public partial class X : Resource` |
| Await signal | `await signal_name` | `await ToSignal(node, "signal_name")` |

### Autoload Registration Order
Register in `Project -> Project Settings -> Autoloads` **in this exact order**:

| Path | Name |
|---|---|
| `res://scripts/autoloads/EventBus.cs` | `EventBus` |
| `res://scripts/autoloads/GameManager.cs` | `GameManager` |
| `res://scripts/autoloads/EconomyManager.cs` | `EconomyManager` |
| `res://scripts/autoloads/UpgradeManager.cs` | `UpgradeManager` |
| `res://scripts/autoloads/SaveSystem.cs` | `SaveSystem` |
| `res://scripts/autoloads/AudioManager.cs` | `AudioManager` |

> **EventBus must be first** — all other singletons subscribe to it in `_Ready()`.

### C# Performance Tips
- Cache `GetNode<T>()` in `_Ready()`, never in `_Process()`
- No LINQ in `_PhysicsProcess()` — use `for` loops
- Use `struct` for small per-frame data objects
- Expect full recompile on every C# file change (unlike GDScript hot reload)

---

*End of Document — Road to Money GDD v1.0*