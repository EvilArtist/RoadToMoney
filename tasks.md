# Road to Money — Task Tracker

> Cập nhật theo tiến độ thực tế. Đánh dấu `[x]` khi hoàn thành từng bước.

---

## GROUP 1 — Project Setup

### 1.1 Cài đặt môi trường
- [x] Tải `Godot_v4.3-stable_mono_win64` (phiên bản Mono/C#)
- [x] Cài `.NET SDK 8.0` từ https://dotnet.microsoft.com/download
- [x] Khởi động lại máy sau khi cài .NET

### 1.2 Tạo Project
- [x] Mở Godot → Project Manager → nhấn **Create**
- [x] Project Name: `road-to-money`
- [x] Chọn folder đã tạo sẵn
- [x] Renderer: **Forward+**
- [x] Nhấn **Create & Edit**

### 1.3 Project Settings
- [x] Vào `Project → Project Settings → General`
- [x] Display → Window → Size → Viewport Width: `1920`
- [x] Display → Window → Size → Viewport Height: `1080`
- [x] Display → Window → Stretch → Mode: `canvas_items`
- [x] Physics → 3D → Default Gravity: `9.8`

### 1.4 Tạo cấu trúc thư mục
- [x] Tạo toàn bộ cấu trúc thư mục theo GDD

---

## GROUP 2 — Autoload Singletons
- [x] Tạo và đăng ký 6 Autoloads: `EventBus`, `GameManager`, `EconomyManager`, `UpgradeManager`, `SaveSystem`, `AudioManager`

---

## GROUP 3 — Player Scene
- [x] Tạo `Player.tscn` với đầy đủ node hierarchy
- [x] Setup CollisionShape, CollectionArea, Scripts
- [x] Setup Input Map
- [x] Test scene chạy thử

---

## GROUP 4 — Ocean World & Zones
- [x] Tạo `OceanWorld.tscn` root scene
- [x] Tạo `WorldEnvironment` với sky và fog
- [x] Tạo `OceanSurface` — WaterPlane mesh
- [x] Tạo 4 zone subscenes
- [x] Setup depth-based fog và lighting
- [x] Viết underwater visual shader
- [x] Seabed nghiêng dần theo trục Z (shader-based, không dùng rotation)
- [x] `GetSeabedY(x, z)` đồng bộ giữa SwimController, CreatureSpawner, EnvironmentSpawner, ResourceSpawner
- [ ] Bake NavigationRegion3D cho từng zone
- [ ] Caustics projected texture
- [ ] Coral reef MultiMeshInstance3D
- [ ] Seaweed với vertex shader sway
- [ ] Setup LOD 3 levels

---

## GROUP 5 — Resource System
- [x] Tạo `SeaResource.cs` — GlobalClass với đầy đủ properties
- [x] Thêm Spawn Distribution fields: `SpawnPeakDistance`, `SpawnSigma`, `SpawnCount`
- [x] Thêm Behavior fields: `FearRadius`, `CanFlee`
- [x] Tạo 7 file `.tres`: shell, clam, shrimp, crab, squid, small_fish, large_fish
- [x] Set Spawn Distribution values cho từng `.tres`
- [x] Viết `ResourceSpawner.cs` — Gaussian distribution spawn theo distance
- [x] Viết `CollectionInteraction.cs` — detect, highlight glow, collect bằng E
- [x] Viết `ResourceMove.cs` — wander + flee khi player lại gần
- [x] Cập nhật `StorageBag.cs` — tính weight thực từ `SeaResource.Weight`
- [x] Glow effect khi lại gần resource (emission duplicate material)

---

## GROUP 6 — Creature AI
### 6.1 Base AI
- [x] Tạo `CreatureAI.cs` — FSM: Idle, Wander, Flee, School, Hiding, Stunned
- [x] Tạo `CreatureData.cs` — GlobalClass custom Resource
- [ ] Setup NavigationRegion3D trong mỗi zone

### 6.2 Fish School (Boids)
- [x] Tạo `SchoolController.cs` — Boids algorithm
- [x] Mỗi group có SchoolController riêng
- [x] Fix Boids weights: SeparationWeight > CohesionWeight
- [x] Thêm wander force để tránh deadlock
- [x] Thêm `FearRadius` — school flee khi player lại gần
- [x] Tạo `Fish.tscn` template scene
- [x] Test flocking behavior

### 6.3 Các creature khác
- [ ] Tạo `Crab.tscn` + hiding state logic
- [ ] Tạo `Squid.tscn` + ink burst VFX khi flee
- [ ] Tạo `Octopus.tscn` + camouflage shader
- [ ] Tạo `Turtle.tscn` — Path3D patrol, uncatchable
- [ ] Tạo `Jellyfish.tscn` — Path3D patrol, uncatchable

### 6.4 Spawn System
- [x] Viết `CreatureSpawner.cs` — zone-aware, Gaussian distribution
- [x] Spawn cách seabed ít nhất 1.5–3m
- [x] AI throttle: update mỗi 3 physics frames
- [ ] Pre-warm pool: 20 instances mỗi loài
- [ ] Max 60 active agents — deactivate khi vượt

---

## GROUP 7 — Economy & Shop UI
- [x] Tạo `ShopScreen.tscn` — sell flow UI
- [x] Tạo `HUD.tscn` — oxygen bar, bag weight, coin counter, depth meter
- [x] Viết sell price formula
- [ ] Tạo `UpgradeScreen.tscn` — upgrade tree UI
- [ ] Tạo `UpgradeData.cs` — GlobalClass custom Resource
- [ ] Tạo 17 file `.tres` upgrades (bag 5, tool 4, oxygen 5, light 3)
	- Bag: bag_1.tres → bag_5.tres (10/20/30/45/60 kg, cost 100/300/700/1500/4000)
	- Tool: tool_1.tres → tool_4.tres (Hand/Net/Spear Gun/Vacuum Trap, cost 200/600/1800/5000)
	- Oxygen: oxygen_1.tres → oxygen_5.tres (60/90/130/200/300s, cost 150/400/900/2500/6000)
	- Light: light_1.tres → light_3.tres (None→8m/8m→16m/16m→28m, cost 250/800/3000)
- [ ] Zone gating: `UpgradeManager.CanDiveTo(zone)`
- [ ] Market variance: reset hàng ngày ±15%
- [ ] Freshness decay sau oxygen warning threshold
- [ ] Test full loop: dive → collect → surface → sell → buy upgrade → dive lại

---

## GROUP 8 — Gameplay Core Rules

> Các rule gameplay cốt lõi — implement song song với GROUP 7

### 8.1 Death by Oxygen
- [X] Khi O2 = 0 → trigger death sequence:
- [X] Màn hình fade to black (0.8s)
- [X] Hiện "You drowned." text giữa màn hình
- [X] Xóa toàn bộ inventory (mất hết hải sản đã bắt)
- [X] Giữ nguyên coins và upgrades đã mua (không mất progress kinh tế)
- [X] Sau 2s → respawn: teleport player về (0, 0, 0), O2 reset về max
- [X] ResourceSpawner respawn lại toàn bộ resources trong zone
- [X] GameManager chuyển state về Surface
- [X] HUD O2 bar pulse đỏ khi O2 < 25% (đã có) — thêm heartbeat SFX trigger
- [X] Test: lặn không lên, chờ hết O2 → xác nhận death flow hoạt động đúng

### 8.2 Surface Reset Position

- [X] Khi player ngoi lên mặt nước (trigger PlayerSurfaced):
- [X] Teleport XZ về (0, 0) — giữ Y ở mặt nước
- [X] Reset player velocity về zero
- [X] Camera snap về forward direction (không giữ rotation cũ)
- [X] Đảm bảo ShopScreen mở sau khi teleport xong (không bị offset UI)
- [X] Test: lặn xa về phía Z âm, ngoi lên → xác nhận về đúng (0, surfaceY, 0)

## GROUP 9 — Save System Integration
- [ ] Hoàn thiện `SaveSystem.cs` — Apply() đọc đúng từ JSON
- [ ] Auto-save khi ngoi lên mặt nước
- [ ] Auto-save khi mua upgrade
- [ ] Auto-save timer mỗi 5 phút
- [ ] Test save/load/corrupt fallback

---

## GROUP 10 — Audio
- [ ] Tạo Audio Bus Layout: Master, Music, SFX, Underwater
- [ ] Thêm EQ filter trên bus Underwater
- [ ] Ambient ocean sound loop
- [ ] Spatial SFX cho từng loài creature
- [ ] Surface splash, oxygen warning beep, coin collect, upgrade purchase sounds

---

## GROUP 10 — UI & Polish
- [ ] Tạo `MainMenu.tscn`
- [ ] Tạo `SettingsScreen.tscn`
- [ ] Tạo `PauseMenu.tscn`
- [ ] Tạo `Logbook.tscn` — bestiary
- [ ] Animated scene transitions
- [ ] Game over screen khi hết oxygen

---

## GROUP 11 — Performance & QA
- [ ] Profile CPU < 8ms, GPU < 12ms
- [ ] RAM peak < 1.5 GB
- [ ] Hardening save system
- [ ] Build pipeline — export Windows .exe

---

## GROUP 12 — Release
- [ ] Final balance pass
- [ ] Trailer capture
- [ ] Itch.io / Steam page setup
- [ ] Upload build

---

## GROUP 13 — Gameplay Redesign
- [x] Swim Controls Redesign — Space dive/swim, mouse look
- [x] Ocean Floor Redesign — shader-based slope
- [x] Resource Spawn on Floor — raycast/formula based

---

## GROUP 14 — Visual Polish
- [x] Underwater Fog & Color Shift theo độ sâu
- [x] WaterPlane Material — transparent, refraction, wave
- [x] Bubble Particles theo player
- [x] Basic Audio — splash, bubble, surface sounds

---

## GROUP 15 — 3D Models cho Resources
- [x] Import và gán model cho tất cả 7 resource types
- [x] `ScaleMultiplier` per-resource để chỉnh scale không cần hardcode
- [x] Collision shape đúng cho từng model

---

## GROUP 16 — Bug Fixes (Sprint hiện tại)
- [x] Lỗi 3: Không bắt được resource — `FindMetaNode` leo lên parent
- [x] Lỗi 2: SchoolFish cluster — tách SchoolController riêng mỗi group
- [x] Lỗi 1: Player xuyên đáy — `GetSeabedY()` formula thay collision shape
- [x] Glow highlight thay vòng tròn đỏ
- [x] Resource spawn theo Gaussian distribution per-loài
- [x] `FearRadius` cho Fish School và Resource (CanFlee)
- [x] Rock collision — StaticBody3D thủ công hoặc bake lúc runtime

---

*Cập nhật lần cuối: Sprint 3 — Bug fixes & Polish hoàn thành*
