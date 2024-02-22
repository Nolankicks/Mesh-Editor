using Sandbox;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Editor;

public abstract class BaseMeshTool : EditorTool
{
	protected SelectionSystem MeshSelection { get; init; } = new();
	protected HashSet<MeshElement> SelectedVertices { get; init; } = new();
	private bool _meshSelectionDirty;

	protected enum MeshElementType
	{
		Vertex,
		Edge,
		Face
	}

	protected struct MeshElement
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

		foreach ( var c in Scene.GetAllComponents<EditorMeshComponent>()
			.Where( x => x.Dirty ) )
		{
			c.CreateSceneObject();
			c.Dirty = false;
		}

		if ( _meshSelectionDirty )
		{
			UpdateSelectedVertices();
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

	protected void UpdateSelectedVertices()
	{
		SelectedVertices.Clear();

		foreach ( var element in MeshSelection.OfType<MeshElement>() )
		{
			if ( element.ElementType == MeshElementType.Face )
			{
				foreach ( var vertexElement in element.Component.GetFaceVertices( element.Index )
					.Select( i => MeshElement.Vertex( element.Component, i ) ) )
				{
					SelectedVertices.Add( vertexElement );
				}
			}
			else if ( element.ElementType == MeshElementType.Vertex )
			{
				SelectedVertices.Add( element );
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
