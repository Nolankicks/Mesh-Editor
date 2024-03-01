using System;

/// <summary>
/// Describes a set of n-gon polygons used to construct a mesh.
/// Any overlapping vertices will be merged if <see cref="MergeVertices"/>.
/// </summary>
public sealed class PolygonMesh
{
	public HalfEdgeMesh Mesh { get; private set; }
	public Model Model { get; private set; }
	public Vector3 TextureOrigin { get; set; }
	public bool UseCollision { get; set; }

	private readonly List<int> _triangleFaces = new();
	private readonly List<int> _meshIndices = new();
	private readonly List<Vector3> _meshVertices = new();
	private Dictionary<int, FaceMesh> _meshFaces;

	public bool Dirty { get; private set; }

	private struct FaceMesh
	{
		public int VertexStart;
		public int IndexStart;
		public int VertexCount;
		public int IndexCount;
	}

	public Vertex[] CreateFace( int face, Transform transform, Color color )
	{
		if ( _meshFaces == null || !_meshFaces.TryGetValue( face, out FaceMesh faceMesh ) )
			return null;

		return Enumerable.Range( faceMesh.IndexStart, faceMesh.IndexCount )
					.Select( x => new Vertex( transform.PointToWorld( _meshVertices[_meshIndices[x]] ), color ) )
					.ToArray();
	}

	private static readonly Material DefaultMaterial = Material.Load( "materials/dev/reflectivity_30.vmat" );

	/// <summary>
	/// Whether to merge overlapping vertices.
	/// </summary>
	public bool MergeVertices { get; set; } = true;

	/// <summary>
	/// Vertex merge tolerance. See <see cref="MergeVertices"/>.
	/// </summary>
	public float VertexMergeTolerance { get; set; } = 0.001f;

	public PolygonMesh()
	{
		Mesh = new();
	}

	public PolygonMesh( HalfEdgeMesh mesh )
	{
		Mesh = mesh;
	}

	/// <summary>
	/// Adds a new vertex to the end of the Vertex list.
	/// </summary>
	/// <param name="vertex">Vertex to add.</param>
	/// <returns>The index of the newly added vertex.</returns>
	public int AddVertex( Vector3 vertex )
	{
		if ( MergeVertices )
		{
			var v = Mesh.Vertices.Select( ( item, index ) => new { Item = item, Index = index } )
				.FirstOrDefault( x => x.Item.Position.Distance( vertex ) <= VertexMergeTolerance );
			if ( v is not null && v.Index >= 0 && !v.Item.IsUnused )
				return v.Index;
		}

		return Mesh.Vertices.Add( vertex );
	}

	/// <summary>
	/// Adds a new face to the end of the Faces list.
	/// </summary>
	/// <param name="indices">The vertex indices which define the face, ordered anticlockwise.</param>
	/// <returns>The newly added face.</returns>
	public void AddFace( params int[] indices )
	{
		// Don't allow degenerate faces
		if ( indices.Length < 3 )
			return;

		Mesh.Faces.AddFace( indices );
	}

	/// <summary>
	/// Adds a new face to the end of the Faces list and it's vertices to the end of the Vertices list.
	/// </summary>
	/// <param name="vertices">The vertices which define the face, ordered anticlockwise.</param>
	/// <returns>The newly added face.</returns>
	public void AddFace( params Vector3[] vertices )
	{
		// Don't allow degenerate faces
		if ( vertices.Length < 3 )
			return;

		AddFace( vertices.Select( AddVertex ).ToArray() );
	}

	public int TriangleToFace( int triangle )
	{
		if ( triangle < 0 || triangle >= _triangleFaces.Count )
			return -1;

		var face = _triangleFaces[triangle];
		if ( Mesh.Faces[face].IsUnused )
			return -1;

		return face;
	}

