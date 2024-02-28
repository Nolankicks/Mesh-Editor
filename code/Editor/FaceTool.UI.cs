using Sandbox;

namespace Editor.MeshEditor;

public partial class FaceTool
{
	private Layout ControlLayout { get; set; }

	protected Widget BuildUI()
	{
		var widget = new Widget( null );
		widget.Layout = Layout.Column();

		ControlLayout = widget.Layout.AddRow();
		RebuildControlSheet();

		widget.Layout.AddStretchCell();

		return widget;
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
}
