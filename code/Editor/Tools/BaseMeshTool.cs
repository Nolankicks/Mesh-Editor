﻿using Sandbox;
using System.Linq;
using System.Collections.Generic;

namespace Editor.MeshEditor;

/// <summary>
/// Base class for vertex, edge and face tools.
/// </summary>
public abstract class BaseMeshTool : EditorTool
{
	public SelectionSystem MeshSelection { get; init; } = new();
	public HashSet<MeshVertex> VertexSelection { get; init; } = new();

	public static Vector2 RayScreenPosition => (Application.CursorPosition - SceneViewWidget.Current.Overlay.Position) * SceneViewWidget.Current.Overlay.DpiScale;

	public static bool IsMultiSelecting => Application.KeyboardModifiers.HasFlag( KeyboardModifiers.Ctrl ) ||
				Application.KeyboardModifiers.HasFlag( KeyboardModifiers.Shift );

	private bool _meshSelectionDirty;
	private bool _nudge;

	public override IEnumerable<EditorTool> GetSubtools()
	{
		yield return new PositionTool( this );
		yield return new RotateTool( this );
		yield return new ScaleTool( this );
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

		foreach ( var go in GetSelectedObjects() )
		{
			if ( !go.IsValid() )
				continue;

			Selection.Add( go );
		}
	}

	public override void OnUpdate()
	{
		base.OnUpdate();

		var removeList = GetInvalidSelection().ToList();
		foreach ( var s in removeList )
			MeshSelection.Remove( s );

		UpdateNudge();

		if ( _meshSelectionDirty )
		{
			CalculateSelectionVertices();
			OnMeshSelectionChanged();

			_meshSelectionDirty = false;
		}
	}

	private void UpdateNudge()
	{
		if ( Gizmo.HasPressed || Application.FocusWidget is null )
			return;

		var keyUp = Application.IsKeyDown( KeyCode.Up );
		var keyDown = Application.IsKeyDown( KeyCode.Down );
		var keyLeft = Application.IsKeyDown( KeyCode.Left );
		var keyRight = Application.IsKeyDown( KeyCode.Right );

		if ( !keyUp && !keyDown && !keyLeft && !keyRight )
		{
			_nudge = false;

			return;
		}

		if ( _nudge )
			return;

		var basis = CalculateSelectionBasis();
		var direction = new Vector2( keyLeft ? 1 : keyRight ? -1 : 0, keyUp ? 1 : keyDown ? -1 : 0 );
		var delta = Gizmo.Nudge( basis, direction );

		if ( Gizmo.IsShiftPressed )
		{
			ExtrudeSelection( delta );
		}
		else
		{
			foreach ( var vertex in VertexSelection )
			{
				var transform = vertex.Transform;
				var position = vertex.Component.PolygonMesh.GetVertexPosition( vertex.Index );
				position = transform.PointToWorld( position ) + delta;
				vertex.Component.PolygonMesh.SetVertexPosition( vertex.Index, transform.PointToLocal( position ) );
			}
		}

		EditLog( "Moved", null );

		_nudge = true;
	}

