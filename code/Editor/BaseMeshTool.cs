﻿using Sandbox;
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

		public readonly Transform Transform => Component.IsValid() ? Component.Transform.World : Transform.Zero;

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

		foreach ( var component in Scene.GetAllComponents<EditorMeshComponent>()
			.Where( x => x.Dirty ) )
		{
			component.CreateSceneObject();
			component.Dirty = false;
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

		var delta = Vector3.Zero;
		delta += Application.IsKeyDown( KeyCode.Up ) ? Vector3.Up : 0.0f;
		delta += Application.IsKeyDown( KeyCode.Down ) ? Vector3.Down : 0.0f;
		delta += Application.IsKeyDown( KeyCode.Right ) ? Vector3.Forward : 0.0f;
		delta += Application.IsKeyDown( KeyCode.Left ) ? Vector3.Backward : 0.0f;

		if ( delta.Length > 0.0f )
		{
			if ( !_nudge )
			{
				var origin = CalculateSelectionOrigin();
				var basis = CalculateSelectionBasis();

				var targetPosition = origin;
				delta *= Gizmo.Settings.GridSpacing;

				if ( Gizmo.Settings.GlobalSpace )
				{
					targetPosition += delta;
					targetPosition = Gizmo.Snap( targetPosition, delta );
				}
				else
				{
					delta = new Vector3( delta.z, delta.x, delta.y );
					targetPosition += delta * basis;
					targetPosition *= basis.Inverse;
					targetPosition = basis * Gizmo.Snap( targetPosition, delta );
				}

				var moveDelta = targetPosition - origin;

				if ( Gizmo.IsShiftPressed )
				{
					foreach ( var faceElement in MeshSelection.OfType<MeshElement>()
						.Where( x => x.ElementType == MeshElementType.Face ) )
					{
						var transform = faceElement.Transform;
						faceElement.Component.ExtrudeFace( faceElement.Index, transform.PointToLocal( moveDelta ) );
					}
				}
				else
				{
					foreach ( var vertexElement in VertexSelection )
					{
						var transform = vertexElement.Transform;
						var position = vertexElement.Component.GetVertexPosition( vertexElement.Index );
						position = transform.PointToWorld( position ) + moveDelta;
						vertexElement.Component.SetVertexPosition( vertexElement.Index, transform.PointToLocal( position ) );
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
		return BBox.FromPoints( VertexSelection
			.Select( x => x.Transform.PointToWorld( x.Component.GetVertexPosition( x.Index ) ) ) );
	}

	public Rotation CalculateSelectionBasis()
	{
		if ( Gizmo.Settings.GlobalSpace )
		{
			return Rotation.Identity;
		}

		var faceElement = MeshSelection.OfType<MeshElement>()
			.FirstOrDefault( x => x.ElementType == MeshElementType.Face );

		if ( !faceElement.Component.IsValid() )
			return Rotation.Identity;

		var normal = faceElement.Component.GetAverageFaceNormal( faceElement.Index );
		var vAxis = EditorMeshComponent.ComputeTextureVAxis( normal );
		var transform = faceElement.Transform;
		var basis = Rotation.LookAt( normal, vAxis * -1.0f );
		return transform.RotationToWorld( basis );
	}

	public Vector3 CalculateSelectionOrigin()
	{
		if ( Gizmo.Settings.GlobalSpace )
		{
			var bounds = CalculateSelectionBounds();
			return bounds.Center;
		}

		var faceElement = MeshSelection.OfType<MeshElement>()
			.FirstOrDefault( x => x.ElementType == MeshElementType.Face );

		if ( !faceElement.Component.IsValid() )
			return Vector3.Zero;

		var transform = faceElement.Transform;
		return transform.PointToWorld( faceElement.Component.GetFaceCenter( faceElement.Index ) );
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
