using Sandbox;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Editor;

public abstract class BaseMeshTool : EditorTool
{
	public SelectionSystem MeshSelection { get; init; } = new();
	public HashSet<MeshElement> VertexSelection { get; init; } = new();

	private bool _meshSelectionDirty;
	private bool _nudge;

	public enum MeshElementType
	{
		Vertex,
		Edge,
		Face
	}

	public struct MeshElement
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

		MeshSelection.OnItemAdded += OnMeshSelectionChanged;
		MeshSelection.OnItemRemoved += OnMeshSelectionChanged;
	}

	public override void OnDisabled()
	{
		base.OnDisabled();

		foreach ( var go in MeshSelection.OfType<MeshElement>()
			.Select( x => x.Component?.GameObject )
			.Distinct() )
		{
			if ( !go.IsValid() )
				continue;

			Selection.Add( go );
		}
	}

	public override void OnUpdate()
	{
		base.OnUpdate();

		var selectionToRemove = MeshSelection.OfType<MeshElement>()
			.Where( x => !x.Component.IsValid() )
			.ToArray();

		foreach ( var s in selectionToRemove )
		{
			MeshSelection.Remove( s );
		}

		UpdateNudge();

		foreach ( var c in Scene.GetAllComponents<EditorMeshComponent>()
			.Where( x => x.Dirty ) )
		{
			c.CreateSceneObject();
			c.Dirty = false;
		}

		if ( _meshSelectionDirty )
		{
			CalculateSelectionVertices();
		}
	}

	private void UpdateNudge()
	{
		if ( Gizmo.HasPressed )
			return;

		var bounds = CalculateSelectionBounds();
		var origin = bounds.Center;

		var delta = Vector3.Zero;
		delta += Application.IsKeyDown( KeyCode.Up ) ? Vector3.Up : 0.0f;
		delta += Application.IsKeyDown( KeyCode.Down ) ? Vector3.Down : 0.0f;
		delta += Application.IsKeyDown( KeyCode.Right ) ? Vector3.Forward : 0.0f;
		delta += Application.IsKeyDown( KeyCode.Left ) ? Vector3.Backward : 0.0f;

		if ( delta.Length > 0.0f )
		{
			if ( !_nudge )
			{
				var offset = origin.SnapToGrid( Gizmo.Settings.GridSpacing ) - origin;
				offset += Gizmo.Settings.GridSpacing;
				offset *= delta;

				if ( Gizmo.IsShiftPressed )
				{
					foreach ( var entry in MeshSelection.OfType<MeshElement>()
						.Where( x => x.ElementType == MeshElementType.Face ) )
					{
						var rotation = entry.Component.Transform.Rotation;
						entry.Component.ExtrudeFace( entry.Index, rotation.Inverse * offset );
					}
				}
				else
				{
					foreach ( var vertexElement in VertexSelection )
					{
						var rotation = vertexElement.Component.Transform.Rotation;
						var localOffset = (vertexElement.Component.GetVertexPosition( vertexElement.Index ) * rotation) + offset;
						vertexElement.Component.SetVertexPosition( vertexElement.Index, rotation.Inverse * localOffset );
					}
				}

				EditLog( "Moved", null );

				CalculateSelectionVertices();

				_nudge = true;
			}
		}
		else
		{
			_nudge = false;
		}
	}

	public override void OnSelectionChanged()
	{
		base.OnSelectionChanged();

		if ( Selection.OfType<GameObject>().Any() )
		{
			EditorToolManager.CurrentModeName = "object";
		}
	}

	public BBox CalculateSelectionBounds()
	{
		return BBox.FromPoints( VertexSelection.Select( x => x.Component.Transform.World.PointToWorld( x.Component.GetVertexPosition( x.Index ) ) ) );
	}

	public void CalculateSelectionVertices()
	{
		VertexSelection.Clear();

		foreach ( var element in MeshSelection.OfType<MeshElement>() )
		{
			if ( element.ElementType == MeshElementType.Face )
			{
				foreach ( var vertexElement in element.Component.GetFaceVertices( element.Index )
					.Select( i => MeshElement.Vertex( element.Component, i ) ) )
				{
					VertexSelection.Add( vertexElement );
				}
			}
			else if ( element.ElementType == MeshElementType.Vertex )
			{
				VertexSelection.Add( element );
			}
		}

		_meshSelectionDirty = false;
	}

	private void OnMeshSelectionChanged( object o )
	{
		_meshSelectionDirty = true;
	}

	protected void Select( MeshElement element )
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
