using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace VarsViewer
{
	public partial class MainForm : Form
	{
		float cellWidth, cellHeight;

		Var selectedVar;

		readonly ToolTip toolTip;
		readonly Brush greenBrush = new SolidBrush(Color.FromArgb(255, 43, 193, 118));
		readonly Brush grayBrush = new SolidBrush(Color.FromArgb(255, 28, 28, 38));
		readonly Brush lightGrayBrush = new SolidBrush(Color.FromArgb(255, 47, 47, 57));
		readonly Brush whiteBrush = new SolidBrush(Color.FromArgb(255, 255, 255, 255));
		readonly Brush redBrush = new SolidBrush(Color.FromArgb(255, 240, 68, 77));
		readonly Brush blueBrush = new SolidBrush(Color.FromArgb(64, 0, 162, 232));
		readonly Brush transparentBrush = new SolidBrush(Color.FromArgb(96, 0, 0, 0));

		readonly StringFormat format = new StringFormat();

		readonly Worker worker;

		public MainForm()
		{
			toolTip = new ToolTip(this);
			worker = new Worker();
			InitializeComponent();
		}

		void NeedRefresh()
		{
			foreach(Var var in worker.Vars.Concat(worker.Cvars))
			{
				if (var.Refresh)
				{
					var.Refresh = false;
					Invalidate(var.Rectangle);
				}
			}
		}

		void MainFormPaint(object sender, PaintEventArgs e)
		{
			cellWidth = ClientSize.Width / 21.0f;
			cellHeight = ClientSize.Height / 16.0f;

			DrawTab(e, worker.Vars, 0);
			DrawTab(e, worker.Cvars, 12);

			toolTip.OnPaint(e);

			if (worker.Freeze)
			{
				e.Graphics.FillRectangle(blueBrush, e.ClipRectangle);
			}
		}

		protected override void OnPaintBackground(PaintEventArgs e)
		{
		}

		void MainFormLoad(object sender, EventArgs e)
		{
			Font = new Font(Font.Name, Font.Size * 96.0f / AutoScaleDimensions.Width);			
			worker.Update();
		}

		protected override void WndProc(ref Message m)
		{
			if (m.Msg == 0x0112) // WM_SYSCOMMAND
			{
				int wParam = (m.WParam.ToInt32() & 0xFFF0);
				if (wParam == 0xF030 || wParam == 0xF020 || wParam == 0xF120) //SC_MAXIMIZE / SC_MINIMIZE / SC_RESTORE
				{
					 Invalidate();
				}
			}

			base.WndProc(ref m);
		}

		void MainFormResizeEnd(object sender, EventArgs e)
		{
			Invalidate();
		}

		void MainFormKeyDown(object sender, KeyEventArgs e)
		{
			switch(e.KeyCode)
			{
				case Keys.C:
					if (worker.Compare || worker.Vars.Any(x => x.SaveState != x.Value) || worker.Cvars.Any(x => x.SaveState != x.Value))
					{
						worker.Compare = !worker.Compare;
						worker.IgnoreDifferences = !worker.Compare;
						UpdateWorker();
					}
					break;

				case Keys.F:
					worker.Freeze = !worker.Freeze;
					Invalidate();
					break;

				case Keys.S:
					worker.SaveState();
					break;
			}
		}
		
		RectangleF DrawCell(PaintEventArgs e, int x, int y, string text, Brush back, Brush front, StringAlignment alignment)
		{
			return DrawCell(e, x, y, text, back, front, alignment, false);
		}

		RectangleF DrawCell(PaintEventArgs e, int x, int y, string text, Brush back, Brush front, StringAlignment alignment, bool selected)
		{
			var rect = new RectangleF(x * cellWidth, y * cellHeight, cellWidth, cellHeight);
			if (rect.IntersectsWith(e.ClipRectangle))
			{
				format.LineAlignment = StringAlignment.Center;
				format.Alignment = alignment;

				e.Graphics.FillRectangle(back, rect);
				if (selected) e.Graphics.FillRectangle(transparentBrush, rect);
				e.Graphics.DrawString(text, Font, front, rect, format);
			}

			return rect;
		}
		
		void DrawTab(PaintEventArgs e, Var[] vars, int position)
		{
			DrawHeader(e, vars, position);
			DrawCells(e, vars, position);
		}

		void DrawHeader(PaintEventArgs e, Var[] vars, int position)
		{
			DrawCell(e, 0, position,  string.Empty, greenBrush, grayBrush, StringAlignment.Center);

			for(int i = 0 ; i < 20 ; i++)
			{
				DrawCell(e, i + 1, position, i.ToString(), greenBrush, grayBrush, StringAlignment.Center);
			}

			int rows = (vars.Length + 19) / 20;
			for(int i = 0 ; i < rows ; i++)
			{
				DrawCell(e, 0, i + 1 + position, (i * 20).ToString(), greenBrush, grayBrush, StringAlignment.Far);
			}
		}

		void DrawCells(PaintEventArgs e, Var[] vars, int position)
		{
			int rows = (vars.Length + 19) / 20;
			for(int j = 0 ; j < rows ; j++)
			{
				for(int i = 0 ; i < 20 ; i++)
				{
					int index = j * 20 + i;
					if(index < vars.Length)
					{
						Var var = vars[index];
						bool selected = var == selectedVar;
						var.Rectangle = DrawCell(e, i + 1, j + position + 1, var.Text, GetBackgroundBrush(var), whiteBrush, StringAlignment.Center, selected);				
					}
					else
					{
						DrawCell(e, i + 1, j + position + 1, string.Empty, lightGrayBrush, grayBrush, StringAlignment.Center);
					}
				}
			}
		}

		Brush GetBackgroundBrush(Var var)
		{
			if(var.Difference)
			{
				return redBrush;
			}
			
			return grayBrush;
		}

		void MainFormMouseMove(object sender, MouseEventArgs e)
		{
			Var var;
			TryFindVarAtPosition(e.Location, out var);

			if(selectedVar != var)
			{
				if(var != null)
				{
					toolTip.Show(string.Format("#{0}\n{1}", var.Index, var.Name), var.Rectangle);
				}
				else
				{
					toolTip.Hide();
				}

				if(var != null) Invalidate(var.Rectangle);
				if(selectedVar != null) Invalidate(selectedVar.Rectangle);
				selectedVar = var;
			}
		}

		bool TryFindVarAtPosition(Point position, out Var result)
		{
			result = worker.Vars.Concat(worker.Cvars)
				.FirstOrDefault(x => x.Rectangle.Contains(position));

			return result != null;
		}

		void MainFormMouseClick(object sender, MouseEventArgs e)
		{
			Var var;

			if (e.Button == MouseButtons.Left && TryFindVarAtPosition(e.Location, out var) && var.MemoryAddress >= 0)
			{
				var title = string.Format("#{0} {1}", var.Index, var.Name);
				var	input = var.Value == 0 ? string.Empty : var.Value.ToString();
				
				if (DialogBox.Show(title, ref input) == DialogResult.OK)
				{
					int value = 0;
					if ((input == string.Empty || int.TryParse(input, out value)) && value != var.Value)
					{
						value = Math.Min(value, short.MaxValue);
						value = Math.Max(value, short.MinValue);
						worker.Write(var, (short)value);
						UpdateWorker();
					}
				}
			}
		}

		void MainFormMouseLeave(object sender, EventArgs e)
		{
			if (!ClientRectangle.Contains(PointToClient(Cursor.Position)))
			{
				if(selectedVar != null)
				{
					Invalidate(selectedVar.Rectangle);
					selectedVar = null;
				}

				toolTip.Hide();
			}
		}

		void Invalidate(RectangleF rectangle)
		{
			using (var region = new Region(rectangle))
			{
				Invalidate(region);
			}
		}
		
		void TimerTick(object sender, EventArgs e)
		{
			UpdateWorker();
			timer.Interval = worker.IsRunning ? 15 : 1000;		
		}
		
		void UpdateWorker()
		{
			if(worker.Update())
			{
				NeedRefresh();
			}
		}
	}
}
