using Godot;
using System.Collections.Generic;

public partial class CollectionInteraction : Area3D
{
	[Export] public float CollectCooldown = 0.5f;

	// ── Tool definitions ─────────────────────────────────────────────────────
	// Radius/Bonus giờ lấy trực tiếp từ UpgradeManager.Instance (UpgradeItemData),
	// không giữ bản sao cục bộ nữa — tránh lệch dữ liệu giữa 2 nơi.
	// Index khớp với UpgradeManager.Instance.GetToolLevel(): 0=Hand | 1=Net | 2=Spear Gun | 3=Vacuum Trap

	// Spear Gun (2) → chỉ bắt moving creatures (CanMove = true)
	// Vacuum Trap (3) → chỉ bắt static resources (CanMove = false)
	// Hand / Net → bắt tất cả
	private enum ToolFilter { All, MoveOnly, StaticOnly }
	private static readonly ToolFilter[] ToolFilters =
	{
		ToolFilter.All,        // Hand
		ToolFilter.All,        // Net
		ToolFilter.MoveOnly,   // Spear Gun
		ToolFilter.StaticOnly, // Vacuum Trap
	};

	// ── State ────────────────────────────────────────────────────────────────
	private float      _cooldownTimer = 0f;
	private StorageBag _storageBag;
	private Node3D     _nearestResource = null;
	private Node3D     _glowTarget      = null;
	private List<(MeshInstance3D mesh, Material originalMat)> _originalMaterials = new();

	private float _bagFullMessageCooldown = 0f;
	private const float BagFullMessageInterval = 1.5f;
	private float _messageInteractInterval = 10f; // giây giữa 2 lần print, chỉnh trong Inspector
	private float _messageInteractTimer = 0f;
	private int _messageInteractCount = 0;

	// Track tool level để detect khi player mua tool mới
	private int _lastToolLevel = -1;

	// ── Ready ─────────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		_storageBag = GetNode<StorageBag>("../StorageBag");
		CollisionLayer = 0;
		CollisionMask  = 1;

		// Subscribe upgrade event để resize radius ngay khi mua
		EventBus.Instance.UpgradePurchased += OnUpgradePurchased;

