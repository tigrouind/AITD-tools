using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Media;
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
		readonly Brush grayBrush = new SolidBrush(Color.FromArgb(255, 28, 28, 38));
		readonly Brush whiteBrush = new SolidBrush(Color.FromArgb(255, 255, 255, 255));
		readonly Brush lightGrayBrush = new SolidBrush(Color.FromArgb(255, 67, 67, 77));
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
			timer = new Timer();
			timer.Interval = 530;
			timer.Tick += TimerTick;
		}

		#region Drawing

		void DrawTab(PaintEventArgs e, List<Var> cells, int position, int rows)
		{
			DrawHeader(e, position, rows);
			DrawCells(e, cells);
		}

		void DrawHeader(PaintEventArgs e, int position, int rows)
		{
			DrawCell(e, 0, position, lightGrayBrush);

			for (int i = 0 ; i < 20 ; i++)
			{
				DrawCell(e, i + 1, position, lightGrayBrush, i.ToString(), Brushes.Black, StringAlignment.Center, StringAlignment.Far);
			}

			for (int i = 0 ; i < rows ; i++)
			{
				DrawCell(e, 0, i + 1 + position, lightGrayBrush, (i * 20).ToString(), Brushes.Black, StringAlignment.Far);
			}
		}

		void DrawCells(PaintEventArgs e, List<Var> cells)
		{
			foreach (var var in cells)
			{
				bool selected = var == selectedVar;
				bool highlight = var == focusVar;
				string text = var == focusVar && inputText != null ? inputText : var.Text;
				DrawCell(e, GetRectangle(var), GetBackgroundBrush(var), text, whiteBrush, StringAlignment.Center, StringAlignment.Center, selected, highlight);
			}
		}

		Brush GetBackgroundBrush(Var var)
		{
			if (var.Difference)
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

				format.LineAlignment = lineAlignment;
				format.Alignment = alignment;
				format.FormatFlags = StringFormatFlags.NoWrap;

				if (text != string.Empty)
				{
					if (highlight && inputText == null)
					{
						var textSize = e.Graphics.MeasureString(text, parent.Font, rect.Size, format);
						var center = new PointF((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2);
						e.Graphics.FillRectangle(highlightBrush, new RectangleF(center.X - textSize.Width / 2, center.Y - textSize.Height / 2, textSize.Width, textSize.Height));
					}

					e.Graphics.DrawString(text, parent.Font, front, rect, format);
				}

				if (highlight && (inputText != null || text == string.Empty) && carretState)
				{
					DrawCarret(e, rect, text);
				}
			}
		}

		void DrawCarret(PaintEventArgs e, RectangleF rect, string text)
		{
			if (text == string.Empty)
			{
				var bounds = GetCaretBounds(e.Graphics, rect, ".");
				var center = (bounds.Left + bounds.Right) / 2;
				e.Graphics.DrawLine(Pens.White, center, bounds.Top, center, bounds.Bottom);
			}
			else
			{
				var bounds = GetCaretBounds(e.Graphics, rect, text);
				e.Graphics.DrawLine(Pens.White, bounds.Right, bounds.Top, bounds.Right, bounds.Bottom);
			}
		}

		public void Refresh()
		{
			foreach (Var var in vars.Concat(cvars))
			{
				if (var.Refresh)
				{
					var.Refresh = false;
					Invalidate(var);
				}
			}
		}

		RectangleF GetCaretBounds(Graphics graphics, RectangleF rect, string text)
		{
			CharacterRange[] characterRanges = { new CharacterRange(text.Length - 1, 1) };
			format.SetMeasurableCharacterRanges(characterRanges);
			var region = graphics.MeasureCharacterRanges(text, parent.Font, rect, format).First();
			format.SetMeasurableCharacterRanges(new CharacterRange[0]); //restore to previous state
			var bounds = region.GetBounds(graphics);
			return bounds;
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
				switch (e.KeyCode)
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
				switch ((Keys)e.KeyChar)
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
						ResetCarret();
						if (inputText.Length < (inputText.Contains("-") ? 6 : 5))
						{
							inputText += e.KeyChar;
							Invalidate(focusVar);
						}
						break;

					case Keys.Insert:
						BeginEdit();
						ResetCarret();
						if (inputText.Length == 0)
						{
							inputText = "-";
							Invalidate(focusVar);
						}
						break;

					case Keys.Back:
						BeginEdit();
						ResetCarret();
						if (inputText.Length > 0)
						{
							inputText = inputText.Remove(inputText.Length - 1);
							Invalidate(focusVar);
						}
						break;

					case Keys.Enter:
						CommitEdit();
						break;

					case Keys.Escape:
						AbortEdit();
						break;

					default:
						SystemSounds.Beep.Play();
						break;
				}
			}
		}

		public void MouseMove(MouseEventArgs e)
		{
			TryFindVarAtPosition(e.Location, out Var var);

			if (selectedVar != var)
			{
				if (var != null) CellEnter.Invoke(this, new CellEventArgs { Var = var, Rectangle = GetRectangle(var) });
				if (var == null) CellLeave.Invoke(this, new CellEventArgs { Var = selectedVar, Rectangle = GetRectangle(selectedVar) });

				if (var != null) Invalidate(var);
				if (selectedVar != null) Invalidate(selectedVar);
				selectedVar = var;
			}
		}

		public void MouseDown(MouseEventArgs e)
		{
			TryFindVarAtPosition(e.Location, out Var var);

			if (!Editable)
			{
				var = null;
			}

			if (focusVar != var)
			{
				CommitEdit();

				if (var != null)
				{
					inputText = null;
					Invalidate(var);
				}

				focusVar = var;
				ResetCarret();
			}
			else if (inputText == null && var != null && var.Value != 0)
			{
				inputText = var.Text;
				ResetCarret();
				Invalidate(var);
			}
		}

		public void MouseLeave()
		{
			if (selectedVar != null)
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
			if (focusVar != null)
			{
				Invalidate(focusVar);
				focusVar = null;
				timer.Stop();
			}
		}

		public void CommitEdit()
		{
			if (focusVar != null)
			{
				int value = 0;
				if ((inputText == string.Empty || int.TryParse(inputText, out value)) && value != focusVar.Value)
				{
					value = Math.Min(value, short.MaxValue);
					value = Math.Max(value, short.MinValue);
					CellCommit.Invoke(this, new CellEventArgs { Var = focusVar, Rectangle = GetRectangle(focusVar), Value = (short)value });
				}

				Invalidate(focusVar);
				focusVar = null;
				timer.Stop();
			}
		}

		void TimerTick(object sender, EventArgs e)
		{
			if (focusVar != null)
			{
				carretState = !carretState;
				Invalidate(focusVar);
			}
		}

		void ResetCarret()
		{
			if (focusVar != null)
			{
				timer.Stop();
				timer.Start();

				if (!carretState)
				{
					carretState = true;
					Invalidate(focusVar);
				}
			}
		}

		#endregion
	}
}
