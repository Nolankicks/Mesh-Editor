﻿using Sandbox;
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
	private readonly Dictionary<BaseMeshTool.MeshElement, Vector3> _startVertices = new();
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

				EditLog( "Scale Mesh Element", _meshTool.MeshSelection.OfType<BaseMeshTool.MeshElement>()
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