using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GuidControl
{
    [TemplatePart(Name = "PART_TextBox", Type = typeof(TextBox))]
    public class GuidTextBox : Control
    {
        private TextBox _textBox;
        private bool _suppress;

        static GuidTextBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(GuidTextBox),
                new FrameworkPropertyMetadata(typeof(GuidTextBox)));
        }

        // ── GuidValue ──────────────────────────────────────────────────────
        public static readonly DependencyProperty GuidValueProperty =
            DependencyProperty.Register(
                nameof(GuidValue),
                typeof(Guid?),
                typeof(GuidTextBox),
                new FrameworkPropertyMetadata(
                    null,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnGuidValueChanged));

        public Guid? GuidValue
        {
            get { return (Guid?)GetValue(GuidValueProperty); }
            set { SetValue(GuidValueProperty, value); }
        }

        private static void OnGuidValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (GuidTextBox)d;
            if (ctrl._textBox == null) return;

            Guid? guid = (Guid?)e.NewValue;
            ctrl.SetText(guid.HasValue ? guid.Value.ToString().ToUpper() : string.Empty);
        }

        public event EventHandler<Guid?> GuidChanged;

        // ── Template ───────────────────────────────────────────────────────
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (_textBox != null)
            {
                _textBox.TextChanged      -= OnTextChanged;
                _textBox.PreviewTextInput -= OnPreviewTextInput;
                _textBox.PreviewKeyDown   -= OnPreviewKeyDown;
                DataObject.RemovePastingHandler(_textBox, OnPasting);
            }

            _textBox = GetTemplateChild("PART_TextBox") as TextBox;

            if (_textBox != null)
            {
                _textBox.TextChanged      += OnTextChanged;
                _textBox.PreviewTextInput += OnPreviewTextInput;
                _textBox.PreviewKeyDown   += OnPreviewKeyDown;
                DataObject.AddPastingHandler(_textBox, OnPasting);

                if (GuidValue.HasValue)
                    SetText(GuidValue.Value.ToString().ToUpper());
            }
        }

        // ── Saisie ─────────────────────────────────────────────────────────
        private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsHex(e.Text);
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
                e.Handled = true;
        }

        private void OnPasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string pasted = (string)e.DataObject.GetData(typeof(string));
                string clean  = Reformat(pasted);
                e.CancelCommand();
                if (clean != null) SetText(clean);
            }
            else
            {
                e.CancelCommand();
            }
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppress) return;

            int caret = _textBox.CaretIndex;
            string formatted = AutoFormat(_textBox.Text, ref caret);

            if (formatted != _textBox.Text)
            {
                _suppress           = true;
                _textBox.Text       = formatted;
                _textBox.CaretIndex = Math.Min(caret, formatted.Length);
                _suppress           = false;
            }

            Guid guid;
            if (Guid.TryParse(_textBox.Text, out guid))
            {
                if (GuidValue != guid)
                {
                    GuidValue = guid;
                    GuidChanged?.Invoke(this, guid);
                }
            }
            else
            {
                if (GuidValue != null)
                {
                    GuidValue = null;
                    GuidChanged?.Invoke(this, null);
                }
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────
        private void SetText(string text)
        {
            if (_textBox == null) return;
            _suppress           = true;
            _textBox.Text       = text.ToUpper();
            _textBox.CaretIndex = text.Length;
            _suppress           = false;
        }

        private static string AutoFormat(string input, ref int caret)
        {
            string hex = new string(input.Where(c => IsHex(c.ToString())).ToArray());
            if (hex.Length > 32)
                hex = hex.Substring(0, 32);

            int hexCaret = CountHexBefore(input, caret);
            var sb = new System.Text.StringBuilder();
            int pos = 0;

            foreach (char c in hex)
            {
                if (pos == 8 || pos == 12 || pos == 16 || pos == 20)
                    sb.Append('-');
                sb.Append(char.ToUpper(c));
                pos++;
            }

            string result = sb.ToString();
            caret = HexToFormattedIndex(result, hexCaret);
            return result;
        }

        // Retourne null si aucun caractère hex trouvé
        private static string Reformat(string input)
        {
            Guid g;
            if (Guid.TryParse(input, out g))
                return g.ToString().ToUpper();

            string hex = new string(input.Where(c => IsHex(c.ToString())).ToArray());
            if (hex.Length == 0) return null;
            if (hex.Length > 32) hex = hex.Substring(0, 32);

            int dummy = 0;
            return AutoFormat(hex, ref dummy);
        }

        private static bool IsHex(string s)
        {
            return !string.IsNullOrEmpty(s) && "0123456789ABCDEFabcdef".IndexOf(s[0]) >= 0;
        }

        private static int CountHexBefore(string text, int pos)
        {
            int count = 0;
            int limit = Math.Min(pos, text.Length);
            for (int i = 0; i < limit; i++)
                if (IsHex(text[i].ToString())) count++;
            return count;
        }

        private static int HexToFormattedIndex(string formatted, int hexIndex)
        {
            int hex = 0;
            for (int i = 0; i < formatted.Length; i++)
            {
                if (IsHex(formatted[i].ToString()))
                {
                    if (hex == hexIndex) return i;
                    hex++;
                }
            }
            return formatted.Length;
        }
    }
}
