using System;
using Sandbox;

namespace Editor.MeshEditor;

/// <summary>
/// References a face handle and the mesh component it belongs to.
/// </summary>
public readonly struct MeshFace : IMeshElement
{
	[Hide] public EditorMeshComponent Component { get; private init; }
	[Hide] public int Index { get; private init; }

	[Hide] public readonly bool IsValid => Component.IsValid() && Index >= 0;
	[Hide] public readonly Transform Transform => IsValid ? Component.Transform.World : Transform.Zero;

	[Hide] public Vector3 Center => IsValid ? Component.PolygonMesh.GetFaceCenter( Index ) : Vector3.Zero;

	public MeshFace( EditorMeshComponent component, int index )
	{
		Component = component;
		Index = index;
	}

	public readonly override int GetHashCode() => HashCode.Combine( Component, nameof( MeshFace ), Index );
	public override readonly string ToString() => IsValid ? $"{Component.GameObject.Name} Face {Index}" : "Invalid Face";


	[Range( -180.0f, 180.0f )]
	public float TextureAngle
	{
		get => IsValid ? Component.PolygonMesh.GetTextureAngle( Index ) : default;
		set => Component?.PolygonMesh.SetTextureAngle( Index, value );
	}

	public Vector2 TextureOffset
	{
		get => IsValid ? Component.PolygonMesh.GetTextureOffset( Index ) : default;
		set => Component?.PolygonMesh.SetTextureOffset( Index, value );
	}

	public Vector2 TextureScale
	{
		get => IsValid ? Component.PolygonMesh.GetTextureScale( Index ) : default;
		set => Component?.PolygonMesh.SetTextureScale( Index, value );
	}

	public Material Material
	{
		get => IsValid ? Component.PolygonMesh.GetFaceMaterial( Index ) : default;
		set => Component?.PolygonMesh.SetFaceMaterial( Index, value );
	}

	public MeshVertex GetClosestVertex( Vector2 point, float maxDistance )
	{
		if ( !IsValid )
			return default;

		var transform = Transform;
		var minDistance = maxDistance;
		var closestVertex = -1;

		foreach ( var vertex in Component.PolygonMesh.GetFaceVertices( Index ) )
		{
			var vertexPosition = transform.PointToWorld( Component.PolygonMesh.GetVertexPosition( vertex ) );
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
		if ( !IsValid )
			return default;

		var transform = Transform;
		var minDistance = maxDistance;
		var closestEdge = -1;

		foreach ( var edge in Component.PolygonMesh.GetFaceEdges( Index ) )
		{
			var line = Component.PolygonMesh.GetEdge( edge );
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
