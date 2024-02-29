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

	private class Submesh
	{
		public List<SimpleVertex> Vertices { get; init; } = new();
		public List<int> Indices { get; init; } = new();
		public Material Material { get; set; }
		public Vector2 TextureSize { get; set; }
	}

	public Vector3 GetAverageFaceNormal( int f )
	{
		return Mesh.Faces.GetAverageFaceNormal( f ).Normal;
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
	}

	public void Rebuild()
	{
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
