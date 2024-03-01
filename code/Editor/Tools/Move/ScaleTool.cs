﻿using Sandbox;
using System.Linq;

namespace Editor.MeshEditor;

/// <summary>
/// Scale selected Mesh Elements.<br/> <br/> 
/// <b>Ctrl</b> - toggle snap to grid<br/>
/// <b>Shift</b> - extrude selection
/// </summary>
[Title( "Scale" )]
[Icon( "zoom_out_map" )]
[Alias( "mesh.scale" )]
[Group( "2" )]
[Shortcut( "mesh.scale", "r" )]
public sealed class ScaleTool : BaseMoveTool
{
	private Vector3 _moveDelta;
	private Vector3 _size;
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

			var bounds = BBox.FromPoints( MeshTool.VertexSelection
				.Select( x => _basis.Inverse * x.PositionWorld ) );

			_size = bounds.Size;
			_origin = bounds.Center * _basis;

			if ( _size.x < 0.1f ) _size.x = 0;
			if ( _size.y < 0.1f ) _size.y = 0;
			if ( _size.z < 0.1f ) _size.z = 0;
		}

		using ( Gizmo.Scope( "Tool", new Transform( _origin ) ) )
		{
			Gizmo.Hitbox.DepthBias = 0.01f;

			if ( Gizmo.Control.Scale( "scale", Vector3.Zero, out var delta, _basis ) )
			{
				_moveDelta += delta / 0.01f;

				var size = _size + Gizmo.Snap( _moveDelta, _moveDelta ) * 2.0f;
				var scale = new Vector3(
					_size.x != 0 ? size.x / _size.x : 1,
					_size.y != 0 ? size.y / _size.y : 1,
					_size.z != 0 ? size.z / _size.z : 1
				);

				StartDrag();

				foreach ( var entry in StartVertices )
				{
					var position = (entry.Value - _origin) * _basis.Inverse;
					position *= scale;
					position *= _basis;
					position += _origin;

					var transform = entry.Key.Transform;
					entry.Key.Component.PolygonMesh.SetVertexPosition( entry.Key.Index, transform.PointToLocal( position ) );
				}

				EditLog( "Scale Mesh Element", null );
			}
		}
	}
}
