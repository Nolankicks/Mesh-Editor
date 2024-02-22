﻿using Sandbox;
using System.Linq;
using System.Collections.Generic;
using Editor.MapDoc;

namespace Editor;

/// <summary>
/// Rotate selected Mesh Elements.<br/> <br/> 
/// <b>Ctrl</b> - toggle snap to grid
/// </summary>
[Title( "Rotate" )]
[Icon( "360" )]
[Alias( "mesh.rotate" )]
[Group( "1" )]
[Shortcut( "mesh.rotate", "e" )]
public class MeshRotateTool : EditorTool
{
	private readonly BaseMeshTool _meshTool;
	private readonly Dictionary<BaseMeshTool.MeshElement, Vector3> _startVertices = new();
	private Angles _moveDelta;
	private Vector3 _origin;
	private Rotation _basis;

	public MeshRotateTool( BaseMeshTool meshTool )
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
			_origin = _meshTool.CalculateSelectionBounds().Center;

			_basis = Rotation.Identity;

			if ( !Gizmo.Settings.GlobalSpace )
			{
				var faceElement = _meshTool.MeshSelection.OfType<BaseMeshTool.MeshElement>()
					.FirstOrDefault( x => x.ElementType == BaseMeshTool.MeshElementType.Face );

				var normal = faceElement.Component.GetAverageFaceNormal( faceElement.Index );
				var vAxis = EditorMeshComponent.ComputeTextureVAxis( normal );
				_basis = Rotation.LookAt( normal, vAxis * -1.0f );
				_basis = faceElement.Component.Transform.World.RotationToWorld( _basis );
			}
		}

		using ( Gizmo.Scope( "Tool", new Transform( _origin, _basis ) ) )
		{
			Gizmo.Hitbox.DepthBias = 0.01f;

			if ( Gizmo.Control.Rotate( "rotation", out var angleDelta ) )
			{
				StartDrag();

				_moveDelta += angleDelta;
				var snapped = Gizmo.Snap( _moveDelta, _moveDelta );

				foreach ( var entry in _startVertices )
				{
					var rot = _basis * snapped * _basis.Inverse;
					var p = entry.Value - _origin;
					p *= rot;
					p += _origin;

					var transform = entry.Key.Component.Transform.World;
					entry.Key.Component.SetVertexPosition( entry.Key.Index, transform.PointToLocal( p ) );
				}

				EditLog( "Rotated", _meshTool.MeshSelection.OfType<BaseMeshTool.MeshElement>()
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
			_startVertices[entry] = entry.Component.Transform.World.PointToWorld( entry.Component.GetVertexPosition( entry.Index ) );
		}
	}
}
