using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Lithnet.Miiserver.Autosync.UI
{
    public partial class TimeSpanControl : UserControl
    {
        public TimeSpanControl()
        {
            this.InitializeComponent();
        }

        public TimeSpan Value
        {
            get => (TimeSpan)this.GetValue(ValueProperty);
            set => this.SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(TimeSpan), typeof(TimeSpanControl),
                new UIPropertyMetadata(TimeSpan.MinValue, new PropertyChangedCallback(OnValueChanged)));

        private static void OnValueChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            TimeSpanControl control = obj as TimeSpanControl;

            if (control == null)
            {
                return;
            }

            TimeSpan ts = (TimeSpan)e.NewValue;
            
            control.ValidateNewValue(ts);
        }

        private static void OnMinMaxValueChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            TimeSpanControl control = obj as TimeSpanControl;

            if (control == null)
            {
                return;
            }

            control.ValidateNewValue(control.Value);
        }

        private bool updating;

        private void ValidateNewValue(TimeSpan ts)
        {
            if (ts > this.MaximumTimeSpan)
            {
                this.Value = this.MaximumTimeSpan;
                ts = this.MaximumTimeSpan;
            }

            if (ts < this.MinimumTimeSpan)
            {
                this.Value = this.MinimumTimeSpan;
                ts = this.MinimumTimeSpan;
            }

            try
            {
                this.updating = true;
                this.Days = ts.Days;
                this.Hours = ts.Hours;
                this.Minutes = ts.Minutes;
                this.Seconds = ts.Seconds;
                this.Milliseconds = ts.Milliseconds;
            }
            finally
            {
                this.updating = false;
            }
        }

        public TimeSpan MinimumTimeSpan
        {
            get => (TimeSpan)this.GetValue(MinimumTimeSpanProperty);
            set => this.SetValue(MinimumTimeSpanProperty, value);
        }

        public static readonly DependencyProperty MinimumTimeSpanProperty =
            DependencyProperty.Register("MinimumTimeSpan", typeof(TimeSpan), typeof(TimeSpanControl),
                new UIPropertyMetadata(TimeSpan.MinValue, new PropertyChangedCallback(OnMinMaxValueChanged)));

        public TimeSpan MaximumTimeSpan
        {
            get => (TimeSpan)this.GetValue(MaximumTimeSpanProperty);
            set => this.SetValue(MaximumTimeSpanProperty, value);
        }

        public static readonly DependencyProperty MaximumTimeSpanProperty =
            DependencyProperty.Register("MaximumTimeSpan", typeof(TimeSpan), typeof(TimeSpanControl),
                new UIPropertyMetadata(TimeSpan.MaxValue, new PropertyChangedCallback(OnMinMaxValueChanged)));
        
        public int Days
        {
            get => (int)this.GetValue(DaysProperty);
            set => this.SetValue(DaysProperty, value);
        }

        public static readonly DependencyProperty DaysProperty =
            DependencyProperty.Register("Days", typeof(int), typeof(TimeSpanControl),
                new UIPropertyMetadata(0, new PropertyChangedCallback(OnTimeChanged)));
        
        public int Hours
        {
            get => (int)this.GetValue(HoursProperty);
            set => this.SetValue(HoursProperty, value);
        }

        public static readonly DependencyProperty HoursProperty =
            DependencyProperty.Register("Hours", typeof(int), typeof(TimeSpanControl),
                new UIPropertyMetadata(0, new PropertyChangedCallback(OnTimeChanged)));

        public int Minutes
        {
            get => (int)this.GetValue(MinutesProperty);
            set => this.SetValue(MinutesProperty, value);
        }

        public static readonly DependencyProperty MinutesProperty =
            DependencyProperty.Register("Minutes", typeof(int), typeof(TimeSpanControl),
                new UIPropertyMetadata(0, new PropertyChangedCallback(OnTimeChanged)));

        public int Seconds
        {
            get => (int)this.GetValue(SecondsProperty);
            set => this.SetValue(SecondsProperty, value);
        }

        public static readonly DependencyProperty SecondsProperty =
            DependencyProperty.Register("Seconds", typeof(int), typeof(TimeSpanControl),
                new UIPropertyMetadata(0, new PropertyChangedCallback(OnTimeChanged)));

        public int Milliseconds
        {
            get => (int)this.GetValue(MillisecondsProperty);
            set => this.SetValue(MillisecondsProperty, value);
        }

        public static readonly DependencyProperty MillisecondsProperty =
            DependencyProperty.Register("Milliseconds", typeof(int), typeof(TimeSpanControl),
                new UIPropertyMetadata(0, new PropertyChangedCallback(OnTimeChanged)));

        private static void OnTimeChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            TimeSpanControl control = obj as TimeSpanControl;

            if (control == null)
            {
                return;
            }

            if (control.updating)
            {
                return;
            }

            TimeSpan ts = new TimeSpan(control.Days, control.Hours, control.Minutes, control.Seconds, control.Milliseconds);
            
            if (control.Value != ts)
            {
                control.Value = ts;
            }
        }
    }
}
