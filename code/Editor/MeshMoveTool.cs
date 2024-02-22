using Sandbox;
using System.Linq;
using System.Collections.Generic;

namespace Editor;

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
public class MeshMoveTool : EditorTool
{
	private readonly BaseMeshTool _meshTool;
	private readonly Dictionary<BaseMeshTool.MeshElement, Vector3> _startVertices = new();
	private Vector3 _moveDelta;
	private Rotation _basis;

	public MeshMoveTool( BaseMeshTool meshTool )
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

			if ( Gizmo.Settings.GlobalSpace )
			{
				_basis = Rotation.Identity;
			}
			else
			{
				var faceElement = _meshTool.MeshSelection.OfType<BaseMeshTool.MeshElement>()
					.FirstOrDefault( x => x.ElementType == BaseMeshTool.MeshElementType.Face );

				var normal = faceElement.Component.GetAverageFaceNormal( faceElement.Index );
				var vAxis = EditorMeshComponent.ComputeTextureVAxis( normal );
				var transform = faceElement.Component.Transform.World;
				_basis = Rotation.LookAt( normal, vAxis * -1.0f );
				_basis = transform.RotationToWorld( _basis );
			}
		}

		var bbox = _meshTool.CalculateSelectionBounds();

		using ( Gizmo.Scope( "Tool", new Transform( bbox.Center ) ) )
		{
			Gizmo.Hitbox.DepthBias = 0.01f;

			if ( Gizmo.Control.Position( "position", Vector3.Zero, out var delta, _basis ) )
			{
				StartDrag();

				_moveDelta += delta;
				var snapDelta = Gizmo.Snap( _moveDelta, _moveDelta );

				foreach ( var entry in _startVertices )
				{
					var position = entry.Value + snapDelta;
					var transform = entry.Key.Component.Transform.World;
					entry.Key.Component.SetVertexPosition( entry.Key.Index, transform.PointToLocal( position ) );
				}

				EditLog( "Moved", _meshTool.MeshSelection.OfType<BaseMeshTool.MeshElement>()
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
