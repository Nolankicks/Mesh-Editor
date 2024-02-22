using Sandbox;
using System.Linq;
using System.Collections.Generic;

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
	private Vector3 _startOrigin;
	private bool _dragging;

	public MeshRotateTool( BaseMeshTool meshTool )
	{
		_meshTool = meshTool;
	}

	public override void OnUpdate()
	{
		base.OnUpdate();

		if ( !Gizmo.HasPressed )
		{
			_startVertices.Clear();
			_moveDelta = default;
			_dragging = false;
		}

		if ( !_meshTool.MeshSelection.Any() )
			return;

		var bbox = _meshTool.CalculateSelectionBounds();
		var handlePosition = _dragging ? _startOrigin : bbox.Center;
		var handleRotation = Rotation.Identity;

		using ( Gizmo.Scope( "Tool", new Transform( handlePosition ) ) )
		{
			Gizmo.Hitbox.DepthBias = 0.01f;

			if ( Gizmo.Control.Rotate( "rotation", out var angleDelta ) )
			{
				StartDrag();
				handlePosition = _dragging ? _startOrigin : bbox.Center;

				_moveDelta += angleDelta;
				var snapped = Gizmo.Snap( _moveDelta, _moveDelta );

				foreach ( var entry in _startVertices )
				{
					var transform = entry.Key.Component.Transform.World.WithPosition( -handlePosition );
					var w = transform.PointToWorld( entry.Value );
					w *= snapped;

					entry.Key.Component.SetVertexPosition( entry.Key.Index, transform.PointToLocal( w ) );
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

		_startOrigin = _meshTool.CalculateSelectionBounds().Center;
		_dragging = true;
	}
}
