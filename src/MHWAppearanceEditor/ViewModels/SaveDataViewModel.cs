﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Cirilla.Core.Models;
using DynamicData;
using DynamicData.Binding;
using MHWAppearanceEditor.Extensions;
using MHWAppearanceEditor.Helpers;
using MHWAppearanceEditor.Models;
using MHWAppearanceEditor.Services;
using MHWAppearanceEditor.ViewModels.Tabs;
using MHWAppearanceEditor.Views;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Serilog;
using Splat;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;

namespace MHWAppearanceEditor.ViewModels
{
    public class SaveDataViewModel : ViewModelBase
    {
        private static readonly Serilog.ILogger log = Log.ForContext<SaveDataViewModel>();
        private static readonly SolidColorBrush redColorBrush = new SolidColorBrush(Colors.Orange);
        private static readonly SolidColorBrush greenColorBrush = new SolidColorBrush(Colors.LimeGreen);

        public ReactiveCommand<Unit, Unit> OpenNewCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenHelpWindowCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenSettingsWindowCommand { get; }

        public SteamAccount? SteamAccount { get; set; }
        [Reactive] public bool IsLoading { get; private set; } = true;

        public ObservableCollection<object> Tabs { get; } = new ObservableCollection<object>();

        private SaveData? saveData;
        private readonly BackupService backup = Locator.Current.GetService<BackupService>()!;

        public SaveDataViewModel(string saveDataPath)
        {
            OpenNewCommand = ReactiveCommand.Create(OpenNew);
            SaveCommand = ReactiveCommand.CreateFromTask(ShowSaveDialog);
            OpenHelpWindowCommand = ReactiveCommand.Create(OpenHelpWindow);
            OpenSettingsWindowCommand = ReactiveCommand.Create(OpenSettingsWindow);

            // Don't await and run on other thread because it is needed, just trust me
            Task.Run(() => LoadSaveData(saveDataPath));
        }

        private async Task LoadSaveData(string saveDataPath)
        {
            try
            {
                saveData = await Task.Run(() => new SaveData(saveDataPath));

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Tabs.Clear();
                    Tabs.Add(new SaveDataInfoViewModel(saveData));
                    Tabs.AddRange(saveData.SaveSlots.Select(x => new SaveSlotViewModel(x)));
                });

                IsLoading = false;
            }
            catch (Exception ex)
            {
                log.Error(ex, ex.Message);
                MainWindowViewModel.Instance.SetActiveViewModel(new ExceptionViewModel(ex));
            }
        }

        private void OpenNew()
        {
            // TODO: Check for unsaved changes
            MainWindowViewModel.Instance.ShowStartScreen();
        }

        private void OpenHelpWindow()
        {
            new HelpWindow().Show();
        }

        private void OpenSettingsWindow()
        {
            new SettingsWindow().Show();
        }
        private async Task ShowSaveDialog()
        {
            if (saveData == null) return;

            SaveFileDialog sfd = new SaveFileDialog();
            string? initialPath = SteamAccount != null ? SteamUtility.GetMhwSaveDir(SteamAccount) : SteamUtility.GetMhwSaveDir();

            if (initialPath != null)
            {
                sfd.Directory = initialPath;
                sfd.InitialFileName = Path.Combine(initialPath, "SAVEDATA1000");
            }

            string fileName = await sfd.ShowAsync();

            if (fileName == null)
            {
                log.Information("Saving cancelled");
            }
            else
            {
                MainWindowViewModel.Instance.ShowPopup("Creating SaveData backup...", false);
                backup.CreateBackup(saveData);

                MainWindowViewModel.Instance.ShowPopup("Saving...", false);

                try
                {
                    await Task.Run(() => saveData.Save(fileName));
                    MainWindowViewModel.Instance.ShowPopup($"Saved SaveData to '{fileName}'");
                }
                catch (Exception ex)
                {
                    log.Error(ex, ex.Message);
                    MainWindowViewModel.Instance.ShowPopup(ex.Message);
                }
            }
        }
    }
}