		// Apply radius theo tool hiện tại
		ApplyToolRadius();
	}

	// ── Process ───────────────────────────────────────────────────────────────
	public override void _Process(double delta)
	{
		if (_cooldownTimer > 0f)
			_cooldownTimer -= (float)delta;

		if (_bagFullMessageCooldown > 0f)
			_bagFullMessageCooldown -= (float)delta;
		if (_messageInteractTimer > 0f)
		{
			_messageInteractTimer -= (float)delta;
		}
		if (_messageInteractTimer < 0f)
		{
			_messageInteractTimer = 0f;
		}

		// Detect tool level thay đổi (fallback nếu event miss)
		int toolLevel = GetToolLevel();
		if (toolLevel != _lastToolLevel)
			ApplyToolRadius();

		var nearest = GetNearestResource();

		if (nearest != _nearestResource)
		{
			if (_nearestResource != null)
				RemoveGlow();

			_nearestResource = nearest;

			if (_nearestResource != null)
			{
				ApplyGlow(_nearestResource);
				if (_messageInteractTimer <= 0.1f && _messageInteractCount < 5) 
				{
					FloatingMessage.Show(GetTree(), "⚠ " + Tr("INTERACTION_GUID"), new Color(0.92f, 0.30f, 0.28f));
					_messageInteractTimer = 10f;
					_messageInteractCount++;
				}
			}
		}

		if (_nearestResource != null && Input.IsActionJustPressed("interact"))
			TryCollect(_nearestResource);
		

	}

	// ── Tool helpers ──────────────────────────────────────────────────────────
	private int GetToolLevel() => UpgradeManager.Instance.GetToolLevel();

	private void ApplyToolRadius()
	{
		int level = GetToolLevel();
		_lastToolLevel = level;

		float radius = UpgradeManager.Instance.GetToolRadius();

		// Tìm CollisionShape3D con đầu tiên và scale radius
		foreach (var child in GetChildren())
		{
			if (child is CollisionShape3D shape)
			{
				if (shape.Shape is SphereShape3D sphere)
				{
					sphere.Radius = radius;
				}
				break;
			}
		}
	}

	private void OnUpgradePurchased(string category, UpgradeItemData data)
	{
		if (category == "tool")
			ApplyToolRadius();
	}

	// ── Resource filtering ───────────────────────────────────────────────────
	private bool PassesToolFilter(Node3D metaNode)
	{
		int toolLevel = GetToolLevel();
		ToolFilter filter = ToolFilters[toolLevel];

		if (filter == ToolFilter.All) return true;

		// Đọc meta "can_move" được set bởi ResourceSpawner từ SeaResource.CanMove
		bool canMove = metaNode.HasMeta("can_move") && metaNode.GetMeta("can_move").AsBool();

		return filter switch
		{
			ToolFilter.MoveOnly   => canMove,
			ToolFilter.StaticOnly => !canMove,
			_                     => true,
		};
	}

	// ── Catch chance roll ─────────────────────────────────────────────────────
	/// <summary>
	/// catchChance = clamp(toolBonus - catchDifficulty + 0.5, 0.05, 1.0)
	/// Hand (0%) vs crab (difficulty 0.3)  → 0 - 0.3 + 0.5 = 70%
	/// Hand (0%) vs squid (difficulty 0.7) → 0 - 0.7 + 0.5 = 30%  (khó bắt tay không)
	/// Net  (25%) vs squid (0.7)           → 0.25 - 0.7 + 0.5 = 55%
	/// Spear Gun (50%) vs fish (0.6)       → 0.50 - 0.6 + 0.5 = 90%
	/// </summary>
	private bool RollCatchChance(Node3D metaNode)
	{
		float bonus = UpgradeManager.Instance.GetToolBonus();

		// Đọc catch_difficulty từ meta (set bởi ResourceSpawner)
		float difficulty = metaNode.HasMeta("catch_difficulty")
			? metaNode.GetMeta("catch_difficulty").AsSingle()
			: 0.3f; // fallback
		float catchChance = Mathf.Clamp(bonus - difficulty + 1.0f, 0.05f, 1.0f);
		GD.Print($"Try To Catch: Bonus:{bonus} difficulty: {difficulty}, change: {catchChance}");
		return GD.Randf() < catchChance;
	}

	// ── GetNearestResource (có filter tool) ──────────────────────────────────
	private Node3D GetNearestResource()
	{
		var bodies = GetOverlappingBodies();
		Node3D nearest = null;
		float  minDist = float.MaxValue;
		foreach (var body in bodies)
		{
			if (body is not Node3D node) continue;

			Node3D metaNode = FindMetaNode(node);
			if (metaNode == null) continue;

			// Bỏ qua nếu tool không phù hợp loại resource
			// if (!PassesToolFilter(metaNode)) continue;

			float dist = GlobalPosition.DistanceTo(metaNode.GlobalPosition);

			if (dist < minDist)
			{
				minDist = dist;
				nearest = metaNode;
			}
		}
		return nearest;
	}

	private Node3D FindMetaNode(Node node)
	{
		while (node != null)
		{
			if (node is Node3D n3d && n3d.HasMeta("resource_id"))
				return n3d;
			node = node.GetParent();
		}
		return null;
	}

	// ── TryCollect ────────────────────────────────────────────────────────────
	private void TryCollect(Node3D body)
	{
		if (_cooldownTimer > 0f) return;

		string resourceId     = body.GetMeta("resource_id").AsString();
		int    resourceValue  = body.GetMeta("resource_value").AsInt32();
		float  resourceWeight = body.GetMeta("resource_weight").AsSingle();

		if (!_storageBag.CanAdd(resourceWeight))
		{
			if (_bagFullMessageCooldown <= 0f)
			{
				FloatingMessage.ShowBagFull(GetTree());
				_bagFullMessageCooldown = BagFullMessageInterval;
			}
			return;
		}

		// ── Catch chance roll ─────────────────────────────────────────────
		if (!RollCatchChance(body))
		{
			// Miss! Cooldown ngắn để player thử lại sớm
			_cooldownTimer = CollectCooldown * 0.5f;

			// body = StaticBody3D (child), còn ResourceMove attach lên root node
			// FindMetaNode() đã leo lên đúng root rồi — cast nó
			var mover = body as ResourceMove ?? FindMetaNode(body) as ResourceMove;
			mover?.OnMissed(GlobalPosition);

			FloatingMessage.ShowMiss(GetTree());
			EventBus.Instance.EmitCatchMissed(GlobalPosition);
			return;
		}

		EventBus.Instance.EmitResourceCollected(resourceId, resourceWeight, 1);
		EventBus.Instance.EmitResourceCaughtFx(resourceId, body.GlobalPosition); 
		_cooldownTimer = CollectCooldown;

		var spawner = GetTree().Root.FindChild("ResourceSpawner", true, false) as ResourceSpawner;
		spawner?.DespawnItem(body);
	}

	// ── Glow ─────────────────────────────────────────────────────────────────
	private void ApplyGlow(Node3D target)
	{
		_originalMaterials.Clear();
		_glowTarget = target;

		// Màu glow theo tool: Hand=cyan, Net=green, SpearGun=orange, Vacuum=purple
		int toolLevel = GetToolLevel();
		Color glowColor = toolLevel switch
		{
			1 => new Color(0.2f, 1.0f, 0.4f),  // Net — xanh lá
			2 => new Color(1.0f, 0.6f, 0.1f),  // Spear Gun — cam
			3 => new Color(0.8f, 0.3f, 1.0f),  // Vacuum — tím
			_ => new Color(0.2f, 0.8f, 1.0f),  // Hand — cyan (default)
		};

		foreach (var mesh in GetAllMeshes(target))
		{
			var original = mesh.GetActiveMaterial(0);
			_originalMaterials.Add((mesh, original));

			if (original != null)
			{
				var glow = original.Duplicate() as Material;
				if (glow is StandardMaterial3D std)
				{
					std.EmissionEnabled          = true;
					std.Emission                 = glowColor;
					std.EmissionEnergyMultiplier = 0.4f;
					mesh.MaterialOverride        = std;
				}
			}
		}
	}

	private void RemoveGlow()
	{
		foreach (var (mesh, original) in _originalMaterials)
		{
			if (IsInstanceValid(mesh))
				mesh.MaterialOverride = null;
		}
		_originalMaterials.Clear();
		_glowTarget = null;
	}

	private List<MeshInstance3D> GetAllMeshes(Node node)
	{
		var result = new List<MeshInstance3D>();
		if (node is MeshInstance3D m) result.Add(m);
		foreach (var child in node.GetChildren())
			result.AddRange(GetAllMeshes(child));
		return result;
	}

	public override void _ExitTree()
	{
		if (EventBus.Instance != null)
			EventBus.Instance.UpgradePurchased -= OnUpgradePurchased;
		RemoveGlow();
	}
}
