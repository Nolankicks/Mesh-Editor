﻿using Sandbox;
using System.Linq;
using System.Collections.Generic;

namespace Editor.MeshEditor;

/// <summary>
/// Rotate selected Mesh Elements.<br/> <br/> 
/// <b>Ctrl</b> - toggle snap to grid
/// </summary>
[Title( "Rotate" )]
[Icon( "360" )]
[Alias( "mesh.rotate" )]
[Group( "1" )]
[Shortcut( "mesh.rotate", "e" )]
public class RotateTool : BaseTransformTool
{
	private readonly Dictionary<MeshVertex, Vector3> _startVertices = new();
	private Angles _moveDelta;
	private Vector3 _origin;
	private Rotation _basis;

	public RotateTool( BaseMeshTool meshTool ) : base( meshTool )
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
			_origin = MeshTool.CalculateSelectionOrigin();
		}

		using ( Gizmo.Scope( "Tool", new Transform( _origin, _basis ) ) )
		{
			Gizmo.Hitbox.DepthBias = 0.01f;

			if ( Gizmo.Control.Rotate( "rotation", out var angleDelta ) )
			{
				StartDrag();

				_moveDelta += angleDelta;
				var snapDelta = Gizmo.Snap( _moveDelta, _moveDelta );

				foreach ( var entry in _startVertices )
				{
					var rotation = _basis * snapDelta * _basis.Inverse;
					var position = entry.Value - _origin;
					position *= rotation;
					position += _origin;

					var transform = entry.Key.Transform;
					entry.Key.Component.SetVertexPosition( entry.Key.Index, transform.PointToLocal( position ) );
				}

				EditLog( "Rotate Mesh Element", null );
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

			MeshTool.CalculateSelectionVertices();
		}

		foreach ( var vertex in MeshTool.VertexSelection )
		{
			_startVertices[vertex] = vertex.PositionWorld;
		}
	}
}
