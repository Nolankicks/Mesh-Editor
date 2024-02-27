using System;
using Sandbox.Diagnostics;

using static Sandbox.Component;

public sealed class EditorMeshComponent : Collider, ExecuteInEditor, ITintable, IMaterialSetter
{
	[Property, Hide]
	private HalfEdgeMesh Mesh { get; set; }

	public Model Model { get; private set; }
	public Vector3 TextureOrigin { get; set; }

	[Property, Title( "Tint" )]
	public Color Color
	{
		get => _color;
		set
		{
			if ( _color == value )
				return;

			_color = value;

			if ( _sceneObject.IsValid() )
			{
				_sceneObject.ColorTint = Color;
			}
		}
	}

	private Color _color = Color.White;
	private SceneObject _sceneObject;
	private readonly List<int> _triangleFaces = new();
	private List<int> _meshIndices = new();
	private readonly List<Vector3> _meshVertices = new();
	private bool _dirty;

	public IEnumerable<int> Vertices => Mesh != null ? Enumerable.Range( 0, Mesh.Vertices.Count )
		.Where( x => !Mesh.Vertices[x].IsUnused ) : Enumerable.Empty<int>();

	protected override void OnValidate()
	{
		Static = true;
		Transform.Scale = 1;
	}

	private void TransformChanged()
	{
		var scale = Transform.Scale;
		if ( !scale.x.AlmostEqual( scale.y ) || !scale.y.AlmostEqual( scale.z ) )
		{
			Scale( scale );

			Transform.Scale = 1;
		}

		if ( _sceneObject.IsValid() )
		{
			_sceneObject.Transform = Transform.World;
		}

		if ( KeyframeBody.IsValid() )
		{
			KeyframeBody.BodyType = PhysicsBodyType.Keyframed;
			KeyframeBody.BodyType = PhysicsBodyType.Static;
		}
	}

	private void Scale( Vector3 scale )
	{
		for ( int i = 0; i < Mesh.Vertices.Count; i++ )
		{
			if ( Mesh.Vertices[i].IsUnused )
				continue;

			Mesh.Vertices[i].Position *= scale;
		}

		RebuildMesh();
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();

		Transform.OnTransformChanged += TransformChanged;

		if ( Mesh == null )
		{
			Mesh = new();

			Mesh.Vertices.Add( new Vector3( 0, 0, 0 ) );
			Mesh.Vertices.Add( new Vector3( 512, 0, 0 ) );
			Mesh.Vertices.Add( new Vector3( 512, 512, 0 ) );
			Mesh.Vertices.Add( new Vector3( 0, 512, 0 ) );

			var faceData = new FaceData
			{
				TextureScale = 0.25f,
				TextureUAxis = Vector3.Forward,
				TextureVAxis = Vector3.Right,
			};

			Mesh.Faces.AddFace( 0, 1, 2, 3, faceData );
		}

		RebuildMesh();
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		Transform.OnTransformChanged -= TransformChanged;

		if ( _sceneObject.IsValid() )
		{
			_sceneObject.RenderingEnabled = false;
			_sceneObject.Delete();
			_sceneObject = null;
		}
	}

	protected override IEnumerable<PhysicsShape> CreatePhysicsShapes( PhysicsBody targetBody )
	{
		if ( Model is null || Model.Physics is null )
			yield break;

		var bodyTransform = targetBody.Transform.ToLocal( Transform.World );

		foreach ( var part in Model.Physics.Parts )
		{
			Assert.NotNull( part, "Physics part was null" );

			var bx = bodyTransform.ToWorld( part.Transform );

			foreach ( var mesh in part.Meshes )
			{
				var shape = targetBody.AddShape( mesh, bx, false, true );
				Assert.NotNull( shape, "Mesh shape was null" );

				shape.Surface = mesh.Surface;
				shape.Surfaces = mesh.Surfaces;

				yield return shape;
			}
		}
	}

