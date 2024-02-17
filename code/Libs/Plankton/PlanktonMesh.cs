using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Plankton;

/// <summary>
/// This is the main class that describes a plankton mesh.
/// </summary>
public partial class PlanktonMesh<TFaceTraits, THalfedgeTraits, TVertexTraits>
{
	private PlanktonVertexList _vertices;
	private PlanktonHalfEdgeList _halfedges;
	private PlanktonFaceList _faces;

	#region "constructors"
	/// <summary>
	/// Initializes a new (empty) instance of the <see cref="PlanktonMesh"/> class.
	/// </summary>
	public PlanktonMesh()
	{
	}

	/// <summary>
	/// Initializes a new (duplicate) instance of the <see cref="PlanktonMesh"/> class.
	/// </summary>
	public PlanktonMesh( PlanktonMesh<TFaceTraits, THalfedgeTraits, TVertexTraits> source )
	{
		foreach ( var v in source.Vertices )
		{
			Vertices.Add( new PlanktonVertex()
			{
				OutgoingHalfedge = v.OutgoingHalfedge,
				X = v.X,
				Y = v.Y,
				Z = v.Z,
				Traits = v.Traits
			} );
		}
		foreach ( var f in source.Faces )
		{
			Faces.Add( new PlanktonFace()
			{
				FirstHalfedge = f.FirstHalfedge,
				Traits = f.Traits
			} );
		}
		foreach ( var h in source.Halfedges )
		{
			Halfedges.Add( new PlanktonHalfedge()
			{
				StartVertex = h.StartVertex,
				AdjacentFace = h.AdjacentFace,
				NextHalfedge = h.NextHalfedge,
				PrevHalfedge = h.PrevHalfedge,
				Traits = h.Traits
			} );
		}
	}

	public PlanktonMesh(
		IList<Vector3> vertices,
		IList<int> vertexEdgeIndices,
		IList<int> faceEdgeIndices,
		IList<int> edgeVertexIndices,
		IList<int> edgeNextIndices,
		IList<int> edgePrevIndices,
		IList<int> edgeFaceIndices )
	{
		Initialize(
			vertices,
			vertexEdgeIndices,
			faceEdgeIndices,
			edgeVertexIndices,
			edgeNextIndices,
			edgePrevIndices,
			edgeFaceIndices );
	}

	public void Initialize(
		IList<Vector3> vertices,
		IList<int> vertexEdgeIndices,
		IList<int> faceEdgeIndices,
		IList<int> edgeVertexIndices,
		IList<int> edgeNextIndices,
		IList<int> edgePrevIndices,
		IList<int> edgeFaceIndices )
	{
		for ( int i = 0; i < vertices.Count; ++i )
		{
			var vertex = vertices[i];
			var vertexEdgeIndex = vertexEdgeIndices[i];

			Vertices.Add( new PlanktonVertex()
			{
				OutgoingHalfedge = vertexEdgeIndex,
				X = (float)vertex.x,
				Y = (float)vertex.y,
				Z = (float)vertex.z
			} );
		}

		for ( int i = 0; i < faceEdgeIndices.Count; ++i )
		{
			Faces.Add( new PlanktonFace()
			{
				FirstHalfedge = faceEdgeIndices[i]
			} );
		}

		for ( int i = 0; i < edgeVertexIndices.Count; ++i )
		{
			Halfedges.Add( new PlanktonHalfedge()
			{
				StartVertex = edgeVertexIndices[i],
				AdjacentFace = edgeFaceIndices[i],
				NextHalfedge = edgeNextIndices[i],
				PrevHalfedge = edgePrevIndices[i]
			} );
		}

		List<int[]> faces = new();

		for ( int i = 0; i < Faces.Count; i++ )
		{
			if ( Faces[i].IsUnused )
				continue;

			faces.Add( Faces.GetFaceVertices( i ) );
		}

		_vertices = new( this );
		_faces = new( this );
		_halfedges = new( this );

		Vertices.AddVertices( vertices );

		foreach ( var f in faces )
		{
			Faces.AddFace( f );
		}
	}

