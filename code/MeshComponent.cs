using System;

public sealed class MeshComponent : Collider, Component.ExecuteInEditor
{
	public enum PrimitiveType
	{
		Plane,
		Box,
	}

	[Property, Group( "Primitive" ), MakeDirty]
	public PrimitiveType Type { get; set; } = PrimitiveType.Plane;

	[Property, Group( "Primitive" ), MakeDirty]
	public Vector3 Center { get; set; } = 0.0f;

	[Property, Title( "Size" ), Group( "Primitive" ), MakeDirty]
	[ShowIf( nameof( Type ), PrimitiveType.Box )]
	public Vector3 BoxSize { get; set; } = 64.0f;

	[Property, Title( "Size" ), Group( "Primitive" ), MakeDirty]
	[ShowIf( nameof( Type ), PrimitiveType.Plane )]
	public Vector2 PlaneSize { get; set; } = 256.0f;

	[Property, Group( "Texture" ), MakeDirty]
	public Material Material { get; set; }

	[Property, Group( "Texture" ), MakeDirty]
	public float TextureScale { get; set; } = 0.25f;

	[Property, Group( "Texture" ), Range( -180.0f, 180.0f ), MakeDirty]
	public float TextureAngle { get; set; }

	private SceneObject _sceneObject;
	private Vector3 _buildScale;

	protected override void OnValidate()
	{
		Static = true;
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();

		Transform.OnTransformChanged += TransformChanged;

		CreateSceneObject();
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		Transform.OnTransformChanged -= TransformChanged;

		if ( _sceneObject.IsValid() )
		{
			_sceneObject.Delete();
			_sceneObject = null;
		}
	}

	private void TransformChanged()
	{
		if ( Transform.Scale != _buildScale )
		{
			CreateSceneObject();
		}
	}

	protected override void OnPreRender()
	{
		base.OnPreRender();

		if ( _sceneObject.IsValid() )
		{
			_sceneObject.Transform = Transform.World;
		}
	}

	protected override void OnDirty()
	{
		base.OnDirty();

		CreateSceneObject();
	}