	public void FromBox( BBox box )
	{
		Mesh = new();

		var position = box.Center;
		box = BBox.FromPositionAndSize( 0, box.Size );
		Transform.Position = position;

		Mesh.Vertices.Add( new Vector3( box.Mins.x, box.Mins.y, box.Mins.z ) );
		Mesh.Vertices.Add( new Vector3( box.Maxs.x, box.Mins.y, box.Mins.z ) );
		Mesh.Vertices.Add( new Vector3( box.Maxs.x, box.Maxs.y, box.Mins.z ) );
		Mesh.Vertices.Add( new Vector3( box.Mins.x, box.Maxs.y, box.Mins.z ) );
		Mesh.Vertices.Add( new Vector3( box.Mins.x, box.Mins.y, box.Maxs.z ) );
		Mesh.Vertices.Add( new Vector3( box.Maxs.x, box.Mins.y, box.Maxs.z ) );
		Mesh.Vertices.Add( new Vector3( box.Maxs.x, box.Maxs.y, box.Maxs.z ) );
		Mesh.Vertices.Add( new Vector3( box.Mins.x, box.Maxs.y, box.Maxs.z ) );

		var faceData = new FaceData
		{
			TextureOrigin = position,
			TextureScale = 0.25f,
			TextureUAxis = Vector3.Right,
			TextureVAxis = Vector3.Forward
		};

		Mesh.Faces.AddFace( 0, 3, 2, 1, faceData );

		faceData.TextureUAxis = Vector3.Forward;
		faceData.TextureVAxis = Vector3.Right;
		Mesh.Faces.AddFace( 4, 5, 6, 7, faceData );

		faceData.TextureUAxis = Vector3.Right;
		faceData.TextureVAxis = Vector3.Down;
		Mesh.Faces.AddFace( 4, 7, 3, 0, faceData );

		faceData.TextureUAxis = Vector3.Left;
		faceData.TextureVAxis = Vector3.Down;
		Mesh.Faces.AddFace( 1, 2, 6, 5, faceData );

		faceData.TextureUAxis = Vector3.Forward;
		faceData.TextureVAxis = Vector3.Down;
		Mesh.Faces.AddFace( 0, 1, 5, 4, faceData );

		faceData.TextureUAxis = Vector3.Backward;
		faceData.TextureVAxis = Vector3.Down;
		Mesh.Faces.AddFace( 3, 7, 6, 2, faceData );

		RebuildMesh();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( _dirty )
		{
			RebuildMesh();
		}
	}