	public void ExtrudeSelection( Vector3 delta = default )
	{
		foreach ( var group in MeshSelection.OfType<MeshFace>()
			.GroupBy( x => x.Component ) )
		{
			var offset = group.Key.Transform.Rotation.Inverse * delta;
			group.Key.PolygonMesh.ExtrudeFaces( group.Select( x => x.Index ), offset );
		}

		var edges = MeshSelection.OfType<MeshEdge>().ToArray();
		if ( edges.Length > 0 )
			MeshSelection.Clear();

		foreach ( var edge in edges )
		{
			var offset = edge.Transform.Rotation.Inverse * delta;
			var newEdge = new MeshEdge( edge.Component, edge.Component.PolygonMesh.ExtrudeEdge( edge.Index, offset ) );
			if ( newEdge.IsValid() )
				MeshSelection.Add( newEdge );
		}

		CalculateSelectionVertices();
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
			.Select( x => x.Transform.PointToWorld( x.Component.PolygonMesh.GetVertexPosition( x.Index ) ) ) );
	}

	public virtual Rotation CalculateSelectionBasis()
	{
		return Rotation.Identity;
	}

	public Vector3 CalculateSelectionOrigin()
	{
		if ( Gizmo.Settings.GlobalSpace )
		{
			var bounds = CalculateSelectionBounds();
			return bounds.Center;
		}

		var faceElement = MeshSelection.OfType<MeshFace>().FirstOrDefault();
		if ( !faceElement.Component.IsValid() )
			return Vector3.Zero;

		var transform = faceElement.Transform;
		return transform.PointToWorld( faceElement.Component.PolygonMesh.GetFaceCenter( faceElement.Index ) );
	}

	public void CalculateSelectionVertices()
	{
		VertexSelection.Clear();

		foreach ( var face in MeshSelection.OfType<MeshFace>() )
		{
			foreach ( var vertex in face.Component.PolygonMesh.GetFaceVertices( face.Index )
				.Select( i => new MeshVertex( face.Component, i ) ) )
			{
				VertexSelection.Add( vertex );
			}
		}

		foreach ( var vertex in MeshSelection.OfType<MeshVertex>() )
		{
			VertexSelection.Add( vertex );
		}

		foreach ( var edge in MeshSelection.OfType<MeshEdge>() )
		{
			foreach ( var vertex in edge.Component.PolygonMesh.GetEdgeVertices( edge.Index )
				.Select( i => new MeshVertex( edge.Component, i ) ) )
			{
				VertexSelection.Add( vertex );
			}
		}
	}

	private HashSet<GameObject> GetSelectedObjects()
	{
		var objects = new HashSet<GameObject>();
		foreach ( var face in MeshSelection.OfType<IMeshElement>()
			.Where( x => x.IsValid() ) )
		{
			objects.Add( face.GameObject );
		}

		return objects;
	}

	private IEnumerable<IMeshElement> GetInvalidSelection()
	{
		foreach ( var selection in MeshSelection.OfType<IMeshElement>()
			.Where( x => !x.IsValid() ) )
		{
			yield return selection;
		}
	}

	private void OnMeshSelectionChanged( object o )
	{
		_meshSelectionDirty = true;
	}

	protected virtual void OnMeshSelectionChanged()
	{

	}

	protected void Select( object element )
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

	protected MeshVertex GetClosestVertex( int radius )
	{
		var point = RayScreenPosition;
		var bestFace = TraceFace( out var bestHitDistance );
		var bestVertex = bestFace.GetClosestVertex( point, radius );

		if ( bestFace.IsValid() && bestVertex.IsValid() )
			return bestVertex;

		var results = TraceFaces( radius, point );
		foreach ( var result in results )
		{
			var face = result.MeshFace;
			var hitDistance = result.Distance;
			var vertex = face.GetClosestVertex( point, radius );
			if ( !vertex.IsValid() )
				continue;

			if ( hitDistance < bestHitDistance || !bestFace.IsValid() )
			{
				bestHitDistance = hitDistance;
				bestVertex = vertex;
				bestFace = face;
			}
		}

		return bestVertex;
	}

	protected MeshEdge GetClosestEdge( int radius )
	{
		var point = RayScreenPosition;
		var bestFace = TraceFace( out var bestHitDistance );
		var hitPosition = Gizmo.CurrentRay.Project( bestHitDistance );
		var bestEdge = bestFace.GetClosestEdge( hitPosition, point, radius );

		if ( bestFace.IsValid() && bestEdge.IsValid() )
			return bestEdge;

		var results = TraceFaces( radius, point );
		foreach ( var result in results )
		{
			var face = result.MeshFace;
			var hitDistance = result.Distance;
			hitPosition = Gizmo.CurrentRay.Project( hitDistance );

			var edge = face.GetClosestEdge( hitPosition, point, radius );
			if ( !edge.IsValid() )
				continue;

			if ( hitDistance < bestHitDistance || !bestFace.IsValid() )
			{
				bestHitDistance = hitDistance;
				bestEdge = edge;
				bestFace = face;
			}
		}

		return bestEdge;
	}

	private MeshFace TraceFace( out float distance )
	{
		distance = default;

		var result = MeshTrace.Run();
		if ( !result.Hit || result.Component is not EditorMeshComponent component )
			return default;

		distance = result.Distance;
		var face = component.PolygonMesh.TriangleToFace( result.Triangle );
		return new MeshFace( component, face );
	}

	public MeshFace TraceFace()
	{
		var result = MeshTrace.Run();
		if ( !result.Hit || result.Component is not EditorMeshComponent component )
			return default;

		var face = component.PolygonMesh.TriangleToFace( result.Triangle );
		return new MeshFace( component, face );
	}

	private struct MeshFaceTraceResult
	{
		public MeshFace MeshFace;
		public float Distance;
	}

	private List<MeshFaceTraceResult> TraceFaces( int radius, Vector2 point )
	{
		var rays = new List<Ray> { Gizmo.CurrentRay };
		for ( var ring = 1; ring < radius; ring++ )
		{
			rays.Add( Gizmo.Camera.GetRay( point + new Vector2( 0, ring ) ) );
			rays.Add( Gizmo.Camera.GetRay( point + new Vector2( ring, 0 ) ) );
			rays.Add( Gizmo.Camera.GetRay( point + new Vector2( 0, -ring ) ) );
			rays.Add( Gizmo.Camera.GetRay( point + new Vector2( -ring, 0 ) ) );
		}

		var faces = new List<MeshFaceTraceResult>();
		var faceHash = new HashSet<MeshFace>();
		foreach ( var ray in rays )
		{
			var result = MeshTrace.Ray( ray, 5000 ).Run();
			if ( !result.Hit )
				continue;

			if ( result.Component is not EditorMeshComponent component )
				continue;

			var face = component.PolygonMesh.TriangleToFace( result.Triangle );
			var faceElement = new MeshFace( component, face );
			if ( faceHash.Add( faceElement ) )
				faces.Add( new MeshFaceTraceResult { MeshFace = faceElement, Distance = result.Distance } );
		}

		return faces;
	}
}
