using Sandbox;
using System;
using System.Linq;

namespace Editor.MeshEditor;

public partial class BlockTool
{
	Widget Widget { get; set; }
	PropertySheet Properties { get; set; }
	StatusWidget Header { get; set; }

	ComboBox GeometryComboBox { get; set; }

	protected Widget BuildUI()
	{
		Widget = new Widget( null );
		Widget.Layout = Layout.Column();
		Widget.Layout.Margin = 4;

		{
			Header = new StatusWidget( Widget );
			UpdateStatus();
			Widget.Layout.Add( Header );
		}

		Widget.Layout.AddSpacingCell( 8 );

		{
			var hLayout = Widget.Layout.AddRow();
			hLayout.Spacing = 4;

			var label = new Label( "Geometry Type" );
			GeometryComboBox = new ComboBox();

			foreach ( var builder in GetBuilderTypes() )
			{
				var displayInfo = DisplayInfo.ForType( builder.TargetType );
				Log.Info( displayInfo.Name );
				GeometryComboBox.AddItem( displayInfo.Name, displayInfo.Icon ?? "square", () =>
					Current = _primitives.FirstOrDefault( x => x.GetType() == builder.TargetType ) );
			}

			hLayout.Add( label );
			hLayout.Add( GeometryComboBox, 1 );
		}

		Widget.Layout.AddSpacingCell( 8 );

		{
			Properties = new PropertySheet( Widget );
			Properties.IncludeHeader = false;
			Properties.Target = Current;
			//Properties.OnChildValuesChanged += ( Widget _ ) => IBlockTool.UpdateTool();
			Widget.Layout.Add( Properties, 1 );
		}

		Widget.Layout.AddSpacingCell( 8 );
		Widget.Layout.AddStretchCell();

		return Widget;
	}

	private void UpdateStatus()
	{
		Header.Text = $"{(InProgress ? "Placing" : "Create")} Geometry";
		Header.LeadText = InProgress ? "Press Enter to complete the geometry." : "Drag out a rectangle to create the geometry.";
		Header.Color = InProgress ? Theme.Blue : Theme.Green;
		Header.Icon = InProgress ? "check_circle_outline" : "view_in_ar";
		Header.Update();
	}
}

internal class StatusWidget : Widget
{
	public string Icon { get; set; }
	public string Text { get; set; }
	public string LeadText { get; set; }
	public Color Color { get; set; }

	public StatusWidget( Widget parent ) : base( parent )
	{
		MinimumSize = 48;
		SetSizeMode( SizeMode.Default, SizeMode.CanShrink );
	}

	protected override void OnPaint()
	{
		var rect = new Rect( 0, Size );

		Paint.ClearPen();
		Paint.SetBrush( Theme.Black.Lighten( 0.9f ) );
		Paint.DrawRect( rect );

		rect.Left += 8;

		Paint.SetPen( Color );
		var iconRect = Paint.DrawIcon( rect, Icon, 24, TextFlag.LeftCenter );

		rect.Top += 8;
		rect.Left = iconRect.Right + 8;

		Paint.SetPen( Color );
		Paint.SetDefaultFont( 10, 500 );
		var titleRect = Paint.DrawText( rect, Text, TextFlag.LeftTop );

		rect.Top = titleRect.Bottom + 2;

		Paint.SetPen( Color.WithAlpha( 0.6f ) );
		Paint.SetDefaultFont( 8, 400 );
		Paint.DrawText( rect, LeadText, TextFlag.LeftTop );
	}
}
