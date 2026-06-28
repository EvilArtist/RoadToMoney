using Godot;

/// <summary>
/// ZoneBounds — tự dựng 4 tường biên (Bắc/Nam/Đông/Tây) quanh 1 zone,
/// chặn không cho camera nhìn xuyên ra ngoài seabed mesh ra tới Background trần.
/// Gắn vào Node3D gốc của mỗi Zone_X.tscn, set 4 giá trị Inspector là xong.
/// </summary>
public partial class ZoneBounds : Node3D
{
	[ExportGroup("Zone Size")]
	[Export] public float SizeX   = 800f;   // chiều ngang (trục X, hướng bơi qua trái/phải)
	[Export] public float SizeZ   = 400f;   // chiều dài (trục Z, hướng bơi ra xa bờ)
	[Export] public float TopY    = 0f;     // mặt nước
	[Export] public float BottomY = -15f;   // điểm sâu nhất của zone này

	[ExportGroup("Color")]
	[Export] public Color WallColor = new Color(0f, 0.01f, 0.04f); // khớp deep-tier fog

	public override void _Ready() => BuildWalls();

	private void BuildWalls()
	{
		float height = TopY - BottomY;
		float midY   = (TopY + BottomY) * 0.5f;

		BuildWall("WallNorth", new Vector3(0f, midY, -SizeZ / 2f), SizeX, height, Vector3.Zero);
		BuildWall("WallSouth", new Vector3(0f, midY,  SizeZ / 2f), SizeX, height, new Vector3(0f, 180f, 0f));
		BuildWall("WallEast",  new Vector3(SizeX / 2f, midY, 0f), SizeZ, height, new Vector3(0f, 90f, 0f));
		BuildWall("WallWest",  new Vector3(-SizeX / 2f, midY, 0f), SizeZ, height, new Vector3(0f, -90f, 0f));
	}

	private void BuildWall(string name, Vector3 pos, float width, float height, Vector3 rotDeg)
	{
		var body = new StaticBody3D { Name = name, Position = pos, RotationDegrees = rotDeg };
		AddChild(body);

		var mesh = new BoxMesh { Size = new Vector3(width, height, 0.5f) };
		var meshInstance = new MeshInstance3D { Mesh = mesh };

		var mat = new StandardMaterial3D
		{
			AlbedoColor  = WallColor,
			ShadingMode  = BaseMaterial3D.ShadingModeEnum.Unshaded
		};
		meshInstance.MaterialOverride = mat;
		body.AddChild(meshInstance);

		var collider = new CollisionShape3D
		{
			Shape = new BoxShape3D { Size = new Vector3(width, height, 0.5f) }
		};
		body.AddChild(collider);
	}
}
