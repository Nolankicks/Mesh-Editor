using System;

public sealed class EditorMeshComponent : Component, Component.ExecuteInEditor
{
	[Property, Hide] private HalfEdgeMesh Mesh { get; set; }
	private readonly List<int> TriangleFaces = new();
	private SceneObject SceneObject;
	public Model Model { get; private set; }

	protected override void OnEnabled()
	{
		base.OnEnabled();

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

		CreateSceneObject();
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		if ( SceneObject.IsValid() )
		{
			SceneObject.Delete();
			SceneObject = null;
		}
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
	}

	protected override void OnPreRender()
	{
		base.OnPreRender();

		if ( !SceneObject.IsValid() )
			return;

		SceneObject.Transform = Transform.World;
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		using ( Gizmo.ObjectScope( GameObject, GameObject.Transform.World ) )
		{
			if ( !Gizmo.IsHovered && !Gizmo.IsSelected )
				return;
		}

		if ( Mesh is null )
			return;

		Gizmo.Draw.Color = Color.White;
		Gizmo.Draw.LineThickness = 2;

		for ( int i = 0; i < Mesh.Vertices.Count; i++ )
		{
			var v = Mesh.Vertices[i];
			if ( v.IsUnused )
				continue;

			using ( Gizmo.Scope( $"Vertex{i}" ) )
			{
				//Gizmo.Hitbox.Sphere( new Sphere( v.Position, 3 ) );
				Gizmo.Draw.Color = Gizmo.IsHovered ? Color.Green : Color.White;
				Gizmo.Draw.SolidSphere( v.Position, 3, 8, 8 );
			}
		}

		Gizmo.Draw.Color = Color.White;

		for ( int i = 0; i < Mesh.Halfedges.Count; i++ )
		{
			if ( Mesh.Halfedges[i].IsUnused )
				continue;

			int pairIndex = Mesh.Halfedges.GetPairHalfedge( i );
			if ( i > pairIndex )
				continue;

			using ( Gizmo.ObjectScope( i, global::Transform.Zero ) )
			{
				//using var hitScope = Gizmo.Hitbox.LineScope();
				Gizmo.Draw.Color = Gizmo.IsHovered ? Color.Green : Color.White;

				var edgeVertices = Mesh.Halfedges.GetVertices( i );
				var a = Mesh.Vertices[edgeVertices[0]].Position;
				var b = Mesh.Vertices[edgeVertices[1]].Position;

				Gizmo.Draw.Line( a, b );
			}
		}
	}

	public bool Dirty { get; set; }

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
		return Mesh.Faces.GetFaceVertices( f );
	}

	public void CreateSceneObject()
	{
		var vertices = new List<SimpleVertex>();
		var indices = new List<int>();

		TriangleFaces.Clear();

		for ( var i = 0; i < Mesh.Faces.Count; ++i )
		{
			TriangulateFace( i, ref vertices, ref indices );
		}

		var bounds = BBox.FromPoints( vertices.Select( x => x.position ) );
		var material = Material.Load( "materials/dev/reflectivity_30.vmat" );
		var mesh = new Mesh( material );
		mesh.CreateVertexBuffer( vertices.Count, SimpleVertex.Layout, vertices );
		mesh.CreateIndexBuffer( indices.Count, indices );
		mesh.Bounds = bounds;

		Model = Model.Builder
			.AddMesh( mesh )
			.Create();

		if ( SceneObject.IsValid() )
		{
			SceneObject.Delete();
		}

		SceneObject = new SceneObject( Scene.SceneWorld, Model, Transform.World );
		SceneObject.SetComponentSource( this );
		SceneObject.Tags.SetFrom( GameObject.Tags );
	}

	public int TriangleToFace( int triangle )
	{
		if ( triangle < 0 || triangle >= TriangleFaces.Count )
			return -1;

		return TriangleFaces[triangle];
	}

	private static Vector2 PlanarUV( Vector3 vertexPosition, FaceData faceData )
	{
		var uv = Vector2.Zero;
		float scale = 1.0f / 512.0f;

		uv.x = Vector3.Dot( faceData.TextureUAxis, faceData.TextureOrigin + vertexPosition );
		uv.y = Vector3.Dot( faceData.TextureVAxis, faceData.TextureOrigin + vertexPosition );

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

	private void TriangulateFace( int faceIndex, ref List<SimpleVertex> vertices, ref List<int> triangles )
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

		FaceData faceData = face;
		int startVertex = vertices.Count;
		vertices.AddRange( tess.Vertices
			.Where( v => v.Data is not null )
			.Select( v => new SimpleVertex
			{
				position = Mesh.Vertices[(int)v.Data].Position,
				normal = normal,
				tangent = faceData.TextureUAxis,
				texcoord = PlanarUV( Mesh.Vertices[(int)v.Data].Position, faceData ),
			} ) );

		for ( int index = 0; index < numElems; ++index )
		{
			var triangle = index * 3;
			var a = startVertex + elems[triangle];
			var b = startVertex + elems[triangle + 1];
			var c = startVertex + elems[triangle + 2];

			Vector3 ab = vertices[b].position - vertices[a].position;
			Vector3 ac = vertices[c].position - vertices[a].position;
			float area = Vector3.Cross( ab, ac ).Length * 0.5f;

			if ( area.AlmostEqual( 0.0f ) )
				continue;

			triangles.Add( a );
			triangles.Add( b );
			triangles.Add( c );

			TriangleFaces.Add( faceIndex );
		}
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

		Dirty = true;
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
