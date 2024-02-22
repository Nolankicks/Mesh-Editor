using Sandbox;
using System.Linq;
using System.Collections.Generic;

namespace Editor;

/// <summary>
/// Move, rotate and scale mesh faces
/// </summary>
[EditorTool]
[Title( "Face" )]
[Icon( "change_history" )]
[Alias( "Face" )]
[Group( "3" )]
public class FaceTool : BaseMeshTool
{
	private readonly Dictionary<MeshElement, Vector3> _startVertices = new();
	private Vector3 _moveDelta;

	public override IEnumerable<EditorTool> GetSubtools()
	{
		return default;
	}

	public override void OnUpdate()
	{
		base.OnUpdate();

		var tr = MeshTrace.Run();

		if ( tr.Hit && tr.Component is EditorMeshComponent component )
		{
			using ( Gizmo.ObjectScope( tr.GameObject, tr.GameObject.Transform.World ) )
			{
				Gizmo.Hitbox.DepthBias = 1;
				Gizmo.Hitbox.TrySetHovered( tr.Distance );

				var face = component.TriangleToFace( tr.Triangle );

				if ( Gizmo.WasClicked )
				{
					Select( MeshElement.Face( component, face ) );
				}
			}
		}
		else if ( !Gizmo.HasPressed && Gizmo.HasClicked )
		{
			MeshSelection.Clear();
		}

		UpdateMoveGizmo();
	}

	private void UpdateMoveGizmo()
	{
		foreach ( var s in MeshSelection.OfType<MeshElement>()
			.Where( x => x.ElementType == MeshElementType.Face ) )
		{
			Gizmo.Draw.Color = Color.Green;
			var p = s.Component.Transform.World.PointToWorld( s.Component.GetFaceCenter( s.Index ) );
			Gizmo.Draw.SolidSphere( p, 4 );
		}

		if ( !Gizmo.HasPressed )
		{
			_startVertices.Clear();
			_moveDelta = default;
		}

		if ( !MeshSelection.Any() )
			return;

		var bbox = CalculateSelectionBounds();
		var handlePosition = bbox.Center;
		var handleRotation = Rotation.Identity;

		using ( Gizmo.Scope( "Tool", new Transform( handlePosition ) ) )
		{
			Gizmo.Hitbox.DepthBias = 0.01f;

			if ( Gizmo.Control.Position( "position", Vector3.Zero, out var delta, handleRotation ) )
			{
				_moveDelta += delta;

				StartDrag();

				var targetPosition = Gizmo.Snap( _moveDelta, _moveDelta );

				foreach ( var entry in _startVertices )
				{
					var transform = entry.Key.Component.Transform.World;
					entry.Key.Component.SetVertexPosition( entry.Key.Index, transform.PointToLocal( entry.Value + targetPosition ) );
				}

				EditLog( "Moved", MeshSelection.OfType<MeshElement>()
					.Select( x => x.Component )
					.Distinct() );
			}
		}
	}

	private void StartDrag()
	{
		if ( _startVertices.Any() )
			return;

		if ( Gizmo.IsShiftPressed )
		{
			foreach ( var s in MeshSelection.OfType<MeshElement>() )
			{
				if ( s.ElementType != MeshElementType.Face )
					continue;

				s.Component.ExtrudeFace( s.Index, s.Component.GetAverageFaceNormal( s.Index ) * 0.01f );
			}

			CalculateSelectionVertices();
		}

		foreach ( var entry in VertexSelection )
		{
			_startVertices[entry] = entry.Component.Transform.World.PointToWorld( entry.Component.GetVertexPosition( entry.Index ) );
		}
	}
}
