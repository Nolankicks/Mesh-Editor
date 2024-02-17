using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

class HalfEdgeMeshJsonConverter : JsonConverter<HalfEdgeMesh>
{
	public override HalfEdgeMesh Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
	{
		if ( reader.TokenType != JsonTokenType.String )
		{
			throw new JsonException();
		}

		try
		{
			using var ms = new MemoryStream( Convert.FromBase64String( reader.GetString() ) );
			using var zs = new GZipStream( ms, CompressionMode.Decompress );
			using var outStream = new MemoryStream();
			zs.CopyTo( outStream );
			outStream.Position = 0;
			using var br = new BinaryReader( outStream );
			var mesh = new HalfEdgeMesh();
			mesh.Deserialize( br );
			return mesh;
		}
		catch
		{
			return null;
		}
	}

	public override void Write( Utf8JsonWriter writer, HalfEdgeMesh value, JsonSerializerOptions options )
	{
		using var ms = new MemoryStream();
		using ( var zs = new GZipStream( ms, CompressionMode.Compress ) )
		{
			var data = value.Serialize();
			zs.Write( data, 0, data.Length );
		}

		writer.WriteBase64StringValue( ms.ToArray() );
	}
}
