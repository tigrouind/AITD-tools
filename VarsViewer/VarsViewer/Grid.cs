
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Shared;

namespace VarsViewer
{
	public class Grid
	{	
		Var selectedVar;
		Var focusVar;	
		string inputText;
		bool carretState;
		
		readonly List<Var> vars;
		readonly List<Var> cvars;
		readonly Control parent;
		readonly Timer timer;
		
		readonly StringFormat format = new StringFormat();
		readonly Brush greenBrush = new SolidBrush(Color.FromArgb(255, 43, 193, 118));
		readonly Brush grayBrush = new SolidBrush(Color.FromArgb(255, 28, 28, 38));
		readonly Brush whiteBrush = new SolidBrush(Color.FromArgb(255, 255, 255, 255));
		readonly Brush redBrush = new SolidBrush(Color.FromArgb(255, 240, 68, 77));		
		readonly Brush transparentBrush = new SolidBrush(Color.FromArgb(64, 255, 255, 255));
		readonly Brush highlightBrush = new SolidBrush(Color.FromArgb(255, 0, 93, 204));
		
		public EventHandler<CellEventArgs> CellEnter;
		public EventHandler<CellEventArgs> CellLeave;
		public EventHandler<CellEventArgs> CellCommit;
		public bool Editable;
		
		public Grid(Control parent, List<Var> vars, List<Var> cvars)
		{
			this.parent = parent;
			this.vars = vars;
			this.cvars = cvars;	
			this.timer = new Timer();
			this.timer.Interval = 530;
			this.timer.Tick += TimerTick;			
		}
		
		#region Drawing
			
		void DrawTab(PaintEventArgs e, List<Var> cells, int position, int rows)
		{
			DrawHeader(e, position, rows);
			DrawCells(e, cells);
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

		void DrawCells(PaintEventArgs e, List<Var> cells)
		{
			foreach(var var in cells)
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
						var textSize = e.Graphics.MeasureString(text, parent.Font, rect.Size, format);
						var center = new PointF((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2);
						e.Graphics.FillRectangle(highlightBrush, new RectangleF(center.X - textSize.Width / 2, center.Y - textSize.Height / 2, textSize.Width, textSize.Height));
					}
				
					e.Graphics.DrawString(text, parent.Font, front, rect, format);
				}
				else if(highlight && carretState)
				{
					var center = new PointF((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2);					
					e.Graphics.DrawLine(Pens.White, center.X, center.Y - 8, center.X, center.Y + 7);
				}
			}
		}
		
		public void Refresh()
		{
			foreach(Var var in vars.Concat(cvars))
			{
				if (var.Refresh)
				{
					var.Refresh = false;
					Invalidate(var);
				}
			}
		}
		
		#endregion
		
		#region Events
				
		public void Paint(PaintEventArgs e)
		{
			DrawTab(e, vars, 0, 11);
			DrawTab(e, cvars, 12, 1);
		}
		
		public void KeyDown(KeyEventArgs e)
		{
			if (focusVar != null)
			{	
				switch(e.KeyCode)
				{
					case Keys.Delete:
						BeginEdit();	
						ResetCarret();
						break;
				}
			}
		}
		
		public void KeyPress(KeyPressEventArgs e)
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
						if(inputText.Length > 0)
						{
							inputText = inputText.Remove(inputText.Length - 1);	
							Invalidate(focusVar);						
						}
						break;
						
					case Keys.Enter:
						CommitEdit();											
						Invalidate(focusVar);
						focusVar = null;
						break;
												
					case Keys.Escape:
						AbortEdit();
						break;
				}
			}
		}
		
		public void MouseMove(MouseEventArgs e)
		{
			Var var;
			TryFindVarAtPosition(e.Location, out var);	
			
			if (selectedVar != var)
			{		
				if(var != null) CellEnter.Invoke(this, new CellEventArgs { Var = var, Rectangle = GetRectangle(var) });
				if(var == null) CellLeave.Invoke(this, new CellEventArgs { Var = selectedVar, Rectangle = GetRectangle(selectedVar) });
				
				if(var != null) Invalidate(var);
				if(selectedVar != null) Invalidate(selectedVar);
				selectedVar = var;
			}
		}

		public void MouseDown(MouseEventArgs e)
		{
			Var var;
			TryFindVarAtPosition(e.Location, out var);	
			
			if(!Editable)
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

		public void MouseLeave()
		{
			if(selectedVar != null)
			{					
				Invalidate(selectedVar);
				selectedVar = null;
			}
		}
		
		#endregion
						
		RectangleF GetRectangle(Var var)
		{			
			int x = var.Index % 20;
			int y = var.Index / 20;
			int rowIndex = (var.Type == VarEnum.VARS ? 1 : 13);
			return new RectangleF((x + 1) * CellWidth, (y + rowIndex) * CellHeight, CellWidth, CellHeight);
		}

		bool TryFindVarAtPosition(Point position, out Var result)
		{
			result = vars.Concat(cvars)
				.FirstOrDefault(x => GetRectangle(x).Contains(position));

			return result != null;
		}
		
		void Invalidate(Var var)
		{
			using (var region = new Region(GetRectangle(var)))
			{
				parent.Invalidate(region);
			}
		}
			
		float CellWidth 
		{
			get
			{
				return parent.ClientSize.Width / 21.0f;
			}
		}
		
		float CellHeight 
		{
			get
			{
				return parent.ClientSize.Height / 14.0f;
			}
		}
		
		#region CellEdit
						
		void BeginEdit()
		{
			if (inputText == null)
			{				
				inputText = string.Empty;
				Invalidate(focusVar);
			}
		}
				
		public void AbortEdit()
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
				CellCommit.Invoke(this, new CellEventArgs { Var = focusVar, Rectangle = GetRectangle(focusVar), Value = (short)value });
			}
		}
	
		void TimerTick(object sender, EventArgs e)
		{
			if(focusVar != null)
			{
				carretState = !carretState;
				Invalidate(focusVar);
			}
		}	

		void ResetCarret()
		{		
			timer.Stop();
			timer.Start();
			
			if(focusVar != null && !carretState)
			{
				carretState = true;
				Invalidate(focusVar);
			}
		}
				
		#endregion
	}
}
