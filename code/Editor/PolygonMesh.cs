using Sandbox;
using System.Collections.Generic;
using System.Linq;

namespace Editor.MeshEditor;

public sealed class PolygonFace
{
	/// <summary>
	/// Indices of this face for <see cref="PolygonMesh.Vertices"/>.
	/// </summary>
	public List<int> Indices { get; set; }

	/// <summary>
	/// Render material for this face.
	/// </summary>
	public Material Material { get; set; }

	public PolygonFace( Material material = null )
	{
		Indices = new List<int>();
		Material = material;
	}

	public PolygonFace( IEnumerable<int> indices, Material material = null )
	{
		Indices = new List<int>( indices );
		Material = material;
	}
}

/// <summary>
/// Describes a set of n-gon polygons used to construct a Hammer mesh.
/// Any overlapping vertices will be merged if <see cref="MergeVertices"/>.
/// </summary>
public sealed partial class PolygonMesh
{
	/// <summary>
	/// All vertices of this mesh.
	/// </summary>
	public List<Vector3> Vertices { get; set; } = new();

	/// <summary>
	/// All faces of this mesh.
	/// </summary>
	public List<PolygonFace> Faces { get; set; } = new();

	/// <summary>
	/// Whether to merge overlapping vertices.
	/// </summary>
	public bool MergeVertices { get; set; } = true;

	/// <summary>
	/// Vertex merge tolerance. See <see cref="MergeVertices"/>.
	/// </summary>
	public float VertexMergeTolerance { get; set; } = 0.001f;


	/// <summary>
	/// Adds a new vertex to the end of the Vertex list.
	/// </summary>
	/// <param name="vertex">Vertex to add.</param>
	/// <returns>The index of the newly added vertex.</returns>
	public int AddVertex( Vector3 vertex )
	{
		if ( MergeVertices )
		{
			var i = Vertices.FindIndex( x => x.Distance( vertex ) <= VertexMergeTolerance );
			if ( i >= 0 )
				return i;
		}

		Vertices.Add( vertex );
		return Vertices.Count - 1;
	}

	/// <summary>
	/// Adds a new face to the end of the Faces list.
	/// </summary>
	/// <param name="indices">The vertex indices which define the face, ordered anticlockwise.</param>
	/// <returns>The newly added face.</returns>
	public PolygonFace AddFace( params int[] indices )
	{
		// Don't allow degenerate faces
		if ( indices.Length < 3 ) return default;

		Faces.Add( new PolygonFace( indices ) );
		return Faces[^1];
	}

	/// <summary>
	/// Adds a new face to the end of the Faces list and it's vertices to the end of the Vertices list.
	/// </summary>
	/// <param name="vertices">The vertices which define the face, ordered anticlockwise.</param>
	/// <returns>The newly added face.</returns>
	public PolygonFace AddFace( params Vector3[] vertices )
	{
		// Don't allow degenerate faces
		if ( vertices.Length < 3 ) return default;

		return AddFace( vertices.Select( AddVertex ).ToArray() );
	}
}
