using System;
using System.Drawing;
using System.Media;
using System.Windows.Forms;

namespace VarsViewer
{
	public static class DialogBox
	{
		public static DialogResult Show(string title, ref string input)
		{
			Size size = new Size(300, 70);
			
			var okButton = new Button
			{
				DialogResult = DialogResult.OK,
				Name = "okButton",
				Size = new Size(75, 23),
				Text = "&OK",
				Location = new Point(size.Width - 80 - 80, 39)
			};

			var cancelButton = new Button
			{
				DialogResult = DialogResult.Cancel,
				Name = "cancelButton",
				Size = new Size(75, 23),
				Text = "&Cancel",
				Location = new Point(size.Width - 80, 39)
			};
			
			var inputBox = new Form
			{
				FormBorderStyle = FormBorderStyle.FixedDialog,
				MinimizeBox = false,
				MaximizeBox = false,
				ClientSize = size,
				StartPosition = FormStartPosition.CenterParent,
				Text = title,
				AcceptButton = okButton,
				CancelButton = cancelButton		
			};

			var textBox = new TextBox
			{
				Size = new Size(size.Width - 10, 23),
				Location = new Point(5, 5),
				Text = input,
				MaxLength = 6
			};

			textBox.KeyPress += TextBoxKeyPress;
			inputBox.Controls.AddRange(new Control[] { textBox, okButton, cancelButton });
			
			DialogResult result = inputBox.ShowDialog();
			input = textBox.Text;
			return result;
		}
		
		static void TextBoxKeyPress(object sender, KeyPressEventArgs e)
		{
			var textBox = (TextBox)sender;
			if (!(char.IsControl(e.KeyChar) || char.IsDigit(e.KeyChar) || (textBox.SelectionStart == 0 && e.KeyChar == '-')))
			{
				e.Handled = true;
				SystemSounds.Beep.Play();
			}
		}
	}
}
