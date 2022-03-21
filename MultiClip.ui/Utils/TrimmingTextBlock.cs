using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MultiClip.UI
{
    /// <summary>
    /// A TextBlock like control that provides special text trimming logic
    /// designed for a file or folder path.
    /// <para>Based on Smorgg comment of http://www.codeproject.com/Tips/467054/WPF-PathTrimmingTextBlock.
    /// It is extended to dynamically make decision to trim either the end or the middle
    /// </para>
    /// </summary>
    public class TrimmingTextBlock : UserControl
    {
        TextBlock textBlock;

        public TrimmingTextBlock()
        {
            textBlock = new TextBlock();
            AddChild(textBlock);
        }

        protected override Size MeasureOverride(Size constraint)
        {
            if (constraint.Width == 0)
                return base.MeasureOverride(constraint);

            base.MeasureOverride(constraint);
            // This is where the control requests to be as large
            // as is needed while fitting within the given bounds
            var meas = TrimToFit(RawText, constraint);

            // Update the text
            textBlock.Text = meas.Item1;

            return meas.Item2;
        }

        /// <summary>
        /// Trims the given path until it fits within the given constraints.
        /// </summary>
        /// <param name="path">The path to trim.</param>
        /// <param name="constraint">The size constraint.</param>
        /// <returns>The trimmed path and its size.</returns>
        Tuple<string, Size> TrimToFit(string path, Size constraint)
        {
            if (path == null)
                path = "";

            // If the path does not need to be trimmed
            // then return immediately
            Size size = MeasureString(path);
            if (size.Width < constraint.Width)
            {
                return new Tuple<string, Size>(path, size);
            }

            bool trimMiddle = false;

            try
            {
                if (!path.HasInvalidPathCharacters())
                    trimMiddle = System.IO.Path.IsPathRooted(path);
            }
            catch { }

            // Do not perform trimming if the path is not valid
            // because the below algorithm will not work
            // if we cannot separate the filename from the directory
            string rightSide = null;
            string leftSide = null;

            if (!trimMiddle)
            {
                leftSide = path;
            }
            else
            {
                try
                {
                    rightSide = System.IO.Path.GetFileName(path) ?? "";
                    leftSide = System.IO.Path.GetDirectoryName(path) ?? "";
                    if (leftSide == null)
                    {
                        leftSide = path;
                        trimMiddle = false;
                    }
                }
                catch (Exception)
                {
                    return new Tuple<string, Size>(path, size);
                }
            }

            while (true)
            {
                if (trimMiddle)
                    path = $"{leftSide}...\\{rightSide}";
                else
                    path = leftSide + "...";

                size = MeasureString(path);

                if (size.Width <= constraint.Width)
                {
                    // If size is within constraints
                    // then stop trimming
                    break;
                }

                // Shorten the directory component of the path
                // and continue
                if (leftSide.Length > 0)
                    leftSide = leftSide.Substring(0, leftSide.Length - 1);

                if (trimMiddle)
                {
                    // If the directory component is completely gone
                    // then replace it with ellipses and stop
                    if (leftSide.Length == 0)
                    {
                        path = @"...\" + rightSide;
                        size = MeasureString(path);
                        break;
                    }
                }
                else
                {
                    if (leftSide.Length <= 5) //5 - something practical
                    {
                        path = leftSide + @"...\";
                        size = MeasureString(path);
                        break;
                    }
                }
            }

            return new Tuple<string, Size>(path, size);
        }

        /// <summary>
        /// Returns the size of the given string if it were to be rendered.
        /// </summary>
        /// <param name="str">The string to measure.</param>
        /// <returns>The size of the string.</returns>
        Size MeasureString(string str)
        {
            var typeFace = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
            var text = new FormattedText(str, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeFace, FontSize, Foreground);

            return new Size(text.Width, text.Height);
        }

        /// <summary>
        /// Gets or sets the path to display.
        /// The text that is actually displayed will be trimmed appropriately.
        /// </summary>
        public string RawText
        {
            get { return (string)GetValue(RawTextProperty); }
            set { SetValue(RawTextProperty, value); }
        }

        public static readonly DependencyProperty RawTextProperty = DependencyProperty.Register("RawText", typeof(string), typeof(TrimmingTextBlock), new UIPropertyMetadata("", OnRawTextChanged));

        static void OnRawTextChanged(DependencyObject o, DependencyPropertyChangedEventArgs args)
        {
            TrimmingTextBlock @this = (TrimmingTextBlock)o;

            // This element will be re-measured
            // The text will be updated during that process
            @this.InvalidateMeasure();
        }
    }
}