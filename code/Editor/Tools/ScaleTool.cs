using Sandbox;

namespace Editor.MeshEditor;

/// <summary>
/// Scale selected Mesh Elements.<br/> <br/> 
/// <b>Ctrl</b> - toggle snap to grid<br/>
/// <b>Shift</b> - scale all 3 axis
/// </summary>
[Title( "Scale" )]
[Icon( "zoom_out_map" )]
[Alias( "mesh.scale" )]
[Group( "2" )]
[Shortcut( "mesh.scale", "r" )]
public class ScaleTool : BaseTransformTool
{
	private Vector3 _moveDelta;
	private Vector3 _origin;
	private Rotation _basis;

	public ScaleTool( BaseMeshTool meshTool ) : base( meshTool )
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

			var bounds = MeshTool.CalculateSelectionBounds();
			_origin = bounds.Center;
		}

		using ( Gizmo.Scope( "Tool", new Transform( _origin ) ) )
		{
			Gizmo.Hitbox.DepthBias = 0.01f;

			if ( Gizmo.Control.Scale( "scale", Vector3.Zero, out var delta, _basis ) )
			{
				_moveDelta += delta;

				StartDrag();

				foreach ( var entry in StartVertices )
				{
					var position = (entry.Value - _origin) * _basis.Inverse;
					position += position * _moveDelta;
					position *= _basis;
					position += _origin;

					var transform = entry.Key.Transform;
					entry.Key.Component.SetVertexPosition( entry.Key.Index, transform.PointToLocal( position ) );
				}

				EditLog( "Scale Mesh Element", null );
			}
		}
	}
}