	public void ExtrudeFace( int faceIndex, Vector3 offset = default )
	{
		var faceIndices = Mesh.Faces.GetFaceVertices( faceIndex );
		var faceVertices = faceIndices.Select( v => Mesh.Vertices[v] );

		FaceData faceData = Mesh.Faces[faceIndex];
		var sideFaceData = faceData;
		sideFaceData.TextureUAxis = 0;
		sideFaceData.TextureVAxis = 0;

		var newFaceIndices = Mesh.Vertices.AddVertices( faceVertices.Select( v => v.Position + offset ) );
		Mesh.Faces.ReplaceFace( faceIndex, newFaceIndices, faceData );

		int numVertices = newFaceIndices.Length;

		for ( int i = 0; i < numVertices; ++i )
		{
			var v1 = newFaceIndices[i];
			var v2 = faceIndices[i];
			var v3 = faceIndices[(i + 1) % numVertices];
			var v4 = newFaceIndices[(i + 1) % numVertices];

			Mesh.Faces.AddFace( v1, v2, v3, v4, sideFaceData );
		}

		Dirty = true;
	}

	public void ExtrudeFaces( IEnumerable<int> faces, Vector3 offset = default )
	{
		var vertices = new Dictionary<int, int>();
		foreach ( var faceIndex in faces )
		{
			var faceVertices = Mesh.Faces.GetFaceVertices( faceIndex );
			var newVertices = new int[faceVertices.Length];
			for ( int i = 0; i < newVertices.Length; ++i )
			{
				if ( vertices.TryGetValue( faceVertices[i], out var newVertex ) )
				{
					newVertices[i] = newVertex;
				}
				else
				{
					var faceVertex = faceVertices[i];
					newVertex = Mesh.Vertices.Add( Mesh.Vertices[faceVertex].Position + offset );
					newVertices[i] = newVertex;
					vertices[faceVertex] = newVertex;
				}
			}

			var faceEdges = Mesh.Faces.GetHalfedges( faceIndex );
			var numEdges = faceEdges.Length;
			var edgeSet = new HashSet<int>();
			var edgeTraits = new FaceData[numEdges];
			var traits = Mesh.Faces[faceIndex].Traits;

			for ( int i = 0; i < numEdges; ++i )
			{
				var pairEdge = Mesh.Halfedges.GetPairHalfedge( faceEdges[i] );
				var edge = Mesh.Halfedges[pairEdge];

				if ( !faces.Contains( edge.AdjacentFace ) )
				{
					edgeSet.Add( i );
					edgeTraits[i] = edge.AdjacentFace < 0 ? traits : Mesh.Faces[edge.AdjacentFace].Traits;
				}
			}

			Mesh.Faces.ReplaceFace( faceIndex, newVertices, traits );

			faceEdges = Mesh.Faces.GetHalfedges( faceIndex );
			foreach ( var i in edgeSet )
			{
				traits = edgeTraits[i];
				traits.TextureUAxis = 0;
				traits.TextureVAxis = 0;

				var v1 = newVertices[i];
				var v2 = faceVertices[i];
				var v3 = faceVertices[(i + 1) % numEdges];
				var v4 = newVertices[(i + 1) % numEdges];

				Mesh.Faces.AddFace( v1, v2, v3, v4, traits );
			}
		}

		Dirty = true;
	}

	public void DetachFaces( IEnumerable<int> faces, Vector3 offset = default )
	{
		var vertices = new Dictionary<int, int>();
		foreach ( var faceIndex in faces )
		{
			var faceVertices = Mesh.Faces.GetFaceVertices( faceIndex );
			var newVertices = new int[faceVertices.Length];
			for ( int i = 0; i < newVertices.Length; ++i )
			{
				if ( vertices.TryGetValue( faceVertices[i], out var newVertex ) )
				{
					newVertices[i] = newVertex;
				}
				else
				{
					var faceVertex = faceVertices[i];
					newVertex = Mesh.Vertices.Add( Mesh.Vertices[faceVertex].Position + offset );
					newVertices[i] = newVertex;
					vertices[faceVertex] = newVertex;
				}
			}

			var traits = Mesh.Faces[faceIndex].Traits;
			Mesh.Faces.ReplaceFace( faceIndex, newVertices, traits );
		}

		Dirty = true;
	}

