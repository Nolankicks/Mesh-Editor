using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Editor;

/// <summary>
/// Half Edge Mesh Editor
/// </summary>
[EditorTool]
[Title( "Mesh Editor" )]
[Icon( "hardware" )]
[Shortcut( "editortool.mesh", "u" )]
public class MeshEditorTool : EditorTool
{
	private SelectionSystem MeshSelection { get; init; } = new();

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
		AllowGameObjectSelection = false;
	}

	Dictionary<MeshElement, Vector3> startPoints = new();
	Vector3 moveDelta;

	private void StartDrag()
	{
		if ( startPoints.Any() ) return;

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
			startPoints[entry] = entry.Component.Transform.World.PointToWorld( entry.Component.GetVertexPosition( entry.Index ) );
		}
	}

	public override void OnUpdate()
	{
		var selectionToRemove = MeshSelection.OfType<MeshElement>()
			.Where( x => !x.Component.IsValid() )
			.ToArray();

		foreach ( var s in selectionToRemove )
		{
			MeshSelection.Remove( s );
		}

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
			startPoints.Clear();
			moveDelta = default;
		}

		var bbox = BBox.FromPoints( points );
		var handlePosition = bbox.Center;
		var handleRotation = Rotation.Identity;

		using ( Gizmo.Scope( "Tool", new Transform( handlePosition ) ) )
		{
			Gizmo.Hitbox.DepthBias = 0.01f;

			if ( Gizmo.Control.Position( "position", Vector3.Zero, out var delta, handleRotation ) )
			{
				moveDelta += delta;

				StartDrag();

				foreach ( var entry in startPoints )
				{
					var targetPosition = Gizmo.Snap( entry.Value + moveDelta, moveDelta );
					entry.Key.Component.SetVertexPosition( entry.Key.Index, entry.Key.Component.Transform.World.PointToLocal( targetPosition ) );
				}

				EditLog( "Moved", MeshSelection.OfType<MeshElement>()
					.Select( x => x.Component )
					.Distinct() );
			}
		}



		foreach ( var c in Scene.GetAllComponents<EditorMeshComponent>()
			.Where( x => x.Dirty ) )
		{
			c.CreateSceneObject();
			c.Dirty = false;
		}




		var tr = MeshTrace.Run();
		if ( tr.Hit && tr.Component is not null )
		{
			using ( Gizmo.ObjectScope( tr.GameObject, tr.GameObject.Transform.World ) )
			{
				Gizmo.Hitbox.DepthBias = 1;
				Gizmo.Hitbox.TrySetHovered( tr.Distance );

				if ( tr.Component is EditorMeshComponent c && c.Model is not null )
				{
					var f = c.TriangleToFace( tr.Triangle );

					if ( Gizmo.WasClicked )
					{
						Select( MeshElement.Face( c, f ) );

						if ( Application.KeyboardModifiers.HasFlag( KeyboardModifiers.Alt ) )
						{
							c.ExtrudeFace( f, c.GetAverageFaceNormal( f ) * 50 );
							c.EditLog( "Extrude Face", c );
						}
					}

					Gizmo.Draw.Color = Gizmo.Colors.Active.WithAlpha( MathF.Sin( RealTime.Now * 20.0f ).Remap( -1, 1, 0.3f, 0.8f ) );
					Gizmo.Draw.LineBBox( c.Model.Bounds );
				}
			}
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
