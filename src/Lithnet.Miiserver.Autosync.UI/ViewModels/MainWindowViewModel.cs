using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Lithnet.Common.ObjectModel;
using Lithnet.Common.Presentation;
using MahApps.Metro.Controls;
using Microsoft.Win32;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
{

    internal class MainWindowViewModel : ViewModelBase
    {
        private bool confirmedCloseOnDirtyViewModel;

        public ConfigFileViewModel ConfigFile { get; set; }

        public string DisplayName => "Lithnet AutoSync for Microsoft Identity Manager" + (this.ViewModelIsDirty ? "*" : string.Empty);

        private bool ViewModelIsDirty { get; set; }

        private List<Type> ignoreViewModelChanges;

        public MainWindowViewModel()
        {
            UINotifyPropertyChanges.BeginIgnoreAllChanges();
            this.PopulateIgnoreViewModelChanges();
            this.AddDependentPropertyNotification("ViewModelIsDirty", "DisplayName");

            this.IgnorePropertyHasChanged.Add("DisplayName");
            this.IgnorePropertyHasChanged.Add("ChildNodes");
            this.IgnorePropertyHasChanged.Add("ViewModelIsDirty");

            this.Commands.AddItem("Reload", x => this.Reload());
            this.Commands.AddItem("Save", x => this.Save(), x => this.CanSave());
            this.Commands.AddItem("Export", x => this.Export(), x => this.CanExport());
            this.Commands.AddItem("Close", x => this.Close());
            this.Commands.AddItem("Import", x => this.Import());

            ViewModelBase.ViewModelChanged += this.ViewModelBase_ViewModelChanged;
            Application.Current.MainWindow.Closing += new CancelEventHandler(this.MainWindow_Closing);
            UINotifyPropertyChanges.EndIgnoreAllChanges();
        }

        private void PopulateIgnoreViewModelChanges()
        {
            this.ignoreViewModelChanges = new List<Type>();
        }

        private void Reload()
        {
            this.UpdateFocusedBindings();

            if (this.ViewModelIsDirty)
            {
                if (MessageBox.Show("There are unsaved changes. Are you sure you want to reload the config?", "Unsaved Changes", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                {
                    return;
                }
            }

            this.ResetConfigViewModel();
        }

        private void ResetConfigViewModel()
        {
            ConfigClient c = new ConfigClient();
            ConfigFile file = c.GetConfig();

            this.ConfigFile = new ConfigFileViewModel(file);
        }

        private void Import()
        {
            this.UpdateFocusedBindings();

            if (this.ViewModelIsDirty)
            {
                if (MessageBox.Show("There are unsaved changes. Do you want to continue?", "Unsaved Changes", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                {
                    return;
                }
            }

            OpenFileDialog dialog = new OpenFileDialog();
            dialog.DefaultExt = ".xml";
            dialog.Filter = "Configuration Files (.xml)|*.xml|All Files|*.*";
            dialog.CheckFileExists = true;

            bool? result = dialog.ShowDialog();

            if (result != true)
            {
                return;
            }

            if (!System.IO.File.Exists(dialog.FileName))
            {
                return;
            }

            try
            {
                AutoSync.ConfigFile f;
                try
                {
                    f = AutoSync.ConfigFile.Load(dialog.FileName);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex);
                    MessageBox.Show($"Could not open the file\n\n{ex.Message}", "File Open", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                try
                {
                    ConfigClient c = new ConfigClient();
                    c.PutConfig(f);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex);
                    MessageBox.Show($"Could not import the file\n\n{ex.Message}", "File import operation", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            finally
            {
                this.ResetConfigViewModel();
            }

            this.ViewModelIsDirty = false;
        }
        
        private void Save()
        {
            this.UpdateFocusedBindings();

            try
            {
                ConfigClient c = new ConfigClient();
                c.PutConfig(this.ConfigFile.Model);
                this.ViewModelIsDirty = false;

                foreach (MAConfigParametersViewModel p in this.ConfigFile.ManagementAgents)
                {
                    p.IsNew = false;
                }

                //this.ResetConfigViewModel();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                MessageBox.Show($"Could not save the configuration\n\n{ex.Message}", "Save operation", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanSave()
        {
            return true;
        }

        private bool CanExport()
        {
            return true;
        }

        private void Export()
        {
            this.UpdateFocusedBindings();

            if (this.HasErrors)
            {
                if (MessageBox.Show("There are one or more errors present in the configuration. Are you sure you want to save?", "Error", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.No)
                {
                    return;
                }
            }

            if (MessageBox.Show("Any passwords in the configuration will be exported in plain-text. Are you sure you want to continue?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.No)
            {
                return;
            }

            try
            {
                SaveFileDialog dialog = new SaveFileDialog();
                dialog.DefaultExt = ".xml";
                dialog.Filter = "Configuration file backups (*.xml)|*.xml|All Files|*.*";

                bool? result = dialog.ShowDialog();

                if (result == true)
                {
                    ProtectedString.EncryptOnWrite = false;
                    AutoSync.ConfigFile.Save(this.ConfigFile.Model, dialog.FileName);
                    this.ViewModelIsDirty = false;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                MessageBox.Show($"Could not save the file\n\n{ex.Message}", "Save File", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            this.UpdateFocusedBindings();

            if (this.ViewModelIsDirty && !this.confirmedCloseOnDirtyViewModel)
            {
                if (MessageBox.Show("There are unsaved changes. Are you sure you want to exit?", "Unsaved Changes", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                {
                    e.Cancel = true;
                }
            }
        }

        private void ViewModelBase_ViewModelChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender == this)
            {
                return;
            }

            if (this.ViewModelIsDirty)
            {
                return;
            }

            if (this.IgnorePropertyHasChanged.Contains(e.PropertyName))
            {
                return;
            }

            if (this.ignoreViewModelChanges.Contains(sender.GetType()))
            {
                return;
            }

            this.ViewModelIsDirty = true;
            this.RaisePropertyChanged("DisplayName");
        }

        private void Close()
        {
            this.UpdateFocusedBindings();

            if (this.ViewModelIsDirty)
            {
                if (MessageBox.Show("There are unsaved changes. Are you sure you want to exit?", "Unsaved Changes", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                {
                    return;
                }
            }

            this.confirmedCloseOnDirtyViewModel = true;
            Application.Current.Shutdown();
        }

        private void UpdateFocusedBindings()
        {
            object focusedItem = Keyboard.FocusedElement;

            if (focusedItem == null)
            {
                return;
            }

            BindingExpression expression = (focusedItem as TextBox)?.GetBindingExpression(TextBox.TextProperty);
            expression?.UpdateSource();

            expression = (focusedItem as ComboBox)?.GetBindingExpression(ComboBox.TextProperty);
            expression?.UpdateSource();

            expression = (focusedItem as PasswordBox)?.GetBindingExpression(PasswordBoxBindingHelper.PasswordProperty);
            expression?.UpdateSource();

            expression = (focusedItem as TimeSpanControl)?.GetBindingExpression(TimeSpanControl.ValueProperty);
            expression?.UpdateSource();

            expression = (focusedItem as DateTimePicker)?.GetBindingExpression(DateTimePicker.SelectedDateProperty);
            expression?.UpdateSource();

        }
    }
}
