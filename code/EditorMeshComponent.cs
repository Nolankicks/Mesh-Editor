using Sandbox.Diagnostics;

using static Sandbox.Component;

public sealed class EditorMeshComponent : Collider, ExecuteInEditor, ITintable, IMaterialSetter
{
	[Property, Hide]
	private HalfEdgeMesh Mesh { get; set; }

	[Property, Hide]
	public Vector3 TextureOrigin { get; set; }

	public Model Model => PolygonMesh?.Model;
	public PolygonMesh PolygonMesh { get; private set; }

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
				_sceneObject.ColorTint = Color;
		}
	}

	private Color _color = Color.White;
	private SceneObject _sceneObject;
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
			_sceneObject.Transform = Transform.World;

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

		if ( Mesh is null )
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

		PolygonMesh ??= new PolygonMesh( Mesh );

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

	public void CreateFromPolygonMesh( PolygonMesh mesh )
	{
		PolygonMesh = mesh;
		Mesh = PolygonMesh.Mesh;
		TextureOrigin = PolygonMesh.TextureOrigin;

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

		var color = new Color( 0.3137f, 0.7843f, 1.0f, 0.5f );

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

		_dirty = true;
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

		_dirty = true;
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

		_dirty = true;
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

		_dirty = true;
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

		_dirty = true;
	}

	public void RebuildMesh()
	{
		PolygonMesh.UseCollision = true;
		PolygonMesh.TextureOrigin = TextureOrigin;
		PolygonMesh.Rebuild();

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
		return PolygonMesh.TriangleToFace( triangle );
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

		var newFaceIndices = Mesh.Vertices.AddVertices( faceVertices.Select( v => v.Position ) );
		Mesh.Faces.ReplaceFace( faceIndex, newFaceIndices, faceData );

		int numVertices = newFaceIndices.Length;

		for ( int i = 0; i < numVertices; ++i )
		{
			var v1 = newFaceIndices[i];
			var v2 = faceIndices[i];
			var v3 = faceIndices[(i + 1) % numVertices];
			var v4 = newFaceIndices[(i + 1) % numVertices];

			var a = Mesh.Vertices[v1].Position + extrudeOffset;
			var b = Mesh.Vertices[v2].Position;
			var c = Mesh.Vertices[v3].Position;

			var normal = Vector3.Cross( b - a, c - a ).Normal;

			if ( !normal.IsNearZeroLength && extrudeOffset.Length > 0.0 )
			{
				PolygonMesh.ComputeTextureAxes( normal, out var uAxis, out var vAxis );
				sideFaceData.TextureUAxis = uAxis;
				sideFaceData.TextureVAxis = vAxis;
			}
			else
			{
				sideFaceData = faceData;
			}

			Mesh.Faces.AddFace( v1, v2, v3, v4, sideFaceData );
		}

		_dirty = true;
	}

	public int ExtrudeEdge( int edgeIndex, Vector3 extrudeOffset )
	{
		var pairEdge = Mesh.Halfedges.GetPairHalfedge( edgeIndex );
		if ( Mesh.Halfedges[pairEdge].AdjacentFace >= 0 )
			return -1;

		var faceData = Mesh.Faces[Mesh.Halfedges[edgeIndex].AdjacentFace].Traits;
		var edgeVertices = Mesh.Halfedges.GetVertices( edgeIndex );
		var a = Mesh.Vertices[edgeVertices[0]].Position + extrudeOffset;
		var b = Mesh.Vertices[edgeVertices[1]].Position + extrudeOffset;
		var c = Mesh.Vertices[edgeVertices[0]].Position;

		var normal = Vector3.Cross( b - a, c - a ).Normal;
		if ( !normal.IsNearZeroLength && extrudeOffset.Length > 0.0 )
		{
			PolygonMesh.ComputeTextureAxes( normal, out var uAxis, out var vAxis );
			faceData.TextureUAxis = uAxis;
			faceData.TextureVAxis = vAxis;
		}

		var v1 = Mesh.Vertices.Add( a - extrudeOffset );
		var v2 = Mesh.Vertices.Add( b - extrudeOffset );

		var face = Mesh.Faces.AddFace( v1, v2, edgeVertices[1], edgeVertices[0], faceData );
		if ( face < 0 )
			return -1;



		var halfEdges = Mesh.Faces.GetHalfedges( face );
		return halfEdges[0];
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