	public int ExtrudeEdge( int edgeIndex, Vector3 offset = default )
	{
		var pairEdge = Mesh.Halfedges.GetPairHalfedge( edgeIndex );
		if ( Mesh.Halfedges[pairEdge].AdjacentFace >= 0 )
			return -1;

		var faceData = Mesh.Faces[Mesh.Halfedges[edgeIndex].AdjacentFace].Traits;
		faceData.TextureUAxis = 0;
		faceData.TextureVAxis = 0;

		var edgeVertices = Mesh.Halfedges.GetVertices( edgeIndex );
		var a = Mesh.Vertices[edgeVertices[0]].Position + offset;
		var b = Mesh.Vertices[edgeVertices[1]].Position + offset;

		var v1 = Mesh.Vertices.Add( a );
		var v2 = Mesh.Vertices.Add( b );

		var face = Mesh.Faces.AddFace( v1, v2, edgeVertices[1], edgeVertices[0], faceData );
		if ( face < 0 )
			return -1;

		Dirty = true;

		return Mesh.Faces.GetHalfedges( face ).FirstOrDefault();
	}

	public Line GetEdge( int edge )
	{
		if ( Mesh.Halfedges[edge].IsUnused )
			return default;

		var edgeVertices = Mesh.Halfedges.GetVertices( edge );
		var a = Mesh.Vertices[edgeVertices[0]].Position;
		var b = Mesh.Vertices[edgeVertices[1]].Position;

		return new Line( a, b );
	}

	public int[] GetEdgeVertices( int edge )
	{
		if ( Mesh.Halfedges[edge].IsUnused )
			return default;

		return Mesh.Halfedges.GetVertices( edge );
	}

	public void SetVertexPosition( int v, Vector3 position )
	{
		Mesh.Vertices[v].Position = position;
		Dirty = true;
	}

	public Vector3 GetAverageFaceNormal( int f )
	{
		return Mesh.Faces.GetAverageFaceNormal( f ).Normal;
	}

	public Vector3 GetFaceCenter( int f )
	{
		return Mesh.Faces.GetFaceCenter( f );
	}

	public Vector3 GetVertexPosition( int f )
	{
		return Mesh.Vertices[f].Position;
	}

	public int[] GetFaceVertices( int f )
	{
		if ( f < 0 || Mesh.Faces[f].IsUnused )
			return null;

		return Mesh.Faces.GetFaceVertices( f );
	}

	public int[] GetFaceEdges( int f )
	{
		if ( f < 0 || Mesh.Faces[f].IsUnused )
			return null;

		return Mesh.Faces.GetHalfedges( f );
	}

	public float GetTextureAngle( int f )
	{
		if ( f < 0 || Mesh.Faces[f].IsUnused )
			return default;

		return Mesh.Faces[f].Traits.TextureAngle;
	}

	public void SetTextureAngle( int f, float angle )
	{
		if ( f < 0 || Mesh.Faces[f].IsUnused )
			return;

		var traits = Mesh.Faces[f].Traits;
		traits.TextureAngle = angle;
		Mesh.Faces[f].Traits = traits;

		Dirty = true;
	}

	public Vector2 GetTextureOffset( int f )
	{
		if ( f < 0 || Mesh.Faces[f].IsUnused )
			return default;

		return Mesh.Faces[f].Traits.TextureOffset;
	}

	public void SetTextureOffset( int f, Vector2 offset )
	{
		if ( f < 0 || Mesh.Faces[f].IsUnused )
			return;

		var traits = Mesh.Faces[f].Traits;
		traits.TextureOffset = offset;
		Mesh.Faces[f].Traits = traits;

		Dirty = true;
	}

	public Vector2 GetTextureScale( int f )
	{
		if ( f < 0 || Mesh.Faces[f].IsUnused )
			return default;

		return Mesh.Faces[f].Traits.TextureScale;
	}

