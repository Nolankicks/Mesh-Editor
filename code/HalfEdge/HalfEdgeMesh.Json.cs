using System;
using System.IO;
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

		var base64Data = reader.GetString();
		var byteArray = Convert.FromBase64String( base64Data );

		using var ms = new MemoryStream( byteArray );
		using var br = new BinaryReader( ms );

		var mesh = new HalfEdgeMesh();
		mesh.Deserialize( br );

		return mesh;
	}

	public override void Write( Utf8JsonWriter writer, HalfEdgeMesh value, JsonSerializerOptions options )
	{
		using var ms = new MemoryStream();
		using ( var bw = new BinaryWriter( ms ) )
		{
			value.Serialize( bw );
		}

		writer.WriteBase64StringValue( ms.ToArray() );
	}
}
