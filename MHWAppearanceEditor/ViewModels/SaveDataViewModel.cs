﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Cirilla.Core.Models;
using DynamicData;
using DynamicData.Binding;
using MHWAppearanceEditor.Extensions;
using MHWAppearanceEditor.Helpers;
using MHWAppearanceEditor.Models;
using MHWAppearanceEditor.ViewModels.Tabs;
using MHWAppearanceEditor.Views;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;

namespace MHWAppearanceEditor.ViewModels
{
    public class SaveDataViewModel : ViewModelBase
    {
        private static readonly ILogger CtxLog = Log.ForContext<SaveDataViewModel>();

        public ReactiveCommand<Unit, Unit> OpenNewCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenHelpWindowCommand { get; }

        public SteamAccount SteamAccount { get; set; }
        [Reactive] public bool IsLoading { get; private set; } = true;

        private readonly SourceList<object> tabs = new SourceList<object>();
        public IObservableCollection<object> TabsBinding { get; } = new ObservableCollectionExtended<object>();

        private SaveData saveData;

        public SaveDataViewModel(string saveDataPath)
        {
            tabs.Connect()
                .Bind(TabsBinding)
                .Subscribe();

            OpenNewCommand = ReactiveCommand.Create(OpenNew);
            SaveCommand = ReactiveCommand.CreateFromTask(ShowSaveDialog);
            OpenHelpWindowCommand = ReactiveCommand.Create(OpenHelpWindow);

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
                    tabs.Edit(lst =>
                    {
                        lst.Clear();
                        lst.Add(new SaveDataInfoViewModel(saveData));
                        lst.AddRange(saveData.SaveSlots.Select(x => new SaveSlotViewModel(x)));
                    });
                    IsLoading = false;
                });
            }
            catch (Exception ex)
            {
                CtxLog.Error(ex, ex.Message);
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

        private async Task ShowSaveDialog()
        {
            SaveFileDialog sfd = new SaveFileDialog();
            string initialPath = SteamAccount != null ? SteamUtility.GetMhwSaveDir(SteamAccount) : SteamUtility.GetMhwSaveDir();

            if (initialPath != null)
            {
                sfd.Directory = initialPath;
                sfd.InitialFileName = Path.Combine(initialPath, "SAVEDATA1000");
            }

            string fileName = await sfd.ShowAsync();

            if (fileName == null)
            {
                CtxLog.Information("Saving cancelled");
            }
            else
            {
                MainWindowViewModel.Instance.ShowPopup("Saving...", false);

                try
                {
                    await Task.Run(() => saveData.Save(fileName));
                    MainWindowViewModel.Instance.ShowPopup($"Saved SaveData to '{fileName}'");
                }
                catch (Exception ex)
                {
                    CtxLog.Error(ex, ex.Message);
                    MainWindowViewModel.Instance.ShowPopup(ex.Message);
                }
            }
        }
    }
}