	public void SetTextureScale( int f, Vector2 scale )
	{
		if ( f < 0 || Mesh.Faces[f].IsUnused )
			return;

		var traits = Mesh.Faces[f].Traits;
		traits.TextureScale = scale;
		Mesh.Faces[f].Traits = traits;

		Dirty = true;
	}

	public void TextureAlignToGrid( Transform transform, int f )
	{
		if ( f < 0 || Mesh.Faces[f].IsUnused )
			return;

		var traits = Mesh.Faces[f].Traits;
		traits.TextureAngle = 0;
		traits.TextureOffset = 0;
		traits.TextureScale = 0.25f;

		var rotation = transform.Rotation.Inverse;
		traits.TextureOrigin = (transform.Position * rotation) - TextureOrigin;

		var normal = GetAverageFaceNormal( f );
		if ( !normal.IsNearZeroLength )
		{
			ComputeTextureAxes( normal, out var uAxis, out var vAxis );
			traits.TextureUAxis = (uAxis * rotation).Normal;
			traits.TextureVAxis = (vAxis * rotation).Normal;
		}

		Mesh.Faces[f].Traits = traits;

		Dirty = true;
	}

	public void SetFaceMaterial( int f, Material material )
	{
		if ( material is null )
			return;

		if ( f < 0 || Mesh.Faces[f].IsUnused )
			return;

		var traits = Mesh.Faces[f].Traits;
		traits.TextureName = material.Name;
		Mesh.Faces[f].Traits = traits;

		Dirty = true;
	}

	public Material GetFaceMaterial( int f )
	{
		if ( f < 0 || Mesh.Faces[f].IsUnused )
			return default;

		return Material.Load( Mesh.Faces[f].Traits.TextureName );
	}

	public void RemoveFaces( IEnumerable<int> faces )
	{
		foreach ( var i in faces )
		{
			Mesh.Faces.RemoveFace( i );
		}

		Mesh.Compact();

		Dirty = true;
	}

	public void ApplyPlanarMapping()
	{
		var faceData = new FaceData
		{
			TextureScale = 0.25f,
			TextureUAxis = Vector3.Right,
			TextureVAxis = Vector3.Forward
		};

		for ( var i = 0; i < Mesh.Faces.Count; i++ )
		{
			var normal = GetAverageFaceNormal( i );
			if ( !normal.IsNearZeroLength )
			{
				ComputeTextureAxes( normal, out var uAxis, out var vAxis );
				faceData.TextureUAxis = uAxis;
				faceData.TextureVAxis = vAxis;
				faceData.TextureName = Mesh.Faces[i].Traits.TextureName;
				Mesh.Faces[i].Traits = faceData;
			}
		}

		Dirty = true;
	}

	private class Submesh
	{
		public List<SimpleVertex> Vertices { get; init; } = new();
		public List<int> Indices { get; init; } = new();
		public Material Material { get; set; }
		public Vector2 TextureSize { get; set; }
	}

