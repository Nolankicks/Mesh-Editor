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
	private bool _nudge = false;

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
		var points = new List<Vector3>();

		foreach ( var s in MeshSelection.OfType<MeshElement>() )
		{
			if ( s.ElementType != MeshElementType.Face )
				continue;

			Gizmo.Draw.Color = Color.Green;
			var p = s.Component.Transform.World.PointToWorld( s.Component.GetFaceCenter( s.Index ) );
			Gizmo.Draw.SolidSphere( p, 4 );

			points.Add( p );
		}

		if ( !Gizmo.HasPressed )
		{
			_startVertices.Clear();
			_moveDelta = default;
		}

		if ( points.Count == 0 )
			return;

		var bbox = BBox.FromPoints( points );
		var handlePosition = bbox.Center;
		var handleRotation = Rotation.Identity;

		if ( !Gizmo.HasPressed )
		{
			var delta = Vector3.Zero;
			delta += Application.IsKeyDown( KeyCode.Up ) ? Vector3.Up : 0.0f;
			delta += Application.IsKeyDown( KeyCode.Down ) ? Vector3.Down : 0.0f;
			delta += Application.IsKeyDown( KeyCode.Right ) ? Vector3.Forward : 0.0f;
			delta += Application.IsKeyDown( KeyCode.Left ) ? Vector3.Backward : 0.0f;

			if ( delta.Length > 0.0f )
			{
				if ( !_nudge )
				{
					var offset = handlePosition.SnapToGrid( Gizmo.Settings.GridSpacing ) - handlePosition;
					offset += Gizmo.Settings.GridSpacing;
					offset *= delta;

					if ( Gizmo.IsShiftPressed )
					{
						foreach ( var entry in MeshSelection.OfType<MeshElement>() )
						{
							if ( entry.ElementType != MeshElementType.Face )
								continue;

							var rotation = entry.Component.Transform.Rotation;
							entry.Component.ExtrudeFace( entry.Index, rotation.Inverse * offset );
						}
					}
					else
					{
						foreach ( var entry in MeshSelection.OfType<MeshElement>()
							.SelectMany( x => x.Component.GetFaceVertices( x.Index )
							.Select( i => MeshElement.Vertex( x.Component, i ) )
							.Distinct() ) )
						{
							var rotation = entry.Component.Transform.Rotation;
							var localOffset = (entry.Component.GetVertexPosition( entry.Index ) * rotation) + offset;
							entry.Component.SetVertexPosition( entry.Index, rotation.Inverse * localOffset );
						}
					}

					EditLog( "Moved", null );

					_nudge = true;
				}
			}
			else
			{
				_nudge = false;
			}
		}

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
		}

		foreach ( var entry in MeshSelection.OfType<MeshElement>()
			.SelectMany( x => x.Component.GetFaceVertices( x.Index )
			.Select( i => MeshElement.Vertex( x.Component, i ) )
			.Distinct() ) )
		{
			_startVertices[entry] = entry.Component.Transform.World.PointToWorld( entry.Component.GetVertexPosition( entry.Index ) );
		}
	}
}
