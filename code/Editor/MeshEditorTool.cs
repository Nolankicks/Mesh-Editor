using Sandbox;
using System;
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

	public override void OnDisabled()
	{

	}

	public override void OnUpdate()
	{
		var selectionToRemove = MeshSelection.OfType<MeshElement>().Where( x => !x.Component.IsValid() ).ToArray();
		foreach ( var s in selectionToRemove )
		{
			MeshSelection.Remove( s );
		}

		foreach ( var s in MeshSelection.OfType<MeshElement>() )
		{
			if ( s.ElementType != MeshElementType.Face )
				continue;

			Gizmo.Draw.Color = Color.Green;
			var p = s.Component.Transform.World.PointToWorld( s.Component.GetFaceCenter( s.Index ) );
			Gizmo.Draw.SolidSphere( p, 4 );
			Gizmo.Draw.Arrow( p, p + s.Component.GetAverageFaceNormal( s.Index ) * 50 );
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
						}
					}

					Gizmo.Draw.Color = Gizmo.Colors.Active.WithAlpha( MathF.Sin( RealTime.Now * 20.0f ).Remap( -1, 1, 0.3f, 0.8f ) );
					Gizmo.Draw.LineBBox( c.Model.Bounds );
				}
			}
		}
	}

	void Select( MeshElement element )
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
