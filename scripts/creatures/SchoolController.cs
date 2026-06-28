using Godot;
using System.Collections.Generic;

public partial class SchoolController : Node3D
{
	[Export] public float NeighborRadius = 10f;

	[Export] public float SeparationRadius = 2.5f;

	[Export] public float SeparationWeight = 3f;

	[Export] public float AlignmentWeight = 1f;

	[Export] public float CohesionWeight = 1f;

	[Export] public float LeaderWeight = 3f;

	[Export] public float FlockSpeed = 3.5f;

	[Export] public float FearRadius = 8f;

	private readonly RandomNumberGenerator _rng = new();

	private readonly List<CreatureAI> _members = new();

	private Node3D _player;

	private Vector3 _leaderDirection;

	private float _leaderTimer;

	public override void _Ready()
	{
		_rng.Randomize();

		_player =
			GetTree().Root.FindChild(
				"Player",
				true,
				false
			) as Node3D;

		_leaderDirection =
			Vector3.Forward;

		_leaderTimer = 0f;
	}

	public void RegisterMembers()
	{
		_members.Clear();

		foreach (Node child in GetChildren())
		{
			if (child is CreatureAI ai)
				_members.Add(ai);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

		if (Engine.GetPhysicsFrames() % 3 != 0)
			return;

		UpdateLeader(dt);

		foreach (var fish in _members)
		{
			if (_player != null)
			{
				float dist =
					fish.GlobalPosition.DistanceTo(
						_player.GlobalPosition
					);

				if (dist < FearRadius)
				{
					fish.SetState(
						CreatureAI.CreatureState.Flee
					);

					continue;
				}
			}

			if (fish.GetState() ==
				CreatureAI.CreatureState.Flee)
				continue;

			Vector3 steering =
				CalculateSteering(fish);

			fish.ApplyFlockSteering(
				steering,
				FlockSpeed,
				dt * 3f
			);
		}
	}

	private void UpdateLeader(float dt)
	{
		_leaderTimer -= dt;

		if (_leaderTimer <= 0f)
		{
			_leaderDirection =
				new Vector3(
					_rng.RandfRange(-1f, 1f),
					_rng.RandfRange(-0.2f, 0.2f),
					_rng.RandfRange(-1f, 1f)
				).Normalized();

			_leaderTimer =
				_rng.RandfRange(
					5f,
					12f
				);
		}
	}

	private Vector3 CalculateSteering(
		CreatureAI fish
	)
	{
		Vector3 separation = Vector3.Zero;
		Vector3 alignment = Vector3.Zero;
		Vector3 cohesion = Vector3.Zero;

		int count = 0;

		foreach (var other in _members)
		{
			if (other == fish)
				continue;

			float dist =
				fish.GlobalPosition.DistanceTo(
					other.GlobalPosition
				);

			if (dist > NeighborRadius)
				continue;

			if (dist < SeparationRadius)
			{
				separation -=
					(other.GlobalPosition -
					 fish.GlobalPosition)
					.Normalized()
					*
					(SeparationRadius /
					 Mathf.Max(dist, 0.1f));
			}

			alignment += other.Velocity3D;

			cohesion += other.GlobalPosition;

			count++;
		}

		if (count > 0)
		{
			alignment =
				alignment.Normalized();

			cohesion =
				(cohesion / count -
				 fish.GlobalPosition)
				.Normalized();
		}

		return
			separation * SeparationWeight +
			alignment * AlignmentWeight +
			cohesion * CohesionWeight +
			_leaderDirection * LeaderWeight;
	}
}