	protected virtual void WriteFaceTraits( BinaryWriter writer, TFaceTraits face )
	{
	}

	protected virtual TFaceTraits ReadFaceTraits( BinaryReader reader )
	{
		return default;
	}

	public byte[] Serialize()
	{
		using var ms = new MemoryStream();
		using ( var bw = new BinaryWriter( ms ) )
		{
			Serialize( bw );
		}

		return ms.ToArray();
	}

	public void Serialize( BinaryWriter writer )
	{
		writer.Write( Vertices.Count );
		writer.Write( Halfedges.Count );
		writer.Write( Faces.Count );

		foreach ( var v in Vertices )
		{
			writer.Write( v.X );
			writer.Write( v.Y );
			writer.Write( v.Z );
			writer.Write( v.OutgoingHalfedge );
		}

		foreach ( var he in Halfedges )
		{
			writer.Write( he.StartVertex );
			writer.Write( he.AdjacentFace );
			writer.Write( he.NextHalfedge );
		}

		foreach ( var f in Faces )
		{
			writer.Write( f.FirstHalfedge );
			WriteFaceTraits( writer, f.Traits );
		}
	}

	public void Deserialize( BinaryReader reader )
	{
		var vertexCount = reader.ReadInt32();
		var edgeCount = reader.ReadInt32();
		var faceCount = reader.ReadInt32();

		for ( int i = 0; i < vertexCount; ++i )
		{
			Vertices.Add( new PlanktonVertex()
			{
				X = reader.ReadSingle(),
				Y = reader.ReadSingle(),
				Z = reader.ReadSingle(),
				OutgoingHalfedge = reader.ReadInt32(),
			} );
		}

		for ( int i = 0; i < edgeCount; ++i )
		{
			Halfedges.Add( new PlanktonHalfedge()
			{
				StartVertex = reader.ReadInt32(),
				AdjacentFace = reader.ReadInt32(),
				NextHalfedge = reader.ReadInt32(),
			} );
		}

		for ( int i = 0; i < faceCount; ++i )
		{
			Faces.Add( new PlanktonFace()
			{
				FirstHalfedge = reader.ReadInt32(),
				Traits = ReadFaceTraits( reader ),
			} );
		}

		for ( int i = 0; i < edgeCount; ++i )
		{
			if ( Halfedges[i].NextHalfedge < 0 ) continue;
			Halfedges[Halfedges[i].NextHalfedge].PrevHalfedge = i;
		}
	}
	#endregion

	#region "properties"
	/// <summary>
	/// Gets access to the <see cref="PlanktonVertexList"/> collection in this mesh.
	/// </summary>
	public PlanktonVertexList Vertices
	{
		get { return _vertices ?? (_vertices = new PlanktonVertexList( this )); }
	}

	/// <summary>
	/// Gets access to the <see cref="PlanktonHalfedgeList"/> collection in this mesh.
	/// </summary>
	public PlanktonHalfEdgeList Halfedges
	{
		get { return _halfedges ?? (_halfedges = new PlanktonHalfEdgeList( this )); }
	}

	/// <summary>
	/// Gets access to the <see cref="PlanktonFaceList"/> collection in this mesh.
	/// </summary>
	public PlanktonFaceList Faces
	{
		get { return _faces ?? (_faces = new PlanktonFaceList( this )); }
	}
	#endregion

	#region "general methods"

