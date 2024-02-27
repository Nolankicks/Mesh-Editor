using System;
using Sandbox;

namespace Editor.MeshEditor;

/// <summary>
/// References a face handle and the mesh component it belongs to.
/// </summary>
public readonly struct MeshFace : IValid
{
	public EditorMeshComponent Component { get; private init; }
	public int Index { get; private init; }

	public readonly bool IsValid => Component.IsValid() && Index >= 0;
	public readonly Transform Transform => Component.IsValid() ? Component.Transform.World : Transform.Zero;

	public MeshFace( EditorMeshComponent component, int index )
	{
		Component = component;
		Index = index;
	}

	public readonly override int GetHashCode() => HashCode.Combine( Component, nameof( MeshFace ), Index );
	public override readonly string ToString() => $"{Component.GameObject.Name} Face {Index}";

	public MeshVertex GetClosestVertex( Vector2 point, float maxDistance )
	{
		if ( !this.IsValid() )
			return default;

		var transform = Component.Transform.World;
		var minDistance = maxDistance;
		var closestVertex = -1;

		foreach ( var vertex in Component.GetFaceVertices( Index ) )
		{
			var vertexPosition = transform.PointToWorld( Component.GetVertexPosition( vertex ) );
			var vertexCoord = Gizmo.Camera.ToScreen( vertexPosition );
			var distance = vertexCoord.Distance( point );
			if ( distance < minDistance )
			{
				minDistance = distance;
				closestVertex = vertex;
			}
		}

		return new MeshVertex( Component, closestVertex );
	}

	public MeshEdge GetClosestEdge( Vector3 position, Vector2 point, float maxDistance )
	{
		if ( !this.IsValid() )
			return default;

		var transform = Transform;
		var minDistance = maxDistance;
		var closestEdge = -1;

		foreach ( var edge in Component.GetFaceEdges( Index ) )
		{
			var line = Component.GetEdge( edge );
			line = new Line( transform.PointToWorld( line.Start ), transform.PointToWorld( line.End ) );
			var closestPoint = line.ClosestPoint( position );
			var pointCoord = Gizmo.Camera.ToScreen( closestPoint );
			var distance = pointCoord.Distance( point );
			if ( distance < minDistance )
			{
				minDistance = distance;
				closestEdge = edge;
			}
		}

		return new MeshEdge( Component, closestEdge );
	}
}