	protected override void OnPreRender()
	{
		base.OnPreRender();

		if ( !_sceneObject.IsValid() )
			return;

		_sceneObject.Transform = Transform.World;
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		if ( !Gizmo.Settings.GizmosEnabled )
			return;

		if ( Mesh is null )
			return;

		var color = new Color( 0.3137f, 0.7843f, 1.0f );

		using ( Gizmo.Scope( "Vertices" ) )
		{
			Gizmo.Draw.Color = color;

			for ( int i = 0; i < Mesh.Vertices.Count; i++ )
			{
				var v = Mesh.Vertices[i];
				if ( v.IsUnused )
					continue;

				Gizmo.Draw.Sprite( v.Position, 8, null, false );
			}
		}

		using ( Gizmo.Scope( "Edges" ) )
		{
			Gizmo.Draw.Color = color;
			Gizmo.Draw.LineThickness = 2;

			for ( int i = 0; i < Mesh.Halfedges.Count; i++ )
			{
				if ( Mesh.Halfedges[i].IsUnused )
					continue;

				int pairIndex = Mesh.Halfedges.GetPairHalfedge( i );
				if ( i > pairIndex )
					continue;

				var edgeVertices = Mesh.Halfedges.GetVertices( i );
				var a = Mesh.Vertices[edgeVertices[0]].Position;
				var b = Mesh.Vertices[edgeVertices[1]].Position;

				Gizmo.Draw.Line( a, b );
			}
		}
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
		_dirty = true;
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

	private class Submesh
	{
		public List<SimpleVertex> Vertices { get; init; } = new();
		public List<int> Indices { get; init; } = new();
		public Material Material { get; set; }
		public Vector2 TextureSize { get; set; }
	}

	public void RebuildMesh()
	{
		var submeshes = new Dictionary<string, Submesh>();

		_triangleFaces.Clear();
		_meshIndices.Clear();
		_meshVertices.Clear();

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
			var bounds = BBox.FromPoints( vertices.Select( x => x.position ) );
			var material = submesh.Material ?? Material.Load( "materials/dev/reflectivity_30.vmat" );
			var mesh = new Mesh( material );
			mesh.CreateVertexBuffer( vertices.Count, SimpleVertex.Layout, vertices );
			mesh.CreateIndexBuffer( indices.Count, indices );
			mesh.Bounds = bounds;
			builder.AddMesh( mesh );
		}

		builder.AddCollisionMesh( _meshVertices.ToArray(), _meshIndices.ToArray() );
		Model = builder.Create();

		if ( !_sceneObject.IsValid() )
		{
			_sceneObject = new SceneObject( Scene.SceneWorld, Model, Transform.World );
		}
		else
		{
			_sceneObject.Model = Model;
			_sceneObject.Transform = Transform.World;
		}

		_sceneObject.SetComponentSource( this );
		_sceneObject.Tags.SetFrom( GameObject.Tags );
		_sceneObject.ColorTint = Color.WithAlpha( 1.0f );

		Rebuild();

		_dirty = false;
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

	private Vector2 PlanarUV( Vector3 vertexPosition, FaceData faceData, Vector2 textureSize )
	{
		var uv = Vector2.Zero;
		var scale = 1.0f / textureSize;

		uv.x = Vector3.Dot( faceData.TextureUAxis, TextureOrigin + vertexPosition );
		uv.y = Vector3.Dot( faceData.TextureVAxis, TextureOrigin + vertexPosition );

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

		var normal = Mesh.Faces.GetAverageFaceNormal( faceIndex ).Normal;

		var vertices = submesh.Vertices;
		var triangles = submesh.Indices;
		var textureSize = submesh.TextureSize;

		FaceData faceData = face;
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

		for ( int index = 0; index < numElems; ++index )
		{
			var triangle = index * 3;

			var a = startVertex + elems[triangle];
			var b = startVertex + elems[triangle + 1];
			var c = startVertex + elems[triangle + 2];

			if ( a < 0 || a >= vertices.Count )
			{
				return;
			}

			if ( b < 0 || b >= vertices.Count )
			{
				return;
			}

			if ( c < 0 || c >= vertices.Count )
			{
				return;
			}

			Vector3 ab = vertices[b].position - vertices[a].position;
			Vector3 ac = vertices[c].position - vertices[a].position;
			float area = Vector3.Cross( ab, ac ).Length * 0.5f;

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
	}

	public void OffsetFaces( IEnumerable<int> faces, Vector3 offset )
	{
		var faceVertices = faces.SelectMany( Mesh.Faces.GetFaceVertices )
			.Distinct();

		foreach ( var i in faceVertices )
		{
			Mesh.Vertices.OffsetVertex( i, offset.x, offset.y, offset.z );
		}

		_dirty = true;
	}

	public void ExtrudeFace( int faceIndex, Vector3 extrudeOffset )
	{
		var faceIndices = Mesh.Faces.GetFaceVertices( faceIndex );
		var faceVertices = faceIndices.Select( v => Mesh.Vertices[v] );

		FaceData faceData = Mesh.Faces[faceIndex];
		var sideFaceData = faceData;

		var newFaceIndices = Mesh.Vertices.AddVertices( faceVertices.Select( v => v.Position + extrudeOffset ) );
		Mesh.Faces.ReplaceFace( faceIndex, newFaceIndices, faceData );

		int numVertices = newFaceIndices.Length;

		for ( int i = 0; i < numVertices; ++i )
		{
			var v1 = newFaceIndices[i];
			var v2 = faceIndices[i];
			var v3 = faceIndices[(i + 1) % numVertices];
			var v4 = newFaceIndices[(i + 1) % numVertices];

			var a = Mesh.Vertices[v1].Position;
			var b = Mesh.Vertices[v2].Position;
			var c = Mesh.Vertices[v3].Position;

			var normal = Vector3.Cross( b - a, c - a ).Normal;

			if ( !normal.IsNearZeroLength && extrudeOffset.Length > 0.0 )
			{
				ComputeTextureAxes( normal, out var uAxis, out var vAxis );
				sideFaceData.TextureUAxis = uAxis;
				sideFaceData.TextureVAxis = vAxis;
			}
			else
			{
				sideFaceData = faceData;
			}

			Mesh.Faces.AddFace( v1, v2, v3, v4, sideFaceData );
		}
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

	public void SetMaterial( Material material, int triangle )
	{
		if ( Mesh is null )
			return;

		var face = TriangleToFace( triangle );
		if ( face < 0 )
			return;

		if ( Mesh.Faces[face].IsUnused )
			return;

		var materialName = material?.Name ?? "";

		var faceData = Mesh.Faces[face].Traits;
		if ( faceData.TextureName == materialName )
			return;

		faceData.TextureName = materialName;
		Mesh.Faces[face].Traits = faceData;

		RebuildMesh();
	}

	public Material GetMaterial( int triangle )
	{
		if ( Mesh is null )
			return default;

		var face = TriangleToFace( triangle );
		if ( face < 0 )
			return default;

		if ( Mesh.Faces[face].IsUnused )
			return default;

		var faceData = Mesh.Faces[face].Traits;
		return Material.Load( faceData.TextureName );
	}
}
