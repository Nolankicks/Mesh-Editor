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

public abstract class PrimitiveBuilder
{
	/// <summary>
	/// Create the primitive in the mesh.
	/// </summary>
	public virtual void Build( PolygonMesh mesh ) { }

	public virtual void SetFromBox( BBox box ) { }

	/// <summary>
	/// If this primitive is 2D the bounds box will be limited to have no depth.
	/// </summary>
	[Hide]
	public virtual bool Is2D { get => false; }
}

[Title( "Stairs (Experimental)" ), Icon( "stairs" )]
internal class StairsPrimitive : PrimitiveBuilder
{
	[Title( "Number of steps" )]
	public int NumberOfSteps { get; set; } = 16;

	private Vector3 Center;
	private Vector3 Size { get; set; }
	private float StepWidth;
	private float StepDepth;
	private float StepHeight;

	public override void SetFromBox( BBox box )
	{
		Size = box.Size;
		Center = box.Mins;
		StepHeight = Size.z / NumberOfSteps;
		StepDepth = Size.x / NumberOfSteps;
		StepWidth = Size.y;
	}

	public override void Build( PolygonMesh mesh )
	{
		var vertices = new Vector3[(NumberOfSteps + 1) * 2];

		for ( var stepIndex = 0; stepIndex <= NumberOfSteps; stepIndex++ )
		{
			float depth = StepDepth * stepIndex;
			var innerPoint = Vector3.Forward * depth;
			var outerPoint = Vector3.Left * StepWidth + Vector3.Forward * depth;

			vertices[stepIndex * 2] = Center + innerPoint;
			vertices[stepIndex * 2 + 1] = Center + outerPoint;
		}

		for ( var stepIndex = 0; stepIndex < NumberOfSteps; stepIndex++ )
		{
			var lastHeightOffset = Center.z + StepHeight * stepIndex;
			var stepHeightOffset = Center.z + StepHeight * (stepIndex + 1);

			var innerPoint = vertices[stepIndex * 2].WithZ( stepHeightOffset );
			var outerPoint = vertices[stepIndex * 2 + 1].WithZ( stepHeightOffset );

			var innerPoint2 = vertices[(stepIndex + 1) * 2].WithZ( stepHeightOffset );
			var outerPoint2 = vertices[(stepIndex + 1) * 2 + 1].WithZ( stepHeightOffset );

			var lastInnerPoint = vertices[(stepIndex) * 2].WithZ( lastHeightOffset );
			var lastOuterPoint = vertices[(stepIndex) * 2 + 1].WithZ( lastHeightOffset );

			mesh.AddFace(
				innerPoint2,
				outerPoint2,
				outerPoint,
				innerPoint
			);

			mesh.AddFace(
				innerPoint,
				outerPoint,
				lastOuterPoint,
				lastInnerPoint
			);

			mesh.AddFace(
				vertices[(stepIndex + 1) * 2],
				innerPoint2,
				innerPoint,
				vertices[stepIndex * 2]
			);

			mesh.AddFace(
				outerPoint,
				outerPoint2,
				vertices[(stepIndex + 1) * 2 + 1],
				vertices[stepIndex * 2 + 1]
			);
		}

		var backInnerPoint = vertices[^2].WithZ( Center.z + Size.z );
		var backOuterPoint = vertices[^1].WithZ( Center.z + Size.z );

		mesh.AddFace(
			vertices[^2],
			vertices[^1],
			backOuterPoint,
			backInnerPoint
		);
	}
}
