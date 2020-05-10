using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace VarsViewer
{
	public partial class MainForm : Form
	{	
		float cellWidth, cellHeight;
		Var lastToolTip;

		readonly Brush greenBrush = new SolidBrush(Color.FromArgb(255, 43, 193, 118));
		readonly Brush grayBrush = new SolidBrush(Color.FromArgb(255, 28, 28, 38));	
		readonly Brush lightGrayBrush = new SolidBrush(Color.FromArgb(255, 47, 47, 57));	
		readonly Brush darkGrayBrush = new SolidBrush(Color.FromArgb(255, 17, 17, 17));	
		readonly Brush whiteBrush = new SolidBrush(Color.FromArgb(255, 255, 255, 255));		
		readonly Brush redBrush = new SolidBrush(Color.FromArgb(255, 240, 68, 77));	
		readonly Brush blueBrush = new SolidBrush(Color.FromArgb(64, 0, 162, 232));
		
		readonly Font font = new Font("Arial", 13.0f);
		readonly StringFormat format = new StringFormat();	
		readonly Worker worker;		

		public MainForm()
		{					
			worker = new Worker(() => this.Invoke(new Action(NeedRefresh)));		
			InitializeComponent();			
		}
		
		void NeedRefresh()
		{
			using(Region region = new Region(Rectangle.Empty))
			{
				foreach(Var var in worker.Vars.Concat(worker.Cvars))
				{
					if (var.Refresh)
					{
						var.Refresh = false;
						region.Union(var.Rectangle);
					}
				}
				
				Invalidate(region);
			}
		}

		void MainFormPaint(object sender, PaintEventArgs e)
		{
			cellWidth = ClientSize.Width / 21.0f;
			cellHeight = ClientSize.Height / 16.0f;
			
			DrawHeader(e);
			DrawCells(e);
			
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
			worker.Start();
		}
		
		void MainFormFormClosed(object sender, FormClosedEventArgs e)
		{
			worker.Shutdown();
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
					worker.Compare = !worker.Compare;
					worker.IgnoreDifferences = !worker.Compare;	
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
			var rect = new RectangleF(x * cellWidth, y * cellHeight, cellWidth, cellHeight);
			if(new RectangleF(e.ClipRectangle.Location, e.ClipRectangle.Size).IntersectsWith(rect))
			{
				format.LineAlignment = StringAlignment.Center;
				format.Alignment = alignment;
			
				e.Graphics.FillRectangle(back, rect);
				e.Graphics.DrawString(text, font, front, rect, format);
			}
			
			return rect;
		}
		
		void DrawHeader(PaintEventArgs e)
		{ 
			DrawCell(e, 0, 0,  string.Empty, greenBrush, grayBrush, StringAlignment.Center);
			DrawCell(e, 0, 12, string.Empty, greenBrush, grayBrush, StringAlignment.Near);	
			
			for(int i = 0 ; i < 20 ; i++)
			{
				DrawCell(e, i + 1, 0, i.ToString(), greenBrush, grayBrush, StringAlignment.Center);				
				DrawCell(e, i + 1, 12, i.ToString(), greenBrush, grayBrush, StringAlignment.Center);
			}
			
			for(int i = 0 ; i < 11 ; i++)
			{
				DrawCell(e, 0, i + 1, (i * 20).ToString(), greenBrush, grayBrush, StringAlignment.Far);
			}
			
			for(int i = 0 ; i < 3 ; i++)
			{
				DrawCell(e, 0, i + 13, (i * 20).ToString(), greenBrush, grayBrush, StringAlignment.Far);
			}
		}
		
		void DrawCells(PaintEventArgs e)
		{							
			for(int j = 0 ; j < 11 ; j++)
			{
				for(int i = 0 ; i < 20 ; i++)
				{
					int index = j * 20 + i;					
					if(index < worker.Vars.Length)
					{
						Var var = worker.Vars[index];
						var.Rectangle = DrawCell(e, i + 1, j + 1, var.Text, GetBackgroundBrush(var), whiteBrush, StringAlignment.Center);
					}
					else
					{
						DrawCell(e, i + 1, j + 1, string.Empty, lightGrayBrush, grayBrush, StringAlignment.Center);
					}
				}
			}	

			for(int j = 0 ; j < 3 ; j++)
			{
				for(int i = 0 ; i < 20 ; i++)
				{
					int index = j * 20 + i;
					if(index < worker.Cvars.Length)
					{
						Var var = worker.Cvars[index];
						var.Rectangle = DrawCell(e, i + 1, j + 13, var.Text, GetBackgroundBrush(var), whiteBrush, StringAlignment.Center);
					}
					else
					{
						DrawCell(e, i + 1, j + 13, string.Empty, lightGrayBrush, grayBrush, StringAlignment.Center);
					}
				}
			}	
		}
		
		Brush GetBackgroundBrush(Var var)
		{
			if(lastToolTip == var)
			{
				return darkGrayBrush;
			}
			if(var.Difference)
			{
				return redBrush;
			}
			
			return grayBrush;
		}
		
		void MainFormMouseMove(object sender, MouseEventArgs e)
		{		
			Var var;
			if (!FindVarBehindCursor(e.Location, worker.Vars, out var))
			{				
				FindVarBehindCursor(e.Location, worker.Cvars, out var);
			}
			
			if(lastToolTip != var)
			{
				if(var != null)
				{
					toolTip.Show(string.Format("#{0} {1}", var.Index, var.Name), this,
				             	(int)((var.Rectangle.Left + var.Rectangle.Right) / 2.0f),
				             	(int)((var.Rectangle.Top + var.Rectangle.Bottom) / 2.0f),
				             	5000);					
				}
				else
				{
					toolTip.Hide(this);
				}
				
				if(var != null) Invalidate(var.Rectangle);
				if(lastToolTip != null) Invalidate(lastToolTip.Rectangle);
				lastToolTip = var;
			}
		}
		
		bool FindVarBehindCursor(Point position, Var[] vars, out Var result)
		{
			foreach(Var var in vars)
			{
				if(var.Rectangle.Contains(position))
				{
					result = var;
					return true;
				}
			}
			
			result = null;
			return false;
		}

		void MainFormMouseClick(object sender, MouseEventArgs e)
		{
			Var var;
			if (e.Button == MouseButtons.Left && (FindVarBehindCursor(e.Location, worker.Vars, out var) 
			  || FindVarBehindCursor(e.Location, worker.Cvars, out var))
			  && var.MemoryAddress >= 0)
			{
				string input = Interaction.InputBox(string.Empty, string.Format("#{0} {1}", var.Index, var.Name), var.Value.ToString(),
                                          (DesktopLocation.X + Width) / 2, (DesktopLocation.Y + Height) / 2);
				
				short value;
				if(short.TryParse(input, out value))
				{
					worker.Write(var, value);
				}
			}
		}
		
		void MainFormMouseLeave(object sender, EventArgs e)
		{
			if (!ClientRectangle.Contains(PointToClient(Cursor.Position)))
			{
				if(lastToolTip != null)
				{
					Invalidate(lastToolTip.Rectangle);
					lastToolTip = null;
					toolTip.Hide(this);					
				}
			}
		}
		
		void Invalidate(RectangleF rectangle)
		{
			using (var region = new Region(rectangle))
			{
				Invalidate(region);
			}
		}
	}
}
