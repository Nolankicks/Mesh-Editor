using Sandbox;
using System.Linq;
using System.Collections.Generic;

namespace Editor.MeshEditor;

/// <summary>
/// Move selected Mesh Elements.<br/> <br/> 
/// <b>Ctrl</b> - toggle snap to grid<br/>
/// <b>Shift</b> - extrude selection
/// </summary>
[Title( "Move/Position" )]
[Icon( "control_camera" )]
[Alias( "mesh.move" )]
[Group( "0" )]
[Shortcut( "mesh.move", "w" )]
public class MoveTool : BaseTransformTool
{
	private Vector3 _moveDelta;
	private Rotation _basis;

	public MoveTool( BaseMeshTool meshTool ) : base( meshTool )
	{

	}

	public override void OnUpdate()
	{
		base.OnUpdate();

		if ( !MeshTool.MeshSelection.Any() )
			return;

		if ( !Gizmo.HasPressed )
		{
			StartVertices.Clear();
			_moveDelta = default;
			_basis = MeshTool.CalculateSelectionBasis();
		}

		var bounds = MeshTool.CalculateSelectionBounds();
		var origin = bounds.Center;

		using ( Gizmo.Scope( "Tool", new Transform( origin ) ) )
		{
			Gizmo.Hitbox.DepthBias = 0.01f;

			if ( Gizmo.Control.Position( "position", Vector3.Zero, out var delta, _basis ) )
			{
				_moveDelta += delta;

				StartDrag();

				var moveDelta = _moveDelta;
				moveDelta *= _basis.Inverse;
				moveDelta = _basis * Gizmo.Snap( moveDelta, moveDelta );

				foreach ( var entry in StartVertices )
				{
					var position = entry.Value + moveDelta;
					var transform = entry.Key.Transform;
					entry.Key.Component.SetVertexPosition( entry.Key.Index, transform.PointToLocal( position ) );
				}

				EditLog( "Move Mesh Element", null );
			}
		}
	}
}
