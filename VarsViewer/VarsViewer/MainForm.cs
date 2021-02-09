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
		Var focusVar;	
		string inputText;

		readonly ToolTip toolTip;
		readonly Brush greenBrush = new SolidBrush(Color.FromArgb(255, 43, 193, 118));
		readonly Brush grayBrush = new SolidBrush(Color.FromArgb(255, 28, 28, 38));
		readonly Brush lightGrayBrush = new SolidBrush(Color.FromArgb(255, 47, 47, 57));
		readonly Brush whiteBrush = new SolidBrush(Color.FromArgb(255, 255, 255, 255));
		readonly Brush redBrush = new SolidBrush(Color.FromArgb(255, 240, 68, 77));
		readonly Brush blueBrush = new SolidBrush(Color.FromArgb(64, 0, 162, 232));
		readonly Brush transparentBrush = new SolidBrush(Color.FromArgb(64, 255, 255, 255));
		readonly Brush highlightBrush = new SolidBrush(Color.FromArgb(255, 0, 93, 204));

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
						AbortEdit();
						UpdateWorker();
					}
					break;

				case Keys.F:
					worker.Freeze = !worker.Freeze;
					AbortEdit();
					Invalidate();
					break;

				case Keys.S:
					worker.SaveState();
					break;
					
				case Keys.Delete:
					BeginEdit();	
					break;
			}
		}
		
		void MainFormKeyPress(object sender, KeyPressEventArgs e)
		{
			if (focusVar != null)
			{				
				switch((Keys)e.KeyChar)
				{
					case Keys.D0:
					case Keys.D1:
					case Keys.D2:
					case Keys.D3:
					case Keys.D4:
					case Keys.D5:
					case Keys.D6:
					case Keys.D7:
					case Keys.D8:
					case Keys.D9:
						BeginEdit();					
						if(inputText.Length < (inputText.Contains("-") ? 6 : 5))
						{						
							inputText += e.KeyChar;
							Invalidate(focusVar.Rectangle);
						}	
						break;
					
					case Keys.Insert:
						BeginEdit();
						if (inputText.Length == 0)
						{
							inputText = "-";	
							Invalidate(focusVar.Rectangle);
						}
						break;
						
					case Keys.Back:
						BeginEdit();
						if(focusVar != null && inputText.Length > 0)
						{
							inputText = inputText.Remove(inputText.Length - 1);	
							Invalidate(focusVar.Rectangle);						
						}
						break;
						
					case Keys.Enter:
						if(focusVar != null)
						{
							CommitEdit();											
							Invalidate(focusVar.Rectangle);
							focusVar = null;
						}
						break;
												
					case Keys.Escape:
						AbortEdit();
						break;
				}
			}
		}
		
		RectangleF DrawCell(PaintEventArgs e, int x, int y, Brush back, string text = "", Brush front = null, StringAlignment alignment = default(StringAlignment), bool selected = false, bool highlight = false)
		{
			var rect = new RectangleF(x * cellWidth, y * cellHeight, cellWidth, cellHeight);
			if (rect.IntersectsWith(e.ClipRectangle))
			{
				e.Graphics.FillRectangle(back, rect);
				if (selected)
				{
					e.Graphics.FillRectangle(transparentBrush, rect);
				}
				
				if (text != string.Empty)
				{
					format.LineAlignment = StringAlignment.Center;
					format.Alignment = alignment;
					
					if (highlight)
					{
						var textSize = e.Graphics.MeasureString(text, Font, rect.Size, format);
						var center = new PointF((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2);
						e.Graphics.FillRectangle(highlightBrush, new RectangleF(center.X - textSize.Width / 2, center.Y - textSize.Height / 2, textSize.Width, textSize.Height));
					}
				
					e.Graphics.DrawString(text, Font, front, rect, format);
				}
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
			DrawCell(e, 0, position,  greenBrush);

			for(int i = 0 ; i < 20 ; i++)
			{
				DrawCell(e, i + 1, position, greenBrush, i.ToString(), grayBrush, StringAlignment.Center);
			}

			int rows = (vars.Length + 19) / 20;
			for(int i = 0 ; i < rows ; i++)
			{
				DrawCell(e, 0, i + 1 + position, greenBrush, (i * 20).ToString(), grayBrush, StringAlignment.Far);
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
						bool selected = var == selectedVar || var == focusVar;
						bool highlight = var == focusVar && inputText == null;
						string text = var == focusVar && inputText != null ? inputText : var.Text;
						var.Rectangle = DrawCell(e, i + 1, j + position + 1, GetBackgroundBrush(var), text, whiteBrush, StringAlignment.Center, selected, highlight);
					}
					else
					{
						DrawCell(e, i + 1, j + position + 1, lightGrayBrush);
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
			
			if (selectedVar != var)
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

		void MainFormMouseClick(object sender, MouseEventArgs e)
		{
			Var var;
			TryFindVarAtPosition(e.Location, out var);	
			
			if((var != null && var.MemoryAddress == -1) || !worker.IsRunning || worker.Freeze || worker.Compare)
			{
				var = null;
			}
		
			if (focusVar != var)
			{
				if(focusVar != null)
				{
					CommitEdit();		
				}
				
				if(var != null)
				{
					inputText = null;
				}
														
				if(var != null) Invalidate(var.Rectangle);
				if(focusVar != null) Invalidate(focusVar.Rectangle);
				focusVar = var;
			}
		}

		bool TryFindVarAtPosition(Point position, out Var result)
		{
			result = worker.Vars.Concat(worker.Cvars)
				.FirstOrDefault(x => x.Rectangle.Contains(position));

			return result != null;
		}
					
		void BeginEdit()
		{
			if (inputText == null)
			{
				inputText = string.Empty;
				Invalidate(focusVar.Rectangle);
			}
		}
				
		void AbortEdit()
		{
			if(focusVar != null)
			{							
				Invalidate(focusVar.Rectangle);
				focusVar = null;
			}
		}
							
		void CommitEdit()
		{
			int value = 0;
			if ((inputText == string.Empty || int.TryParse(inputText, out value)) && value != focusVar.Value)
			{
				value = Math.Min(value, short.MaxValue);
				value = Math.Max(value, short.MinValue);
				worker.Write(focusVar, (short)value);
				UpdateWorker();				
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
