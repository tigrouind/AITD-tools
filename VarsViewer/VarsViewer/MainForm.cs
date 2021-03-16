using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Shared;

namespace VarsViewer
{
	public partial class MainForm : Form
	{
		Var selectedVar;
		Var focusVar;	
		string inputText;
		bool carretState;
		int carretTime;

		readonly ToolTip toolTip;
		readonly Brush greenBrush = new SolidBrush(Color.FromArgb(255, 43, 193, 118));
		readonly Brush grayBrush = new SolidBrush(Color.FromArgb(255, 28, 28, 38));
		readonly Brush whiteBrush = new SolidBrush(Color.FromArgb(255, 255, 255, 255));
		readonly Brush redBrush = new SolidBrush(Color.FromArgb(255, 240, 68, 77));
		readonly Brush blueBrush = new SolidBrush(Color.FromArgb(64, 0, 162, 232));
		readonly Brush transparentBrush = new SolidBrush(Color.FromArgb(64, 255, 255, 255));
		readonly Brush highlightBrush = new SolidBrush(Color.FromArgb(255, 0, 93, 204));

		readonly StringFormat format = new StringFormat();

		readonly Worker worker;
		int varsLength, cvarsLength;

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
					Invalidate(var);
				}
			}
		}

		void MainFormPaint(object sender, PaintEventArgs e)
		{
			DrawTab(e, worker.Vars, 0, 11);
			DrawTab(e, worker.Cvars, 12, 1);

			toolTip.OnPaint(e);

			if (worker.Freeze)
			{
				e.Graphics.FillRectangle(blueBrush, e.ClipRectangle);
			}
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

		void MainFormResize(object sender, EventArgs e)
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
					ResetCarret();
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
							Invalidate(focusVar);
						}	
						break;
					
					case Keys.Insert:
						BeginEdit();
						if (inputText.Length == 0)
						{
							inputText = "-";	
							Invalidate(focusVar);
						}
						break;
						
					case Keys.Back:
						BeginEdit();
						ResetCarret();
						if(focusVar != null && inputText.Length > 0)
						{
							inputText = inputText.Remove(inputText.Length - 1);	
							Invalidate(focusVar);						
						}
						break;
						
					case Keys.Enter:
						if(focusVar != null)
						{
							CommitEdit();											
							Invalidate(focusVar);
							focusVar = null;
						}
						break;
												
					case Keys.Escape:
						AbortEdit();
						break;
				}
			}
		}
		
		void DrawCell(PaintEventArgs e, int x, int y, Brush back, string text = "", Brush front = null, StringAlignment alignment = StringAlignment.Center, StringAlignment lineAlignment = StringAlignment.Center)
		{
			var rect = new RectangleF(x * CellWidth, y * CellHeight, CellWidth, CellHeight);
			DrawCell(e, rect, back, text, front, alignment, lineAlignment);
		}
		
		void DrawCell(PaintEventArgs e, RectangleF rect, Brush back, string text = "", Brush front = null, StringAlignment alignment = StringAlignment.Center, StringAlignment lineAlignment = StringAlignment.Center, bool selected = false, bool highlight = false)
		{
			if (rect.IntersectsWith(e.ClipRectangle))
			{
				e.Graphics.FillRectangle(back, rect);
				if (selected || highlight)
				{
					e.Graphics.FillRectangle(transparentBrush, rect);
				}
				
				if (text != string.Empty)
				{
					format.LineAlignment = lineAlignment;
					format.Alignment = alignment;
					
					if (highlight && inputText == null)
					{
						var textSize = e.Graphics.MeasureString(text, Font, rect.Size, format);
						var center = new PointF((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2);
						e.Graphics.FillRectangle(highlightBrush, new RectangleF(center.X - textSize.Width / 2, center.Y - textSize.Height / 2, textSize.Width, textSize.Height));
					}
				
					e.Graphics.DrawString(text, Font, front, rect, format);
				}
				else if(highlight && carretState)
				{
					var center = new PointF((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2);					
					e.Graphics.DrawLine(Pens.White, center.X, center.Y - 8, center.X, center.Y + 7);
				}
			}
		}
		
		void DrawTab(PaintEventArgs e, Var[] vars, int position, int rows)
		{
			DrawHeader(e, position, rows);
			DrawCells(e, vars);
		}

		void DrawHeader(PaintEventArgs e, int position, int rows)
		{
			DrawCell(e, 0, position, greenBrush);

			for(int i = 0 ; i < 20 ; i++)
			{
				DrawCell(e, i + 1, position, greenBrush, i.ToString(), grayBrush, StringAlignment.Center, StringAlignment.Far);				
			}
			
			for(int i = 0 ; i < rows ; i++)
			{
				DrawCell(e, 0, i + 1 + position, greenBrush, (i * 20).ToString(), grayBrush, StringAlignment.Far);
			}
		}

		void DrawCells(PaintEventArgs e, Var[] vars)
		{
			foreach(var var in vars)
			{
				bool selected = var == selectedVar;
				bool highlight = var == focusVar;
				string text = var == focusVar && inputText != null ? inputText : var.Text;
				DrawCell(e, GetRectangle(var), GetBackgroundBrush(var), text, whiteBrush, StringAlignment.Center, StringAlignment.Center, selected, highlight);
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
					toolTip.Show(string.Format("#{0}\n{1}", var.Index, var.Name), GetRectangle(var));
				}
				else
				{
					toolTip.Hide();
				}

				if(var != null) Invalidate(var);
				if(selectedVar != null) Invalidate(selectedVar);
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
														
				if(var != null) Invalidate(var);
				if(focusVar != null) Invalidate(focusVar);
				focusVar = var;
				
				ResetCarret();
			}
		}

		bool TryFindVarAtPosition(Point position, out Var result)
		{
			result = worker.Vars.Concat(worker.Cvars)
				.FirstOrDefault(x => GetRectangle(x).Contains(position));

			return result != null;
		}
					
		void BeginEdit()
		{
			if (inputText == null)
			{
				inputText = string.Empty;
				Invalidate(focusVar);
			}
		}
				
		void AbortEdit()
		{
			if(focusVar != null)
			{							
				Invalidate(focusVar);
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
					Invalidate(selectedVar);
					selectedVar = null;
				}

				toolTip.Hide();
			}
		}

		void Invalidate(Var var)
		{
			using (var region = new Region(GetRectangle(var)))
			{
				Invalidate(region);
			}
		}
		
		RectangleF GetRectangle(Var var)
		{			
			int x = var.Index % 20;
			int y = var.Index / 20;
			int rowIndex = (var.Type == VarEnum.VARS ? 1 : 13);
			return new RectangleF((x + 1) * CellWidth, (y + rowIndex) * CellHeight, CellWidth, CellHeight);
		}
		
		float CellWidth 
		{
			get
			{
				return ClientSize.Width / 21.0f;
			}
		}
		
		float CellHeight 
		{
			get
			{
				return ClientSize.Height / 14.0f;
			}
		}
		
		void ResetCarret()
		{
			carretTime = Environment.TickCount;						
			if(focusVar != null && !carretState)
			{
				carretState = true;
				Invalidate(focusVar);
			}
		}
		
		void BlinkCarret()
		{			
			if(focusVar != null)
			{
				carretState = !carretState;
				Invalidate(focusVar);
			}
		}
		
		void TimerTick(object sender, EventArgs e)
		{
			UpdateWorker();
			timer.Interval = worker.IsRunning ? 15 : 1000;		
			if(varsLength != worker.Vars.Length || cvarsLength != worker.Cvars.Length)
			{
				varsLength = worker.Vars.Length;
				cvarsLength = worker.Cvars.Length;
				Invalidate();
			}			
			
			if(!worker.IsRunning)
			{
				AbortEdit();
			}
			
			int time = Environment.TickCount;
			if((time - carretTime) > 530)
			{
			   carretTime = time;
			   BlinkCarret();
			}
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
