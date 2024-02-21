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
[Shortcut( "editortool.mesh", "b" )]
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

	Dictionary<MeshElement, Vector3> startFaceOrigins = new();
	Vector3 moveDelta;

	bool dragging = false;
	Vector3 dragStartPos;

	private void StartDrag()
	{
		if ( startFaceOrigins.Any() )
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
			startFaceOrigins[entry] = entry.Component.Transform.World.PointToWorld( entry.Component.GetVertexPosition( entry.Index ) );
		}
	}

	public override void OnUpdate()
	{
		//var selectionToRemove = MeshSelection.OfType<MeshElement>()
		//	.Where( x => !x.Component.IsValid() )
		//	.ToArray();

		//foreach ( var s in selectionToRemove )
		//{
		//	MeshSelection.Remove( s );
		//}

		//var points = new List<Vector3>();

		//foreach ( var s in MeshSelection.OfType<MeshElement>() )
		//{
		//	if ( s.ElementType != MeshElementType.Face )
		//		continue;

		//	Gizmo.Draw.Color = Color.Green;
		//	var p = s.Component.Transform.World.PointToWorld( s.Component.GetFaceCenter( s.Index ) );
		//	Gizmo.Draw.SolidSphere( p, 4 );

		//	points.Add( p );
		//}

		//if ( !Gizmo.HasPressed )
		//{
		//	startFaceOrigins.Clear();
		//	moveDelta = default;
		//}

		//var bbox = BBox.FromPoints( points );
		//var handlePosition = bbox.Center;
		//var handleRotation = Rotation.Identity;

		//using ( Gizmo.Scope( "Tool", new Transform( handlePosition ) ) )
		//{
		//	Gizmo.Hitbox.DepthBias = 0.01f;

		//	if ( Gizmo.Control.Position( "position", Vector3.Zero, out var delta, handleRotation ) )
		//	{
		//		moveDelta += delta;

		//		StartDrag();

		//		var targetPosition = Gizmo.Snap( moveDelta, moveDelta );

		//		foreach ( var entry in startFaceOrigins )
		//		{
		//			entry.Key.Component.SetVertexPosition( entry.Key.Index, entry.Key.Component.Transform.World.PointToLocal( entry.Value + targetPosition ) );
		//		}

		//		EditLog( "Moved", MeshSelection.OfType<MeshElement>()
		//			.Select( x => x.Component )
		//			.Distinct() );
		//	}
		//}

		//foreach ( var c in Scene.GetAllComponents<EditorMeshComponent>()
		//	.Where( x => x.Dirty ) )
		//{
		//	c.CreateSceneObject();
		//	c.Dirty = false;
		//}

		if ( Gizmo.HasHovered )
			return;

		var tr = MeshTrace.Run();
		if ( !tr.Hit || dragging )
		{
			var plane = dragging ? new Plane( dragStartPos, Vector3.Up ) : new Plane( Vector3.Up, 0.0f );
			if ( plane.TryTrace( new Ray( tr.StartPosition, tr.Direction ), out tr.EndPosition, true ) )
			{
				tr.Hit = true;
				tr.Normal = plane.Normal;
			}
		}

		if ( tr.Hit )
		{
			var r = Rotation.LookAt( tr.Normal );
			var localPosition = tr.EndPosition * r.Inverse;

			if ( Gizmo.Settings.SnapToGrid )
				localPosition = localPosition.SnapToGrid( Gizmo.Settings.GridSpacing, false, true, true );

			tr.EndPosition = localPosition * r;

			if ( !dragging )
			{
				using ( Gizmo.Scope( "Aim Handle", new Transform( tr.EndPosition, Rotation.LookAt( tr.Normal ) ) ) )
				{
					Gizmo.Draw.Color = Color.White;
					Gizmo.Draw.LineCircle( 0, 2 );
					Gizmo.Draw.Color = Color.White.WithAlpha( 0.5f );
					Gizmo.Draw.LineCircle( 0, 3 );
					Gizmo.Draw.Color = Color.White.WithAlpha( 0.3f );
					Gizmo.Draw.LineCircle( 0, 6 );
					Gizmo.Draw.Color = Color.White.WithAlpha( 0.1f );
					Gizmo.Draw.LineCircle( 0, 12 );
				}
			}

			if ( Gizmo.WasLeftMousePressed )
			{
				dragging = true;
				dragStartPos = tr.EndPosition;

				Selection.Clear();
			}
			else if ( Gizmo.WasLeftMouseReleased && dragging )
			{
				var spacing = Gizmo.Settings.SnapToGrid ? Gizmo.Settings.GridSpacing : 1.0f;
				var box = new BBox( dragStartPos, tr.EndPosition );

				if ( box.Size.x >= spacing || box.Size.y >= spacing )
				{
					var go = new GameObject( true, "Box" );
					var mc = go.Components.Create<MeshComponent>();
					mc.Type = MeshComponent.PrimitiveType.Box;

					if ( Gizmo.Settings.SnapToGrid )
					{
						if ( box.Size.x < spacing ) box.Maxs.x += spacing;
						if ( box.Size.y < spacing ) box.Maxs.y += spacing;
					}

					mc.BoxSize = box.Size.WithZ( 128 );
					mc.Transform.Position = box.Center.WithZ( box.Center.z + 64 );
					mc.TextureOrigin = mc.Transform.Position;

					Selection.Set( go );
				}

				dragging = false;
				dragStartPos = default;
			}

			using ( Gizmo.Scope( "box", 0, r ) )
			{
				if ( dragging )
				{
					Gizmo.Draw.IgnoreDepth = true;
					Gizmo.Draw.LineThickness = 2;
					Gizmo.Draw.Color = Gizmo.Colors.Active;
					Gizmo.Draw.LineBBox( new BBox( dragStartPos * r.Inverse, localPosition ) );
				}
			}
		}

		//if ( tr.Hit && tr.Component is not null )
		//{
		//	using ( Gizmo.ObjectScope( tr.GameObject, tr.GameObject.Transform.World ) )
		//	{
		//		Gizmo.Hitbox.DepthBias = 1;
		//		Gizmo.Hitbox.TrySetHovered( tr.Distance );

		//		if ( tr.Component is EditorMeshComponent c && c.Model is not null )
		//		{
		//			var f = c.TriangleToFace( tr.Triangle );

		//			if ( Gizmo.WasClicked )
		//			{
		//				Select( MeshElement.Face( c, f ) );
		//			}
		//		}
		//	}
		//}
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
