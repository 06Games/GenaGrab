﻿using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace GeneaGrab.Views
{
    // geneagrab:registry?url=https://www.geneanet.org/archives/registres/view/17000/10
    public sealed partial class Registry : Page, INotifyPropertyChanged
    {
        public Registry() => InitializeComponent();

        public static RegistryInfo Info { get; set; }
        async protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var (success, inRam) = await LoadRegistry(e.Parameter);
            if (success)
            {
                RefreshView();

                if (!inRam)
                {
                    Pages.Clear();
                    foreach (var page in Info.Registry.Pages) Pages.Add(new PageList { Number = page.Number, Page = page });
                    _ = Task.Run(async () =>
                    {
                        for (int i = 0; i < Pages.Count; i++)
                        {
                            var page = Pages[i];
                            var img = await Info.Provider.API.GetTile(Info.Registry, page.Page, 0);
                            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                            {
                                page.Thumbnail = img.Image.ToImageSource();
                                Pages[i] = page;
                            });
                        }
                    }).ContinueWith((task) => throw task.Exception, TaskContinuationOptions.OnlyOnFaulted);
                }

                await Info.Provider.API.GetTile(Info.Registry, Info.Page, 1);
                RefreshView();
            }
            else await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                if (Frame.CanGoBack) Frame.GoBack();
                else Frame.Navigate(typeof(MainPage));
            });
        }

        public ObservableCollection<PageList> Pages = new ObservableCollection<PageList>();
        public async Task<(bool success, bool inRam)> LoadRegistry(object Parameter)
        {
            var inRam = false;

            if (Parameter is RegistryInfo) Info = Parameter as RegistryInfo;
            else if (Parameter is Dictionary<string, string>)
            {
                var param = Parameter as Dictionary<string, string>;
                if (param.ContainsKey("url") && Uri.TryCreate(param.GetValueOrDefault("url"), UriKind.Absolute, out var uri)) Info = await TryGetFromProviders(uri);


            }
            else if (Parameter is Uri) Info = await TryGetFromProviders(Parameter as Uri);
            else inRam = true;

            async Task<RegistryInfo> TryGetFromProviders(Uri uri)
            {
                foreach (var provider in Data.Providers.Values)
                    if (provider.API.CheckURL(uri)) return await provider.API.Infos(uri);
                return null;
            }

            return (Info != null, inRam);
        }

        private async void ChangePage(object sender, ItemClickEventArgs e) => await ChangePage(e.ClickedItem as PageList);
        public async Task ChangePage(PageList page)
        {
            Info.PageNumber = page.Number;
            await Info.Provider.API.GetTile(Info.Registry, page.Page, 1);
            RefreshView();
        }
        public void RefreshView()
        {
            Info_Location.Text = Info.Location.ToString();
            Info_Registry.Text = Info.Registry.ToString();
            Info_RegistryID.Text = Info.Registry.ID;

            image.Source = Info.Page.Image.ToImageSource();
            PageList.SelectedIndex = Info.Page.Number - 1;
            OnPropertyChanged(nameof(image));
            App.SaveData();
        }

        private async void Download(object sender, Windows.UI.Xaml.RoutedEventArgs e) => await Download();
        async Task<string> Download()
        {
            var page = await Info.Provider.API.Download(Info.Registry, Info.Page);
            RefreshView();
            return await Data.SaveImage(Info.Registry, page);
        }
        private async void OpenFolder(object sender, Windows.UI.Xaml.RoutedEventArgs e) => System.Diagnostics.Process.Start("explorer.exe", $"/select, \"{await Download()}\"");


        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class PageList
    {
        public int Number { get; set; }
        public BitmapImage Thumbnail { get; set; }
        public RPage Page { get; set; }
    }
}