	private Vector2 PlanarUV( Vector3 vertexPosition, Vector3 uAxis, Vector3 vAxis )
	{
		var uv = Vector2.Zero;
		float scale = 1.0f / 512.0f;

		uv.x = Vector3.Dot( uAxis, vertexPosition );
		uv.y = Vector3.Dot( vAxis, vertexPosition );

		uv *= scale;

		var xScale = TextureScale;
		var yScale = TextureScale;
		uv.x /= xScale.AlmostEqual( 0.0f ) ? 0.25f : xScale;
		uv.y /= yScale.AlmostEqual( 0.0f ) ? 0.25f : yScale;

		var cosAngle = MathF.Cos( TextureAngle.DegreeToRadian() );
		var sinAngle = MathF.Sin( TextureAngle.DegreeToRadian() );
		uv = new Vector2( uv.x * cosAngle - uv.y * sinAngle, uv.y * cosAngle + uv.x * sinAngle );

		return uv;
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

	private static void ComputeTextureAxes( Vector3 normal, out Vector3 uAxis, out Vector3 vAxis )
	{
		var orientation = GetOrientationForPlane( normal );
		uAxis = FaceRightVectors[orientation];
		vAxis = FaceDownVectors[orientation];
	}

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

	private static Vector3[] CalculateFaceCorners( BBox box, int face )
	{
		return face switch
		{
			0 => new Vector3[]
			{
				new( box.Mins.x, box.Mins.y, box.Maxs.z ),
				new( box.Maxs.x, box.Mins.y, box.Maxs.z ),
				new( box.Maxs.x, box.Maxs.y, box.Maxs.z ),
				new( box.Mins.x, box.Maxs.y, box.Maxs.z )
			},
			1 => new Vector3[]
			{
				new( box.Maxs.x, box.Mins.y, box.Mins.z ),
				new( box.Mins.x, box.Mins.y, box.Mins.z ),
				new( box.Mins.x, box.Maxs.y, box.Mins.z ),
				new( box.Maxs.x, box.Maxs.y, box.Mins.z )
			},
			2 => new Vector3[]
			{
				new( box.Mins.x, box.Mins.y, box.Mins.z ),
				new( box.Maxs.x, box.Mins.y, box.Mins.z ),
				new( box.Maxs.x, box.Mins.y, box.Maxs.z ),
				new( box.Mins.x, box.Mins.y, box.Maxs.z )
			},
			3 => new Vector3[]
			{
				new( box.Mins.x, box.Maxs.y, box.Maxs.z ),
				new( box.Maxs.x, box.Maxs.y, box.Maxs.z ),
				new( box.Maxs.x, box.Maxs.y, box.Mins.z ),
				new( box.Mins.x, box.Maxs.y, box.Mins.z )
			},
			4 => new Vector3[]
			{
				new( box.Mins.x, box.Mins.y, box.Mins.z ),
				new( box.Mins.x, box.Mins.y, box.Maxs.z ),
				new( box.Mins.x, box.Maxs.y, box.Maxs.z ),
				new( box.Mins.x, box.Maxs.y, box.Mins.z )
			},
			5 => new Vector3[]
			{
				new( box.Maxs.x, box.Mins.y, box.Maxs.z ),
				new( box.Maxs.x, box.Mins.y, box.Mins.z ),
				new( box.Maxs.x, box.Maxs.y, box.Mins.z ),
				new( box.Maxs.x, box.Maxs.y, box.Maxs.z )
			},
			_ => throw new NotImplementedException()
		};
	}

	private void CreateSceneObject()
	{
		if ( _sceneObject.IsValid() )
		{
			_sceneObject.Delete();
			_sceneObject = null;
		}

		_buildScale = Transform.Scale;

		var vertices = new List<SimpleVertex>();
		var indices = new List<int>();

		if ( Type == PrimitiveType.Plane )
		{
			var box = BBox.FromPositionAndSize( Center, PlaneSize );
			var z = box.Center.z;

			var planeVertices = new Vector3[4]
			{
				new( box.Mins.x, box.Mins.y, z ),
				new( box.Maxs.x, box.Mins.y, z ),
				new( box.Maxs.x, box.Maxs.y, z ),
				new( box.Mins.x, box.Maxs.y, z )
			};

			for ( int i = 0; i < 4; i++ )
			{
				var uv = PlanarUV( planeVertices[i] * Transform.Scale.WithZ( 1 ), Vector3.Forward, Vector3.Right );
				vertices.Add( new SimpleVertex( planeVertices[i], Vector3.Up, Vector3.Forward, uv ) );
			}

			indices.AddRange( new[] { 0, 1, 2, 2, 3, 0 } );
		}
		else if ( Type == PrimitiveType.Box )
		{
			var box = BBox.FromPositionAndSize( Center, BoxSize );
			var verticesList = new List<Vector3>( 24 );
			var inside = Transform.Scale.x < 0 || Transform.Scale.y < 0 || Transform.Scale.z < 0;

			for ( int face = 0; face < 6; face++ )
			{
				var corners = CalculateFaceCorners( box, face );

				var normal = FaceNormals[face] * (inside ? -1.0f : 1.0f);
				var tangent = FaceRightVectors[face];
				ComputeTextureAxes( normal, out var uAxis, out var vAxis );

				foreach ( var corner in corners )
				{
					var uv = PlanarUV( corner * Transform.Scale, uAxis, vAxis );
					vertices.Add( new SimpleVertex( corner, normal, tangent, uv ) );
				}
			}

			indices.AddRange( new int[]
			{
				0, 1, 2, 0, 2, 3,
				4, 5, 6, 4, 6, 7,
				8, 9, 10, 8, 10, 11,
				12, 13, 14, 12, 14, 15,
				16, 17, 18, 16, 18, 19,
				20, 21, 22, 20, 22, 23
			} );
		}
		else
		{
			return;
		}

		var bounds = BBox.FromPoints( vertices.Select( x => x.position ) );
		var material = Material ?? Material.Load( "materials/dev/reflectivity_30.vmat" );
		var mesh = new Mesh( material );
		mesh.CreateVertexBuffer( vertices.Count, SimpleVertex.Layout, vertices );
		mesh.CreateIndexBuffer( indices.Count, indices );
		mesh.Bounds = bounds;

		var model = Model.Builder
			.AddMesh( mesh )
			.Create();

		_sceneObject = new SceneObject( Scene.SceneWorld, model, Transform.World );
		_sceneObject.SetComponentSource( this );
		_sceneObject.Tags.SetFrom( GameObject.Tags );
	}

	protected override IEnumerable<PhysicsShape> CreatePhysicsShapes( PhysicsBody targetBody )
	{
		var tx = targetBody.Transform.ToLocal( Transform.World );

		PhysicsShape shape;

		if ( Type == PrimitiveType.Box )
		{
			var box = BBox.FromPositionAndSize( Center, BoxSize );
			box.Mins *= Transform.Scale;
			box.Maxs *= Transform.Scale;
			box.Mins += tx.Position;
			box.Maxs += tx.Position;
			var inside = Transform.Scale.x < 0 || Transform.Scale.y < 0 || Transform.Scale.z < 0;

			if ( inside )
			{
				var vertices = new Vector3[8]
				{
					new( box.Mins.x, box.Mins.y, box.Mins.z ),
					new( box.Maxs.x, box.Mins.y, box.Mins.z ),
					new( box.Maxs.x, box.Maxs.y, box.Mins.z ),
					new( box.Mins.x, box.Maxs.y, box.Mins.z ),
					new( box.Mins.x, box.Mins.y, box.Maxs.z ),
					new( box.Maxs.x, box.Mins.y, box.Maxs.z ),
					new( box.Maxs.x, box.Maxs.y, box.Maxs.z ),
					new( box.Mins.x, box.Maxs.y, box.Maxs.z )
				};

				for ( int i = 0; i < vertices.Length; i++ )
				{
					vertices[i] *= tx.Rotation;
				}

				shape = targetBody.AddMeshShape( vertices, new int[]
				{
					0, 2, 1, 0, 3, 2,
					4, 5, 6, 4, 6, 7,
					0, 1, 5, 0, 5, 4,
					3, 7, 6, 3, 6, 2,
					0, 7, 3, 0, 4, 7,
					1, 2, 6, 1, 6, 5
				} );
			}
			else
			{
				shape = targetBody.AddBoxShape( box, tx.Rotation );
			}
		}
		else if ( Type == PrimitiveType.Plane )
		{
			var box = BBox.FromPositionAndSize( Center, PlaneSize );
			box.Mins *= Transform.Scale;
			box.Maxs *= Transform.Scale;
			box.Mins += tx.Position;
			box.Maxs += tx.Position;

			var z = box.Center.z;

			var vertices = new Vector3[4]
			{
				new( box.Mins.x, box.Mins.y, z ),
				new( box.Maxs.x, box.Mins.y, z ),
				new( box.Maxs.x, box.Maxs.y, z ),
				new( box.Mins.x, box.Maxs.y, z )
			};

			for ( int i = 0; i < vertices.Length; i++ )
			{
				vertices[i] *= tx.Rotation;
			}

			shape = targetBody.AddMeshShape( vertices, new[] { 0, 1, 2, 2, 3, 0 } );
		}
		else
		{
			yield break;
		}

		if ( Surface is not null )
		{
			shape.SurfaceMaterial = Surface.ResourceName;
		}

		yield return shape;
	}
}
