using Sandbox;

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
	private Vector3 _origin;
	private Rotation _basis;

	public MoveTool( BaseMeshTool meshTool ) : base( meshTool )
	{
	}

	public override void OnUpdate()
	{
		base.OnUpdate();

		if ( !MeshTool.MeshSelection.Any() )
			return;

		var bounds = MeshTool.CalculateSelectionBounds();
		var origin = bounds.Center;

		if ( !Gizmo.HasPressed )
		{
			StartVertices.Clear();
			_moveDelta = default;
			_basis = MeshTool.CalculateSelectionBasis();
			_origin = bounds.Center;
		}

		using ( Gizmo.Scope( "Tool", new Transform( origin ) ) )
		{
			Gizmo.Hitbox.DepthBias = 0.01f;

			if ( Gizmo.Control.Position( "position", Vector3.Zero, out var delta, _basis ) )
			{
				_moveDelta += delta;

				StartDrag();

				var moveDelta = _moveDelta * _basis.Inverse;
				moveDelta = Gizmo.Snap( (_origin * _basis.Inverse) + moveDelta, moveDelta ) - _origin;
				moveDelta *= _basis;

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
