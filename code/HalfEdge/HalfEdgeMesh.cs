using Plankton;
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

	protected override void WriteFaceTraits( BinaryWriter writer, FaceData face )
	{
		writer.Write( face.TextureOrigin.x );
		writer.Write( face.TextureOrigin.y );
		writer.Write( face.TextureOrigin.z );
		writer.Write( face.TextureUAxis.x );
		writer.Write( face.TextureUAxis.y );
		writer.Write( face.TextureUAxis.z );
		writer.Write( face.TextureVAxis.x );
		writer.Write( face.TextureVAxis.y );
		writer.Write( face.TextureVAxis.z );
		writer.Write( face.TextureScale.x );
		writer.Write( face.TextureScale.y );
		writer.Write( face.TextureOffset.x );
		writer.Write( face.TextureOffset.y );
		writer.Write( face.TextureAngle );
	}

	protected override FaceData ReadFaceTraits( BinaryReader reader )
	{
		var textureOrigin = new Vector3( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
		var textureUAxis = new Vector3( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
		var textureVAxis = new Vector3( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
		var textureScale = new Vector2( reader.ReadSingle(), reader.ReadSingle() );
		var textureOffset = new Vector2( reader.ReadSingle(), reader.ReadSingle() );
		var textureAngle = reader.ReadSingle();

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
