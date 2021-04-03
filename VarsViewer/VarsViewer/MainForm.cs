using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Shared;

namespace VarsViewer
{
	public partial class MainForm : Form
	{	
		readonly Grid grid;
		readonly ToolTip toolTip;
		readonly Brush blueBrush = new SolidBrush(Color.FromArgb(64, 0, 162, 232));
		
		readonly List<Var> vars = new List<Var>();
		readonly List<Var> cvars = new List<Var>();
		readonly VarParser varParser = new VarParser();
		
		readonly Worker worker;
		int varsLength, cvarsLength;

		public MainForm()
		{
			worker = new Worker(vars, cvars);
			toolTip = new ToolTip(this);
			
			grid = new Grid(this, vars, cvars);
			grid.CellEnter += GridCellEnter;
			grid.CellLeave += GridCellLeave;
			grid.CellCommit += GridCellCommit;
			
			const string varPath = @"GAMEDATA\vars.txt";
			if (File.Exists(varPath))
			{
				varParser.Load(varPath, VarEnum.VARS, VarEnum.C_VARS);
			}
			
			InitializeComponent();
		}

		void MainFormPaint(object sender, PaintEventArgs e)
		{
			grid.Paint(e);
			toolTip.Paint(e);

			if (worker.Freeze)
			{
				e.Graphics.FillRectangle(blueBrush, e.ClipRectangle);
			}
		}

		void MainFormLoad(object sender, EventArgs e)
		{
			Font = new Font(Font.Name, Font.Size * 96.0f / AutoScaleDimensions.Width);			
			worker.Update();
			grid.AllowEdit = worker.IsRunning;
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
					if (worker.Compare || vars.Any(x => x.SaveState != x.Value) || cvars.Any(x => x.SaveState != x.Value))
					{
						worker.Compare = !worker.Compare;
						worker.IgnoreDifferences = !worker.Compare;
						grid.AbortEdit();
						UpdateWorker();
					}
					break;

				case Keys.F:
					worker.Freeze = !worker.Freeze;
					grid.AbortEdit();
					Invalidate();
					break;

				case Keys.S:
					worker.SaveState();
					break;								
			}
			
			grid.KeyDown(e);
		}
		
		void MainFormKeyPress(object sender, KeyPressEventArgs e)
		{
			grid.KeyPress(e);
		}		

		void MainFormMouseMove(object sender, MouseEventArgs e)
		{
			grid.MouseMove(e);
		}

		void MainFormMouseDown(object sender, MouseEventArgs e)
		{
			grid.MouseDown(e);
		}

		void MainFormMouseLeave(object sender, EventArgs e)
		{
			if (!ClientRectangle.Contains(PointToClient(Cursor.Position)))
			{					
				grid.MouseLeave();
				toolTip.Hide();
			}
		}
		
		void GridCellEnter(object sender, CellEventArgs e)
		{
			string text = varParser.GetText(e.Var.Type, e.Var.Index);
			toolTip.Show(string.Format("#{0}\n{1}", e.Var.Index, text), e.Rectangle);
		}
		
		void GridCellLeave(object sender, CellEventArgs e)
		{
			toolTip.Hide();
		}
		
		void GridCellCommit(object sender, CellEventArgs e)
		{
			worker.Write(e.Var, e.Value);
			UpdateWorker();				
		}
		
		void TimerTick(object sender, EventArgs e)
		{
			UpdateWorker();
			timer.Interval = worker.IsRunning ? 15 : 1000;		
			if (varsLength != vars.Count || cvarsLength != cvars.Count)
			{
				varsLength = vars.Count;
				cvarsLength = cvars.Count;
				Invalidate();
			}			
			
			if (!worker.IsRunning)
			{
				grid.AbortEdit();
			}			
			
			grid.AllowEdit = worker.IsRunning && !worker.Freeze && !worker.Compare;
		}
		
		void UpdateWorker()
		{
			if (worker.Update())
			{
				grid.Refresh();
			}
		}
	}
}
