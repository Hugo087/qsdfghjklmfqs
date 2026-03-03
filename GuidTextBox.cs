using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GuidControl
{
    /// <summary>
    /// Custom Control WPF de saisie de GUID.
    /// Le visuel est entièrement défini dans Themes/Generic.xaml.
    /// 
    /// Parts nommés (PART_) requis dans le ControlTemplate :
    ///   PART_TextBox    — TextBox de saisie
    ///   PART_Placeholder — TextBlock placeholder
    ///   PART_Label      — TextBlock du label
    ///   PART_Status     — TextBlock du message de statut
    ///   PART_Seg1Bar … PART_Seg5Bar   — Border des barres de progression
    ///   PART_Seg1Count … PART_Seg5Count — TextBlock des compteurs
    /// </summary>
    [TemplatePart(Name = "PART_TextBox",    Type = typeof(TextBox))]
    [TemplatePart(Name = "PART_Placeholder",Type = typeof(System.Windows.Controls.TextBlock))]
    [TemplatePart(Name = "PART_Label",      Type = typeof(System.Windows.Controls.TextBlock))]
    [TemplatePart(Name = "PART_Status",     Type = typeof(System.Windows.Controls.TextBlock))]
    [TemplatePart(Name = "PART_Seg1Bar",    Type = typeof(Border))]
    [TemplatePart(Name = "PART_Seg2Bar",    Type = typeof(Border))]
    [TemplatePart(Name = "PART_Seg3Bar",    Type = typeof(Border))]
    [TemplatePart(Name = "PART_Seg4Bar",    Type = typeof(Border))]
    [TemplatePart(Name = "PART_Seg5Bar",    Type = typeof(Border))]
    [TemplatePart(Name = "PART_Seg1Count",  Type = typeof(System.Windows.Controls.TextBlock))]
    [TemplatePart(Name = "PART_Seg2Count",  Type = typeof(System.Windows.Controls.TextBlock))]
    [TemplatePart(Name = "PART_Seg3Count",  Type = typeof(System.Windows.Controls.TextBlock))]
    [TemplatePart(Name = "PART_Seg4Count",  Type = typeof(System.Windows.Controls.TextBlock))]
    [TemplatePart(Name = "PART_Seg5Count",  Type = typeof(System.Windows.Controls.TextBlock))]
    public class GuidTextBox : Control
    {
        // ─── Références aux parts du template ──────────────────────────────
        private TextBox?                            _textBox;
        private System.Windows.Controls.TextBlock? _placeholder;
        private System.Windows.Controls.TextBlock? _status;
        private Border[]                            _segBars   = Array.Empty<Border>();
        private System.Windows.Controls.TextBlock[] _segCounts = Array.Empty<System.Windows.Controls.TextBlock>();

        // ─── Constantes ────────────────────────────────────────────────────
        private static readonly int[] SegmentLengths = { 8, 4, 4, 4, 12 };

        private static readonly Color[] SegmentColors =
        {
            (Color)ColorConverter.ConvertFromString("#7C6FCD"),
            (Color)ColorConverter.ConvertFromString("#3DBAED"),
            (Color)ColorConverter.ConvertFromString("#FF9F43"),
            (Color)ColorConverter.ConvertFromString("#FF5370"),
            (Color)ColorConverter.ConvertFromString("#3DDC84"),
        };

        private bool _suppressTextChanged;

        // ═══════════════════════════════════════════════════════════════════
        //  ENREGISTREMENT DU STYLE PAR DÉFAUT (Generic.xaml)
        // ═══════════════════════════════════════════════════════════════════

        static GuidTextBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(GuidTextBox),
                new FrameworkPropertyMetadata(typeof(GuidTextBox)));
        }

        // ═══════════════════════════════════════════════════════════════════
        //  PROPRIÉTÉS DE DÉPENDANCE
        // ═══════════════════════════════════════════════════════════════════

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
            get => (Guid?)GetValue(GuidValueProperty);
            set => SetValue(GuidValueProperty, value);
        }

        private static void OnGuidValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GuidTextBox)d).SyncTextFromGuidValue((Guid?)e.NewValue);
        }

        // ── Label ──────────────────────────────────────────────────────────
        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(
                nameof(Label),
                typeof(string),
                typeof(GuidTextBox),
                new PropertyMetadata("GUID"));

        /// <summary>Texte du label affiché au-dessus du champ.</summary>
        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        // ─── Événement ─────────────────────────────────────────────────────
        public event EventHandler<Guid?>? GuidChanged;

        // ═══════════════════════════════════════════════════════════════════
        //  APPLIQUE LE TEMPLATE — récupération des PART_
        // ═══════════════════════════════════════════════════════════════════

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // Détacher les anciens handlers si le template est rechargé
            if (_textBox != null)
            {
                _textBox.TextChanged       -= TextBox_TextChanged;
                _textBox.PreviewTextInput  -= TextBox_PreviewTextInput;
                _textBox.PreviewKeyDown    -= TextBox_PreviewKeyDown;
                DataObject.RemovePastingHandler(_textBox, TextBox_Pasting);
            }

            // Récupérer les parts nommés
            _textBox     = GetTemplateChild("PART_TextBox")     as TextBox;
            _placeholder = GetTemplateChild("PART_Placeholder") as System.Windows.Controls.TextBlock;
            _status      = GetTemplateChild("PART_Status")      as System.Windows.Controls.TextBlock;

            _segBars = new[]
            {
                GetTemplateChild("PART_Seg1Bar") as Border ?? new Border(),
                GetTemplateChild("PART_Seg2Bar") as Border ?? new Border(),
                GetTemplateChild("PART_Seg3Bar") as Border ?? new Border(),
                GetTemplateChild("PART_Seg4Bar") as Border ?? new Border(),
                GetTemplateChild("PART_Seg5Bar") as Border ?? new Border(),
            };

            _segCounts = new[]
            {
                GetTemplateChild("PART_Seg1Count") as System.Windows.Controls.TextBlock ?? new(),
                GetTemplateChild("PART_Seg2Count") as System.Windows.Controls.TextBlock ?? new(),
                GetTemplateChild("PART_Seg3Count") as System.Windows.Controls.TextBlock ?? new(),
                GetTemplateChild("PART_Seg4Count") as System.Windows.Controls.TextBlock ?? new(),
                GetTemplateChild("PART_Seg5Count") as System.Windows.Controls.TextBlock ?? new(),
            };

            // Attacher les handlers à la nouvelle TextBox
            if (_textBox != null)
            {
                _textBox.TextChanged      += TextBox_TextChanged;
                _textBox.PreviewTextInput += TextBox_PreviewTextInput;
                _textBox.PreviewKeyDown   += TextBox_PreviewKeyDown;
                DataObject.AddPastingHandler(_textBox, TextBox_Pasting);
            }

            // Initialiser l'affichage
            UpdateSegmentBars(string.Empty);

            // Restituer la valeur courante si déjà définie
            if (GuidValue.HasValue)
                SyncTextFromGuidValue(GuidValue);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  GESTION DE LA SAISIE
        // ═══════════════════════════════════════════════════════════════════

        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsHexChar(e.Text);
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                e.Handled = true;
                return;
            }
            if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
            {
                _textBox?.SelectAll();
                e.Handled = true;
            }
        }

        private void TextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text  = (string)e.DataObject.GetData(typeof(string));
                string? clean = CleanAndFormat(text);
                e.CancelCommand();
                if (clean != null) SetFormattedText(clean);
            }
            else
            {
                e.CancelCommand();
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextChanged || _textBox == null) return;

            string raw   = _textBox.Text;
            int caret    = _textBox.CaretIndex;
            string fmt   = AutoFormat(raw, ref caret);

            if (fmt != raw)
            {
                _suppressTextChanged = true;
                _textBox.Text        = fmt;
                _textBox.CaretIndex  = Math.Min(caret, fmt.Length);
                _suppressTextChanged = false;
            }

            if (_placeholder != null)
                _placeholder.Visibility = string.IsNullOrEmpty(_textBox.Text)
                    ? Visibility.Visible : Visibility.Collapsed;

            UpdateUI(fmt);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  FORMATAGE
        // ═══════════════════════════════════════════════════════════════════

        private static string AutoFormat(string input, ref int caret)
        {
            string hex = new string(input.Where(c => IsHexChar(c.ToString())).ToArray());
            if (hex.Length > 32) hex = hex[..32];

            int hexCaret = CountHexCharsBeforePosition(input, caret);
            var sb = new System.Text.StringBuilder();
            int pos = 0;

            foreach (char c in hex)
            {
                if (pos == 8 || pos == 12 || pos == 16 || pos == 20) sb.Append('-');
                sb.Append(c);
                pos++;
            }

            string result = sb.ToString();
            caret = HexIndexToFormattedIndex(result, hexCaret);
            return result;
        }

        private static string? CleanAndFormat(string input)
        {
            if (Guid.TryParse(input, out Guid g)) return g.ToString().ToUpper();
            string hex = new string(input.Where(c => IsHexChar(c.ToString())).ToArray());
            if (hex.Length == 0) return null;
            if (hex.Length > 32) hex = hex[..32];
            int dummy = 0;
            return AutoFormat(hex, ref dummy);
        }

        private void SetFormattedText(string text)
        {
            if (_textBox == null) return;
            _suppressTextChanged = true;
            _textBox.Text        = text.ToUpper();
            _textBox.CaretIndex  = text.Length;
            _suppressTextChanged = false;

            if (_placeholder != null)
                _placeholder.Visibility = string.IsNullOrEmpty(text)
                    ? Visibility.Visible : Visibility.Collapsed;

            UpdateUI(_textBox.Text);
        }

        private void SyncTextFromGuidValue(Guid? guid)
        {
            // Appelé seulement si le template est déjà appliqué
            if (_textBox == null) return;
            SetFormattedText(guid.HasValue ? guid.Value.ToString().ToUpper() : string.Empty);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  MISE À JOUR DE L'UI
        // ═══════════════════════════════════════════════════════════════════

        private void UpdateUI(string text)
        {
            UpdateSegmentBars(text);
            UpdateStatus(text);
            UpdateGuidValue(text);
        }

        private void UpdateSegmentBars(string text)
        {
            string hex = new string(text.Where(c => IsHexChar(c.ToString())).ToArray());
            int[] starts = { 0, 8, 12, 16, 20 };

            for (int i = 0; i < 5; i++)
            {
                int filled = Math.Max(0, Math.Min(SegmentLengths[i], hex.Length - starts[i]));
                double ratio = (double)filled / SegmentLengths[i];

                Color c     = SegmentColors[i];
                Color empty = Color.FromArgb(50,  c.R, c.G, c.B);
                Color fill  = Color.FromArgb(200, c.R, c.G, c.B);

                _segBars[i].Background = new LinearGradientBrush(
                    new GradientStopCollection
                    {
                        new GradientStop(fill,  0),
                        new GradientStop(fill,  ratio),
                        new GradientStop(empty, ratio),
                        new GradientStop(empty, 1),
                    },
                    new Point(0, 0), new Point(1, 0));

                _segCounts[i].Text       = $"{filled}/{SegmentLengths[i]}";
                _segCounts[i].Opacity    = filled == SegmentLengths[i] ? 0.9 : 0.5;
                _segCounts[i].FontWeight = filled == SegmentLengths[i] ? FontWeights.SemiBold : FontWeights.Normal;
            }
        }

        private void UpdateStatus(string text)
        {
            if (_status == null) return;

            if (string.IsNullOrWhiteSpace(text))
            {
                _status.Text       = "Entrez un GUID";
                _status.Foreground = Brushes("#8888AA");
                return;
            }

            string hex       = new string(text.Where(c => IsHexChar(c.ToString())).ToArray());
            int remaining    = 32 - hex.Length;

            if (Guid.TryParse(text, out _))
            {
                _status.Text       = "✓  GUID valide";
                _status.Foreground = Brushes("#3DDC84");
            }
            else if (remaining > 0)
            {
                _status.Text       = $"{remaining} caractère{(remaining > 1 ? "s" : "")} restant{(remaining > 1 ? "s" : "")}";
                _status.Foreground = Brushes("#8888AA");
            }
            else
            {
                _status.Text       = "✗  Format invalide";
                _status.Foreground = Brushes("#FF5370");
            }
        }

        private void UpdateGuidValue(string text)
        {
            if (Guid.TryParse(text, out Guid g))
            {
                if (GuidValue != g) { GuidValue = g; GuidChanged?.Invoke(this, g); }
            }
            else
            {
                if (GuidValue != null) { GuidValue = null; GuidChanged?.Invoke(this, null); }
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  UTILITAIRES
        // ═══════════════════════════════════════════════════════════════════

        private static SolidColorBrush Brushes(string hex) =>
            new((Color)ColorConverter.ConvertFromString(hex));

        private static bool IsHexChar(string s) =>
            !string.IsNullOrEmpty(s) && "0123456789ABCDEFabcdef".Contains(s[0]);

        private static int CountHexCharsBeforePosition(string text, int pos)
        {
            int count = 0;
            for (int i = 0; i < Math.Min(pos, text.Length); i++)
                if (IsHexChar(text[i].ToString())) count++;
            return count;
        }

        private static int HexIndexToFormattedIndex(string formatted, int hexIndex)
        {
            int hex = 0;
            for (int i = 0; i < formatted.Length; i++)
            {
                if (IsHexChar(formatted[i].ToString()))
                {
                    if (hex == hexIndex) return i;
                    hex++;
                }
            }
            return formatted.Length;
        }
    }
}