	/// <summary>
	/// Calculate the volume of the mesh
	/// </summary>
	public float Volume()
	{
		float volumeSum = 0;
		for ( int i = 0; i < this.Faces.Count; i++ )
		{
			int[] FaceVerts = this.Faces.GetFaceVertices( i );
			int EdgeCount = FaceVerts.Length;
			if ( EdgeCount == 3 )
			{
				var p = this.Vertices[FaceVerts[0]].Position;
				var q = this.Vertices[FaceVerts[1]].Position;
				var r = this.Vertices[FaceVerts[2]].Position;
				//get the signed volume of the tetrahedron formed by the triangle and the origin
				volumeSum += (1 / 6f) * (
				   p.x * q.y * r.z +
				   p.y * q.z * r.x +
				   p.z * q.x * r.y -
				   p.x * q.z * r.y -
				   p.y * q.x * r.z -
				   p.z * q.y * r.x);
			}
			else
			{
				var p = this._faces.GetFaceCenter( i );
				for ( int j = 0; j < EdgeCount; j++ )
				{
					var q = this.Vertices[FaceVerts[j]].Position;
					var r = this.Vertices[FaceVerts[(j + 1) % EdgeCount]].Position;
					volumeSum += (1 / 6f) * (
						p.x * q.y * r.z +
						p.y * q.z * r.x +
						p.z * q.x * r.y -
						p.x * q.z * r.y -
						p.y * q.x * r.z -
						p.z * q.y * r.x);
				}
			}
		}
		return volumeSum;
	}

	public PlanktonMesh<TFaceTraits, THalfedgeTraits, TVertexTraits> Dual()
	{
		// hack for open meshes
		// TODO: improve this ugly method
		if ( this.IsClosed() == false )
		{
			var dual = new PlanktonMesh<TFaceTraits, THalfedgeTraits, TVertexTraits>();

			// create vertices from face centers
			for ( int i = 0; i < this.Faces.Count; i++ )
			{
				dual.Vertices.Add( this.Faces.GetFaceCenter( i ) );
			}

			// create faces from the adjacent face indices of non-boundary vertices
			for ( int i = 0; i < this.Vertices.Count; i++ )
			{
				if ( this.Vertices.IsBoundary( i ) )
				{
					continue;
				}
				dual.Faces.AddFace( this.Vertices.GetVertexFaces( i ) );
			}

			return dual;
		}

		// can later add options for other ways of defining face centres (barycenter/circumcenter etc)
		// won't work yet with naked boundaries

		var P = this;
		var D = new PlanktonMesh<TFaceTraits, THalfedgeTraits, TVertexTraits>();

		//for every primal face, add the barycenter to the dual's vertex list
		//dual vertex outgoing HE is primal face's start HE
		//for every vertex of the primal, add a face to the dual
		//dual face's startHE is primal vertex's outgoing's pair

		for ( int i = 0; i < P.Faces.Count; i++ )
		{
			var fc = P.Faces.GetFaceCenter( i );
			D.Vertices.Add( new PlanktonVertex( fc.x, fc.y, fc.z ) );
			int[] FaceHalfedges = P.Faces.GetHalfedges( i );
			for ( int j = 0; j < FaceHalfedges.Length; j++ )
			{
				if ( P.Halfedges[P.Halfedges.GetPairHalfedge( FaceHalfedges[j] )].AdjacentFace != -1 )
				{
					// D.Vertices[i].OutgoingHalfedge = FaceHalfedges[j];
					D.Vertices[D.Vertices.Count - 1].OutgoingHalfedge = P.Halfedges.GetPairHalfedge( FaceHalfedges[j] );
					break;
				}
			}
		}

		for ( int i = 0; i < P.Vertices.Count; i++ )
		{
			if ( P.Vertices.NakedEdgeCount( i ) == 0 )
			{
				int df = D.Faces.Add( PlanktonFace.Unset );
				// D.Faces[i].FirstHalfedge = P.PairHalfedge(P.Vertices[i].OutgoingHalfedge);
				D.Faces[df].FirstHalfedge = P.Vertices[i].OutgoingHalfedge;
			}
		}

		// dual halfedge start V is primal AdjacentFace
		// dual halfedge AdjacentFace is primal end V
		// dual nextHE is primal's pair's prev
		// dual prevHE is primal's next's pair

		// halfedge pairs stay the same

		for ( int i = 0; i < P.Halfedges.Count; i++ )
		{
			if ( (P.Halfedges[i].AdjacentFace != -1) & (P.Halfedges[P.Halfedges.GetPairHalfedge( i )].AdjacentFace != -1) )
			{
				PlanktonHalfedge DualHE = PlanktonHalfedge.Unset;
				PlanktonHalfedge PrimalHE = P.Halfedges[i];
				//DualHE.StartVertex = PrimalHE.AdjacentFace;
				DualHE.StartVertex = P.Halfedges[P.Halfedges.GetPairHalfedge( i )].AdjacentFace;

				if ( P.Vertices.NakedEdgeCount( PrimalHE.StartVertex ) == 0 )
				{
					//DualHE.AdjacentFace = P.Halfedges[P.PairHalfedge(i)].StartVertex;
					DualHE.AdjacentFace = PrimalHE.StartVertex;
				}
				else { DualHE.AdjacentFace = -1; }

				//This will currently fail with open meshes...
				//one option could be to build the dual with all halfedges, but mark some as dead
				//if they connect to vertex -1
				//mark the 'external' faces all as -1 (the ones that are dual to boundary verts)
				//then go through and if any next or prevs are dead hes then replace them with the next one around
				//this needs to be done repeatedly until no further change

				//DualHE.NextHalfedge = P.Halfedges[P.PairHalfedge(i)].PrevHalfedge;
				DualHE.NextHalfedge = P.Halfedges.GetPairHalfedge( PrimalHE.PrevHalfedge );

				//DualHE.PrevHalfedge = P.PairHalfedge(PrimalHE.NextHalfedge);
				DualHE.PrevHalfedge = P.Halfedges[P.Halfedges.GetPairHalfedge( i )].NextHalfedge;

				D.Halfedges.Add( DualHE );
			}
		}
		return D;
	}

