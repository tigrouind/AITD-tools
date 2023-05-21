
using System;
using System.Drawing;
using System.Windows.Forms;

namespace VarsViewer
{
	public class ToolTip
	{
		RectangleF toolTipRect;
		string toolTipText;

		readonly Control parent;
		readonly StringFormat format = new StringFormat();

		readonly Brush darkGreenBrush = new SolidBrush(Color.FromArgb(255, 21, 103, 79));
		readonly Brush whiteBrush = new SolidBrush(Color.FromArgb(255, 255, 255, 255));

		public ToolTip(Control parent)
		{
			this.parent = parent;
			format.LineAlignment = StringAlignment.Center;
			format.Alignment = StringAlignment.Center;
		}

		public void Paint(PaintEventArgs e)
		{
			if (toolTipText != string.Empty && toolTipRect.IntersectsWith(e.ClipRectangle))
			{
				e.Graphics.FillRectangle(darkGreenBrush, toolTipRect);
				e.Graphics.DrawString(toolTipText, parent.Font, whiteBrush, toolTipRect, format);
			}
		}

		public void Show(string text, RectangleF rectangle)
		{
			SizeF textSize;
			using (var graphics = parent.CreateGraphics())
			{
				textSize = graphics.MeasureString(text, parent.Font, new SizeF(250.0f, int.MaxValue), format);
			}

			var point = new PointF((rectangle.Left + rectangle.Right - textSize.Width) / 2.0f, rectangle.Bottom);

			var rect = new RectangleF(Math.Max(Math.Min(point.X, parent.ClientRectangle.Width - textSize.Width), 0.0f),
									Math.Max(Math.Min(point.Y, parent.ClientRectangle.Height - textSize.Height), 0.0f), textSize.Width, textSize.Height);

			if (toolTipRect != RectangleF.Empty) Invalidate(toolTipRect);
			if (rect != RectangleF.Empty) Invalidate(rect);

			toolTipRect = rect;
			toolTipText = text;
		}

		public void Hide()
		{
			if (toolTipRect != RectangleF.Empty)
			{
				Invalidate(toolTipRect);
				toolTipRect = Rectangle.Empty;
			}

			toolTipText = string.Empty;
		}

		void Invalidate(RectangleF rectangle)
		{
			using (var region = new Region(rectangle))
			{
				parent.Invalidate(region);
			}
		}
	}
}
