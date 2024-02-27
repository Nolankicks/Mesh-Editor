using Sandbox;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Editor.MeshEditor;

public abstract class BaseMeshTool : EditorTool
{
	public SelectionSystem MeshSelection { get; init; } = new();
	public HashSet<MeshElement> VertexSelection { get; init; } = new();

	public static Vector2 RayScreenPosition => Application.CursorPosition - SceneViewWidget.Current.Overlay.Position;

	private bool _meshSelectionDirty;
	private bool _nudge;

	public enum MeshElementType
	{
		Vertex,
		Edge,
		Face
	}

	public struct MeshElement : IValid
	{
		public EditorMeshComponent Component;
		public MeshElementType ElementType;
		public int Index;

		public readonly Transform Transform => Component.IsValid() ? Component.Transform.World : Transform.Zero;

		public readonly bool IsValid => Component.IsValid() && Index >= 0;

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

		if ( _meshSelectionDirty )
		{
			CalculateSelectionVertices();
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
			foreach ( var faceElement in MeshSelection.OfType<MeshElement>()
				.Where( x => x.ElementType == MeshElementType.Face ) )
			{
				var transform = faceElement.Transform;
				faceElement.Component.ExtrudeFace( faceElement.Index, transform.Rotation.Inverse * delta );
			}
		}
		else
		{
			foreach ( var vertexElement in VertexSelection )
			{
				var transform = vertexElement.Transform;
				var position = vertexElement.Component.GetVertexPosition( vertexElement.Index );
				position = transform.PointToWorld( position ) + delta;
				vertexElement.Component.SetVertexPosition( vertexElement.Index, transform.PointToLocal( position ) );
			}
		}

		EditLog( "Moved", null );

		CalculateSelectionVertices();

		_nudge = true;
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
			else if ( element.ElementType == MeshElementType.Edge )
			{
				foreach ( var vertexElement in element.Component.GetEdgeVertices( element.Index )
					.Select( i => MeshElement.Vertex( element.Component, i ) ) )
				{
					VertexSelection.Add( vertexElement );
				}
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

	protected List<(MeshElement, float)> TraceFaces( int radius, Vector2 point )
	{
		var rays = new List<Ray> { Gizmo.CurrentRay };
		for ( var ring = 1; ring < radius; ring++ )
		{
			rays.Add( Gizmo.Camera.GetRay( point + new Vector2( 0, ring ) ) );
			rays.Add( Gizmo.Camera.GetRay( point + new Vector2( ring, 0 ) ) );
			rays.Add( Gizmo.Camera.GetRay( point + new Vector2( 0, -ring ) ) );
			rays.Add( Gizmo.Camera.GetRay( point + new Vector2( -ring, 0 ) ) );
		}

		var faces = new List<(MeshElement, float)>();
		var faceHash = new HashSet<MeshElement>();
		foreach ( var ray in rays )
		{
			var result = MeshTrace.Ray( ray, 5000 ).Run();
			if ( !result.Hit )
				continue;

			if ( result.Component is not EditorMeshComponent component )
				continue;

			var face = component.TriangleToFace( result.Triangle );
			var faceElement = MeshElement.Face( component, face );
			if ( faceHash.Add( faceElement ) )
				faces.Add( (faceElement, result.Distance) );
		}

		return faces;
	}

	protected MeshElement TraceFace( out float distance )
	{
		distance = default;

		var result = MeshTrace.Run();
		if ( !result.Hit || result.Component is not EditorMeshComponent component )
			return default;

		distance = result.Distance;
		var face = component.TriangleToFace( result.Triangle );
		return MeshElement.Face( component, face );
	}

	protected static MeshElement GetClosestVertexInFace( MeshElement face, Vector2 point, float maxDistance )
	{
		if ( !face.IsValid() || face.ElementType != MeshElementType.Face )
			return default;

		var transform = face.Component.Transform.World;
		var minDistance = maxDistance;
		var closestVertex = -1;

		foreach ( var vertex in face.Component.GetFaceVertices( face.Index ) )
		{
			var vertexPosition = transform.PointToWorld( face.Component.GetVertexPosition( vertex ) );
			var vertexCoord = Gizmo.Camera.ToScreen( vertexPosition );
			var distance = vertexCoord.Distance( point );
			if ( distance < minDistance )
			{
				minDistance = distance;
				closestVertex = vertex;
			}
		}

		return MeshElement.Vertex( face.Component, closestVertex );
	}

	protected static MeshElement GetClosestEdgeInFace( MeshElement face, Vector3 hitPosition, Vector2 point, float maxDistance )
	{
		if ( !face.IsValid() || face.ElementType != MeshElementType.Face )
			return default;

		var transform = face.Transform;
		var minDistance = maxDistance;
		var closestEdge = -1;

		foreach ( var edge in face.Component.GetFaceEdges( face.Index ) )
		{
			var line = face.Component.GetEdge( edge );
			line = new Line( transform.PointToWorld( line.Start ), transform.PointToWorld( line.End ) );
			var closestPoint = line.ClosestPoint( hitPosition );
			var pointCoord = Gizmo.Camera.ToScreen( closestPoint );
			var distance = pointCoord.Distance( point );
			if ( distance < minDistance )
			{
				minDistance = distance;
				closestEdge = edge;
			}
		}

		return MeshElement.Edge( face.Component, closestEdge );
	}

	protected MeshElement GetClosestVertex( int radius )
	{
		var point = RayScreenPosition;
		var bestFace = TraceFace( out var bestHitDistance );
		var bestVertex = GetClosestVertexInFace( bestFace, point, radius );

		if ( bestFace.IsValid() && bestVertex.IsValid() )
			return bestVertex;

		var results = TraceFaces( radius, point );
		foreach ( var result in results )
		{
			var face = result.Item1;
			var hitDistance = result.Item2;
			var vertex = GetClosestVertexInFace( face, point, radius );
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

	protected MeshElement GetClosestEdge( int radius )
	{
		var point = RayScreenPosition;
		var bestFace = TraceFace( out var bestHitDistance );
		var hitPosition = Gizmo.CurrentRay.Project( bestHitDistance );
		var bestEdge = GetClosestEdgeInFace( bestFace, hitPosition, point, radius );

		if ( bestFace.IsValid() && bestEdge.IsValid() )
			return bestEdge;

		var results = TraceFaces( radius, point );
		foreach ( var result in results )
		{
			var face = result.Item1;
			var hitDistance = result.Item2;
			hitPosition = Gizmo.CurrentRay.Project( hitDistance );

			var edge = GetClosestEdgeInFace( face, hitPosition, point, radius );
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
}
