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

		if ( PolygonMesh.Dirty )
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
	}

	public void SetMaterial( Material material, int triangle )
	{
		if ( Mesh is null )
			return;

		var face = PolygonMesh.TriangleToFace( triangle );
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

		var face = PolygonMesh.TriangleToFace( triangle );
		if ( face < 0 )
			return default;

		if ( Mesh.Faces[face].IsUnused )
			return default;

		var faceData = Mesh.Faces[face].Traits;
		return Material.Load( faceData.TextureName );
	}
}