	public bool IsClosed()
	{
		for ( int i = 0; i < this.Halfedges.Count; i++ )
		{
			if ( this.Halfedges[i].AdjacentFace < 0 )
			{
				return false;
			}
		}
		return true;
	}

	/// <summary>
	/// Truncates the vertices of a mesh.
	/// </summary>
	/// <param name="t">Optional parameter for the normalised distance along each edge to control the amount of truncation.</param>
	/// <returns>A new mesh, the result of the truncation.</returns>
	public PlanktonMesh<TFaceTraits, THalfedgeTraits, TVertexTraits> TruncateVertices( float t = 1f / 3 )
	{
		// TODO: handle special cases (t = 0.0, t = 0.5, t > 0.5)
		var tMesh = new PlanktonMesh<TFaceTraits, THalfedgeTraits, TVertexTraits>( this );

		var vxyz = tMesh.Vertices.Select( v => v.Position ).ToArray();
		Vector3 v0, v1, v2;
		int[] oh;
		for ( int i = 0; i < this.Vertices.Count; i++ )
		{
			oh = this.Vertices.GetHalfedges( i );
			tMesh.Vertices.TruncateVertex( i );
			foreach ( var h in oh )
			{
				v0 = vxyz[this.Halfedges[h].StartVertex];
				v1 = vxyz[this.Halfedges.EndVertex( h )];
				v2 = v0 + (v1 - v0) * t;
				tMesh.Vertices.SetVertex( tMesh.Halfedges[h].StartVertex, v2.x, v2.y, v2.z );
			}
		}

		return tMesh;
	}

	/// <summary>
	/// Removes any unreferenced objects from arrays, reindexes as needed and shrinks arrays to minimum required size.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown if halfedge count is odd after compaction.
	/// Most likely caused by only marking one of the halfedges in a pair for deletion.</exception>
	public void Compact()
	{
		// Compact vertices, faces and halfedges
		this.Vertices.CompactHelper();
		this.Faces.CompactHelper();
		this.Halfedges.CompactHelper();
	}

	#endregion
}
