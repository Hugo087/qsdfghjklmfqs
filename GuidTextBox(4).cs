using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GuidInput
{
    /// <summary>
    /// TextBox spécialisé pour la saisie de GUID.
    /// Formatte automatiquement au format : xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
    /// Filtre les caractères non hexadécimaux.
    /// Expose une propriété GuidValue (Guid?) et IsValidGuid (bool).
    /// </summary>
    [TemplatePart(Name = "PART_ContentHost", Type = typeof(ScrollViewer))]
    public class GuidTextBox : TextBox
    {
        // ──────────────────────────────────────────────────────────────────────
        // Groupes du format GUID : 8-4-4-4-12
        // ──────────────────────────────────────────────────────────────────────
        private static readonly int[] GuidGroups = { 8, 4, 4, 4, 12 };

        // Verrou anti-récursion lors des mises à jour programmatiques du texte
        private bool _isUpdating;

        // ══════════════════════════════════════════════════════════════════════
        // Dependency Properties
        // ══════════════════════════════════════════════════════════════════════

        #region GuidValue

        public static readonly DependencyProperty GuidValueProperty =
            DependencyProperty.Register(
                nameof(GuidValue),
                typeof(Guid?),
                typeof(GuidTextBox),
                new FrameworkPropertyMetadata(
                    defaultValue: null,
                    flags: FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    propertyChangedCallback: OnGuidValuePropertyChanged));

        /// <summary>Valeur GUID parsée. Null si le GUID est incomplet ou invalide.</summary>
        public Guid? GuidValue
        {
            get => (Guid?)GetValue(GuidValueProperty);
            set => SetValue(GuidValueProperty, value);
        }

        private static void OnGuidValuePropertyChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (GuidTextBox)d;
            if (ctrl._isUpdating) return;

            ctrl._isUpdating = true;
            try
            {
                if (e.NewValue is Guid guid)
                {
                    ctrl.Text = guid.ToString("D");
                    ctrl.IsValidGuid = true;
                }
                else
                {
                    ctrl.Text = string.Empty;
                    ctrl.IsValidGuid = false;
                }
            }
            finally
            {
                ctrl._isUpdating = false;
            }
        }

        #endregion

        #region IsValidGuid (read-only)

        private static readonly DependencyPropertyKey IsValidGuidPropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(IsValidGuid),
                typeof(bool),
                typeof(GuidTextBox),
                new PropertyMetadata(false));

        public static readonly DependencyProperty IsValidGuidProperty =
            IsValidGuidPropertyKey.DependencyProperty;

        /// <summary>True uniquement lorsque les 32 caractères hexadécimaux ont été saisis.</summary>
        public bool IsValidGuid
        {
            get => (bool)GetValue(IsValidGuidProperty);
            private set => SetValue(IsValidGuidPropertyKey, value);
        }

        #endregion

        #region PlaceholderText

        public static readonly DependencyProperty PlaceholderTextProperty =
            DependencyProperty.Register(
                nameof(PlaceholderText),
                typeof(string),
                typeof(GuidTextBox),
                new PropertyMetadata("xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"));

        /// <summary>Texte affiché quand le champ est vide.</summary>
        public string PlaceholderText
        {
            get => (string)GetValue(PlaceholderTextProperty);
            set => SetValue(PlaceholderTextProperty, value);
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Constructeurs
        // ══════════════════════════════════════════════════════════════════════

        static GuidTextBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(GuidTextBox),
                new FrameworkPropertyMetadata(typeof(GuidTextBox)));
        }

        public GuidTextBox()
        {
            // Police à chasse fixe pour que le masque soit lisible
            FontFamily = new FontFamily("Consolas, Courier New");
            MaxLength = 36; // 32 hex + 4 tirets
            DataObject.AddPastingHandler(this, OnPaste);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Overrides TextBox
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Filtre les caractères en entrée : seuls les hexadécimaux sont acceptés.</summary>
        protected override void OnPreviewTextInput(TextCompositionEventArgs e)
        {
            foreach (char c in e.Text)
            {
                if (!IsHexChar(c))
                {
                    e.Handled = true;
                    return;
                }
            }
            base.OnPreviewTextInput(e);
        }

        /// <summary>
        /// Gère les cas spéciaux de Backspace/Suppr lorsque le curseur
        /// est adjacent à un tiret auto-inséré.
        /// </summary>
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Back && SelectionLength == 0 && CaretIndex > 0)
            {
                // Curseur juste après un tiret → supprimer le dernier caractère hex avant le tiret
                if (CaretIndex <= Text.Length && Text[CaretIndex - 1] == '-')
                {
                    e.Handled = true;
                    int hexBeforeDash = CountHexChars(Text, CaretIndex - 1);
                    if (hexBeforeDash > 0)
                        ApplyHexDeletion(hexBeforeDash - 1);
                    return;
                }
            }
            else if (e.Key == Key.Delete && SelectionLength == 0 && CaretIndex < Text.Length)
            {
                // Curseur juste avant un tiret → supprimer le premier caractère hex après le tiret
                if (Text[CaretIndex] == '-')
                {
                    e.Handled = true;
                    int hexBeforeDash = CountHexChars(Text, CaretIndex);
                    ApplyHexDeletion(hexBeforeDash);
                    return;
                }
            }

            base.OnPreviewKeyDown(e);
        }

        /// <summary>Reformate le texte dès qu'il change.</summary>
        protected override void OnTextChanged(TextChangedEventArgs e)
        {
            if (_isUpdating)
            {
                // Appel ré-entrant provenant d'une mise à jour programmatique :
                // on propage l'événement sans reformater.
                base.OnTextChanged(e);
                return;
            }

            _isUpdating = true;
            try
            {
                int oldCaret = CaretIndex;
                string oldText = Text ?? string.Empty;

                string hexOnly = ExtractHexChars(oldText);
                if (hexOnly.Length > 32)
                    hexOnly = hexOnly.Substring(0, 32);

                string formatted = FormatAsGuid(hexOnly);

                if (formatted != oldText)
                {
                    // Mémoriser combien de hex étaient avant le curseur
                    int hexBeforeCaret = CountHexChars(oldText, oldCaret);

                    // Ce Set déclenche un OnTextChanged ré-entrant, géré par le flag
                    Text = formatted;
                    CaretIndex = FindCaretPosition(formatted, hexBeforeCaret);

                    // L'événement "propre" (avec le texte formaté) est émis
                    // par l'appel ré-entrant ci-dessus → on ne rappelle pas base ici.
                }
                else
                {
                    base.OnTextChanged(e);
                }

                UpdateGuidValue(hexOnly);
            }
            finally
            {
                _isUpdating = false;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Logique interne
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Supprime le nième caractère hex (0-indexé) de la chaîne courante,
        /// reformate, et repositionne le curseur.
        /// </summary>
        private void ApplyHexDeletion(int hexIndex)
        {
            _isUpdating = true;
            try
            {
                string hexOnly = ExtractHexChars(Text);
                if (hexIndex < 0 || hexIndex >= hexOnly.Length) return;

                hexOnly = hexOnly.Remove(hexIndex, 1);
                string formatted = FormatAsGuid(hexOnly);
                Text = formatted;
                CaretIndex = FindCaretPosition(formatted, hexIndex);
                UpdateGuidValue(hexOnly);
            }
            finally
            {
                _isUpdating = false;
            }
        }

        /// <summary>Met à jour GuidValue et IsValidGuid à partir de la chaîne hex pure.</summary>
        private void UpdateGuidValue(string hexOnly)
        {
            if (hexOnly.Length == 32)
            {
                // Un GUID est valide dès lors qu'il contient 32 caractères hexadécimaux
                string guidStr = string.Format(
                    "{0}-{1}-{2}-{3}-{4}",
                    hexOnly.Substring(0, 8),
                    hexOnly.Substring(8, 4),
                    hexOnly.Substring(12, 4),
                    hexOnly.Substring(16, 4),
                    hexOnly.Substring(20, 12));

                if (Guid.TryParse(guidStr, out Guid guid))
                {
                    GuidValue = guid;
                    IsValidGuid = true;
                    return;
                }
            }

            GuidValue = null;
            IsValidGuid = false;
        }

        // ══════════════════════════════════════════════════════════════════════
        // Gestion du collage (Ctrl+V)
        // ══════════════════════════════════════════════════════════════════════

        private void OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.UnicodeText))
            {
                e.CancelCommand();
                return;
            }

            string pasted = (string)e.DataObject.GetData(DataFormats.UnicodeText);

            // Cas idéal : la chaîne collée est déjà un GUID valide → l'appliquer directement
            if (Guid.TryParse(pasted, out Guid parsed))
            {
                e.CancelCommand();
                _isUpdating = true;
                try
                {
                    Text = parsed.ToString("D");
                    CaretIndex = Text.Length;
                    GuidValue = parsed;
                    IsValidGuid = true;
                }
                finally
                {
                    _isUpdating = false;
                }
                return;
            }

            // Sinon : filtrer pour ne garder que les hex et laisser OnTextChanged formater
            string hexOnly = ExtractHexChars(pasted);
            if (string.IsNullOrEmpty(hexOnly))
            {
                e.CancelCommand();
                return;
            }

            e.DataObject = new DataObject(DataFormats.UnicodeText, hexOnly);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Helpers statiques
        // ══════════════════════════════════════════════════════════════════════

        private static bool IsHexChar(char c)
            => (c >= '0' && c <= '9')
            || (c >= 'a' && c <= 'f')
            || (c >= 'A' && c <= 'F');

        /// <summary>Extrait uniquement les caractères hexadécimaux d'une chaîne.</summary>
        private static string ExtractHexChars(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (IsHexChar(c)) sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>Compte les caractères hexadécimaux jusqu'à l'index spécifié (exclu).</summary>
        private static int CountHexChars(string text, int upToIndex)
        {
            int count = 0;
            int limit = Math.Min(upToIndex, text?.Length ?? 0);
            for (int i = 0; i < limit; i++)
            {
                if (IsHexChar(text[i])) count++;
            }
            return count;
        }

        /// <summary>
        /// Reconstruit la chaîne formatée xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
        /// à partir d'une chaîne de caractères hex purs (sans tirets).
        /// </summary>
        private static string FormatAsGuid(string hexOnly)
        {
            if (string.IsNullOrEmpty(hexOnly)) return string.Empty;

            var sb = new StringBuilder(36);
            int idx = 0;

            for (int g = 0; g < GuidGroups.Length && idx < hexOnly.Length; g++)
            {
                if (g > 0) sb.Append('-');
                int take = Math.Min(GuidGroups[g], hexOnly.Length - idx);
                sb.Append(hexOnly, idx, take);
                idx += take;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Trouve la position du curseur dans la chaîne formatée
        /// après que <paramref name="hexCount"/> caractères hex ont été traités.
        /// Saute automatiquement les tirets en fin de groupe.
        /// </summary>
        private static int FindCaretPosition(string formatted, int hexCount)
        {
            if (hexCount == 0) return 0;
            if (string.IsNullOrEmpty(formatted)) return 0;

            int count = 0;
            for (int i = 0; i < formatted.Length; i++)
            {
                if (IsHexChar(formatted[i]))
                {
                    count++;
                    if (count == hexCount)
                    {
                        int pos = i + 1;
                        // Si on est exactement à la limite d'un groupe, passer le tiret
                        if (pos < formatted.Length && formatted[pos] == '-')
                            pos++;
                        return pos;
                    }
                }
            }
            return formatted.Length;
        }
    }
}
