﻿using Sandbox;
using System.Linq;

namespace Editor.MeshEditor;

public partial class FaceTool
{
	private Layout ControlLayout { get; set; }

	private Widget BuildUI()
	{
		var widget = new Widget( null );
		widget.Layout = Layout.Column();

		ControlLayout = widget.Layout.AddRow();
		BuildControlSheet();

		widget.Layout.AddStretchCell();

		var button = widget.Layout.Add( new Button( "Align to Grid", widget ) );
		button.ButtonType = "clear";
		button.Clicked += AlignToGrid;

		button = widget.Layout.Add( new Button( "Detach Faces", widget ) );
		button.ButtonType = "clear";
		button.Clicked += DetachFaces;

		return widget;
	}

	private void AlignToGrid()
	{
		foreach ( var face in MeshSelection.OfType<MeshFace>() )
		{
			face.Component.PolygonMesh.TextureAlignToGrid( face.Transform, face.Index );
		}
	}

	private void DetachFaces()
	{
		foreach ( var group in MeshSelection.OfType<MeshFace>()
			.GroupBy( x => x.Component ) )
		{
			group.Key.PolygonMesh.DetachFaces( group.Select( x => x.Index ) );
		}

		CalculateSelectionVertices();
	}

	private void CreateOverlay()
	{
		var window = new WidgetWindow( SceneOverlay, "Face Tool" );
		window.Layout = Layout.Column();
		window.Layout.Margin = 4;
		window.Layout.Add( BuildUI() );
		window.FixedWidth = 400;
		window.FixedHeight = 200;
		window.AdjustSize();

		AddOverlay( window, TextFlag.LeftBottom, 10 );
	}

	protected override void OnMeshSelectionChanged()
	{
		BuildControlSheet();
		SceneOverlay.Update();
	}

	private void BuildControlSheet()
	{
		if ( !ControlLayout.IsValid() )
			return;

		ControlLayout.Clear( true );
		var sheet = new ControlSheet();
		ControlLayout.Add( sheet );

		var mso = new MultiSerializedObject();
		foreach ( var face in MeshSelection.Where( x => x is MeshFace ) )
		{
			var serialized = EditorTypeLibrary.GetSerializedObject( face );
			mso.Add( serialized );
		}

		mso.Rebuild();
		sheet.AddObject( mso );
	}
}
