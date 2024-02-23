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
public class RotateTool : EditorTool
{
	private readonly BaseMeshTool _meshTool;
	private readonly Dictionary<BaseMeshTool.MeshElement, Vector3> _startVertices = new();
	private Angles _moveDelta;
	private Vector3 _origin;
	private Rotation _basis;

	public RotateTool( BaseMeshTool meshTool )
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
			_origin = _meshTool.CalculateSelectionOrigin();
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

				EditLog( "Rotate Mesh Element", _meshTool.MeshSelection.OfType<BaseMeshTool.MeshElement>()
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
			foreach ( var s in _meshTool.MeshSelection.OfType<BaseMeshTool.MeshElement>()
				.Where( x => x.ElementType == BaseMeshTool.MeshElementType.Face ) )
			{
				s.Component.ExtrudeFace( s.Index, s.Component.GetAverageFaceNormal( s.Index ) * 0.01f );
			}

			_meshTool.CalculateSelectionVertices();
		}

		foreach ( var entry in _meshTool.VertexSelection )
		{
			_startVertices[entry] = entry.Transform.PointToWorld( entry.Component.GetVertexPosition( entry.Index ) );
		}
	}
}