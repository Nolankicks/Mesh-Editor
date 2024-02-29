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
	private readonly Dictionary<MeshVertex, Vector3> _startVertices = new();
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
			_startVertices.Clear();
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

				foreach ( var entry in _startVertices )
				{
					var position = entry.Value + moveDelta;
					var transform = entry.Key.Transform;
					entry.Key.Component.SetVertexPosition( entry.Key.Index, transform.PointToLocal( position ) );
				}

				EditLog( "Move Mesh Element", null );
			}
		}
	}

	private void StartDrag()
	{
		if ( _startVertices.Any() )
			return;

		if ( Gizmo.IsShiftPressed )
		{
			foreach ( var face in MeshTool.MeshSelection.OfType<MeshFace>() )
			{
				face.Component.ExtrudeFace( face.Index );
			}

			var edge = MeshTool.MeshSelection.OfType<MeshEdge>().FirstOrDefault();
			if ( edge.IsValid() )
			{
				edge = new MeshEdge( edge.Component, edge.Component.ExtrudeEdge( edge.Index ) );
				if ( edge.IsValid() )
					MeshTool.MeshSelection.Set( edge );
			}

			MeshTool.CalculateSelectionVertices();
		}

		foreach ( var vertex in MeshTool.VertexSelection )
		{
			_startVertices[vertex] = vertex.PositionWorld;
		}
	}
}