	public void Rebuild()
	{
		Log.Info( "rebuild" );
		var submeshes = new Dictionary<string, Submesh>();

		_triangleFaces.Clear();
		_meshIndices.Clear();
		_meshVertices.Clear();
		_meshFaces = new();

		var builder = Model.Builder;

		for ( var i = 0; i < Mesh.Faces.Count; ++i )
		{
			var face = Mesh.Faces[i];
			if ( face.IsUnused )
				continue;

			FaceData faceData = face;
			var textureName = faceData.TextureName ?? "";

			if ( !submeshes.TryGetValue( textureName, out var submesh ) )
			{
				var material = Material.Load( textureName );
				submesh = new();
				submesh.Material = material;
				submeshes.Add( textureName, submesh );

				Vector2 textureSize = 512;
				if ( material != null )
				{
					var width = material.Attributes.GetInt( "WorldMappingWidth" );
					var height = material.Attributes.GetInt( "WorldMappingHeight" );
					var texture = material.FirstTexture;
					if ( texture != null )
					{
						textureSize = texture.Size;
						if ( width > 0 ) textureSize.x = width / 0.25f;
						if ( height > 0 ) textureSize.y = height / 0.25f;
					}
				}

				submesh.TextureSize = textureSize;
			}

			TriangulateFace( i, submesh );
		}

		foreach ( var submesh in submeshes.Values )
		{
			var vertices = submesh.Vertices;
			var indices = submesh.Indices;

			if ( vertices.Count < 3 )
				continue;

			if ( indices.Count < 3 )
				continue;

			var bounds = BBox.FromPoints( vertices.Select( x => x.position ) );
			var material = submesh.Material ?? DefaultMaterial;
			var mesh = new Mesh( material );
			mesh.CreateVertexBuffer( vertices.Count, SimpleVertex.Layout, vertices );
			mesh.CreateIndexBuffer( indices.Count, indices );
			mesh.Bounds = bounds;
			builder.AddMesh( mesh );
		}

		if ( UseCollision )
			builder.AddCollisionMesh( _meshVertices.ToArray(), _meshIndices.ToArray() );

		Model = builder.Create();

		Dirty = false;
	}

	private Vector2 PlanarUV( Vector3 vertexPosition, FaceData faceData, Vector2 textureSize )
	{
		var uv = Vector2.Zero;
		var scale = 1.0f / textureSize;

		uv.x = Vector3.Dot( faceData.TextureUAxis, TextureOrigin + faceData.TextureOrigin + vertexPosition );
		uv.y = Vector3.Dot( faceData.TextureVAxis, TextureOrigin + faceData.TextureOrigin + vertexPosition );

		uv *= scale;

		var xScale = faceData.TextureScale.x;
		var yScale = faceData.TextureScale.y;
		uv.x /= xScale.AlmostEqual( 0.0f ) ? 0.25f : xScale;
		uv.y /= yScale.AlmostEqual( 0.0f ) ? 0.25f : yScale;

		var cosAngle = MathF.Cos( faceData.TextureAngle.DegreeToRadian() );
		var sinAngle = MathF.Sin( faceData.TextureAngle.DegreeToRadian() );
		uv = new Vector2( uv.x * cosAngle - uv.y * sinAngle, uv.y * cosAngle + uv.x * sinAngle );

		uv += faceData.TextureOffset * scale;

		return uv;
	}

	private static LibTessDotNet.Vec3 ConvertToTessVertex( Vector3 p ) => new() { X = p.x, Y = p.y, Z = p.z };

