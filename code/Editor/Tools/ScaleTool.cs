using Sandbox;
using System.Linq;
using System.Collections.Generic;

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
public class ScaleTool : EditorTool
{
	private readonly BaseMeshTool _meshTool;
	private readonly Dictionary<MeshVertex, Vector3> _startVertices = new();
	private Vector3 _moveDelta;
	private Vector3 _origin;
	private Rotation _basis;

	public ScaleTool( BaseMeshTool meshTool )
	{
		_meshTool = meshTool;
	}

	public override void OnUpdate()
	{
		base.OnUpdate();

		if ( !_meshTool.MeshSelection.Any() )
			return;

		if ( !Gizmo.HasPressed )
		{
			_startVertices.Clear();
			_moveDelta = default;
			_basis = _meshTool.CalculateSelectionBasis();

			var bounds = _meshTool.CalculateSelectionBounds();
			_origin = bounds.Center;
		}

		using ( Gizmo.Scope( "Tool", new Transform( _origin ) ) )
		{
			Gizmo.Hitbox.DepthBias = 0.01f;

			if ( Gizmo.Control.Scale( "scale", Vector3.Zero, out var delta, _basis ) )
			{
				StartDrag();

				_moveDelta += delta;

				foreach ( var entry in _startVertices )
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

	private void StartDrag()
	{
		if ( _startVertices.Any() )
			return;

		if ( Gizmo.IsShiftPressed )
		{
			foreach ( var face in _meshTool.MeshSelection.OfType<MeshFace>() )
			{
				face.Component.ExtrudeFace( face.Index, face.Component.GetAverageFaceNormal( face.Index ) * 0.01f );
			}

			_meshTool.CalculateSelectionVertices();
		}

		foreach ( var vertex in _meshTool.VertexSelection )
		{
			_startVertices[vertex] = vertex.PositionWorld;
		}
	}
}
