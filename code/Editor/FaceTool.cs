using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Editor;

/// <summary>
/// Move, rotate and scale mesh faces
/// </summary>
[EditorTool]
[Title( "Face" )]
[Icon( "change_history" )]
[Alias( "Face" )]
[Group( "3" )]
public class FaceTool : EditorTool
{
	private SelectionSystem MeshSelection { get; init; } = new();
	private readonly Dictionary<MeshElement, Vector3> _startVertices = new();
	private Vector3 _moveDelta;

	public enum MeshElementType
	{
		Vertex,
		Edge,
		Face
	}

	private struct MeshElement
	{
		public EditorMeshComponent Component;
		public MeshElementType ElementType;
		public int Index;

		public MeshElement( EditorMeshComponent component, MeshElementType elementType, int index )
		{
			Component = component;
			ElementType = elementType;
			Index = index;
		}

		public readonly override int GetHashCode() => HashCode.Combine( Component, ElementType, Index );

		public override readonly string ToString()
		{
			return $"{Component.GameObject.Name} {ElementType} {Index}";
		}

		public static MeshElement Vertex( EditorMeshComponent component, int index ) => new( component, MeshElementType.Vertex, index );
		public static MeshElement Edge( EditorMeshComponent component, int index ) => new( component, MeshElementType.Edge, index );
		public static MeshElement Face( EditorMeshComponent component, int index ) => new( component, MeshElementType.Face, index );
	}

	public override void OnEnabled()
	{
		base.OnEnabled();

		AllowGameObjectSelection = false;

		Selection.Clear();
	}

	public override void OnDisabled()
	{
		base.OnDisabled();

		foreach ( var go in MeshSelection
			.OfType<MeshElement>()
			.Select( x => x.Component?.GameObject )
			.Distinct() )
		{
			if ( !go.IsValid() )
				continue;

			Selection.Add( go );
		}
	}

	public override IEnumerable<EditorTool> GetSubtools()
	{
		return default;
	}

	public override void OnSelectionChanged()
	{
		base.OnSelectionChanged();

		if ( Selection.OfType<GameObject>().Any() )
		{
			EditorToolManager.CurrentModeName = "object";
		}
	}

	public override void OnUpdate()
	{
		base.OnUpdate();

		var tr = MeshTrace.Run();

		if ( tr.Hit && tr.Component is not null )
		{
			using ( Gizmo.ObjectScope( tr.GameObject, tr.GameObject.Transform.World ) )
			{
				Gizmo.Hitbox.DepthBias = 1;
				Gizmo.Hitbox.TrySetHovered( tr.Distance );

				if ( tr.Component is EditorMeshComponent c )
				{
					var f = c.TriangleToFace( tr.Triangle );

					if ( Gizmo.WasClicked )
					{
						Select( MeshElement.Face( c, f ) );
					}
				}
			}
		}
		else if ( !Gizmo.HasPressed && Gizmo.HasClicked )
		{
			MeshSelection.Clear();
		}

		var selectionToRemove = MeshSelection.OfType<MeshElement>()
			.Where( x => !x.Component.IsValid() )
			.ToArray();

		foreach ( var s in selectionToRemove )
		{
			MeshSelection.Remove( s );
		}

		UpdateMoveGizmo();

		foreach ( var c in Scene.GetAllComponents<EditorMeshComponent>()
			.Where( x => x.Dirty ) )
		{
			c.CreateSceneObject();
			c.Dirty = false;
		}
	}

	bool nudge = false;

	private void UpdateMoveGizmo()
	{
		var points = new List<Vector3>();

		foreach ( var s in MeshSelection.OfType<MeshElement>() )
		{
			if ( s.ElementType != MeshElementType.Face )
				continue;

			Gizmo.Draw.Color = Color.Green;
			var p = s.Component.Transform.World.PointToWorld( s.Component.GetFaceCenter( s.Index ) );
			Gizmo.Draw.SolidSphere( p, 4 );

			points.Add( p );
		}

		if ( !Gizmo.HasPressed )
		{
			_startVertices.Clear();
			_moveDelta = default;
		}

		if ( points.Count == 0 )
			return;

		var bbox = BBox.FromPoints( points );
		var handlePosition = bbox.Center;
		var handleRotation = Rotation.Identity;

		if ( !Gizmo.HasPressed )
		{
			var delta = Vector3.Zero;
			delta += Application.IsKeyDown( KeyCode.Up ) ? Vector3.Up : 0.0f;
			delta += Application.IsKeyDown( KeyCode.Down ) ? Vector3.Down : 0.0f;
			delta += Application.IsKeyDown( KeyCode.Right ) ? Vector3.Forward : 0.0f;
			delta += Application.IsKeyDown( KeyCode.Left ) ? Vector3.Backward : 0.0f;

			if ( delta.Length > 0.0f )
			{
				if ( !nudge )
				{
					foreach ( var s in MeshSelection.OfType<MeshElement>() )
					{
						if ( s.ElementType != MeshElementType.Face )
							continue;

						var up = handlePosition.SnapToGrid( Gizmo.Settings.GridSpacing ) - handlePosition;
						up += Gizmo.Settings.GridSpacing;
						up *= delta;

						s.Component.OffsetFaces( MeshSelection.OfType<MeshElement>().Select( x => x.Index ), up );
					}

					EditLog( "Moved", MeshSelection.OfType<MeshElement>()
						.Select( x => x.Component )
						.Distinct() );

					nudge = true;
				}
			}
			else
			{
				nudge = false;
			}
		}

		using ( Gizmo.Scope( "Tool", new Transform( handlePosition ) ) )
		{
			Gizmo.Hitbox.DepthBias = 0.01f;

			if ( Gizmo.Control.Position( "position", Vector3.Zero, out var delta, handleRotation ) )
			{
				_moveDelta += delta;

				StartDrag();

				var targetPosition = Gizmo.Snap( _moveDelta, _moveDelta );

				foreach ( var entry in _startVertices )
				{
					var transform = entry.Key.Component.Transform.World;
					entry.Key.Component.SetVertexPosition( entry.Key.Index, transform.PointToLocal( entry.Value + targetPosition ) );
				}

				EditLog( "Moved", MeshSelection.OfType<MeshElement>()
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
			foreach ( var s in MeshSelection.OfType<MeshElement>() )
			{
				if ( s.ElementType != MeshElementType.Face )
					continue;

				s.Component.ExtrudeFace( s.Index, s.Component.GetAverageFaceNormal( s.Index ) * 0.01f );
			}
		}

		foreach ( var entry in MeshSelection.OfType<MeshElement>()
			.SelectMany( x => x.Component.GetFaceVertices( x.Index )
			.Select( i => MeshElement.Vertex( x.Component, i ) )
			.Distinct() ) )
		{
			_startVertices[entry] = entry.Component.Transform.World.PointToWorld( entry.Component.GetVertexPosition( entry.Index ) );
		}
	}

	private void Select( MeshElement element )
	{
		if ( Application.KeyboardModifiers.HasFlag( KeyboardModifiers.Ctrl ) )
		{
			if ( MeshSelection.Contains( element ) )
			{
				MeshSelection.Remove( element );
			}
			else
			{
				MeshSelection.Add( element );
			}

			return;
		}
		else if ( Application.KeyboardModifiers.HasFlag( KeyboardModifiers.Shift ) )
		{
			if ( !MeshSelection.Contains( element ) )
			{
				MeshSelection.Add( element );
			}

			return;
		}

		MeshSelection.Set( element );
	}
}