	private void TriangulateFace( int faceIndex, Submesh submesh )
	{
		var face = Mesh.Faces[faceIndex];
		if ( face.IsUnused )
			return;

		var faceVertexIndices = Mesh.Faces.GetFaceVertices( faceIndex );

		var tess = new LibTessDotNet.Tess();
		tess.AddContour( faceVertexIndices
			.Select( v => new LibTessDotNet.ContourVertex
			{
				Position = ConvertToTessVertex( Mesh.Vertices[v].Position ),
				Data = v
			} )
			.ToArray() );

		tess.Tessellate( LibTessDotNet.WindingRule.EvenOdd, LibTessDotNet.ElementType.Polygons, 3 );

		var elems = tess.Elements;
		var numElems = tess.ElementCount;

		if ( numElems == 0 )
			return;

		var normal = Mesh.Faces.GetAverageFaceNormal( faceIndex );

		var vertices = submesh.Vertices;
		var triangles = submesh.Indices;
		var textureSize = submesh.TextureSize;

		FaceData faceData = face;
		if ( faceData.TextureUAxis.IsNearlyZero() || faceData.TextureVAxis.IsNearlyZero() )
		{
			if ( !normal.IsNearlyZero() )
			{
				ComputeTextureAxes( normal, out var uAxis, out var vAxis );
				faceData.TextureUAxis = uAxis;
				faceData.TextureVAxis = vAxis;
				Mesh.Faces[faceIndex].Traits = faceData;
			}
		}

		int startVertex = vertices.Count;
		int startCollisionVertex = _meshVertices.Count;
		vertices.AddRange( tess.Vertices
			.Where( v => v.Data is not null )
			.Select( v => new SimpleVertex
			{
				position = Mesh.Vertices[(int)v.Data].Position,
				normal = normal,
				tangent = faceData.TextureUAxis,
				texcoord = PlanarUV( Mesh.Vertices[(int)v.Data].Position, faceData, textureSize ),
			} ) );

		_meshVertices.AddRange( tess.Vertices
			.Where( v => v.Data is not null )
			.Select( v => Mesh.Vertices[(int)v.Data].Position ) );

		if ( startVertex == vertices.Count )
			return;

		var startIndex = _meshIndices.Count;
		for ( int index = 0; index < numElems; ++index )
		{
			var triangle = index * 3;

			var a = startVertex + elems[triangle];
			var b = startVertex + elems[triangle + 1];
			var c = startVertex + elems[triangle + 2];

			if ( a < 0 || a >= vertices.Count )
				return;

			if ( b < 0 || b >= vertices.Count )
				return;

			if ( c < 0 || c >= vertices.Count )
				return;

			var ab = vertices[b].position - vertices[a].position;
			var ac = vertices[c].position - vertices[a].position;
			var area = Vector3.Cross( ab, ac ).Length * 0.5f;

			if ( area.AlmostEqual( 0.0f ) )
				continue;

			triangles.Add( a );
			triangles.Add( b );
			triangles.Add( c );

			_triangleFaces.Add( faceIndex );

			_meshIndices.Add( startCollisionVertex + elems[triangle] );
			_meshIndices.Add( startCollisionVertex + elems[triangle + 1] );
			_meshIndices.Add( startCollisionVertex + elems[triangle + 2] );
		}

		_meshFaces.Add( faceIndex, new FaceMesh
		{
			VertexCount = _meshVertices.Count - startCollisionVertex,
			IndexCount = _meshIndices.Count - startIndex,
			VertexStart = startCollisionVertex,
			IndexStart = startIndex,
		} );
	}

	private static readonly Vector3[] FaceNormals =
	{
		new( 0, 0, 1 ),
		new( 0, 0, -1 ),
		new( 0, -1, 0 ),
		new( 0, 1, 0 ),
		new( -1, 0, 0 ),
		new( 1, 0, 0 ),
	};

	private static readonly Vector3[] FaceRightVectors =
	{
		new( 1, 0, 0 ),
		new( 1, 0, 0 ),
		new( 1, 0, 0 ),
		new( -1, 0, 0 ),
		new( 0, -1, 0 ),
		new( 0, 1, 0 ),
	};

	private static readonly Vector3[] FaceDownVectors =
	{
		new( 0, -1, 0 ),
		new( 0, -1, 0 ),
		new( 0, 0, -1 ),
		new( 0, 0, -1 ),
		new( 0, 0, -1 ),
		new( 0, 0, -1 ),
	};

	public static void ComputeTextureAxes( Vector3 normal, out Vector3 uAxis, out Vector3 vAxis )
	{
		var orientation = GetOrientationForPlane( normal );
		uAxis = FaceRightVectors[orientation];
		vAxis = FaceDownVectors[orientation];
	}

	public static Vector3 ComputeTextureUAxis( Vector3 normal ) => FaceRightVectors[GetOrientationForPlane( normal )];
	public static Vector3 ComputeTextureVAxis( Vector3 normal ) => FaceDownVectors[GetOrientationForPlane( normal )];

	private static int GetOrientationForPlane( Vector3 plane )
	{
		plane = plane.Normal;

		var maxDot = 0.0f;
		int orientation = 0;

		for ( int i = 0; i < 6; i++ )
		{
			var dot = Vector3.Dot( plane, FaceNormals[i] );

			if ( dot >= maxDot )
			{
				maxDot = dot;
				orientation = i;
			}
		}

		return orientation;
	}
}
