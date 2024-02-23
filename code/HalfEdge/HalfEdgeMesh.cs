using Plankton;
using System;
using System.IO;
using System.Text.Json.Serialization;

public struct FaceData
{
	public Vector3 TextureOrigin;
	public Vector3 TextureUAxis;
	public Vector3 TextureVAxis;
	public Vector2 TextureScale;
	public Vector2 TextureOffset;
	public float TextureAngle;
}

public struct VertexData
{
}

public struct HalfEdgeData
{
}

[JsonConverter( typeof( HalfEdgeMeshJsonConverter ) )]
public class HalfEdgeMesh : PlanktonMesh<FaceData, HalfEdgeData, VertexData>
{
	public HalfEdgeMesh() : base()
	{
	}

	public HalfEdgeMesh( HalfEdgeMesh copy ) : base( copy )
	{
	}

	public HalfEdgeMesh(
		IList<Vector3> vertices,
		IList<int> vertexEdgeIndices,
		IList<int> faceEdgeIndices,
		IList<int> edgeVertexIndices,
		IList<int> edgeNextIndices,
		IList<int> edgePrevIndices,
		IList<int> edgeFaceIndices ) : base( vertices, vertexEdgeIndices, faceEdgeIndices, edgeVertexIndices, edgeNextIndices, edgePrevIndices, edgeFaceIndices )
	{
	}

	[Flags]
	private enum TextureFlags
	{
		None = 0,
		TextureOrigin = 1 << 0,
		TextureUAxis = 1 << 1,
		TextureVAxis = 1 << 2,
		TextureScale = 1 << 3,
		TextureOffset = 1 << 4,
		TextureAngle = 1 << 5,
	}

	protected override void WriteFaceTraits( BinaryWriter writer, FaceData face )
	{
		var flags = TextureFlags.None;
		if ( face.TextureOrigin != Vector3.Zero ) flags |= TextureFlags.TextureOrigin;
		if ( face.TextureUAxis != Vector3.Zero ) flags |= TextureFlags.TextureUAxis;
		if ( face.TextureVAxis != Vector3.Zero ) flags |= TextureFlags.TextureVAxis;
		if ( face.TextureScale != Vector2.Zero ) flags |= TextureFlags.TextureScale;
		if ( face.TextureOffset != Vector2.Zero ) flags |= TextureFlags.TextureOffset;
		if ( face.TextureAngle != 0 ) flags |= TextureFlags.TextureAngle;

		writer.Write( (int)flags );

		if ( flags.HasFlag( TextureFlags.TextureOrigin ) )
		{
			writer.Write( face.TextureOrigin.x );
			writer.Write( face.TextureOrigin.y );
			writer.Write( face.TextureOrigin.z );
		}

		if ( flags.HasFlag( TextureFlags.TextureUAxis ) )
		{
			writer.Write( face.TextureUAxis.x );
			writer.Write( face.TextureUAxis.y );
			writer.Write( face.TextureUAxis.z );
		}

		if ( flags.HasFlag( TextureFlags.TextureVAxis ) )
		{
			writer.Write( face.TextureVAxis.x );
			writer.Write( face.TextureVAxis.y );
			writer.Write( face.TextureVAxis.z );
		}

		if ( flags.HasFlag( TextureFlags.TextureScale ) )
		{
			writer.Write( face.TextureScale.x );
			writer.Write( face.TextureScale.y );
		}

		if ( flags.HasFlag( TextureFlags.TextureOffset ) )
		{
			writer.Write( face.TextureOffset.x );
			writer.Write( face.TextureOffset.y );
		}

		if ( flags.HasFlag( TextureFlags.TextureAngle ) )
		{
			writer.Write( face.TextureAngle );
		}
	}

	protected override FaceData ReadFaceTraits( BinaryReader reader )
	{
		var flags = (TextureFlags)reader.ReadInt32();
		var textureOrigin = Vector3.Zero;
		var textureUAxis = Vector3.Zero;
		var textureVAxis = Vector3.Zero;
		var textureScale = Vector2.Zero;
		var textureOffset = Vector2.Zero;
		var textureAngle = 0.0f;

		if ( flags.HasFlag( TextureFlags.TextureOrigin ) )
			textureOrigin = new Vector3( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
		if ( flags.HasFlag( TextureFlags.TextureUAxis ) )
			textureUAxis = new Vector3( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
		if ( flags.HasFlag( TextureFlags.TextureVAxis ) )
			textureVAxis = new Vector3( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
		if ( flags.HasFlag( TextureFlags.TextureScale ) )
			textureScale = new Vector2( reader.ReadSingle(), reader.ReadSingle() );
		if ( flags.HasFlag( TextureFlags.TextureOffset ) )
			textureOffset = new Vector2( reader.ReadSingle(), reader.ReadSingle() );
		if ( flags.HasFlag( TextureFlags.TextureAngle ) )
			textureAngle = reader.ReadSingle();

		Log.Info( textureUAxis );

		return new FaceData
		{
			TextureOrigin = textureOrigin,
			TextureUAxis = textureUAxis,
			TextureVAxis = textureVAxis,
			TextureScale = textureScale,
			TextureOffset = textureOffset,
			TextureAngle = textureAngle,
		};
	}
}
